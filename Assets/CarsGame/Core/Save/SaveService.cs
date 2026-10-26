using System;
using System.Threading.Tasks;
using RacingGame.Core.Platform;

namespace RacingGame.Core.Save
{
    public interface ILocalSaveStore { string Read(); void Write(string json); }

    /// <summary>
    /// Owns the in-memory <see cref="SaveData"/>. Local writes are immediate; cloud writes are
    /// coalesced (≥ <see cref="cloudMinIntervalSeconds"/>) and forced at checkpoints, which
    /// keeps well inside Yandex's 100 setData calls per 5 minutes.
    /// </summary>
    public sealed class SaveService
    {
        public const string LocalKey = "cars.save.v1";
        public float cloudMinIntervalSeconds = 5f;

        public SaveData Data { get; private set; } = new SaveData();
        public bool Loaded { get; private set; }
        /// <summary>True once the cloud copy was read and merged. Until then nothing is written to the cloud,
        /// so a failed load can never let local data overwrite newer cloud progress.</summary>
        public bool CloudSynced { get; private set; }
        public int CloudWrites { get; private set; }
        public int CloudFailures { get; private set; }
        public string DeviceId { get; }
        public DeviceLedger Local => Data.Local(DeviceId);
        public event Action<SaveData> Changed;
        private double nextCloudRetryAt = double.NegativeInfinity;
        private bool retrying;
        private int identityGeneration;
        private Task loadTask;
        public SaveParseStatus LastCloudStatus { get; private set; } = SaveParseStatus.Empty;
        public SaveParseStatus LastLocalStatus { get; private set; } = SaveParseStatus.Empty;
        /// <summary>An older build cannot safely modify a newer profile on this device.</summary>
        public bool LocalVersionTooNew => LastLocalStatus == SaveParseStatus.NewerVersion;

        private readonly ILocalSaveStore local;
        private readonly Func<ICloudSave> cloud;
        private readonly Func<double> clock;    // realtime seconds
        private bool dirty;
        private double lastCloudAt = double.NegativeInfinity;

        public SaveService(ILocalSaveStore localStore, Func<ICloudSave> cloudProvider, Func<double> realtimeClock, string deviceId)
        {
            local = localStore; cloud = cloudProvider; clock = realtimeClock;
            DeviceId = deviceId ?? "";
            Data.deviceId = DeviceId;
        }

        /// <summary>Single-flight: concurrent callers share one load. Cloud data is merged into the live
        /// in-memory data at completion, so progress made while the load was in flight is kept.</summary>
        public Task LoadAsync()
        {
            if (loadTask == null || loadTask.IsCompleted && !Loaded) loadTask = LoadCoreAsync();
            return loadTask;
        }

        private async Task LoadCoreAsync()
        {
            var localData = SaveMigrator.Parse(local?.Read(), out var localStatus);
            LastLocalStatus = localStatus;
            if (!Loaded) { Data = SaveMerge.Merge(Data, localData) ?? Data; Data.deviceId = DeviceId; Loaded = true; Changed?.Invoke(Data); }

            var c = cloud?.Invoke();
            if (c == null) { CloudSynced = true; return; }
            await FetchAndMergeCloudAsync(c);
        }

        // Cloud is only "known" after an explicitly empty or valid, migratable payload. Corrupt or
        // newer-version blobs block writes so we never overwrite data we refused to read.
        private async Task<bool> FetchAndMergeCloudAsync(ICloudSave c)
        {
            int generation = identityGeneration;
            var r = await c.LoadAsync();
            // A delayed guest/account response cannot mark a newer identity as synced or
            // merge its data. The next retry must fetch the account that is current now.
            if (generation != identityGeneration) return false;
            if (!r.Ok) { CloudFailures++; nextCloudRetryAt = clock() + 10d; return false; }
            var cloudData = SaveMigrator.Parse(r.Value, out var status);
            LastCloudStatus = status;
            if (status == SaveParseStatus.Corrupt || status == SaveParseStatus.NewerVersion)
            {
                CloudFailures++;
                nextCloudRetryAt = clock() + 60d;
                UnityEngine.Debug.LogWarning($"[SaveService] cloud save refused ({status}); cloud writes stay blocked.");
                return false;
            }
            if (cloudData != null)
            {
                Data = SaveMerge.Merge(Data, cloudData) ?? Data;   // merge into whatever the game changed meanwhile
                Data.deviceId = DeviceId;
                Changed?.Invoke(Data);
            }
            CloudSynced = true;
            return true;
        }

        // Cloud unknown (load failed): retry the load, merge what comes back, then allow writes.
        private async Task RetryCloudAsync()
        {
            if (retrying) return;
            retrying = true;
            try
            {
                var c = cloud?.Invoke();
                if (c == null) { CloudSynced = true; return; }
                if (await FetchAndMergeCloudAsync(c)) dirty = true;
            }
            finally { retrying = false; }
        }

        /// <summary>The platform switched player (sign-in): the cloud copy is unknown again. Writes stay
        /// blocked until the new account's save has been fetched and merged.</summary>
        public void OnIdentityChanged()
        {
            identityGeneration++;
            CloudSynced = false;
            nextCloudRetryAt = clock();
            dirty = true;
        }

        /// <summary>Call after any progress change. Writes locally now; the cloud write is coalesced.</summary>
        public void MarkDirty()
        {
            if (LocalVersionTooNew) return;
            Data.updatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            local?.Write(SaveMigrator.ToJson(Data));
            dirty = true;
            Changed?.Invoke(Data);
        }

        /// <summary>Poll from Update; sends a coalesced cloud write when due.</summary>
        public void Tick()
        {
            if (LocalVersionTooNew) return;
            if (!dirty) return;
            double now = clock();
            if (!CloudSynced) { if (now >= nextCloudRetryAt) _ = RetryCloudAsync(); return; }
            if (now - lastCloudAt < cloudMinIntervalSeconds) return;
            SendToCloud();
        }

        /// <summary>Checkpoint: race end, purchase, settings, pause/hidden, before an ad. Data stays dirty if the write fails.</summary>
        public async Task<Result> FlushAsync()
        {
            if (LocalVersionTooNew) return Result.Fail(ResultCategory.Error, "newer local save version", false);
            var c = cloud?.Invoke();
            if (c == null) { dirty = false; return Result.Success; }
            if (!CloudSynced) { await RetryCloudAsync(); if (!CloudSynced) return Result.Fail(ResultCategory.Error, "cloud not synced", true); }
            if (dirty) SendToCloud();
            var r = await c.FlushAsync();
            if (!r.Ok) { dirty = true; CloudFailures++; }
            return r;
        }

        private void SendToCloud()
        {
            var c = cloud?.Invoke();
            lastCloudAt = clock();
            if (c == null) { dirty = false; return; }
            c.Queue(SaveMigrator.ToJson(Data));
            CloudWrites++;
            dirty = false;   // the platform layer keeps the payload until it is confirmed; FlushAsync re-dirties on failure
        }
    }
}
