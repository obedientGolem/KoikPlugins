#if VR_DIR
using KK.RootMotion.FinalIK;
#else
using RootMotion.FinalIK;
#endif
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace IKNoise
{
    internal class IKNoiseModifierLimb(IKEffector[] effectors, IKNoiseModifier parent, IKNoiseEffector.Cfg cfg, IKNoiseEffector.BaseCfg baseCfg) 
        : IKNoiseModifier(effectors, cfg, baseCfg)
    {
        private readonly IKNoiseModifier _parent = parent;


        internal override void UpdateModifier(float dt, float dtInv, float animLenInv)
        {
            var velLen = GetVelocityLen(dt, dtInv);
            //UpdateTorque(dt, dtInv);

            var velLenFactor = velLen * (1f / 0.03f);
            var freq = _baseFreq + (velLenFactor * _freqVelFactor) + (animLenInv * _freqAnimFactor);
            var ampl = _baseAmpl + (velLenFactor * _amplAnimFactor) + (animLenInv * _amplAnimFactor);
#if DEBUG
            devCurrVelFactor = velLenFactor;
            devCurrAnimFactor = animLenInv;
#endif
            for (var i = 0; i < len; i++)
            {
                var e = _elements[i];

                var noiseVec = _elements[i].GetNoiseVecPos(freq * dt, ampl);

                noiseVec += _parent.currNoiseVec[i] * cfg.parentAppFactor;

                var effector = _ikEffectors[i];

                var result = effector.bone.InverseTransformDirection(noiseVec);

                var posSignScale = new Vector3(
                    result.x > 0f ? e.appPositive.x : e.appNegative.x,
                    result.y > 0f ? e.appPositive.y : e.appNegative.y,
                    result.z > 0f ? e.appPositive.z : e.appNegative.z
                    );

                result = Vector3.Scale(result, posSignScale);

                effector.positionOffset = result;
            }
        }
    }
}
