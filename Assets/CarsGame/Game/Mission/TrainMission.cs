using System.Collections.Generic;
using RacingGame.Core.Sim;
using UnityEngine;

namespace RacingGame
{
    public enum TrainOutcome { Running, CapturedOnTrain, CapturedAtStation, Escaped }

    public sealed class TrainChaseRules
    {
        public TrainOutcome Outcome { get; private set; }
        public bool Diverted { get; private set; }
        private static readonly int RequiredTicks = SimulationDriver.SecondsToTicks(TrainMission.HoldSeconds);
        private int holdTicks;
        public float CaptureProgress => (float)holdTicks / RequiredTicks;

        public bool TryDivert(bool playerAtSwitch, bool trainPassedSwitch)
        {
            if (!playerAtSwitch || trainPassedSwitch || Diverted || Outcome != TrainOutcome.Running) return false;
            Diverted = true;
            return true;
        }

        public void Step(bool boarding, bool collectedAtStation, bool trainAtBorder)
        {
            if (Outcome != TrainOutcome.Running) return;
            holdTicks = boarding && !Diverted ? holdTicks + 1 : 0;
            // A completed transfer on the border tick puts the trophy in the player's car.
            if (holdTicks >= RequiredTicks) Outcome = TrainOutcome.CapturedOnTrain;
            else if (Diverted && collectedAtStation) Outcome = TrainOutcome.CapturedAtStation;
            else if (trainAtBorder) Outcome = TrainOutcome.Escaped;
        }
    }

    public sealed class TrainMission : MonoBehaviour, ISimulated, IBattleSite
    {
        public const string EventId = "m2";
        public const float HoldSeconds = 3f;
        public static TrainMission Current { get; private set; }
        public RaceManager Race { get; private set; }
        public TrainRoute Route { get; private set; }
        public TrainEscort Escort { get; private set; }
        public TrainOutcome Outcome => rules.Outcome;
        public bool Diverted => rules.Diverted;
        public float CaptureProgress => rules.CaptureProgress;
        public int SimulationOrder => int.MaxValue - 6000;

        private readonly TrainChaseRules rules = new TrainChaseRules();
        private readonly List<GameObject> hidden = new List<GameObject>();

        public static TrainMission Create(RaceManager race)
        {
            var go = new GameObject("TrainMission");
            var mission = go.AddComponent<TrainMission>();
            Current = mission;
            mission.Race = race;
            mission.Route = go.AddComponent<TrainRoute>();
            mission.Route.Build(AbyssTrack.SceneryMaterial());
            AbyssTrack.HideScenery(mission.hidden, AbyssTrack.Scenery);
            AbyssTrack.HideScenery(mission.hidden, new[] { "Environment/Mountains" });
            mission.Escort = go.AddComponent<TrainEscort>();
            if (race.spawner != null) race.spawner.Site = mission;
            race.OnFieldSpawned += mission.OnFieldSpawned;
            return mission;
        }

        private void OnFieldSpawned()
        {
            if (Race.Player != null) Teams.Set(Race.Player.gameObject, Team.Players);
            Escort.Initialize(Route, Race);
        }

        public void SimulationStep(int tick)
        {
            if (Race == null || Race.State != RaceState.Racing || PauseController.IsPaused || Outcome != TrainOutcome.Running) return;
            Route.Step(SimulationDriver.FixedStep);
            var participant = Race.Player;
            var car = participant != null && participant.Health != null && !participant.Health.IsDead ? participant.Car : null;
            Escort.Step(car);
            if (rules.TryDivert(car != null && Route.InSwitchZone(car.transform.position), Route.PassedSwitch)) Route.Divert();
            rules.Step(car != null && Escort.ApproachClear && Escort.PlayerAtTail,
                Route.AtStation && car != null && Route.InStationZone(car.transform.position), Route.AtBorder);
            if (Outcome == TrainOutcome.Running) return;
            bool captured = Outcome != TrainOutcome.Escaped;
            if (captured) Route.CollectTrophy(car.transform);
            Race.EndMission(captured);
        }

        public void GetSpawnPose(int slot, int total, out Vector3 position, out Quaternion rotation)
        {
            var pose = Route.StartPose;
            position = pose.position;
            rotation = pose.rotation;
        }

        public bool TryGetRecoveryPose(CarController car, out Vector3 position, out Quaternion rotation)
        {
            var pose = car != null ? Route.RecoveryPose(car.transform.position) : Route.StartPose;
            position = pose.position;
            rotation = pose.rotation;
            return true;
        }

        // The event has one race participant. Its single enemy is driven by TrainEscort.
        public ICarInputSource CreateBotDriver(GameObject car, AiDriverProfile profile) => null;

        private void OnEnable() => SimulationDriver.Instance.Register(this);
        private void OnDisable() { var driver = SimulationDriver.Current; if (driver != null) driver.Unregister(this); }
        private void OnDestroy() { if (Current == this) Dispose(); }

        public void Dispose()
        {
            if (Race != null)
            {
                Race.OnFieldSpawned -= OnFieldSpawned;
                if (Race.spawner != null && ReferenceEquals(Race.spawner.Site, this)) Race.spawner.Site = null;
            }
            gameObject.SetActive(false);
            foreach (var obj in hidden) if (obj != null) obj.SetActive(true);
            hidden.Clear();
            if (Current == this) Current = null;
        }
    }
}
