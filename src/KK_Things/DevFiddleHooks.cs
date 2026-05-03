using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KK_Things
{
    internal static class DevFiddleHooks
    {
        private static Harmony _patch;
        internal static void HandleEnable()
        {
            _patch ??= Harmony.CreateAndPatchAll(typeof(DevFiddleHooks));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.LoadAnimation))]
        public static void ChaControl_LoadAnimationPostfix(ChaControl __instance)
        {
            var component = __instance.GetComponent<DevFiddleCharaController>();

            if (component == null) return;

            component.OnLoadAnimation();
#if KK
            if (__instance.sex == 1 || !AniMorphPlugin.MaleEnableDB.Value) return;

            foreach (ChaInfo.DynamicBoneKind dbKind in Enum.GetValues(typeof(ChaInfo.DynamicBoneKind)))
            {
                __instance.playDynamicBoneBust(dbKind, true);
            }
#endif
        }

    }
}
