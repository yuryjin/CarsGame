#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;

namespace RacingGame.Net
{
    internal sealed class PrivateRooms
    {
        private sealed class Room
        {
            internal uint Code, Seed;
            internal byte Mode;
            internal bool Started, Finished;
            internal readonly NetworkConnection[] Players = new NetworkConnection[NetProtocol.Seats];

            internal byte Mask()
            {
                int mask = 0;
                for (int i = 0; i < Players.Length; i++) if (Players[i].IsCreated) mask |= 1 << i;
                return (byte)mask;
            }
        }

        private readonly Dictionary<uint, Room> byCode = new Dictionary<uint, Room>();
        private readonly Dictionary<NetworkConnection, Room> byPeer = new Dictionary<NetworkConnection, Room>();
        internal bool Contains(NetworkConnection peer) => byPeer.ContainsKey(peer);

        private static uint RandomValue()
        {
            var bytes = new byte[4];
            using (var generator = RandomNumberGenerator.Create()) generator.GetBytes(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        internal void Receive(NetworkConnection peer, byte type, ref DataStreamReader data, ref NetworkDriver driver, NativeArray<byte> buffer)
        {
            int length = data.Length - data.GetBytesRead();
            byPeer.TryGetValue(peer, out var room);
            if (type == NetProtocol.CreateRoom || type == NetProtocol.JoinRoom)
            {
                int expected = type == NetProtocol.CreateRoom ? 2 : 5;
                if (room != null || length != expected || data.ReadByte() != NetProtocol.RoomVersion)
                { Reply(peer, NetProtocol.Rejected, ref driver); return; }
                if (type == NetProtocol.CreateRoom)
                {
                    byte mode = data.ReadByte();
                    if (mode > 1 || byCode.Count >= 128) { Reply(peer, NetProtocol.Rejected, ref driver); return; }
                    uint code;
                    do code = 10000000 + RandomValue() % 90000000; while (byCode.ContainsKey(code));
                    room = new Room { Code = code, Mode = mode };
                    byCode.Add(code, room);
                }
                else if (!byCode.TryGetValue(data.ReadUInt(), out room) || room.Started || room.Mask() == 15)
                { Reply(peer, NetProtocol.Rejected, ref driver); return; }
                int slot = Array.IndexOf(room.Players, default(NetworkConnection));
                room.Players[slot] = peer;
                byPeer.Add(peer, room);
                BroadcastRoster(room, NetProtocol.Lobby, ref driver);
                return;
            }
            if (room == null) return;
            if (type == NetProtocol.Leave && length == 0) { Remove(peer, ref driver); return; }
            bool host = room.Players[0] == peer;
            if (type == NetProtocol.StartMatch && host && !room.Started && length == 0 && room.Mask() > 1)
            {
                room.Seed = RandomValue();
                room.Started = true;
                BroadcastRoster(room, NetProtocol.BeginMatch, ref driver);
                Debug.Log($"[PrivateRoom] start {room.Code} mode={room.Mode} seats={room.Mask()}");
                return;
            }
            if (!room.Started || room.Finished) return;
            if (type == NetProtocol.Input && !host && length == NetProtocol.InputBytes)
            {
                if (driver.BeginSend(room.Players[0], out var writer) != 0) return;
                writer.WriteByte(type);
                writer.WriteByte((byte)Array.IndexOf(room.Players, peer));
                for (int i = 0; i < length; i++) writer.WriteByte(data.ReadByte());
                driver.EndSend(writer);
                return;
            }
            if (!host || length < 1 || length > buffer.Length) return;
            if (type != NetProtocol.Snapshot && type != NetProtocol.Finish && type != NetProtocol.Actors && type != NetProtocol.Shot && type != NetProtocol.Standings) return;
            if (type == NetProtocol.Actors && room.Mode != 1) return;
            if (type == NetProtocol.Standings && (room.Mode != 0 || length != 12)) return;
            if (type == NetProtocol.Finish && length != 6) return;
            var payload = buffer.GetSubArray(0,length);
            data.ReadBytes(payload);
            if (type == NetProtocol.Finish)
            {
                int mask = 0;
                for (int i = 1; i < 5; i++) { if (payload[i] >= 4) return; mask |= 1 << payload[i]; }
                if (mask != 15 || payload[5] > 15 || (room.Mode == 0 ? payload[0] != 0 : payload[0] != 1 && payload[0] != 2)) return;
                room.Finished = true;
                Debug.Log($"[PrivateRoom] finish {room.Code} outcome={payload[0]} order={payload[1]},{payload[2]},{payload[3]},{payload[4]} finished={payload[5]}");
            }
            for (int i = type == NetProtocol.Finish ? 0 : 1; i < room.Players.Length; i++)
            {
                if (!room.Players[i].IsCreated || driver.BeginSend(room.Players[i], out var next) != 0) continue;
                next.WriteByte(type); next.WriteBytes(payload); driver.EndSend(next);
            }
        }

        internal void Remove(NetworkConnection peer, ref NetworkDriver driver)
        {
            if (!byPeer.TryGetValue(peer, out var room)) return;
            byPeer.Remove(peer);
            int slot = Array.IndexOf(room.Players, peer);
            room.Players[slot] = default;
            if (slot != 0 && !room.Started)
            {
                BroadcastRoster(room, NetProtocol.Lobby, ref driver); return;
            }
            byCode.Remove(room.Code);
            foreach (var other in room.Players)
            {
                if (!other.IsCreated) continue;
                if (!room.Finished) Reply(other, NetProtocol.Closed, ref driver);
                byPeer.Remove(other);
            }
            Debug.Log($"[PrivateRoom] close {room.Code} departing={slot} finished={room.Finished}");
        }

        private static void BroadcastRoster(Room room, byte type, ref NetworkDriver driver)
        {
            for (int i = 0; i < room.Players.Length; i++)
            {
                if (!room.Players[i].IsCreated || driver.BeginSend(room.Players[i], out var w) != 0) continue;
                w.WriteByte(type); w.WriteUInt(room.Code); w.WriteByte((byte)i);
                w.WriteByte(room.Mask()); w.WriteByte(room.Mode); w.WriteUInt(room.Seed);
                driver.EndSend(w);
            }
        }

        private static void Reply(NetworkConnection peer, byte type, ref NetworkDriver driver)
        {
            if (driver.BeginSend(peer,out var w) == 0) { w.WriteByte(type); driver.EndSend(w); }
        }
    }
}
#endif
