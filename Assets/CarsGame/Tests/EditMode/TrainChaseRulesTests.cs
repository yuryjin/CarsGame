using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public sealed class TrainChaseRulesTests
    {
        [Test]
        public void CrossingTogetherClosesSwitchAndTheTrainCanEscape()
        {
            var rules = new TrainChaseRules();
            Assert.IsFalse(rules.TryDivert(true, true));
            rules.Step(false, true, true);
            Assert.AreEqual(TrainOutcome.Escaped, rules.Outcome);
            Assert.IsFalse(rules.TryDivert(true, false));
            rules.Step(false, true, false);
            Assert.AreEqual(TrainOutcome.Escaped, rules.Outcome);
        }

        [Test]
        public void SwitchOnlyDivertsOnceAndStationStillRequiresCollection()
        {
            var rules = new TrainChaseRules();
            Assert.IsFalse(rules.TryDivert(false, false));
            Assert.IsTrue(rules.TryDivert(true, false));
            Assert.IsFalse(rules.TryDivert(true, false));
            for (int i = 0; i < 200; i++) rules.Step(true, false, false);
            Assert.AreEqual(0f, rules.CaptureProgress);
            Assert.AreEqual(TrainOutcome.Running, rules.Outcome);
            rules.Step(false, true, false);
            Assert.AreEqual(TrainOutcome.CapturedAtStation, rules.Outcome);
        }

        [TestCase(true, TrainOutcome.CapturedOnTrain)]
        [TestCase(false, TrainOutcome.Escaped)]
        public void OnlyTheFinalContinuousBoardingTickCanBeatTheBorder(bool boardOnLastTick, TrainOutcome expected)
        {
            var rules = new TrainChaseRules();
            int count = SimulationDriver.SecondsToTicks(3f);
            for (int i = 1; i < count; i++) rules.Step(true, false, false);
            Assert.That(rules.CaptureProgress, Is.InRange(0.98f, 0.9999f));
            rules.Step(false, false, false);
            Assert.AreEqual(0f, rules.CaptureProgress);
            for (int i = 1; i < count; i++) rules.Step(true, false, false);
            Assert.AreEqual(TrainOutcome.Running, rules.Outcome);
            rules.Step(boardOnLastTick, false, true);
            rules.Step(false, false, true);
            Assert.AreEqual(expected, rules.Outcome);
        }

        [Test]
        public void EventCanStartOnTheDefaultCarWithOnlyOneRaceParticipant()
        {
            var train = Resources.Load<RaceEventDefinition>("Events/Event_m2");
            Assert.IsNotNull(train);
            Assert.AreEqual(TrainMission.EventId, train.eventId);
            Assert.AreEqual(EventMode.Mission, train.mode);
            Assert.IsEmpty(train.unlockAfterEventId);
            Assert.AreEqual(0, train.aiCountOverride);
            Assert.AreEqual(5, train.StarMask(true, 1, 0, 0));
            Assert.AreEqual(7, train.StarMask(true, 1, 1, 0));
            Assert.AreEqual(0, train.StarMask(false, 1, 0, 0));
        }
    }
}
