using ADV.Commands.Base;
using KKAPI;
using KKAPI.Chara;
#if VR
using KK.RootMotion;
using KK.RootMotion.FinalIK;
#elif NOVR
using RootMotion;
using RootMotion.FinalIK;
#endif
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using static ActionGame.InformationUI;

namespace IKPlugin
{
    internal class IKPluginCharaController : CharaCustomFunctionController
    {
        private IKPluginEffector _effector;
        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {

        }

        protected override void OnReload(GameMode currentGameMode, bool maintainState)
        {
            base.OnReload(currentGameMode, maintainState);

            _effector?.OnDestroy();
            _effector = null;

            if (ChaControl.sex == 0)
            {
                enabled = false;
                return;
            }
        }

        public void DevStart()
        {
            _effector = new(ChaControl);
        }

        private void LateUpdate()
        {

        }

        public void DevStop()
        {
        }

        internal void OnAnimatorStateChange()
        {
            
        }

    }
}
