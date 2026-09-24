using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public class AiRecoveryTests
    {
        [UnityTest]
        public IEnumerator PinnedCarRecoversWithAndWithoutThrottle()
        {
            yield return new EnterPlayMode();
            var trackObject = new GameObject("RecoveryTrack");
            var carObject = new GameObject("PinnedCar");
            carObject.SetActive(false);
            var profile = ScriptableObject.CreateInstance<AiDriverProfile>();
            try
            {
                for (int i = 0; i < 32; i++)
                {
                    var point = new GameObject("Point").transform;
                    point.SetParent(trackObject.transform);
                    float angle = i * Mathf.PI * 2f / 32;
                    point.position = new Vector3(Mathf.Sin(angle) * 100f, 0f, Mathf.Cos(angle) * 100f);
                }
                var path = trackObject.AddComponent<TrackPath>();
                path.Pose(20f, 0f, out var position, out var rotation);
                carObject.transform.SetPositionAndRotation(position, rotation);
                carObject.AddComponent<BoxCollider>().size = new Vector3(1.8f, 0.7f, 4f);
                var car = carObject.AddComponent<CarController>();
                car.playerControlled = false;
                carObject.AddComponent<RaceParticipant>();
                var ai = carObject.AddComponent<AiDriver>();
                profile.reactionDelay = 0f;
                carObject.SetActive(true);

                foreach (float slip in new[] { 4f, 0f })
                {
                    ai.Initialize(profile, 0f);
                    car.Body.linearVelocity = car.transform.right * slip;
                    bool reversed = false, reset = false;
                    for (int tick = 1; tick <= 600 && !reset; tick++)
                    {
                        var command = ai.Sample(tick);
                        reversed |= command.Throttle < 0f;
                        reset = command.ResetEdge;
                    }
                    Assert.IsTrue(reversed, "A pinned car must try reversing even when slip suppresses its throttle.");
                    Assert.IsTrue(reset, "Failed reverse attempts must eventually reset the car; slip=" + slip);
                    car.Teleport(position, rotation);
                    Assert.AreEqual(0f, ai.StuckTime, "Recovery starts with a fresh stuck timer.");
                }
            }
            finally
            {
                Object.DestroyImmediate(carObject);
                Object.DestroyImmediate(trackObject);
                Object.DestroyImmediate(profile);
            }
        }

        [UnityTearDown]
        public IEnumerator ExitPlayModeAfterTest()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
