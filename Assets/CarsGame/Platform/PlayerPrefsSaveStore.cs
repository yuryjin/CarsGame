using UnityEngine;
using RacingGame.Core.Save;

namespace RacingGame.Platform
{
    /// <summary>Local mirror of the save (PlayerPrefs = localStorage on WebGL).</summary>
    public sealed class PlayerPrefsSaveStore : ILocalSaveStore
    {
        private readonly string key;
        public PlayerPrefsSaveStore(string prefsKey = SaveService.LocalKey) { key = prefsKey; }
        public string Read() { try { return PlayerPrefs.GetString(key, ""); } catch { return ""; } }
        public void Write(string json) { try { PlayerPrefs.SetString(key, json); PlayerPrefs.Save(); } catch { } }
    }
}
