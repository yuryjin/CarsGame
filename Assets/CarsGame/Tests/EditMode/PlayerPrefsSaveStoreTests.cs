using NUnit.Framework;
using UnityEngine;
using RacingGame.Platform;
using RacingGame.Core.Save;
using System.Threading.Tasks;

namespace RacingGame.Tests
{
    public class PlayerPrefsSaveStoreTests
    {
        private const string Key = "CarsGame.Tests.PlayerPrefsSaveStore";

        [SetUp, TearDown]
        public void ClearStore()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(Key + ".backup");
            PlayerPrefs.DeleteKey(Key + ".damaged");
        }

        [Test]
        public async Task DamagedProfileRestoresThePreviousRaceOfflineAndKeepsTheOriginal()
        {
            SaveService Open() => new SaveService(new PlayerPrefsSaveStore(Key), () => null, () => 0d, "driver");
            var game = Open();
            await game.LoadAsync();
            game.Local.coinsEarned = 100;
            game.Local.races = 1;
            game.Data.progress.unlockedEvents.Add("e2");
            game.MarkDirty();
            game.Local.coinsEarned = 150;
            game.MarkDirty();

            const string damaged = "{broken save: прогресс";
            PlayerPrefs.SetString(Key, damaged);
            PlayerPrefs.Save();
            var recovered = Open();
            await recovered.LoadAsync();
            Assert.AreEqual(100, recovered.Data.Coins);
            Assert.AreEqual(1, recovered.Data.Races);
            CollectionAssert.Contains(recovered.Data.progress.unlockedEvents, "e2");
            Assert.AreEqual(damaged, PlayerPrefs.GetString(Key + ".damaged"));

            recovered.Local.coinsEarned += 30;
            recovered.MarkDirty();
            Assert.IsTrue((await recovered.FlushAsync()).Ok);
            var reopened = Open();
            await reopened.LoadAsync();
            Assert.AreEqual(130, reopened.Data.Coins);
            Assert.AreEqual(damaged, PlayerPrefs.GetString(Key + ".damaged"));
        }

        [Test]
        public void WriteAndFlushFailuresAreObservableByTheSaveService()
        {
            var setFails = new PlayerPrefsSaveStore(Key,
                (_, _) => throw new System.IO.IOException("QA full storage"), () => { });
            Assert.Throws<System.IO.IOException>(() => setFails.Write("progress"));
            var flushFails = new PlayerPrefsSaveStore(Key,
                (_, _) => { }, () => throw new System.IO.IOException("QA flush denied"));
            Assert.Throws<System.IO.IOException>(() => flushFails.Write("progress"));
        }

        [Test]
        public void MissingValueReadsAsEmptyString()
        {
            Assert.AreEqual(string.Empty, new PlayerPrefsSaveStore(Key).Read());
        }

        [Test]
        public void WriteRoundTripsJsonWithoutChangingIt()
        {
            const string json = "{\"version\":4,\"progress\":{\"coins\":12}}";
            var store = new PlayerPrefsSaveStore(Key);
            store.Write(json);
            Assert.AreEqual(json, store.Read());
        }
    }
}
