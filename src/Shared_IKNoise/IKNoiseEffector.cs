using HarmonyLib;
using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace IKNoise
{
    internal class IKNoiseEffector
    {
        private readonly Animator _anim;

        private readonly IKNoiseModifier[] _modifiers = new IKNoiseModifier[3];
        internal IKNoiseEffector(ChaControl chara)
        {
            if (chara == null) throw new ArgumentNullException(nameof(ChaControl));

            if (chara.animBody == null) throw new ArgumentNullException(nameof(Animator));

            var fbbik = chara.objAnim.GetComponent<FullBodyBipedIK>();

            if (fbbik == null) throw new ArgumentNullException(nameof(FullBodyBipedIK));

            _anim = chara.animBody;

            _modifiers[0] = new IKNoiseModifier([fbbik.solver.effectors[1], fbbik.solver.effectors[2]]);
            _modifiers[1] = new IKNoiseModifier([fbbik.solver.effectors[0]]);
            _modifiers[2] = new IKNoiseModifier([fbbik.solver.effectors[3], fbbik.solver.effectors[4]]);

        }

        internal void OnLateUpdate()
        {
            var dt = Time.deltaTime;

            if (dt == 0f) return;

            var dtInv = 1f / dt;

            var animState = _anim.GetCurrentAnimatorStateInfo(0);

            var animLen = animState.length;
            var animLenInv = animLen == 0f ? 1f : (1f / animLen) * (1f / 2.25f);

            foreach (var modifier in _modifiers)
            {
                modifier.UpdateModifier(dt, dtInv, animLenInv);
            }
        }

        internal void OnSettingChanged(int sex)
        {
            for (var i = 0;  i < _modifiers.Length; i++)
            {
                _modifiers[i].OnSettingChanged(IKNoisePlugin.ConfigDic[(Body)i], sex);
            }
        }
    }
}
