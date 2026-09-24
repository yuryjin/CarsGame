using Unity.Collections;

namespace RacingGame.Net
{
    /// <summary>
    /// Wire format of the transport probe (Docs/AI/Multiplayer.md §8). One byte of message type, then
    /// fixed little-endian fields. Kept tiny on purpose: the real match protocol (§4.2) replaces it.
    /// </summary>
    public static class NetProtocol
    {
        public const byte Ping = 1;   // client → server: uint seq, double clientTime
        public const byte Pong = 2;   // server → client: uint seq, double clientTime, ushort peers
        public const byte Relay = 3;  // client → server → every other client: ushort length, bytes

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
