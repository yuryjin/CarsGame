using System;
using RacingGame.Core.Sim;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;

namespace RacingGame.Net
{
    public sealed partial class RoomClient : MonoBehaviour, IPrivateRoomLink
    {
        public RoomState State { get; private set; }
        public uint Code { get; private set; }
        public int Seat { get; private set; }
        public int Members { get; private set; }
        public bool Fortress { get; private set; }
        public int Outcome { get; private set; }
        public int FinishedSeats { get; private set; }
        public int[] Shots { get; } = new int[NetProtocol.Seats];
        public float[] CoreDamage { get; } = new float[NetProtocol.Seats];
        public int[] FinishOrder { get; } = new int[NetProtocol.Seats];

        private NetworkDriver driver;
        private NetworkConnection peer;
        private GameFlow flow;
        private RaceManager race;
        private MenuScreens menu;
        private bool connected, creating, finishSent;
        private float connectDeadline, nextFrame;
        private uint sequence, lastSnapshot;
        private readonly RemoteInput[] inputs = new RemoteInput[NetProtocol.Seats];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (GameFlow.Room is RoomClient live && live != null) return;
            var go = new GameObject("PrivateRoom");
            DontDestroyOnLoad(go);
            GameFlow.Room = go.AddComponent<RoomClient>();
        }

        private void FindScene()
        {
            if (flow != null) return;
            flow = FindAnyObjectByType<GameFlow>();
            if (flow != null) { race = flow.GetComponent<RaceManager>(); menu = flow.GetComponent<MenuScreens>(); }
        }

        private static Uri Endpoint()
        {
            Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var page);
            if (page != null && page.IsLoopback) foreach (string pair in page.Query.TrimStart('?').Split('&'))
            {
                if (!pair.StartsWith("roomServer=", StringComparison.Ordinal)) continue;
                if (Uri.TryCreate(Uri.UnescapeDataString(pair.Substring(11)), UriKind.Absolute, out var local) &&
                    local.IsLoopback && (local.Scheme == "ws" || local.Scheme == "wss")) return local;
            }
            var address = Resources.Load<TextAsset>("RoomServer");
            if (address != null)
            {
                if (Uri.TryCreate(address.text.Trim(),UriKind.Absolute,out var service) && service.Scheme == "wss" &&
                    !string.IsNullOrEmpty(service.Host) && service.UserInfo.Length == 0 && service.Fragment.Length == 0) return service;
                throw new FormatException("RoomServer requires a wss URL");
            }
            if (page == null) return new Uri("ws://127.0.0.1:7777/net");
            return new UriBuilder(page.Scheme == "https" ? "wss" : "ws", page.Host, page.Port, "/net").Uri;
        }

        public void Create(bool fortress) { Fortress = fortress; Open(0); }
        public void Join(uint code)
        {
            if (code < 10000000 || code > 99999999) { Show(RoomState.Unavailable); return; }
            Open(code);
        }

        private void Open(uint code)
        {
            if (State == RoomState.Playing || State == RoomState.Lobby) return;
            CloseSocket();
            FindScene();
            Code = code; creating = code == 0; Members = FinishedSeats = Outcome = 0;
            try
            {
                var endpoint = Endpoint();
                peer = NetProbe.Connect(endpoint, (ushort)endpoint.Port, out driver);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrivateRoom] " + e.Message);
                CloseSocket(); Show(RoomState.Unavailable); return;
            }
            connectDeadline = Time.realtimeSinceStartup + 12f;
            Show(RoomState.Connecting);
        }

        public void StartMatch()
        {
            if (State == RoomState.Lobby && Seat == 0 && Members > 1) SendType(NetProtocol.StartMatch);
        }

        public void Leave()
        {
            if (connected) SendType(NetProtocol.Leave);
            CloseSocket(); Members = 0; Code = 0;
            Show(RoomState.Offline);
        }

        private void CloseSocket()
        {
            if (RaceDirector.Current != null) RaceDirector.Current.ProjectileSpawned -= SendShot;
            MissionUnit.Damaged -= CountCoreHit;
            if (driver.IsCreated)
            {
                if (peer.IsCreated) { driver.Disconnect(peer); driver.ScheduleUpdate().Complete(); }
                driver.Dispose();
            }
            connected = false; peer = default;
        }

        private void OnDestroy()
        {
            CloseSocket();
            if (ReferenceEquals(GameFlow.Room, this)) GameFlow.Room = null;
        }

        private void Show(RoomState state)
        {
            State = state;
            FindScene();
            if (state == RoomState.Playing) return;
            if (state != RoomState.Offline && flow != null && flow.State != FlowState.Menu) flow.EnterMenu();
            if (menu != null && (menu.Current == "rooms" || state != RoomState.Offline)) menu.Show("rooms");
        }

        private void SendType(byte type)
        {
            if (!connected || driver.BeginSend(peer, out var writer) != 0) return;
            writer.WriteByte(type); driver.EndSend(writer);
        }

        private void Update()
        {
            if (!driver.IsCreated) return;
            driver.ScheduleUpdate().Complete();
            NetworkEvent.Type ev;
            while (peer.IsCreated && (ev = peer.PopEvent(driver,out var stream)) != NetworkEvent.Type.Empty)
            {
                if (ev == NetworkEvent.Type.Disconnect)
                {
                    var next = State == RoomState.Results ? RoomState.Results :
                        State == RoomState.Connecting ? RoomState.Unavailable : RoomState.Interrupted;
                    CloseSocket(); Show(next); return;
                }
                if (ev == NetworkEvent.Type.Connect)
                {
                    connected = true;
                    if (driver.BeginSend(peer,out var w) == 0)
                    {
                        w.WriteByte(creating ? NetProtocol.CreateRoom : NetProtocol.JoinRoom);
                        w.WriteByte(NetProtocol.RoomVersion);
                        if (creating) w.WriteByte((byte)(Fortress ? 1 : 0)); else w.WriteUInt(Code);
                        driver.EndSend(w);
                    }
                }
                if (ev == NetworkEvent.Type.Data && stream.Length > 0) Receive(ref stream);
                if (!driver.IsCreated) return;
            }
            float now = Time.realtimeSinceStartup;
            if (State == RoomState.Connecting && now >= connectDeadline)
            { CloseSocket(); Show(RoomState.Unavailable); return; }
            if (State != RoomState.Playing || now < nextFrame || race == null) return;
            nextFrame = now + (Seat == 0 ? .05f : .033f);
            if (Seat == 0) SendSnapshot(); else SendInput();
        }

        private void Receive(ref DataStreamReader r)
        {
            byte type = r.ReadByte();
            if (type == NetProtocol.Closed || type == NetProtocol.Rejected)
            {
                CloseSocket(); Show(type == NetProtocol.Closed ? RoomState.Interrupted : RoomState.Unavailable); return;
            }
            if ((type == NetProtocol.Lobby || type == NetProtocol.BeginMatch) && r.Length == 12)
            {
                uint code = r.ReadUInt(); byte seat = r.ReadByte(), members = r.ReadByte(), mode = r.ReadByte(); int seed = (int)r.ReadUInt();
                if (code < 10000000 || code > 99999999 || seat >= 4 || members > 15 || (members & (1 << seat)) == 0 || mode > 1) return;
                Code = code; Seat = seat; Members = members; Fortress = mode == 1;
                if (type == NetProtocol.Lobby) { Show(RoomState.Lobby); return; }
                FindScene();
                State = RoomState.Playing;
                sequence = lastSnapshot = 0; finishSent = false; nextFrame = 0f;
                Array.Clear(Shots,0,Shots.Length); Array.Clear(CoreDamage,0,CoreDamage.Length);
                if (flow == null || !flow.StartPrivateMatch(seed)) { Leave(); return; }
                if (Seat == 0)
                {
                    RaceDirector.Instance.ProjectileSpawned += SendShot;
                    if (Fortress) MissionUnit.Damaged += CountCoreHit;
                    for (int slot = 1; slot < 4; slot++)
                        if ((Members & (1 << slot)) != 0)
                        {
                            inputs[slot] = new RemoteInput(race.Participants[slot].Car.Cannon);
                            race.Participants[slot].Car.SetInputSource(inputs[slot]);
                        }
                }
                Debug.Log($"[PrivateRoom] begin {Code} seat={Seat} members={Members} seed={seed} mode={mode}");
                return;
            }
            if (State != RoomState.Playing) return;
            if (type == NetProtocol.Input && Seat == 0 && r.Length == 2 + NetProtocol.InputBytes)
            {
                int slot = r.ReadByte();
                if (slot > 0 && slot < 4) inputs[slot]?.Receive(ref r);
            }
            else if (type == NetProtocol.Snapshot && Seat != 0) ReadSnapshot(ref r);
            else if (type == NetProtocol.Standings && Seat != 0 && !Fortress && r.Length == 13 && race.Participants.Count == 4)
            {
                for (int i = 0; i < NetProtocol.Seats; i++)
                    race.Participants[i].Progress.ApplyRoomStanding(r.ReadByte(),r.ReadByte(),r.ReadByte() == 1);
            }
            else if (type == NetProtocol.Actors && Seat != 0 && Fortress) ReadActors(ref r);
            else if (type == NetProtocol.Shot && Seat != 0) ReadShot(ref r);
            else if (type == NetProtocol.Finish && r.Length == 7)
            {
                int result = r.ReadByte(), mask = 0;
                if (result > 2) return;
                for (int i = 0; i < 4; i++)
                {
                    byte s = r.ReadByte(); if (s >= 4) return;
                    FinishOrder[i] = s; mask |= 1 << s;
                }
                byte finished = r.ReadByte();
                if (mask != 15 || finished > 15) return;
                FinishedSeats = finished;
                Outcome = result;
                Debug.Log($"[PrivateRoom] result {Code} seat={Seat} outcome={Outcome} order={string.Join(",",FinishOrder)}");
                Show(RoomState.Results);
            }
        }

        public void ReportFinish(RaceResults results)
        {
            if (Seat != 0 || State != RoomState.Playing || finishSent || results.Entries.Count != 4) return;
            if (driver.BeginSend(peer,out var w) != 0) return;
            w.WriteByte(NetProtocol.Finish);
            w.WriteByte((byte)(Fortress ? MissionDirector.LastVictory ? 1 : 2 : 0));
            int finished = 0;
            foreach (var entry in results.Entries)
            {
                int slot = 0;
                while (slot < 4 && race.Participants[slot].Car.CarId != entry.CarId) slot++;
                if (slot == 4) { driver.AbortSend(w); return; }
                if (entry.Finished) finished |= 1 << slot;
                w.WriteByte((byte)slot);
            }
            w.WriteByte((byte)finished);
            driver.EndSend(w); finishSent = true;
        }
    }
}
