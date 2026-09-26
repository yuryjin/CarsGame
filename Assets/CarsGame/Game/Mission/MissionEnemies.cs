using UnityEngine;

namespace RacingGame
{
    /// <summary>Layer of soft mission units (drones, troopers, planes): shells hit them, cars drive through them.</summary>
    public static class MissionLayers
    {
        public const int SoftUnit = 2;   // "Ignore Raycast": wheel ground probes skip it; the mission turns off Car×SoftUnit contacts
    }

    /// <summary>Kinematic body + collider on the soft-unit layer for a moving unit.</summary>
    internal static class UnitBody
    {
        public static Rigidbody Add(GameObject go, Vector3 centre, float radius)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var sphere = go.AddComponent<SphereCollider>();
            sphere.center = centre;
            sphere.radius = radius;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = MissionLayers.SoftUnit;
            return rb;
        }
    }

    /// <summary>
    /// Quad-rotor launched from the fortress's hangar: circles a car of the other side at low height and fires short
    /// bursts after its eye glows. Fast enough to follow, slow enough to shake off at full speed. Falls when shot.
    /// </summary>
    public sealed class DroneUnit : MissionUnit
    {
        private const float CruiseSpeed = 19f, OrbitRadius = 15f, Altitude = 6.5f, Range = 60f, BulletSpeed = 95f;
        private DesertMap map;
        private Rigidbody rb;
        private Vector3 velocity;
        private CarController target;
        private float orbitAngle;
        private int readyTick, burstLeft, retargetTick;
        private float bob;

        public override Vector3 Velocity => velocity;
        public override Vector3 AimPoint => transform.position;

        public static DroneUnit Spawn(DesertMap map, Vector3 position, int seed)
        {
            var go = new GameObject("Drone");
            go.transform.position = position;
            var d = go.AddComponent<DroneUnit>();
            d.map = map;
            d.Kind = UnitKind.Drone;
            d.SetHealth(35f);
            d.Radius = 1.2f;
            d.orbitAngle = seed * 1.7f;
            d.bob = seed * 0.9f;
            MissionArt.Attach(go.transform, MissionArt.Cached("Drone", MissionArt.Drone), MissionLayers.SoftUnit);
            d.rb = UnitBody.Add(go, Vector3.zero, 1.1f);
            d.CollectRenderers();
            d.velocity = Vector3.up * 6f;
            return d;
        }

        protected override void Step(int tick)
        {
            if (tick >= retargetTick || target == null || !Usable(target))
            {
                target = FindHostileCar(transform.position, 160f, false);
                retargetTick = tick + 50;
            }
            float dt = SimulationDriver.FixedStep;
            Vector3 goal;
            if (target != null)
            {
                orbitAngle += dt * 0.9f;
                Vector3 centre = target.transform.position + (target.Body != null ? target.Body.linearVelocity * 0.6f : Vector3.zero);
                goal = centre + new Vector3(Mathf.Cos(orbitAngle), 0f, Mathf.Sin(orbitAngle)) * OrbitRadius;
            }
            else goal = transform.position + transform.forward * 10f;
            goal.y = map.Height(goal) + Altitude + Mathf.Sin(tick * 0.05f + bob) * 0.8f;
            Vector3 desired = Vector3.ClampMagnitude(goal - transform.position, 1f) * CruiseSpeed;
            if ((goal - transform.position).magnitude > 1f) desired = (goal - transform.position).normalized * CruiseSpeed;
            velocity = Vector3.MoveTowards(velocity, desired, 22f * dt);
            Vector3 pos = transform.position + velocity * dt;
            float floor = map.Height(pos) + 2.5f;
            if (pos.y < floor) { pos.y = floor; velocity.y = Mathf.Max(0f, velocity.y); }
            Vector3 look = target != null ? target.transform.position - pos : velocity;
            look.y = 0f;
            var rot = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) * Quaternion.Euler(Mathf.Clamp(velocity.magnitude * 0.8f, 0f, 15f), 0f, 0f) : transform.rotation;
            rb.MovePosition(pos);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, rot, 240f * dt));

            if (target == null || tick < readyTick) return;
            Vector3 aim = target.transform.position + Vector3.up * 0.5f;
            if ((aim - pos).sqrMagnitude > Range * Range) return;
            if (burstLeft <= 0) { burstLeft = 3; readyTick = tick + SimulationDriver.SecondsToTicks(0.5f); return; }   // wind-up
            Vector3 lead = Lead(pos, aim, target.Body != null ? target.Body.linearVelocity : Vector3.zero, BulletSpeed, false);
            FireShell(pos + Vector3.down * 0.4f, (lead - pos).normalized * BulletSpeed, 4f, tick, ProjectileKind.MachineGun, PrimaryWeaponType.MachineGun, 600f);
            burstLeft--;
            readyTick = tick + SimulationDriver.SecondsToTicks(burstLeft > 0 ? 0.12f : 1.7f);
        }

        private static bool Usable(CarController car)
        {
            if (car == null || !car.isActiveAndEnabled) return false;
            var h = car.GetComponent<CarHealth>();
            return h == null || !h.IsDead;
        }

        protected override void OnDied(GameObject attacker)
        {
            Explode(transform.position, 0.8f);
            Tint(new Color(0.15f, 0.12f, 0.1f));
            velocity = new Vector3(velocity.x * 0.5f, 2f, velocity.z * 0.5f);
        }

        protected override float DespawnSeconds => 2.5f;

        protected override void StepDying(int tick)
        {
            float dt = SimulationDriver.FixedStep;
            velocity += Physics.gravity * dt;
            Vector3 pos = transform.position + velocity * dt;
            float floor = map.Height(pos) + 0.3f;
            if (pos.y <= floor) { pos.y = floor; if (velocity.y < -3f) Explode(pos, 0.6f); velocity = Vector3.zero; }
            rb.MovePosition(pos);
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, 400f * dt, 90f * dt));
        }
    }

    /// <summary>
    /// Cyborg trooper (a scrap robot, no humans: see ArtDirection): walks towards the nearest car of the other side,
    /// stops at range and fires. The rocket variant stands still for a telegraphed shot. Any car that rolls over one
    /// faster than a jog squashes it.
    /// </summary>
    public sealed class TrooperUnit : MissionUnit
    {
        private const float WalkSpeed = 2.6f;
        private DesertMap map;
        private Rigidbody rb;
        private Transform legL, legR;
        private CarController target;
        private bool rocket;
        private int readyTick, burstLeft, retargetTick, aimUntil = -1;
        private float stride, facing;
        private bool walking;

        public override Vector3 AimPoint => transform.position + Vector3.up * 1.3f;
        public override Vector3 Velocity => walking ? transform.forward * WalkSpeed : Vector3.zero;

        public static TrooperUnit Spawn(DesertMap map, Vector3 position, bool rocket, int seed)
        {
            var go = new GameObject(rocket ? "RocketTrooper" : "Trooper");
            position.y = map.Height(position);
            go.transform.position = position;
            var u = go.AddComponent<TrooperUnit>();
            u.map = map;
            u.rocket = rocket;
            u.Kind = rocket ? UnitKind.RocketTrooper : UnitKind.Infantry;
            u.SetHealth(rocket ? 32f : 22f);
            u.Radius = 0.8f;
            u.facing = seed * 47f;
            u.readyTick = SimulationDriver.Instance.Tick + SimulationDriver.SecondsToTicks(1f + (seed % 5) * 0.3f);
            MissionArt.Attach(go.transform, MissionArt.Cached(rocket ? "TrooperRocket" : "Trooper", (w, g) => MissionArt.TrooperBody(w, g, rocket)), MissionLayers.SoftUnit);
            u.legL = MissionArt.Pivot(go.transform, "LegL", new Vector3(-0.13f, 0.98f, 0f));
            u.legR = MissionArt.Pivot(go.transform, "LegR", new Vector3(0.13f, 0.98f, 0f));
            var leg = MissionArt.Cached("TrooperLeg", MissionArt.TrooperLeg);
            MissionArt.Attach(u.legL, leg, MissionLayers.SoftUnit);
            MissionArt.Attach(u.legR, leg, MissionLayers.SoftUnit);
            u.rb = UnitBody.Add(go, new Vector3(0f, 1.1f, 0f), 0.75f);
            u.CollectRenderers();
            return u;
        }

        protected override void Step(int tick)
        {
            float dt = SimulationDriver.FixedStep;
            if (tick % 3 == 0 && CheckSquash()) return;
            if (tick >= retargetTick || target == null)
            {
                target = FindHostileCar(AimPoint, 110f, false);
                retargetTick = tick + 40;
            }
            walking = false;
            Vector3 pos = transform.position;
            if (target != null)
            {
                Vector3 to = target.transform.position - pos; to.y = 0f;
                float dist = to.magnitude;
                facing = Mathf.MoveTowardsAngle(facing, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 220f * dt);
                bool inRange = dist < (rocket ? 55f : 38f);
                if (!inRange && tick >= aimUntil)
                {
                    walking = true;
                    pos += to / Mathf.Max(0.01f, dist) * WalkSpeed * dt;
                }
                else if (inRange) TryShoot(tick, dist);
            }
            pos.y = map.Height(pos);
            rb.MovePosition(pos);
            rb.MoveRotation(Quaternion.Euler(0f, facing, 0f));
        }

        private void TryShoot(int tick, float dist)
        {
            if (tick < readyTick) return;
            Vector3 muzzle = transform.position + Vector3.up * 1.4f + transform.forward * 0.8f;
            Vector3 aim = target.transform.position + Vector3.up * 0.5f;
            if (Physics.Linecast(muzzle, aim, TargetFinder.ObstacleMask, QueryTriggerInteraction.Ignore)) { readyTick = tick + 25; return; }
            Vector3 v = target.Body != null ? target.Body.linearVelocity : Vector3.zero;
            if (rocket)
            {
                if (aimUntil < 0) { aimUntil = tick + SimulationDriver.SecondsToTicks(0.9f); return; }   // shoulder the tube: the tell
                if (tick < aimUntil) return;
                aimUntil = -1;
                Vector3 lead = Lead(muzzle, aim, v * 0.7f, 38f, false);
                FireShell(muzzle + Vector3.up * 0.3f, (lead - muzzle).normalized * 38f, 16f, tick, ProjectileKind.Shell, PrimaryWeaponType.HomingRocket, 5500f);
                readyTick = tick + SimulationDriver.SecondsToTicks(5.5f);
                return;
            }
            if (burstLeft <= 0) burstLeft = 3;
            Vector3 point = Lead(muzzle, aim, v, 80f, false);
            FireShell(muzzle, (point - muzzle).normalized * 80f, 3f, tick, ProjectileKind.MachineGun, PrimaryWeaponType.MachineGun, 500f);
            burstLeft--;
            readyTick = tick + SimulationDriver.SecondsToTicks(burstLeft > 0 ? 0.14f : 2.2f);
        }

        // Rolled over by a car of the other side.
        private bool CheckSquash()
        {
            var director = RaceDirector.Current;
            if (director == null) return false;
            Vector3 p = transform.position;
            foreach (var car in director.Cars)
            {
                if (car == null || !car.isActiveAndEnabled || Mathf.Abs(car.SpeedKmh) < 12f) continue;
                Vector3 d = car.transform.position - p;
                if (Mathf.Abs(d.y) > 2.2f || d.x * d.x + d.z * d.z > 2.4f * 2.4f) continue;
                if (!Teams.Hostile(gameObject, car.gameObject)) continue;
                TakeHit(car.gameObject, 999f, p + Vector3.up);
                return true;
            }
            return false;
        }

        protected override void OnDied(GameObject attacker)
        {
            var pool = EffectPool.Current;
            if (pool != null) { pool.PlayShellImpact(AimPoint); pool.PlaySparks(AimPoint); }
            if (Application.isPlaying) SfxPlayer.Instance.Play(SfxSynth.HitMetal, AimPoint, 0.7f, 1.3f);
            Tint(new Color(0.15f, 0.12f, 0.1f));
            walking = false;
        }

        protected override float DespawnSeconds => 1.6f;

        protected override void StepDying(int tick)
        {
            // Topples over and sinks into the sand.
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, Quaternion.Euler(80f, facing, 0f), 300f * SimulationDriver.FixedStep));
            rb.MovePosition(transform.position + Vector3.down * 0.5f * SimulationDriver.FixedStep);
        }

        private void Update()
        {
            if (!Alive || legL == null) return;
            stride += walking ? Time.deltaTime * 7f : 0f;
            float swing = walking ? Mathf.Sin(stride) * 28f : 0f;
            legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
            legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        }
    }

    /// <summary>Outpost gun on the sand: laser lock, then a three-shell burst with lead. Static, solid, destructible.</summary>
    public sealed class GroundTurretUnit : MissionUnit
    {
        private const float Range = 95f, ShellSpeed = 72f;
        private Transform head, muzzle;
        private LineRenderer laser;
        private CarController target;
        private int readyTick, fireAtTick = -1, shotsLeft, lastTargetId;
        private Vector3 aimPoint;

        public override Vector3 AimPoint => head.position + Vector3.up * 0.4f;

        public static GroundTurretUnit Spawn(DesertMap map, Vector3 position, Vector3 facing)
        {
            var go = new GameObject("OutpostTurret");
            position.y = map.Height(position) - 0.2f;
            facing.y = 0f;
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing.sqrMagnitude > 0.01f ? facing : Vector3.forward));
            var t = go.AddComponent<GroundTurretUnit>();
            t.Kind = UnitKind.Turret;
            t.SetHealth(170f);
            t.Radius = 2f;
            MissionArt.Attach(go.transform, MissionArt.Cached("TurretBase", MissionArt.TurretBase), 0);
            t.head = MissionArt.Pivot(go.transform, "Head", new Vector3(0f, 1.6f, 0f));
            MissionArt.Attach(t.head, MissionArt.Cached("TurretHead", MissionArt.TurretHead), 0);
            t.muzzle = MissionArt.Pivot(t.head, "Muzzle", new Vector3(0f, 0.4f, 2.8f));
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.1f, 0f);
            box.size = new Vector3(4.4f, 2.4f, 4.4f);
            t.laser = new GameObject("AimLaser").AddComponent<LineRenderer>();
            t.laser.transform.SetParent(go.transform, false);
            t.laser.sharedMaterial = MissionArt.MarkerRed;
            t.laser.positionCount = 2;
            t.laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.laser.enabled = false;
            t.CollectRenderers();
            return t;
        }

        protected override void Step(int tick)
        {
            if (tick < readyTick) return;
            if (shotsLeft <= 0)
            {
                if (target == null)
                {
                    target = FindHostileCar(muzzle.position, Range, true, lastTargetId);
                    if (target == null) { readyTick = tick + 25; return; }
                    fireAtTick = tick + SimulationDriver.SecondsToTicks(1f);
                }
                var h = target.GetComponent<CarHealth>();
                if (!target.isActiveAndEnabled || (h != null && (h.IsDead || h.IsInvulnerable))) { target = null; return; }
            }
            if (target == null) { shotsLeft = 0; return; }
            aimPoint = Lead(muzzle.position, target.transform.position + Vector3.up * 0.6f, target.Body != null ? target.Body.linearVelocity : Vector3.zero, ShellSpeed);
            Vector3 flat = transform.InverseTransformDirection(aimPoint - head.position); flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f) head.localRotation = Quaternion.RotateTowards(head.localRotation, Quaternion.LookRotation(flat), 130f * SimulationDriver.FixedStep);
            if (tick < fireAtTick) return;
            if (shotsLeft <= 0) shotsLeft = 3;
            FireShell(muzzle.position, (aimPoint - muzzle.position).normalized * ShellSpeed, 9f, tick, impact: 3500f);
            shotsLeft--;
            if (shotsLeft > 0) { readyTick = tick + SimulationDriver.SecondsToTicks(0.14f); return; }
            lastTargetId = target.CarId;
            target = null;
            fireAtTick = -1;
            readyTick = tick + SimulationDriver.SecondsToTicks(2.6f);
        }

        protected override void OnDied(GameObject attacker)
        {
            Explode(AimPoint, 1.4f);
            Tint(new Color(0.15f, 0.12f, 0.1f));
            laser.enabled = false;
            head.localRotation *= Quaternion.Euler(25f, 0f, 12f);
        }

        protected override float DespawnSeconds => 1e5f;   // the wreck stays as cover

        protected override void LateUpdate()
        {
            base.LateUpdate();
            bool show = Alive && Running && target != null && fireAtTick > 0;
            laser.enabled = show;
            if (!show) return;
            laser.SetPosition(0, muzzle.position);
            laser.SetPosition(1, target.transform.position + Vector3.up * 0.6f);
            laser.startWidth = laser.endWidth = 0.05f + 0.05f * Mathf.PingPong(Time.time * 8f, 1f);
        }
    }

    /// <summary>
    /// Drone bomber on one straight pass: the director lays the strike telegraphs along its line when the raid is
    /// announced; the plane itself only flies, can be shot down (low pass, ~28 m) and crashes with a blast.
    /// </summary>
    public sealed class PlaneUnit : MissionUnit
    {
        public const float Speed = 58f, Altitude = 28f;
        private DesertMap map;
        private Rigidbody rb;
        private Vector3 velocity;
        private Vector3 end;
        private bool crashing;

        public override Vector3 Velocity => velocity;
        public override Vector3 AimPoint => transform.position;

        public static PlaneUnit Spawn(DesertMap map, Vector3 start, Vector3 end)
        {
            var go = new GameObject("Bomber");
            start.y = map.Height(start) + Altitude;
            go.transform.SetPositionAndRotation(start, Quaternion.LookRotation(end - start));
            var p = go.AddComponent<PlaneUnit>();
            p.map = map;
            p.end = end;
            p.Kind = UnitKind.Plane;
            p.SetHealth(80f);
            p.Radius = 5f;
            var dir = end - start; dir.y = 0f;
            p.velocity = dir.normalized * Speed;
            MissionArt.Attach(go.transform, MissionArt.Cached("Plane", MissionArt.Plane), MissionLayers.SoftUnit);
            p.rb = UnitBody.Add(go, Vector3.zero, 4f);
            p.CollectRenderers();
            return p;
        }

        protected override void Step(int tick)
        {
            float dt = SimulationDriver.FixedStep;
            Vector3 pos = transform.position + velocity * dt;
            float h = map.Height(pos) + Altitude;
            pos.y = Mathf.MoveTowards(pos.y, Mathf.Max(h, pos.y - 2f * dt), 8f * dt);
            rb.MovePosition(pos);
            Vector3 flat = end - pos; flat.y = 0f;
            Hidden = !map.Inside(pos);
            if (Vector3.Dot(flat, velocity) < 0f && Hidden) Remove();   // flown off the map
        }

        protected override void OnDied(GameObject attacker)
        {
            Explode(transform.position, 1.6f);
            Tint(new Color(0.15f, 0.12f, 0.1f));
            crashing = true;
        }

        protected override float DespawnSeconds => 8f;

        protected override void StepDying(int tick)
        {
            if (!crashing) return;
            float dt = SimulationDriver.FixedStep;
            velocity += Physics.gravity * 0.7f * dt;
            Vector3 pos = transform.position + velocity * dt;
            rb.MoveRotation(rb.rotation * Quaternion.Euler(20f * dt, 0f, 160f * dt));
            if (pos.y <= map.Height(pos) + 0.5f)
            {
                crashing = false;
                pos.y = map.Height(pos);
                Explode(pos, 3f);
                Blast(gameObject, pos, 9f, 30f, 7f);   // the wreck lands where it lands: a telegraph-free but rare hazard
                DespawnTick = tick + SimulationDriver.SecondsToTicks(1f);
            }
            rb.MovePosition(pos);
        }
    }
}
