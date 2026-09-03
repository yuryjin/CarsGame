using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
using RacingGame.Core.Platform;

namespace RacingGame.Platform
{
    /// <summary>
    /// Persistent MonoBehaviour that hosts the active <see cref="IPlatformServices"/>, receives
    /// JavaScript callbacks (SendMessage target "PlatformRuntime") and expires pending requests
    /// on an unscaled realtime deadline — ads set timeScale to 0, so scaled timers must not be
    /// used here. Preserved so IL2CPP stripping never removes the SendMessage targets.
    /// </summary>
    [Preserve]
    public sealed class PlatformRuntime : MonoBehaviour
    {
        public const string ObjectName = "PlatformRuntime";

        private static PlatformRuntime instance;
        public static PlatformRuntime Instance => instance;
        public static IPlatformServices Current { get; private set; }

        private sealed class Pending { public TaskCompletionSource<string> Tcs; public float Deadline; }
        private readonly Dictionary<int, Pending> pending = new Dictionary<int, Pending>();
        private readonly List<int> expired = new List<int>();
        private int nextRequestId = 1;

        public event Action<string, string> JsMessage;      // (channel, payload) for unsolicited messages
        public event Action<bool> HostPauseChanged;
        public event Action AdOpened, AdClosed;

        public static PlatformRuntime Ensure()
        {
            if (instance != null) return instance;
            var go = new GameObject(ObjectName);
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PlatformRuntime>();
            return instance;
        }

        public static void Install(IPlatformServices services) { Ensure(); Current = services; }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            gameObject.name = ObjectName;
        }

        /// <summary>Register a request; the JS side answers with the same id through <see cref="OnJs"/>.</summary>
        public int Begin(out Task<string> task, float timeoutSeconds)
        {
            int id = nextRequestId++;
            var p = new Pending { Tcs = new TaskCompletionSource<string>(), Deadline = Time.realtimeSinceStartup + timeoutSeconds };
            pending[id] = p;
            task = p.Tcs.Task;
            return id;
        }

        /// <summary>Complete a request from managed code (mocks) or JS.</summary>
        public void Complete(int id, string payload)
        {
            if (!pending.TryGetValue(id, out var p)) return;
            pending.Remove(id);
            p.Tcs.TrySetResult(payload);
        }

        private void Update()
        {
            if (pending.Count == 0) return;
            float now = Time.realtimeSinceStartup;
            expired.Clear();
            foreach (var kv in pending) if (now >= kv.Value.Deadline) expired.Add(kv.Key);
            foreach (int id in expired)
            {
                var p = pending[id];
                pending.Remove(id);
                p.Tcs.TrySetResult("{\"ok\":false,\"error\":\"timeout\"}");
            }
        }

        // ---- SendMessage targets (called from the WebGL template) ----
        [Preserve]
        public void OnJs(string json)
        {
            // Minimal envelope: {"id":N,...}. The full payload is handed to the awaiting caller.
            int id = ExtractId(json);
            if (id > 0) Complete(id, json); else JsMessage?.Invoke("js", json);
        }

        [Preserve] public void OnHostPause(string flag) { HostPauseChanged?.Invoke(flag == "1" || flag == "true"); }
        [Preserve] public void OnAdOpen(string _) { AdOpened?.Invoke(); }
        [Preserve] public void OnAdClose(string _) { AdClosed?.Invoke(); }

        private static int ExtractId(string json)
        {
            if (string.IsNullOrEmpty(json)) return 0;
            int i = json.IndexOf("\"id\":", StringComparison.Ordinal);
            if (i < 0) return 0;
            i += 5;
            int end = i;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == ' ')) end++;
            return int.TryParse(json.Substring(i, end - i).Trim(), out int id) ? id : 0;
        }
    }
}
