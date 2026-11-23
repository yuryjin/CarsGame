using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RacingGame.Tests
{
    public class PrivateRoomMenuPlayTests
    {
        private sealed class Room : IPrivateRoomLink
        {
            public RoomState State { get; set; }
            public uint Code => 54321098;
            public int Seat { get; set; }
            public int Members => 3;
            public bool Fortress => false;
            public int[] FinishOrder { get; } = new[] { 0, 2, 1, 3 };
            public int FinishedSeats => 7;
            public int[] Shots { get; } = new int[4];
            public float[] CoreDamage { get; } = new float[4];
            public int Outcome => 0;
            public void Create(bool fortress) { State = RoomState.Connecting; }
            public void Join(uint code) { }
            public void StartMatch() { }
            public void Leave() { State = RoomState.Offline; }
            public RaceResults ReportedResult { get; private set; }
            public void ReportFinish(RaceResults result) => ReportedResult = result;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator AFinishedHostWaitsForTheSecondDriverBeforeShowingRoomResults()
        {
            float previousClock = Time.captureDeltaTime;
            var saved = GameFlow.Room;
            GameFlow flow = null;
            try
            {
                Time.captureDeltaTime = SimulationDriver.FixedStep;
                yield return SceneManager.LoadSceneAsync("SampleScene");
                while (!PlatformGlue.IsReady) yield return null;
                yield return null;
                flow = Object.FindAnyObjectByType<GameFlow>();
                var race = flow.GetComponent<RaceManager>();
                var room = new Room { State = RoomState.Playing };
                GameFlow.Room = room;
                Assert.IsTrue(flow.StartPrivateMatch(12345));

                var guest = race.Participants[1];
                var driving = guest.Car.InputSource;
                Assert.IsInstanceOf<AiDriver>(driving);
                var parked = guest.gameObject.AddComponent<ScriptedDriverInput>();
                parked.Set(0f, 0f, brakeValue: true);
                // The live room replaces this seat's grid AI with the connected driver's input.
                guest.Car.SetInputSource(parked);
                Object.FindAnyObjectByType<DebugBus>().Execute("autopilot", "on");
                float deadline = Time.realtimeSinceStartup + 110f;
                while (!race.HasFinished(race.Player) && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsTrue(race.HasFinished(race.Player));
                Assert.Greater(parked.SamplesTaken, 0);
                Assert.IsTrue(parked.LastCommand.Brake);
                double hostTime = race.PlayerElapsedTime;

                // Give the solo spectator grace period time to expire with the guest still on the grid.
                while (race.State == RaceState.Finishing && race.RaceTime < hostTime + race.finishGraceSeconds + .5d)
                    yield return null;
                Assert.IsFalse(race.HasFinished(guest));
                Assert.AreEqual(0, guest.Progress.LapsCompleted);
                Assert.IsFalse(race.ShowResultsAfterFinish());
                Assert.IsNull(race.PhotoFirst);
                Assert.AreEqual(RaceState.Finishing, race.State);
                Assert.IsNull(room.ReportedResult);

                guest.Car.SetInputSource(driving);
                deadline = Time.realtimeSinceStartup + 110f;
                while (room.ReportedResult == null && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.NotNull(room.ReportedResult);
                var guestResult = room.ReportedResult.Entries.Find(entry => entry.CarId == guest.Car.CarId);
                Assert.NotNull(guestResult);
                Assert.IsTrue(guestResult.Finished);
                Assert.AreEqual(race.laps, guestResult.Laps);
                Assert.Greater(guestResult.TotalTime, hostTime);
                Assert.AreEqual(RaceState.Finished, race.State);
                TestContext.WriteLine($"Host finished at {hostTime:F3}s. Guest finished at {guestResult.TotalTime:F3}s after {parked.SamplesTaken} stationary input samples.");
            }
            finally
            {
                if (flow != null) flow.EnterMenu();
                GameFlow.Room = saved;
                Time.captureDeltaTime = previousClock;
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator GuestTracksTheReceivedCarThroughTheCircuitAndUsesHostGates()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            while (!PlatformGlue.IsReady) yield return null;
            yield return null;
            var flow = Object.FindAnyObjectByType<GameFlow>();
            var race = flow.GetComponent<RaceManager>();
            var saved = GameFlow.Room;
            try
            {
                GameFlow.Room = new Room { State = RoomState.Playing, Seat = 1 };
                Assert.IsTrue(flow.StartPrivateMatch(1234));
                race.ApplyRoomClock(RaceState.Racing,0,1f);
                var p = race.Player;
                var path = p.Progress.Path;
                float distance = path.Wrap(p.Progress.Distance + path.Length * .22f);
                path.Pose(distance,0f,out var at,out var facing);
                p.Car.ApplyRoomPose(at,facing,75f);
                p.Car.SimulationStep(SimulationDriver.Instance.Tick);
                Assert.That(Mathf.Abs(path.SignedDelta(distance,p.Progress.Distance)),Is.LessThan(1f));
                Assert.AreEqual(0,p.Progress.LapsCompleted);
                p.Progress.ApplyRoomStanding(1,2,false);
                Assert.AreEqual(1,p.Progress.LapsCompleted);
                Assert.AreEqual(2,p.Progress.GatesPassedThisLap);
                Assert.IsFalse(race.HasFinished(p));
                p.offTrackResetSeconds = SimulationDriver.FixedStep;
                p.Car.ApplyRoomPose(at + Vector3.up * 30f,facing,75f);
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                Assert.AreEqual(0,p.AutoResets, "Only the host can reset a car reported off the road");
                var received = p.Car.transform.position;
                p.Car.ResetCar();
                Assert.AreEqual(received, p.Car.transform.position);
                Assert.IsTrue(p.Car.ReadRoomInput(100).ResetEdge);
                Assert.IsFalse(p.Car.ReadRoomInput(101).ResetEdge);
            }
            finally { flow.EnterMenu(); GameFlow.Room = saved; }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator FocusSurvivesConnectingLobbyAndResults()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            while (!PlatformGlue.IsReady) yield return null;
            yield return null;
            var menu = Object.FindAnyObjectByType<MenuScreens>();
            var saved = GameFlow.Room;
            var room = new Room();
            try
            {
                GameFlow.Room = room;
                menu.Show("rooms");
                EventSystem.current.SetSelectedGameObject(GameObject.Find("CreateRaceRoom"));
                room.Create(false);
                menu.Show("rooms");
                yield return null;
                Assert.AreEqual("Back",EventSystem.current.currentSelectedGameObject.name);
                room.State = RoomState.Lobby;
                menu.Show("rooms");
                Assert.AreEqual("54321098",GameObject.Find("RoomInvite").GetComponent<TMP_InputField>().text);
                Assert.IsTrue(GameObject.Find("RoomStart").GetComponent<Button>().interactable);
                room.State = RoomState.Results;
                menu.Show("rooms");
                Assert.That(GameObject.Find("RoomPlace3").GetComponentInChildren<TMP_Text>().text,Does.Contain(Loc.Get("results.dnf")));
            }
            finally { GameFlow.Room = saved; menu.Show("main"); }
        }
    }
}
