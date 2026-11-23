using System;
using System.Collections.Generic;
using RacingGame.Core.Save;
using RacingGame.Core.Sim;
using UnityEngine;

namespace RacingGame
{
    public enum MeasurementPhase { Settling, Accelerating, Braking, Slalom, Missed, Finished }

    public sealed class BodyMeasurementCourse : MonoBehaviour, ISimulated, ICarInputSource
    {
        public const float TargetKmh = 45f, GateHalfWidth = 1.8f;
        public const int Gates = 5;
        public int SimulationOrder => 1400;
        public MeasurementPhase Phase { get; private set; }
        public int NextGate { get; private set; }
        public int AccelerationMs { get; private set; }
        public int BrakingCm { get; private set; }
        public int SlalomMs { get; private set; }
        private RaceParticipant driver;
        private ICarInputSource original;
        private TrackPath path;
        private string conditions;
        private float startDistance, brakeDistance;
        private float lastSpeed, runStart, lastTime, slalomStart = -1;
        private int settleUntil;
        private Vector3 previous;
        private bool positioning;

        public void Begin(RaceParticipant participant, TrackPath route, ProgressData progress)
        {
            driver = participant; path = route; original = driver.Car.InputSource;
            conditions = BodyMeasurement.Conditions(progress);
            startDistance = path.startLineDistance + 20f;
            PlaceAtStart();
            driver.Car.Teleported += OnTeleported;
            driver.Car.SetInputSource(this);
            settleUntil = SimulationDriver.Instance.Tick + SimulationDriver.SecondsToTicks(.8f);
            SimulationDriver.Instance.Register(this);
            for (int i = 0; i < Gates; i++)
            {
                float d = GateDistance(i);
                var center = GateCenter(i);
                for (int side = -1; side <= 1; side += 2)
                {
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    post.name = "Gate" + i + (side < 0 ? "L" : "R");
                    post.transform.SetParent(transform, false);
                    post.transform.position = center + path.Right(d) * (side * GateHalfWidth) + Vector3.up * .85f;
                    post.transform.localScale = new Vector3(.38f, .85f, .38f);
                    var collider = post.GetComponent<Collider>(); collider.enabled = false;
                    var paint = new MaterialPropertyBlock();
                    paint.SetColor("_BaseColor", side < 0 ? new Color(.12f,.8f,1f) : new Color(1f,.65f,.04f));
                    paint.SetColor("_Color", side < 0 ? Color.cyan : Color.yellow);
                    post.GetComponent<Renderer>().SetPropertyBlock(paint);
                }
            }
        }

        public float GateDistance(int gate) => startDistance + 15f + 16f * gate;
        public Vector3 GateCenter(int gate) => path.Position(GateDistance(gate)) + path.Right(GateDistance(gate)) * ((gate & 1) == 0 ? -2.15f : 2.15f);

        private void PlaceAtStart()
        {
            path.Pose(startDistance, 0f, out var origin, out var facing);
            positioning = true;
            driver.Car.Teleport(origin + Vector3.up * .45f, facing);
            positioning = false;
            previous = driver.Car.Body.position; lastSpeed = 0f;
        }

        public DriveCommand Sample(int tick) => new DriveCommand
        {
            Tick = tick, Throttle = Phase == MeasurementPhase.Accelerating ? 1f : 0f, Brake = Phase != MeasurementPhase.Accelerating
        };

        private void OnTeleported()
        {
            if (!positioning && Phase < MeasurementPhase.Missed) Fail();
        }

        private void Fail()
        {
            Phase = MeasurementPhase.Missed;
            if (driver?.Car != null && driver.Car.InputSource == this) driver.Car.SetInputSource(original);
        }

        public void SimulationStep(int tick)
        {
            if (driver == null || Phase >= MeasurementPhase.Missed) return;
            float now = tick * SimulationDriver.FixedStep;
            var car = driver.Car;
            Vector3 position = car.Body.position;
            float speed = Mathf.Abs(car.SpeedKmh);
            if (Phase == MeasurementPhase.Settling)
            {
                if (tick >= settleUntil && car.IsGrounded)
                { Phase = MeasurementPhase.Accelerating; runStart = now; }
            }
            else if (Phase == MeasurementPhase.Accelerating && speed >= TargetKmh)
            {
                float fraction = Mathf.InverseLerp(lastSpeed, speed, TargetKmh);
                AccelerationMs = Mathf.RoundToInt((Mathf.Lerp(lastTime, now, fraction) - runStart) * 1000f);
                brakeDistance = Vector3.Distance(Vector3.Lerp(previous, position, fraction), position);
                Phase = MeasurementPhase.Braking;
            }
            else if (Phase == MeasurementPhase.Braking)
            {
                brakeDistance += Vector3.Distance(previous, position);
                if (speed <= .7f)
                {
                    BrakingCm = Mathf.RoundToInt(brakeDistance * 100f);
                    PlaceAtStart();
                    Phase = MeasurementPhase.Slalom;
                    car.SetInputSource(original);
                    position = car.Body.position;
                }
            }
            else if (Phase == MeasurementPhase.Slalom)
            {
                if (slalomStart < 0f && speed >= 1f) slalomStart = now;
                var center = GateCenter(NextGate); float d = GateDistance(NextGate);
                var forward = path.Tangent(d);
                float before = Vector3.Dot(previous - center, forward), after = Vector3.Dot(position - center, forward);
                if (before < 0 && after >= 0)
                {
                    float fraction = -before / (after - before);
                    var crossing = Vector3.Lerp(previous, position, fraction);
                    if (Mathf.Abs(Vector3.Dot(crossing - center, path.Right(d))) > GateHalfWidth) Fail();
                    else if (++NextGate == Gates)
                    {
                        SlalomMs = Mathf.Max(1, Mathf.RoundToInt((Mathf.Lerp(lastTime, now, fraction) - slalomStart) * 1000f));
                        Phase = MeasurementPhase.Finished;
                        var save = PlatformGlue.Save;
                        var row = new BodyMeasurement { body = driver.GetComponent<CarBody>().BodyId, conditions = conditions,
                            accelerationMs = AccelerationMs, brakingCm = BrakingCm, slalomMs = SlalomMs, recordedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                        save.Data.progress.measurements = BodyMeasurement.Merge(save.Data.progress.measurements, new List<BodyMeasurement> { row });
                        save.MarkDirty();
                    }
                }
            }
            if ((Phase == MeasurementPhase.Accelerating || Phase == MeasurementPhase.Braking) && car.IsGrounded && car.Surface != GroundSurface.Track) Fail();
            previous = position; lastSpeed = speed; lastTime = now;
        }

        private void OnDestroy()
        {
            if (SimulationDriver.Current != null) SimulationDriver.Current.Unregister(this);
            if (driver == null || driver.Car == null) return;
            driver.Car.Teleported -= OnTeleported;
            if (driver.Car.InputSource == this) driver.Car.SetInputSource(original);
        }
    }
}
