using UnityEngine;

namespace RacingGame
{
    /// <summary>Sides of a co-op mission. Races and the arena tag nobody, so everyone stays everyone's rival there.</summary>
    public enum Team : byte { None = 0, Players = 1, Enemy = 2 }

    /// <summary>Marks a car or a mission unit as one side's. Put on the root; children inherit it.</summary>
    public sealed class TeamTag : MonoBehaviour
    {
        public Team team;
    }

    public static class Teams
    {
        public static Team Of(GameObject go)
        {
            if (go == null) return Team.None;
            var tag = go.GetComponentInParent<TeamTag>();
            return tag != null ? tag.team : Team.None;
        }

        /// <summary>Untagged (and owner-less, e.g. a track turret's shell) counts as hostile to everyone.</summary>
        public static bool Hostile(Team a, Team b) => a == Team.None || b == Team.None || a != b;

        public static bool Hostile(GameObject a, GameObject b) => a != b && Hostile(Of(a), Of(b));

        public static void Set(GameObject go, Team team)
        {
            var tag = go.GetComponent<TeamTag>() ?? go.AddComponent<TeamTag>();
            tag.team = team;
        }
    }
}
