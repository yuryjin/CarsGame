using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace RacingGame.Net
{
    public sealed partial class RoomClient
    {
        private const int ActorsPerPacket = 20, MaxActors = 96;
        private readonly List<RoomActor> actors = new List<RoomActor>(MaxActors);
        private uint actorSequence;

        private void SendActors(MissionDirector mission)
        {
            mission.CopyRoomActors(actors);
            int total = Mathf.Min(actors.Count,MaxActors);
            for (int offset = 0; offset < total || offset == 0; offset += ActorsPerPacket)
            {
                if (driver.BeginSend(peer,out var w) != 0) return;
                int count = Mathf.Min(total - offset,ActorsPerPacket);
                w.WriteByte(NetProtocol.Actors); w.WriteUInt(sequence);
                w.WriteByte((byte)total); w.WriteByte((byte)offset); w.WriteByte((byte)count);
                for (int i = offset; i < offset+count; i++)
                {
                    var a = actors[i];
                    w.WriteInt(a.id); w.WriteByte(a.kind); WritePose(ref w,a.position,a.rotation); w.WriteFloat(a.health);
                }
                driver.EndSend(w);
            }
        }

        private void ReadActors(ref DataStreamReader r)
        {
            if (r.Length < 8 || MissionDirector.Current == null) return;
            uint seq = r.ReadUInt(); int total = r.ReadByte(), offset = r.ReadByte(), count = r.ReadByte();
            if (total > MaxActors || count > ActorsPerPacket || offset+count > total || r.Length != 8+37*count) return;
            if (offset == 0) { actors.Clear(); actorSequence = seq; }
            if (seq != actorSequence || offset != actors.Count) return;
            for (int i = 0; i < count; i++)
            {
                var a = new RoomActor { id = r.ReadInt(), kind = r.ReadByte() };
                if (a.kind > RoomActor.Tank || !ReadPose(ref r,out a.position,out a.rotation)) return;
                a.health = r.ReadFloat();
                if (!(a.health > 0f && a.health < 50000f)) return;
                actors.Add(a);
            }
            if (actors.Count == total) MissionDirector.Current.ApplyRoomActors(actors);
        }

        private int DriverSeat(GameObject owner)
        {
            for (int i = 0; i < race.Participants.Count; i++)
                if (race.Participants[i].gameObject == owner) return i;
            return NetProtocol.Seats;
        }

        private void CountCoreHit(MissionUnit target, GameObject attacker, float damage)
        {
            if (State != RoomState.Playing || target != MissionDirector.Current?.Boss?.Core) return;
            int seat = DriverSeat(attacker);
            if (seat < NetProtocol.Seats) CoreDamage[seat] += damage;
        }

        private void SendShot(ProjectileSpawn shot)
        {
            if (State != RoomState.Playing || Seat != 0) return;
            int owner = DriverSeat(shot.Owner);
            if (owner < NetProtocol.Seats) Shots[owner]++;
            if (driver.BeginSend(peer,out var w) != 0) return;
            w.WriteByte(NetProtocol.Shot); w.WriteByte((byte)owner);
            w.WriteByte((byte)shot.Kind); w.WriteByte((byte)shot.Weapon);
            w.WriteFloat(shot.Position.x); w.WriteFloat(shot.Position.y); w.WriteFloat(shot.Position.z);
            w.WriteFloat(shot.Velocity.x); w.WriteFloat(shot.Velocity.y); w.WriteFloat(shot.Velocity.z);
            driver.EndSend(w);
        }

        private void ReadShot(ref DataStreamReader r)
        {
            if (r.Length != 28 || race.Participants.Count != 4) return;
            int seat = r.ReadByte(), kind = r.ReadByte(), weapon = r.ReadByte();
            var at = new Vector3(r.ReadFloat(),r.ReadFloat(),r.ReadFloat());
            var velocity = new Vector3(r.ReadFloat(),r.ReadFloat(),r.ReadFloat());
            if (seat > 4 || kind > (int)ProjectileKind.Freeze || weapon > (int)PrimaryWeaponType.HomingRocket ||
                !(at.sqrMagnitude < 1e10f && velocity.sqrMagnitude < 1e8f)) return;
            var car = seat < 4 ? race.Participants[seat].Car : null;
            RaceDirector.Instance.RequestProjectile(new ProjectileSpawn { Position = at, Velocity = velocity,
                Owner = car != null ? car.gameObject : MissionDirector.Current?.Boss?.gameObject, OwnerId = car != null ? car.CarId : -1,
                Kind = (ProjectileKind)kind, Weapon = (PrimaryWeaponType)weapon, Tick = SimulationDriver.Instance.Tick });
        }
    }
}
