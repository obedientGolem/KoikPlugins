using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LBUtils
{
    [DefaultExecutionOrder(12200)]
    internal class LBUtilsLateSword : MonoBehaviour
    {
        private static Harmony _harmony;
        // Track called instances of lookat_dan each frame,
        // first call is suppressed.
        private static readonly Dictionary<Lookat_dan, int> _trackDic = [];

        private void Awake() => TryEnable();
        private void OnDestroy() => TryDisable();

        internal void TryEnable()
        {
            _harmony ??= Harmony.CreateAndPatchAll(typeof(LBUtilsLateSword));

            _trackDic.Clear();
        }

        internal void TryDisable()
        {
            _harmony?.UnpatchSelf();
            _harmony = null;

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
