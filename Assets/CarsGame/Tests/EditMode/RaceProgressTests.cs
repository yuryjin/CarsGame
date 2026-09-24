using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Track;

namespace RacingGame.Tests
{
    public class RaceProgressTests
    {
        private GameObject trackObject;
        private GameObject carObject;
        private TrackPath path;
        private RaceProgress progress;

        [SetUp]
        public void SetUp()
        {
            trackObject = new GameObject("RaceProgressTrack");
            const int points = 40;
            for (int i = 0; i < points; i++)
            {
                float angle = i * Mathf.PI * 2f / points;
                var marker = new GameObject("Marker");
                marker.transform.SetParent(trackObject.transform, false);
                marker.transform.position = new Vector3(Mathf.Cos(angle) * 100f, 0f, Mathf.Sin(angle) * 100f);
            }
            path = trackObject.AddComponent<TrackPath>();
            path.startLineDistance = 60f;

            carObject = new GameObject("RaceProgressCar");
            progress = carObject.AddComponent<RaceProgress>();
            progress.gateCount = 8;
            progress.maxDeltaPerTick = 6f;
            progress.wrongWaySeconds = 0.1f;
            progress.Resync();
            progress.enabled = true;
            RaceClock.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            RaceClock.Reset();
            if (carObject != null) Object.DestroyImmediate(carObject);
            if (trackObject != null) Object.DestroyImmediate(trackObject);
        }

        [Test]
        public void ProgressCompletesOneLapAndReportsItsClockTime()
        {
            float start = path.startLineDistance - 10f;
            progress.transform.position = path.Position(start);
            progress.BeginRace();
            var completed = new List<double>();
            progress.LapCompleted += (_, time) => completed.Add(time);

            float end = start + path.Length + 12f;
            for (float distance = start; distance <= end; distance += 3f)
            {
                progress.transform.position = path.Position(distance);
                RaceClock.Advance(SimulationDriver.FixedStep);
                progress.SimulationStep(0);
            }

            Assert.AreEqual(1, progress.LapsCompleted);
            Assert.AreEqual(1, completed.Count);
            Assert.AreEqual(completed[0], progress.LastLapTime, 1e-6);
            Assert.AreEqual(completed[0], progress.BestLapTime, 1e-6);
            Assert.Greater(progress.TotalProgress, path.Length);
        }

        [Test]
        public void SustainedReverseMotionRaisesWrongWayAndForwardMotionClearsIt()
        {
            const float start = 200f;
            progress.transform.position = path.Position(start);
            progress.BeginRace();

            for (int i = 1; i <= 6; i++)
            {
                progress.transform.position = path.Position(start - i * 0.5f);
                progress.SimulationStep(i);
            }
            Assert.IsTrue(progress.WrongWay);

            progress.transform.position = path.Position(start - 2.5f + 0.5f);
            progress.SimulationStep(7);
            Assert.IsFalse(progress.WrongWay);
        }

        [Test]
        public void NotifyTeleportResynchronizesWithoutCreditingALap()
        {
            const float start = 45f;
            progress.transform.position = path.Position(start);
            progress.BeginRace();
            progress.transform.position = path.Position(start + path.Length * 0.75f);
            progress.NotifyTeleport();
            progress.transform.position = path.Position(start + path.Length * 0.75f + 2f);
            progress.SimulationStep(1);

            Assert.AreEqual(0, progress.LapsCompleted);
            Assert.AreEqual(0, progress.GatesPassedThisLap);
            Assert.IsFalse(progress.WrongWay);
        }

        [Test]
        public void StartGridPlacesMirroredColumnsBehindTheLineAndFacesForward()
        {
            var gridObject = new GameObject("StartGridTest");
            try
            {
                var grid = gridObject.AddComponent<StartGrid>();
                grid.GetSlot(0, path, out var poleLeft, out var poleRotation, out float leftLane);
                grid.GetSlot(1, path, out var poleRight, out var rightRotation, out float rightLane);
                grid.GetSlot(2, path, out var secondRow, out _, out _);

                Assert.AreEqual(-grid.columnOffset, leftLane);
                Assert.AreEqual(grid.columnOffset, rightLane);
                Assert.AreEqual(grid.dropHeight, poleLeft.y - path.Position(path.startLineDistance - grid.poleSetback).y, 1e-4f);
                Assert.AreEqual(2f * grid.columnOffset, Vector3.Distance(poleLeft, poleRight), 1e-3f);
                int hint = -1;
                float poleDistance = path.Project(poleLeft, ref hint);
                hint = -1;
                float secondRowDistance = path.Project(secondRow, ref hint);
                Assert.AreEqual(grid.rowSpacing, Mathf.Abs(path.SignedDelta(poleDistance, secondRowDistance)), 0.2f);
                Vector3 tangent = path.Tangent(path.startLineDistance - grid.poleSetback);
                Assert.Greater(Vector3.Dot(poleRotation * Vector3.forward, tangent), 0.999f);
                Assert.Greater(Vector3.Dot(rightRotation * Vector3.forward, tangent), 0.999f);
            }
            finally { Object.DestroyImmediate(gridObject); }
        }
    }
}
