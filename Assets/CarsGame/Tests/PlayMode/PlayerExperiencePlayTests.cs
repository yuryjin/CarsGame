using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using RacingGame.Core.Platform;

namespace RacingGame.Tests
{
    public sealed class PlayerExperiencePlayTests
    {
        private float capture;
        private string before;
        private GameFlow flow;
        private RaceEventDefinition practice;
        private RaceEventDefinition lockedRequest;
        [SetUp] public void Setup() { capture = Time.captureDeltaTime; }
        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (before != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(before, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            if (practice != null) Object.DestroyImmediate(practice);
            if (lockedRequest != null) Object.DestroyImmediate(lockedRequest);
            PauseController.ResetAll();
            Time.captureDeltaTime = capture;
        }

        private static Button Button(string name) => System.Array.Find(Object.FindObjectsByType<Button>(), b => b.name == name);

        [UnityTest, Timeout(180000)]
        public IEnumerator LoadoutTouchControlsGoalsAndHelpFollowRaceRules()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            var menus = Object.FindAnyObjectByType<MenuScreens>();
            var touch = Object.FindAnyObjectByType<TouchControlsPresenter>();
            before = JsonUtility.ToJson(PlatformGlue.Save.Data);
            var p = PlatformGlue.Save.Data.progress;
            p.equippedPrimary = 0; p.reservePrimary = 2; p.fullArsenal = false;
            menus.Show("garage"); yield return null;
            Assert.NotNull(Button("ChooseSlot0"));
            Assert.NotNull(Button("ChooseSlot1"));
            Button("ChooseSlot1").onClick.Invoke(); yield return null;
            Assert.AreEqual("arsenal", menus.Current);
            Button("Filter_Traps").onClick.Invoke(); yield return null;
            Assert.NotNull(Button("EquipSlot_40"));
            Assert.IsNull(Button("EquipSlot_0"));
            Button("Filter_Control").onClick.Invoke(); yield return null;
            Assert.NotNull(Button("EquipSlot_6"));
            Assert.NotNull(Button("EquipSlot_22"));
            Assert.IsNull(Button("EquipSlot_0"));
            Button("Filter_All").onClick.Invoke(); yield return null;
            Button("EquipSlot_49").onClick.Invoke(); yield return null;
            Assert.AreEqual(49, p.reservePrimary);
            Button("Back").onClick.Invoke(); yield return null;
            Assert.AreEqual("garage", menus.Current);

            practice = Object.Instantiate(System.Array.Find(flow.events, e => e.eventId == "e1"));
            practice.aiCountOverride = 0;
            touch.forceShow = true;
            flow.StartRace(practice, null);
            while (race.State == RaceState.Countdown) yield return null;
            for (int i = 0; i < 20; i++) yield return null;
            Assert.IsTrue(race.Player.Weapons.CompactLoadout);
            Assert.NotNull(Button("Fire"));
            Assert.NotNull(Button("SwapWeapon"));
            Assert.AreEqual(Loc.Get("touch.reset"), Button("Reset").GetComponentInChildren<TMPro.TMP_Text>().text);
            foreach (string name in new[] { "Brake", "Reset", "SwapWeapon", "PauseButton", "LiveGoals" })
            {
                var target = (RectTransform)Button(name).transform;
                Assert.GreaterOrEqual(target.rect.height * target.lossyScale.y, 43.5f, name + " needs a usable touch target");
            }
            Button("SwapWeapon").onClick.Invoke();
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(PrimaryWeaponType.HomingRocket, race.Player.Weapons.PrimaryType);
            var weaponName = System.Array.Find(Object.FindObjectsByType<TMPro.TMP_Text>(), t => t.name == "ActiveWeapon");
            Assert.NotNull(weaponName);
            Assert.AreEqual(Loc.Get("weapon.homing_rocket"), weaponName.text);

            lockedRequest = Object.Instantiate(practice);
            lockedRequest.eventId = "qa_locked_experience";
            lockedRequest.unlockAfterEventId = "e5";
            var playerBefore = race.Player;
            var tierBefore = RaceSetup.Tier;
            flow.StartRace(lockedRequest, flow.tiers[0]);
            Assert.AreSame(practice, RaceSetup.Event, "Rejected events must not change active rules");
            Assert.AreSame(tierBefore, RaceSetup.Tier);
            Assert.AreSame(playerBefore, race.Player);
            Assert.AreEqual(RaceState.Racing, race.State);
            var goals = Button("LiveGoals");
            Assert.NotNull(goals);
            StringAssert.IsMatch(@"^★ \d/3$", goals.GetComponentInChildren<TMPro.TMP_Text>().text);
            float compactHeight = ((RectTransform)goals.transform).rect.height;
            goals.onClick.Invoke(); yield return null;
            Assert.Greater(((RectTransform)goals.transform).rect.height, compactHeight);

            PauseController.Set(PauseReason.User, true); yield return null;
            Button("Hint").onClick.Invoke(); yield return null;
            Assert.NotNull(Button("CloseHelp"));
            var lessons = System.Array.Find(Object.FindObjectsByType<TMPro.TextMeshProUGUI>(), label => label.name == "Lessons");
            StringAssert.Contains(Loc.Get("help.launch"), lessons.text);
            var hud = Object.FindAnyObjectByType<HudScreen>();
            PauseController.Set(PauseReason.HostPause, true);
            var reasons = PauseController.Reasons;
            Assert.IsTrue(hud.CloseHelpIfOpen());
            Assert.AreEqual(reasons, PauseController.Reasons, "Closing help must preserve user and external pauses");
            Assert.IsFalse(hud.CloseHelpIfOpen(), "Back consumes only one visible help layer");
            PauseController.Set(PauseReason.HostPause, false);
            Button("Hint").onClick.Invoke(); yield return null;
            Button("CloseHelp").onClick.Invoke();
            PauseController.Set(PauseReason.User, false);
            flow.EnterMenu(); yield return null;

            var bomb = System.Array.Find(flow.events, e => e.eventId == "e18");
            flow.StartRace(bomb, null);
            while (race.State == RaceState.Countdown) yield return null;
            yield return null;
            Assert.IsNull(Button("Fire"), "bomb mode must not offer a disabled gun");
            Assert.IsNull(Button("Secondary"));
            Assert.IsNull(Button("SwapWeapon"));
            Assert.AreEqual(Loc.Get("lesson.bomb"), HudScreen.ControlsHint(true));
            Assert.IsTrue(race.Player.Car.Cannon.locked);

            // Heat-only weapons must retain their HUD after a retry with the same loadout.
            p.equippedPrimary = (int)PrimaryWeaponType.Chainsaw;
            for (int run = 0; run < 2; run++)
            {
                flow.StartRace(practice, null);
                yield return null;
                var timer = System.Array.Find(Object.FindObjectsByType<TMPro.TMP_Text>(), t => t.name == "Timer");
                Assert.AreEqual("0:00.000", timer.GetParsedText(), "A retry must not display the previous race time during countdown");
                while (race.State == RaceState.Countdown) yield return null;
                for (int i = 0; i < 5; i++) yield return null;
                weaponName = System.Array.Find(Object.FindObjectsByType<TMPro.TMP_Text>(), t => t.name == "ActiveWeapon");
                Assert.NotNull(weaponName, "Heat-only HUD must be visible after every start");
                Assert.AreEqual(Loc.Get("weapon.chainsaw"), weaponName.text);
            }

            flow.EnterMenu(); yield return null;
            Assert.IsTrue(flow.StartTestDrive("dune"));
            var testEvent = RaceSetup.Event;
            flow.StartRace(lockedRequest, flow.tiers[0]);
            Assert.IsTrue(flow.IsTestDrive, "Rejected requests must not consume the garage return snapshot");
            Assert.AreSame(testEvent, RaceSetup.Event);
        }
    }
}
