using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using RacingGame.Core.Save;

namespace RacingGame.Tests
{
    public sealed class AudioSettingsPlayTests
    {
        private string saved;
        private float volume;
        [TearDown] public void Cleanup()
        {
            if (saved != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
                SfxPlayer.Instance.volume = volume;
            }
        }

        private static Slider Slider(string name) => System.Array.Find(Object.FindObjectsByType<Slider>(), s => s.name == name);

        [UnityTest, Timeout(180000)]
        public IEnumerator VolumeControlsStayIndependentAndPreserveAnExplicitEightyPercent()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            volume = SfxPlayer.Instance.volume;
            var settings = PlatformGlue.Save.Data.settings;
            settings.sfx = 1f; settings.music = 0.8f; settings.musicChosen = false;
            var menus = Object.FindAnyObjectByType<MenuScreens>();
            menus.Show("settings"); yield return null;
            Assert.AreEqual(100f, Slider("MusicVolume").value, "Legacy default reflects the actual music level");
            Slider("SoundVolume").value = 0f;
            Assert.AreEqual(0f, settings.sfx);
            Assert.AreEqual(0f, SfxPlayer.Instance.volume);
            Assert.AreEqual(1f, MusicDirector.MusicLevel(settings), "Effects no longer mute the independent music channel");
            Slider("MusicVolume").value = 80f;
            Assert.AreEqual(0.8f, MusicDirector.MusicLevel(settings));
            Assert.AreEqual(0f, settings.sfx);
            menus.Show("main"); menus.Show("settings"); yield return null;
            Assert.AreEqual(80f, Slider("MusicVolume").value);
            Assert.AreEqual(0f, Slider("SoundVolume").value);
            var music = Slider("MusicVolume");
            EventSystem.current.SetSelectedGameObject(music.gameObject);
            music.OnMove(new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Right });
            Assert.AreEqual(81f, music.value, "Keyboard/gamepad right changes the focused volume by one percent");
            Slider("SoundVolume").value = 35f;
            Assert.AreEqual(0.28f, SfxPlayer.Instance.volume, 0.0001f);
            Assert.AreEqual(0.81f, MusicDirector.MusicLevel(settings), 0.0001f);
            var restored = SaveMigrator.FromJson(SaveMigrator.ToJson(PlatformGlue.Save.Data));
            Assert.AreEqual(0.35f, restored.settings.sfx);
            Assert.AreEqual(0.81f, MusicDirector.MusicLevel(restored.settings), 0.0001f);
            Slider("MusicVolume").value = 0f;
            Assert.AreEqual(0f, MusicDirector.MusicLevel(settings));
            Assert.AreEqual(0.35f, settings.sfx);
        }
    }
}
