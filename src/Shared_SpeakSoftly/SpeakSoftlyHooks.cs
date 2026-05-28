using HarmonyLib;
using KK_SpeakSoftly;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace KK_SpeakSoftly
{
    internal class SpeakSoftlyHooks
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
            SpeakSoftlyCharaController.OnAudioStart(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HActionBase), nameof(HActionBase.SetPlay))]
        public static void HActionBase_SetPlayPostfix(string _nextAnimation, HActionBase __instance)
        {
            SpeakSoftlyCharaController.OnSetPlay(__instance.flags, _nextAnimation);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(FaceBlendShape), nameof(FaceBlendShape.SetVoiceVaule))]
        public static void FaceBlendShape_SetVoiceVaulePrefix(ref float value, FaceBlendShape __instance)
        {
            value *= SpeakSoftlyCharaController.OnSetVoiceValue(__instance);
        }

        #endregion
    }
}
