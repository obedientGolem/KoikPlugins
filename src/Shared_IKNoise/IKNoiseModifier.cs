#if VR_DIR
using KK.RootMotion.FinalIK;
#else
using RootMotion.FinalIK;
#endif
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;
using static IKNoise.IKNoiseEffector;

namespace IKNoise
{
    internal class IKNoiseModifier
    {
        internal IKNoiseModifier(IKEffector[] effectors, Cfg cfg, BaseCfg baseCfg)
        {
            var len = effectors.Length;
            _ikEffectors = effectors;
            _elements = new Element[len];
            currNoiseVec = new Vector3[len];
            for (var i = 0; i < len; i++)
            {
                _elements[i] = new(effectors[i].bone);
            }
            this.len = len;
            lenInv = 1f / len;
            this.cfg = cfg;
            this.baseCfg = baseCfg;

            LimitMovements(90);
        }


        protected Cfg cfg;
        protected BaseCfg baseCfg;

        protected readonly Element[] _elements;
        protected readonly IKEffector[] _ikEffectors;
        internal readonly Vector3[] currNoiseVec;


        protected readonly int len;
        protected readonly float lenInv;


        protected float _baseFreq;
        protected float _baseAmpl;

        protected float _freqVelFactor;
        protected float _amplVelFactor;

        protected float _freqAnimFactor;
        protected float _amplAnimFactor;

#if DEBUG
        protected float devCurrVelFactor;
        protected float devCurrAnimFactor;
#endif

        internal virtual void UpdateModifier(float dt, float dtInv, float animLenInv)
        {
            var velLen = GetVelocityLen(dt, dtInv);
            //UpdateTorque(dt, dtInv);

            var velLenFactor = velLen * (100f / 3f);

            var freq = _baseFreq + (velLenFactor * _freqVelFactor) + (animLenInv * _freqAnimFactor);
            var ampl = _baseAmpl + (velLenFactor * _amplVelFactor) + (animLenInv * _amplAnimFactor);
#if DEBUG
            devCurrVelFactor = velLenFactor;
            devCurrAnimFactor = animLenInv;
#endif
            var syncAxisX = cfg.syncAxisX;
            var x = 0f;

            for (var i = 0; i < len; i++)
            {
                ref var e = ref _elements[i];

                var noiseVec = e.GetNoiseVecPos(freq * dt, ampl);

                var effector = _ikEffectors[i];

                var result = effector.bone.InverseTransformDirection(noiseVec);

                if (syncAxisX)
                {
                    if (i == 0)
                        x = result.x;
                    else
                        result.x = x;
                }

                var posSignScale = new Vector3(
                    result.x > 0f ? e.appPositive.x : e.appNegative.x,
                    result.y > 0f ? e.appPositive.y : e.appNegative.y,
                    result.z > 0f ? e.appPositive.z : e.appNegative.z
                    );

                result = Vector3.Scale(result, posSignScale);

                effector.positionOffset = result;
                currNoiseVec[i] = noiseVec;
            }
        }

        protected float GetVelocityLen(float dt, float dtInv)
        {
            var totalVelocity = Vector3.zero;

            for (var i = 0; i < len; i++)
            {
                ref var element = ref _elements[i];

                var delta = element.GetPosDelta();

                var accel = ((28f * (1f / 60f)) * dtInv * delta) + (-14f * element.velocity);

                element.velocity += accel * dt;

                totalVelocity += element.velocity;
            }
            return (totalVelocity * lenInv).magnitude;
        }

        protected float GetTorqueLen(float dt, float dtInv)
        {
            var totalTorque = Vector3.zero;

            for (var i = 0; i < len; i++)
            {
                ref var element = ref _elements[i];

                var delta = element.GetRotDelta();

                delta.ToAngleAxis(out var angle, out var axis);

                if (angle > 180f)
                    angle -= 360f;

                var accel = (angle * dtInv * (1f * (1f / 60f)) * axis) + (element.torque * -0.5f);

                element.torque += accel * dt;

                totalTorque += element.torque;
            }

            return (totalTorque * lenInv).magnitude;
        }

        internal Vector3 GetAvgPosition()
        {
            var vec = new Vector3();

            foreach (var element in _elements)
                vec += element.transform.position;

            return vec * lenInv;
        }

        internal float GetAvgDegVerticalAlignment()
        {
            var totalDeg = 0f;
#if VR
            var isLimb = baseCfg.body == Body.Arms || baseCfg.body == Body.Legs;
#endif
            var isSpine = baseCfg.body == Body.Spine;
            foreach (var e in _elements)
            {
#if VR
                var transform = isLimb ? e.transform.GetChild(0) : isSpine ? e.transform : e.transform.parent;
#else
                var transform = isSpine ? e.transform : e.transform.parent;
#endif
                if (transform == null) transform = e.transform;

                var dot = Vector3.Dot(transform.rotation * baseCfg.axisForDotUp, Vector3.up);

                totalDeg += Mathf.Acos(dot) * Mathf.Rad2Deg;
            }
            return totalDeg * lenInv;
        }

        internal void LimitMovements(float deg)
        {
            if (deg < baseCfg.angleLimit)
            {
//                var hMoveF = TwoThirds;

//                var vMoveUpF = TwoThirds;
//                var vMoveDownF = OneThird;
//#if VR
//                var isLimb = baseCfg.body == Body.Arms || baseCfg.body == Body.Legs;
//                if (isLimb)
//                {
//                    hMoveF = 0f;
//                    vMoveUpF = OneThird;
//                    vMoveDownF = 0f;
//                }
//#endif
//                var vAxis = baseCfg.verticalAxis;

                for (var i = 0; i < len; i++)
                {
                    ref var e = ref _elements[i];

                    //var vecPositive = new Vector3(
                    //    vAxis.x == 0f ? hMoveF : vMoveUpF,
                    //    vAxis.y == 0f ? hMoveF : vMoveUpF,
                    //    vAxis.z == 0f ? hMoveF : vMoveUpF
                    //    );
                    
                    //var vecNegative = new Vector3(
                    //    vAxis.x == 0f ? hMoveF : vMoveDownF,
                    //    vAxis.y == 0f ? hMoveF : vMoveDownF,
                    //    vAxis.z == 0f ? hMoveF : vMoveDownF
                    //    );

                    e.appPositive = Vector3.Scale(baseCfg.appPositive[i], baseCfg.appFSideUpPositive);
                    e.appNegative = Vector3.Scale(baseCfg.appNegative[i], baseCfg.appFSideUpNegative);
                }
            }
            else if (deg > (180f - baseCfg.angleLimit))
            {
                for (var i = 0; i < len; i++)
                {
                    ref var e = ref _elements[i];

                    e.appPositive = Vector3.Scale(baseCfg.appPositive[i], baseCfg.appFSideDownPositive);
                    e.appNegative = Vector3.Scale(baseCfg.appNegative[i], baseCfg.appFSideDownNegative);
                }
            }
            else
            {
                for (var i = 0; i < len; i++)
                {
                    ref var e = ref _elements[i];

                    e.appPositive = baseCfg.appPositive[i];
                    e.appNegative = baseCfg.appNegative[i];
                }
            }
        }


        #region OnHooks


        internal void OnSettingChanged(ConfigType config, int sex, float sceneFactor)
        {
            _baseFreq = config.BaseFreq.Value;

            var freq = config.Freq.Value;

            _freqAnimFactor = freq * (1f - config.FreqSclRatio.Value);
            _freqVelFactor = freq * config.FreqSclRatio.Value;


            _baseAmpl = config.BaseAmpl.Value * sceneFactor;

            var ampl = config.Ampl.Value;

            _amplAnimFactor = ampl * (1f - config.AmplSclRatio.Value);
            _amplVelFactor = ampl * config.AmplSclRatio.Value;
        }


        #endregion


        #region Types


        protected struct Element
        {
            internal Vector3 appPositive;
            internal Vector3 appNegative;

            internal readonly Transform transform;

            internal Vector3 prevPos;
            internal Quaternion prevRot;

            internal Vector3 velocity;
            internal Vector3 torque;

            private Vector3 noiseVec;

            internal Element(Transform transform)
            {
                this.transform = transform;

                prevPos = transform.position;
                prevRot = transform.rotation;

                noiseVec = new(Random.Range(0f, 1000f), Random.Range(0f, 1000f), Random.Range(0f, 1000f));
            }

            internal Vector3 GetNoiseVecPos(float freq, float ampl)
            {
                var vec = noiseVec;

                noiseVec = new(
                    noiseVec.x + freq,
                    noiseVec.y + freq,
                    noiseVec.z + freq
                    );
                var x = 0f;
                var y = 0f;
                var z = 0f;

                for (var i = 0; i < 4; i++)
                {
                    x += (Mathf.PerlinNoise(vec.x + freq, 0f) - 0.5f) * ampl;
                    y += (Mathf.PerlinNoise(0f, vec.y + freq) - 0.5f) * ampl;
                    z += (Mathf.PerlinNoise(vec.z + freq, vec.z + freq) - 0.5f) * ampl;

                    ampl *= 0.5f;
                    freq *= 2f;
                }
                return new Vector3(x, y, z);
            }

            internal Vector3 GetPosDelta()
            {
                var currPos = transform.position;

                var result = currPos - prevPos;

                prevPos = currPos;

                return result;
            }

            internal Quaternion GetRotDelta()
            {
                var currRot = transform.rotation;

                var result = Quaternion.Inverse(currRot) * prevRot;

                prevRot = currRot;

                return result;
            }
        }


        #endregion
    }
}
