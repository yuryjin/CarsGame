using System;
using UnityEngine;
using RacingGame.Core.Combat;
using RacingGame.Core.Sim;

namespace RacingGame
{
    public enum PrimaryWeaponType
    {
        Cannon,
        MachineGun,
        Shotgun,
        TwinCannon,
        Autocannon,
        Sniper,
        FreezeGun,
        EmpGun,
        SpikeGun,
        HeavyCannon,
        Boxer,
        CircularSaw,
        Drill,
        Sledgehammer,
        BattleAxe,
        Chainsaw,
        LaserSaw,
        Welder,
        SpringSpear,
        WreckingBall,
        Flamethrower,
        TeslaCoil,
        Harpoon,
        TractorBeam,
        Repulsor,
        SonicCannon,
        PlasmaCutter,
        AcidSprayer,
        CryoJet,
        NailFan,
        GiantBanana,
        RubberChicken,
        GloveLauncher,
        AnvilLauncher,
        BowlingBall,
        DiscoPulse,
        ConfettiCannon,
        GiantBoot,
        FryingPan,
        Plunger,
        MineDispenser,
        BearTrap,
        GlueTrap,
        IceTrap,
        EmpMine,
        SpringTrap,
        CactusMine,
        WaterCannon,
        RicochetSaw,
        HomingRocket
    }

    public enum SecondaryWeaponType
    {
        None,
        Mines,
        FreezeLauncher,
        /// <summary>Planted charge: first press plants it, the next press sets it off (or the fuse does).</summary>
        RemoteCharge
    }

    /// <summary>
    /// Coordinates primary and secondary weapon slots, heat/overheat mechanics,
    /// cooldowns and ammo management in simulation ticks.
    /// </summary>
    [DisallowMultipleComponent]
    public class CarWeapons : MonoBehaviour, ISimulated
    {
        [Header("Primary Weapon Configuration")]
        public PrimaryWeaponType primaryType = PrimaryWeaponType.Cannon;
        [Tooltip("Heat buildup per shot.")]
        public float heatPerShot = 2.5f;
        [Tooltip("Maximum heat capacity.")]
        public float heatMax = 100f;
        [Tooltip("Heat cooldown rate per second.")]
        public float heatCooldownRate = 20f;
        [Tooltip("Heat threshold that triggers an overheat lock.")]
        public float overheatThreshold = 100f;
        [Tooltip("Heat must drop to or below this threshold to clear overheat.")]
        public float overheatRecoveryThreshold = 40f;
        [Tooltip("The driver may swap to the machine gun mid-race (bought in the garage).")]
        public bool machineGunUnlocked;
        public bool CompactLoadout { get; private set; }
        public PrimaryWeaponType LoadoutFirst { get; private set; }
        public PrimaryWeaponType LoadoutSecond { get; private set; }

        public void ConfigureLoadout(bool compact, PrimaryWeaponType first, PrimaryWeaponType second)
        {
            LoadoutFirst = CanOwn(first) ? first : PrimaryWeaponType.Cannon;
            LoadoutSecond = CanOwn(second) && second != LoadoutFirst ? second
                : LoadoutFirst == PrimaryWeaponType.Shotgun ? PrimaryWeaponType.Cannon : PrimaryWeaponType.Shotgun;
            CompactLoadout = compact;
            SetPrimaryType(LoadoutFirst);
        }

        private bool CanOwn(PrimaryWeaponType type) => PrimaryWeaponCatalog.IsValid(type)
            && (type != PrimaryWeaponType.MachineGun || machineGunUnlocked);

        public int LoadoutSlot(int index) => (int)(index == 0 ? LoadoutFirst : LoadoutSecond) + 1;
        [Tooltip("Machine gun heat upgrade: scales heat per shot, divides the cooling rate (<1 = cooler).")]
        public float machineGunHeatFactor = 1f;
        [Tooltip("Seconds after a mid-race swap before the new primary can fire.")]
        public float primarySwitchSeconds = 0.3f;

        [Header("Secondary Weapon Configuration")]
        public SecondaryWeaponType secondaryType = SecondaryWeaponType.Mines;
        [Tooltip("Current secondary ammunition/charges.")]
        public int secondaryAmmo = 3;
        [Tooltip("Maximum secondary ammunition capacity.")]
        public int secondaryAmmoMax = 3;
        [Tooltip("Cooldown between secondary uses in seconds.")]
        public float secondaryCooldown = 3f;
        [Tooltip("Damage at the centre of a planted charge's blast (garage upgrades raise it).")]
        public float chargeDamage = 45f;

        public event Action<float, float> HeatChanged; // current, max
        public event Action<int, int> SecondaryAmmoChanged; // current, max
        public event Action<bool> OverheatChanged; // isOverheated
        public event Action<PrimaryWeaponType> PrimarySwitched; // mid-race swap done
        public event Action PrimarySwitchDenied; // asked for the machine gun without owning it
        public event Action ChargeDetonated; // the driver set off their planted charge

        private CarController car;
        private CarStatus status;
        private Rigidbody rb;
        private bool wantsSecondary;
        private bool secondaryHeld;   // charges act on the press, so holding the button never plants and blows in one go
        private bool wantsPrimary;
        private int nextSecondaryTick;
        private int lastTickedTick = -1;

        public float CurrentHeat { get; private set; }
        public bool IsOverheated { get; private set; }
        public PrimaryWeaponType PrimaryType => primaryType;
        public SecondaryWeaponType SecondaryType => secondaryType;
        public int SecondaryAmmo => secondaryAmmo;
        public int SecondaryAmmoMax => secondaryAmmoMax;
        public int SimulationOrder => 950 + (car != null ? car.CarId : 0);

        public bool IsSecondaryReady(int tick) => tick >= nextSecondaryTick && (secondaryAmmoMax <= 0 || secondaryAmmo > 0);

        /// <summary>This car's charge still on the ground (armed or arming), or null.</summary>
        public MineHazard PlantedCharge
        {
            get
            {
                if (secondaryType != SecondaryWeaponType.RemoteCharge) return null;
                var director = RaceDirector.Current;
                return director != null ? director.FindCharge(gameObject) : null;
            }
        }

        private void Awake()
        {
            car = GetComponent<CarController>();
            status = GetComponent<CarStatus>();
            rb = GetComponent<Rigidbody>();
            secondaryAmmo = secondaryAmmoMax;
            SetPrimaryType(primaryType);
        }

        public void SetPrimaryType(PrimaryWeaponType type)
        {
            primaryType = PrimaryWeaponCatalog.IsValid(type) ? type : PrimaryWeaponType.Cannon;
            ApplyHeatProfile();
            var cannon = GetComponent<CarCannon>();
            if (cannon != null)
            {
                cannon.ApplyPrimaryProfile(primaryType);
            }
        }

        /// <summary>
        /// Mid-race swap (keys 1–0). Unlike <see cref="SetPrimaryType"/> it keeps the upgrade multipliers,
        /// the heat and the cannon's half-used magazine, and holds fire for <see cref="primarySwitchSeconds"/>.
        /// </summary>
        public bool TrySwitchPrimary(PrimaryWeaponType type, int tick)
        {
            if (!PrimaryWeaponCatalog.IsValid(type) || type == primaryType) return false;
            if (!IsPrimaryAvailable(type))
            {
                PrimarySwitchDenied?.Invoke();
                return false;
            }
            primaryType = type;
            ApplyHeatProfile();
            var cannon = GetComponent<CarCannon>();
            if (cannon != null) cannon.SwitchPrimaryProfile(type, tick + SimulationDriver.SecondsToTicks(primarySwitchSeconds));
            PrimarySwitched?.Invoke(type);
            return true;
        }

        public bool IsPrimaryAvailable(PrimaryWeaponType type) => CanOwn(type)
            && (!CompactLoadout || type == LoadoutFirst || type == LoadoutSecond);

        public int CyclePrimarySlot(int currentSlot, int direction)
        {
            int slot = Mathf.Clamp(currentSlot, 1, PrimaryWeaponCatalog.Count) - 1;
            for (int i = 0; i < PrimaryWeaponCatalog.Count; i++)
            {
                slot = (slot + (direction >= 0 ? 1 : -1) + PrimaryWeaponCatalog.Count) % PrimaryWeaponCatalog.Count;
                if (IsPrimaryAvailable((PrimaryWeaponType)slot)) return slot + 1;
            }
            return currentSlot;
        }

        private void ApplyHeatProfile()
        {
            if (primaryType == PrimaryWeaponType.MachineGun)
            {
                heatPerShot = 2.5f * machineGunHeatFactor;
                heatCooldownRate = 20f / machineGunHeatFactor;
            }
            else // Cannon
            {
                heatPerShot = PrimaryWeaponCatalog.Get(primaryType).Heat;
                heatCooldownRate = 20f;
            }
            overheatThreshold = 100f;
            overheatRecoveryThreshold = 40f;
        }

        private void OnEnable()
        {
            if (SimulationDriver.Instance != null)
            {
                SimulationDriver.Instance.Register(this);
            }
        }

        private void OnDisable()
        {
            var driver = SimulationDriver.Current;
            if (driver != null)
            {
                driver.Unregister(this);
            }
        }

        public void SetWantsSecondary(bool value)
        {
            wantsSecondary = value;
        }

        /// <summary>
        /// Records the primary trigger state for deterministic heat handling. Heat does not
        /// dissipate while the trigger is held; cooling resumes as soon as it is released.
        /// </summary>
        public void SetWantsPrimary(bool value)
        {
            wantsPrimary = value;
        }

        public void SimulationStep(int tick)
        {
            if (tick == lastTickedTick) return;
            lastTickedTick = tick;

            if (!wantsPrimary)
            {
                CoolHeat(SimulationDriver.FixedStep);
            }

            bool pressed = wantsSecondary && !secondaryHeld;
            secondaryHeld = wantsSecondary;
            if (secondaryType == SecondaryWeaponType.RemoteCharge ? pressed : wantsSecondary)
            {
                TryFireSecondary(tick);
            }
        }

        public void AddHeat(float amount)
        {
            if (amount <= 0f) return;
            CurrentHeat = Mathf.Clamp(CurrentHeat + amount, 0f, heatMax);
            if (!IsOverheated && CurrentHeat >= overheatThreshold)
            {
                IsOverheated = true;
                OverheatChanged?.Invoke(true);
            }
            HeatChanged?.Invoke(CurrentHeat, heatMax);
        }

        public void CoolHeat(float dt)
        {
            if (CurrentHeat > 0f)
            {
                CurrentHeat = Mathf.Max(0f, CurrentHeat - heatCooldownRate * dt);
                if (IsOverheated && CurrentHeat <= overheatRecoveryThreshold)
                {
                    IsOverheated = false;
                    OverheatChanged?.Invoke(false);
                }
                HeatChanged?.Invoke(CurrentHeat, heatMax);
            }
        }

        public void ResetHeat()
        {
            CurrentHeat = 0f;
            if (IsOverheated)
            {
                IsOverheated = false;
                OverheatChanged?.Invoke(false);
            }
            HeatChanged?.Invoke(0f, heatMax);
        }

        public bool TryFireSecondary(int tick)
        {
            if (!enabled) return false;
            if (status == null) status = GetComponent<CarStatus>();
            if (status != null && status.IsJammed) return false;
            if (secondaryType == SecondaryWeaponType.None) return false;
            if (secondaryType == SecondaryWeaponType.RemoteCharge)
            {
                // A charge on the ground turns the button into the detonator; the cooldown only gates planting.
                var planted = PlantedCharge;
                if (planted != null)
                {
                    if (!planted.TryDetonateCharge(tick)) return false;
                    ChargeDetonated?.Invoke();
                    return true;
                }
            }
            if (tick < nextSecondaryTick) return false;
            if (secondaryAmmoMax > 0 && secondaryAmmo <= 0) return false;

            if (secondaryType == SecondaryWeaponType.Mines)
            {
                var director = RaceDirector.Current ?? RaceDirector.Instance;
                if (director != null)
                {
                    Vector3 dropPos = transform.position - transform.forward * 2.2f + Vector3.up * 0.1f;
                    director.RequestMine(new MineSpawn
                    {
                        Owner = gameObject,
                        OwnerId = car != null ? car.CarId : 0,
                        Position = dropPos,
                        Rotation = transform.rotation,
                        Damage = 25f,
                        Tick = tick
                    });
                }
            }
            else if (secondaryType == SecondaryWeaponType.RemoteCharge)
            {
                var director = RaceDirector.Current ?? RaceDirector.Instance;
                if (director != null)
                {
                    director.RequestMine(new MineSpawn
                    {
                        Owner = gameObject,
                        OwnerId = car != null ? car.CarId : 0,
                        Position = transform.position - transform.forward * 2.4f + Vector3.up * 0.1f,
                        Rotation = transform.rotation,
                        Damage = chargeDamage,
                        Tick = tick,
                        Kind = MineKind.Charge
                    });
                }
            }
            else if (secondaryType == SecondaryWeaponType.FreezeLauncher)
            {
                var director = RaceDirector.Current ?? RaceDirector.Instance;
                if (director != null)
                {
                    Vector3 spawnPos = transform.position + transform.forward * 2.0f + Vector3.up * 0.8f;
                    Vector3 inherited = rb != null ? rb.linearVelocity : Vector3.zero;
                    Vector3 vel = transform.forward * 40f + inherited;
                    director.RequestProjectile(new ProjectileSpawn
                    {
                        Owner = gameObject,
                        OwnerId = car != null ? car.CarId : 0,
                        Position = spawnPos,
                        Velocity = vel,
                        Damage = 5f,
                        Tick = tick,
                        Kind = ProjectileKind.Freeze,
                        Status = StatusKind.Frozen,
                        StatusDuration = 1.5f
                    });
                }
            }

            if (secondaryAmmoMax > 0)
            {
                secondaryAmmo--;
                SecondaryAmmoChanged?.Invoke(secondaryAmmo, secondaryAmmoMax);
            }

            nextSecondaryTick = tick + SimulationDriver.SecondsToTicks(secondaryCooldown);
            return true;
        }

        public void RefillSecondaryAmmo()
        {
            secondaryAmmo = secondaryAmmoMax;
            SecondaryAmmoChanged?.Invoke(secondaryAmmo, secondaryAmmoMax);
        }

        public void RefillAll()
        {
            ResetHeat();
            RefillSecondaryAmmo();
            var cannon = GetComponent<CarCannon>();
            if (cannon != null)
            {
                cannon.RefillAmmo();
            }
        }
    }
}
