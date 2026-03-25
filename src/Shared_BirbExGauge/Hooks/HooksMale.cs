using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace BirbExGauge
{
    internal class HooksMale
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HFlag), nameof(HFlag.MaleGaugeUp))]
        public static void PrefixMaleGaugeUp(ref float _addPoint, HFlag __instance)
        {
            _addPoint = HooksCommon.GetMaleGaugeAdjustment(_addPoint);
        }
    }
}
