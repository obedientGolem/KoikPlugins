using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

namespace IKNoise
{
    internal class IKNoiseModifier
    {
        internal IKNoiseModifier(IKEffector[] effectors)
        {
            _ikEffector = effectors;
            _len = effectors.Length;
            _elements = new Element[_len];
            for (var i = 0; i < _len; i++)
            {
                _elements[i] = new(effectors[i].bone);
            }
            _tempVectors = new Vector3[_len];
            _invLen = 1f / _len;
        }
        private bool _enabled;

        private readonly Element[] _elements;
        private readonly IKEffector[] _ikEffector;
        private readonly Vector3[] _tempVectors;

        private readonly int _len;
        private readonly float _invLen;

        private float _baseFreq = 0.5f;
        private float _baseAmpl = 0.05f;

        private float _freqVelFactor;
        private float _amplVelFactor;

        private float _freqAnimFactor;
        private float _amplAnimFactor;

        private float currVelocityLen;
        private float currTorqueLen;

        private float currVelFactor;
        private float currAnimFactor;


        internal void UpdateModifier(float dt, float dtInv, float animLenInv)
        {
            if (!_enabled) return;

            UpdateVelocity(dt, dtInv);
            //UpdateTorque(dt, dtInv);

            var xOffset = 0f;

            var freq = _baseFreq + (currVelocityLen * (1f / 0.03f) * _freqVelFactor) + (animLenInv * _freqAnimFactor);
            var ampl = _baseAmpl + (currVelocityLen * (1f / 0.03f) * _amplAnimFactor) + (animLenInv * _amplAnimFactor);

            currVelFactor = currVelocityLen * (1f / 0.03f);
            currAnimFactor = animLenInv;

            for (var i = 0; i < _len; i++)
            {
                var noiseVec = _elements[i].GetNoiseVecPos(freq * dt, ampl);
                
                if (i > 0) 
                    noiseVec = new Vector3(xOffset, noiseVec.y, noiseVec.z);
                else
                    xOffset = noiseVec.x;
                
                _tempVectors[i] = noiseVec;
            }

            for (var i = 0; i < _len; i++)
            {
                var effector = _ikEffector[i];

                effector.positionOffset = effector.bone.InverseTransformDirection(_tempVectors[i]);
            }
        }



        private void UpdateVelocity(float dt, float dtInv)
        {
            var totalVelocity = Vector3.zero;

            for (var i = 0; i < _len; i++)
            {
                ref var element = ref _elements[i];

                var delta = element.GetPosDelta();

                var accel = ((28f * (1f / 60f)) * dtInv * delta) + (-14f * element.velocity);

                element.velocity += accel * dt;

                totalVelocity += element.velocity;
            }

            currVelocityLen = (totalVelocity * _invLen).magnitude;
        }

        private void UpdateTorque(float dt, float dtInv)
        {
            var totalTorque = Vector3.zero;

            for (var i = 0; i < _len; i++)
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

            currTorqueLen = (totalTorque * _invLen).magnitude;
        }


        #region OnHooks


        internal void OnSettingChanged(ConfigType config, int sex)
        {
            _enabled = (config.EnableSex.Value & (Sex)(sex + 1)) != 0;

            if (!_enabled) return;
            
            _baseFreq = config.BaseFreq.Value;

            var freq = _baseFreq * config.Freq.Value;

            _freqAnimFactor = freq * (1f - config.FreqSclRatio.Value);
            _freqVelFactor = freq * config.FreqSclRatio.Value;


            _baseAmpl = config.BaseAmpl.Value;

            var ampl = _baseAmpl * config.Ampl.Value;

            _amplAnimFactor = ampl * (1f - config.AmplSclRatio.Value);
            _amplVelFactor = ampl * config.AmplSclRatio.Value;
        }


        #endregion


        private struct Element
        {
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

            internal readonly Transform transform;

            internal Vector3 prevPos;
            internal Quaternion prevRot;

            internal Vector3 velocity;
            internal Vector3 torque;
            private Vector3 noiseVec;
        }

    }
}
