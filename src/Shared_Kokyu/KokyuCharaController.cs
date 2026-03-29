using ExtensibleSaveFormat;
using KKABMX.Core;
using KKAPI;
using KKAPI.Chara;
using KKAPI.MainGame;
using KKAPI.Studio;
using KKAPI.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace Kokyu
{
    public class KokyuCharaController : CharaCustomFunctionController
    {
        private static readonly List<KokyuCharaController> _instances = [];

        private KokyuEffector _effector;
        private float _breathMagFactorData = 1f;
        // Stores value from Setting && ExData.
        private bool _enabled;
        private bool _enabledSetting;
        private bool _enabledData;
        private Pattern _pattern;
        private float _breathSpeed;
        private float _rotationFactorData;
        private AnimTracker _animTracker;
        private BoneController _boneCtr;


        #region ExtendedData Properties 


        public bool BreathEnabled
        {
            get => _enabledData;
            set
            {
                _enabledData = value;
                OnSettingChangedIntern();
            }
        }

        public float BreathMagnitude
        {
            get => _breathMagFactorData;
            set
            {
                _breathMagFactorData = value;
                _effector?.UpdateBreathExData(_breathMagFactorData, _breathSpeed, _rotationFactorData);
            }
        }

        // Property for maker to test magnitude with various patterns.
        public Pattern BreathPattern
        {
            get => _pattern;
            set
            {
                _pattern = value;
                _effector?.UpdatePattern(value);
            }
        }

        public float BreathSpeed
        {
            get => _breathSpeed;
            set
            {
                _breathSpeed = value;
                _effector?.UpdateBreathExData(_breathMagFactorData, _breathSpeed, _rotationFactorData);
            }
        }

        public float BreathRotation
        {
            get => _rotationFactorData;
            set
            {
                _rotationFactorData = value;
                _effector?.UpdateBreathExData(_breathMagFactorData, _breathSpeed, _rotationFactorData);

            }
        }


        #endregion


        #region Overrides


        protected override void Awake()
        {
            base.Awake();

            _instances.Add(this);

            CurrentCoordinate.Subscribe(_ => StartCoroutine(StartCo()));
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            _instances.Remove(this);
        }

        protected override void OnReload(GameMode currentGameMode, bool maintainState)
        {
            base.OnReload(currentGameMode, maintainState);
            BreathPattern = 0;

            var data = GetExtendedData();

            if (data != null)
            {
                if (data.data.TryGetValue("BreathMagnitude", out var value))
                    BreathMagnitude = (float)value;
                else
                    BreathMagnitude = 1f;


                if (data.data.TryGetValue("BreathEnabled", out var value1))
                    BreathEnabled = (bool)value1;
                else
                    BreathEnabled = true;


                if (data.data.TryGetValue("BreathSpeed", out var value2))
                    BreathSpeed = (float)value2;
                else
                    BreathSpeed = 1f;


                if (data.data.TryGetValue("BreathRotation", out var value3))
                    BreathRotation = (float)value3;
                else
                    BreathRotation = 1f;
            }
            else
            {
                BreathMagnitude = 1f;
                BreathEnabled = true;
                BreathSpeed = 1f;
                BreathRotation = 1f;
            }

            var stateMask = KokyuPlugin.SettingEnableMask.Value;

            var setting = ChaControl.sex == 0 ?
                (stateMask & KokyuPlugin.Sex.Male) != 0 :
                (stateMask & KokyuPlugin.Sex.Female) != 0;

            _enabledSetting = setting && _enabledData;

            if (_enabledSetting)
            {
                StopAllCoroutines();
                StartCoroutine(StartCo());
            }
        }

        protected override void Update()
        {
            base.Update();

            if (_enabled)
            {
                _effector?.OnUpdate();
            }
        }

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            var data = new PluginData
            {
                version = 1
            };

            data.data.Add("BreathMagnitude", BreathMagnitude);
            data.data.Add("BreathEnabled", BreathEnabled);
            data.data.Add("BreathSpeed", BreathSpeed);
            data.data.Add("BreathRotation", BreathRotation);

            SetExtendedData(data);

            _effector?.OnRestart();
        }

        //protected override void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate, bool maintainState)
        //{
        //    base.OnCoordinateBeingLoaded(coordinate, maintainState);

        //    if (maintainState) return;

        //    StopAllCoroutines();
        //    StartCoroutine(StartCo());
        //}


        #endregion


        private void OnSettingChangedIntern()
        {
            var stateMask = KokyuPlugin.SettingEnableMask.Value;

            _enabledSetting = ChaControl.sex == 0 ?
                (stateMask & KokyuPlugin.Sex.Male) != 0 :
                (stateMask & KokyuPlugin.Sex.Female) != 0;

            _enabled = _enabledSetting && _enabledData;

            _effector?.UpdateBreathExData(_breathMagFactorData, _breathSpeed, _rotationFactorData);
            _effector?.OnRestart();
        }

        

        internal static void OnSettingChanged()
        {
            foreach (var inst in  _instances)
            {
                inst.OnSettingChangedIntern();
            }
        }

        internal void OnAnimatorStateChange(Pattern pattern)
        {
            BreathPattern = pattern;
        }



        // Can't be ahead of the ABMX.
        private IEnumerator StartCo(int frameWait = 1)
        {
#if DEBUG
            KokyuPlugin.Logger.LogDebug($"{ChaControl.name}: StartCo[Start]");
#endif
            // ABMX has identical condition, here we add extra frame on top.
            do yield return CoroutineUtils.WaitForEndOfFrame;
            while (ChaControl.animBody == null || frameWait-- > 0);


            if (_boneCtr == null)
            {
                _boneCtr = ChaControl.transform.GetComponent<BoneController>();
                if (_boneCtr == null)
                {
                    KokyuPlugin.Logger.LogWarning($"{ChaControl.name}'s ABMX component is absent. Can't start.");
                    yield break;
                }
            }

            if (_effector == null)
            {
                _effector = new KokyuEffector(ChaControl, _boneCtr);
                _boneCtr.AddBoneEffect(_effector);
            }
            _effector.UpdateBreathExData(_breathMagFactorData, _breathSpeed, _rotationFactorData);
            _effector.OnReload(this, _boneCtr);

            if (!StudioAPI.InsideStudio)
            {
                _animTracker ??= new(this);
                _animTracker.OnReload(ChaControl);
            }
#if DEBUG
            KokyuPlugin.Logger.LogDebug($"{ChaControl.name}: StartCo[Finish]");
#endif
        }

        /// <summary>
        /// Disables breast movements when something wants to interfere.
        /// </summary>
        internal void UpdateCaress(bool bustL, bool bustR)
        {
#if DEBUG
            KokyuPlugin.Logger.LogDebug($"[{ChaControl.name} - UpdateCaress] bustL[{bustL}] bustR[{bustR}]");
#endif
            if (_effector == null) return;

            if (_boneCtr == null)
            {
                _boneCtr = ChaControl.transform.GetComponent<BoneController>();
                if (_boneCtr == null) return;
            }
            
            if (bustL)
            {
                var mod = _boneCtr.GetModifier("cf_j_bust01_L", BoneLocation.BodyTop);

                if (mod != null)
                    _boneCtr.RemoveModifier(mod);
            }
            if (bustR)
            {
                var mod = _boneCtr.GetModifier("cf_j_bust01_R", BoneLocation.BodyTop);

                if (mod != null)
                    _boneCtr.RemoveModifier(mod);
            }
            _effector.OnUpdateCaress(_boneCtr, bustL, bustR);
        }

        public enum Pattern
        {
            Idle,
            ActivityLight,
            ActivityModerate,
            ActivityHeavy,
            Anxiety,
            Panic,
            Exhaustion,
            Sleep,
            PhysiologicalSigh,
        }
    }
}
