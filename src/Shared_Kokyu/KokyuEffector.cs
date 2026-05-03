using KKABMX.Core;
using KKAPI.MainGame;
using RootMotion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;
using static Kokyu.KokyuCharaController;

namespace Kokyu
{
    internal class KokyuEffector : BoneEffect
    {
        private const int BreathSamples = 256;
        private const float Step = 1f / (BreathSamples - 1);

        private readonly string[] _bones;
        private readonly Dictionary<string, int> _bonesMap;
        // Big struct, ref access is preferable.
        private readonly Param[] _params;
        // Small struct, copying is optimal.
        private readonly AuxParam[] _auxParams;
        private readonly Vector3[] _mods;

        private readonly float[] _breathCurve = new float[BreathSamples];

        // Big struct, ref access is preferable.
        private Breath _breath;
        private bool _pregnant;

        // Temp placement until more of its kind accumulated for a dedicated struct.
        private Vector2 _vecFwdN4Bust;
        private bool _caressBustL;
        private bool _caressBustR;


        #region DicInit


        private static readonly Dictionary<Pattern, BreathPattern> _patternDic = new()
        {
            { 
                Pattern.Idle, new BreathPattern(
                    bpm: [8, 14],
                    rangeInhale: [0.3f, 0.4f],
                    rangePauseIn: [0.05f, 0.1f],
                    rangePauseOut: [0.1f, 0.15f],
                    //expansionRangeWeights: [new Vector3(0.15f, 0.35f, 0.35f), new Vector3(0.25f, 0.45f, 0.45f)],
                    expansionRangeWeights: [new Vector3(0.15f, 0.35f, (0.35f * 0.67f) + 0.35f), new Vector3(0.25f, 0.45f, (0.45f * 0.67f) + 0.45f)],
                    //expansionRangeFactor: [0.08f, 0.15f],
                    interpIn: [Interp.Smooth , Interp.Smoother],
                    interpOut: [Interp.Sine, Interp.Smooth, Interp.Smoother, Interp.Linear],
                    variability: Variability.PhysiologicalSigh
                    )
            },
            {
                Pattern.ActivityLight, new BreathPattern(
                    bpm: [14, 20],
                    rangeInhale: [0.35f, 0.4f],
                    rangePauseIn: [0.025f, 0.075f],
                    rangePauseOut: [0.025f, 0.075f],
                    //expansionRangeWeights: [new Vector3(0.25f, 0.4f, 0.25f), new Vector3(0.35f, 0.5f, 0.35f)],
                    expansionRangeWeights: [new Vector3(0.25f, 0.4f, (0.4f * 0.67f) + 0.25f), new Vector3(0.35f, 0.5f, (0.5f * 0.67f) + 0.35f)],
                    //expansionRangeFactor: [0.15f, 0.25f],
                    interpIn: [Interp.Sine, Interp.Smooth, Interp.Linear],
                    interpOut: [Interp.Smooth , Interp.Linear],
                    variability: Variability.None
                    )
            },
            {
                Pattern.ActivityModerate, new BreathPattern(
                    bpm: [20, 35],
                    rangeInhale: [0.4f, 0.45f],
                    rangePauseIn: [0f, 0.05f],
                    rangePauseOut: [0f, 0.05f],
                    //expansionRangeWeights: [new Vector3(0.35f, 0.35f, 0.2f), new Vector3(0.45f, 0.45f, 0.3f)],
                    expansionRangeWeights: [new Vector3(0.35f, 0.35f, (0.35f * 0.67f) + 0.2f), new Vector3(0.45f, 0.45f, (0.45f * 0.67f) + 0.3f)],
                    //expansionRangeFactor: [0.25f, 0.45f],
                    interpIn: [Interp.Sine, Interp.SinePlus, Interp.Smooth, Interp.Linear],
                    interpOut: [Interp.Smooth, Interp.Linear, Interp.Power],
                    variability: Variability.None
                    )
            },
            {
                Pattern.ActivityHeavy, new BreathPattern(
                    bpm: [35, 60],
                    rangeInhale: [0.45f, 0.5f],
                    rangePauseIn: [0f, 0.025f],
                    rangePauseOut: [0f, 0.025f],
                    //expansionRangeWeights: [new Vector3(0.45f, 0.3f, 0.05f), new Vector3(0.6f, 0.4f, 0.15f)],
                    expansionRangeWeights: [new Vector3(0.45f, 0.3f, (0.3f * 0.67f) + 0.05f), new Vector3(0.6f, 0.4f, (0.4f * 0.67f) + 0.15f)],
                    // Koik adjusted // human average
                    //expansionRangeFactor: [0.6f, 0.8f], // [0.45f, 0.7f],
                    interpIn: [Interp.SinePlus, Interp.Sine, Interp.Linear],
                    interpOut: [Interp.Smooth, Interp.Linear, Interp.Power],
                    variability: Variability.None
                    )
            },
            {
                Pattern.Anxiety, new BreathPattern(
                    bpm: [16, 26],
                    rangeInhale: [0.4f, 0.45f],
                    rangePauseIn: [0.025f, 0.075f],
                    rangePauseOut: [0.025f, 0.075f],
                    //expansionRangeWeights: [new Vector3(0.4f, 0.25f, 0.1f), new Vector3(0.6f, 0.35f, 0.25f)],
                    expansionRangeWeights: [new Vector3(0.4f, 0.25f, (0.25f * 0.67f) + 0.1f), new Vector3(0.6f, 0.35f, (0.35f * 0.67f) + 0.25f)],
                    //expansionRangeFactor: [0.125f, 0.25f],
                    interpIn: [Interp.Smooth, Interp.Sine, Interp.Linear, Interp.Power, Interp.Double],
                    interpOut: [Interp.Smooth,Interp.Linear, Interp.Power, Interp.Double],
                    variability: Variability.None
                    )
            },
            {
                Pattern.Panic, new BreathPattern(
                    bpm: [25, 50],
                    rangeInhale: [0.45f, 0.55f],
                    rangePauseIn: [0f, 0.025f],
                    rangePauseOut: [0f, 0.025f],
                    expansionRangeWeights: [new Vector3(0.6f, 0.15f, (0.15f * 0.67f) + 0.05f), new Vector3(0.75f, 0.25f, (0.25f * 0.67f) + 0.15f)],
                    //expansionRangeFactor: [0.1f, 0.3f],
                    interpIn: [Interp.SinePlus, Interp.Sine, Interp.Smooth, Interp.Linear],
                    interpOut: [ Interp.Smooth, Interp.Linear, Interp.Power, Interp.Double],
                    variability: Variability.None
                    )
            },
            {
                Pattern.Exhaustion, new BreathPattern(
                    bpm: [25, 45],
                    rangeInhale: [0.4f, 0.5f],
                    rangePauseIn: [0f, 0.025f],
                    rangePauseOut: [0f, 0.025f],
                    expansionRangeWeights: [new Vector3(0.4f, 0.3f, (0.3f * 0.67f) + 0.15f), new Vector3(0.55f, 0.4f, (0.4f * 0.67f) + 0.3f)],
                    // expansionRangeFactor: [0.35f, 0.65f],
                    interpIn: [Interp.Smooth, Interp.Linear, Interp.Power],
                    interpOut: [Interp.SinePlus, Interp.Sine, Interp.Smooth],
                    variability: Variability.None
                    )
            },
            {
                Pattern.Sleep, new BreathPattern(
                    bpm: [8, 12],
                    rangeInhale: [0.25f, 0.3f],
                    rangePauseIn: [0.05f, 0.1f],
                    rangePauseOut: [0.15f, 0.25f],
                    expansionRangeWeights: [new Vector3(0.1f, 0.35f, (0.35f * 0.67f) + 0.4f), new Vector3(0.2f, 0.45f, (0.45f * 0.67f) + 0.5f)],
                    //expansionRangeFactor: [0.06f, 0.12f],
                    interpIn: [Interp.Smooth, Interp.Smoother, Interp.Linear],
                    interpOut: [Interp.Sine, Interp.Smooth, Interp.Smoother],
                    variability: Variability.None
                    )
            },
            {
                Pattern.PhysiologicalSigh, new BreathPattern(
                    bpm: [6, 10],
                    rangeInhale: [0.35f, 0.45f],
                    rangePauseIn: [0.05f, 0.1f],
                    rangePauseOut: [0.1f, 0.15f],
                    expansionRangeWeights: [new Vector3(0.4f, 0.6f, (0.6f * 0.67f) + 0.6f), new Vector3(0.6f, 0.8f, (0.8f * 0.67f) + 0.8f)],
                    //expansionRangeFactor: [0.4f, 0.8f],
                    interpIn: [Interp.Smooth, Interp.Smoother],
                    interpOut: [Interp.SinePlus, Interp.Sine],
                    variability: Variability.None
                    )
            }
        };


        #endregion

        //// TODO optimize
        //private readonly Bone[] _bonesWithRotation = [Bone.Neck, Bone.Spine01, Bone.BustL, Bone.BustR];   //Bone.Bust00];

        // Dev
        private bool _devForceExhale;
        //private readonly Vector3[] _devCoefs;
        //private Vector3 _devNeckRot = new(1f, 1f, 1f);
        //private Vector3 _devSpineRot = new(-2f, 0.33f, 0.33f);


        #region Init


        internal KokyuEffector(ChaControl chara, BoneController boneController)
        {
            // Init arrays.

            var eValues = Enum.GetValues(typeof(Bone));
            var len = eValues.Length;

            _bones = new string[len];
            _bonesMap = new Dictionary<string, int>(StringComparer.Ordinal);
            _params = new Param[len];
            _auxParams = new AuxParam[Enum.GetValues(typeof(AuxBone)).Length];
            _mods = new Vector3[Enum.GetValues(typeof(Modifier)).Length];

            // Create data and add default values.

            foreach (Bone eVal in eValues)
            {
                var idx = (int)eVal;
                _params[idx].scaleMod = GetScaleModifier(eVal);
                _params[idx].modifierData = new();
            }

            // Find and add bones.

            var bones = chara.objBodyBone.GetComponentsInChildren<Transform>();

            foreach (Bone eVal in Enum.GetValues(typeof(Bone)))
            {
                var idx = (int)eVal;
                var name = GetBoneName(eVal);
                var bone = bones.Where(
                    t => t.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();

                if (bone == null)
                {
                    throw new NullReferenceException($"Couldn't find bone[{name}]");
                }

                _bones[idx] = name;
                _bonesMap[name] = idx;

                ref var param = ref _params[idx];

                //boneParam.localScale = bone.localScale;
                param.transform = bone;
                param.SaveScale();
            }

            // Find and add auxiliary bones.

            foreach (AuxBone eVal in Enum.GetValues(typeof(AuxBone)))
            {
                var name = GetAuxBoneName(eVal);
                var bone = bones.Where(
                    t => t.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();

                if (bone == null)
                {
                    throw new NullReferenceException($"Couldn't find bone[{name}]");
                }
                _auxParams[(int)eVal].transform = bone;
            }

            // TODO. Remove this.
            //_devCoefs = new Vector3[len];

            //for (var i = 0; i < len; i++)
            //{
            //    _devCoefs[i] = GetDevCoef((Bone)i);
            //}

            //static Vector3 GetDevCoef(Bone bone)
            //{
            //    return bone switch
            //    {
            //        // !!!!!!!!!!!!!!!!!!!!!!!!!!!
            //        // !!!! DEV. COEFFICIENTS !!!!
            //        // !!!!!!!!!!!!!!!!!!!!!!!!!!!
            //        Bone.RibUp => new(0f, 0.1f, 0.075f),
            //        Bone.RibLow => new(0f, 0.2f, 0.125f),
            //        Bone.AbsUp => new(0F, 0f, 0.08f),
            //        Bone.AbsLow => new(0f, 0f, 0.08f),
            //        Bone.ShldrL => new(0.125f, 1f, 1f),
            //        Bone.ShldrR => new(0.125f, 1f, 1f),
            //        _ => Vector3.zero,
            //        // !!!!!!!!!!!!!!!!!!!!!!!!!!!
            //        // !!!! DEV. COEFFICIENTS !!!!
            //        // !!!!!!!!!!!!!!!!!!!!!!!!!!!
            //    };
            //}
            static Vector3 GetScaleModifier(Bone bone)
            {
                return bone switch
                {
                    Bone.RibUp => new(0.08f, 0.08f, 0.08f),
                    Bone.RibLow => new(0.08f, 0.08f, 0.08f),
                    Bone.AbsUp => new(-0.05f, 0f, 0.08f),
                    Bone.AbsLow => new(0.05f, 0f, 0.08f),

                    //Bone.RibcageUpper => new(0.0125f, 0.075f, 0.025f),
                    //Bone.RibcageLower => new(0.03f, 0.125f, 0.03f),
                    //Bone.AbsUpper => new(-0.02f, 0f, -0.025f),
                    //Bone.AbsLower => new(0f, 0f /*0.05f*/, -0.03f),

                    _ => Vector3.zero,
                };
            }
            static string GetAuxBoneName(AuxBone bone)
            {
                return bone switch
                {
                    // The Bone.BreastLeft (cf_j_bust01_L) doesn't fit the bill as it placed too deep inside of the ribcage.
                    AuxBone.BustL02 => "cf_j_bust02_L",
                    AuxBone.BustR02 => "cf_j_bust02_R",
                    AuxBone.Neck => "cf_j_neck",
                    _ => throw new NotImplementedException()
                };
            }
        }

        internal void OnReload(KokyuCharaController customChaCtrl, BoneController boneController)
        {
            if (customChaCtrl == null || boneController == null) throw new ArgumentNullException();

            // Animated bones require special treatment by the ABMX.
            // Reset by the ABMX on reload and has to be set again.
            
            // 4 item collection isn't worth it.
            boneController.CollectBaselineOnUpdate(GetBoneName(Bone.Neck), BoneLocation.BodyTop, Baseline.Rotation);
            boneController.CollectBaselineOnUpdate(GetBoneName(Bone.Spine01), BoneLocation.BodyTop, Baseline.Rotation);
            boneController.CollectBaselineOnUpdate(GetBoneName(Bone.BustL), BoneLocation.BodyTop, Baseline.Rotation);
            boneController.CollectBaselineOnUpdate(GetBoneName(Bone.BustR), BoneLocation.BodyTop, Baseline.Rotation);

            foreach (Bone eVal in Enum.GetValues(typeof(Bone)))
            {
                var idx = (int)eVal;

                ref var param = ref _params[idx];
                param.SaveScale();
            }

            _pregnant = Helpers.GetPregnancyWeek(customChaCtrl.ChaControl) >= 15;

            OnRestart();

            UpdateNormalization();

            if (AnimTracker.IsHScene)
            {
                var mainHeroine = GameAPI.GetCurrentHeroine();

                if (mainHeroine != null && customChaCtrl.ChaControl == mainHeroine.chaCtrl)
                {
                    CaressPatch.TryEnable(customChaCtrl);
                }
            }
        }

        internal void OnRestart()
        {
            ref var breath = ref _breath;
            // Pauses Update() for 1 second.
            breath.time = -1f;
            breath.bpmCur = 15f;
            breath.len = 60f / 15f;
            breath.lenInv = 1f / (60f / 15f);

            // Reset vectors returned to the ABMX
            for (var i = 0; i < _mods.Length; i++)
            {
                if (i == (int)Modifier.RibUpScl ||
                    i == (int)Modifier.RibLowScl ||
                    i == (int)Modifier.AbsUpScl ||
                    i == (int)Modifier.AbsLowScl)
                {
                    _mods[i] = Vector3.one;
                }
                else
                {
                    _mods[i] = Vector3.zero;
                }
            }
            
            for (var i = 0; i < _params.Length; i++)
            {
                ref var param = ref _params[i];

                param.modifierData.Clear();
            }
        }

        private string GetBoneName(Bone bone)
        {
            return bone switch
            {
                Bone.Neck => "cf_j_neck",
                Bone.RibUp => "cf_s_spine03",
                Bone.RibLow => "cf_s_spine02",
                Bone.ShldrL => "cf_j_shoulder_L",
                Bone.ShldrR => "cf_j_shoulder_R",
                Bone.BustL => "cf_j_bust01_L",
                Bone.BustR => "cf_j_bust01_R",
                Bone.AbsUp => "cf_s_spine01",
                Bone.AbsLow => "cf_s_waist01",
                Bone.Spine01 => "cf_j_spine01",
                _ => throw new NotImplementedException(bone.ToString())
            };
        }


        #endregion


        #region Overrides


        public override IEnumerable<string> GetAffectedBones(BoneController origin) => _bones;

        public override BoneModifierData GetEffect(string bone, BoneController origin, ChaFileDefine.CoordinateType coordinate)
        {
            var idx = _bonesMap[bone];
            var mod = _mods;

            return (Bone)idx switch
            {
                Bone.Neck => UpdateBoneData(idx, Vector3.one, mod[(int)Modifier.NeckPos], mod[(int)Modifier.NeckRot]),
                Bone.RibUp => UpdateBoneData(idx, mod[(int)Modifier.RibUpScl], mod[(int)Modifier.RibUpPos], Vector3.zero),
                Bone.RibLow => UpdateBoneData(idx, mod[(int)Modifier.RibLowScl], mod[(int)Modifier.RibLowPos], Vector3.zero),
                Bone.ShldrL => UpdateBoneData(idx, Vector3.one, mod[(int)Modifier.ShldrLPos], Vector3.zero),
                Bone.ShldrR => UpdateBoneData(idx, Vector3.one, mod[(int)Modifier.ShldrRPos], Vector3.zero),
                Bone.BustL => _caressBustL ? null : UpdateBoneData(idx, Vector3.one, mod[(int)Modifier.BustLPos], mod[(int)Modifier.BustLRot]),
                Bone.BustR => _caressBustR ? null : UpdateBoneData(idx, Vector3.one, mod[(int)Modifier.BustRPos], mod[(int)Modifier.BustRRot]),
                Bone.AbsUp => UpdateBoneData(idx, mod[(int)Modifier.AbsUpScl], mod[(int)Modifier.AbsUpPos], Vector3.zero),
                Bone.AbsLow => UpdateBoneData(idx, mod[(int)Modifier.AbsLowScl], mod[(int)Modifier.AbsLowPos], Vector3.zero),
                Bone.Spine01 => UpdateBoneData(idx, Vector3.one, Vector3.zero, mod[(int)Modifier.SpineRot]),
                _ => throw new NotImplementedException()
            };

            ref BoneModifierData UpdateBoneData(int idx, Vector3 scale, Vector3 pos, Vector3 rot)
            {
                ref var data = ref _params[idx].modifierData;

                data.ScaleModifier = scale;
                data.PositionModifier = pos;
                data.RotationModifier = rot;

                return ref data;
            }
        }


        #endregion


        #region UpdateGameCycle


        internal void OnUpdate()
        {
            var deltaTime = Time.deltaTime;

            if (deltaTime == 0f) return;

            // Struct. Big.
            ref var breath = ref _breath;
            breath.time += deltaTime;

            // Wait for warmup, happens rarely, struct ref is almost never an overhead.
            if (breath.time < 0f) return;

            // (0..~4)
            if (breath.time >= breath.len)
                breath.time -= breath.len;

            UpdateBPM();

            // First frame of the breath cycle.
            if (breath.time <= deltaTime)
            {
                PrepareCurve(ref breath);
                PrepareCycle(ref breath);
            }

            var tNorm = breath.time * breath.lenInv;

            var f = tNorm * (BreathSamples - 1);
            var idx = (int)f;
            var frac = f - idx;

            // Without Mathf.Min() it's possible to get OutOfRange exception on very high fps,
            // not really relevant to koik, though.
            var cyclePos = Mathf.Lerp(_breathCurve[idx], _breathCurve[Mathf.Min(idx + 1, BreathSamples - 1)], frac);

            //KokyuPlugin.Logger.LogDebug($"Update: t[{tNorm:F2}] pos[{cyclePos:F2}] f[{f:F2}] idx[{idx}] frac[{frac:F2}])");

            ref var ribUpParam = ref _params[(int)Bone.RibUp];
            ref var ribLowParam = ref _params[(int)Bone.RibLow];
            ref var absUp = ref _params[(int)Bone.AbsUp];
            ref var absLow = ref _params[(int)Bone.AbsLow];

            var scale = cyclePos * breath.magnitude;

            var ribUpScale = ribUpParam.randScl * scale;
            var ribLowScale = ribLowParam.randScl * scale;
            var absUpperScale = absUp.randScl * scale;
            var absLowerScale = absLow.randScl * scale;
            
            var ribUpPos = Vector3.Scale(ribUpScale, new(0f, 0.1f, 0.075f));
            var ribLowPos = Vector3.Scale(ribLowScale, new(0f, 0.2f, 0.125f));
            var absUpPos = Vector3.Scale(absUpperScale, new(0f, 0f, 0.08f));
            var absLowPos = Vector3.Scale(absLowerScale, new(0f, 0f, 0.08f));

            // Find out how far (7..9) ribs moved relative to the default position,
            // and apply it as ~navel offset.
            absLowPos.y += Vector2.Distance(new Vector2(ribLowPos.y, ribLowPos.z), Vector2.zero);

            // Don't move sideways.
            ribUpPos.x = 0f;
            ribLowPos.x = 0f;
            absUpPos.x = 0f;
            absLowPos.x = 0f;

            var neckPos = ribUpPos * 0.67f;

            // BREASTS
            // Move breast forward and sideways.
            var bustLPos = ribUpPos + ribLowPos;
            bustLPos.x += bustLPos.z * 0.33f;

            var bustRPos = bustLPos;
            bustRPos.x *= -1f;

            ref var bustLParam = ref _params[(int)Bone.BustL];
            ref var bustRParam = ref _params[(int)Bone.BustR];

            bustLPos *= bustLParam.randFactor;
            bustRPos *= bustRParam.randFactor;

            //breastLeftPos.x *= paramBreastLeft.randFactor;
            //breastLeftPos.y *= paramBreastLeft.randFactor;
            //breastRightPos.x *= paramBreastRight.randFactor;
            //breastRightPos.y *= paramBreastRight.randFactor;


            var neckAuxParam = _auxParams[(int)AuxBone.Neck];
            var bust02LAuxParam = _auxParams[(int)AuxBone.BustL02];
            var bust02RAuxParam = _auxParams[(int)AuxBone.BustR02];

            var neckAuxPos = neckAuxParam.startPos + new Vector2(ribUpPos.z, ribUpPos.y);
            var bust02LPos = bust02LAuxParam.startPos + new Vector2(bustLPos.z, bustLPos.y);
            var bust02RPos = bust02RAuxParam.startPos + new Vector2(bustRPos.z, bustRPos.y);

            var bustLPitch = bust02LAuxParam.startAngle -
                (Mathf.Acos(Vector2.Dot(_vecFwdN4Bust, (neckAuxPos - bust02LPos).normalized)) * Mathf.Rad2Deg);

            var BustRPitch = bust02RAuxParam.startAngle -
                (Mathf.Acos(Vector2.Dot(_vecFwdN4Bust, (neckAuxPos - bust02RPos).normalized)) * Mathf.Rad2Deg);

            // SHOULDERS

            var shoulderPos = (ribUpScale.x * 0.5f);// + (ribcageLowerScale.x * (0.5f * 0.25f));

            var shoulderLPos = ribUpPos;
            shoulderLPos.x += shoulderPos * (-1f * 0.125f);

            var shoulderRPos = ribUpPos;
            shoulderRPos.x += shoulderPos * 0.125f;

            ref var shoulderLParam = ref _params[(int)Bone.ShldrL];
            ref var shoulderRParam = ref _params[(int)Bone.ShldrR];
            shoulderLPos *= shoulderLParam.randFactor;
            shoulderRPos *= shoulderRParam.randFactor;
            //KokyuPlugin.Logger.LogDebug($"Update: tNorm[{tNorm:F3}] pos[{cyclePos:F3}] bustLeftPitch[{breastLeftPitch:F3}] bustRightPitch[{breastRightPitch:F3}])");

            var mod = _mods;

            mod[(int)Modifier.RibUpScl] = ribUpScale + Vector3.one;
            mod[(int)Modifier.RibUpPos] = ribUpPos;
            mod[(int)Modifier.RibLowScl] = ribLowScale + Vector3.one;
            mod[(int)Modifier.RibLowPos] = ribLowPos;

            mod[(int)Modifier.ShldrLPos] = shoulderLPos;
            mod[(int)Modifier.ShldrRPos] = shoulderRPos;

            mod[(int)Modifier.BustLPos] = bustLPos;
            mod[(int)Modifier.BustLRot] = new Vector3(bustLPitch, 0f, 0f);
            mod[(int)Modifier.BustRPos] = bustRPos;
            mod[(int)Modifier.BustRRot] = new Vector3(BustRPitch, 0f, 0f);

            mod[(int)Modifier.AbsUpScl] = absUpperScale + Vector3.one;
            mod[(int)Modifier.AbsUpPos] = absUpPos;
            mod[(int)Modifier.AbsLowScl] = absLowerScale + Vector3.one;
            mod[(int)Modifier.AbsLowPos] = absLowPos;

            mod[(int)Modifier.NeckPos] = neckPos;
            mod[(int)Modifier.NeckRot] = breath.neckRot * cyclePos;
            mod[(int)Modifier.SpineRot] = breath.spineRot * cyclePos;
        }

        private void PrepareCycle(ref Breath breath)
        {
            // Can't access in old C# dic value with ref.
            var pattern = _patternDic[breath.pattern];

            breath.interpIn = breath.interpInNext;
            breath.interpInNext = pattern.interpIn[Random.Range(0, pattern.interpIn.Length)];
            breath.interpOut = pattern.interpOut[Random.Range(0, pattern.interpOut.Length)];

            // Fluctuate BPM
            if (Random.value > 0.67f)
            {
                breath.bpmTar = Mathf.MoveTowards(breath.bpmCur, GetRandomFromRange(pattern.bpm), breath.bpmCur * 0.1f);
            }

            for (var i = 0; i < _params.Length; i++)
            {
                // Reference struct. It's big.
                ref var param = ref _params[i];
                var randScaleModifier = Vector3.Scale(param.scaleMod, param.lossyScl);

                switch (i)
                {
                    case (int)Bone.RibUp:
                        randScaleModifier *=
                            Random.Range(pattern.expWeights[0].x, pattern.expWeights[1].x);
                        break;
                    case (int)Bone.RibLow:
                        randScaleModifier *=
                            Random.Range(pattern.expWeights[0].y, pattern.expWeights[1].y);
                        break;
                    case (int)Bone.AbsUp:
                    case (int)Bone.AbsLow:
                        randScaleModifier *=
                            Random.Range(pattern.expWeights[0].z, pattern.expWeights[1].z);
                        break;
                }
                param.randScl = randScaleModifier;
                param.randFactor = Random.Range(0.75f, 1.25f);
            }

            /*
             * We look for approximate direction and an angle of sternum,
             * so that later we can adjust breast pitch based on adjusted orientation of sternum.
             * 
             * Store positions of neck and breasts as Vec2(z, y) 
             * as if we are looking at it in 2D space form the side.
             * 
             * Find directions between (breast -> neck), find an angle 
             * between those directions and some static vec, store it.
             * 
             * On next frames we'll add adjustments to those values, figure out delta and apply it.
             * 
             * Technically we do it this frame too, because less confusion with local variables.
             */
            // Copy structs. They are small.
            var neckAuxParam = _auxParams[(int)AuxBone.Neck];
            var bust02LParam = _auxParams[(int)AuxBone.BustL02];
            var bust02RParam = _auxParams[(int)AuxBone.BustR02];
            // Store positions as Vec2.
            neckAuxParam.SavePos();
            bust02LParam.SavePos();
            bust02RParam.SavePos();
            // Find directions of ~sternum.
            var bust02LDir = neckAuxParam.startPos - bust02LParam.startPos;
            var bust02RDir = neckAuxParam.startPos - bust02RParam.startPos;
            // Arbitrary vec in ~right direction, as we can't have angle close to 180 degrees.
            var vecFwd = neckAuxParam.transform.forward;
            var vecFwdN = new Vector2(vecFwd.z, vecFwd.y).normalized;

            bust02LParam.startAngle =
                Mathf.Acos(Vector2.Dot(vecFwdN, bust02LDir.normalized)) * Mathf.Rad2Deg;
            bust02RParam.startAngle =
                Mathf.Acos(Vector2.Dot(vecFwdN, bust02RDir.normalized)) * Mathf.Rad2Deg;

            _vecFwdN4Bust = vecFwdN;
            // Write structs. They are small.
            _auxParams[(int)AuxBone.Neck] = neckAuxParam;
            _auxParams[(int)AuxBone.BustL02] = bust02LParam;
            _auxParams[(int)AuxBone.BustR02] = bust02RParam;

            var min = pattern.expWeights[0];
            var max = pattern.expWeights[1];

            // Randomize rotations
            // Multiply pitch by expansion factor of lowerRibcage + abdomen;
            var expInfRibUp = breath.rotation *
                Random.Range(min.x, max.x);
            var expInfRibLow = breath.rotation *
                Random.Range(min.y, max.y);
            var expInfAbs = breath.rotation *
                Random.Range(min.z, max.z);

            // By default expansionInfluence of abdomen (hidden behind Z axis) has added in
            // expansionInfluence of lowerRibcage (hidden behind Y axis), because when 7-10 ribs move, abdomen moves too automatically.
            // Here we separate them to combine properly.  
            var expInfTotal =
                expInfRibUp +
                expInfRibLow +
                (expInfAbs - expInfRibLow);

            breath.spineRot = new Vector3(
                expInfRibLow + (expInfAbs - expInfRibLow) * Random.Range(0.5f, 1f),
                expInfTotal * Random.Range(-1f, 1f),
                expInfTotal * Random.Range(-1f, 1f));
            breath.spineRot.Scale(new(-2f, 0.33f, 0.33f));

            breath.neckRot = new(1f, 1f, 1f);
            breath.neckRot = new Vector3(
                // Compensate pitch with ± 10% deviation and add random.
                -breath.spineRot.x * Random.Range(0.9f, 1.1f) + (breath.neckRot.x * (Random.Range(-1f, 1f) * expInfTotal)),
                // Compensate yaw with ± 10% deviation and add random.
                -breath.spineRot.y * Random.Range(0.9f, 1.1f) + (breath.neckRot.y * (Random.Range(-1f, 1f) * expInfTotal)),
                 breath.neckRot.z * (Random.Range(-1f, 1f) * expInfTotal)
                );

//#if DEBUG
//            KokyuPlugin.Logger.LogDebug($"Update: NewCycle ribcageUpper({_params[(int)Bone.RibUp].randScl.x:F2},{_params[(int)Bone.RibUp].randScl.y:F2},{_params[(int)Bone.RibUp].randScl.z:F2}) " +
//                $"ribcageLower({_params[(int)Bone.RibLow].randScl.x:F2},{_params[(int)Bone.RibLow].randScl.y:F2},{_params[(int)Bone.RibLow].randScl.z:F2}) " +
//                $"absUpper({_params[(int)Bone.AbsUp].randScl.x:F2},{_params[(int)Bone.AbsUp].randScl.y:F2},{_params[(int)Bone.AbsUp].randScl.z:F2}) " +
//                $"absLower({_params[(int)Bone.AbsLow].randScl.x:F2},{_params[(int)Bone.AbsLow].randScl.y:F2},{_params[(int)Bone.AbsLow].randScl.z:F2}) " +
//                $"interpIn[{breath.interpIn}] interpOut[{breath.interpOut}]");
//#endif
        }

        private void PrepareCurve(ref Breath breath)
        {
            // Cache struct fields

            var inEnd = breath.inEnd;
            var inLen = breath.inLen;
            var outEnd = breath.outEnd;
            var interpIn = breath.interpIn;
            var interpOut = breath.interpOut;

            var inhaleP1 = GetP1(breath.interpIn);
            var inhaleP2 = GetP2(breath.interpIn);

            var exhaleP1 = GetP1(breath.interpOut);
            var exhaleP2 = GetP2(breath.interpOut);

            // I would really love to leave a comment wtf I do here,
            // but given how math/graph heavy all this is,
            // descriptions are pretty much pointless,
            // it has to be seen to be understood,
            // so grab math and go to desmos.com/calculator or something.
            var sharpDome = IsSharpDome(interpIn) && IsSharpDome(interpOut);
            var sharpCraterL = IsSharpCrater(interpOut);
            var shardCraterR = IsSharpCrater(breath.interpInNext);

            static bool IsSharpDome(Interp i) => (i == Interp.Power || i == Interp.Linear || i == Interp.Double);
            static bool IsSharpCrater(Interp i) => (i == Interp.SinePlus || i == Interp.Sine || i == Interp.Linear || i == Interp.Double);
            //var interpInConcave =
            //    interpIn == Interp.Power ||
            //    interpIn == Interp.Linear ||
            //    interpIn == Interp.Double;

            //var interpOutConcave =
            //    interpIn == Interp.Power ||
            //    interpIn == Interp.Linear ||
            //    interpIn == Interp.Double;

            //var domeP1 = 0f;
            //var domeP2 = 0f;
            //var domeHeightFactor = 0f;

            //if (interpInConcave && interpOutConcave)
            //{
            //    // P1(0.5) P2(0.5) – peak(0.5, 0.375)
            //    domeP1 = 0.25f + Random.value * 0.25f;
            //    domeP2 = domeP1;
            //    domeHeightFactor = (0.1125f / 0.375f);
            //}
            //else if (interpInConcave)// && !interpOutConcave)
            //{
            //    // P1(0.5) P2(0) – peak(0.34, 0.22)
            //    domeP1 = 0.25f + Random.value * 0.25f;
            //    domeP2 = 0f;
            //    domeHeightFactor = (0.1125f / 0.22f);
            //}
            //else if (interpOutConcave)// && !interpInConcave)
            //{
            //    // P1(0) P2(0.5) – peak(1 - 0.34, 0.22)
            //    domeP1 = 0f;
            //    domeP2 = 0.25f + Random.value * 0.25f;
            //    domeHeightFactor = (0.1125f / 0.22f);
            //}
            //else// if (!interpInConcave && !interpOutConcave)
            //{
            //    // P1(0.15) P2(0.15) – peak(0.5, 0.1125)
            //    domeP1 = 0.05f + Random.value * 0.1f;
            //    domeP2 = 0.05f + Random.value * 0.1f;
            //    domeHeightFactor = 1f;
            //}

            // P1(-0.15) P2(-0.15) – peak(0.5, -0.1125)
            //var craterP1 = -0.05f - Random.value * 0.1f;
            //var craterP2 = -0.05f - Random.value * 0.1f;
            //var craterHeightFactor = 1f;

            var domeLen = inLen - inEnd;
            var craterLen = 1f - outEnd;

            // default * (length / 0.1) 0.1 being highest full lungs pause.
            var domeHeightFactor = 0.1f * (domeLen * (1f / 0.1f));
            // default * (length / 0.15) 0.15 being highest non-sleep empty lungs pause.
            // * 0.5 because it looks too much otherwise
            var craterHeightFactor = 0.1f * (craterLen * (1f / 0.15f));

            // Prepare reciprocals

            var inhaleEndInv = 1f / inEnd;
            var domeLenInv = 1f / domeLen;
            var exhaleEndInv = 1f / (outEnd - inLen);
            var craterLenInv = 1f / craterLen;


            // Prepare indexes

            var inhaleEndIdx = (int)(inEnd * (BreathSamples - 1));
            var domeEndIdx = (int)(inLen * (BreathSamples - 1));
            var exhaleEndIdx = (int)(outEnd * (BreathSamples - 1));
            var craterHalfIdx = (int)((1f - outEnd) * 0.5f * (BreathSamples - 1));

            var t = 0f;
            var i = 0;

            // INHALE
            for (; i <= inhaleEndIdx; i++, t += Step)
            {
                var x = t * inhaleEndInv;
                _breathCurve[i] = ApplyInterpolation(x, interpIn, inhaleP1, inhaleP2);
            }

            // DOME
            for (; i <= domeEndIdx; i++, t += Step)
            {
                if (sharpDome)
                {
                    var x = (t - inEnd) * domeLenInv;
                    _breathCurve[i] = 1f + BumpParabola(x) * domeHeightFactor;
                    //_breathCurve[i] = 1f + ZeroToZeroBezier(x, domeP1, domeP2) * domeHeightFactor;
                }
                else
                {
                    _breathCurve[i] = 1f;
                }
            }

            // EXHALE
            for (; i <= exhaleEndIdx; i++, t += Step)
            {
                var x = 1f - ((t - inLen) * exhaleEndInv);
                _breathCurve[i] = ApplyInterpolation(x, interpOut, exhaleP1, exhaleP2);
            }

            // CRATER first half
            for (; i <= craterHalfIdx; i++, t += Step)
            {
                var x = (t - outEnd) * craterLenInv;
                if (sharpCraterL)
                {
                    _breathCurve[i] = -BumpParabola(x) * craterHeightFactor;
                }
                else
                {
                    _breathCurve[i] = -BumpSmoothStep(x) * craterHeightFactor;
                }
                //_breathCurve[i] = BumpBezier(x, craterP1, craterP2) * craterHeightFactor;
            }
            // CRATER second half
            for (; i < BreathSamples; i++, t += Step)
            {
                var x = (t - outEnd) * craterLenInv;
                if (shardCraterR)
                {
                    _breathCurve[i] = -BumpParabola(x) * craterHeightFactor;
                }
                else
                {
                    _breathCurve[i] = -BumpSmoothStep(x) * craterHeightFactor;
                }
                //_breathCurve[i] = BumpBezier(x, craterP1, craterP2) * craterHeightFactor;
            }

//#if DEBUG
//            KokyuPlugin.Logger.LogDebug($"Curve: dome[{domeHeightFactor:F3}] crater[{craterHeightFactor:F3}] Max[{Mathf.Max(_breathCurve):F3}] Min[{Mathf.Min(_breathCurve):F3}]");
//#endif
            // LOCAL FUNCTIONS
            static float ApplyInterpolation(float t, Interp interp, float p1, float p2)
            {
                return interp switch
                {
                    Interp.Smooth => t * t * (3f - 2f * t),
                    Interp.Smoother => t * t * t * (10f - 15f * t + 6f * t * t),
                    Interp.Linear => t,
                    _ => Bezier(t, p1, p2),
                };
            }
            static float GetP1(Interp interp)
            {
                return interp switch
                {
                    Interp.SinePlus => 0.5f + Random.value * 0.25f,
                    Interp.Sine => 0.25f + Random.value * 0.25f,
                    Interp.Double => 0.25f + Random.value * 0.5f,
                    _ => 0f
                };
            }
            static float GetP2(Interp interp)
            {
                return interp switch
                {
                    Interp.SinePlus or Interp.Sine => 1f,
                    Interp.Power => Random.value * 0.5f,
                    _ => 0f
                };
            }
            static float Bezier(float t, float p1, float p2)
            {
                var u = 1 - t;

                return 3 * u * u * t * p1 +
                       3 * u * t * t * p2 +
                       t * t * t;
            }

            static float BumpBezier(float t, float p1, float p2)
            {
                var u = 1 - t;

                return 3 * u * u * t * p1 +
                       3 * u * t * t * p2;
            }
            static float BumpSmoothStep(float t) => 4f * t * t * (3f - 2f * t) * (1f - t * t * (3f - 2f * t));
            static float BumpParabola(float t) => 4f * t * (1f - t);
        }

        private void UpdateNormalization()
        {
            ref var breath = ref _breath;
            var pattern = _patternDic[breath.pattern];

            //_bpmFloor = pattern.bpm[0];
            //_bpmCeiling = pattern.bpm[1];
            breath.bpmTar = breath.speed * GetRandomFromRange(pattern.bpm);

            breath.inEnd = GetRandomFromRange(pattern.rangeInhale);
            breath.inLen = breath.inEnd + GetRandomFromRange(pattern.rangePauseIn);
            breath.outEnd = 1f - GetRandomFromRange(pattern.rangePauseOut);

        }

        private float GetRandomFromRange(float[] range) => range[0] + (Random.value * (range[1] - range[0]));

        private void UpdateBPM()
        {
            // Big struct, ref access.
            ref var breath = ref _breath;

            if (breath.bpmCur == breath.bpmTar) return;

            // Aim to finish transition in 3 seconds.
            var step = _breath.bpmTar * 0.33f * Time.deltaTime;
            breath.bpmCur = Mathf.MoveTowards(breath.bpmCur, breath.bpmTar, step);

            var cyclePos = breath.time * breath.lenInv;

            var breathLength = 60f / breath.bpmCur;

            // Adjust time based on the new breathLength while maintaining the same cycle position.
            breath.time = cyclePos * breathLength;
            breath.len = breathLength;
            breath.lenInv = 1f / breathLength;
        }


        #endregion


        #region UpdateOnDemand


        internal void OnUpdateCaress(BoneController controller, bool bustL, bool bustR)
        {
            // If caress just stopped
            if (_caressBustL && !bustL)
            {
                // Add back modifier with rotation setting.
                controller.CollectBaselineOnUpdate(GetBoneName(Bone.BustL), BoneLocation.BodyTop, Baseline.Rotation);
            }
            // If caress just stopped
            if (_caressBustR && !bustR)
            {
                controller.CollectBaselineOnUpdate(GetBoneName(Bone.BustR), BoneLocation.BodyTop, Baseline.Rotation);
            }
            _caressBustL = bustL;
            _caressBustR = bustR;
        }

        internal void UpdateBreathExData(float magnitudeFactor, float speedFactor, float rotationFactor)
        {
            ref var breath = ref _breath;

            var oldSpeed = breath.speed;

            breath.magnitude = magnitudeFactor * KokyuPlugin.SettingGlobalMagnitude.Value;
            breath.speed = speedFactor * KokyuPlugin.SettingGlobalSpeed.Value;
            breath.rotation = rotationFactor * KokyuPlugin.SettingGlobalRotation.Value;

            if (oldSpeed != breath.speed)
            {
                UpdateNormalization();
            }
        }

        internal void UpdatePattern(Pattern pattern)
        {
            ref var breath = ref _breath;

            breath.pattern = pattern;

            UpdateNormalization();
        }


        #endregion


        #region Enums


        private enum Bone
        {
            RibUp,
            RibLow,
            AbsUp,
            AbsLow,
            BustL,
            BustR,
            ShldrL,
            ShldrR,
            Neck,
            Spine01,
        }

        // Bones that we track but don't modify.
        private enum AuxBone
        {
            Neck,
            BustL02,
            BustR02,
        }


        internal enum Modifier
        {
            RibUpScl,
            RibUpPos,
            RibLowScl,
            RibLowPos,

            ShldrLPos,
            ShldrRPos,

            BustLPos,
            BustLRot,
            BustRPos,
            BustRRot,

            AbsUpScl,
            AbsUpPos,
            AbsLowScl,
            AbsLowPos,

            NeckPos,
            NeckRot,

            SpineRot
        }

        // Ordered by the speed of gain.
        private enum Interp
        {
            // Bezier with P1(0.5..0.75) P2(1)
            // Extra steep sine.
            SinePlus,
            // Bezier with P1(0.25..0.5) P2(1)
            // Should be much cheaper then Math.Sin()
            // Comes with plenty of variability that OG sine lacks.
            Sine,
            // SmoothStep
            Smooth,
            // SmootherStep
            Smoother,
            // Plain x = y
            Linear,
            // Bezier with P1(0) P2(0..0.75)
            // Because OG power functions are too rigid in variability.
            Power,
            // Bezier with P1(0.25..0.75) P2(0)
            // Not necessarily a double, can be just an uneven weird one.
            Double,
        }

        [Flags]
        private enum Variability
        {
            None = 0,
            // Happens every few minutes (3 - 10 from research but it's too long for our purpose)
            // Has pause (warm up) phase of (2..10) seconds.
            PhysiologicalSigh = 1 << 0,
        }


        #endregion


        #region Wrappers


        ///// <summary>
        ///// Only sequential int Enums.
        ///// </summary>
        //class EnumArray<TEnum, TValue> : IEnumerable<TValue>
        //    where TEnum : Enum
        //{
        //    private static readonly TEnum[] keys = Enum.GetValues(typeof(TEnum)) as TEnum[];
        //    private readonly TValue[] data = new TValue[keys.Length];

        //    public ref TValue this[TEnum index]
        //    {
        //        get => ref data[Unsafe.As<TEnum, int>(ref index)];
        //    }
        //    public int Length => data.Length;
        //    public IEnumerator<TValue> GetEnumerator()
        //        => ((IEnumerable<TValue>)data).GetEnumerator();

        //    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        //    public IEnumerable<(TEnum Key, TValue Value)> Pairs()
        //    {
        //        foreach (var key in keys)
        //            yield return (key, this[key]);
        //    }
        //}


        #endregion


        #region Structs


        private struct Param
        {
            internal Transform transform;
            internal BoneModifierData modifierData;
            internal Vector3 scaleMod;
            internal Vector3 lossyScl;
            //internal Vector3 localScale;
            internal Vector3 randScl;
            internal float randFactor;

            internal void SaveScale() => lossyScl = transform.localScale;
        }

        private struct AuxParam
        {
            internal Transform transform;
            internal Vector2 startPos;
            internal float startAngle;
            internal void SavePos()
            {
                var position = transform.position;
                startPos = new Vector2(position.z, position.y);
            }
        }

        private struct Breath
        {
            internal float time;
            internal float magnitude;
            internal float speed;
            internal float rotation;
            internal Pattern pattern;

            internal float inLen;
            internal float inEnd;
            internal float outEnd;

            internal float bpmCur;
            internal float bpmTar;
            internal float len;
            internal float lenInv;
            internal Interp interpIn;
            internal Interp interpOut;
            // Interpolation prepared for the next cycle.
            internal Interp interpInNext;

            internal Vector3 neckRot;
            internal Vector3 spineRot;
        }

        private readonly struct BreathPattern(
            float[] bpm,
            float[] rangeInhale,
            float[] rangePauseIn,
            float[] rangePauseOut,
            Vector3[] expansionRangeWeights,
            Interp[] interpIn,
            Interp[] interpOut,
            Variability variability
            )
        {
            internal readonly float[] bpm = bpm;
            internal readonly float[] rangeInhale = rangeInhale;
            internal readonly float[] rangePauseIn = rangePauseIn;
            internal readonly float[] rangePauseOut = rangePauseOut;
            /// <summary>
            /// Vector3(RibcageUpper, RibcageLower, Abs).
            /// </summary>
            internal readonly Vector3[] expWeights = expansionRangeWeights;
            //internal /*readonly*/ float[] expansionRangeFactor = expansionRangeFactor;
            internal readonly Interp[] interpIn = interpIn;
            internal readonly Interp[] interpOut = interpOut;
            internal readonly Variability variability = variability;
        }


        #endregion


    }
}
