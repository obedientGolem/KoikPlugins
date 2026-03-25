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
        private static BoneController _controller;
        private static KokyuEffector _effector;

        public static void TryEnable(BoneController controller, KokyuEffector effector)
        {
            if (controller == null || effector == null) throw new ArgumentNullException();

            _effector = effector;
            _controller = controller;
            _harmonyPatch ??= Harmony.CreateAndPatchAll(typeof(CaressPatch));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HandCtrl), nameof(HandCtrl.SetItem))]
        public static void HandCtrlSetItemPostfix(HandCtrl __instance)
        {
#if DEBUG
            KokyuPlugin.Logger.LogDebug($"HandCtrl.SetItem.Postfix: left[{__instance.useAreaItems[0] != null}] right[{__instance.useAreaItems[1] != null}]");
#endif
            var controller = _controller;
            if (controller == null || _effector == null) return;

            var left = __instance.useAreaItems[0] != null;
            var right = __instance.useAreaItems[1] != null;

            if (left)
            {
                var mod = controller.GetModifier("cf_j_bust01_L", BoneLocation.BodyTop);

                if (mod != null)
                    controller.RemoveModifier(mod);
            }
            if (right)
            {
                var mod = controller.GetModifier("cf_j_bust01_R", BoneLocation.BodyTop);

                if (mod != null)
                    controller.RemoveModifier(mod);
            }
            _effector.UpdateCaress(controller, left, right);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HandCtrl), nameof(HandCtrl.DetachItem))]
        public static void DetachItemPostfix(HandCtrl __instance)
        {
            var left = __instance.useAreaItems[0] != null;
            var right = __instance.useAreaItems[1] != null;

#if DEBUG
            KokyuPlugin.Logger.LogDebug($"HandCtrl.SetItem.Postfix: left[{left}] right[{right}]");
#endif
            _effector?.UpdateCaress(_controller, left, right);
        }
    }
}
