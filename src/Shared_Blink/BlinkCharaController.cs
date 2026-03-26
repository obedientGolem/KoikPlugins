using ExtensibleSaveFormat;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Blink
{
    public class BlinkCharaController : CharaCustomFunctionController
    {
        private static readonly List<BlinkCharaController> _instances = [];

        private BlinkEffector effector;

        private float _blinkLength;
        private float _blinkFlurry;
        private float _eyeOpenLvl;

        public float BlinkLength
        {
            get => _blinkLength;
            set
            {
                _blinkLength = value;
                effector?.OnReload(this);
            }
        }

        public float BlinkFlurry
        {
            get => _blinkFlurry;
            set
            {
                _blinkFlurry = value;
                effector?.OnReload(this);
            }
        }

        public float EyeOpenLvl
        {
            get => _eyeOpenLvl;
            set
            {
                _eyeOpenLvl = value;
                effector?.OnReload(this);
            }
        }


        #region Overrides


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

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            var data = new PluginData
            {
                version = 1
            };

            data.data.Add("BlinkLength", BlinkLength);
            data.data.Add("BlinkFlurry", BlinkFlurry);
            data.data.Add("EyeOpenLvl", EyeOpenLvl);

            SetExtendedData(data);
        }

        protected override void OnReload(GameMode currentGameMode, bool maintainState)
        {
            base.OnReload(currentGameMode, maintainState);

            var data = GetExtendedData();

            if (data != null)
            {
                if (data.data.TryGetValue("BlinkLength", out var value))
                    BlinkLength = (float)value;
                else
                    BlinkLength = 0.3f;


                if (data.data.TryGetValue("BlinkFlurry", out var value1))
                    BlinkFlurry = (float)value1;
                else
                    BlinkFlurry = 0.25f;


                if (data.data.TryGetValue("EyeOpenLvl", out var value2))
                    EyeOpenLvl = (float)value2;
                else
                    EyeOpenLvl = 0.8f;
            }
            else
            {
                BlinkLength = 0.3f;
                BlinkFlurry = 0.25f;
                EyeOpenLvl = 0.8f;
            }


            effector ??= BlinkHooks.OnReload(ChaControl);

            StartCoroutine(LateReloadCo());
        }


        #endregion


        private IEnumerator LateReloadCo()
        {
            yield return null;
            foreach (var inst in _instances)
            {
                inst.OnLateReload();
            }
        }

        private void OnLateReload()
        {
            effector = BlinkHooks.OnReload(ChaControl);
            effector?.OnReload(this);
        }

        internal static void TryEnable(bool enable)
        {
            if (!enable) return;

            foreach (var inst in _instances)
            {
                inst.OnLateReload();
            }
        }

        public void Blink()
        {
            effector?.Blink();
        }

    }
}
