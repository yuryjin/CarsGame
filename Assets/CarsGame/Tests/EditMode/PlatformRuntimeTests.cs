using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using RacingGame.Platform;

namespace RacingGame.Tests
{
    public class PlatformRuntimeTests
    {
        private GameObject host;
        private PlatformRuntime runtime;

        [SetUp]
        public void SetUp()
        {
            typeof(PlatformRuntime).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            host = new GameObject("platform test");
            runtime = host.AddComponent<PlatformRuntime>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            typeof(PlatformRuntime).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        }

        [Test]
        public void CompleteResolvesMatchingRequestAndIgnoresUnknownIds()
        {
            int id = runtime.Begin(out var task, 10f);
            runtime.Complete(id + 100, "wrong");
            Assert.IsFalse(task.IsCompleted);
            runtime.Complete(id, "payload");
            Assert.AreEqual("payload", task.Result);
            runtime.Complete(id, "duplicate");
            Assert.AreEqual("payload", task.Result);
        }

        [Test]
        public void JsEnvelopeCompletesRequestAndUnsolicitedJsonIsBroadcast()
        {
            int id = runtime.Begin(out var task, 10f);
            runtime.OnJs("{\"id\": " + id + ",\"ok\":true}");
            Assert.AreEqual("{\"id\": " + id + ",\"ok\":true}", task.Result);

            string channel = null, payload = null;
            runtime.JsMessage += (c, p) => { channel = c; payload = p; };
            runtime.OnJs("{\"event\":\"ready\"}");
            Assert.AreEqual("js", channel);
            Assert.AreEqual("{\"event\":\"ready\"}", payload);
        }

        [Test]
        public void HostLifecycleCallbacksParseFlags()
        {
            bool? pause = null, hidden = null;
            runtime.HostPauseChanged += value => pause = value;
            runtime.HiddenChanged += value => hidden = value;
            runtime.OnHostPause("true");
            runtime.OnHidden("1");
            Assert.AreEqual(true, pause);
            Assert.AreEqual(true, hidden);
            runtime.OnHostPause("0");
            runtime.OnHidden("false");
            Assert.AreEqual(false, pause);
            Assert.AreEqual(false, hidden);
        }

        [Test]
        public void ExpiredRequestCompletesWithTimeoutEnvelope()
        {
            runtime.Begin(out var task, 0f);
            typeof(PlatformRuntime).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(runtime, null);
            StringAssert.Contains("\"error\":\"timeout\"", task.Result);
        }

        [Test]
        public void AdCallbacksRaiseOpenAndCloseEvents()
        {
            int opened = 0, closed = 0;
            runtime.AdOpened += () => opened++;
            runtime.AdClosed += () => closed++;
            runtime.OnAdOpen("");
            runtime.OnAdClose("");
            Assert.AreEqual(1, opened);
            Assert.AreEqual(1, closed);
        }
    }
}
