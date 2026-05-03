using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;

namespace Blink
{
    internal static class BlinkHooks
    {
        private static readonly Dictionary<FaceBlendShape, BlinkEffector> _dicBlend = [];

        private static Harmony _harmony;

        internal static void TryEnable(bool enable)
        {
            if (enable)
            {
                _harmony ??= Harmony.CreateAndPatchAll(typeof(BlinkHooks));
            }
            else if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
        }

        internal static BlinkEffector OnReload(ChaControl chara)
        {
            if (chara == null) throw new ArgumentNullException(nameof(chara));

            var fbsCtrl = chara.fbsCtrl;

            if (fbsCtrl == null)
            {
#if DEBUG
                BlinkPlugin.Logger.LogError($"Blink effect wasn't created due to the absence of {nameof(FaceBlendShape)} type on {chara.name}");
#endif
                return null;
            }

            if (_dicBlend.Count > 0)
            {
                var deadKeys = _dicBlend.Keys
                .Where(k => k == null)
                .ToList();

                foreach (var deadKey in deadKeys)
                    _dicBlend.Remove(deadKey);
            }

            if (_dicBlend.TryGetValue(fbsCtrl, out var effector))
            {
                return effector;
            }

            effector = new BlinkEffector();
            _dicBlend.Add(fbsCtrl, effector);
            return effector;
        }

        // When character reloads all? the components are created anew.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(FaceBlendShape), nameof(FaceBlendShape.Awake))]
        public static void FaceBlendShapeAwakePostfix(FaceBlendShape __instance)
        {
            if (_dicBlend.ContainsKey(__instance))
            {
                return;
            }

            var controller = new BlinkEffector();
            _dicBlend.Add(__instance, controller);
        }

        // Adjust the movement of eyebrows from default 1:1 to setting's ratio.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(FBSCtrlEyebrow), nameof(FBSCtrlEyebrow.CalcBlend))]
        public static void FBSCtrlEyebrowCalcBlend(ref float blinkRate)
        {
            if (blinkRate == -1f)
            {
                return;
            }
            blinkRate = 1f - (1f - blinkRate) * BlinkPlugin.EyebrowMovement.Value;
        }

        // Add call to method that changes openness rate.
        // Why not pre/postfix? It greatly simplifies tracking of instances and their association with the character,
        // as it can become very troublesome when the character is reloaded.
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(FaceBlendShape), nameof(FaceBlendShape.LateUpdate))]
        public static IEnumerable<CodeInstruction> FaceBlendShapeLateUpdateTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var done = false;

            foreach(var code in instructions)
            {
                yield return code;

                if (!done && code.opcode == OpCodes.Callvirt &&
                    code.operand.ToString().Contains(nameof(FBSBlinkControl.CalcBlink)))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(BlinkHooks), nameof(BlinkHooks.OnBlendShapeLateUpdate), [typeof(FaceBlendShape)]));

                    done = true;
                }
            }
        }

        // The method for the transpiler above.
        public static void OnBlendShapeLateUpdate(FaceBlendShape blendShape)
        {
            if (!_dicBlend.TryGetValue(blendShape, out var controller))
                return;

            blendShape.BlinkCtrl.openRate = controller.OnCalcBlink(blendShape.BlinkCtrl.openRate);
        }
    }
}
