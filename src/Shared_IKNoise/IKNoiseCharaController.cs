#if VR_DIR
using KK.RootMotion;
using KK.RootMotion.FinalIK;
#else
using RootMotion;
using RootMotion.FinalIK;
#endif
using KKAPI;
using KKAPI.Chara;
using System.Collections.Generic;
using KKAPI.MainGame;
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using KKAPI.Studio;
using System;


namespace IKNoise
{
    internal class IKNoiseCharaController : CharaCustomFunctionController
    {
        private static readonly List<IKNoiseCharaController> _instances = [];
        internal IKNoiseEffector effector;

        private bool IsProperScene(out Scene scene)
        {
                var setting = IKNoisePlugin.EnableScene.Value;

                var actScene =
#if KK
                Manager.Game.Instance.actScene;
#else
                ActionScene.instance;
#endif
            if ((setting & Scene.Adv) != 0 && actScene != null && actScene.AdvScene != null && actScene.AdvScene.isActiveAndEnabled)
            {
                scene = Scene.Adv;
                return true;
            }

            if ((setting & Scene.Talk) != 0 && IKNoisePlugin.IsSceneLoaded("Talk"))
            {
                scene = Scene.Talk;
                return true;
            }

            if ((setting & Scene.HScene) != 0 && IKNoisePlugin.IsSceneLoaded("HProc"))
            {
                scene = Scene.HScene; 
                return true;
            }
            scene = Scene.None; 
            return false;            
        }

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
            effector?.OnLateUpdate();
        }

        internal static void OnSettingChanged()
        {
            foreach (var inst in _instances)
                inst.OnSettingChangedIntern();
        }

        private void OnSettingChangedIntern()
        {
            var sex = ChaControl.sex;

            enabled = (IKNoisePlugin.EnableSex.Value & (Sex)(sex + 1)) != 0;

            if (!enabled || !IsProperScene(out var scene))
            {
                effector = null;
                return;
            }

            if (effector == null)
            {
                var anim = ChaControl.animBody;
                var fbbik = anim != null ? anim.GetComponent<FullBodyBipedIK>() : null;

                if (anim != null && fbbik != null)
                {
                    effector = new(anim, fbbik);
                }
                else
                {
                    StopAllCoroutines();
                    StartCoroutine(StartCo());
                }
            }

            effector?.OnSettingChanged(sex, GetSceneAmplFactor(scene));

            static float GetSceneAmplFactor(Scene scene)
            {
                return scene switch
                {
                    Scene.Adv => IKNoisePlugin.AdvSceneFactor.Value,
                    Scene.Talk => IKNoisePlugin.TalkSceneFactor.Value,
                    Scene.HScene => IKNoisePlugin.HSceneFactor.Value,
                    _ => throw new NotImplementedException(nameof(scene))
                };
            }
        }

        private IEnumerator StartCo()
        {
            var i = 0;

            var wait = new WaitForSeconds(1f);

            while (ChaControl.animBody == null || ChaControl.animBody.GetComponent<FullBodyBipedIK>() == null)
            {
                if (i++ > 30) yield break;

                yield return wait;
            }
            OnSettingChangedIntern();
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
