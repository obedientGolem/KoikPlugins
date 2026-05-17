using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace IKNoise
{
    internal static class IKNoiseHooks
    {
        private static Harmony _patch;
        internal static void TryEnable()
        {
            _patch ??= Harmony.CreateAndPatchAll(typeof(IKNoiseHooks));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.setPlay))]
        public static void ChaControl_setPlayPostfix(ChaControl __instance)
        {
            var component = __instance.GetComponent<IKNoiseCharaController>();

            if (component != null) 
                component.effector?.OnSetPlay();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.LoadAnimation))]
        public static void ChaControl_LoadAnimationPostfix(ChaControl __instance)
        {

            var component = __instance.GetComponent<IKNoiseCharaController>();

            if (component != null) 
                component.effector?.OnLoadAnimation();
        }
    }
}
