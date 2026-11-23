using Unity.Collections;

namespace RacingGame.Net
{
    /// <summary>
    /// The transport probe and private rooms share a one-byte discriminator and little-endian fields.
    /// </summary>
    public static class NetProtocol
    {
        public const byte Ping = 1;   // client → server: uint seq, double clientTime
        public const byte Pong = 2;   // server → client: uint seq, double clientTime, ushort peers
        public const byte Relay = 3;  // client → server → every other client: ushort length, bytes
        public const byte CreateRoom = 10, JoinRoom = 11, Lobby = 12, StartMatch = 13, BeginMatch = 14;
        public const byte Input = 15, Snapshot = 16, Finish = 17, Leave = 18, Closed = 19, Rejected = 20;
        public const byte Actors = 21, Shot = 22, Standings = 23;
        public const byte RoomVersion = 2;
        public const int Seats = 4, InputBytes = 18;

        public static void WritePing(ref DataStreamWriter w, uint seq, double clientTime)
        {
            w.WriteByte(Ping);
            w.WriteUInt(seq);
            w.WriteDouble(clientTime);
        }

        public static void WritePong(ref DataStreamWriter w, uint seq, double clientTime, int peers)
        {
            w.WriteByte(Pong);
            w.WriteUInt(seq);
            w.WriteDouble(clientTime);
            w.WriteUShort((ushort)peers);
        }
    }
}
