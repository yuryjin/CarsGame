using System;
using System.Collections.Generic;
using UnityEngine;
using RacingGame.Core.Combat;

namespace RacingGame
{
    /// <summary>
    /// Represents an active or pooled oil slick on track.
    /// </summary>
    public struct OilSlick
    {
        public Vector3 Position;
        public int ExpireTick;
        public bool IsActive;
        public GameObject Visual;
    }

    /// <summary>
    /// Triggerless simulation-level oil puddle hazard pool.
    /// Spawns on car wrecks with a max capacity of 3 active slicks and 10-second lifetime.
    /// Performs distance checks in simulation ticks (2.5m radius) and applies Slowed status (2s, 0.4 grip factor).
    /// </summary>
    public class OilHazardPool : MonoBehaviour, ISimulated
    {
        public const int MaxSlicks = 3;
        public const float DefaultLifetimeSeconds = 10f;
        public const float HazardRadius = 2.5f;
        public const float HazardRadiusSqr = HazardRadius * HazardRadius; // 6.25
        public const float SlowedDurationSeconds = 2.0f;
        public const float SlowedGripMultiplier = 0.4f;

        private readonly OilSlick[] slicks = new OilSlick[MaxSlicks];
        private Transform slickRoot;
        private RaceDirector director;
        private Material oilMaterial;

        public int MaxCapacity => MaxSlicks;

        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < MaxSlicks; i++)
                {
                    if (slicks[i].IsActive) count++;
                }
                return count;
            }
        }

        public IReadOnlyList<OilSlick> Slicks => slicks;

        public OilSlick GetSlick(int index)
        {
            if (index < 0 || index >= MaxSlicks) return default;
            return slicks[index];
        }

        public int SimulationOrder => 1050;

        private void Awake()
        {
            director = GetComponent<RaceDirector>();
            EnsureVisuals();
        }

        private void OnEnable()
        {
            if (director == null) director = GetComponent<RaceDirector>();
            EnsureVisuals();
        }

        private void OnDestroy()
        {
            if (oilMaterial != null)
            {
                if (Application.isPlaying) Destroy(oilMaterial);
                else DestroyImmediate(oilMaterial);
                oilMaterial = null;
            }
        }

        private void EnsureVisuals()
        {
            if (slickRoot == null)
            {
                var existingRoot = transform.Find("OilSlicks");
                if (existingRoot != null) slickRoot = existingRoot;
                else
                {
                    var rootGo = new GameObject("OilSlicks");
                    rootGo.transform.SetParent(transform, false);
                    slickRoot = rootGo.transform;
                }
            }

            if (oilMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Standard")
                             ?? Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null)
                {
                    oilMaterial = new Material(shader) { name = "M_OilSlick" };
                    var oilColor = new Color(0.06f, 0.06f, 0.08f, 0.9f);
                    if (oilMaterial.HasProperty("_BaseColor")) oilMaterial.SetColor("_BaseColor", oilColor);
                    else if (oilMaterial.HasProperty("_Color")) oilMaterial.SetColor("_Color", oilColor);
                    if (oilMaterial.HasProperty("_Smoothness")) oilMaterial.SetFloat("_Smoothness", 0.95f);
                    if (oilMaterial.HasProperty("_Metallic")) oilMaterial.SetFloat("_Metallic", 0.2f);
                }
            }

            for (int i = 0; i < MaxSlicks; i++)
            {
                if (slicks[i].Visual == null)
                {
                    slicks[i].Visual = CreateVisual(slickRoot, i, oilMaterial);
                }
            }
        }

        private static GameObject CreateVisual(Transform parent, int index, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = $"OilSlick_{index}";
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(HazardRadius * 2f, 0.02f, HazardRadius * 2f);

            int layer = LayerMask.NameToLayer("Track");
            if (layer >= 0) go.layer = layer;

            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }

            var rend = go.GetComponent<Renderer>();
            if (rend != null && mat != null)
            {
                rend.sharedMaterial = mat;
            }

            go.SetActive(false);
            return go;
        }

        /// <summary>
        /// Spawns an oil slick at the specified position.
        /// If all 3 slots are active, recycles the oldest active slick.
        /// </summary>
        public int SpawnSlick(Vector3 position, int currentTick = -1, float lifetimeSeconds = DefaultLifetimeSeconds)
        {
            if (currentTick < 0)
            {
                currentTick = SimulationDriver.Instance != null ? SimulationDriver.Instance.Tick : 0;
            }

            EnsureVisuals();

            int slotIndex = -1;

            // 1. Look for an inactive slot
            for (int i = 0; i < MaxSlicks; i++)
            {
                if (!slicks[i].IsActive)
                {
                    slotIndex = i;
                    break;
                }
            }

            // 2. If all active, recycle the oldest slick (lowest ExpireTick)
            if (slotIndex < 0)
            {
                int minExpireTick = int.MaxValue;
                for (int i = 0; i < MaxSlicks; i++)
                {
                    if (slicks[i].ExpireTick < minExpireTick)
                    {
                        minExpireTick = slicks[i].ExpireTick;
                        slotIndex = i;
                    }
                }
            }

            if (slotIndex < 0) slotIndex = 0;

            int durationTicks = SimulationDriver.SecondsToTicks(lifetimeSeconds);
            int expireTick = currentTick + durationTicks;

            slicks[slotIndex].Position = position;
            slicks[slotIndex].ExpireTick = expireTick;
            slicks[slotIndex].IsActive = true;

            if (slicks[slotIndex].Visual != null)
            {
                slicks[slotIndex].Visual.transform.position = position + Vector3.up * 0.02f;
                slicks[slotIndex].Visual.SetActive(true);
            }

            return slotIndex;
        }

        /// <summary>
        /// Deactivates a specific oil slick slot.
        /// </summary>
        public void DespawnSlick(int index)
        {
            if (index < 0 || index >= MaxSlicks) return;
            slicks[index].IsActive = false;
            if (slicks[index].Visual != null)
            {
                slicks[index].Visual.SetActive(false);
            }
        }

        /// <summary>
        /// Deactivates all active slicks and hides visuals.
        /// </summary>
        public void ResetSlicks()
        {
            for (int i = 0; i < MaxSlicks; i++)
            {
                DespawnSlick(i);
            }
        }

        public void SimulationStep(int tick)
        {
            var cars = director != null ? director.Cars : null;
            SimulationStep(tick, cars);
        }

        /// <summary>
        /// Simulation step without PhysX triggers: expires old slicks and performs distance squared checks against active cars.
        /// </summary>
        public void SimulationStep(int tick, IReadOnlyList<CarController> cars)
        {
            for (int i = 0; i < MaxSlicks; i++)
            {
                if (!slicks[i].IsActive) continue;

                if (tick >= slicks[i].ExpireTick)
                {
                    DespawnSlick(i);
                    continue;
                }

                if (cars == null) continue;

                Vector3 slickPos = slicks[i].Position;
                int carCount = cars.Count;
                for (int c = 0; c < carCount; c++)
                {
                    var car = cars[c];
                    if (car == null || !car.gameObject.activeInHierarchy) continue;

                    var health = car.GetComponent<CarHealth>();
                    if (health != null && health.IsDead) continue;

                    Vector3 carPos = car.transform.position;
                    float dx = carPos.x - slickPos.x;
                    float dy = carPos.y - slickPos.y;
                    float dz = carPos.z - slickPos.z;
                    float distSqr = dx * dx + dy * dy + dz * dz;

                    if (distSqr <= HazardRadiusSqr)
                    {
                        var status = car.GetComponent<CarStatus>();
                        if (status != null)
                        {
                            status.ApplyStatus(StatusKind.Slowed, SlowedDurationSeconds);
                        }
                    }
                }
            }
        }
    }
}
