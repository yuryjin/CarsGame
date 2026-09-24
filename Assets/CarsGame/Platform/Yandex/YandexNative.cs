#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;

namespace RacingGame.Platform.Yandex
{
    internal static class YandexNative
    {
        [DllImport("__Internal")] public static extern int CarsYa_Available();
        [DllImport("__Internal")] public static extern void CarsYa_Init(int reqId);
        [DllImport("__Internal")] public static extern IntPtr CarsYa_GetEnv();
        [DllImport("__Internal")] public static extern void CarsYa_LoadSave(int reqId);
        [DllImport("__Internal")] public static extern void CarsYa_QueueSave(string json);
        [DllImport("__Internal")] public static extern void CarsYa_FlushSave(int reqId);
        [DllImport("__Internal")] public static extern void CarsYa_ShowInterstitial(int reqId);
        [DllImport("__Internal")] public static extern void CarsYa_ShowRewarded(int reqId);
        [DllImport("__Internal")] public static extern void CarsYa_GameplayStart();
        [DllImport("__Internal")] public static extern void CarsYa_GameplayStop();
        [DllImport("__Internal")] public static extern void CarsYa_Ready();
        [DllImport("__Internal")] public static extern void CarsYa_RequestAuth(int reqId);
        [DllImport("__Internal")] public static extern void CarsYa_SubmitScore(int reqId, string board, double score, string extra);   // double: JS Number, no i64 ABI games
        [DllImport("__Internal")] public static extern void CarsYa_GetLeaderboard(int reqId, string board, int top, int includeUser, int around);
        [DllImport("__Internal")] public static extern void CarsYa_SessionsLoad(int reqId, int count, string meta);
        [DllImport("__Internal")] public static extern void CarsYa_SessionsCommit(string payload);
        [DllImport("__Internal")] public static extern void CarsYa_SessionsPush(int reqId, string meta);
        [DllImport("__Internal")] public static extern void CarsYa_Debug(string json);
        [DllImport("__Internal")] public static extern double CarsYa_ServerTime();   // ms; 0 = unknown
        [DllImport("__Internal")] public static extern void CarsYa_Free(IntPtr ptr);   // free() lives in JS: a C "_free" import does not link

        public static string TakeString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return null;
            string s = Marshal.PtrToStringUTF8(ptr);
            CarsYa_Free(ptr);
            return s;
        }
    }
}
#endif
