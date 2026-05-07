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
        #region Fields


        // --- Cheeks ---
        //private const string Cheeks  = "cf_J_CheekUpBase";

        // --- Head ---
        private const string Head = "cf_s_head";
        private const string Neck_S = "cf_s_neck";

        // --- Chest ---
        private const string Spine3 = "cf_s_spine03";
        private const string Spine2 = "cf_s_spine02";
        private const string Spine1 = "cf_s_spine01";

        private const string Neck_J = "cf_j_neck";

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
        private readonly List<MotionModifier> _effectsToUpdate = [];


        private readonly Dictionary<Body, float> _bodyPartSizeDic = [];

        private bool _lateUpdated;
        private bool _animChange;
        private float _animChangeTimestamp;

        private float _prevAnimNormTime = 1f;
        private int _animLoopFrameCount;

        private readonly Animator _animator;

        private readonly List<MotionModifierMaster> _devMasterList = [];

        #endregion


        #region CollectionInit


        private static readonly BaseConfig[] _soloInitList =
            [

            // --- Thighs ---

            new (
                name:           Thigh1R,
                allowedEffects: Effect.Pos | Effect.Scl | Effect.RotOffset | Effect.SclOffset,
                posAppPositive: new Vector3(OneThird, 1f, 1f + TwoThirds),
                posAppNegative: new Vector3(OneThird, 1f, 1f + TwoThirds),
                sclApplication: new Vector3(1f, 0f, 1f),
                dotScl_pos: new Vector3(0f, 0f, 0.0825f)
                ),
            new (
                name:           Thigh2R,
                allowedEffects: Effect.Pos | Effect.Scl | Effect.RotOffset | Effect.SclOffset,
                posAppPositive: new Vector3(0.25f, TwoThirds, 1f),
                posAppNegative: new Vector3(0.25f, TwoThirds, 1f),
                sclApplication: new Vector3(1f, 0f, 1f),
                dotScl_pos: new(0f, 0f, 0.0375f)
                ),
            new (
                name:           Thigh3R,
                allowedEffects: Effect.Pos | Effect.Scl | Effect.RotOffset | Effect.SclOffset,
                posAppPositive: new Vector3(0f, OneThird, OneThird),
                posAppNegative: new Vector3(0f, OneThird, OneThird),
                sclApplication: new Vector3(OneThird, 0f, OneThird),
                dotScl_pos: new(0f, 0f, 0.0125f) //0.0625f
                ),
            new (
                name:           Thigh1L,
                allowedEffects: Effect.Pos | Effect.Scl | Effect.RotOffset | Effect.SclOffset,
                posAppPositive: new Vector3(OneThird, 1f, 1f + TwoThirds),
                posAppNegative: new Vector3(OneThird, 1f, 1f + TwoThirds),
                sclApplication: new Vector3(1f, 0f, 1f),
                dotScl_pos: new(0f, 0f, 0.0825f),
                isLeft: true
                ),
            new (
                name:           Thigh2L,
                allowedEffects: Effect.Pos | Effect.Scl | Effect.RotOffset | Effect.SclOffset,
                posAppPositive: new Vector3(0.25f, TwoThirds, 1f),
                posAppNegative: new Vector3(0.25f, TwoThirds, 1f),
                sclApplication: new Vector3(1f, 0f, 1f),
                dotScl_pos: new(0f, 0f, 0.0375f),
                isLeft: true
                ),
            new (
                name:           Thigh3L,
                allowedEffects: Effect.Pos | Effect.Scl | Effect.RotOffset | Effect.SclOffset,
                posAppPositive: new Vector3(0f, OneThird, OneThird),
                posAppNegative: new Vector3(0f, OneThird, OneThird),
                sclApplication: new Vector3(OneThird, 0f, OneThird),
                dotScl_pos: new(0f, 0f, 0.0125f), //0.0625f
                isLeft: true
                ),


            // --- Arms ---

            new (
                name:           Arm2L,
                allowedEffects: Effect.Pos,
                posAppPositive: new Vector3(TwoThirds, 2f, 1f),
                posAppNegative: new Vector3(1f, 1f, OneThird),
                isLeft: true
                ),
            new (
                name:           Arm2R,
                allowedEffects: Effect.Pos,
                posAppPositive: new Vector3(1f, 2f, 1f),
                posAppNegative: new Vector3(TwoThirds, 1f, OneThird)
                ),
            new (
                name:           Arm3L,
                allowedEffects: Effect.Pos,
                posAppPositive: new Vector3(1f + OneThird, OneThird, TwoThirds),
                posAppNegative: new Vector3(OneThird, OneThird, OneThird),
                isLeft: true
                ),
            new (
                name:           Arm3R,
                allowedEffects: Effect.Pos,
                posAppPositive: new Vector3(OneThird, OneThird, TwoThirds),
                posAppNegative: new Vector3(1f + OneThird, OneThird, OneThird)
                ),
            new (
                name:           FArm1L,
                allowedEffects: Effect.Pos,
                posAppPositive: new Vector3(TwoThirds, TwoThirds, TwoThirds),
                posAppNegative: new Vector3(1f ,TwoThirds, TwoThirds),
                isLeft: true
                ),
            new (
                name:           FArm1R,
                allowedEffects: Effect.Pos,
                posAppPositive: new Vector3(1f, TwoThirds, TwoThirds),
                posAppNegative: new Vector3(TwoThirds, TwoThirds, TwoThirds)
                ),
            new (
                name:           FArm2L,
                allowedEffects: Effect.Pos,
                posAppPositive: new Vector3(TwoThirds, TwoThirds, TwoThirds),
                posAppNegative: new Vector3(1f, TwoThirds, TwoThirds),
                isLeft: true
                ),
            new (
                name:           FArm2R,
                allowedEffects: Effect.Pos,
                posAppPositive: new Vector3(1f, OneThird, TwoThirds),
                posAppNegative: new Vector3(TwoThirds, OneThird, TwoThirds)
                ),
            // Children of 'Bust'   
            new (
                name:           Bust1L,
                allowedEffects: Effect.Pos | Effect.Rot | Effect.Scl | Effect.PosOffset | Effect.RotOffset | Effect.SclOffset,
                inheritEffects: Effect.None,
                posAppPositive: new Vector3(TwoThirds, 1f, 1f),
                posAppNegative: new Vector3(1f, 1f, 0f),
                isLeft: true
                ),
            new (
                name:           Bust1R,
                allowedEffects: Effect.Pos | Effect.Rot | Effect.Scl | Effect.PosOffset | Effect.RotOffset | Effect.SclOffset,
                inheritEffects: Effect.None,
                posAppPositive: Vector3.one,
                posAppNegative: new Vector3(TwoThirds, 1f, 0f)
                ),
            ];
        ///*
        // * Master, [ Slave1 ]
        // *         [ Slave2 ]
        // *         [ Slave3(SlaveMaster, has own slaves), SlaveMaster's Slave1, SlaveMaster's Slave2]
        // */
        //private readonly Dictionary<BaseConfig, BaseConfig[][]> _advMasterSlaveInitDic = new()
        //{
        //    {
        //        new (
        //            name:           Spine2,
        //            allowedEffects: Effect.Pos | Effect.Rot | Effect.Scl,
        //            noiseRotF: new Vector3(1f, 1f, TwoThirds),
        //            noiseRotCfg: 5f

        //            ),
        //            [
        //                [
        //                    new (
        //                        name:           Spine1,
        //                        allowedEffects: Effect.None,
        //                        inheritEffects: Effect.Pos | Effect.Rot | Effect.Scl,
        //                        posAppPositive: new Vector3(1f, 1f, 1f),
        //                        posAppNegative: Vector3.one,
        //                        noiseRotF: new Vector3(1f, 1f, OneThird)
        //                        ),
        //                ],
        //                [
        //                    new (
        //                        name:           Spine3,
        //                        allowedEffects: Effect.None,
        //                        inheritEffects: Effect.Pos | Effect.Rot | Effect.Scl,
        //                        posAppPositive: new Vector3(TwoThirds, TwoThirds, TwoThirds), // new Vector3(TwoThirds, OneThird, OneThird),
        //                        posAppNegative: new Vector3(TwoThirds, 1f, OneThird)  // new Vector3(TwoThirds, TwoThirds, OneThird),
        //                        )
        //                ],
        //                [
        //                    new (
        //                        name:           Neck_J,
        //                        allowedEffects: Effect.Rot,
        //                        inheritEffects: Effect.Pos | Effect.Rot,
        //                        posAppPositive: new Vector3(TwoThirds, 1f, 1f),
        //                        posAppNegative: new Vector3(TwoThirds, 1f, 1f),
        //                        noiseRotF:      new Vector3(1f, 1f, TwoThirds),
        //                        noiseRotCfg:    (5f * 1.5f),
        //                        inheritPosF:    TwoThirds
        //                        )
        //                ],
        //                [
        //                    new(
        //                        name:           ShldrL,
        //                        allowedEffects: Effect.Pos,
        //                        inheritEffects: Effect.Pos | Effect.Rot,
        //                        posAppPositive: new Vector3(OneThird, 1f + OneThird, 1f),
        //                        posAppNegative: new Vector3(OneThird, TwoThirds, 1f),
        //                        inheritPosF:    TwoThirds,
        //                        posSpringCfg:         OneThird,
        //                        isLeft: true
                                
        //                        ),
        //                ],
        //                [
        //                    new (
        //                        name:           ShldrR,
        //                        allowedEffects: Effect.Pos,
        //                        inheritEffects: Effect.Pos | Effect.Rot,
        //                        posAppPositive: new Vector3(OneThird, 1f + OneThird, 1f),
        //                        posAppNegative: new Vector3(OneThird, TwoThirds, 1f),
        //                        inheritPosF:    TwoThirds,
        //                        posSpringCfg:   OneThird

        //                        ),
        //                ],
        //                [
        //                    new (
        //                        name:           Arm1L,
        //                        allowedEffects: Effect.Pos,
        //                        inheritEffects: Effect.Pos | Effect.Rot,
        //                        posAppPositive: new Vector3(1f + OneThird, OneThird, 1f),
        //                        posAppNegative: new Vector3(TwoThirds, TwoThirds, OneThird),
        //                        inheritPosF:    OneThird,
        //                        posSpringCfg:   OneThird,
        //                        isLeft: true
        //                        ),
        //                ],
        //                [
        //                    new (
        //                        name:           Arm1R,
        //                        allowedEffects: Effect.Pos,
        //                        inheritEffects: Effect.Pos | Effect.Rot,
        //                        posAppPositive: new Vector3(TwoThirds, OneThird, 1f),
        //                        posAppNegative: new Vector3(1f + OneThird, TwoThirds, OneThird),
        //                        inheritPosF:    OneThird,
        //                        posSpringCfg:   OneThird
        //                        ),
        //                ],
        //                [
        //                    new (
        //                        name:           Bust,
        //                        allowedEffects: Effect.Pos | Effect.Rot,
        //                        inheritEffects: Effect.Pos | Effect.Rot,
        //                        posAppPositive: Vector3.one,
        //                        posAppNegative: new Vector3(1f, 1f, 0f),
        //                        rotApplication: new Vector3(1f, 1f, 1f),
        //                        posSpringCfg: OneThird,
        //                        rotSpringCfg: OneThird,
        //                        noisePosCfg: 0.5f,
        //                        noiseRotCfg: 0.5f

        //                        ),
        //                ]
        //    ]
        //    },
        //};

        private readonly Dictionary<BaseConfig, BaseConfig[]> _masterSlaveInitDic = new()
        {
            {
                new (
                    name:           Spine2,
                    allowedEffects: Effect.Pos | Effect.Rot | Effect.Scl,
                    noiseRotF: new Vector3(1f, 1f, TwoThirds),
                    noiseRotCfg: 5f
                    ),
                    [
                        new (
                            name:           Spine1,
                            allowedEffects: Effect.None,
                            inheritEffects: Effect.Pos | Effect.Rot | Effect.Scl,
                            posAppPositive: new Vector3(1f, 1f, 1f),
                            posAppNegative: Vector3.one,
                            noiseRotF: new Vector3(1f, 1f, OneThird)
                            ),
                        new (
                            name:           Spine3,
                            allowedEffects: Effect.None,
                            inheritEffects: Effect.Pos | Effect.Rot | Effect.Scl,
                            posAppPositive: new Vector3(TwoThirds, TwoThirds, TwoThirds), // new Vector3(TwoThirds, OneThird, OneThird),
                            posAppNegative: new Vector3(TwoThirds, 1f, OneThird)  // new Vector3(TwoThirds, TwoThirds, OneThird),
                            ),
                        new (
                            name:           Neck_J,
                            allowedEffects: Effect.Rot,
                            inheritEffects: Effect.Pos | Effect.Rot,
                            posAppPositive: new Vector3(TwoThirds, 1f, 1f),
                            posAppNegative: new Vector3(TwoThirds, 1f, 1f),
                            noiseRotF:      new Vector3(1f, 1f, TwoThirds),
                            noiseRotCfg:    (5f * 1.5f),
                            inheritPosF:    TwoThirds
                            ),
                        new(
                            name:           ShldrL,
                            allowedEffects: Effect.Pos,
                            inheritEffects: Effect.Pos | Effect.Rot,
                            posAppPositive: new Vector3(OneThird, 1f + OneThird, 1f),
                            posAppNegative: new Vector3(OneThird, TwoThirds, 1f),
                            inheritPosF:    TwoThirds,
                            posSpringCfg:         OneThird,
                            isLeft: true
                            ),
                        new (
                            name:           ShldrR,
                            allowedEffects: Effect.Pos,
                            inheritEffects: Effect.Pos | Effect.Rot,
                            posAppPositive: new Vector3(OneThird, 1f + OneThird, 1f),
                            posAppNegative: new Vector3(OneThird, TwoThirds, 1f),
                            inheritPosF:    TwoThirds,
                            posSpringCfg:   OneThird                            
                            ),
                        new (
                            name:           Arm1L,
                            allowedEffects: Effect.Pos,
                            inheritEffects: Effect.Pos | Effect.Rot,
                            posAppPositive: new Vector3(1f + OneThird, OneThird, 1f),
                            posAppNegative: new Vector3(TwoThirds, TwoThirds, OneThird),
                            inheritPosF:    OneThird,
                            posSpringCfg:   OneThird,
                            isLeft: true
                            ),
                        new (
                            name:           Arm1R,
                            allowedEffects: Effect.Pos,
                            inheritEffects: Effect.Pos | Effect.Rot,
                            posAppPositive: new Vector3(TwoThirds, OneThird, 1f),
                            posAppNegative: new Vector3(1f + OneThird, TwoThirds, OneThird),
                            inheritPosF:    OneThird,
                            posSpringCfg:   OneThird
                            ),
                        new (
                            name:           Bust,
                            allowedEffects: Effect.Pos | Effect.Rot,
                            inheritEffects: Effect.Pos | Effect.Rot,
                            posAppPositive: Vector3.one,
                            posAppNegative: new Vector3(1f, 1f, 0f),
                            rotApplication: new Vector3(1f, 1f, 1f),
                            posSpringCfg: OneThird,
                            rotSpringCfg: OneThird,
                            noisePosCfg: 0.5f,
                            noiseRotCfg: 0.5f
                            ),
                    ]
            },
            {
                new (
                    name:           Waist2,
                    allowedEffects: Effect.Pos | Effect.Rot,
                    posAppPositive: Vector3.one,
                    posAppNegative: Vector3.one,
                    posSpringCfg:   TwoThirds,
                    rotSpringCfg:   TwoThirds,
                    posDampCfg:     1f + TwoThirds,
                    rotDampCfg:     1f + TwoThirds
                    ),
                    [
                        new (
                            name:           Waist1,
                            allowedEffects: Effect.None,
                            inheritEffects: Effect.Pos | Effect.Rot,
                            posAppPositive: Vector3.one,
                            posAppNegative: Vector3.one
                            ),
                        new (
                            name:           ButtL,
                            inheritEffects: Effect.Pos | Effect.Rot,
                            allowedEffects: Effect.Pos | Effect.Rot | Effect.Scl | Effect.Tether | Effect.PosOffset | Effect.RotOffset | Effect.SclOffset,
                            posAppPositive: new Vector3(OneThird, TwoThirds, OneThird),
                            posAppNegative: new Vector3(1f, 1f + OneThird, 1f),
                            dotFlipSign: true
                            ),
                        new (
                            name:           ButtR,
                            inheritEffects: Effect.Pos | Effect.Rot,
                            allowedEffects: Effect.Pos | Effect.Rot | Effect.Scl | Effect.Tether | Effect.PosOffset | Effect.RotOffset | Effect.SclOffset,
                            posAppPositive: new Vector3(1f, TwoThirds, OneThird),
                            posAppNegative: new Vector3(OneThird, 1f + OneThird, 1f),
                            dotFlipSign: true
                            ),
                        new (
                            name:           Kokan,
                            allowedEffects: Effect.None,
                            inheritEffects: Effect.Pos,
                            posAppPositive: new Vector3(1f, 1f, 1f),
                            posAppNegative: Vector3.one
                            ),
                        new (
                            name:           Ana,
                            allowedEffects: Effect.None,
                            inheritEffects: Effect.Pos,
                            posAppPositive: new Vector3(1f, 1f, 1f),
                            posAppNegative: Vector3.one
                            ),
                    ]
            },
            {

                new (
                    name:           Neck_S,
                    allowedEffects: Effect.Pos,
                    posAppPositive: new Vector3(1f, TwoThirds, 1f),
                    posAppNegative: new Vector3(1f, 1f, TwoThirds),
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),
                    [
                        new (
                            name:           Head,
                            allowedEffects: Effect.Rot,
                            inheritEffects: Effect.Pos,
                            posAppPositive: Vector3.one,
                            posAppNegative: Vector3.one,
                            rotApplication: Vector3.one,
                            sclApplication: Vector3.one,
                            noiseRotF: new Vector3(1f, 1f, TwoThirds)
                            ),
                    ]
            },
            //{

            //    new (
            //        name:           Spine1,
            //        allowedEffects: Effect.DevAnything,
            //        posApplication: Vector3.one,
            //        posAppPositive: new Vector3(1f, TwoThirds, 1f),
            //        posAppNegative: Vector3.one,
            //        rotApplication: Vector3.one,
            //        sclApplication: Vector3.one,
            //        noiseRotFactor: new Vector3(1f, 1f, OneThird)

            //        ),
            //        [
            //            new (
            //                name:           Waist1,
            //                allowedEffects: Effect.None,
            //                inheritEffects: Effect.Pos | Effect.Rot | Effect.Scl,
            //                posApplication: new Vector3(1f, 1f, 1f),
            //                posAppPositive: new Vector3(1f, 1f, 1f),
            //                posAppNegative: new Vector3(1f, 1f, TwoThirds),
            //                rotApplication: Vector3.one,
            //                sclApplication: Vector3.one
            //                ),
            //        ]
            //}
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


        #endregion
                    

        #region Init


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


            // TODO This is Broken!!!
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

            foreach (var baseCfg in _soloInitList)
            {
                if (boneLookup.TryGetValue(baseCfg.name, out var boneTransform))
                {
                    _effectsToReturn.Add(baseCfg.name);

                    AddToDic(baseCfg, boneTransform, null);
                }
            }

            foreach (var kv in _masterSlaveInitDic)
            {
                var slaveLen = kv.Value.Length;

                var slaveTransforms = new Transform[slaveLen];

                for (var i = 0; i < slaveLen; i++)
                {
                    var baseCfg = kv.Value[i];

                    if (boneLookup.TryGetValue(baseCfg.name, out var slaveTransform))
                    {
                        _effectsToReturn.Add(baseCfg.name);

                        slaveTransforms[i] = slaveTransform;
                    }
                }

                var master = kv.Key.name;

                if (boneLookup.TryGetValue(master, out var masterTransform))
                {
                    _effectsToReturn.Add(master);

                    AddToDicTandem(kv.Key, kv.Value, masterTransform, slaveTransforms);
                }
            }

            //foreach (var kv in _advMasterSlaveInitDic)
            //{
            //    var masterBaseCfg = kv.Key;

            //    if (!boneLookup.TryGetValue(masterBaseCfg.name, out var masterTransform)) continue;

            //    var slaveLen = kv.Value.Length;

            //    var slaveModifiers = new MotionModifierSlave[slaveLen];

            //    for (var i = 0; i < slaveLen; i++)
            //    {
            //        var jLen = kv.Value[i].Length;
            //        // Has own slaves
            //        if (jLen > 1)
            //        {
            //            var slaveMasterBaseCfg = kv.Value[i][0];

            //            if (!boneLookup.TryGetValue(slaveMasterBaseCfg.name, out var slaveMasterTransform)) continue;

            //            var slaveSlaveModifiers = new MotionModifierSlave[jLen - 1];

            //            for (var j = 1; j < jLen; j++)
            //            {
            //                var slaveSlaveBaseCfg = kv.Value[i][j];

            //                if (!boneLookup.TryGetValue(slaveSlaveBaseCfg.name, out var slaveSlaveTransform)) continue;

            //                var slaveSlaveModifier = new MotionModifierSlave(slaveSlaveBaseCfg, slaveSlaveTransform, slaveMasterTransform);
            //                slaveSlaveModifiers[j - 1] = slaveSlaveModifier;

            //                _mainDic.Add(slaveSlaveBaseCfg.name, slaveSlaveModifier);
            //                _effectsToReturn.Add(slaveSlaveBaseCfg.name);

            //            }

            //            slaveModifiers[i] = new MotionModifierSlaveMaster(slaveMasterBaseCfg, slaveMasterTransform, masterTransform, slaveSlaveModifiers);

            //            _mainDic.Add(slaveMasterBaseCfg.name, slaveModifiers[i]);
            //            _effectsToReturn.Add(slaveMasterBaseCfg.name);
            //        }
            //        else
            //        {
            //            var simpleSlaveBaseCfg = kv.Value[i][0];

            //            if (!boneLookup.TryGetValue(simpleSlaveBaseCfg.name, out var slaveTransform)) continue;

            //            slaveModifiers[i] = new MotionModifierSlave(simpleSlaveBaseCfg, slaveTransform, masterTransform);

            //            _mainDic.Add(simpleSlaveBaseCfg.name, slaveModifiers[i]);
            //            _effectsToReturn.Add(simpleSlaveBaseCfg.name);
            //        }
            //    }

            //    var masterModifier = new MotionModifierMaster(masterBaseCfg, masterTransform, slaveModifiers);

            //    _effectsToUpdate.Add(masterModifier);
            //    _effectsToReturn.Add(masterTransform.name);
            //    _mainDic.Add(masterTransform.name, masterModifier);
            //}

            void AddToDic(BaseConfig cfg, Transform bone, Transform centerBone) //,  Mesh bakedMesh, SkinnedMeshRenderer skinnedMesh)
            {
                if (bone == null || _mainDic.ContainsKey(cfg.name)) return;

                var motionModifier = new MotionModifier(cfg, bone, centerBone);

                _effectsToUpdate.Add(motionModifier);
                _mainDic.Add(cfg.name, motionModifier);
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

                for (var i = 0; i < cfgSlaves.Length; i++)
                {
                    //var isAnimRot = _bonesWithAnimRot.Contains(cfgSlaves[i].name);

                    slaveModifiers[i] = new MotionModifierSlave(cfgSlaves[i], tformSlaves[i], tformMaster);

                    _mainDic.Add(cfgSlaves[i].name, slaveModifiers[i]);
                }

                var masterModifier = new MotionModifierMaster(cfgMaster, tformMaster, slaveModifiers);

                _effectsToUpdate.Add(masterModifier);
                _mainDic.Add(cfgMaster.name, masterModifier);
                _devMasterList.Add(masterModifier);
            }
        }


        #endregion


        #region Update Cycle


        internal void OnUpdate()
        {
            // Called on Update to sample states.
            _lateUpdated = false;

            foreach (var value in _mainDic.Values)
                value.OnUpdate();
        }

        private void UpdateModifiers()
        {
            // Called on LateUpdate by ABMX at 12199 order,
            // if earlier then we miss out on the IK evaluation and the game starts to eff with us.

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
                effect.UpdateModifier(dt, dtInv, animLenInv);
      

            if (isNewAnimLoop)
            {
                foreach (var value in _mainDic.Values)
                    value.OnAnimationLoopStart(animLoopFrameCountInv, dt);
            }
        }


        private void ResetModifiers()
        {
            // Reset everything and start from ground zero.

            foreach (var value in _mainDic.Values)
            {
                value.Reset();
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


        #endregion


        #region Overrides


        public override IEnumerable<string> GetAffectedBones(BoneController origin) => _effectsToReturn;

        public override BoneModifierData GetEffect(string bone, BoneController origin, ChaFileDefine.CoordinateType coordinate)
        {
            if (!_lateUpdated)
            {
                _lateUpdated = true;

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


        #region Settings & Hooks


        internal void OnSettingChanged()
        {
            foreach (var kv in _mainDic)
            {
                var bodyPart = ConvertBoneToBody(kv.Key);
                kv.Value.OnSettingChanged(bodyPart, _chara);

                var mass = _bodyPartSizeDic.TryGetValue(bodyPart, out var value) ? value : 1f;

                kv.Value.SetMass(mass);
            }
        }

        internal void OnSetClothesState(ChaControl chara)
        {
            foreach (var kv in _mainDic)
            {
                var bodyPart = ConvertBoneToBody(kv.Key);
                kv.Value.OnSetClothesState(bodyPart, chara);
            }
        }

        private Body ConvertBoneToBody(string name) => name switch
        {
            //Cheeks => Body.Cheeks,

            Neck_S or Head => Body.Head,

            Spine1 or Spine2 or Spine3 or Neck_J => Body.Chest,

            ShldrL or Arm1L or Arm2L or Arm3L or FArm1L or FArm2L => Body.Shoulders,
            ShldrR or Arm1R or Arm2R or Arm3R or FArm1R or FArm2R => Body.Shoulders,

            Bust or Bust1L or Bust1R => Body.Breast,

            Waist1 or Waist2 or Kokan or Ana or ButtL or ButtR => Body.Pelvis,

            Thigh1L or Thigh2L or Thigh3L => Body.Thighs,
            Thigh1R or Thigh2R or Thigh3R => Body.Thighs,

            //Waist1 or Spine1 => Body.Tummy,

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


        #endregion


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
                Vector3 posAppPositive = new(),
                Vector3 posAppNegative = new(),
                Vector3 rotApplication = new(),
                Vector3 sclApplication = new(),
                Effect inheritEffects = Effect.None,

                float posSpringCfg = 1f,
                float rotSpringCfg = 1f,
                float sclSpringCfg = 1f,

                float posDampCfg = 1f,
                float rotDampCfg = 1f,
                float sclDampCfg = 1f,

                bool dotFlipSign = false,
                Vector3 dotScl_pos = new(),
                bool isLeft = false,
                
                float noisePosCfg = 1f,
                float noiseRotCfg = 1f,
                float noiseSclCfg = 1f,

                Vector3 noisePosF = new(),
                Vector3 noiseRotF = new(),
                Vector3 noiseSclF = new(),

                float inheritPosF = 1f

                )
            {
                this.name = name;

                this.allowedEffects = allowedEffects;
                this.inheritEffects = inheritEffects;

                this.posAppPositive = (posAppPositive == Vector3.zero) ? Vector3.one : posAppPositive; 
                this.posAppNegative = (posAppNegative == Vector3.zero) ? Vector3.one : posAppNegative; 
                this.rotApplication = (rotApplication == Vector3.zero) ? Vector3.one : rotApplication; 
                this.sclApplication = (sclApplication == Vector3.zero) ? Vector3.one : sclApplication; 

                this.posSpringCfg = posSpringCfg;
                this.rotSpringCfg = rotSpringCfg;
                this.sclSpringCfg = sclSpringCfg;

                this.posDampCfg = posDampCfg;
                this.rotDampCfg = rotDampCfg;
                this.sclDampCfg = sclDampCfg;

                this.dotFlipSign = dotFlipSign;
                this.dotScl_pos = dotScl_pos;
                
                this.isLeft = isLeft;

                this.noisePosCfg = noisePosCfg;
                this.noiseRotCfg = noiseRotCfg;
                this.noiseSclCfg = noiseSclCfg;

                this.noisePosF = (noisePosF == Vector3.zero) ? Vector3.one : noisePosF;
                this.noiseRotF = (noiseRotF == Vector3.zero) ? Vector3.one : noiseRotF;
                this.noiseSclF = (noiseSclF == Vector3.zero) ? Vector3.one : noiseSclF;

                this.inheritPosF = inheritPosF;
            }

            internal readonly string name;
            internal  Effect allowedEffects;
            internal  Effect inheritEffects;
            internal  Vector3 posAppPositive;
            internal  Vector3 posAppNegative;
            internal  Vector3 rotApplication;
            internal  Vector3 sclApplication;

            internal  float posSpringCfg;
            internal  float rotSpringCfg;
            internal  float sclSpringCfg;

            internal float posDampCfg;
            internal float rotDampCfg;
            internal float sclDampCfg;

            internal  bool dotFlipSign;
            internal  Vector3 dotScl_pos;

            internal  bool isLeft;

            internal  float noisePosCfg;
            internal  float noiseRotCfg;
            internal  float noiseSclCfg;

            internal  Vector3 noisePosF;
            internal  Vector3 noiseRotF;
            internal  Vector3 noiseSclF;

            internal float inheritPosF;
        }


        #endregion
    }
}
