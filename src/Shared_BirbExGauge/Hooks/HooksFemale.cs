using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace BirbExGauge
{
    internal class HooksFemale
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HFlag), nameof(HFlag.FemaleGaugeUp))]
        public static void PrefixFemaleGaugeUp(ref float _addPoint)
        {
            _addPoint = HooksCommon.GetFemaleGaugeAdjustment(_addPoint);
        }
    }
}
