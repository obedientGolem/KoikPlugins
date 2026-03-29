using HarmonyLib;
using KKABMX.Core;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Kokyu
{
    internal class CaressPatch
    {
        private static Harmony _harmonyPatch;
        // Can be only by the main heroine, no need for extra checks.
        private static KokyuCharaController _customChaCtrl;

        public static void TryEnable(KokyuCharaController customChaCtrl)
        {
            if (customChaCtrl == null) throw new ArgumentNullException();

            _customChaCtrl = customChaCtrl;
            _harmonyPatch ??= Harmony.CreateAndPatchAll(typeof(CaressPatch));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HandCtrl), nameof(HandCtrl.SetItem))]
        public static void HandCtrlSetItemPostfix(HandCtrl __instance)
        {
            if (_customChaCtrl == null) return;
#if DEBUG
            KokyuPlugin.Logger.LogDebug($"HandCtrl.SetItem.Postfix: left[{__instance.useAreaItems[0] != null}] right[{__instance.useAreaItems[1] != null}]");
#endif
            var left = __instance.useAreaItems[0] != null;
            var right = __instance.useAreaItems[1] != null;

            _customChaCtrl.UpdateCaress(left, right);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HandCtrl), nameof(HandCtrl.DetachItem))]
        public static void DetachItemPostfix(HandCtrl __instance)
        {
            if (_customChaCtrl == null) return;

            var left = __instance.useAreaItems[0] != null;
            var right = __instance.useAreaItems[1] != null;

#if DEBUG
            KokyuPlugin.Logger.LogDebug($"HandCtrl.SetItem.Postfix: left[{left}] right[{right}]");
#endif
            _customChaCtrl.UpdateCaress(left, right);
        }
    }
}
