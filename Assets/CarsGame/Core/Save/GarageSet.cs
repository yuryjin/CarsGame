using System;

namespace RacingGame.Core.Save
{
    [Serializable] public class GarageSet
    {
        public const int Slots = 3, NameLimit = 24;
        public string name, body, paint, livery, stripe;
        public int primary, reserve, secondary;
        public bool fullArsenal;
        public long writtenAt;

        public static GarageSet At(GarageSet[] sets, int index) =>
            sets != null && index >= 0 && index < sets.Length && !string.IsNullOrWhiteSpace(sets[index]?.name) ? sets[index] : null;

        public static GarageSet[] Merge(GarageSet[] a, GarageSet[] b)
        {
            var result = new GarageSet[Slots];
            for (int i = 0; i < Slots; i++)
            {
                var left = At(a, i); var right = At(b, i);
                if (left == null) result[i] = right;
                else if (right == null) result[i] = left;
                else
                {
                    int order = left.writtenAt.CompareTo(right.writtenAt);
                    if (order == 0) order = string.CompareOrdinal(UnityEngine.JsonUtility.ToJson(left), UnityEngine.JsonUtility.ToJson(right));
                    result[i] = order >= 0 ? left : right;
                }
            }
            return result;
        }
    }
}
