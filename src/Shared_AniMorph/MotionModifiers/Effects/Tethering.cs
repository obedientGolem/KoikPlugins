using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AniMorph
{
    internal class Tethering
    {


        // Multiplier for velocity
        internal float multiplier = -500;
        // Limit of rotation
        internal float maxAngle = 30f;
        // Natural frequency of bounce
        internal float frequency = 3f; // 4f;

        // 0 = undamped, 1 = critically damped
        internal float damping = 0.3f;

        private Vector3 _velocity;
        // Current rotation offset
        private Vector3 _position;
        // How much Z velocity influences bone
        private readonly float _influenceZ;
        // How much X velocity influences bone
        private readonly float _influenceX;

        internal Tethering(Transform centeredBone, Vector3 bonePosition)
        {
            var localBonePosition = centeredBone.InverseTransformPoint(bonePosition);
            var divider = Mathf.Abs(localBonePosition.x) + Mathf.Abs(localBonePosition.z);
            _influenceZ = divider == 0f ? (0f) : (localBonePosition.x / divider);
            _influenceX = 1f - Mathf.Abs(_influenceZ);
        }

        internal void Clear()
        {
            _velocity = Vector3.zero;
            _position = Vector3.zero;
        }


        internal Vector3 GetTetheringOffset(Vector3 velocity, float deltaTime)
        {

            var targetEuler = new Vector3(
                -velocity.y,
                (velocity.x * _influenceX) + (-velocity.z * _influenceZ),
                0f
                ) * multiplier;

            targetEuler = Vector3.ClampMagnitude(targetEuler, maxAngle);

            _position = DampedSpring(_position, targetEuler, ref _velocity, frequency, damping, deltaTime);
            return _position;
        }

        /// <summary>
        /// Spat out by GPT.
        /// Damped harmonic oscillator for smooth overshoot & bounce behavior.
        /// Based on https://www.youtube.com/watch?v=KPoeNZZ6H4s 
        /// </summary>
        private Vector3 DampedSpring(Vector3 current, Vector3 target, ref Vector3 velocity, float freq, float damp, float dt)
        {
            float omega = 2f * Mathf.PI * freq;
            float zeta = damp;
            float omegaZeta = omega * zeta;
            float omegaSq = omega * omega;

            Vector3 f = velocity + omegaZeta * (current - target);
            Vector3 a = -omegaSq * (current - target) - 2f * omegaZeta * f;

            velocity += a * dt;
            return current + velocity * dt;
        }  
        //   (forward)
        //
        //        -Z
        //       |   
        // +X    |    -X
        // <—————|—————>
        //       |
        //       |
        //        +Z
        //
        // R  — together  + separate
        // L  — separate  + together
    }   
}
