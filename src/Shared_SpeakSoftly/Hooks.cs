using HarmonyLib;
using KK_SpeakSoftly;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace KK_SpeakSoftly
{
    internal class Hooks
    { 
        #region Harmony


        [HarmonyPostfix]
#if KK
        [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.SetVoiceTransform))]
#elif KKS
        [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.SetLipSync))]
#endif
        public static void SetVoiceTransformPostfix(ChaControl __instance)
        {
            foreach (var instance in SpeakSoftlyCharaController.Instances)
            {
                if (instance.ChaControl == __instance)
                {
                    instance.OnAudioStart();
                    break;
                }
            }

        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HActionBase), nameof(HActionBase.SetPlay))]
        public static void HActionBase_SetPlayPostfix(string _nextAnimation, HActionBase __instance)
        {
            SpeakSoftlyCharaController.OnSetPlay(__instance.flags, _nextAnimation);
        }


        #endregion
    }
}
