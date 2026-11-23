using RacingGame.World.Gen;
using UnityEngine;

namespace RacingGame
{
    public sealed class GrandstandReaction : MonoBehaviour
    {
        private GrandstandPlacement stand;
        private RaceManager race;
        private bool heardThisPass;
        public int Reactions { get; private set; }
        public Vector3 StandPosition => stand.Position;

        public void Watch(RaceManager field, TrackPath track)
        {
            race = field;
            var authored = track.reversed ? track.Spline.Reversed() : track.Spline;
            stand = PropLayout.GrandstandAt(authored, track.startLineDistance);
        }

        private void Update()
        {
            if (race == null || race.State != RaceState.Racing || PauseController.IsPaused) return;
            var player = race.Player;
            if (player == null || player.Progress == null) return;
            Vector3 rel = player.transform.position - stand.Position;
            float across = Vector3.Dot(rel, stand.Forward);
            float along = Mathf.Abs(Vector3.Dot(rel, Vector3.Cross(stand.Forward, Vector3.up)));
            if (along > stand.Length * .5f + 8f || across < 1f || across > 29f)
            { heardThisPass = false; return; }
            if (heardThisPass || !player.Car.IsGrounded || Mathf.Abs(player.Car.SpeedKmh) < 25f) return;
            foreach (var other in race.Participants)
            {
                if (other == player || RaceManager.IsOut(other) || race.HasFinished(other) || other.Progress == null) continue;
                if (Mathf.Abs(player.Progress.TotalProgress - other.Progress.TotalProgress) > 9f
                    || (other.transform.position - player.transform.position).sqrMagnitude > 121f) continue;
                heardThisPass = true; Reactions++;
                SfxPlayer.Instance.Play(Cheer, stand.Position + Vector3.up * 3f, .78f);
                return;
            }
        }

        private static AudioClip cheer;
        public static AudioClip Cheer
        {
            get
            {
                if (cheer != null) return cheer;
                // Four overlapping vowel glides form a short spectator cheer without spoken words.
                const int rate = 22050;
                float[] pcm = new float[(int)(rate * 1.25f)];
                for (int i = 0; i < pcm.Length; i++)
                {
                    float t = i / (float)rate, sample = 0f;
                    for (int voice = 0; voice < 4; voice++)
                    {
                        float v = t - voice * .061f;
                        if (v <= 0f || v >= .91f) continue;
                        float env = Mathf.Sin(v / .91f * Mathf.PI);
                        float phase = (142f + 24f * voice) * v + 12f * Mathf.Sin(v * Mathf.PI);
                        sample += env * (.10f * Mathf.Sin(phase * 2f * Mathf.PI)
                            + .066f * Mathf.Sin(phase * 6f * Mathf.PI) + .027f * Mathf.Sin(phase * 10f * Mathf.PI));
                    }
                    pcm[i] = sample;
                }
                cheer = AudioClip.Create("grandstand_cheer", pcm.Length, 1, rate, false);
                cheer.SetData(pcm, 0);
                return cheer;
            }
        }
    }
}
