using System;
using RacingGame.Core.Economy;
using RacingGame.Core.Save;

namespace RacingGame
{
    public sealed partial class GameFlow
    {
        private FriendChallenge friendChallenge;
        private RaceEventDefinition friendEvent, eventBeforeChallenge;
        private DifficultyTier tierBeforeChallenge;
        private string bodyBeforeChallenge;
        public int FriendTargetMs => friendChallenge?.targetMs ?? 0;

        public bool StartFriendChallenge(string code, out string error)
        {
            var challenge = FriendChallenge.Decode(code, out error);
            if (challenge == null) return false;
            error = "challenge.unavailable";
            if (State != FlowState.Menu || !PlatformGlue.IsReady || PlatformGlue.Save.LocalVersionTooNew || race.spawner == null) return false;
            var ev = Array.Find(events, e => e != null && e.eventId == challenge.eventId);
            var s = challenge.setup;
            var tier = Array.Find(tiers ?? Array.Empty<DifficultyTier>(), t => t != null && t.tierId == s.tier);
            var garage = PlatformGlue.Save.Data.progress;
            bool PrimaryAvailable(int type) => PrimaryWeaponCatalog.IsValid((PrimaryWeaponType)type)
                && (type != (int)PrimaryWeaponType.MachineGun || s.weapons[(int)WeaponKind.MachineGun] > 0);
            if (ev == null || ev.mode != EventMode.Race || ev.laps != s.laps || !IsUnlocked(ev) || tier == null || !CarBodies.Owns(garage.cosmetics, s.body)
                || !PrimaryAvailable(s.primary) || !PrimaryAvailable(s.reserve) || !s.fullArsenal && s.primary == s.reserve
                || !OwnedLevels(garage.upgrades, s.upgrades) || !OwnedLevels(garage.weapons, s.weapons)) return false;
            if (ev.twist != Core.Daily.RaceTwist.MinesOnly && s.secondary != 0 && s.weapons[(int)PlayerProgression.KindOf((SecondaryWeaponType)s.secondary)] == 0) return false;
            Teardown(); ConfigureCircuit(ev, true);
            bool matching = TrackHash(ev) == s.trackHash;
            Teardown();
            if (!matching) return false;

            friendChallenge = challenge; friendEvent = ev; eventBeforeChallenge = RaceSetup.Event;
            bodyBeforeChallenge = race.spawner.playerBodyId; tierBeforeChallenge = RaceSetup.Tier;
            race.spawner.playerBodyId = s.body;
            StartRace(ev, tier);
            if (State != FlowState.Racing) { RestoreFriendChallenge(); return false; }
            error = null; return true;
        }

        private static bool OwnedLevels(int[] owned, int[] required)
        {
            for (int i = 0; i < required.Length; i++) if (required[i] > (owned != null && i < owned.Length ? owned[i] : 0)) return false;
            return true;
        }

        private ProgressData ChallengeEquipment()
        {
            var s = friendChallenge.setup;
            return new ProgressData { body = s.body, buildPreset = s.build, fullArsenal = s.fullArsenal,
                upgrades = s.upgrades, weapons = s.weapons, equippedPrimary = s.primary, reservePrimary = s.reserve, equippedSecondary = s.secondary };
        }

        private void RestoreFriendChallenge()
        {
            if (friendChallenge == null) return;
            if (race != null && race.spawner != null) race.spawner.playerBodyId = bodyBeforeChallenge;
            RaceSetup.Event = eventBeforeChallenge; RaceSetup.Tier = tierBeforeChallenge;
            friendChallenge = null; friendEvent = eventBeforeChallenge = null; tierBeforeChallenge = null; bodyBeforeChallenge = null;
        }
    }
}
