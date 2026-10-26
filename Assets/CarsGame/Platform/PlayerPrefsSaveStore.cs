using System;
using UnityEngine;
using RacingGame.Core.Save;

namespace RacingGame.Platform
{
    /// <summary>Native PlayerPrefs, or an acknowledged localStorage write on WebGL. WebGL reads
    /// legacy PlayerPrefs once when no new mirror exists; Unity's asynchronous IDBFS flush alone
    /// cannot report a durable write failure to the game.</summary>
    public sealed class PlayerPrefsSaveStore : ILocalSaveStore
    {
        private readonly string key;
        private readonly Action<string, string> setString;
        private readonly Action persist;
        public PlayerPrefsSaveStore(string prefsKey = SaveService.LocalKey,
            Action<string, string> writeValue = null, Action save = null)
        {
            key = prefsKey;
            setString = writeValue ?? PlayerPrefs.SetString;
            persist = save ?? PlayerPrefs.Save;
        }
        public string Read()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string reply = Yandex.YandexNative.TakeString(Yandex.YandexNative.CarsLocal_Read(key));
            var result = JsonUtility.FromJson<ReadResult>(reply);
            if (result == null || !result.ok) throw new InvalidOperationException("local storage read failed");
            if (result.found) return result.value;
#endif
            return PlayerPrefs.GetString(key, "");
        }

        public void Write(string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Yandex.YandexNative.CarsLocal_Write(key, json) == 0)
                throw new InvalidOperationException("local storage write failed");
#else
            setString(key, json); persist();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [Serializable] private sealed class ReadResult { public bool ok, found; public string value; }
#endif
    }
}
