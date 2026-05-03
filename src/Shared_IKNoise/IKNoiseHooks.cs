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
        public static void ChaControl_setPlayPostfix(string _strAnmName, int _nLayer, ChaControl __instance)
        {
            var component = __instance.GetComponent<IKNoiseCharaController>();

            if (component == null) return;

            component.effector?.OnSetPlay();
        }
    }
}
