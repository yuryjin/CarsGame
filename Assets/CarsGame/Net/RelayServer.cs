#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Threading;
using Unity.Collections;
using Unity.Networking.Transport;
using Debug = UnityEngine.Debug;

namespace RacingGame.Net
{
    /// <summary>
    /// Transport-probe server (Docs/AI/Multiplayer.md §8): WebSocket listener that answers pings and
    /// forwards relay messages to every other client. Blocking loop, no scene and no player loop, so it
    /// runs from a batch Editor or a headless player:
    /// <c>Unity -batchmode -nographics -projectPath … -executeMethod RacingGame.Net.RelayServer.RunFromCommandLine
    /// -netPort 7777 -netPath /net -netSeconds 600 -quit</c>. TLS is expected in front (nginx/caddy → ws://).
    /// </summary>
    public static class RelayServer
    {
        public static void RunFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            ushort port = 7777;
            double seconds = 600;
            string path = "/";
            bool loopback = Array.IndexOf(args, "-netLocal") >= 0;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-netPort") port = ushort.Parse(args[i + 1]);
                else if (args[i] == "-netSeconds") seconds = double.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture);
                else if (args[i] == "-netPath") path = args[i + 1];
            }
            Run(port, path, seconds, loopback);
        }

        public static void Run(ushort port, string path, double seconds, bool loopback = false)
        {
            var settings = new NetworkSettings();
            settings.WithWebSocketParameters(path);
            var driver = NetworkDriver.Create(new WebSocketNetworkInterface(), settings);
            var address = loopback ? NetworkEndpoint.LoopbackIpv4 : NetworkEndpoint.AnyIpv4;
            if (driver.Bind(address.WithPort(port)) != 0 || driver.Listen() != 0)
            {
                Debug.LogError($"[RelayServer] cannot listen on port {port}");
                driver.Dispose();
                return;
            }
            Debug.Log($"[RelayServer] listening on {address.WithPort(port)}{path} for {seconds:F0} s");

            var connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
            var relayBuffer = new NativeArray<byte>(1024, Allocator.Persistent);
            var rooms = new PrivateRooms();
            long pings = 0, relays = 0;
            var clock = Stopwatch.StartNew();
            double nextStats = 10;
            try
            {
                while (clock.Elapsed.TotalSeconds < seconds)
                {
                    driver.ScheduleUpdate().Complete();

                    for (int i = 0; i < connections.Length; i++)
                        if (!connections[i].IsCreated) { connections.RemoveAtSwapBack(i); i--; }

                    NetworkConnection accepted;
                    while ((accepted = driver.Accept()) != default)
                    {
                        connections.Add(accepted);
                        Debug.Log($"[RelayServer] connect, {connections.Length} peers");
                    }

                    for (int i = 0; i < connections.Length; i++)
                    {
                        NetworkEvent.Type cmd;
                        while ((cmd = driver.PopEventForConnection(connections[i], out DataStreamReader stream)) != NetworkEvent.Type.Empty)
                        {
                            if (cmd == NetworkEvent.Type.Disconnect)
                            {
                                rooms.Remove(connections[i], ref driver);
                                connections[i] = default;
                                Debug.Log("[RelayServer] disconnect");
                                break;
                            }
                            if (cmd != NetworkEvent.Type.Data || stream.Length < 1) continue;

                            byte type = stream.ReadByte();
                            if (type >= NetProtocol.CreateRoom)
                                rooms.Receive(connections[i], type, ref stream, ref driver, relayBuffer);
                            else if (type == NetProtocol.Ping && stream.Length == 13)
                            {
                                uint seq = stream.ReadUInt();
                                double sent = stream.ReadDouble();
                                pings++;
                                if (driver.BeginSend(connections[i], out DataStreamWriter w) == 0)
                                {
                                    NetProtocol.WritePong(ref w, seq, sent, connections.Length);
                                    driver.EndSend(w);
                                }
                            }
                            else if (type == NetProtocol.Relay && stream.Length >= 3 && !rooms.Contains(connections[i]))
                            {
                                int length = stream.ReadUShort();
                                if (length > relayBuffer.Length || length > stream.Length - stream.GetBytesRead()) continue;
                                var payload = relayBuffer.GetSubArray(0, length);
                                stream.ReadBytes(payload);
                                relays++;
                                for (int j = 0; j < connections.Length; j++)
                                {
                                    if (j == i || !connections[j].IsCreated || rooms.Contains(connections[j])) continue;
                                    if (driver.BeginSend(connections[j], out DataStreamWriter w) != 0) continue;
                                    w.WriteByte(NetProtocol.Relay);
                                    w.WriteUShort((ushort)length);
                                    w.WriteBytes(payload);
                                    driver.EndSend(w);
                                }
                            }
                        }
                    }

                    if (clock.Elapsed.TotalSeconds >= nextStats)
                    {
                        nextStats += 10;
                        Debug.Log($"[RelayServer] {clock.Elapsed.TotalSeconds:F0} s: {connections.Length} peers, {pings} pings, {relays} relays");
                    }
                    Thread.Sleep(2);
                }
            }
            finally
            {
                relayBuffer.Dispose();
                connections.Dispose();
                driver.Dispose();
                Debug.Log($"[RelayServer] stopped: {pings} pings, {relays} relays");
            }
        }
    }
}
#endif
