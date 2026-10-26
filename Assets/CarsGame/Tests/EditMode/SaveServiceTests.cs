using System;
using System.Threading.Tasks;
using NUnit.Framework;
using RacingGame.Core.Platform;
using RacingGame.Core.Save;

namespace RacingGame.Tests
{
    public class SaveServiceTests
    {
        private sealed class MockLocalStore : ILocalSaveStore
        {
            public string StoredJson;
            public int WriteCount;

            public string Read() => StoredJson;
            public void Write(string json)
            {
                StoredJson = json;
                WriteCount++;
            }
        }

        private sealed class MockCloudSave : ICloudSave
        {
            public string StoredJson;
            public string QueuedJson;
            public int LoadCalls;
            public int QueueCalls;
            public int FlushCalls;
            public bool ShouldFailLoad;
            public bool ShouldFailFlush;
            public Task<Result<string>> PendingLoad;

            public Task<Result<string>> LoadAsync()
            {
                LoadCalls++;
                if (PendingLoad != null) return PendingLoad;
                if (ShouldFailLoad)
                    return Task.FromResult(Result<string>.Fail(ResultCategory.Error, "load failed", true));
                return Task.FromResult(Result<string>.Success(StoredJson));
            }

            public void Queue(string json)
            {
                QueueCalls++;
                QueuedJson = json;
            }

            public Task<Result> FlushAsync()
            {
                FlushCalls++;
                if (ShouldFailFlush)
                    return Task.FromResult(Result.Fail(ResultCategory.Error, "flush failed", true));
                StoredJson = QueuedJson;
                return Task.FromResult(Result.Success);
            }
        }

        [Test]
        public void InitialStateHasValidDefaults()
        {
            var local = new MockLocalStore();
            var service = new SaveService(local, () => null, () => 0d, "device_1");

            Assert.AreEqual("device_1", service.DeviceId);
            Assert.AreEqual("device_1", service.Data.deviceId);
            Assert.IsFalse(service.Loaded);
            Assert.IsFalse(service.CloudSynced);
            Assert.AreEqual(0, service.CloudWrites);
            Assert.AreEqual(0, service.CloudFailures);
            Assert.NotNull(service.Local);
        }

        [Test]
        public async Task LoadAsyncLoadsLocalStoreAndSetsLoaded()
        {
            var local = new MockLocalStore();
            var initData = new SaveData { deviceId = "device_1" };
            initData.Local("device_1").coinsEarned = 100;
            local.StoredJson = SaveMigrator.ToJson(initData);

            bool changedFired = false;
            var service = new SaveService(local, () => null, () => 0d, "device_1");
            service.Changed += _ => changedFired = true;

            await service.LoadAsync();

            Assert.IsTrue(service.Loaded);
            Assert.IsTrue(service.CloudSynced);
            Assert.IsTrue(changedFired);
            Assert.AreEqual(100, service.Data.CoinsEarned);
        }

        [Test]
        public async Task LoadAsyncMergesCloudDataWhenAvailable()
        {
            var local = new MockLocalStore();
            var localData = new SaveData { deviceId = "device_1" };
            localData.Local("device_1").coinsEarned = 100;
            local.StoredJson = SaveMigrator.ToJson(localData);

            var cloud = new MockCloudSave();
            var cloudData = new SaveData { deviceId = "device_cloud" };
            cloudData.Local("device_cloud").coinsEarned = 250;
            cloud.StoredJson = SaveMigrator.ToJson(cloudData);

            var service = new SaveService(local, () => cloud, () => 0d, "device_1");
            await service.LoadAsync();

            Assert.IsTrue(service.Loaded);
            Assert.IsTrue(service.CloudSynced);
            Assert.AreEqual(350, service.Data.CoinsEarned);
        }

        [Test]
        public async Task LoadAsyncRefusesCorruptOrNewerCloudSave()
        {
            var local = new MockLocalStore();
            var cloud = new MockCloudSave { StoredJson = "{\"version\":999}" };

            var service = new SaveService(local, () => cloud, () => 0d, "device_1");
            await service.LoadAsync();

            Assert.IsTrue(service.Loaded);
            Assert.IsFalse(service.CloudSynced);
            Assert.AreEqual(SaveParseStatus.NewerVersion, service.LastCloudStatus);
            Assert.AreEqual(1, service.CloudFailures);
        }

        [Test]
        public async Task NewerLocalProfileIsNeverOverwrittenByAnOlderClient()
        {
            const string future = "{\"version\":999,\"progress\":{\"futureProgress\":\"keep me\"}}";
            var local = new MockLocalStore { StoredJson = future };
            var cloud = new MockCloudSave { StoredJson = SaveMigrator.ToJson(new SaveData()) };
            var service = new SaveService(local, () => cloud, () => 100d, "old_client");
            await service.LoadAsync();
            Assert.IsTrue(service.LocalVersionTooNew);
            Assert.AreEqual(SaveParseStatus.NewerVersion, service.LastLocalStatus);
            service.Local.coinsEarned = 200;
            service.MarkDirty(); service.Tick();
            Assert.IsFalse((await service.FlushAsync()).Ok);
            Assert.AreEqual(future, local.StoredJson, "An unreadable future profile must remain intact");
            Assert.AreEqual(0, local.WriteCount);
            Assert.AreEqual(0, cloud.QueueCalls, "An old client must not publish a partial replacement profile");
        }

        [Test]
        public void MarkDirtyWritesToLocalStoreAndFiresChanged()
        {
            var local = new MockLocalStore();
            var service = new SaveService(local, () => null, () => 0d, "device_1");

            bool changedFired = false;
            service.Changed += _ => changedFired = true;

            service.Data.Local("device_1").coinsEarned = 500;
            service.MarkDirty();

            Assert.IsTrue(changedFired);
            Assert.AreEqual(1, local.WriteCount);
            Assert.NotNull(local.StoredJson);
            Assert.That(service.Data.updatedAtUnixMs, Is.GreaterThan(0));
        }

        [Test]
        public async Task TickCoalescesCloudWritesBasedOnInterval()
        {
            double currentTime = 10d;
            var local = new MockLocalStore();
            var cloud = new MockCloudSave();
            var service = new SaveService(local, () => cloud, () => currentTime, "device_1")
            {
                cloudMinIntervalSeconds = 5f
            };

            await service.LoadAsync();
            Assert.AreEqual(0, cloud.QueueCalls);

            service.MarkDirty();
            // First tick sends to cloud
            service.Tick();
            Assert.AreEqual(1, cloud.QueueCalls);
            Assert.AreEqual(1, service.CloudWrites);

            // Mark dirty again immediately
            service.MarkDirty();
            currentTime = 12d; // Only 2 seconds passed (< 5s interval)
            service.Tick();
            Assert.AreEqual(1, cloud.QueueCalls, "Should not send before interval");

            currentTime = 16d; // 6 seconds passed (> 5s interval)
            service.Tick();
            Assert.AreEqual(2, cloud.QueueCalls, "Should send once interval has elapsed");
            Assert.AreEqual(2, service.CloudWrites);
        }

        [Test]
        public async Task FlushAsyncImmediatelySendsPendingChanges()
        {
            var local = new MockLocalStore();
            var cloud = new MockCloudSave();
            var service = new SaveService(local, () => cloud, () => 10d, "device_1");

            await service.LoadAsync();
            service.MarkDirty();

            var result = await service.FlushAsync();
            Assert.IsTrue(result.Ok);
            Assert.AreEqual(1, cloud.QueueCalls);
            Assert.AreEqual(1, cloud.FlushCalls);
        }

        [Test]
        public void OnIdentityChangedResetsCloudSyncAndMarksDirty()
        {
            var local = new MockLocalStore();
            var service = new SaveService(local, () => null, () => 10d, "device_1");

            service.OnIdentityChanged();
            Assert.IsFalse(service.CloudSynced);
        }

        private static string CoinsOnDevice(string device, int coins)
        {
            var data = new SaveData { deviceId = device };
            data.Local(device).coinsEarned = coins;
            return SaveMigrator.ToJson(data);
        }

        [Test]
        public async Task AccountChangeDuringInitialLoadCannotAcceptThePreviousAccountsResponse()
        {
            var pending = new TaskCompletionSource<Result<string>>();
            var previous = new MockCloudSave { PendingLoad = pending.Task };
            var current = new MockCloudSave { StoredJson = CoinsOnDevice("new_account", 200) };
            ICloudSave provider = previous;
            var local = new MockLocalStore { StoredJson = CoinsOnDevice("device_1", 100) };
            var service = new SaveService(local, () => provider, () => 10d, "device_1");

            Task loading = service.LoadAsync();
            provider = current;
            service.OnIdentityChanged();
            pending.SetResult(Result<string>.Success(CoinsOnDevice("old_account", 900)));
            await loading;

            Assert.IsFalse(service.CloudSynced, "An old response must not authorize writes to the new account");
            Assert.AreEqual(100, service.Data.CoinsEarned, "Ignore data received for a superseded identity");
            Assert.IsTrue((await service.FlushAsync()).Ok);
            Assert.AreEqual(1, current.LoadCalls, "Read the new account before writing to it");
            Assert.AreEqual(300, SaveMigrator.FromJson(current.StoredJson).CoinsEarned);
            Assert.AreEqual(0, previous.QueueCalls);
        }

        [Test]
        public async Task AccountChangeDuringRetryKeepsWritesBlockedUntilTheNewAccountIsRead()
        {
            var previous = new MockCloudSave { ShouldFailLoad = true };
            var current = new MockCloudSave { StoredJson = CoinsOnDevice("new_account", 200) };
            ICloudSave provider = previous;
            var local = new MockLocalStore { StoredJson = CoinsOnDevice("device_1", 100) };
            var service = new SaveService(local, () => provider, () => 10d, "device_1");
            await service.LoadAsync();
            var pending = new TaskCompletionSource<Result<string>>();
            previous.PendingLoad = pending.Task;
            Task<Result> flushing = service.FlushAsync();
            provider = current;
            service.OnIdentityChanged();
            pending.SetResult(Result<string>.Success(CoinsOnDevice("old_account", 900)));

            Assert.IsFalse((await flushing).Ok, "A superseded retry cannot flush to either account");
            Assert.IsFalse(service.CloudSynced);
            Assert.AreEqual(0, previous.FlushCalls);
            Assert.AreEqual(0, current.QueueCalls);
            Assert.IsTrue((await service.FlushAsync()).Ok);
            Assert.AreEqual(1, current.LoadCalls);
            Assert.AreEqual(300, SaveMigrator.FromJson(current.StoredJson).CoinsEarned);
        }
    }
}
