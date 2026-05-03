using ADV.Commands.Base;
using KKABMX.Core;
using KKAPI.Studio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UniRx.Operators;
using UnityEngine;
using static AniMorph.AniMorphPlugin;
using static AniMorph.MotionModifier;

namespace AniMorph
{
    internal class AniMorphEffector : BoneEffect
    {
        // --- Cheeks ---
        //private const string Cheeks  = "cf_J_CheekUpBase";

        // --- Head ---
        private const string Head = "cf_s_head";
        private const string Neck = "cf_s_neck";

        // --- Chest ---
        private const string Spine3 = "cf_s_spine03";
        private const string Spine2 = "cf_s_spine02";
        private const string Spine1 = "cf_s_spine01";

        // --- Shoulders ---
        private const string ShldrL = "cf_s_shoulder02_L";
        private const string Arm1L = "cf_s_arm01_L";
        private const string Arm2L = "cf_s_arm02_L";
        private const string Arm3L = "cf_s_arm03_L";
        private const string FArm1L = "cf_s_forearm01_L";
        private const string FArm2L = "cf_s_forearm02_L";

        private const string ShldrR = "cf_s_shoulder02_R";
        private const string Arm1R = "cf_s_arm01_R";
        private const string Arm2R = "cf_s_arm02_R";
        private const string Arm3R = "cf_s_arm03_R";
        private const string FArm1R = "cf_s_forearm01_R";
        private const string FArm2R = "cf_s_forearm02_R";

        // --- Breast ---
        private const string Bust = "cf_d_bust00";
        private const string Bust1L = "cf_d_bust01_L";
        private const string Bust1R = "cf_d_bust01_R";

        // --- Tummy ---
        private const string Waist1 = "cf_s_waist01";        // Position reset by the HScene xyz(false, true, true)

        // --- Pelvis ---
        private const string Waist2 = "cf_s_waist02";        // Position reset by the HScene xyz(false, false, true)
        private const string Kokan = "cf_d_kokan";
        private const string Ana = "cf_d_ana";
        private const string ButtL = "cf_s_siri_L";         // Reset by the HScene xyz(true, true, true)
        private const string ButtR = "cf_s_siri_R";         // Reset by the HScene xyz(true, true, true)

        // --- Thighs ---
        private const string Thigh1R = "cf_s_thigh01_R";
        private const string Thigh2R = "cf_s_thigh02_R";
        private const string Thigh3R = "cf_s_thigh03_R";

        private const string Thigh1L = "cf_s_thigh01_L";
        private const string Thigh2L = "cf_s_thigh02_L";
        private const string Thigh3L = "cf_s_thigh03_L";


        private readonly ChaControl _chara;

        //private readonly Dictionary<string, BoneData> _mainDic = new(StringComparer.Ordinal);
        private readonly Dictionary<string, MotionModifier> _mainDic = new(StringComparer.Ordinal);

        private readonly List<string> _effectsToReturn = [];
        private readonly List<string> _effectsToUpdate = [];
        private static readonly BaseConfig[] _soloInitList =
            [

            // --- Thighs ---

            new (
                name:           Thigh1R,
                allowedEffects: Effect.Pos | Effect.Scl | Effect.RotOffset | Effect.SclOffset,
                posApplication: new Vector3(OneThird, 1f, 1f + TwoThirds),
                posAppPositive: Vector3.one,
                posAppNegative: Vector3.one,
                rotApplication: Vector3.one,
                sclApplication: new Vector3(1f, 0f, 1f),
                dotScl_posFactor: new Vector3(0f, 0f, 0.0825f)
                ),
            new (
                name:           Thigh2R,
                allowedEffects: Effect.Pos | Effect.Scl | Effect.RotOffset | Effect.SclOffset,
                posApplication: new Vector3(0.25f, TwoThirds, 1f),
                posAppPositive: Vector3.one,
                posAppNegative: Vector3.one,
                rotApplication: Vector3.one,
                sclApplication: new Vector3(1f, 0f, 1f),
                dotScl_posFactor: new(0f, 0f, 0.0375f)
                ),
            new (
                name:           Thigh3R,
                allowedEffects: Effect.Pos | Effect.Scl | Effect.RotOffset | Effect.SclOffset,
                posApplication: new Vector3(0f, OneThird, OneThird),
                posAppPositive: Vector3.one,
                posAppNegative: Vector3.one,
                rotApplication: Vector3.one,
                sclApplication: new Vector3(OneThird, 0f, OneThird),
                dotScl_posFactor: new(0f, 0f, 0.0125f) //0.0625f
                ),
            new (
                name:           Thigh1L,
                allowedEffects: Effect.Pos | Effect.Scl | Effect.RotOffset | Effect.SclOffset,
                posApplication: new Vector3(OneThird, 1f, 1f + TwoThirds),
                posAppPositive: Vector3.one,
                posAppNegative: Vector3.one,
                rotApplication: Vector3.one,
                sclApplication: new Vector3(1f, 0f, 1f),
                dotScl_posFactor: new(0f, 0f, 0.0825f),
                isLeft: true
                ),
            new (
                name:           Thigh2L,
                allowedEffects: Effect.Pos | Effect.Scl | Effect.RotOffset | Effect.SclOffset,
                posApplication: new Vector3(0.25f, TwoThirds, 1f),
                posAppPositive: Vector3.one,
                posAppNegative: Vector3.one,
                rotApplication: Vector3.one,
                sclApplication: new Vector3(1f, 0f, 1f),
                dotScl_posFactor: new(0f, 0f, 0.0375f),
                isLeft: true
                ),
            new (
                name:           Thigh3L,
                allowedEffects: Effect.Pos | Effect.Scl | Effect.RotOffset | Effect.SclOffset,
                posApplication: new Vector3(0f, OneThird, OneThird),
                posAppPositive: Vector3.one,
                posAppNegative: Vector3.one,
                rotApplication: Vector3.one,
                sclApplication: new Vector3(OneThird, 0f, OneThird),
                dotScl_posFactor: new(0f, 0f, 0.0125f), //0.0625f
                isLeft: true
                ),

            // --- Arms ---

            new (
                name:           ShldrL,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posAppPositive: new Vector3(OneThird, 1f + OneThird, 1f),
                posAppNegative: new Vector3(OneThird, TwoThirds, 1f),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one,
                posFactor:      TwoThirds,
                isLeft: true
                ),
            new (
                name:          ShldrR,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posAppPositive: new Vector3(OneThird, 1f + OneThird, 1f),
                posAppNegative: new Vector3(OneThird, TwoThirds, 1f),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one,
                posFactor:      TwoThirds
                ),
            new (
                name:           Arm1L,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posAppPositive: new Vector3(1f + OneThird, OneThird, 1f),
                posAppNegative: new Vector3(TwoThirds, TwoThirds, OneThird),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one,
                isLeft: true
                ),
            new (
                name:           Arm1R,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posAppPositive: new Vector3(1f + OneThird, OneThird, 1f),
                posAppNegative: new Vector3(TwoThirds, TwoThirds, OneThird),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            new (
                name:           Arm2L,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posAppPositive: new Vector3(TwoThirds, 2f, 1f),
                posAppNegative: new Vector3(1f, 1f, OneThird),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one,
                isLeft: true
                ),
            new (
                name:           Arm2R,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posAppPositive: new Vector3(TwoThirds, 2f, 1f),
                posAppNegative: new Vector3(1f, 1f, OneThird),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            new (
                name:           Arm3L,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posAppPositive: new Vector3(1f + OneThird, OneThird, TwoThirds),
                posAppNegative: new Vector3(OneThird, OneThird, OneThird),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one,
                isLeft: true
                ),
            new (
                name:           Arm3R,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posAppPositive: new Vector3(1f + OneThird, OneThird, TwoThirds),
                posAppNegative: new Vector3(OneThird, OneThird, OneThird),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            new (
                name:           FArm1L,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posAppPositive: new Vector3(OneThird, 0.5f, OneThird),
                posAppNegative: new Vector3(TwoThirds, 0.5f, TwoThirds),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one,
                isLeft: true
                ),
            new (
                name:           FArm1R,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posAppPositive: new Vector3(OneThird, 0.5f, OneThird),
                posAppNegative: new Vector3(TwoThirds, 0.5f, TwoThirds),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            new (
                name:           FArm2L,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posAppPositive: new Vector3(TwoThirds, OneThird, OneThird),
                posAppNegative: new Vector3(1f, OneThird, OneThird),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one,
                isLeft: true
                ),
            new (
                name:           FArm2R,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posAppPositive: new Vector3(TwoThirds, OneThird, OneThird),
                posAppNegative: new Vector3(1f, OneThird, OneThird),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            ];

        private readonly Dictionary<BaseConfig, BaseConfig[]> _masterSlaveInitDic = new()
        {
            {
                new (
                    name:           Bust,
                    allowedEffects: Effect.None,
                    posApplication: Vector3.one,
                    posAppPositive: Vector3.one,
                    posAppNegative: Vector3.one,
                    rotApplication: new Vector3(0f, 0f, 1f),
                    sclApplication: Vector3.one
                    ),
                    [
                        new (
                            name:           Bust1L,
                            allowedEffects: Effect.DevAnything,
                            inheritEffects: Effect.None,
                            posApplication: Vector3.one,
                            posAppPositive: new Vector3(TwoThirds, 1f, 1f),
                            posAppNegative: new Vector3(1f, 1f, 0f),
                            rotApplication: Vector3.one,
                            sclApplication: Vector3.one
                            ),
                        new (
                            name:           Bust1R,
                            allowedEffects: Effect.DevAnything,
                            inheritEffects: Effect.None,
                            posApplication: Vector3.one,
                            posAppPositive: Vector3.one,
                            posAppNegative: new Vector3(TwoThirds, 1f, 0f),
                            rotApplication: Vector3.one,
                            sclApplication: Vector3.one
                            ),
                    ]
            },
            {
                new (
                    name:           Waist2,
                    allowedEffects: Effect.Pos | Effect.Rot,
                    posApplication: Vector3.one,
                    posAppPositive: Vector3.one,
                    posAppNegative: Vector3.one,
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),
                    [
                        new (
                            name:           ButtL,
                            inheritEffects: Effect.Pos | Effect.Rot,
                            allowedEffects: Effect.Pos | Effect.Rot | Effect.Scl | Effect.Tether | Effect.PosOffset | Effect.RotOffset | Effect.SclOffset,
                            posApplication: Vector3.one,
                            posAppPositive: new Vector3(OneThird, TwoThirds, OneThird),
                            posAppNegative: new Vector3(1f, 1f + OneThird, 1f),
                            rotApplication: Vector3.one,
                            sclApplication: Vector3.one,
                            dotFlipSign: true
                            ),
                        new (
                            name:           ButtR,
                            inheritEffects: Effect.Pos | Effect.Rot,
                            allowedEffects: Effect.Pos | Effect.Rot | Effect.Scl | Effect.Tether | Effect.PosOffset | Effect.RotOffset | Effect.SclOffset,
                            posApplication: new Vector3(1f, TwoThirds, OneThird),
                            posAppPositive: new Vector3(OneThird, 1f + OneThird, 1f),
                            posAppNegative: Vector3.one,
                            rotApplication: Vector3.one,
                            sclApplication: Vector3.one,
                            dotFlipSign: true
                            ),
                        new (
                            name:           Kokan,
                            allowedEffects: Effect.None,
                            inheritEffects: Effect.Pos,
                            posApplication: new Vector3(1f, 1f, 1f),
                            posAppPositive: new Vector3(1f, 1f, 1f),
                            posAppNegative: Vector3.one,
                            rotApplication: Vector3.one,
                            sclApplication: Vector3.one
                            ),
                        new (
                            name:           Ana,
                            allowedEffects: Effect.None,
                            inheritEffects: Effect.Pos,
                            posApplication: new Vector3(1f, 1f, 1f),
                            posAppPositive: new Vector3(1f, 1f, 1f),
                            posAppNegative: Vector3.one,
                            rotApplication: Vector3.one,
                            sclApplication: Vector3.one
                            ),
                    ]
            },
            {
                new (
                    name:           Spine2,
                    allowedEffects: Effect.DevAnything,
                    posApplication: Vector3.one,
                    posAppPositive: Vector3.one,
                    posAppNegative: Vector3.one,
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),
                    [
                        new (
                            name:           Spine3,
                            allowedEffects: Effect.None,
                            inheritEffects: Effect.Pos | Effect.Rot | Effect.Scl,
                            posApplication: Vector3.one,
                            posAppPositive: new Vector3(1f, OneThird, 1f),
                            posAppNegative: Vector3.one,
                            rotApplication: Vector3.one,
                            sclApplication: Vector3.one
                            ),
                        //new(
                        //    name:           Neck,
                        //    allowedEffects: Effect.Pos,
                        //    inheritEffects: Effect.None,
                        //    posApplication: Vector3.one,
                        //    posAppPositive: Vector3.one,
                        //    posAppNegative: Vector3.one,
                        //    rotApplication: Vector3.one,
                        //    sclApplication: Vector3.one                    ),
                        //new (
                        //    name:           Head,
                        //    allowedEffects: Effect.Rot,
                        //    inheritEffects: Effect.Pos,
                        //    posApplication: Vector3.one,
                        //    posAppPositive: Vector3.one,
                        //    posAppNegative: Vector3.one,
                        //    rotApplication: Vector3.one,
                        //    sclApplication: Vector3.one
                        //    ),
                    ]
            },
            {

                new (
                    name:           Neck,
                    allowedEffects: Effect.Pos | Effect.Rot,
                    inheritEffects: Effect.None,
                    posApplication: Vector3.one,
                    posAppPositive: Vector3.one,
                    posAppNegative: Vector3.one,
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),
                    [
                        new (
                            name:           Head,
                            allowedEffects: Effect.Rot,
                            inheritEffects: Effect.Pos,
                            posApplication: Vector3.one,
                            posAppPositive: Vector3.one,
                            posAppNegative: Vector3.one,
                            rotApplication: Vector3.one,
                            sclApplication: Vector3.one
                            ),
                    ]
            },
            {

                new (
                    name:           Spine1,
                    allowedEffects: Effect.DevAnything,
                    posApplication: Vector3.one,
                    posAppPositive: new Vector3(1f, TwoThirds, 1f),
                    posAppNegative: Vector3.one,
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),
                    [
                        new (
                            name:           Waist1,
                            allowedEffects: Effect.None,
                            inheritEffects: Effect.Pos | Effect.Rot | Effect.Scl,
                            posApplication: new Vector3(1f, 1f, 1f),
                            posAppPositive: new Vector3(1f, 1f, 1f),
                            posAppNegative: new Vector3(1f, 1f, TwoThirds),
                            rotApplication: Vector3.one,
                            sclApplication: Vector3.one
                            ),
                    ]
            }
        };

        //private static readonly List<string> _bonesWithAnimRot = new()
        //{
        //    // Animated rotation
        //    Bust1L,
        //    // Animated rotation
        //    Bust1R,
        //    // Animated rotation
        //    Bust, 
        //    //BoneName.Butt,
        //    //// Static rotation
        //    //BoneName.Waist02,
        //    // Animated rotation
        //    ButtL,
        //    // Animated rotation
        //    ButtR,
        //    // I think it's not.
        //    // Animated rotation
        //    // Kokan,
        //    // Head,
        //};

        // BodyPart + boneNames-defaultMass pairs
        private static readonly Dictionary<Body, BodyPartMeasurement> _bonesToCheckForSizeDic = new()
        {
            { Body.Breast, 
                new (
                    [ "cf_d_bust03_L"],
                    1.0555f
                    ) 
            },
            { Body.Pelvis, 
                new (
                    ["cf_s_siri_L" ],
                    1.3573f
                    ) 
            },
        };

        private readonly Dictionary<Body, float> _bodyPartSizeDic = [];

        private bool _filterDeltaTime;
        private bool _updated;
        private bool _animChange;
        private float _animChangeTimestamp;

        private float _prevAnimNormTime = 1f;
        private int _animLoopFrameCount;

        private readonly Animator _animator;

            

        #region Initialization


        internal AniMorphEffector(ChaControl chara)
        {
            _chara = chara;

            if (chara.animBody == null) throw new NullReferenceException(nameof(Animator));

            _animator = chara.animBody;


            Setup();
            OnSettingChanged();
        }

        private void Setup()
        {
            // Setup bones for effector

            //// Required for mesh measurements
            //var skinnedMesh = (_chara.rendBody.GetComponent<SkinnedMeshRenderer>());

            //if (skinnedMesh == null)
            //{
            //    AniMorph.Logger.LogDebug($"{GetType().Name} couldn't find mesh.");
            //    return;
            //}
            //var bakedMesh = new Mesh();
            //skinnedMesh.BakeMesh(bakedMesh);



            var boneLookup = new Dictionary<string, Transform>(StringComparer.Ordinal);

            foreach (var t in _chara.transform.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (boneLookup.ContainsKey(t.name))
                    continue;
                
                boneLookup.Add(t.name, t);
            }


            foreach (var keyValuePair in _bonesToCheckForSizeDic)
            {
                var boneSet = new HashSet<string>(keyValuePair.Value.bonesToMeasure);

                var bodyPartScale = 1f;
                var found = 0;

                foreach (var kv in boneLookup)
                {
                    if (!boneSet.Contains(kv.Key))
                        continue;

                    found++;
                    var lossyScale = kv.Value.lossyScale;
                    bodyPartScale *= lossyScale.x * lossyScale.y * lossyScale.z;
                }
                // Fallback to default
                if (found != keyValuePair.Value.bonesToMeasure.Length)
                {
                    AniMorphPlugin.Logger.LogWarning($"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}:Couldn't find all bones for measurement of {keyValuePair.Key}, falling back to the default.");

                    bodyPartScale = keyValuePair.Value.defaultMass;
                }
                else
                {
                    bodyPartScale /= keyValuePair.Value.defaultMass;
                }

                _bodyPartSizeDic.Add(keyValuePair.Key, bodyPartScale);
            }

            // Iterate through singular items
            foreach (var single in _soloInitList)
            {
                if (boneLookup.TryGetValue(single.name, out var boneTransform))
                {
                    _effectsToReturn.Add(single.name);
                    _effectsToUpdate.Add(single.name);

                    Transform centeredBoneTransform = null;

                    if (GetCenteredBone(single.name, out var centeredBone))
                    {
                        boneLookup.TryGetValue(centeredBone, out centeredBoneTransform);
                    }
                    AddToDic(single, boneTransform, centeredBoneTransform);
                }
            }

            // Iterate through tandems
            foreach (var kv in _masterSlaveInitDic)
            {
                var slaveLen = kv.Value.Length;
                // Prepare arrays for init under master
                var slaveTransforms = new Transform[slaveLen];
                for (var i = 0; i < slaveLen; i++)
                {
                    var slave = kv.Value[i];

                    if (boneLookup.TryGetValue(slave.name, out var slaveTransform))
                    {
                        // Slaves are updated by master.
                        _effectsToReturn.Add(slave.name);
                        // Fill in arrays to pass them to the master
                        slaveTransforms[i] = slaveTransform;
                    }
                }

                var master = kv.Key.name;

                if (boneLookup.TryGetValue(master, out var masterTransform))
                {
                    // Some masters track one bone but apply to another.
                    _effectsToReturn.Add(master);
                    // Master is called to update himself and his slaves.
                    _effectsToUpdate.Add(kv.Key.name);

                    // Init master with slaves together
                    AddToDicTandem(kv.Key, kv.Value, masterTransform, slaveTransforms); // bakedMesh, skinnedMesh);
                }
            }

            void AddToDic(BaseConfig cfg, Transform bone, Transform centerBone) //,  Mesh bakedMesh, SkinnedMeshRenderer skinnedMesh)
            {
                // Perform null checks
                if (_mainDic.ContainsKey(cfg.name) || bone == null) return;

                //var isAnimRot = _bonesWithAnimRot.Contains(cfg.name);

                _mainDic.Add(cfg.name, new MotionModifier(cfg,bone, centerBone));
            }

            void AddToDicTandem(BaseConfig cfgMaster, BaseConfig[] cfgSlaves, Transform tformMaster, Transform[] tformSlaves)
            {
                // Perform null checks
                if (_mainDic.ContainsKey(cfgMaster.name) || tformMaster == null) return;

                for (var i = 0;  i < cfgSlaves.Length; i++)
                {
                    if (_mainDic.ContainsKey(cfgSlaves[i].name) || tformSlaves[i] == null) return;
                }

                // Add and organize slaves
                var slaveModifiers = new MotionModifierSlave[cfgSlaves.Length];

                // Bit of an oversight with boneModifierData as modifiers got access
                // to it much later in the development, so we add it twice on init.
                var boneModifierDataSlaves = new BoneModifierData[cfgSlaves.Length];

                for (var i = 0; i < cfgSlaves.Length; i++)
                {
                    //var isAnimRot = _bonesWithAnimRot.Contains(cfgSlaves[i].name);

                    slaveModifiers[i] = new MotionModifierSlave(cfgSlaves[i], tformSlaves[i], tformMaster);

                    _mainDic.Add(cfgSlaves[i].name, slaveModifiers[i]);
                }

                // Add master with slaves
                //var isAnimatedBone = _bonesWithAnimRot.Contains(cfgMaster.name);

                _mainDic.Add(cfgMaster.name, new MotionModifierMaster(cfgMaster, tformMaster, slaveModifiers));

            }
            bool GetCenteredBone(string boneName, out string centeredBoneName)
            {
                centeredBoneName = boneName switch
                {
                    Bust1L or Bust1R => Bust,
                    ButtL or ButtR => Waist2,
                    _ => "",
                };
                return !centeredBoneName.IsNullOrEmpty();
            }
        }


        #endregion


        #region Update Cycle


        internal void OnUpdate()
        {
            _updated = false;

            foreach (var name in _effectsToUpdate)
            {
                _mainDic[name].OnUpdate();
            }
        }

        private bool IsSeriousLagSpike
        {
            get
            {
                var value = IsFade || (IsLagSpike && _animChangeTimestamp > Time.time);

                if (!value && field)
                    ResetModifiers();
                
                field = value;

                return field;
            }
        }

        private void UpdateModifiers()
        {
            if (IsSeriousLagSpike || IsPause) return;

            var dt = IsLagSpike ? dtAvg : Time.deltaTime;

            var animState = _animator.GetCurrentAnimatorStateInfo(0);

            var animLen = animState.length;
            var animTime = animState.normalizedTime;

            var animLenInv = animLen == 0f ? 1f : 1f / animLen;

            var animTimeF = animTime - (int)animTime;
            var isNewAnimLoop = animTimeF < _prevAnimNormTime;
            _prevAnimNormTime = animTimeF;

            var animLoopFrameCountInv = 0f;

            if (!isNewAnimLoop)
            {
                _animLoopFrameCount++;
            }
            else if (_animLoopFrameCount != 0)
            {
                animLoopFrameCountInv = 1f / _animLoopFrameCount;
                _animLoopFrameCount = 0;
            }

            var dtInv = AniMorphPlugin.dtInv;

            foreach (var effect in _effectsToUpdate)
            {
                if (isNewAnimLoop)
                {
                    var motion = _mainDic[effect];

                    motion.OnAnimationLoopStart(animLoopFrameCountInv, dt);
                    motion.UpdateModifier(dt, dtInv, animLenInv);
                }
                else
                {
                    _mainDic[effect].UpdateModifier(dt, dtInv, animLenInv);
                }
            }
        }


        private void ResetModifiers()
        {
            foreach (var value in _mainDic.Values)
            {
                value.Reset();
            }
        }


        #endregion


        #region Overrides


        public override IEnumerable<string> GetAffectedBones(BoneController origin) => _effectsToReturn;

        public override BoneModifierData GetEffect(string bone, BoneController origin, ChaFileDefine.CoordinateType coordinate)
        {
            if (!_updated)
            {
                _updated = true;

                if (_animChange)
                {
                    _animChange = false;

                    ResetModifiers();
                }
                else
                {
                    UpdateModifiers();
                }
            }

            return _mainDic[bone].GetBoneModifierData;
        }


        #endregion


        internal void OnSettingChanged()
        {
            foreach (var keyValuePair in _mainDic)
            {
                var bodyPart = ConvertBoneToBody(keyValuePair.Key);
                keyValuePair.Value.OnSettingChanged(bodyPart, _chara);

                var mass = _bodyPartSizeDic.TryGetValue(bodyPart, out var value) ? value : 1f;

                keyValuePair.Value.SetMass(mass);
            }
        }

        internal void OnSetClothesState(ChaControl chara)
        {
            foreach (var keyValuePair in _mainDic)
            {
                var bodyPart = ConvertBoneToBody(keyValuePair.Key);
                keyValuePair.Value.OnSetClothesState(bodyPart, chara);
            }
        }

        private Body ConvertBoneToBody(string name) => name switch
        {
            //Cheeks => Body.Cheeks,

            Neck or Head => Body.Head,

            Spine2 or Spine3 => Body.Chest,

            ShldrL or Arm1L or Arm2L or Arm3L or FArm1L or FArm2L => Body.Shoulders,
            ShldrR or Arm1R or Arm2R or Arm3R or FArm1R or FArm2R => Body.Shoulders,

            Bust or Bust1L or Bust1R => Body.Breast,

            Waist2 or Kokan or Ana or ButtL or ButtR => Body.Pelvis,

            Thigh1L or Thigh2L or Thigh3L => Body.Thighs,
            Thigh1R or Thigh2R or Thigh3R => Body.Thighs,

            Waist1 or Spine1 => Body.Tummy,

            _ => throw new NotImplementedException(name)
        };

        internal void OnLoadAnimation()
        {
            _animChange = true;
            _animChangeTimestamp = Time.time + 3f;
        }

        internal void OnSetPlay(string animName)
        {

        }

        internal void OnDisable()
        {
            foreach (var value in _mainDic.Values)
            {
                value.GetBoneModifierData.Clear();
            }
        }


        #region Types


        private readonly struct BodyPartMeasurement(string[] bonesToMeasure, float defaultMass)
        {
            internal readonly string[] bonesToMeasure = bonesToMeasure;
            internal readonly float defaultMass = defaultMass;
        }

        // Very rare access, no point in struct.
        internal class BaseConfig
        {
            internal BaseConfig(
                string name,
                Effect allowedEffects,
                Vector3 posApplication,
                Vector3 posAppPositive,
                Vector3 posAppNegative,
                Vector3 rotApplication,
                Vector3 sclApplication,
                Effect inheritEffects = Effect.None,
                float posFactor = 1f,
                float rotFactor = 1f,
                float sclFactor = 1f,
                bool dotFlipSign = false,
                Vector3 dotScl_posFactor = new(),
                bool isLeft = false

                )
            {
                this.name = name;

                this.posApplication = posApplication;
                this.posAppPositive = posAppPositive;
                this.posAppNegative = posAppNegative;
                this.rotApplication = rotApplication;
                this.sclApplication = sclApplication;

                this.allowedEffects = allowedEffects;
                this.inheritEffects = inheritEffects;

                this.posFactor = posFactor;
                this.rotFactor = rotFactor;
                this.sclFactor = sclFactor;

                this.dotFlipSign = dotFlipSign;
                this.dotScl_posFactor = dotScl_posFactor;
                
                this.isLeft = isLeft;
                
            }

            internal readonly string name;
            internal readonly Effect allowedEffects;
            internal readonly Effect inheritEffects;
            internal readonly Vector3 posApplication;
            internal readonly Vector3 posAppPositive;
            internal readonly Vector3 posAppNegative;
            internal readonly Vector3 rotApplication;
            internal readonly Vector3 sclApplication;

            internal readonly float posFactor;
            internal readonly float rotFactor;
            internal readonly float sclFactor;

            internal readonly bool dotFlipSign;
            internal readonly Vector3 dotScl_posFactor;

            internal readonly bool isLeft;
        }


        #endregion
    }
}
