using ADV.Commands.Base;
using KKAPI;
using KKAPI.Chara;
using System.Collections.Generic;
#if VR
using KK.RootMotion;
using KK.RootMotion.FinalIK;
#elif NOVR
using RootMotion;
using RootMotion.FinalIK;
#endif

namespace IKNoise
{
    internal class IKNoiseCharaController : CharaCustomFunctionController
    {
        private static List<IKNoiseCharaController> _instances = [];
        private IKNoiseEffector _effector;


        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {

        }

        protected override void OnReload(GameMode currentGameMode, bool maintainState)
        {
            base.OnReload(currentGameMode, maintainState);

            OnSettingChangedIntern();
        }

        private void LateUpdate()
        {
            _effector?.OnLateUpdate();
        }

        internal static void OnSettingChanged()
        {
            foreach (var inst in _instances)
                inst.OnSettingChangedIntern();
        }

        private void OnSettingChangedIntern()
        {
            var sex = ChaControl.sex;

            enabled = (IKNoisePlugin.Enable.Value & (Sex)(sex + 1)) != 0;

            if (enabled)
            {
                _effector ??= new(ChaControl);
                _effector.OnSettingChanged(sex);
            }
        }

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

    }
}
