using NUnit.Framework;
using UnityEngine;
using RacingGame.Platform;

namespace RacingGame.Tests
{
    public class PlayerPrefsSaveStoreTests
    {
        private const string Key = "CarsGame.Tests.PlayerPrefsSaveStore";

        [SetUp]
        public void SetUp() => PlayerPrefs.DeleteKey(Key);

        [TearDown]
        public void TearDown() => PlayerPrefs.DeleteKey(Key);

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
