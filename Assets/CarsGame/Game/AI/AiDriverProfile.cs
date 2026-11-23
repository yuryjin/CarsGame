using UnityEngine;

namespace RacingGame
{
    public enum RivalRole { Racer, Shooter, Defender }

    /// <summary>Personality of one AI driver. Several assets give a field of distinguishable rivals.</summary>
    [CreateAssetMenu(menuName = "Racing/AI Driver Profile", fileName = "AiProfile")]
    public sealed class AiDriverProfile : ScriptableObject
    {
        public RivalRole role;
        public string rivalName = "";
        public string rivalId = "";
        public bool signaturePrimary;
        public PrimaryWeaponType signatureWeapon;
        [Min(0f)] public float signatureMinRange = 6f;
        [Min(1f)] public float signatureMaxRange = 45f;
        public Color paint = Color.gray;

        public void Equip(CarWeapons weapons)
        {
            weapons.SetPrimaryType(signaturePrimary && PrimaryWeaponCatalog.IsValid(signatureWeapon) ? signatureWeapon
                : role == RivalRole.Shooter ? PrimaryWeaponType.MachineGun : PrimaryWeaponType.Cannon);
            weapons.secondaryType = role == RivalRole.Defender ? SecondaryWeaponType.Mines : SecondaryWeaponType.None;
            weapons.secondaryAmmoMax = role == RivalRole.Defender ? 3 : 0;
            weapons.RefillSecondaryAmmo();
        }

        [Tooltip("Top speed as a fraction of the car's maxSpeed.")]
        [Range(0.5f, 1.2f)] public float topSpeedFactor = 0.9f;
        [Tooltip("How fast the driver dares to take corners (1 = model estimate).")]
        [Range(0.6f, 1.3f)] public float corneringConfidence = 1f;
        [Tooltip("Random steering jitter amplitude (fraction of full lock).")]
        [Range(0f, 0.3f)] public float steerNoise = 0.04f;
        [Tooltip("Seconds between deciding and acting.")]
        [Range(0f, 0.4f)] public float reactionDelay = 0.1f;
        [Tooltip("0 = polite (yields, rarely fires), 1 = rams and fires whenever possible.")]
        [Range(0f, 1f)] public float aggression = 0.5f;
        [Tooltip("Preferred lateral position on the track (m, +right).")]
        [Range(-3f, 3f)] public float baseLane = 0f;

        public static AiDriverProfile Default
        {
            get
            {
                if (defaultProfile == null) defaultProfile = CreateInstance<AiDriverProfile>();
                return defaultProfile;
            }
        }
        private static AiDriverProfile defaultProfile;
    }
}
