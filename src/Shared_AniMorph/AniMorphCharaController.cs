using KKABMX.Core;
using KKAPI;
using KKAPI.Chara;
using KKAPI.MainGame;
using KKAPI.Studio;
using KKAPI.Utilities;
using Manager;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace AniMorph
{
    internal class AniMorphCharaController : CharaCustomFunctionController
    {
        public AniMorphEffector BoneEffector => _boneEffector;
        private static readonly List<AniMorphCharaController> _instances = [];


        private AniMorphEffector _boneEffector;

#if DEBUG

        private Vector3 _devRotEuler = new Vector3(0f, 180f, 0f);
        private Vector3 _devRotOnceEuler = new Vector3(0f, 360f, 0f);
        private float _devRotOnceRate = 0.5f;

        //public bool DevRotate
        //{
        //    get => _verbotenRotate;
        //    set
        //    {
        //        if (value)
        //        {
        //            DevRotateOnce = false;
        //        }
        //        _verbotenRotate = value;
        //    }
        //}
        public bool DevRotateOnce
        {
            get => _verbotenRotateOnce;
            set
            {
                if (value)
                {
                    //DevRotate = false;
                    _verbotenRotateOnceAmount = 0f;
                    _verbotenRotateOnceStartRot = ChaControl.transform.rotation;
                }
                _verbotenRotateOnce = value;
            }
        }
        private bool _follow;
        private bool Follow
        {
            get => _follow;
            set  
            { 
                _follow = value; 
                _prevPosition = _bust.position; 
                _prevRotation = _bust.rotation;
            }
        }

        private Transform _bust;
        private Transform _camera;
        private Vector3 _prevPosition;
        private Quaternion _prevRotation;

        //private bool _verbotenRotate;
        private bool _verbotenRotateOnce;
        private float _verbotenRotateOnceAmount;
        private Quaternion _verbotenRotateOnceStartRot;

        private Smoothing DevSmoothing {  get; set; }
        private enum Smoothing
        {
            Linear,
            Power,
            Smooth,
            Smoother,
            Sine
        }
#endif

        protected override void Awake()
        {
            base.Awake();
            _instances.Add(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _instances.Remove(this);
        }

        private void OnDisable()
        {
            _boneEffector?.OnDisable();
        }

        private bool IsProperScene
        {
            get
            {
                var scene =
#if KK
                 Scene.Instance.AddSceneName;
#elif KKS
                 Scene.AddSceneName;
#endif
                return StudioAPI.InsideStudio || scene.Equals("HProc");
            }
        }

        internal void HandleEnable(bool forceStart = false)
        {
            var setting = AniMorphPlugin.Enable.Value;

            var wasEnabled = enabled;

            // Enable if chara is male and male setting is selected, same for female.
            enabled = 
                (ChaControl.sex == 0 && (setting & AniMorphPlugin.Gender.Male) != 0) 
                || 
                (ChaControl.sex == 1 && (setting & AniMorphPlugin.Gender.Female) != 0);

            var boneController = ChaControl.GetComponent<BoneController>();
            if (boneController == null)
            {
                throw new Exception($"No ABMX BoneController on {ChaControl.name}");
            }


            if (forceStart || wasEnabled != enabled)
            {
                StopAllCoroutines();

                if (enabled)
                {
                    if (forceStart) RemoveBoneEffector();

                    if (_boneEffector == null)
                    {
                        StartCoroutine(StartCo(boneController));
                    }
                }
                else
                {
                    RemoveBoneEffector();
                }
            }
            void RemoveBoneEffector()
            {
                if (_boneEffector == null) return;

                boneController.RemoveBoneEffect(_boneEffector);
                _boneEffector = null;
                boneController.NeedsBaselineUpdate = true;
            }

        }

        private Quaternion ratRotation = Quaternion.Euler(0f, 10f, 0f);
        private bool rotateRat;
        private Transform rat;
        private Transform alpha;
        private Transform omega;
        private void SpawnRat()
        {
            if (rat == null)
            {
                rat = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                rat.gameObject.name = "Rat";
                rat.position = new Vector3(3f, 0f, 0f);
            }
            if (omega == null)
            {
                omega = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                omega.gameObject.name = "Omega";
                omega.position = new Vector3(1.5f, 0f, 0f);
            }
            if (alpha == null)
            {
                alpha = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                alpha.gameObject.name = "Alpha";
                alpha.position = new Vector3(0, 0f, 0f);
            }
            omega.parent = alpha;
            rat.parent = omega;

            var db = omega.gameObject.AddComponent<DynamicBone>();
            db.m_Root = omega;
            // lul
            db.m_notRolls = [];
            db.SetupParticles();
        }

        private IEnumerator StartCo(BoneController boneController)
        {
            // Wait for loading-scene-lag to avoid delta time spikes and chara teleportation.
            // Requires atleast a frame wait because we are ahead of ABMX init, 3 for a better measurement.
            var count = 3;
            var endOfFrame = CoroutineUtils.WaitForEndOfFrame;

            // Don't filter lags (unless huge) in the studio or if setting.
            var settingValue = AniMorphPlugin.FilterDeltaTime.Value;
            var bigDeltaTime = 
                StudioAPI.InsideStudio ? 1f 
                : (settingValue == AniMorphPlugin.FilterDeltaTimeKind.Enable || settingValue == AniMorphPlugin.FilterDeltaTimeKind.OnlyInGame) ? (1f / 30f) 
                : 1f;

            while (count-- > 0 || Time.deltaTime > bigDeltaTime) // || count++ < 1000)
            {
#if DEBUG
                AniMorphPlugin.Logger.LogDebug($"StartCo:deltaTime[{Time.deltaTime:F3}]");
#endif
                yield return endOfFrame;
            }
            _boneEffector = new AniMorphEffector(ChaControl);
            boneController.AddBoneEffect(_boneEffector);
        }
        private float _devMoveRadius = 1f / 3f;
        private float _devMoveSpeed = 2.5f;
        private Vector3 _devMove = new(1f, 1f / 3f, 1f / 3f);

        private Vector3 _devMoveStartPos;
        private float _devMoveAngle = 0f;   
        private bool _verbotenMove;
        private bool DevMove
        {
            get => _verbotenMove;
            set
            {
                _verbotenMove = value;
                if (value)
                {
                    _devMoveStartPos = transform.position;
                }
                else
                {
                    transform.position = _devMoveStartPos;
                }
            }
        }

        protected override void Update()
        {
            base.Update();
#if DEBUG
            //if (DevRotate) ChaControl.transform.rotation *= Quaternion.Euler(_devRotEuler * Time.deltaTime);

            if (DevRotateOnce)
            {
                _verbotenRotateOnceAmount += Time.deltaTime * _devRotOnceRate;

                if (_verbotenRotateOnceAmount >= 1f)
                {
                    ChaControl.transform.rotation = _verbotenRotateOnceStartRot;
                    DevRotateOnce = false;
                }
                else
                {
                    var t = _verbotenRotateOnceAmount;
                    var amount = DevSmoothing switch
                    {
                        Smoothing.Linear => t,
                        Smoothing.Power => t * t,
                        Smoothing.Smooth => t * t * (3f - 2f * t),
                        Smoothing.Smoother => t * t * t * (10f - 15f * t + 6f * t * t),
                        Smoothing.Sine => Mathf.Sin(t * (Mathf.PI * 0.5f)),
                        _ => t
                    };
                    ChaControl.transform.rotation = Quaternion.Euler(_devRotOnceEuler * amount) * _verbotenRotateOnceStartRot;
                }
            }
            if (_follow)
            {
                //_camera.rotation *= Quaternion.Inverse(_prevRotation) * _bust.rotation;
                _camera.position += _bust.position - _prevPosition;

                _prevPosition = _bust.position;
                _prevRotation = _bust.rotation;
            }

            if (_verbotenMove)
            {
                var degLimit = 360f * Mathf.Deg2Rad;
                _devMoveAngle += _devMoveSpeed * Time.deltaTime;
                if (_devMoveAngle > degLimit)
                {
                    _devMoveAngle = _devMoveAngle - degLimit;
                }

                var move = _devMove;
                var x = _devMoveStartPos.x + Mathf.Cos(_devMoveAngle) * _devMoveRadius * move.x;
                var sine = Mathf.Sin(_devMoveAngle);
                var y = _devMoveStartPos.y + sine * _devMoveRadius * move.y;
                var z = _devMoveStartPos.z + sine * _devMoveRadius * move.z;

                transform.position = new Vector3(x, y, z);
            }

            if (rotateRat)
            {
                alpha.rotation *= ratRotation;
            }
#endif

            _boneEffector?.OnUpdate();
        }

        public static void OnSettingChanged()
        {
            foreach (var instance in _instances)
            {
                instance.HandleEnable();
                instance._boneEffector?.OnSettingChanged();
            }
        }

        protected override void OnReload(GameMode currentGameMode)
        {
#if DEBUG
            AniMorphPlugin.Logger.LogDebug($"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}:Pop");
#endif
            if (ChaControl != null && IsProperScene)
            {
                HandleEnable(forceStart: true);
            }
        }

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {

        }

        internal static void OnSetClothesState(ChaControl chara)
        {
            if (chara == null) return;

            foreach (var instance in _instances)
            {
                if (instance.ChaControl == chara)
                {
                    instance._boneEffector?.OnSetClothesState(chara);
                    return;
                }
            }
        }

    }
}
