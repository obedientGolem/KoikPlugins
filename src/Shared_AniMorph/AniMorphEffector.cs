using ADV.Commands.Base;
using KKABMX.Core;
using KKAPI.Studio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using static AniMorph.AniMorphPlugin;
using static AniMorph.MotionModifier;

namespace AniMorph
{
    internal class AniMorphEffector : BoneEffect
    {
        private const float OneThird = (1f / 3f);
        private const float TwoThirds = (2f / 3f);

        // --- Cheeks ---
        //private const string Cheeks  = "cf_J_CheekUpBase";

        // --- Head ---
        private const string Head    = "cf_s_head";
        private const string Neck    = "cf_s_neck";

        // --- Chest ---
        private const string Spine3  = "cf_s_spine03";
        private const string Spine2  = "cf_s_spine02";
        private const string Spine1  = "cf_s_spine01";

        // --- Shoulders ---
        private const string ShldrL  = "cf_s_shoulder02_L";
        private const string Arm1L   = "cf_s_arm01_L";
        private const string Arm2L   = "cf_s_arm02_L";
        private const string Arm3L   = "cf_s_arm03_L";
        private const string FArm1L  = "cf_s_forearm01_L";
        private const string FArm2L  = "cf_s_forearm02_L";

        private const string ShldrR  = "cf_s_shoulder02_R";
        private const string Arm1R   = "cf_s_arm01_R";
        private const string Arm2R   = "cf_s_arm02_R";
        private const string Arm3R   = "cf_s_arm03_R";
        private const string FArm1R  = "cf_s_forearm01_R";
        private const string FArm2R  = "cf_s_forearm02_R";

        // --- Breast ---
        private const string Bust    = "cf_d_bust00";
        private const string Bust1L  = "cf_d_bust01_L";
        private const string Bust1R  = "cf_d_bust01_R";

        // --- Tummy ---
        private const string Waist1  = "cf_s_waist01";        // Position reset by the HScene xyz(false, true, true)

        // --- Pelvis ---
        private const string Waist2  = "cf_s_waist02";        // Position reset by the HScene xyz(false, false, true)
        private const string Kokan   = "cf_d_kokan";
        private const string Ana     = "cf_d_ana";
        private const string ButtL   = "cf_s_siri_L";         // Reset by the HScene xyz(true, true, true)
        private const string ButtR   = "cf_s_siri_R";         // Reset by the HScene xyz(true, true, true)

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
            new (
                name:           Thigh1R,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(OneThird, 1f, 1f + TwoThirds),
                posPositiveApp: Vector3.one,
                posNegativeApp: Vector3.one,
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            new (
                name:           Thigh2R,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(0.25f, TwoThirds, 1f),
                posPositiveApp: Vector3.one,
                posNegativeApp: Vector3.one,
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            new (
                name:           Thigh3R,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(0f, OneThird, OneThird),
                posPositiveApp: Vector3.one,
                posNegativeApp: Vector3.one,
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            
            new (
                name:           Thigh1L,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(OneThird, 1f, 1f + TwoThirds),
                posPositiveApp: Vector3.one,
                posNegativeApp: Vector3.one,
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            new (
                name:           Thigh2L,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(0.25f, TwoThirds, 1f),
                posPositiveApp: Vector3.one,
                posNegativeApp: Vector3.one,
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            new (
                name:           Thigh3L,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(0f, OneThird, OneThird),
                posPositiveApp: Vector3.one,
                posNegativeApp: Vector3.one,
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            new (
                name:           ShldrL,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posPositiveApp: new Vector3(OneThird, 1f + OneThird, 1f),
                posNegativeApp: new Vector3(OneThird, TwoThirds, 1f),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one,
                posFactor:      TwoThirds
                ),
            new (
                name:          ShldrR,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posPositiveApp: new Vector3(OneThird, 1f + OneThird, 1f),
                posNegativeApp: new Vector3(OneThird, TwoThirds, 1f),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one,
                posFactor:      TwoThirds
                ),
            new (
                name:           Arm1L,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posPositiveApp: new Vector3(1f + OneThird, OneThird, 1f),
                posNegativeApp: new Vector3(TwoThirds, TwoThirds, OneThird),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            new (
                name:           Arm1R,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posPositiveApp: new Vector3(1f + OneThird, OneThird, 1f),
                posNegativeApp: new Vector3(TwoThirds, TwoThirds, OneThird),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            new (
                name:           Arm2L,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posPositiveApp: new Vector3(TwoThirds, 2f, 1f),
                posNegativeApp: new Vector3(1f, 1f, OneThird),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            new (
                name:           Arm2R,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posPositiveApp: new Vector3(TwoThirds, 2f, 1f),
                posNegativeApp: new Vector3(1f, 1f, OneThird),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            new (
                name:           Arm3L,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posPositiveApp: new Vector3(1f + OneThird, OneThird, TwoThirds),
                posNegativeApp: new Vector3(OneThird, OneThird, OneThird),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            new (
                name:           Arm3R,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posPositiveApp: new Vector3(1f + OneThird, OneThird, TwoThirds),
                posNegativeApp: new Vector3(OneThird, OneThird, OneThird),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            new (
                name:           FArm1L,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posPositiveApp: new Vector3(OneThird, 0.5f, OneThird),
                posNegativeApp: new Vector3(TwoThirds, 0.5f, TwoThirds),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            new (
                name:           FArm1R,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posPositiveApp: new Vector3(OneThird, 0.5f, OneThird),
                posNegativeApp: new Vector3(TwoThirds, 0.5f, TwoThirds),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            new (
                name:           FArm2L,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posPositiveApp: new Vector3(TwoThirds, OneThird, OneThird),
                posNegativeApp: new Vector3(1f, OneThird, OneThird),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            new (
                name:           FArm2R,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posPositiveApp: new Vector3(TwoThirds, OneThird, OneThird),
                posNegativeApp: new Vector3(1f, OneThird, OneThird),
                rotApplication: Vector3.one,
                sclApplication: Vector3.one
                ),
            new (
                name:           Waist1,
                allowedEffects: Effect.DevAnything,
                posApplication: new Vector3(1f, 1f, 1f),
                posPositiveApp: new Vector3(1f, 1f, 1f),
                posNegativeApp: Vector3.one,
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
                    posPositiveApp: Vector3.one,
                    posNegativeApp: Vector3.one,
                    rotApplication: new Vector3(0f, 0f, 1f),
                    sclApplication: Vector3.one
                    ),
                    [
                        new (
                            name:           Bust1L,
                            allowedEffects: Effect.DevAnything,
                            inheritEffects: Effect.Pos,
                            posApplication: Vector3.one,                
                            posPositiveApp: new Vector3(TwoThirds, 1f, 1f),
                            posNegativeApp: new Vector3(1f, 1f, 0f),
                            rotApplication: Vector3.one,
                            sclApplication: Vector3.one
                            ),
                        new (
                            name:           Bust1R,
                            allowedEffects: Effect.DevAnything,
                            inheritEffects: Effect.Pos,
                            posApplication: Vector3.one,
                            posPositiveApp: Vector3.one,
                            posNegativeApp: new Vector3(TwoThirds, 1f, 0f),
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
                    posPositiveApp: Vector3.one,
                    posNegativeApp: Vector3.one,
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),
                    [
                        new (
                            name:           ButtL,
                            allowedEffects: Effect.DevAnything,
                            posApplication: Vector3.one,
                            posPositiveApp: new Vector3(OneThird, TwoThirds, OneThird),
                            posNegativeApp: new Vector3(1f, 1f + OneThird, 1f),
                            rotApplication: Vector3.one,
                            sclApplication: Vector3.one
                            ),
                        new (
                            name:           ButtR,
                            allowedEffects: Effect.DevAnything,
                            posApplication: new Vector3(1f, TwoThirds, OneThird),
                            posPositiveApp: new Vector3(OneThird, 1f + OneThird, 1f),
                            posNegativeApp: Vector3.one,
                            rotApplication: Vector3.one,
                            sclApplication: Vector3.one
                            ),
                        new (
                            name:           Kokan,
                            allowedEffects: Effect.None,
                            inheritEffects: Effect.Pos,
                            posApplication: new Vector3(1f, 1f, 1f),
                            posPositiveApp: new Vector3(1f, 1f, 1f),
                            posNegativeApp: Vector3.one,
                            rotApplication: Vector3.one,
                            sclApplication: Vector3.one
                            ),
                        new (
                            name:           Ana,
                            allowedEffects: Effect.None,
                            inheritEffects: Effect.Pos,
                            posApplication: new Vector3(1f, 1f, 1f),
                            posPositiveApp: new Vector3(1f, 1f, 1f),
                            posNegativeApp: Vector3.one,
                            rotApplication: Vector3.one,
                            sclApplication: Vector3.one
                            ),
                    ] 
            },
            {
                // Master
                new (
                    name:           Spine2,
                    allowedEffects: Effect.DevAnything,
                    posApplication: Vector3.one,
                    posPositiveApp: Vector3.one,
                    posNegativeApp: Vector3.one,
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),
                    [
                        new (
                            name:           Spine3,
                            allowedEffects: Effect.None,
                            inheritEffects: Effect.Pos,
                            posApplication: Vector3.one,
                            posPositiveApp: new Vector3(1f, OneThird, 1f),
                            posNegativeApp: Vector3.one,
                            rotApplication: Vector3.one,
                            sclApplication: Vector3.one
                            ),
                        new (
                            name:           Spine1,
                            allowedEffects: Effect.None,
                            inheritEffects: Effect.Pos,
                            posApplication: Vector3.one,
                            posPositiveApp: new Vector3(1f, 1f, 1f),
                            posNegativeApp: Vector3.one,
                            rotApplication: Vector3.one,
                            sclApplication: Vector3.one
                            ),
                    ] 
            },
            {
                
                new (
                    name:           Neck,
                    allowedEffects: Effect.None,
                    inheritEffects: Effect.Pos,
                    posApplication: Vector3.one,
                    posPositiveApp: Vector3.one,
                    posNegativeApp: Vector3.one,
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),
                    [
                        new (
                            name:           Head,
                            allowedEffects: Effect.Pos | Effect.Rot,
                            inheritEffects: Effect.Pos,
                            posApplication: Vector3.one,
                            posPositiveApp: Vector3.one,
                            posNegativeApp: Vector3.one,
                            rotApplication: Vector3.one,
                            sclApplication: Vector3.one
                            ),
                    ]
            }
        };

        private static readonly List<string> _bonesWithAnimRot =
        [
            // Animated rotation
            Bust1L,
            // Animated rotation
            Bust1R, 
            // Animated rotation
            Bust,
            //BoneName.Butt,
            //// Static rotation
            //BoneName.Waist02,
            // Animated rotation
            ButtL,
            // Animated rotation
            ButtR,
            // I think it's not.
            // Animated rotation
            // Kokan,
           // Head,
       ];

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

                var isAnimRot = _bonesWithAnimRot.Contains(cfg.name);

                var boneModifierData = new BoneModifierData();

                _mainDic.Add(cfg.name, new BoneData(new MotionModifier(cfg,bone, centerBone, boneModifierData, isAnimRot), boneModifierData));
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
                    var isAnimRot = _bonesWithAnimRot.Contains(cfgSlaves[i].name);

                    boneModifierDataSlaves[i] = new();

                    slaveModifiers[i] = new MotionModifierSlave(cfgSlaves[i], tformSlaves[i], tformMaster, boneModifierDataSlaves[i], isAnimRot);

                    _mainDic.Add(cfgSlaves[i].name, new BoneData(slaveModifiers[i], boneModifierDataSlaves[i]));
                }

                // Add master with slaves
                var isAnimatedBone = _bonesWithAnimRot.Contains(cfgMaster.name);

                var masterModifierData = new BoneModifierData();

                _mainDic.Add(cfgMaster.name, new BoneData(new MotionModifierMaster(cfgMaster, tformMaster, slaveModifiers, masterModifierData, isAnimatedBone), masterModifierData));

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

        private void UpdateModifiers()
        {
            // For some reason it doesn't work on Update(),
            // can't remember why exactly though.
            var dt = Time.deltaTime;
            if (dt == 0f) return;

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


            // Opt for lesser evil during the lag spike,
            if (_filterDeltaTime && dt > (1f / 15f)) dt = (1f / 15f);

            var dtInv = (1f / dt);
            foreach (var key in _effectsToUpdate)
            {
                if (isNewAnimLoop)
                {
                    var motion = _mainDic[key].motion;

                    motion.OnAnimationLoopStart(animLoopFrameCountInv, dt);
                    motion.UpdateModifier(dt, dtInv, animLenInv);
                }
                else
                {
                    _mainDic[key].motion.UpdateModifier(dt, dtInv, animLenInv);
                }
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
                UpdateModifiers();
            }

            return _mainDic[bone].modifier;
        }


        #endregion


        internal void OnSettingChanged()
        {
            foreach (var keyValuePair in _mainDic)
            {
                var bodyPart = ConvertBoneToBody(keyValuePair.Key);
                keyValuePair.Value.motion.OnSettingChanged(bodyPart, _chara);

                var mass = _bodyPartSizeDic.TryGetValue(bodyPart, out var value) ? value : 1f;

                keyValuePair.Value.motion.SetMass(mass);
            }

            var deltaTimeSettingValue = AniMorphPlugin.FilterDeltaTime.Value;

            _filterDeltaTime = deltaTimeSettingValue == AniMorphPlugin.FilterDeltaTimeKind.Enable
                    || (deltaTimeSettingValue == AniMorphPlugin.FilterDeltaTimeKind.OnlyInGame && !StudioAPI.InsideStudio)
                    || (deltaTimeSettingValue == AniMorphPlugin.FilterDeltaTimeKind.OnlyInStudio && StudioAPI.InsideStudio);
        }

        internal void OnSetClothesState(ChaControl chara)
        {
            foreach (var keyValuePair in _mainDic)
            {
                var bodyPart = ConvertBoneToBody(keyValuePair.Key);
                keyValuePair.Value.motion.OnSetClothesState(bodyPart, chara);
            }
        }

        private Body ConvertBoneToBody(string name) => name switch
        {
            //Cheeks => Body.Cheeks,

            Neck or Head => Body.Head,

            Spine1 or Spine2 or Spine3 => Body.Chest,

            ShldrL or Arm1L or Arm2L or Arm3L or FArm1L or FArm2L => Body.Shoulders,
            ShldrR or Arm1R or Arm2R or Arm3R or FArm1R or FArm2R => Body.Shoulders,

            Bust or Bust1L or Bust1R => Body.Breast,

            Waist2 or Kokan or Ana or ButtL or ButtR => Body.Pelvis,

            Thigh1L or Thigh2L or Thigh3L => Body.Thighs,
            Thigh1R or Thigh2R or Thigh3R => Body.Thighs,

            Waist1 => Body.Tummy,

            _ => throw new NotImplementedException(name)
        };

        internal void OnChangeAnimator()
        {
            foreach (var entry in _effectsToUpdate)
            {
                _mainDic[entry].motion.OnChangeAnimator();
            }
        }

        internal void OnSetPlay(string animName)
        {

        }

        internal void OnDisable()
        {
            foreach (var value in _mainDic.Values)
            {
                value.modifier.Clear();
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
            internal BaseConfig(string name, Effect allowedEffects, Vector3 posApplication, Vector3 posPositiveApp, Vector3 posNegativeApp, Vector3 rotApplication, Vector3 sclApplication, Effect inheritEffects = Effect.None, float posFactor = 1f, float rotFactor = 1f, float sclFactor = 1f)
            {
                this.name = name;

                this.posApplication = posApplication;
                this.posPositiveApp = posPositiveApp;
                this.posNegativeApp = posNegativeApp;
                this.rotApplication = rotApplication;
                this.sclApplication = sclApplication;

                this.allowedEffects = allowedEffects;
                this.inheritEffects = inheritEffects;

                this.posFactor = posFactor;
                this.rotFactor = rotFactor;
                this.sclFactor = sclFactor;
            }

            internal readonly string name;
            internal readonly Effect allowedEffects;
            internal readonly Effect inheritEffects;
            internal readonly Vector3 posApplication;
            internal readonly Vector3 posPositiveApp;
            internal readonly Vector3 posNegativeApp;
            internal readonly Vector3 rotApplication;
            internal readonly Vector3 sclApplication;

            internal readonly float posFactor;
            internal readonly float rotFactor;
            internal readonly float sclFactor;
        }


        #endregion
    }
}
