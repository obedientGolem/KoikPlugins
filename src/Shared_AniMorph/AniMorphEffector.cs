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

        // --- Chest ---
        private const string Spine03 = "cf_s_spine03";
        private const string Spine02 = "cf_s_spine02";
        private const string Spine01 = "cf_s_spine01";

        // --- Shoulders ---
        private const string ShldrL = "cf_s_arm01_L";
        private const string ShldrR = "cf_s_arm01_R";

        // --- Breast ---
        private const string Bust    = "cf_d_bust00";
        private const string Bust1L  = "cf_d_bust01_L";
        private const string Bust1R  = "cf_d_bust01_R";

        // --- Pelvis ---
        private const string Waist01 = "cf_s_waist01";        // Position reset by the HScene xyz(false, true, true)
        private const string Waist02 = "cf_s_waist02";        // Position reset by the HScene xyz(false, false, true)
        private const string Kokan = "cf_j_kokan";
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

        private readonly Dictionary<BoneName, BoneData> _mainDic = [];
        private static readonly Dictionary<string, BoneName> _mapDic = new(StringComparer.Ordinal)
        {
            { Spine03, BoneName.Spine03 },
            { Spine02, BoneName.Spine02 },
            { Spine01, BoneName.Spine01 },
            { Bust,    BoneName.Bust },
            { Bust1L,  BoneName.Bust1L },
            { Bust1R,  BoneName.Bust1R },
            { Thigh1R, BoneName.Thigh1R },
            { Thigh2R, BoneName.Thigh2R },
            { Thigh3R, BoneName.Thigh3R },
            { Thigh1L, BoneName.Thigh1L },
            { Thigh2L, BoneName.Thigh2L },
            { Thigh3L, BoneName.Thigh3L },


            { ButtL,   BoneName.ButtL },
            { ButtR,   BoneName.ButtR },
            { Kokan,   BoneName.Kokan },
            { Waist01, BoneName.Waist01 },
            { Waist02, BoneName.Waist02 },
        };

        private readonly List<string> _effectsToReturn = [];
        private readonly List<BoneName> _effectsToUpdate = [];
        private static readonly BoneConfig[] _singleList =
            [
                new (
                    name:           Thigh1R,
                    enumName:       BoneName.Thigh1R,
                    allowedEffects: Effect.DevAnything,
                    posApplication: new Vector3(OneThird, 1f, 1f + TwoThirds),
                    posPositiveApp: Vector3.one,
                    posNegativeApp: Vector3.one,
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),
                new (
                    name:           Thigh2R,
                    enumName:       BoneName.Thigh2R,
                    allowedEffects: Effect.DevAnything,
                    posApplication: new Vector3(0.25f, TwoThirds, 1f),
                    posPositiveApp: Vector3.one,
                    posNegativeApp: Vector3.one,
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),
                new (
                    name:           Thigh3R,
                    enumName:       BoneName.Thigh3R,
                    allowedEffects: Effect.DevAnything,
                    posApplication: new Vector3(0f, OneThird, OneThird),
                    posPositiveApp: Vector3.one,
                    posNegativeApp: Vector3.one,
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),

                new (
                    name:           Thigh1L,
                    enumName:       BoneName.Thigh1L,
                    allowedEffects: Effect.DevAnything,
                    posApplication: new Vector3(OneThird, 1f, 1f + TwoThirds),
                    posPositiveApp: Vector3.one,
                    posNegativeApp: Vector3.one,
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),
                new (
                    name:           Thigh2L,
                    enumName:       BoneName.Thigh2L,
                    allowedEffects: Effect.DevAnything,
                    posApplication: new Vector3(0.25f, TwoThirds, 1f),
                    posPositiveApp: Vector3.one,
                    posNegativeApp: Vector3.one,
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),
                new (
                    name:           Thigh3L,
                    enumName:       BoneName.Thigh3L,
                    allowedEffects: Effect.DevAnything,
                    posApplication: new Vector3(0f, OneThird, OneThird),
                    posPositiveApp: Vector3.one,
                    posNegativeApp: Vector3.one,
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),



            //Kokan,
            //Waist01,
            //Waist02,
            ];

        private readonly Dictionary<BoneConfig, BoneConfig[]> _masterSlaveInitDic = new()
        {
            {
                // Master
                new (
                    name:           Bust,
                    enumName:       BoneName.Bust,
                    allowedEffects: Effect.Rot,
                    sharedEffects:  Effect.Pos,
                    posApplication: Vector3.one,
                    posPositiveApp: Vector3.one,
                    posNegativeApp: Vector3.one,
                    rotApplication: new Vector3(0f, 0f, 1f),
                    sclApplication: Vector3.one
                    ),
                // Slaves
            [

                new (
                    name:           Bust1L,
                    enumName:       BoneName.Bust1L,
                    allowedEffects: Effect.DevAnything,
                    posApplication: Vector3.one,
                    posPositiveApp: new Vector3(TwoThirds, 1f, 1f),
                    posNegativeApp: new Vector3(1f, 1f, 0f),
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),
                //new (
                //    name:           Bust1R,
                //    enumName:       BoneName.Bust1R,
                //    effects:        Effect.DevAnything,
                //    posApplication: Vector3.one,
                //    posPositiveApp: Vector3.one,
                //    posNegativeApp: new Vector3(TwoThirds, 1f, 0f),
                //    rotApplication: Vector3.one,
                //    sclApplication: Vector3.one
                //    ),
            ] },

            {
                // Master
                new (
                    name:           Waist02,
                    enumName:       BoneName.Waist02,
                    allowedEffects: Effect.Pos,
                    sharedEffects:  Effect.None,
                    posApplication: Vector3.one,
                    posPositiveApp: Vector3.one,
                    posNegativeApp: Vector3.one,
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),
                // Slaves
            [
                new (
                    name:           ButtL,
                    enumName:       BoneName.ButtL,
                    allowedEffects: Effect.DevAnything,
                    posApplication: Vector3.one,
                    posPositiveApp: new Vector3(OneThird, TwoThirds, OneThird),
                    posNegativeApp: new Vector3(1f, 1f + OneThird, 1f),
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),
                new (
                    name:           ButtR,
                    enumName:       BoneName.ButtR,
                    allowedEffects: Effect.DevAnything,
                    posApplication: new Vector3(1f, TwoThirds, OneThird),
                    posPositiveApp: new Vector3(OneThird, 1f + OneThird, 1f),
                    posNegativeApp: Vector3.one,
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),
            ] },

            {
                // Master
                new (
                    name:           Spine02,
                    enumName:       BoneName.Spine02,
                    allowedEffects: Effect.DevAnything,
                    sharedEffects:  Effect.Pos,
                    posApplication: Vector3.one,
                    posPositiveApp: Vector3.one,
                    posNegativeApp: Vector3.one,
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),
                // Slaves
            [
                new (
                    name:           Spine01,
                    enumName:       BoneName.Spine01,
                    allowedEffects: Effect.None,
                    posApplication: Vector3.one,
                    posPositiveApp: Vector3.one,
                    posNegativeApp: Vector3.one,
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),
                new (
                    name:           Spine03,
                    enumName:       BoneName.Spine03,
                    allowedEffects: Effect.None,
                    posApplication: Vector3.one,
                    posPositiveApp: Vector3.one,
                    posNegativeApp: Vector3.one,
                    rotApplication: Vector3.one,
                    sclApplication: Vector3.one
                    ),
            ] },
        };

        //// Master comes first
        //private static readonly List<List<string>> _masterSlavesList =
        //[ 
        //    [ Bust, Bust1L, Bust1R ],
        //    [ Waist02, ButtL, ButtR ],
        //];
        //private static readonly List<KeyValuePair<List<string>, Effect>> _tandemList =
        //    [
        //    new ( [Bust, Bust1L, Bust1R], Effect.Pos ),
        //    //[ Waist02, ButtL, ButtR ],
        //    ];
        private static readonly List<BoneName> _bonesWithAnimRot =
        [
            // Animated rotation
            BoneName.Bust1L,
            // Animated rotation
            BoneName.Bust1R, 
            // Animated rotation
            BoneName.Bust,
            //BoneName.Butt,
            //// Static rotation
            //BoneName.Waist02,
            // Animated rotation
            BoneName.ButtL,
            // Animated rotation
            BoneName.ButtR,
            // Animated rotation
            BoneName.Kokan,
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
            { Body.Butt, 
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
            foreach (var single in _singleList)
            {
                if (boneLookup.TryGetValue(single.name, out var boneTransform))
                {
                    _effectsToReturn.Add(single.name);
                    _effectsToUpdate.Add(single.enumName);

                    Transform centeredBoneTransform = null;

                    if (GetCenteredBone(single.enumName, out var centeredBone))
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
                    _effectsToUpdate.Add(kv.Key.enumName);

                    // Init master with slaves together
                    AddToDicTandem(kv.Key, kv.Value, masterTransform, slaveTransforms); // bakedMesh, skinnedMesh);
                }
            }

            void AddToDic(BoneConfig cfg, Transform bone, Transform centerBone) //,  Mesh bakedMesh, SkinnedMeshRenderer skinnedMesh)
            {
                // Perform null checks
                if (_mainDic.ContainsKey(cfg.enumName) || bone == null) return;

                var isAnimRot = _bonesWithAnimRot.Contains(cfg.enumName);

                var boneModifierData = new BoneModifierData();

                _mainDic.Add(cfg.enumName, new BoneData(new MotionModifier(cfg,bone, centerBone, boneModifierData, isAnimRot), boneModifierData));
            }

            void AddToDicTandem(BoneConfig cfgMaster, BoneConfig[] cfgSlaves, Transform tformMaster, Transform[] tformSlaves)
            {
                // Perform null checks
                if (_mainDic.ContainsKey(cfgMaster.enumName) || tformMaster == null) return;

                for (var i = 0;  i < cfgSlaves.Length; i++)
                {
                    if (_mainDic.ContainsKey(cfgSlaves[i].enumName) || tformSlaves[i] == null) return;
                }

                // Add and organize slaves
                var slaveModifiers = new MotionModifierSlave[cfgSlaves.Length];

                // Bit of an oversight with boneModifierData as modifiers got access
                // to it much later in the development, so we add it twice on init.
                var boneModifierDataSlaves = new BoneModifierData[cfgSlaves.Length];

                for (var i = 0; i < cfgSlaves.Length; i++)
                {
                    var isAnimRot = _bonesWithAnimRot.Contains(cfgSlaves[i].enumName);

                    boneModifierDataSlaves[i] = new();

                    slaveModifiers[i] = new MotionModifierSlave(cfgSlaves[i], tformSlaves[i], tformMaster, boneModifierDataSlaves[i], isAnimRot);

                    _mainDic.Add(cfgSlaves[i].enumName, new BoneData(slaveModifiers[i], boneModifierDataSlaves[i]));
                }

                // Add master with slaves
                var isAnimatedBone = _bonesWithAnimRot.Contains(cfgMaster.enumName);

                var masterModifierData = new BoneModifierData();

                _mainDic.Add(cfgMaster.enumName, new BoneData(new MotionModifierMaster(cfgMaster, tformMaster, slaveModifiers, masterModifierData, isAnimatedBone), masterModifierData));

            }
            bool GetCenteredBone(BoneName boneName, out string centeredBone)
            {
                centeredBone = boneName switch
                {
                    BoneName.Bust1L => Bust,
                    BoneName.Bust1R => Bust,
                    BoneName.ButtL => Waist02,
                    BoneName.ButtR => Waist02,
                    _ => "",
                };
                return !centeredBone.IsNullOrEmpty();
            }
        }


        #endregion


        #region Update Cycle


        internal void OnUpdate()
        {
            _updated = false;

            foreach (var key in _effectsToUpdate)
            {
                _mainDic[key].motion.OnUpdate();
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

            if (_mapDic.TryGetValue(bone, out var name))
                return _mainDic[name].modifier;

            return null;
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

        private Body ConvertBoneToBody(BoneName boneName) => boneName switch
        {
            BoneName.Spine01 or BoneName.Spine02 or BoneName.Spine03 => Body.Chest,
            BoneName.Bust or BoneName.Bust1L or BoneName.Bust1R => Body.Breast,
            BoneName.Waist02 or BoneName.ButtL or BoneName.ButtR => Body.Butt,
            BoneName.Thigh1R or BoneName.Thigh2R or BoneName.Thigh3R => Body.Thigh,
            BoneName.Thigh1L or BoneName.Thigh2L or BoneName.Thigh3L => Body.Thigh,
            BoneName.Kokan => Body.Kokan,
            BoneName.Waist01 => Body.Pelvis,
            _ => throw new NotImplementedException(boneName.ToString())
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


        internal enum BoneName
        {
            // --- Chest ---
            Spine03,
            Spine02,
            Spine01,

            // --- Breast ---
            Bust,
            Bust1L,
            Bust1R,

            // --- Pelvis ---
            Waist01,
            Waist02,
            Kokan,
            ButtL,
            ButtR,

            // --- Thighs ---
            Thigh1R,
            Thigh2R,
            Thigh3R,

            Thigh1L,
            Thigh2L,
            Thigh3L,
        }

        //internal enum Bone


        private readonly struct BodyPartMeasurement(string[] bonesToMeasure, float defaultMass)
        {
            internal readonly string[] bonesToMeasure = bonesToMeasure;
            internal readonly float defaultMass = defaultMass;
        }

        // Very rare access, no point in struct.
        internal class BoneConfig
        {
            internal BoneConfig(string name, BoneName enumName, Effect allowedEffects, Vector3 posApplication, Vector3 posPositiveApp, Vector3 posNegativeApp, Vector3 rotApplication, Vector3 sclApplication, Effect sharedEffects = Effect.None)
            {
                this.name = name;

                this.posApplication = posApplication;
                this.posPositiveApp = posPositiveApp;
                this.posNegativeApp = posNegativeApp;

                this.rotApplication = rotApplication;
                this.sclApplication = sclApplication;
                this.allowedEffects = allowedEffects;
                this.sharedEffects = sharedEffects;
                this.enumName = enumName;
            }

            internal readonly string name;
            internal readonly BoneName enumName;
            internal readonly Effect allowedEffects;
            internal readonly Effect sharedEffects;
            internal readonly Vector3 posApplication;
            internal readonly Vector3 posPositiveApp;
            internal readonly Vector3 posNegativeApp;
            internal readonly Vector3 rotApplication;
            internal readonly Vector3 sclApplication;
        }


        #endregion
    }
}
