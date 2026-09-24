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

        [Header("Bodies")]
        [Tooltip("Player body id (CarBodies). Empty = the one selected in the save, else the standard car.")]
        public string playerBodyId = "";
        [Tooltip("Rivals drive bodies that fit their role (seeded by the event). Off = everyone in the standard car.")]
        public bool aiBodyVariety = true;

        public RaceParticipant Player { get; private set; }
        public IReadOnlyList<RaceParticipant> Participants => participants;
        public ArenaLayout Arena { get; private set; }
        private readonly List<RaceParticipant> participants = new List<RaceParticipant>();

        private void Start()
        {
            if (spawnOnStart) Spawn();
        }

        /// <summary>Spawns the field. Returns false (and leaves nothing behind) when the setup is incomplete.</summary>
        public bool Spawn(bool arena = false)
        {
            if (track == null) track = TrackPath.Instance;
            if (grid == null) grid = FindAnyObjectByType<StartGrid>();
            if (raceCarPrefab == null || (!arena && (track == null || grid == null)))
            {
                Debug.LogError("[RaceSpawner] prefab, TrackPath and StartGrid are required.", this);
                Clear();
                return false;
            }

            Clear();
            if (arena)
            {
                var renderer = raceCarPrefab.GetComponentInChildren<Renderer>();
                Arena = ArenaLayout.Create(renderer != null ? renderer.sharedMaterial : null);
            }
            int total = aiCount + 1;
            int playerSlot = playerGridSlot < 0 ? total - 1 : Mathf.Clamp(playerGridSlot, 0, total - 1);
            int aiIndex = 0;
            for (int slot = 0; slot < total; slot++)
            {
                bool isPlayer = slot == playerSlot;
                Vector3 pos; Quaternion rot; float lane = 0f;
                if (arena) Arena.GetSpawnPose(slot, total, out pos, out rot);
                else grid.GetSlot(slot, track, out pos, out rot, out lane);
                var go = Instantiate(raceCarPrefab, pos, rot);
                go.name = isPlayer ? "Player_Car" : $"AI_Car_{aiIndex + 1}";
                CarBody.Apply(go, isPlayer ? PlayerBody() : RivalBody(aiIndex));   // before drivers and upgrades, which build on it
                var car = go.GetComponent<CarController>();
                var participant = go.GetComponent<RaceParticipant>() ?? go.AddComponent<RaceParticipant>();
                var paint = go.GetComponent<CarPaint>() ?? go.AddComponent<CarPaint>();
                participant.isPlayer = isPlayer;
                participant.laneOffset = lane;
                participant.Arena = Arena;
                participant.Progress.enabled = !arena;
                if (participant.Health != null)
                {
                    participant.Health.respawnOnDeath = true;
                    if (arena)
                    {
                        participant.Health.respawnDelay = 2f;
                        participant.Health.respawnInvulnerability = 2f;
                    }
                }

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
                    if (profile != null)
                    {
                        if (!string.IsNullOrEmpty(profile.rivalName))
                            participant.displayName = profile.rivalName + (aiIndex >= profiles.Length ? " " + (aiIndex / profiles.Length + 1) : "");
                        paint.SetColor(profile.paint);
                        profile.Equip(participant.Weapons);
                    }
                    var gunner = go.GetComponent<AiGunner>() ?? go.AddComponent<AiGunner>();
                    gunner.Initialize(profile, aiFireCooldown);
                    if (arena)
                    {
                        var ai = go.AddComponent<ArenaDriver>();
                        ai.Initialize(Arena, profile);
                        car.SetInputSource(ai);
                    }
                    else
                    {
                        var ai = go.GetComponent<AiDriver>() ?? go.AddComponent<AiDriver>();
                        ai.Initialize(profile, lane); // keep the two grid columns apart
                        car.SetInputSource(ai);
                    }
                    var health = go.GetComponent<CarHealth>();
                    if (health != null) { health.showScreenBar = false; health.showWorldBar = true; }
                    aiIndex++;
                }

                if (go.GetComponent<CarDamageSmoke>() == null && go.GetComponent<CarHealth>() != null) go.AddComponent<CarDamageSmoke>();
                participants.Add(participant);
            }

            foreach (var p in participants) if (p.Progress != null) p.Progress.BeginRace();
            if (Player == null || participants.Count == 0) { Clear(); return false; }
            return true;
        }

        private Core.Economy.BodyDef PlayerBody()
        {
            if (!string.IsNullOrEmpty(playerBodyId)) return Core.Economy.CarBodies.Get(playerBodyId);
            var progress = PlatformGlue.Save?.Data.progress;
            return Core.Economy.CarBodies.Get(progress != null ? Core.Economy.CarBodies.Selected(progress.cosmetics, progress.body) : null);
        }

        private Core.Economy.BodyDef RivalBody(int aiIndex)
        {
            if (!aiBodyVariety) return Core.Economy.CarBodies.Default;
            var profile = profiles != null && profiles.Length > 0 ? profiles[aiIndex % profiles.Length] : null;
            string eventId = RaceSetup.Event != null ? RaceSetup.Event.eventId : "";
            int seed = 17;
            foreach (char ch in eventId) seed = seed * 31 + ch;   // stable across runs, unlike string.GetHashCode
            return Core.Economy.CarBodies.ForRival(profile != null ? (int)profile.role : 0, aiIndex, seed);
        }

        public void Clear()
        {
            foreach (var p in participants)
            {
                if (p != null)
                {
                    p.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(p.gameObject);
                    else DestroyImmediate(p.gameObject);
                }
            }
            participants.Clear();
            Player = null;
            if (Arena != null)
            {
                Arena.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(Arena.gameObject);
                else DestroyImmediate(Arena.gameObject);
                Arena = null;
            }
        }

        private void AttachCamera(GameObject playerCar)
        {
            var follow = FindAnyObjectByType<CarCameraFollow>();
            if (follow == null) return;
            follow.target = playerCar.transform;
            follow.targetRigidbody = playerCar.GetComponent<Rigidbody>();
            follow.transform.position = playerCar.transform.TransformPoint(follow.offset);
            follow.transform.LookAt(playerCar.transform.position + Vector3.up * follow.lookAtHeightOffset);
        }
    }
}
