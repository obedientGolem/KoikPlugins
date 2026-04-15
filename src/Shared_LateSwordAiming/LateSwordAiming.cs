using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Koik_LateSwordAiming
{

    [BepInPlugin(GUID, Name, Version)]
    [DefaultExecutionOrder(12200)]
    public class LateSwordAiming : BaseUnityPlugin
    {
        public const string GUID = "koik.lateSwordAiming";
        public const string Name = "LateSwordAiming" +
#if DEBUG
            " Debug"
#endif
            ;
        public const string Version = "1.0.0";

        internal static new ManualLogSource Logger;
        private static Harmony _harmony;
        // Track called instances of lookat_dan each frame,
        // first call is suppressed.
        private static readonly Dictionary<Lookat_dan, int> _trackDic = [];
        //private static int _trackFrameBP;

        private void Awake()
        {
            Logger = base.Logger;
        }

        public static void Enable()
        {
            _trackDic.Clear();

            _harmony ??= Harmony.CreateAndPatchAll(typeof(LateSwordAiming));
        }

        public static void Disable()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            _trackDic.Clear();
        }

        private void LateUpdate()
        {
            foreach (var kv in _trackDic)
            {
                var behavior = kv.Key;

                if (kv.Key == null)
                {
                    _trackDic.Clear();
                    break;
                }

                if (!behavior.enabled || behavior.male == null) continue;

                behavior.LateUpdate();
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Lookat_dan), nameof(Lookat_dan.LateUpdate))]
        public static bool Lookat_dan_LateUpdatePrefix(Lookat_dan __instance)
        {
            if (_trackDic.ContainsKey(__instance))
            {
                var frameCount = Time.frameCount;

                if (_trackDic[__instance] != frameCount)
                {
                    _trackDic[__instance] = frameCount;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                _trackDic.Add(__instance, Time.frameCount);
            }
            return false;
        }

        //[HarmonyPrefix]
        //[HarmonyPatch("Core_BetterPenetration.CoreGame, KKS_BetterPenetration", "LookAtDanUpdate")]
        //public static bool BetterPen_Postfix(object __instance)
        //{
        //    var frameCount = Time.frameCount;

        //    if (_trackFrameBP != frameCount)
        //    {
        //        Logger.LogDebug($"BP: Don't run [{frameCount}]");
        //        _trackFrameBP = frameCount;

        //        return false;
        //    }
        //    else
        //    {
        //        Logger.LogDebug($"BP: Run [{frameCount}]");
        //        return true;
        //    }
        //}

    }
}
