using System.Collections;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RacingGame.Tests
{
    public sealed class EventDiscoveryPlayTests
    {
        private string profile;
        private GameFlow flow;

        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (profile != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(profile, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            PauseController.ResetAll();
        }

        private static Button StartButton(string id) => Object.FindObjectsByType<Button>().Single(b => b.name == "Start_" + id);
        private static string CardText(Button start, string name) => start.transform.parent.Find(name).GetComponent<TMP_Text>().text;

        private static void AssertAvailableFirst(GameFlow flow)
        {
            var events = flow.events.Where(e => e != null).OrderBy(e => e.order).ToArray();
            int lastAvailable = -1, firstLocked = int.MaxValue;
            for (int i = 0; i < events.Length; i++)
            {
                var ev = events[i];
                var button = StartButton(ev.eventId);
                var row = button.transform.parent;
                bool available = flow.IsUnlocked(ev);
                Assert.AreEqual(available, button.interactable, ev.eventId + " retains its progression gate");
                Assert.AreEqual((i + 1).ToString("00"), row.Find("Number").GetComponent<TMP_Text>().text,
                    "Display order must not renumber the campaign");
                if (available) lastAvailable = Mathf.Max(lastAvailable, row.GetSiblingIndex());
                else
                {
                    firstLocked = Mathf.Min(firstLocked, row.GetSiblingIndex());
                    var prerequisite = events.Single(e => e.eventId == ev.unlockAfterEventId);
                    StringAssert.Contains(Loc.Get(prerequisite.nameKey), row.Find("Status").GetComponent<TMP_Text>().text,
                        "A reordered list must name the actual prerequisite race");
                }
            }
            Assert.Less(lastAvailable, firstLocked, "Already playable special events must precede locked career races");
            Assert.Less(StartButton("e1").transform.parent.GetSiblingIndex(),
                StartButton("e1").transform.parent.parent.Find("CupsTitle").GetSiblingIndex(),
                "Playable race cards lead the list before longer-term cup goals");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator AvailableModesStayVisibleWithoutGrantingCampaignProgress()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            profile = JsonUtility.ToJson(PlatformGlue.Save.Data);
            flow = Object.FindAnyObjectByType<GameFlow>();
            var menu = Object.FindAnyObjectByType<MenuScreens>();
            var progress = PlatformGlue.Save.Data.progress;
            progress.unlockedEvents.Clear();
            progress.bests.Clear();
            string initial = JsonUtility.ToJson(progress);
            menu.Show("main");
            GameObject.Find("ChooseRace").GetComponent<Button>().onClick.Invoke();
            yield return null; yield return null;
            AssertAvailableFirst(flow);
            Assert.AreEqual(initial, JsonUtility.ToJson(progress), "Browsing cannot unlock or award anything");
            Assert.IsFalse(StartButton("e2").interactable);
            var train = StartButton(TrainMission.EventId);
            Assert.IsTrue(train.interactable);
            var trainCard = train.transform.parent;
            Assert.AreEqual(Loc.Get("events.m2.name"), trainCard.Find("Name").GetComponent<TMP_Text>().text);
            Assert.AreEqual(Loc.Get("train.detail"), trainCard.Find("Stats").GetComponent<TMP_Text>().text);
            StringAssert.Contains(Loc.Get("train.star1"), trainCard.Find("Stars").GetComponent<TMP_Text>().text);
            StringAssert.Contains(Loc.Get("train.star2"), trainCard.Find("Stars").GetComponent<TMP_Text>().text);
            train.onClick.Invoke();
            yield return null;
            Assert.AreEqual(Loc.Get("train.fork"), GameObject.Find("TrainGoal").GetComponent<TMP_Text>().text);
            Assert.IsNull(GameObject.Find("RaceStatus"), "The cup chase has no laps or rival ranking");
            flow.EnterMenu(); yield return null;
            Assert.IsNull(GameObject.Find("TrainObjective"));
            // A controlled saved-unlock fixture verifies that rebuilding promotes the newly available row.
            progress.unlockedEvents.Add("e2");
            menu.Show("events"); yield return null; yield return null;
            AssertAvailableFirst(flow);
            Assert.IsTrue(StartButton("e2").interactable);
            Assert.Less(StartButton("e2").transform.parent.GetSiblingIndex(), StartButton("e16").transform.parent.GetSiblingIndex());
            StartButton("e17").onClick.Invoke();
            yield return null;
            Assert.AreEqual("e17", RaceSetup.Event.eventId);
            Assert.AreEqual(RaceState.Countdown, Object.FindAnyObjectByType<RaceManager>().State,
                "The surfaced mode must start through its actual UI action");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator MainPlayStartsTheDisplayedEventOnTheChosenTier()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            var save = PlatformGlue.Save;
            profile = JsonUtility.ToJson(save.Data);
            flow = Object.FindAnyObjectByType<GameFlow>();
            var menu = Object.FindAnyObjectByType<MenuScreens>();
            save.Data.progress.bests.Clear();
            save.Data.progress.unlockedEvents.Clear();
            save.Data.progress.ledger.Clear();
            menu.Show("main");
            GameObject.Find("Play").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(FlowState.Racing, flow.State);
            Assert.AreEqual("e1", flow.CurrentEvent.eventId);
            flow.EnterMenu();

            save.Local.races = 1;
            save.Data.progress.bests.Add(new Core.Save.EventBest { eventId = "e1", bestTimeMs = 60000 });
            save.Data.progress.unlockedEvents.Add("e2");
            GameObject.Find("ChooseRace").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Tier2").GetComponent<Button>().onClick.Invoke();
            string selectedTier = save.Data.settings.tier;
            GameObject.Find("Back").GetComponent<Button>().onClick.Invoke();
            var next = flow.events.Single(e => e.eventId == "e2");
            Assert.AreEqual(Loc.Get(next.nameKey), GameObject.Find("EventName").GetComponent<TMP_Text>().text);
            GameObject.Find("Play").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(FlowState.Racing, flow.State);
            Assert.AreSame(next, flow.CurrentEvent);
            Assert.AreEqual(selectedTier, RaceSetup.TierId);
            Assert.AreEqual(1, save.Data.Races, "Starting a race cannot grant a finish");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator DifficultyCardsMatchSpawnedFieldsAndCreditedRewards()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null;
            profile = JsonUtility.ToJson(PlatformGlue.Save.Data);
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = flow.GetComponent<RaceManager>();
            var menu = flow.GetComponent<MenuScreens>();
            var culture = CultureInfo.GetCultureInfo(Loc.Language);
            race.countdownSeconds = 0f;
            for (int i = 0; i < flow.tiers.Length; i++)
            {
                foreach (string id in new[] { "e1", "a1" })
                {
                    menu.Show("events");
                    GameObject.Find("Tier" + i).GetComponent<Button>().onClick.Invoke();
                    var start = StartButton(id);
                    string stats = CardText(start, "Stats"), difficulty = CardText(start, "Difficulty");
                    start.onClick.Invoke();
                    Assert.AreSame(flow.tiers[i], race.tier);
                    var field = race.Participants.Where(p => !p.isPlayer).ToArray();
                    var ev = RaceSetup.Event;
                    int expectedCount = id == "a1" ? ev.aiCountOverride : race.tier.aiCount;
                    Assert.AreEqual(expectedCount, field.Length, id + " on " + race.tier.tierId);
                    StringAssert.Contains(Loc.Format(id == "a1" ? "arena.detail" : "events.rivals", "n", field.Length), stats);
                    foreach (var racer in field)
                    {
                        float interval = racer.GetComponent<AiGunner>().fireCooldown;
                        Assert.AreEqual(race.tier.aiFireCooldown, interval);
                        StringAssert.Contains(Loc.Format("events.rival_fire", "s", interval.ToString("0.##", culture)), difficulty);
                    }
                    while (race.State == RaceState.Countdown) yield return null;
                    race.QaFinishNow();
                    Assert.AreEqual(FlowState.Results, flow.State);
                    var payout = flow.LastPayout;
                    Assert.AreEqual(race.tier.rewardMultiplier, payout.Multiplier);
                    Assert.Greater(payout.BaseCoins, 0);
                    StringAssert.Contains(Loc.Format("events.race_reward", "m", payout.Multiplier.ToString("0.##", culture)), difficulty);
                    flow.EnterMenu();
                    yield return null;
                }
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator SpecialEventCardsIdentifyAlliesGuardAndDisabledGuns()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null;
            profile = JsonUtility.ToJson(PlatformGlue.Save.Data);
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = flow.GetComponent<RaceManager>();
            var menu = flow.GetComponent<MenuScreens>();
            var culture = CultureInfo.GetCultureInfo(Loc.Language);
            if (!PlatformGlue.Save.Data.progress.unlockedEvents.Contains("e6"))
                PlatformGlue.Save.Data.progress.unlockedEvents.Add("e6");
            foreach (string id in new[] { "m1", TrainMission.EventId, "e18", "e6" })
            {
                menu.Show("events");
                GameObject.Find("Tier2").GetComponent<Button>().onClick.Invoke();
                var start = StartButton(id);
                string stats = CardText(start, "Stats"), difficulty = CardText(start, "Difficulty");
                Assert.IsTrue(start.interactable);
                start.onClick.Invoke();
                var field = race.Participants.Where(p => !p.isPlayer).ToArray();
                if (id == "m1")
                {
                    Assert.IsNotEmpty(field);
                    Assert.AreEqual(Loc.Format("mission.detail", "n", field.Length), stats);
                    foreach (var ally in field)
                    {
                        Assert.AreEqual(Team.Players, Teams.Of(ally.gameObject));
                        float interval = ally.GetComponent<AiGunner>().fireCooldown;
                        StringAssert.Contains(Loc.Format("mission.ally_fire", "s", interval.ToString("0.##", culture)), difficulty);
                    }
                }
                else if (id == TrainMission.EventId)
                {
                    Assert.IsEmpty(field);
                    var guard = TrainMission.Current.Escort.Guard;
                    Assert.AreEqual(Team.Enemy, Teams.Of(guard.gameObject));
                    float interval = guard.GetComponent<AiGunner>().fireCooldown;
                    Assert.AreEqual(Loc.Get("train.detail"), stats);
                    StringAssert.Contains("1", stats);
                    StringAssert.Contains(Loc.Format("train.guard_fire", "s", interval.ToString("0.##", culture)), difficulty);
                }
                else
                {
                    Assert.IsNotEmpty(field);
                    Assert.IsTrue(field.All(p => p.Car.Cannon.locked));
                    StringAssert.Contains(Loc.Get("events.guns_disabled"), difficulty);
                }
                flow.EnterMenu();
                yield return null;
            }
        }
    }
}
