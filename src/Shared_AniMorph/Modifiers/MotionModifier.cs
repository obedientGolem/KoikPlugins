using ADV.Commands.Camera;
using KKABMX.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static AniMorph.AniMorphPlugin;
using static RootMotion.FinalIK.IKSolver;

namespace AniMorph
{
    internal class MotionModifier
    {
        protected readonly Transform transform;

        protected readonly Tethering tether;

        protected KKABMX.Core.BoneModifier abmxModifier;


        protected bool Active;


        protected bool isLeftSide;

        private readonly float _baseScaleVolume;
        private readonly float _baseScaleMagnitude;
        private readonly bool _isAnimatedRotation;

        // Big, only ref access.
        protected MotionConfig config = new();

        // ~80 bytes, ref access is preferred.
        protected PreviousFrame previous;

        private static readonly Vector3 vecOne = Vector3.one;

        internal void SetMass(float value)
        {
            if (value <= 0f) value = 0.01f;

            ref var cfg = ref config;
            cfg.mass = value;
            cfg.massInv = 1f / value;
        }

        private void SetMaxVelocity(float value)
        {
            ref var cfg = ref config;

            cfg.linearMaxVelocityLen = value;
            cfg.linearMaxSqrVelocity = value * value;
        }


        protected Vector3 AngularApplication = Vector3.one;


        protected readonly BoneModifierData abmxModifierData;

        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="centeredBone">A centered bone with normal orientation on the body. Required for setup only.</param>
        /// <param name="bone">Bone that will be modified.</param>
        /// <param name="bakedMesh">Baked skinned mesh</param>
        /// <param name="skinnedMesh"></param>
        internal MotionModifier(Transform bone, Transform centeredBone, Mesh bakedMesh, SkinnedMeshRenderer skinnedMesh, BoneModifierData boneModifierData, bool isAnimatedRotation)
        {
            if (bone == null) 
                throw new ArgumentNullException(nameof(bone));
            
            ref var prev = ref previous;

            transform = bone;
            abmxModifierData = boneModifierData;
            var pos = bone.position;
            prev.position = pos;
            prev.cleanPos = pos;
            //previous.cleanRot = bone.rotation;
            prev.localRot = bone.localRotation;
            prev.adjustedRot = Quaternion.identity;
            _baseScaleVolume = bone.localScale.x * bone.localScale.y * bone.localScale.z;
            _baseScaleMagnitude = bone.localScale.magnitude;
            _isAnimatedRotation = isAnimatedRotation;

            if (centeredBone != null)
            {
                tether = new Tethering(centeredBone, prev.position);

                var localBonePosition = centeredBone.InverseTransformPoint(bone.position);
                var divider = Mathf.Abs(localBonePosition.x) + Mathf.Abs(localBonePosition.z);
                var result = divider == 0f ? (0f) : (localBonePosition.x / divider);
                isLeftSide = result < 0f;
            }

            // Skip mesh measurements
            //if (bakedMesh == null || skinnedMesh == null) return;

            //var vertices = bakedMesh.vertices;
            //var triangles = bakedMesh.triangles;
            //var t = skinnedMesh.transform;
            //Ray[] rays = [
            //    new Ray(bone.position, bone.position + bone.forward), 
            //    new Ray(bone.position, bone.position - bone.forward), 
            //    new Ray(bone.position, bone.position - bone.right), 
            //    new Ray(bone.position, bone.position + bone.right)
            //    ];

            //float[] closestDist = [Mathf.Infinity, Mathf.Infinity, Mathf.Infinity, Mathf.Infinity];

            //for (var i = 0; i < triangles.Length; i += 3)
            //{
            //    var v0 = t.TransformPoint(vertices[triangles[i]]);
            //    var v1 = t.TransformPoint(vertices[triangles[i + 1]]);
            //    var v2 = t.TransformPoint(vertices[triangles[i + 2]]);

            //    for (var j = 0; j < rays.Length; j++)
            //    {
            //        var ray = rays[j];
            //        if (IntersectRayTriangle(ray, v0, v1, v2, out var hitPoint))
            //        {
            //            var dist = Vector3.Distance(ray.origin, hitPoint);
            //            if (dist < closestDist[j])
            //            {
            //                closestDist[j] = dist;
            //            }
            //        }
            //    }
            //}
            //for (var i = 0; i < closestDist.Length; i++)
            //{
            //    if (closestDist[i] == Mathf.Infinity)
            //    {
            //        AniMorph.Logger.LogError($"{this.GetType().Name} couldn't find the intersection point[{i}].");
            //    }
            //}
            //_height = closestDist[0] + closestDist[1];
            //_width = closestDist[2] + closestDist[3];


            //static bool IntersectRayTriangle(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2, out Vector3 hitPoint)
            //{
            //    hitPoint = Vector3.zero;
            //    Vector3 edge1 = v1 - v0;
            //    Vector3 edge2 = v2 - v0;

            //    Vector3 h = Vector3.Cross(ray.direction, edge2);
            //    float a = Vector3.Dot(edge1, h);
            //    if (Mathf.Abs(a) < Mathf.Epsilon)
            //        return false; // Ray is parallel to triangle

            //    float f = 1f / a;
            //    Vector3 s = ray.origin - v0;
            //    float u = f * Vector3.Dot(s, h);
            //    if (u < 0f || u > 1f)
            //        return false;

            //    Vector3 q = Vector3.Cross(s, edge1);
            //    float v = f * Vector3.Dot(ray.direction, q);
            //    if (v < 0f || u + v > 1f)
            //        return false;

            //    // At this stage we can compute t to find out where the intersection point is on the line
            //    float t = f * Vector3.Dot(edge2, q);
            //    if (t >= 0f) // ray intersection
            //    {
            //        hitPoint = ray.origin + ray.direction * t;
            //        return true;
            //    }

            //    return false; // Line intersection but not a ray intersection
            //}
        }

        //protected KKABMX.Core.BoneModifier FindBoneModifier(BoneController origin)
        //{
        //    var boneName = transform.name;
        //    foreach (var boneModifier in origin.GetAllModifiers())
        //    {
        //        if (boneModifier.BoneName.Equals(boneName))
        //        {
        //            return boneModifier;
        //        }
        //    }
        //    return null;
        //}


        #region UpdateCycle


        internal void OnUpdate()
        {
            // Grab modified local orientations from previous frame,
            // Later on Update we'll
            ref var prev = ref previous;
            prev.localPos = transform.localPosition;
            prev.localRot = transform.localRotation;
        }

        /// <summary>
        /// Meant for access from BoneEffector.
        /// </summary>
        internal virtual void UpdateModifiers(float deltaTime, float fps)
        {
            if (!Active) return;

            //AniMorph.Logger.LogDebug($"UpdateModifiers[{transform.name}] pos({transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3})");

            ref var config = ref this.config;
            ref var prev = ref previous;

            // Apply linear offset, its calculations are necessary to other methods even if the offset itself isn't.
            var posModifier = GetLinearOffset(ref config, ref prev, deltaTime, out var velocity, out var velocityMagnitude);

            // Remove linear offset if setting
            if ((config.effects & Effect.Pos) == 0)
                posModifier = Vector3.zero;

            // Apply angular offset
            var rotModifier = (config.effects & Effect.Rot) != 0 ?
                GetAngularOffset(ref config, ref prev, deltaTime) : Vector3.zero;

            // Not allowed axes are multiplied by zero, allowed by one.
            rotModifier = Vector3.Scale(rotModifier, AngularApplication);

            // Apply acceleration scale distortion
            var scaleModifier = GetScaleOffset(
                ref config,
                ref prev,
                velocity, velocityMagnitude, deltaTime, fps,
                (config.effects & Effect.Accel) != 0,
                (config.effects & Effect.Decel) != 0
                );

            if ((config.effects & Effect.Tether) != 0)
                rotModifier += tether.GetTetheringOffset(velocity, deltaTime);

            var dotUp = Vector3.Dot(transform.up, Vector3.up);
            var dotR = Vector3.Dot(transform.right, Vector3.up);
            var dotFwd = Vector3.Dot(transform.forward, Vector3.up);

            // Apply gravity position offset
            if ((config.effects & Effect.GravPos) != 0)
                posModifier += GetGravityPositionOffset(ref config, dotUp, dotR);
            // Apply gravity scale offset
            if ((config.effects & Effect.GravScl) != 0)
                scaleModifier = Vector3.Scale(scaleModifier, GetGravityScaleOffset(ref config, dotFwd));
            // Apply gravity rotation offset
            if ((config.effects & Effect.GravRot) != 0)
                rotModifier += GetGravityAngularOffset(dotFwd, dotR);

            var boneModifierData = abmxModifierData;
            // Write modifiers for ABMX consumption
            boneModifierData.PositionModifier = posModifier;
            boneModifierData.RotationModifier = rotModifier;
            boneModifierData.ScaleModifier = scaleModifier;

            //AniMorph.Logger.LogDebug($"UpdateModifiers: pos[{positionModifier}] rot[{rotationModifier}] scale[{scaleModifier}]");

            // Store current variables as "previous" for the next frame.
            prev.velocity = velocity;

            // Positional offset that will be the ABMX,
            // required for calculation of (semi)static bones.
            prev.posModifier = posModifier;
            prev.rotModifier = rotModifier;
        }


        #endregion


        #region Position


        // Implementation with Hooke's Law 
        /// <param name="deltaTime">Requires unscaled time to work properly on abnormal speed.</param>
        protected Vector3 GetLinearOffset(ref MotionConfig config, ref PreviousFrame prev, float deltaTime, out Vector3 velocity, out float velocityLen)
        {
            // This works with bones that are reset by animator
            //velocity = previous.velocity;
            //var vec = transform.InverseTransformPoint(previous.position);
            //previous.position = transform.position;


            // This works with bones that aren't reset by the animator,
            // i.e. retain their local adjustments between frames.
            // Works with boobs and x axis of waist01, not kokan
            //velocity = previous.velocity;
            //var cleanPosition = transform.TransformPoint(-previous.posModifier);
            //var vec = transform.InverseTransformDirection(previous.position - cleanPosition);
            //previous.position = cleanPosition;


            var localPos = transform.localPosition;
            var localPosOnUpdate = prev.localPos;

            var x = localPos.x != localPosOnUpdate.x;
            var y = localPos.y != localPosOnUpdate.y;
            var z = localPos.z != localPosOnUpdate.z;

            velocity = prev.velocity;

            //AniMorph.Logger.LogDebug($"[{transform.name}] GetLinearOffset: dynamicBone({x},{y},{z})");

            Vector3 vec;
            Vector3 cleanPosition;

            var curPos = transform.position;

            // Bone is reset each frame by the animator.
            if (x && y && z)
            {
                cleanPosition = curPos;
                vec = transform.InverseTransformPoint(prev.position);
            }
            // Bone isn't reset by the animator.
            else if (!x && !y && !z)
            {
                cleanPosition = transform.TransformPoint(-prev.posModifier);
                vec = transform.InverseTransformDirection(prev.cleanPos - cleanPosition);
            }
            // The Illusion way.
            else
            {
                cleanPosition = transform.TransformPoint(-prev.posModifier);
                var vecDynamic = transform.InverseTransformPoint(prev.position);
                var vecStatic = transform.InverseTransformDirection(prev.cleanPos - cleanPosition);

                vec = new Vector3(
                    x ? vecDynamic.x : vecStatic.x,
                    y ? vecDynamic.y : vecStatic.y,
                    z ? vecDynamic.z : vecStatic.z
                    );
            }
            prev.position = transform.position;
            prev.cleanPos = cleanPosition;
            
            //var mag = vec.magnitude;

            //// Normalized displacement, avoid division by zero
            //var vecNorm = (mag > 1E-05f) ? (vec * (1f / mag)) : Vector3.zero;

            //var stretch = currentLength - restLength;
            // F_spring = -k * x
            var springForce = -config.posSpring * vec;
            // F_damp = -c * v
            var dampingForce = -config.posDamping * velocity;
            // Forces combined
            var totalForce = springForce + dampingForce;
            //// Apply gravity force
            if (config.useLinearGravity)
            {
                totalForce += config.mass * transform.InverseTransformDirection(config.posGravityForce);
            }
            // a = F / m
            var accel = totalForce * (config.massInv * deltaTime);

            // Limit axes

            accel.x *= accel.x > 0f ? config.posLimitPositive.x : config.posLimitNegative.x;
            accel.y *= accel.y > 0f ? config.posLimitPositive.y : config.posLimitNegative.y;
            accel.z *= accel.z > 0f ? config.posLimitPositive.z : config.posLimitNegative.z;

            velocity += accel;

            // Check if clamp is necessary
            velocityLen = velocity.magnitude;
            var maxVelocityLen = config.linearMaxVelocityLen;

            if (velocityLen > maxVelocityLen)
            {
                velocity = (velocity / velocityLen) * maxVelocityLen;
                velocityLen = maxVelocityLen;
            }

//#if DEBUG
//            AniMorph.Logger.LogDebug($"{GetType().Name}.GetLinearOffset: - " +
//                $"mag[{mag:F3}] pos[{previous.cleanPosition}]" +
//                $"velocity({velocity.x:F3},{velocity.y:F3},{velocity.z:F3}) " +
//                $"vec({vec.x:F3},{vec.y:F3},{vec.z:F3}) " +
//                $"acceleration({acceleration.x:F3},{acceleration.y:F3},{acceleration.z:F3}) " +
//                $"dampingForce({dampingForce.x:F3},{dampingForce.y:F3},{dampingForce.z:F3}) " +
//                $"result({result.x:F3},{result.y:F3},{result.z:F3})");
//#endif


            return vec - velocity;
        }


        #endregion


        #region Rotation


        //        protected Vector3 GetAngularOffset(ref MotionConfig config, ref PreviousFrame previous, float deltaTime)
        //        {
        //            //            var currentRotation = transform.rotation;
        //            //            var prevRotation = previous.rotation;

        //            //            var deltaRotation = currentRotation * Quaternion.Inverse(previous.rotation);

        //            //            deltaRotation.ToAngleAxis(out var angle, out var axis);

        //            //            // Avoid Vector3(Infinity) in axis.
        //            //            if (float.IsInfinity(axis.x))
        //            //            {
        //            //#if DEBUG
        //            //                AniMorph.Logger.LogDebug($"Infinity axis detected[{axis}] angle[{angle:F3}], using fallback axis.");
        //            //#endif
        //            //                //axis = new Vector3(axis.x < 0f ? 1f : 0f, axis.y < 0f ? 1f : 0f, axis.z < 0f ? 1f : 0f);
        //            //                axis = Vector3.up;
        //            //            }

        //            //            // Convert angle to (-180 .. 180) format
        //            //            if (angle > 180f)
        //            //                angle -= 360f;

        //            //            var angularVelocity = previous.angularVelocity;

        //            //            var torque = config.angularSpringStrength * angle * axis;
        //            //            var damping = -config.angularDamping * angularVelocity;

        //            //            angularVelocity += (torque + damping) * deltaTime;
        //            //            var newRotation = Quaternion.Euler(angularVelocity * deltaTime) * prevRotation;
        //            //            previous.rotation = newRotation;
        //            //            previous.angularVelocity = angularVelocity;

        //            //            var absAngle = Mathf.Abs(angle);
        //            //            if (absAngle > config.angularMaxAngle)
        //            //                newRotation = Quaternion.Slerp(currentRotation, newRotation, config.angularMaxAngle / absAngle);

        //            //            var result = (Quaternion.Inverse(currentRotation) * newRotation).eulerAngles;
        //            //            // _prevRotation = Quaternion.Euler(result) * _prevRotation;
        //            //            //AniMorph.Logger.LogDebug($"angle[{angle}] deltaEuler{deltaRotation.eulerAngles} result{result} prevRotation{prevRotation} newRotation{newRotation} currentRotation{currentRotation} angularVelocity{angularVelocity}");
        //            //            return result;


        //            var curRot = transform.rotation;
        //            var curLocalRot = transform.localRotation;
        //            var prevLocalRot = previous.localRot;


        //            Quaternion deltaRot;
        //            var cleanRot = curRot;
        //            // Dynamic bone.
        //            // Someone resets bone's rotation each frame.
        //            if (prevLocalRot != curLocalRot)
        //            {
        //                // Difference between previous and current rotation.
        //                deltaRot = cleanRot * Quaternion.Inverse(previous.adjustedRot);

        //                AniMorph.Logger.LogDebug($"[{transform.name}] - {GetType().Name}.GetAngularOffset: " +
        //                    $"DynamicBone delta[{deltaRot.eulerAngles}]");
        //            }
        //            // Static bone.
        //            else
        //            {
        //                cleanRot = Quaternion.Inverse(Quaternion.Euler(previous.rotModifier)) * curRot;
        //                deltaRot = cleanRot * Quaternion.Inverse(previous.adjustedRot);

        //                AniMorph.Logger.LogDebug($"[{transform.name}] - {GetType().Name}.GetAngularOffset: " +
        //                    $"StaticBone delta[{deltaRot.eulerAngles}]");
        //            }
        //            previous.cleanRot = cleanRot;

        //            deltaRot.ToAngleAxis(out var angle, out var axis);

        //            // Avoid corruption due to a very small angle.
        //            var infAxis = float.IsInfinity(axis.x);
        //#if DEBUG
        //            if (infAxis)
        //            {
        //                AniMorph.Logger.LogDebug($"[{transform.name}] - {GetType().Name}.GetAngularOffset: " +
        //                    $"Infinity axis[{axis}] angle[{angle:F3}] delta[{deltaRot.eulerAngles}]");
        //            }
        //#endif

        //            // Convert angle to (-180 .. 180) format
        //            if (angle > 180f)
        //                angle -= 360f;


        //            var angularVelocity = previous.angularVelocity;
        //            var torque = infAxis ? Vector3.zero : config.rotSpring * angle * axis;
        //            var damping = -config.rotDamping * angularVelocity;

        //            angularVelocity += (torque + damping) * deltaTime;

        //            var rot = Quaternion.Euler(angularVelocity * deltaTime) * previous.adjustedRot;

        //            //var absAngle = Mathf.Abs(angle);
        //            //if (absAngle > config.angularMaxAngle)
        //            //{
        //            //    var rot = Quaternion.Euler(rotEuler);
        //            //    rot = Quaternion.Slerp(curRot, rot, config.angularMaxAngle / absAngle);
        //            //    rotEuler = rot.eulerAngles;
        //            //}

        //            previous.adjustedRot = rot;
        //            previous.angularVelocity = angularVelocity;

        //            //var result = (Quaternion.Inverse(curRot) * rot).eulerAngles;
        //            return (Quaternion.Inverse(curRot) * rot).eulerAngles;
        //        }


        protected virtual Vector3 GetAngularOffset(ref MotionConfig config, ref PreviousFrame prev, float deltaTime)
        {
            //            var curRot = transform.rotation;
            //            var dynamic = transform.localRotation != previous.localRot;
            //            // if static

            //            var prevAdjInv = Quaternion.Inverse(Quaternion.Euler(previous.rotModifier));
            //            var cleanRot = curRot * prevAdjInv;
            //            var delta = Quaternion.Inverse(previous.adjustedRot) * cleanRot;

            //            var adjustment = Quaternion.Euler(_adjustment);
            //            var rot = cleanRot * adjustment;
            //            previous.adjustedRot = rot;

            //            var result = (Quaternion.Inverse(cleanRot) * rot).eulerAngles;
            //#if DEBUG
            //            AniMorph.Logger.LogDebug($"[{transform.name}] - Rotation: " +
            //                $"dynamic[{dynamic}] curRot[{curRot.eulerAngles}] cleanRot[{cleanRot.eulerAngles}] newRot[{previous.adjustedRot.eulerAngles}] delta[{delta.eulerAngles}] result[{result}]");
            //#endif
            //            return result;


            var curRot = transform.rotation;
            var dynamic = transform.localRotation != prev.localRot;
            Quaternion deltaRot;
            var cleanRot = curRot;
            // Dynamic bone.
            // Someone resets bone's rotation each frame.
            if (dynamic)
            {
                deltaRot = cleanRot * Quaternion.Inverse(prev.adjustedRot);
                //deltaRot = Quaternion.Inverse(previous.adjustedRot) * cleanRot;
            }
            // Static bone.
            else
            {
                //cleanRot = Quaternion.Inverse(Quaternion.Euler(previous.rotModifier)) * curRot;
                //deltaRot = cleanRot * Quaternion.Inverse(previous.adjustedRot);
                cleanRot = curRot * Quaternion.Inverse(Quaternion.Euler(prev.rotModifier));
                //deltaRot = Quaternion.Inverse(previous.adjustedRot) * cleanRot;
                deltaRot = cleanRot * Quaternion.Inverse(prev.adjustedRot);
            }


            // ToAngleAxis - 2 trig or trig + sqrt and few divs,
            // Angle - 1 trig,
            // Slerp - 4 trig and few divs.

            deltaRot.ToAngleAxis(out var angle, out var axis);

            // Avoid corruption due to a very small angle and float approximation,
            // it will corrupt whole vector so one axis check is enough.
            var infAxis = float.IsInfinity(axis.x);
#if DEBUG
            if (infAxis)
            {
                AniMorphPlugin.Logger.LogDebug($"[{transform.name}] - {GetType().Name}.GetAngularOffset: " +
                    $"Infinity axis[{axis}] angle[{angle:F3}] delta[{deltaRot.eulerAngles}]");
            }
#endif
            // Convert angle to (-180 .. 180)
            if (angle > 180f)
                angle -= 360f;

            var angVelocity = prev.angularVelocity;
            var torque = infAxis ? Vector3.zero : config.rotSpring * angle * axis;
            var damping = -config.rotDamping * angVelocity;

            angVelocity += (torque + damping) * deltaTime;

            // AngleAxis should be stable as contrary to Euler.
            var angVelocityLen = angVelocity.magnitude;
            var rot = angVelocityLen == 0f ?
                prev.adjustedRot :
                Quaternion.AngleAxis(angVelocityLen * deltaTime * config.rotRate, angVelocity * (1f / angVelocityLen)) * prev.adjustedRot;
            //var rot = Quaternion.Euler(angVelocity * (deltaTime * config.rotRate)) * previous.adjustedRot;


            //previous.cleanRot = cleanRot
            var prevAdjRot = prev.adjustedRot;
            prev.adjustedRot = rot;
            prev.angularVelocity = angVelocity;

            // TODO update previous.adjustedRot if clamp happened.

            // Clamp rotation /w setting angle.
            var result = Quaternion.Inverse(cleanRot) * rot;
            //var resultAngle = Quaternion.Angle(Quaternion.identity, result);

            //if (resultAngle > config.rotMaxAngle)
            //{
            //    result = Quaternion.Slerp(Quaternion.identity, result, config.rotMaxAngle / resultAngle);
            //    previous.adjustedRot = result * cleanRot;
            //}
            var resultEuler = result.eulerAngles;
//#if DEBUG
//            AniMorphPlugin.Logger.LogDebug($"[{transform.name}] - Rotation: " +
//                $"dynamic[{dynamic}] curRot[{curRot.eulerAngles}] cleanRot[{cleanRot.eulerAngles}] prevAdjRot[{prevAdjRot.eulerAngles}] delta[{deltaRot.eulerAngles}] rot[{rot.eulerAngles}] proposedRot[{rot.eulerAngles}] result[{resultEuler}] torque[{torque:F1}] damping[{damping:F1}]");// angle[{angle}] axis[{axis}]");
//#endif
            return resultEuler;
        }


        #endregion


        #region Scale


        protected Vector3 GetScaleOffset(ref MotionConfig config, ref PreviousFrame prev, Vector3 velocity, float velocityMagnitude, float deltaTime, float fps, bool acceleration,  bool deceleration)
        {
            if (!acceleration && !deceleration) return Vector3.one;
            // Avoid division by zero
            if (velocityMagnitude == 0f)
            {
#if DEBUG
                AniMorphPlugin.Logger.LogDebug($"{GetType().Name}.{MethodInfo.GetCurrentMethod().Name}:Undesirable parameter value in 'velocityMagnitude', avoided divisions by zero.");
#endif
                return Vector3.one;
            }

            // Normalize velocity
            var velocityNormalized = velocity * (1f / velocityMagnitude);

            var absVelocityNormalized = new Vector3(Mathf.Abs(velocityNormalized.x), Mathf.Abs(velocityNormalized.y), Mathf.Abs(velocityNormalized.z));
            var accelerationVec = (velocity - prev.velocity) * fps;
            // Project acceleration onto direction to get deceleration if negative
            var accelerationDot = Vector3.Dot(accelerationVec, velocityNormalized);

            // Initialize distortion as neutral
            var distortion = Vector3.one;
            // Proper acceleration with accumulation or without
            // looks worse then a dumb magnitude based implementation.
            // But hey it's an option.

            if (acceleration)
            { 
                var distortionAmount = config.sclAccelerationStrength;

                if (config.sclDumbAcceleration)
                {
                    distortionAmount *= velocityMagnitude * fps;
                }
                else
                {
                    // Accumulate acceleration, deltaTime is a passive drain to avoid awkward accumulations in some idle animations.
                    var totalAcceleration = Mathf.Clamp01(config.sclAccumulatedAcceleration + accelerationDot - (deltaTime * 0.1f));

                    distortionAmount *= totalAcceleration;

                    config.sclAccumulatedAcceleration = totalAcceleration;
                }

                distortionAmount = Mathf.Clamp(distortionAmount, 0f, config.sclMaxDistortion);
                // Apply distortion amount to velocity axes
                distortion += absVelocityNormalized * distortionAmount;
                // Shrink axes perpendicular to the velocity 
                var perpendicularScale = Vector3.one + Vector3.Scale(absVelocityNormalized - Vector3.one, distortionAmount * config.sclAxisDistribution);
                // Combine vectors
                distortion = Vector3.Scale(distortion, perpendicularScale);
//#if DEBUG
//                AniMorph.Logger.LogDebug($"Acceleration:distortion({distortion.x:F3},{distortion.y:F3},{distortion.z:F3})" +
//                    $"distortionAmount[{distortionAmount:F3}] velocityMagnitude[{(velocityMagnitude * fps):F5}" +
//                   // $"scale({perpendicularScale.x:F3},{perpendicularScale.y:F3},{perpendicularScale.z:F3})" +
//                    //$"absVelocityNorm({absVelocityNormalized.x:F3},{absVelocityNormalized.y:F3},{absVelocityNormalized.z:F3})"
//                    "");
//#endif
            }

            if (deceleration)
            { 
                var decelerationDot = Vector3.Dot(accelerationVec, velocityNormalized);

                //AniMorph.Logger.LogDebug($"Deceleration:dot[{decelerationDot:F3}] " +
                //    $"totalDeceleration[{_totalDeceleration:F3}" 
                //    //$"velocityDir({velocityDir.x:F3},{velocityDir.y:F3},{velocityDir.z:F3})" +
                //    //$"accelerationVec{accelerationVec} accelerationVecMag{accelerationVec.magnitude:F3}"
                //    );
                // Amplify deceleration as it tends to be too small.
                if (accelerationDot < 0f) accelerationDot *= 2f;
                // Accumulate deceleration, deltaTime is a passive drain to avoid awkward accumulations in some idle animations.
                var totalDeceleration = Mathf.Clamp01(config.scaleAccumulatedDeceleration + accelerationDot - (deltaTime * 0.1f));
                // Store for the next frame
                config.scaleAccumulatedDeceleration = totalDeceleration;

                if (totalDeceleration > 0f)
                {
                    // How much scale can deviate
                    var distortionAmount = totalDeceleration  * config.sclDecelerationStrength;
                    distortionAmount = Mathf.Clamp(distortionAmount, 0f, config.sclMaxDistortion);
                    // Apply distortion amount to velocity axes
                    var decelerationScale = Vector3.one - (absVelocityNormalized * distortionAmount);
                    // Expand axes perpendicular to the velocity
                    var perpendicularScale = Vector3.one + Vector3.Scale((Vector3.one - absVelocityNormalized), distortionAmount * config.sclAxisDistribution); // * (squashAmount * 0.5f);
                    // Combine vectors
                    decelerationScale = Vector3.Scale(decelerationScale, perpendicularScale);

                    distortion = Vector3.Scale(distortion, decelerationScale);

//#if DEBUG
//                    AniMorph.Logger.LogDebug($"Deceleration:reverseMoment:dot[{decelerationDot:F3}] squashAmount[{distortionAmount:F3}] totalDeceleration[{totalDeceleration}]" +
//                        $"perpendicularScale({perpendicularScale.x:F3},{perpendicularScale.y:F3},{perpendicularScale.z:F3}) " +
//                        $"decelerationScale({decelerationScale.x:F3},{decelerationScale.y:F3},{decelerationScale.z:F3})" +
//                        $"");
//#endif
                }
                //#if DEBUG
                //                else
                //                {
                //                    AniMorph.Logger.LogDebug($"Deceleration:dot[{decelerationDot}]");
                //                }
                //#endif
            }
            if (config.sclPreserveVolume)
            {
                // Preserve original volume
                var stretchVolume = distortion.x * distortion.y * distortion.z;
                var volumeCorrection = Mathf.Pow(_baseScaleVolume / stretchVolume, (1f / 3f));
                distortion *= volumeCorrection;
            }

            var finalScale = Vector3.Lerp(prev.scale, distortion, deltaTime * config.sclLerpSpeed);
            //#if DEBUG
            //            if (!acceleration && !deceleration)
            //            {
            //                AniMorph.Logger.LogDebug($"stretch({finalScale.x:F3},{finalScale.y:F3},{finalScale.z:F3})" +
            //                //    $"stretchVolume[{stretch.x + stretch.y + stretch.z}] " +
            //                //    $"finalScale({finalScale.x:F3},{finalScale.y:F3},{finalScale.z:F3}) " +
            //                //    $"finalVolume[{finalScale.x + finalScale.y + finalScale.z}]");
            //                "");
            //            }
            //#endif
            prev.scale = finalScale;
            return finalScale;
        }


        #endregion


        protected Vector3 GetGravityPositionOffset(ref MotionConfig config, float dotUp, float dotR)
        {
            var result = Vector3.Lerp(config.gravityUpMid, dotUp > 0f ? config.gravityUpUp : config.gravityUpDown, Mathf.Abs(dotUp));

            result += Vector3.Lerp(config.gravityRightMid, dotR > 0f ? config.gravityRightUp : config.gravityRightDown, Mathf.Abs(dotR));

            //AniMorph.Logger.LogDebug($"GravityPosOffset:dotUp[{dotUp:F3}] dotR[{dotR:F3}] result({result.x:F3},{result.y:F3},{result.z:F3})");

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dotFwd"></param>
        /// <returns>Offset for flat addition to the scale.</returns>
        protected Vector3 GetGravityScaleOffset(ref MotionConfig config, float dotFwd)
        {
            var result = Vector3.Lerp(config.gravityForwardMid, dotFwd > 0f ? config.gravityForwardUp : config.gravityForwardDown, Mathf.Abs(dotFwd));
//#if DEBUG
//            AniMorph.Logger.LogDebug($"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}:dotFwd[{dotFwd:F3}] result({result.x:F3},{result.y:F3},{result.z:F3})");
//#endif

            return result;
        }

        private float _sidewaysAngleLimit = 20f;
        //private Quaternion _upRotation = Quaternion.Euler(90f, 0f, 0f);
        //private Quaternion _downRotation = Quaternion.Euler(-90f, 0f, 0f);


        protected Vector3 GetGravityAngularOffset(float masterDotFwd, float masterDotRight)
        {
            var dotFwd = masterDotFwd; // Vector3.Dot(Bone.forward, Vector3.up);
            var dotRight = masterDotRight; // Vector3.Dot(Bone.right, Vector3.up);

            //var absDotFwd = Math.Abs(dotFwd);
            //var absDotRight = Math.Abs(dotRight);
            var angleLimit = _sidewaysAngleLimit;

            // A way to reduce angle spread when lying face up.
            if (dotFwd > 0f) dotFwd *= 0.33f;

            if (isLeftSide) angleLimit = -angleLimit;

            var result = new Vector3(0f, (angleLimit * (dotFwd + dotRight)), 0f);

            //var boneUp = Bone.up;

            ////var boneRotation = Bone.rotation;

            ////var deltaEuler = (boneRotation * Quaternion.Inverse(_upRotation)).eulerAngles;

            ////var deltaAngleY = Mathf.DeltaAngle(0f, deltaEuler.y);

            ////var deviationY = Mathf.Min(_angleLimitRad, Mathf.Abs(deltaAngleY));

            ////if (deltaAngleY < 0f) deviationY = -deviationY;

            //var lookRot = Quaternion.LookRotation(-Vector3.up, boneUp);
            ////var result = new Vector3(0f, deviationY * masterFwdDot, 0f);
            //AniMorph.Logger.LogDebug($"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}:dotFwd[{dotFwd:F3}] dotRight[{dotRight:F3}] result({result.x:F3},{result.y:F3},{result.z:F3})");
            return result;
        }



        #region OnHooks

        internal void OnChangeAnimator()
        {
            var bone = transform;

            ref var previous = ref this.previous;
            ref var config = ref this.config;
            var pos = bone.position;
            previous.position = pos;
            previous.cleanPos = pos;
            previous.posModifier = Vector3.zero;
            previous.rotModifier = Vector3.zero;
            previous.adjustedRot = bone.rotation;
            //previous.cleanRot = bone.rotation;
            previous.localRot = bone.localRotation;
            previous.velocity = Vector3.zero;
            previous.angularVelocity = Vector3.zero;
            previous.scale = Vector3.one;
            config.sclAccumulatedAcceleration = 0f;
            config.scaleAccumulatedDeceleration = 0f;
            abmxModifierData.Clear();

            if (_isAnimatedRotation)
            {
                // Added in ABMX 5.4+
                var boneController = transform.GetComponentInParent<BoneController>();
                if (boneController != null)
                {
                    abmxModifier ??= boneController.CollectBaselineOnUpdate(transform.name, BoneLocation.Unknown, KKABMX.Core.Baseline.Rotation);
                }
            }
        }


        internal virtual void OnSettingChanged(AniMorphPlugin.Body body, ChaControl chara)
        {
#if DEBUG
            AniMorphPlugin.Logger.LogDebug($"[{transform.name}] - {GetType().Name}.OnSettingChanged: [{chara.name}:{body}]");
#endif
            ref var config = ref this.config;

            var pluginConfig = AniMorphPlugin.ConfigDic[body];

            config.effects = pluginConfig.Effects.Value;
            config.posSpring = pluginConfig.PosSpring.Value;
            config.posDamping = config.posSpring * pluginConfig.PosDamping.Value;
            config.posGravityForce = new Vector3(0f, pluginConfig.PosGravity.Value, 0f);
            config.useLinearGravity = pluginConfig.PosGravity.Value != 0f;
            //SetLinearMassMultiplier = pluginConfig.LinearMass.Value;

            config.rotSpring = pluginConfig.RotSpring.Value;
            config.rotDamping = config.rotSpring * pluginConfig.RotDamping.Value;
            config.rotMaxAngle = pluginConfig.RotMaxAngle.Value;
            config.rotRate = pluginConfig.RotRate.Value;

            config.sclAccelerationStrength = pluginConfig.ScaleAccelerationFactor.Value;
            config.sclDecelerationStrength = pluginConfig.ScaleDecelerationFactor.Value;
            config.sclLerpSpeed = pluginConfig.ScaleLerpSpeed.Value;
            config.sclMaxDistortion = pluginConfig.ScaleMaxDistortion.Value;
            config.sclAxisDistribution = pluginConfig.ScaleUnevenDistribution.Value;
            config.sclPreserveVolume = pluginConfig.ScalePreserveVolume.Value;
            config.sclDumbAcceleration = pluginConfig.ScaleDumbAcceleration.Value;

            if (tether != null)
            {
                tether.multiplier = -1000 * pluginConfig.TetheringMultiplier.Value;
                tether.frequency = pluginConfig.TetheringFrequency.Value;
                tether.damping = pluginConfig.TetheringDamping.Value;
                tether.maxAngle = pluginConfig.TetheringMaxAngle.Value;
            }

            config.gravityUpUp = pluginConfig.GravityUpUp.Value;
            config.gravityUpMid = pluginConfig.GravityUpMid.Value;
            config.gravityUpDown = pluginConfig.GravityUpDown.Value;
            // Scale uses vector multiplication rather then addition.
            config.gravityForwardUp = Vector3.one + pluginConfig.GravityFwdUp.Value;
            config.gravityForwardMid = Vector3.one + pluginConfig.GravityFwdMid.Value;
            config.gravityForwardDown = Vector3.one + pluginConfig.GravityFwdDown.Value;
            config.gravityRightUp = pluginConfig.GravityRightUp.Value;
            config.gravityRightMid = pluginConfig.GravityRightMid.Value;
            config.gravityRightDown = pluginConfig.GravityRightDown.Value;

            Active = config.effects != 0;

            OnChangeAnimator();
            OnSetClothesState(body, chara);


            switch (body)
            {
                case Body.Butt:
                    config.posLimitPositive = new Vector3(1f, 1.33f, 1f);
                    config.posLimitNegative = new Vector3(1f, 0.67f, 1f);
                    break;
                default:
                    config.posLimitPositive = Vector3.one;
                    config.posLimitNegative = Vector3.one;
                    break;

            }


            config.linearMaxVelocityLen = body switch
            {
                Body.Breast => 0.01f,
                _ => 1f
            };
            config.linearMaxSqrVelocity = Mathf.Sqrt(config.linearMaxVelocityLen);
        }


        internal void OnSetClothesState(AniMorphPlugin.Body body, ChaControl chara)
        {
            var pluginConfig = AniMorphPlugin.ConfigDic[body];

            var settingValue = pluginConfig.DisableWhenClothes.Value;

            if (settingValue == 0 || chara.objClothes == null) return;

            var slotList = new List<int>();

            foreach (var enumValue in AniMorphPlugin.ClothesKindValues)
            {
                var activeSlot = (settingValue & enumValue) != 0;

                if (!activeSlot) continue;

                slotList.Add(GetPower((int)enumValue));
            }

            foreach (var slot in slotList)
            {
                if (chara.objClothes.Length >= slot
                    && chara.objClothes[slot] != null
                    && chara.fileStatus.clothesState.Length >= slot
                    && chara.fileStatus.clothesState[slot] == 0)
                {
                    Active = false;
                    return;
                }
            }
            var wasActive = Active;
            Active = pluginConfig.Effects.Value != 0;

            if (!wasActive && Active) OnChangeAnimator();
        }


        #endregion


        protected void UpdateAngularApplication(AniMorphPlugin.Axis enumValue)
        {
            foreach (AniMorphPlugin.Axis value in Enum.GetValues(typeof(AniMorphPlugin.Axis)))
            {
                // 1 if true, 0f if false so we can simply multiply it.
                AngularApplication[GetPower((int)value)] = ((enumValue & value) != 0) ? 1f : 0f;
            }
        }

        // Find what power of 2 the number is.
        private int GetPower(int number)
        {
            var result = 0;

            while (number > 1)
            {
                number >>= 1;
                result++;
            }
            return result;
        }

        /// <summary>
        /// Mathf.SmoothStep but limited to 0..1f.
        /// </summary>
        protected float CheapStep(float t) => t * t * (3f - 2f * t);

        /// <summary>
        /// Mathf.SmoothStep but limited to 0..1f and done through cosine.
        /// </summary>
        protected float NeatStep(float t) => 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI);

        protected float EaseIn(float t) => t * t * (2f - t);
        protected Vector3 CheapStep(Vector3 t) => new(CheapStep(t.x), CheapStep(t.y), CheapStep(t.z));
        protected Vector3 NeatStep(Vector3 t) => new(NeatStep(t.x), NeatStep(t.y), NeatStep(t.z));
        protected Vector3 EaseIn(Vector3 t) => new(EaseIn(t.x), EaseIn(t.y), EaseIn(t.z));
        float EaseOutQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return 1 - (1 - t) * (1 - t); // Ease out quad
        }
        float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return 1 - Mathf.Pow(1 - t, 3); // Smoother than quad
        }
        protected Vector3 SmoothStep(Vector3 from, Vector3 to, Vector3 t) => new(
            Mathf.SmoothStep(from.x, to.x, t.x), 
            Mathf.SmoothStep(from.y, to.y, t.y), 
            Mathf.SmoothStep(from.z, to.z, t.z)
            );


        #region Types


        [Flags]
        internal enum Effect
        {
            None = 0,
            Pos = 1 << 0,
            Rot = 1 << 1,
            Tether = 1 << 2,
            Accel = 1 << 3,
            Decel = 1 << 4,
            GravPos = 1 << 5,
            GravRot = 1 << 6,
            GravScl = 1 << 7,
        }

        protected struct MotionConfig()
        {
            internal Effect effects;

            internal float posSpring = 25f;
            internal float posDamping = 10f;
            internal float massInv = 1f;
            internal Vector3 posGravityForce;
            internal bool useLinearGravity;
            internal Vector3 posLimitPositive;
            internal Vector3 posLimitNegative;

            internal float rotSpring = 30f;
            internal float rotDamping = 5f;
            internal float rotMaxAngle = 45f;
            internal float rotRate = 2f;

            // How much the scale stretches along velocity direction.
            internal float sclAccelerationStrength = 40f; //0.01f;
                                                            // How much to squash along deceleration axis
            internal float sclDecelerationStrength = 0.5f;
            internal Vector3 sclAxisDistribution = new Vector3(0.67f, 0.5f, 0.33f);
            // How fast squash reacts
            internal float sclLerpSpeed = 10f;
            // Max squash on deceleration
            internal float sclMaxDistortion = 0.4f;
            internal bool sclPreserveVolume;
            internal bool sclDumbAcceleration;
            internal float sclAccumulatedAcceleration;
            internal float scaleAccumulatedDeceleration;


            // When Dot(Bone.up, Vector3.up) points in up/middle/down direction.
            internal Vector3 gravityUpUp = Vector3.zero;
            internal Vector3 gravityUpMid = new Vector3(0f, 0.02f, 0f);
            internal Vector3 gravityUpDown = new Vector3(0f, 0.05f, 0f);

            // When Dot(Bone.forward, Vector3.up) points in up/middle/down direction.
            internal Vector3 gravityForwardUp = new Vector3(0.075f, 0.075f, -0.15f);
            internal Vector3 gravityForwardMid;
            // Half of Z volume are perpendicular axes, half is subcutaneous fat from all around.
            internal Vector3 gravityForwardDown = new Vector3(-0.05f, -0.05f, 0.2f);

            // When Dot(Bone.right, Vector3.up) points in up/middle/down direction.
            internal Vector3 gravityRightUp = new Vector3(-0.025f, -0.02f, 0f);
            internal Vector3 gravityRightMid;
            internal Vector3 gravityRightDown = new Vector3(0.025f, -0.02f, 0f);



            internal float mass = 1f;
            internal float linearMaxVelocityLen = 1f;
            internal float linearMaxSqrVelocity = 1f;
        }

        protected struct PreviousFrame
        {
            // Snapshots of previous frame
            internal Vector3 velocity;
            internal Vector3 angularVelocity;
            internal Vector3 position;
            internal Vector3 cleanPos;
            //internal Vector3 localAdjVec;
            internal Vector3 scale;
            // Pseudo previous as we use dead reckoning and have no way to know,
            // Synchronized periodically when animator changes states.
            internal Vector3 posModifier;
            // Clean rotation from that frame
            //internal Quaternion cleanRot;
            //internal Quaternion rotAdjustment;
            //internal Vector3 rotModifier;
            internal Quaternion adjustedRot;
            internal Vector3 rotModifier;

            internal Vector3 localPos;
            internal Quaternion localRot;

        }


        ///// <summary>
        ///// For indexing purpose of 'Effect' enum.
        ///// </summary>
        //protected enum RefEffect
        //{
        //    // Follow the position as if attached by a rubber spring.
        //    Linear,
        //    // Follow the rotation as if attached by a rubber spring.
        //    Angular,
        //    // Adjust the rotation based on the linear offset as if connected by tether.
        //    Tethering,
        //    // Increase the scale along the axis of acceleration, while decreasing perpendicular ones.
        //    Acceleration,
        //    // Decrease the scale along the axis of deceleration, while increasing perpendicular ones,
        //    // when the momentum reversal is in critical state.
        //    Deceleration,
        //    // Apply a position offset based on the rotation of the bone and the correlating gravity force.
        //    GravityLinear,
        //    // Apply a rotation offset based on the rotation of the bone and the correlating gravity force.
        //    GravityAngular,
        //    // Apply a scale offset based on the rotation of the bone and the correlating gravity force.
        //    GravityScale
        //}


        #endregion
    }
}
