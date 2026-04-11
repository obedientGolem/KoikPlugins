using ADV.Commands.Base;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace IKPlugin
{
    //
    internal class IKPluginAmplifier
    {
        private readonly Transform _bone;
        private readonly Transform _refPoint;

        private Vector3 _smoothVec;
        private Vector3 _prevPos;
        private Vector3 _velocity;
        private Vector3 _totalVelocity;
        private Vector3 _posOffset;
        private float _velocityLen;

        private float _speed = 1f;
        public float _damping;
        public float _defaultDamping = 6f;
        internal float weight = 1f;

        internal IKPluginAmplifier(Transform bone, Transform refPoint, float weight)
        {
            _bone = bone;
            _refPoint = refPoint;
            _prevPos = GetRelativePos;
            this.weight = weight;
        }

        //private readonly Vector3 GetRelativePos => _refPoint.InverseTransformDirection(_bone.position - _refPoint.position);
        private Vector3 GetRelativePos => _refPoint.InverseTransformPoint(_bone.position);
        internal void SetSpeed(float t) => _speed = Mathf.Max(t, 0f);

        internal void AddVelocity(Vector3 velocity)
        {
            if (_totalVelocity.sqrMagnitude < (0.001f * 0.001f))
            {
                _damping = _defaultDamping;
            }
            _totalVelocity += velocity * (weight * _speed);
        }

        internal Vector3 GetPosOffsetDamping(float deltaTime, float normTime)
        {
            //_velocity = Vector3.Lerp(_velocity, _totalVelocity, spring * deltaTime);
            _velocity = Vector3.Lerp(_velocity, _totalVelocity, Mathf.SmoothStep(0f, 1f, normTime));

            _totalVelocity *= Mathf.Exp(-_damping * deltaTime);

            _damping += deltaTime * _defaultDamping;

            return _velocity;
        }

        //internal Vector3 GetPosOffset(float deltaTime, float deltaTimeInv, float weight)
        //{
            // We just reserved an enormous amount of force.
            // transfer it to velocity


            //var accel = (-stiffness * _velocity) - (damping * dampingVelocity);

            //_totalVelocity += accel * deltaTime;
            //_velocity += _totalVelocity * deltaTime;


        //    var vel = _velocity;

        //    var curPos = GetRelativePos;

        //    //var vec = (curPos - _prevPos) * deltaTimeInv;

        //    var vec = (curPos - _prevPos);


        //    // F(spring) = k * x
        //    var springForce = posSpring * vec;
        //    // F(damp) = -c * v
        //    var dampingForce = -posDamp * vel;
        //    // Forces combined
        //    var totalForce = springForce + dampingForce;
        //    // a = F / m
        //    var accel = totalForce * (deltaTime * weight);

        //    vel += accel;

        //    // Check if clamp is necessary
        //    var velLen = vel.magnitude;
        //    var maxVelocityLen = 1f;

        //    if (velLen > maxVelocityLen)
        //    {
        //        vel = (vel / velLen) * maxVelocityLen;
        //        velLen = maxVelocityLen;
        //    }

        //    //#if DEBUG
        //    //            AniMorph.Logger.LogDebug($"{GetType().Name}.GetLinearOffset: - " +
        //    //                $"mag[{mag:F3}] pos[{previous.cleanPosition}]" +
        //    //                $"velocity({velocity.x:F3},{velocity.y:F3},{velocity.z:F3}) " +
        //    //                $"vec({vec.x:F3},{vec.y:F3},{vec.z:F3}) " +
        //    //                $"acceleration({acceleration.x:F3},{acceleration.y:F3},{acceleration.z:F3}) " +
        //    //                $"dampingForce({dampingForce.x:F3},{dampingForce.y:F3},{dampingForce.z:F3}) " +
        //    //                $"result({result.x:F3},{result.y:F3},{result.z:F3})");
        //    //#endif

        //    _velocity = vel;
        //    _velocityLen = velLen;
        //    _prevPos = curPos;

        //    return vel;


        //    var smoothVec = _speed == 0f ? vec : Vector3.Lerp(_smoothVec, vec, deltaTime * _speed);

        //    var offset = _refPoint.TransformDirection(smoothVec);

        //    _prevPos = curPos;
        //    _smoothVec = smoothVec;

        //    return offset;
        //}
    }
}
