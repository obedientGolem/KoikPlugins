using HarmonyLib;
using Illusion.Component.Correct.Process;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AniMorph
{
    public class AniMorphBaseProcessHelper 
    {
        private readonly Dictionary<IKBeforeProcess, Action> _processDict = [];

        internal AniMorphBaseProcessHelper(ChaControl chara)
        {
            var processes = chara.GetComponentsInChildren<IKBeforeProcess>();

            var type = typeof(IKBeforeProcess);

            foreach (var process in processes)
            {
                var methodInfo = type
                    .GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                if (methodInfo == null)
                {
                    AniMorphPlugin.Logger.LogError($"[{process.data.bone.name}]: Failed to find LateUpdate method via reflection!");
                    return;
                }

                var dlg = (Action)Delegate.CreateDelegate(typeof(Action), process, methodInfo);

                _processDict.Add(process, dlg);
            }
        }

        internal void OnLateUpdate()
        {
            foreach (var dlg in _processDict.Values)
                dlg();
        }

        internal void OnAnimStateChange()
        {
            // Instead of Harmony shenanigans we keep it disabled and call when appropriate.
            foreach(var process in _processDict.Keys)
                process.enabled = false;            
        }


        #region Harmony


        //[HarmonyPrefix]
        //[HarmonyPatch(typeof(IKBeforeProcess), "LateUpdate")]
        //public static bool BaseProcessLateUpdate_Prefix(IKBeforeProcess __instance)
        //{
        //    var frame = Time.frameCount;

        //    // Second call this frame, run LateUpdate.
        //    if (_baseProcessDict[__instance] == frame) return true;

        //    // First call this frame, skip LateUpdate.
        //    _baseProcessDict[__instance] = frame;

        //    return false;
        //}

        //[HarmonyPostfix]
        //[HarmonyPatch(typeof(BaseProcess), MethodType.Constructor)]
        //public static void BaseProcessConstructor_Postfix(BaseProcess __instance)
        //{

        //    var methodInfo = typeof(BaseProcess)
        //        .GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        //    if (methodInfo == null)
        //    {
        //        AniMorphPlugin.Logger.LogError($"[{__instance.data.bone.name}]: Failed to find LateUpdate method via reflection!");
        //        return;
        //    }

        //    var dlg = (Action)Delegate.CreateDelegate(typeof(Action), __instance, methodInfo);

        //    _baseProcessDict.Add(__instance, 0);
        //    _delegateDict.Add(__instance, dlg);
        //}


        #endregion
    }
}

