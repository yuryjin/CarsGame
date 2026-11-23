using RacingGame.Core.Sim;
using Unity.Collections;
using UnityEngine;

namespace RacingGame.Net
{
    public sealed partial class RoomClient
    {
        private sealed class RemoteInput : ICarInputSource
        {
            private readonly CarCannon cannon;
            private ushort last;
            private DriveCommand command;
            private float until;
            internal RemoteInput(CarCannon cannon) { this.cannon = cannon; }

            internal void Receive(ref DataStreamReader r)
            {
                ushort seq = r.ReadUShort();
                if ((short)(seq - last) <= 0) return;
                last = seq;
                command.Throttle = (sbyte)r.ReadByte() / 127f;
                command.Steer = (sbyte)r.ReadByte() / 127f;
                byte flags = r.ReadByte();
                command.Brake = (flags & 1) != 0; command.Fire = (flags & 2) != 0;
                command.Secondary = (flags & 4) != 0; command.UseItem |= (flags & 8) != 0;
                command.ResetEdge |= (flags & 16) != 0;
                int slot = r.ReadByte();
                if (slot > 0 && slot <= 50) command.SelectPrimary = slot;
                var point = new Vector3(r.ReadFloat(),r.ReadFloat(),r.ReadFloat());
                if (cannon != null)
                {
                    if ((flags & 32) != 0 && (point - cannon.transform.position).sqrMagnitude < 4000f*4000f)
                        cannon.CommitAim(point,allowHoming:true,yawLimit:90f);
                    else cannon.ClearAimCommit();
                }
                until = Time.realtimeSinceStartup + .45f;
            }

            public DriveCommand Sample(int tick)
            {
                if (Time.realtimeSinceStartup > until) { command = default; cannon?.ClearAimCommit(); }
                command.Tick = tick;
                var sample = command.Clamped();
                command.ResetEdge = command.UseItem = false; command.SelectPrimary = 0;
                return sample;
            }
        }

        private struct PoseFrame
        {
            internal Vector3 position;
            internal Quaternion rotation;
            internal float speed, health, yaw, pitch, reload, coreDamage;
            internal int place, primary, ammo, shots;
            internal bool dead;
        }

        private readonly PoseFrame[] poses = new PoseFrame[4], from = new PoseFrame[4], incoming = new PoseFrame[4];
        private float receivedAt;
        private float[] parts;

        private void SendInput()
        {
            if (race.Player?.Car == null || SimulationDriver.Current == null || driver.BeginSend(peer,out var w) != 0) return;
            var c = race.Player.Car.ReadRoomInput(SimulationDriver.Current.Tick).Clamped();
            var cannon = race.Player.Car.Cannon;
            bool aim = cannon != null && cannon.HasAimCommit;
            Vector3 point = aim ? cannon.AimCommitPoint : Vector3.zero;
            w.WriteByte(NetProtocol.Input); w.WriteUShort((ushort)++sequence);
            w.WriteByte((byte)(sbyte)Mathf.RoundToInt(c.Throttle * 127f)); w.WriteByte((byte)(sbyte)Mathf.RoundToInt(c.Steer * 127f));
            w.WriteByte((byte)((c.Brake ? 1 : 0) | (c.Fire ? 2 : 0) | (c.Secondary ? 4 : 0) | (c.UseItem ? 8 : 0) | (c.ResetEdge ? 16 : 0) | (aim ? 32 : 0)));
            w.WriteByte((byte)c.SelectPrimary);
            w.WriteFloat(point.x); w.WriteFloat(point.y); w.WriteFloat(point.z);
            driver.EndSend(w);
        }

        private void SendSnapshot()
        {
            if (race.Participants.Count != 4) return;
            if (race.State == RaceState.Finished && !finishSent && race.Results != null) { ReportFinish(race.Results); return; }
            if (driver.BeginSend(peer,out var w) != 0) return;
            w.WriteByte(NetProtocol.Snapshot); w.WriteUInt(++sequence); w.WriteByte((byte)race.State);
            w.WriteByte((byte)Mathf.Clamp(race.CountdownValue,0,9)); w.WriteFloat((float)race.RaceTime);
            for (int i = 0; i < race.Participants.Count; i++)
            {
                var p = race.Participants[i];
                WritePose(ref w,p.Car.Body.position,p.Car.Body.rotation);
                w.WriteFloat(p.Car.SpeedKmh); w.WriteFloat(p.Health.Health);
                w.WriteByte((byte)race.PositionOf(p)); w.WriteByte((byte)(p.Health.IsDead ? 1 : 0));
                var cannon = p.Car.Cannon;
                w.WriteByte((byte)p.Weapons.PrimaryType); w.WriteByte((byte)Mathf.Min(cannon.Ammo,255));
                w.WriteFloat(cannon.TurretYaw); w.WriteFloat(cannon.BarrelPitch);
                w.WriteByte((byte)Mathf.RoundToInt(cannon.ReloadProgress(SimulationDriver.Instance.Tick)*255f));
                w.WriteUShort((ushort)Mathf.Min(Shots[i],ushort.MaxValue)); w.WriteFloat(CoreDamage[i]);
            }
            var boss = Fortress ? MissionDirector.Current?.Boss : null;
            w.WriteByte((byte)(boss != null ? boss.Parts.Count : 0));
            if (boss != null)
            {
                WritePose(ref w,boss.transform.position,boss.transform.rotation);
                w.WriteFloat(boss.Distance); w.WriteByte((byte)boss.Phase);
                w.WriteByte((byte)MissionDirector.Current.Result);
                foreach (var part in boss.Parts) w.WriteFloat(part != null && part.Alive ? part.Health : 0f);
            }
            driver.EndSend(w);
            if (Fortress && sequence % 3 == 0 && MissionDirector.Current != null) SendActors(MissionDirector.Current);
            if (!Fortress && sequence % 10 == 0 && driver.BeginSend(peer,out var standings) == 0)
            {
                standings.WriteByte(NetProtocol.Standings);
                foreach (var p in race.Participants)
                {
                    var gates = p.Progress.Counter;
                    standings.WriteByte((byte)gates.LapsCompleted); standings.WriteByte((byte)gates.NextGate);
                    standings.WriteByte((byte)(gates.LineArmed ? 1 : 0));
                }
                driver.EndSend(standings);
            }
        }

        private static void WritePose(ref DataStreamWriter w, Vector3 p, Quaternion q)
        {
            w.WriteFloat(p.x); w.WriteFloat(p.y); w.WriteFloat(p.z);
            w.WriteFloat(q.x); w.WriteFloat(q.y); w.WriteFloat(q.z); w.WriteFloat(q.w);
        }

        private static bool ReadPose(ref DataStreamReader r, out Vector3 p, out Quaternion q)
        {
            p = new Vector3(r.ReadFloat(),r.ReadFloat(),r.ReadFloat());
            q = new Quaternion(r.ReadFloat(),r.ReadFloat(),r.ReadFloat(),r.ReadFloat());
            float norm = Quaternion.Dot(q,q);
            return p.sqrMagnitude <= 100000f * 100000f && norm > .98f && norm < 1.02f;
        }

        private void ReadSnapshot(ref DataStreamReader r)
        {
            if (race == null || race.Participants.Count != 4 || r.Length < 232) return;
            uint seq = r.ReadUInt();
            if (seq <= lastSnapshot) return;
            var state = (RaceState)r.ReadByte(); int count = r.ReadByte(); float elapsed = r.ReadFloat();
            if (state < RaceState.Countdown || state > RaceState.Finished || count > 9 || !(elapsed >= 0f && elapsed < 7200f)) return;
            for (int i = 0; i < 4; i++)
            {
                ref var pose = ref incoming[i];
                if (!ReadPose(ref r,out pose.position,out pose.rotation)) return;
                pose.speed = r.ReadFloat(); pose.health = r.ReadFloat(); pose.place = r.ReadByte();
                int dead = r.ReadByte(); pose.dead = dead == 1;
                pose.primary = r.ReadByte(); pose.ammo = r.ReadByte(); pose.yaw = r.ReadFloat(); pose.pitch = r.ReadFloat();
                pose.reload = r.ReadByte()/255f;
                pose.shots = r.ReadUShort(); pose.coreDamage = r.ReadFloat();
                if (!(Mathf.Abs(pose.speed) <= 2000f) || !(pose.health >= 0f && pose.health <= 20000f) ||
                    pose.place < 1 || pose.place > 4 || dead > 1 || pose.primary >= PrimaryWeaponCatalog.Count ||
                    !(Mathf.Abs(pose.yaw) <= 181f && Mathf.Abs(pose.pitch) <= 91f && pose.coreDamage >= 0f && pose.coreDamage <= 50000f)) return;
            }
            int n = r.ReadByte();
            int remaining = r.Length - r.GetBytesRead();
            if (n != 0)
            {
                var boss = MissionDirector.Current?.Boss;
                if (!Fortress || boss == null || n != boss.Parts.Count || remaining != 34 + 4*n ||
                    !ReadPose(ref r,out var position,out var rotation)) return;
                float distance = r.ReadFloat(); int phase = r.ReadByte(), outcome = r.ReadByte();
                if (!(distance >= 0f && distance <= 50000f) || phase < 1 || phase > 3 || outcome > 2) return;
                if (parts == null || parts.Length != n) parts = new float[n];
                for (int i = 0; i < n; i++)
                {
                    parts[i] = r.ReadFloat();
                    if (!(parts[i] >= 0f && parts[i] <= 50000f)) return;
                }
                boss.ApplyRoomState(position,rotation,distance,phase,parts);
                MissionDirector.Current.ApplyRoomOutcome((MissionDirector.Outcome)outcome);
            }
            else if (remaining != 0 || Fortress) return;
            System.Array.Copy(incoming,poses,poses.Length);
            race.ApplyRoomClock(state,count,elapsed);
            for (int i = 0; i < 4; i++)
            {
                var p = race.Participants[i];
                from[i].position = p.transform.position; from[i].rotation = p.transform.rotation;
                if (lastSnapshot == 0 || Vector3.SqrMagnitude(poses[i].position - from[i].position) > 18f * 18f) from[i] = poses[i];
                p.Health.ApplyRoomHealth(poses[i].health,poses[i].dead);
                Shots[i] = poses[i].shots; CoreDamage[i] = poses[i].coreDamage;
                if ((int)p.Weapons.PrimaryType != poses[i].primary) p.Weapons.SetPrimaryType((PrimaryWeaponType)poses[i].primary);
                p.Car.Cannon.ApplyRoomBarrel(poses[i].yaw,poses[i].pitch,poses[i].ammo,poses[i].reload);
                race.ApplyRoomStanding(i, poses[i].place);
            }
            receivedAt = Time.realtimeSinceStartup;
            lastSnapshot = seq;
        }

        private void LateUpdate()
        {
            if (State != RoomState.Playing || Seat == 0 || lastSnapshot == 0 || race?.Participants.Count != 4) return;
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - receivedAt) / .05f);
            for (int i = 0; i < 4; i++)
                race.Participants[i].Car.ApplyRoomPose(Vector3.Lerp(from[i].position,poses[i].position,t),
                    Quaternion.Slerp(from[i].rotation,poses[i].rotation,t),poses[i].speed);
        }
    }
}
