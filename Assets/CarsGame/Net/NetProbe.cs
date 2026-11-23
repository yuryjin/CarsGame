using System;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.TLS;
using UnityEngine;

namespace RacingGame.Net
{
    /// <summary>
    /// Transport probe (Docs/AI/Multiplayer.md §8): connects to a WebSocket server, measures round-trip
    /// time and checks that relay messages from other clients arrive. Off unless asked for:
    /// WebGL — <c>?net=ws://host:port/path</c> (or <c>wss://</c>) in the page URL; elsewhere —
    /// <c>-netProbe ws://host:port/path</c> on the command line. Reports through <see cref="Status"/> and
    /// <c>[NetProbe]</c> log lines, which the browser test reads from the console.
    /// </summary>
    public sealed class NetProbe : MonoBehaviour
    {
        private const float PingInterval = 0.25f;
        private const float RelayInterval = 1f;
        private const float ReportInterval = 2f;

        public static string Status { get; private set; } = "off";

        private NetworkDriver driver;
        private NetworkConnection connection;
        private bool connected;
        private uint pingSeq;
        private float nextPing, nextRelay, nextReport;
        private int pongs, relaysIn, relaysOut, peers;
        private double rttSum, rttMin = double.MaxValue, rttMax;
        private bool reportedOk;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            string url = FindUrl();
            if (string.IsNullOrEmpty(url)) return;
            var go = new GameObject("NetProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<NetProbe>().Begin(url);
        }

        private static string FindUrl()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-netProbe") return args[i + 1];

            string page = Application.absoluteURL;
            int q = string.IsNullOrEmpty(page) ? -1 : page.IndexOf('?');
            if (q < 0) return null;
            int hash = page.IndexOf('#', q);
            string query = hash < 0 ? page.Substring(q + 1) : page.Substring(q + 1, hash - q - 1);
            foreach (string pair in query.Split('&'))
                if (pair.StartsWith("net=", StringComparison.Ordinal))
                    return Uri.UnescapeDataString(pair.Substring(4));
            return null;
        }

        private void Begin(string url)
        {
            Uri uri;
            try { uri = new Uri(url); }
            catch (UriFormatException) { Report($"bad url {url}"); return; }

            bool secure = uri.Scheme == "wss";
            if (!secure && uri.Scheme != "ws") { Report($"bad scheme {uri.Scheme}"); return; }
            ushort port = (ushort)(uri.IsDefaultPort || uri.Port <= 0 ? (secure ? 443 : 80) : uri.Port);

            connection = Connect(uri, port, out driver);
            Report($"connecting {url}");
        }

        internal static NetworkConnection Connect(Uri uri, ushort port, out NetworkDriver socket)
        {
            var settings = new NetworkSettings();
            try
            {
                settings.WithWebSocketParameters(uri.AbsolutePath);
                if (uri.Scheme == "wss") settings.WithSecureClientParameters(uri.Host);
                socket = NetworkDriver.Create(new WebSocketNetworkInterface(), settings);
            }
            finally { settings.Dispose(); }
            return socket.Connect(uri.Host, port);
        }

        private void OnDestroy()
        {
            if (driver.IsCreated) driver.Dispose();
        }

        private void Update()
        {
            if (!driver.IsCreated) return;
            driver.ScheduleUpdate().Complete();
            if (!connection.IsCreated) return;

            NetworkEvent.Type cmd;
            while ((cmd = connection.PopEvent(driver, out DataStreamReader stream)) != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Connect)
                {
                    connected = true;
                    Report("connected");
                }
                else if (cmd == NetworkEvent.Type.Data)
                {
                    Receive(ref stream);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    var reason = stream.Length > 0 ? (Unity.Networking.Transport.Error.DisconnectReason)stream.ReadByte() : default;
                    Report($"disconnected {reason} after {pongs} pongs");
                    connection = default;
                    connected = false;
                    return;
                }
            }

            if (!connected) return;
            float now = Time.realtimeSinceStartup;
            if (now >= nextPing)
            {
                nextPing = now + PingInterval;
                if (driver.BeginSend(connection, out DataStreamWriter w) == 0)
                {
                    NetProtocol.WritePing(ref w, ++pingSeq, Time.realtimeSinceStartupAsDouble);
                    driver.EndSend(w);
                }
            }
            if (now >= nextRelay)
            {
                nextRelay = now + RelayInterval;
                SendRelay();
            }
            if (now >= nextReport && pongs > 0)
            {
                nextReport = now + ReportInterval;
                Report(Summary());
                if (!reportedOk && pongs >= 20)
                {
                    reportedOk = true;
                    Report("OK " + Summary());
                }
            }
        }

        private void Receive(ref DataStreamReader stream)
        {
            byte type = stream.ReadByte();
            if (type == NetProtocol.Pong)
            {
                stream.ReadUInt();
                double sent = stream.ReadDouble();
                peers = stream.ReadUShort();
                double rtt = (Time.realtimeSinceStartupAsDouble - sent) * 1000.0;
                pongs++;
                rttSum += rtt;
                if (rtt < rttMin) rttMin = rtt;
                if (rtt > rttMax) rttMax = rtt;
            }
            else if (type == NetProtocol.Relay)
            {
                relaysIn++;
            }
        }

        // A pose-sized dummy (position, rotation, velocity ≈ 40 bytes) so relays exercise the stage-1 path.
        private void SendRelay()
        {
            if (driver.BeginSend(connection, out DataStreamWriter w) != 0) return;
            w.WriteByte(NetProtocol.Relay);
            w.WriteUShort(40);
            for (int i = 0; i < 10; i++) w.WriteFloat(relaysOut + i * 0.5f);
            driver.EndSend(w);
            relaysOut++;
        }

        private string Summary() =>
            $"rtt avg {rttSum / Math.Max(1, pongs):F1} min {rttMin:F1} max {rttMax:F1} ms, pongs {pongs}, peers {peers}, relays in {relaysIn} out {relaysOut}";

        private static void Report(string s)
        {
            Status = s;
            Debug.Log("[NetProbe] " + s);
        }
    }
}
