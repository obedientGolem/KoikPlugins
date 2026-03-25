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
        private const string Thigh1R = "cf_s_thigh01_R";
        private const string Thigh2R = "cf_s_thigh02_R";
        private const string Thigh3R = "cf_s_thigh03_R";

        private const string Bust    = "cf_d_bust00";
        private const string Bust1L  = "cf_d_bust01_L";
        private const string Bust1R  = "cf_d_bust01_R";

        // Reset by the HScene (true, true, true)
        private const string ButtL   = "cf_s_siri_L";
        // Reset by the HScene (true, true, true)
        private const string ButtR   = "cf_s_siri_R";

        private const string Kokan   = "cf_j_kokan";

        // Position reset by the HScene (false, true, true)
        private const string Waist01 = "cf_s_waist01";

        // Position reset by the HScene (false, false, true)
        private const string Waist02 = "cf_s_waist02";

        private readonly ChaControl _chara;

        private readonly Dictionary<BoneName, BoneData> _mainDic = [];
        private static readonly Dictionary<string, BoneName> _mapDic = new(StringComparer.Ordinal)
        {
            { Bust,    BoneName.Bust },
            { Bust1L,  BoneName.Bust1L },
            { Bust1R,  BoneName.Bust1R },
            { ButtL,   BoneName.ButtL },
            { ButtR,   BoneName.ButtR },
            { Thigh1R, BoneName.Thigh1R },
            { Thigh2R, BoneName.Thigh2R },
            { Thigh3R, BoneName.Thigh3R },
            { Kokan,   BoneName.Kokan },
            { Waist01, BoneName.Waist01 },
            { Waist02, BoneName.Waist02 },

        };

        private readonly List<string> _returnToABMX = [];
        private readonly List<BoneName> _updateList = [];
        private static readonly string[] _singleList =
            [
#if DEBUG
            //Thigh1R,
            //Thigh2R,
            //Thigh3R,
#endif
            Kokan,
            Waist01,
            //Waist02,
        ];

        // Master comes first
        private static readonly List<List<string>> _tandemList =
        [ 
            [ Bust, Bust1L, Bust1R ],
            [ Waist02, ButtL, ButtR ],
        ];
        //private static readonly List<KeyValuePair<List<string>, Effect>> _tandemList =
        //    [
        //    new ( [Bust, Bust1L, Bust1R], Effect.Pos ),
        //    //[ Waist02, ButtL, ButtR ],
        //    ];
        private static readonly List<BoneName> _bonesWithDynamicRotation =
        [
            // Animated rotation
            BoneName.Bust1L,
            // Animated rotation
            BoneName.Bust1R, 
            // Animated rotation
            BoneName.Bust,
            // BoneName.Butt,
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
            

        #region Initialization


        internal AniMorphEffector(ChaControl chara)
        {
            _chara = chara;

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
                if (boneLookup.TryGetValue(single, out var boneTransform)
                    && _mapDic.TryGetValue(single, out var boneEnum))
                {
                    _returnToABMX.Add(single);
                    _updateList.Add(boneEnum);
                    Transform centeredBoneTransform = null;

                    if (GetCenteredBone(boneEnum, out var centeredBone))
                    {
                        boneLookup.TryGetValue(centeredBone, out centeredBoneTransform);
                    }
                    AddToDic(boneEnum, boneTransform, centeredBoneTransform, null, null); // bakedMesh, skinnedMesh);
                }
            }

            // Iterate through tandems
            foreach (var boneNames in _tandemList)
            {
                // Skip if master without slaves
                if (boneNames.Count < 2) continue;

                // Prepare arrays for init under master
                var slaveEnums = new BoneName[boneNames.Count - 1];
                var slaveTransforms = new Transform[boneNames.Count - 1];
                for (var i = 1; i < boneNames.Count; i++)
                {
                    var slave = boneNames[i];
                    if (boneLookup.TryGetValue(slave, out var slaveTransform)
                        && _mapDic.TryGetValue(slave, out var slaveEnum))
                    {
                        // Slaves don't get own update
                        _returnToABMX.Add(slave);
                        // Fill in arrays for master
                        slaveEnums[i - 1] = slaveEnum;
                        slaveTransforms[i - 1] = slaveTransform;
                    }
                }

                var master = boneNames[0];
                if (boneLookup.TryGetValue(master, out var masterTransform)
                    && _mapDic.TryGetValue(master, out var masterEnum))
                {
                    // Some masters track one bone but apply to another.
                    _returnToABMX.Add(master);
                    // Master updates his slaves
                    _updateList.Add(masterEnum);

                    // Init master with slaves together
                    AddToDicTandem(masterEnum, slaveEnums, masterTransform, slaveTransforms, null, null); // bakedMesh, skinnedMesh);
                }
            }

            void AddToDic(BoneName enumName, Transform boneTransform, Transform centeredBone, Mesh bakedMesh, SkinnedMeshRenderer skinnedMesh)
            {
                // Perform null checks
                if (_mainDic.ContainsKey(enumName) || boneTransform == null) return;

                var dynamicRotation = _bonesWithDynamicRotation.Contains(enumName);

                var boneModifierData = new BoneModifierData();
                _mainDic.Add(enumName, new BoneData(new MotionModifier(boneTransform, centeredBone, bakedMesh, skinnedMesh, boneModifierData, dynamicRotation), boneModifierData));
            }

            void AddToDicTandem(BoneName master, BoneName[] slaves, Transform masterTransform, Transform[] slaveTransforms, Mesh bakedMesh, SkinnedMeshRenderer skinnedMesh)
            {
                // Perform null checks
                if (_mainDic.ContainsKey(master) || masterTransform == null) return;
                for (var i = 0;  i < slaves.Length; i++)
                {
                    if (_mainDic.ContainsKey(slaves[i]) || slaveTransforms[i] == null) return;
                }

                // Add and organize slaves
                var boneModifierSlaves = new MotionModifierSlave[slaves.Length];
                // Bit of an oversight with boneModifierData as modifiers got access
                // to it much later in the development, so we add it twice on init.
                var boneModifierDataSlaves = new BoneModifierData[slaves.Length];
                for (var i = 0; i < slaves.Length; i++)
                {

                    var animatedBoneSlave = _bonesWithDynamicRotation.Contains(slaves[i]);
                    boneModifierDataSlaves[i] = new();
                    boneModifierSlaves[i] = new MotionModifierSlave(slaveTransforms[i], masterTransform, bakedMesh, skinnedMesh, boneModifierDataSlaves[i], animatedBoneSlave);
                    _mainDic.Add(slaves[i], new BoneData(boneModifierSlaves[i], boneModifierDataSlaves[i]));
                }

                // Add master with slaves
                var animatedBoneMaster = _bonesWithDynamicRotation.Contains(master);
                var boneModifierDataMaster = new BoneModifierData();
                _mainDic.Add(master, new BoneData(new MotionModifierMaster(masterTransform, boneModifierSlaves, boneModifierDataMaster, animatedBoneMaster), boneModifierDataMaster));

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

            foreach (var key in _updateList)
            {
                _mainDic[key].motion.OnUpdate();
            }

        }

        private void UpdateModifiers()
        {
            // For some reason it doesn't work on Update(),
            // can't remember why exactly though.
            var deltaTime = Time.deltaTime;
            if (deltaTime == 0f) return;

            // Opt for lesser evil during the lag spike,
            if (_filterDeltaTime && deltaTime > (1f / 15f)) deltaTime = (1f / 15f);

            var invDelta = (1f / deltaTime);
            foreach (var key in _updateList)
            {
                _mainDic[key].motion.UpdateModifiers(deltaTime, invDelta);
            }
        }


        #endregion


        #region Overrides


        public override IEnumerable<string> GetAffectedBones(BoneController origin) => _returnToABMX;

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
                var bodyPart = GetBodyPart(keyValuePair.Key);
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
                var bodyPart = GetBodyPart(keyValuePair.Key);
                keyValuePair.Value.motion.OnSetClothesState(bodyPart, chara);
            }
        }

        private Body GetBodyPart(BoneName boneName) => boneName switch
        {
            BoneName.Bust or BoneName.Bust1L or BoneName.Bust1R => Body.Breast,
            BoneName.Waist02 or BoneName.ButtL or BoneName.ButtR => Body.Butt,
            BoneName.Thigh1R or BoneName.Thigh2R or BoneName.Thigh3R => Body.Thigh,
            BoneName.Kokan => Body.Kokan,
            BoneName.Waist01 => Body.Waist01,
            _ => throw new NotImplementedException(boneName.ToString())
        };

        internal void OnChangeAnimator()
        {
            foreach (var entry in _updateList)
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


        private enum BoneName
        {
            None,
            Bust1L,
            Bust1R,
            Bust,
           // Butt,
            ButtL,
            ButtR,
            Thigh1R,
            Thigh2R,
            Thigh3R,
            Kokan,
            Waist01,
            Waist02,
        }

        //internal enum Bone


        private readonly struct BodyPartMeasurement(string[] bonesToMeasure, float defaultMass)
        {
            internal readonly string[] bonesToMeasure = bonesToMeasure;
            internal readonly float defaultMass = defaultMass;
        }


        #endregion
    }
}
