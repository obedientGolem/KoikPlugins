using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using UnityEngine;
using static BirbExGauge.BirbExGaugePlugin;

namespace BirbExGauge
{
    internal static class HooksCommon
    {
        private static float _speedReciprocal = 1f;
        private static SmoothingType _maleSmoothing;
        private static SmoothingType _femaleSmoothing;
        private static float _maleFloor;
        private static float _femaleFloor;
        private static HFlag _hFlag;

        private static Harmony _patchCommon;
        private static Harmony _patchMale;
        private static Harmony _patchFemale;

        private static float GetSpeed
        {
            get
            {
                return _hFlag.mode switch
                {
                    HFlag.EMode.aibu => _hFlag.speed * _speedReciprocal,
                    HFlag.EMode.lesbian => _hFlag.speed,
                    _ => _hFlag.speedCalc * _speedReciprocal,
                };
            }
        }

        private static bool IsValidScene
        {
            get
            {
                if (_hFlag == null) return false;

                var configValue = EnabledScenes.Value;

                return _hFlag.mode switch
                {
                    HFlag.EMode.aibu => (configValue & SceneType.Caress) != 0,
                    HFlag.EMode.houshi or HFlag.EMode.houshi3P or HFlag.EMode.houshi3PMMF => (configValue & SceneType.Service) != 0,
                    HFlag.EMode.sonyu or HFlag.EMode.sonyu3P or HFlag.EMode.sonyu3PMMF => (configValue & SceneType.Intercourse) != 0,
                    HFlag.EMode.lesbian => (configValue & SceneType.Lesbian) != 0,
                    _ => false,
                };
            }
        }

        internal static float GetMaleGaugeAdjustment(float addPoint)
        {
#if DEBUG
            var prevAddPoint = addPoint;
#endif
            var speed = GetSpeed;
            var speedInfluence = _maleSmoothing switch
            {
                SmoothingType.Linear => Linear(_maleFloor, speed),
                SmoothingType.Cubic => Cubic(_maleFloor, speed),
                SmoothingType.CubicPlateau => CubicPlateau(_maleFloor, speed),
                //SmoothingType.Quint => Quint(_maleFloor, speed),
                //SmoothingType.Sine => Sine(_maleFloor, speed),
                _ => 1f
            };
#if DEBUG
            BirbExGaugePlugin.Logger.LogDebug($"MaleGaugeUp:prevValue[{prevAddPoint}] newValue[{addPoint * speedInfluence}] speed[{speed}] speedInfluence[{speedInfluence}]");
#endif
            return addPoint * speedInfluence;
        }

        internal static float GetFemaleGaugeAdjustment(float addPoint)
        {
#if DEBUG
            var prevAddPoint = addPoint;
#endif
            var speed = GetSpeed;
            var speedInfluence = _femaleSmoothing switch
            {
                SmoothingType.Linear => Linear(_femaleFloor, speed),
                SmoothingType.Cubic => Cubic(_femaleFloor, speed),
                SmoothingType.CubicPlateau => CubicPlateau(_femaleFloor, speed),
                //SmoothingType.Quint => Quint(_femaleFloor, speed),
                //SmoothingType.Sine => Sine(_femaleFloor, speed),
                _ => addPoint
            };
#if DEBUG
            BirbExGaugePlugin.Logger.LogDebug($"FemaleGaugeUp:prevValue[{prevAddPoint}] newValue[{addPoint * speedInfluence}] speed[{speed}] speedInfluence[{speedInfluence}]");
#endif
            return addPoint * speedInfluence;
        }

        internal static void TryEnable(HFlag hFlag = null)
        {
            if (_hFlag == null)
            {
                if (hFlag == null)
                {
                    _hFlag = UnityEngine.Object.FindObjectOfType<HFlag>();
                }
                else
                {
                    _hFlag = hFlag;
                }

            }

            if (_hFlag != null)
            {
                // Update reciprocal as the chance to update might have been skipped.
                if (_hFlag.speedMaxBody != 0f)
                {
                    _speedReciprocal = 1f / _hFlag.speedMaxBody;
                }
                else
                {
                    _speedReciprocal = 1f;
                }

                TryEnableMale();
                TryEnableFemale();
                UpdateConfig();
            }
            _patchCommon ??= Harmony.CreateAndPatchAll(typeof(HooksCommon));
        }

        private static void TryEnableMale()
        {
            if (EnableMale.Value != SmoothingType.Disable && IsValidScene)
            {
                _patchMale ??= Harmony.CreateAndPatchAll(typeof(HooksMale));
            }
            else if (_patchMale != null)
            {
                _patchMale.UnpatchSelf();
                _patchMale = null;
            }
        }

        private static void TryEnableFemale()
        {
            if (EnableFemale.Value != SmoothingType.Disable && IsValidScene)
            {
                _patchFemale ??= Harmony.CreateAndPatchAll(typeof(HooksFemale));
            }
            else if (_patchFemale != null)
            {
                _patchFemale.UnpatchSelf();
                _patchFemale = null;
            }
        }

        internal static void UpdateConfig()
        {
            _maleSmoothing = EnableMale.Value;
            _femaleSmoothing = EnableFemale.Value;
            _maleFloor = MaleFloor.Value;
            _femaleFloor = MaleFloor.Value;
        }

        private static float Linear(float floor, float t)
        {
            return floor + (1f - floor) * t;
        }

        private static float Cubic(float floor, float t)
        {
            return floor + (1f - floor) * (t * t * (3f - 2f * t));
        }

        private static float Quint(float floor, float t)
        {
            return floor + (1f - floor) * t * t * t * (t * (6f * t - 15f) + 10f);
        }

        private static float Sine(float floor, float t)
        {
            return floor + (1f - floor) * (0.5f * (1f - Mathf.Cos(t * Mathf.PI)));
        }
        private static float CubicPlateau(float floor, float t)
        {
            return floor + (1f - floor) * PlateauSmooth(t);

            static float PlateauSmooth(float t)
            {
                // edge = 0.2
                if (t < 0.2f)
                    // Mathf.SmoothStep(0f, 0.5f, t / edge);
                    return Mathf.SmoothStep(0f, 0.5f, t * 5f);

                if (t > 0.8f)
                    // Mathf.SmoothStep(0.5f, 1f, (t - (1f - edge)) / edge);
                    return Mathf.SmoothStep(0.5f, 1f, (t - 0.8f) * 5f);

                return 0.5f; // plateau
            }
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(HActionBase), nameof(HActionBase.SetPlay))]
        public static void SetPlayPostfix(HActionBase __instance)
        {
            if (__instance == null || __instance.flags == null) return;

            if (__instance.flags.speedMaxBody != 0f)
            {
                _speedReciprocal = 1f / __instance.flags.speedMaxBody;
            }
            else
            {
                _speedReciprocal = 1f;
            }
#if DEBUG
            BirbExGaugePlugin.Logger.LogDebug($"ChangeAnimatorPostfix:speedMaxBody[{__instance.flags.speedMaxBody}] _speedReciprocal[{_speedReciprocal}]");
#endif
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSceneProc), nameof(HSceneProc.ChangeAnimator))]
        public static void ChangeAnimatorPostfix(HSceneProc __instance)
        {
            TryEnable(__instance.flags);
        }



    }
}
