using System.Text;
using RacingGame.Core.Sim;
using UnityEngine;

namespace RacingGame
{
    // TEMPORARY diagnostic (not for commit): ring buffer of the last 2 s of driving, dumped on a hard barrier hit.
    public sealed class CrashProbe : MonoBehaviour
    {
        private struct S { public int tick; public float s, lat, kmh, want, thr, steer, curv, lane, tlane, slip, hp; public bool brake, boost, grounded; public GroundSurface surf; }
        private readonly S[] buf = new S[100];
        private int head, count;
        private CarController car; private AiDriver ai; private RaceParticipant part; private Rigidbody rb;

        private void Awake() { car = GetComponent<CarController>(); ai = GetComponent<AiDriver>(); part = GetComponent<RaceParticipant>(); rb = GetComponent<Rigidbody>(); }

        private void FixedUpdate()
        {
            if (part == null || part.Progress == null || part.Progress.Path == null) return;
            var g = part.Progress; var c = car.LastCommand;
            buf[head] = new S
            {
                tick = SimulationDriver.Instance.Tick, s = g.Distance, lat = g.Path.Spline.LateralOffset(transform.position, g.Distance),
                kmh = car.SpeedKmh, want = ai != null ? ai.LastDesiredSpeed : -1f, thr = c.Throttle, steer = c.Steer, brake = c.Brake,
                curv = ai != null ? ai.LastCurvature : -1f, lane = ai != null ? ai.CurrentLane : 0f, tlane = ai != null ? ai.TargetLane : 0f,
                slip = car.SideSlipSpeed, boost = car.IsBoosting, grounded = car.IsGrounded, surf = car.Surface, hp = part.Health != null ? part.Health.Health : -1f
            };
            head = (head + 1) % buf.Length; if (count < buf.Length) count++;
        }

        private void OnCollisionEnter(Collision col)
        {
            if (!col.collider.name.StartsWith("Barrier_")) return;
            float impact = col.impulse.magnitude / rb.mass;
            if (impact < 8f) return;
            var sb = new StringBuilder($"[CRASH] {name} -> {col.collider.name} impact={impact:0.0} ai={(ai != null && ai.enabled)}\n");
            for (int k = count - 1; k >= 0; k -= 8)
            {
                var x = buf[(head - 1 - k + buf.Length * 2) % buf.Length];
                sb.Append($"  t-{k * 0.02f:0.00}s s={x.s:0} lat={x.lat:0.0} lane={x.lane:0.0}/{x.tlane:0.0} kmh={x.kmh:0} want={x.want:0} thr={x.thr:0.00} st={x.steer:0.00} br={x.brake} curv={x.curv:0.0} slip={x.slip:0.0} boost={x.boost} gnd={x.grounded} {x.surf} hp={x.hp:0}\n");
            }
            Debug.Log(sb.ToString());
        }
    }
}

namespace RacingGame
{
    // TEMPORARY: open-loop turn test. Throttle holds targetKmh, steer is fixed; logs steady yaw rate and radius.
    public sealed class TurnTest : MonoBehaviour, ICarInputSource
    {
        public float targetKmh = 40f, steer = 1f;
        public string result = "";
        private CarController car; private int t0 = -1; private float yawSum; private int n;
        private void Awake() { car = GetComponent<CarController>(); }
        public DriveCommand Sample(int tick)
        {
            float kmh = car.SpeedKmh;
            if (t0 < 0 && kmh >= targetKmh - 1f) t0 = tick;
            float s = t0 >= 0 ? steer : 0f;
            if (t0 >= 0 && tick - t0 > 100 && tick - t0 <= 250) { yawSum += Mathf.Abs(car.Body.angularVelocity.y); n++; }
            if (t0 >= 0 && tick - t0 == 251)
            {
                float w = yawSum / n, v = car.Body.linearVelocity.magnitude;
                result = $"kmh={kmh:0} yaw={w * Mathf.Rad2Deg:0.0}deg/s radius={v / Mathf.Max(w, 1e-3f):0.0}m curv={w * Mathf.Rad2Deg / Mathf.Max(v, 0.1f):0.00}deg/m surf={car.Surface}";
                Debug.Log("[TURN] " + result);
            }
            return new DriveCommand { Tick = tick, Throttle = kmh < targetKmh ? 1f : 0f, Steer = s };
        }
    }
}
