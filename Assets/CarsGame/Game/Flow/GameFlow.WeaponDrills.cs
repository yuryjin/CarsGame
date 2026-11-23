using RacingGame.Core.Combat;
using UnityEngine;

namespace RacingGame
{
    public sealed partial class GameFlow
    {
        private RaceParticipant drillTarget;
        public bool StartWeaponDrill(string bodyId, PracticeLesson drill) =>
            drill >= PracticeLesson.CannonTarget && drill <= PracticeLesson.PursuitMine && StartDrive(bodyId, drill);

        private void ConfigureGarageDrill()
        {
            var player = race.Player;
            foreach (var other in race.Participants) if (!other.isPlayer) { drillTarget = other; break; }
            if (player == null || drillTarget == null) return;
            var weapons = player.Weapons;
            var first = weapons.LoadoutFirst;
            if (CurrentPractice == PracticeLesson.CannonTarget) first = PrimaryWeaponType.Cannon;
            if (CurrentPractice == PracticeLesson.BeamShield && PrimaryWeaponCatalog.Get(first).Mode != WeaponMode.Beam)
                first = PrimaryWeaponCatalog.Get(weapons.LoadoutSecond).Mode == WeaponMode.Beam ? weapons.LoadoutSecond : PrimaryWeaponType.TractorBeam;
            if (CurrentPractice == PracticeLesson.PursuitMine)
            {
                weapons.secondaryType = SecondaryWeaponType.Mines;
                weapons.secondaryAmmoMax = Mathf.Max(3, weapons.secondaryAmmoMax);
                weapons.RefillSecondaryAmmo();
            }
            else if (first != weapons.PrimaryType)
            {
                // Preserve the gun's upgrade multipliers when handing out range equipment.
                weapons.ConfigureLoadout(true, first, weapons.LoadoutFirst);
                var equipment = PlatformGlue.Save.Data.progress;
                PlayerProgression.ApplyTo(null, null, player.Car.Cannon, equipment.upgrades, (Core.Economy.CarBuild)equipment.buildPreset);
            }
            practiceDirector = RaceDirector.Current;
            if (practiceDirector != null)
            {
                practiceDirector.ShellHit += OnDrillShot;
                practiceDirector.MineHit += OnDrillMine;
            }
            drillTarget.Health.HitResolved += OnDrillShield;
            race.OnRaceStarted += PositionDrillCars;
        }

        private void PositionDrillCars()
        {
            var path = TrackPath.Instance;
            if (path == null || drillTarget == null) return;
            float start = path.startLineDistance + 20f;
            void Place(RaceParticipant driver, float distance)
            {
                path.Pose(distance, 0f, out var position, out var rotation);
                driver.Car.Teleport(position + Vector3.up * .45f, rotation);
            }
            Place(race.Player, start);
            Place(drillTarget, start + (CurrentPractice == PracticeLesson.PursuitMine ? -15f : 10f));
            if (CurrentPractice == PracticeLesson.BeamShield)
                drillTarget.Status.ApplyStatus(StatusKind.Shield, 3600f);
        }

        private void OnDrillShot(GameObject attacker, GameObject victim)
        {
            if (CurrentPractice == PracticeLesson.CannonTarget && drillTarget != null && victim == drillTarget.gameObject
                && attacker == race.Player.gameObject && race.Player.Weapons.PrimaryType == PrimaryWeaponType.Cannon) CompletePractice();
        }

        private void OnDrillShield(HitInfo hit, HitResult result)
        {
            if (CurrentPractice == PracticeLesson.BeamShield && result.ShieldAbsorbed && hit.HitType == HitType.Direct
                && hit.Attacker == race.Player.gameObject && PrimaryWeaponCatalog.Get(race.Player.Weapons.PrimaryType).Mode == WeaponMode.Beam) CompletePractice();
        }

        private void OnDrillMine(GameObject owner, GameObject victim)
        {
            if (CurrentPractice == PracticeLesson.PursuitMine && drillTarget != null && owner == race.Player.gameObject && victim == drillTarget.gameObject) CompletePractice();
        }

        private void RestoreGarageDrill()
        {
            race.OnRaceStarted -= PositionDrillCars;
            if (drillTarget != null) drillTarget.Health.HitResolved -= OnDrillShield;
            if (practiceDirector != null) { practiceDirector.ShellHit -= OnDrillShot; practiceDirector.MineHit -= OnDrillMine; }
            drillTarget = null;
        }
    }
}
