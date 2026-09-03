using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Instantiates the player and AI cars from the RaceCar prefab onto the start grid and wires
    /// drivers, paint, names and the chase camera. The prefab ships with playerControlled=false,
    /// so the driver component is chosen here.
    /// </summary>
    public sealed class RaceSpawner : MonoBehaviour
    {
        [Header("Setup")]
        public GameObject raceCarPrefab;
        public TrackPath track;
        public StartGrid grid;
        public bool spawnOnStart = true;
        [Range(0, 7)] public int aiCount = 3;
        [Tooltip("-1 = last slot (arcade convention), otherwise the grid index of the player.")]
        public int playerGridSlot = -1;

        [Header("AI")]
        public AiDriverProfile[] profiles;
        public float aiFireCooldown = 1.5f;
        public string[] aiNames = { "Rex", "Nova", "Bolt", "Vega", "Zed", "Kira", "Max" };
        public Color[] paints =
        {
            new Color(0.15f, 0.35f, 0.95f), new Color(0.95f, 0.75f, 0.1f), new Color(0.2f, 0.8f, 0.3f),
            new Color(0.9f, 0.9f, 0.9f), new Color(0.6f, 0.2f, 0.8f), new Color(1f, 0.5f, 0.1f), new Color(0.1f, 0.1f, 0.1f)
        };
        public Color playerPaint = new Color(0.8f, 0.1f, 0.1f);
        public string playerName = "You";

        public RaceParticipant Player { get; private set; }
        public IReadOnlyList<RaceParticipant> Participants => participants;
        private readonly List<RaceParticipant> participants = new List<RaceParticipant>();

        private void Start()
        {
            if (spawnOnStart) Spawn();
        }

        /// <summary>Spawns the field. Returns false (and leaves nothing behind) when the setup is incomplete.</summary>
        public bool Spawn()
        {
            if (track == null) track = TrackPath.Instance;
            if (grid == null) grid = FindAnyObjectByType<StartGrid>();
            if (raceCarPrefab == null || track == null || grid == null)
            {
                Debug.LogError("[RaceSpawner] prefab, TrackPath and StartGrid are required.", this);
                Clear();
                return false;
            }

            Clear();
            int total = aiCount + 1;
            int playerSlot = playerGridSlot < 0 ? total - 1 : Mathf.Clamp(playerGridSlot, 0, total - 1);
            int aiIndex = 0;
            for (int slot = 0; slot < total; slot++)
            {
                bool isPlayer = slot == playerSlot;
                grid.GetSlot(slot, track, out var pos, out var rot, out float lane);
                var go = Instantiate(raceCarPrefab, pos, rot);
                go.name = isPlayer ? "Player_Car" : $"AI_Car_{aiIndex + 1}";
                var car = go.GetComponent<CarController>();
                var participant = go.GetComponent<RaceParticipant>() ?? go.AddComponent<RaceParticipant>();
                var paint = go.GetComponent<CarPaint>() ?? go.AddComponent<CarPaint>();
                participant.isPlayer = isPlayer;
                participant.laneOffset = lane;

                if (isPlayer)
                {
                    participant.displayName = playerName;
                    paint.SetColor(playerPaint);
                    var input = go.GetComponent<PlayerDriverInput>() ?? go.AddComponent<PlayerDriverInput>();
                    car.SetInputSource(input);
                    var health = go.GetComponent<CarHealth>();
                    if (health != null) { health.showScreenBar = true; health.showWorldBar = false; }
                    Player = participant;
                    AttachCamera(go);
                }
                else
                {
                    participant.displayName = aiNames.Length > 0 ? aiNames[aiIndex % aiNames.Length] : $"AI {aiIndex + 1}";
                    paint.SetColor(paints.Length > 0 ? paints[aiIndex % paints.Length] : Color.gray);
                    var profile = profiles != null && profiles.Length > 0 ? profiles[aiIndex % profiles.Length] : null;
                    var gunner = go.GetComponent<AiGunner>() ?? go.AddComponent<AiGunner>();
                    gunner.Initialize(profile, aiFireCooldown);
                    var ai = go.GetComponent<AiDriver>() ?? go.AddComponent<AiDriver>();
                    ai.Initialize(profile, lane * 0.4f);   // after AddComponent: OnEnable already ran with defaults
                    car.SetInputSource(ai);
                    var health = go.GetComponent<CarHealth>();
                    if (health != null) { health.showScreenBar = false; health.showWorldBar = true; }
                    aiIndex++;
                }

                participants.Add(participant);
            }

            foreach (var p in participants) if (p.Progress != null) p.Progress.BeginRace();
            if (Player == null || participants.Count == 0) { Clear(); return false; }
            return true;
        }

        public void Clear()
        {
            foreach (var p in participants) if (p != null) Destroy(p.gameObject);
            participants.Clear();
            Player = null;
        }

        private void AttachCamera(GameObject playerCar)
        {
            var follow = FindAnyObjectByType<CarCameraFollow>();
            if (follow == null) return;
            follow.target = playerCar.transform;
            follow.targetRigidbody = playerCar.GetComponent<Rigidbody>();
        }
    }
}
