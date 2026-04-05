using ADV.Commands.Base;
using ADV.Commands.Camera;
using HarmonyLib;
using KKABMX.Core;
using RootMotion;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static AniMorph.AniMorphEffector;
using static AniMorph.AniMorphPlugin;
using static RootMotion.FinalIK.Grounding;
using Random = UnityEngine.Random;

namespace AniMorph
{
    internal class MotionModifier
    {
        private static readonly Vector3 vecOne = Vector3.one;

        protected readonly Transform transform;
        protected readonly Tethering tether;
        protected readonly bool isLeftSide;
        protected readonly BoneModifierData abmxModifierData;
        protected Vector3 posApplication = Vector3.one;
        protected Vector3 rotApplication = Vector3.one;
        protected Vector3 sclApplication = Vector3.one;

        protected bool active;
        protected BoneModifier abmxModifier;
        // Big struct, only ref access.
        protected MotionConfig config = new();
        // Big struct, only ref access.
        protected PreviousFrame previous = new();


        private readonly float _baseScaleVolume;
        private readonly float _baseScaleMagnitude;
        private readonly bool _isAnimatedRotation;

        private BoneModifierData _combineModifiersCachedReturn;



        private float devVelInf = 0.05f;


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






        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="centeredBone">A centered bone with normal orientation on the body. Required for setup only.</param>
        /// <param name="bone">Bone that will be modified.</param>
        /// <param name="bakedMesh">Baked skinned mesh</param>
        /// <param name="skinnedMesh"></param>
        internal MotionModifier(BoneConfig cfg, Transform bone, Transform centeredBone, BoneModifierData boneModifierData, bool isAnimatedRotation)
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

            posApplication = cfg.posApplication;
            rotApplication = cfg.rotApplication;
            sclApplication = cfg.sclApplication;

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


        #region Update Cycle


        internal void OnUpdate()
        {
            // Grab modified local orientations from previous frame,
            // local orientations are just fields with Vector3, extremely cheap to access.
            ref var prev = ref previous;
            prev.localPos = transform.localPosition;
            prev.localRot = transform.localRotation;
        }

        internal virtual void UpdateModifier(float dt, float dtInv, float animLenInv)
        {
            if (!active) return;

            ref var config = ref this.config;
            ref var prev = ref previous;

            var posOffset = Vector3.zero;
            var rotOffset = Vector3.zero;
            var sclOffset = Vector3.one;

            // Apply linear offset, its calculations are necessary to other methods even if the offset itself isn't.
            posOffset = GetPosOffset(ref config, ref prev, dt, dtInv, animLenInv, out var velocity, out var velocityLen, out var accel);

            // Remove linear offset if setting
            if ((config.effects & Effect.Pos) == 0)
                posOffset = Vector3.zero;

            // Apply angular offset
            if ((config.effects & Effect.Rot) != 0)
                rotOffset = GetRotOffset(ref config, ref prev, dt, dtInv, animLenInv);

            // Not allowed axes are multiplied by zero, allowed by one.

            // Apply acceleration scale distortion
            //var scaleModifier = GetScaleOffset(
            //    ref config,
            //    ref prev,
            //    velocity, velocityMagnitude, deltaTime, deltaTimeInv,
            //    (config.effects & Effect.Accel) != 0,
            //    (config.effects & Effect.Decel) != 0
            //    );
            if ((config.effects & Effect.Scl) != 0)
                sclOffset = GetScaleOffset(ref config, ref prev, velocity, velocityLen, dt, dtInv);

            if ((config.effects & Effect.Tether) != 0)
                rotOffset += tether.GetTetheringOffset(velocity, dt);

            var dotUp = Vector3.Dot(transform.up, Vector3.up);
            var dotR = Vector3.Dot(transform.right, Vector3.up);
            var dotFwd = Vector3.Dot(transform.forward, Vector3.up);

            // Apply gravity position offset
            if ((config.effects & Effect.GravPos) != 0)
                posOffset += GetGravityPositionOffset(ref config, dotUp, dotR);
            // Apply gravity scale offset
            if ((config.effects & Effect.GravScl) != 0)
                sclOffset = Vector3.Scale(sclOffset, GetGravityScaleOffset(ref config, dotFwd));
            // Apply gravity rotation offset
            if ((config.effects & Effect.GravRot) != 0)
                rotOffset += GetGravityAngularOffset(dotFwd, dotR);


            posOffset = Vector3.Scale(posOffset, posApplication);
            rotOffset = Vector3.Scale(rotOffset, rotApplication);
            sclOffset = Vector3.Scale(sclOffset, sclApplication);

            var boneModifierData = abmxModifierData;

            boneModifierData.PositionModifier = posOffset;
            boneModifierData.RotationModifier = rotOffset;
            boneModifierData.ScaleModifier = sclOffset;

            prev.velocity = velocity;
            prev.posOffset = posOffset;
            prev.rotOffset = rotOffset;
        }


        #endregion


        #region Shared Functions


        private Vector3 GetNoiseVec(int octaves, Vector3 noiseVec, float ampl, float freq, out Vector3 cfgNoiseVec)
        {
            //var ampl = noiseAmpl * animLenInv;
            //var freq = cfg.noiseFreq;
            //var noiseVec = cfg.noiseVec;

            cfgNoiseVec = new(noiseVec.x + freq, noiseVec.y + freq, noiseVec.z + freq);

            var x = 0f;
            var y = 0f;
            var z = 0f;

            for (var i = 0; i < octaves; i++)
            {
                x += (Mathf.PerlinNoise(noiseVec.x + freq, 0f) - 0.5f) * ampl;
                y += (Mathf.PerlinNoise(0f, noiseVec.y + freq) - 0.5f) * ampl;
                z += (Mathf.PerlinNoise(noiseVec.z + freq, noiseVec.z + freq) - 0.5f) * ampl;

                ampl *= 0.5f;
                freq *= 2f;
            }
            noiseVec = new Vector3(x, y, z);

            return noiseVec;
        }


        private float FastExp(float t)
        {
            // Almost free and accurate for our ranges Math.Exp().
            var u = t * t;
            return 1f + t + u * (0.5f + u * 0.144f);
        }


        #endregion


        #region Position


        protected Vector3 GetCleanDeltaPos(ref PreviousFrame prev)
        {
            // Orientations can be contaminated if the bone isn't reset by animator on each frame,
            // here we figure out what kind of a bone we are dealing with
            // and extract clean measurements without our contaminations.

            // If someone else will try to adjust dynamically the same bone,
            // those calculations will fall apart for bones that aren't reset by the animator.
            // Can be fixed with increased sampling through out the frame,
            // but currently there is no such need, so we avoid extra convolution.

            // Early Update() to ~12000 order LateUpdate() state comparison.
            var localPos = transform.localPosition;
            var localPosOnUpdate = prev.localPos;

            var x = localPos.x != localPosOnUpdate.x;
            var y = localPos.y != localPosOnUpdate.y;
            var z = localPos.z != localPosOnUpdate.z;

            Vector3 cleanDeltaPos;
            Vector3 cleanPos;
            var currPos = transform.position;

            // --- Delta position ---
            if (x && y && z)
            {
                // Bone is reset each frame by the animator.
                cleanPos = currPos;
                cleanDeltaPos = transform.InverseTransformPoint(prev.cleanPos);

            }
            else if (!x && !y && !z)
            {
                // Bone isn't reset by the animator.
                cleanPos = transform.TransformPoint(-prev.posOffset);
                cleanDeltaPos = transform.InverseTransformDirection(prev.cleanPos - cleanPos);
            }
            else
            {
                // The Illusion way.
                cleanPos = transform.TransformPoint(-prev.posOffset);
                var vecDynamic = transform.InverseTransformPoint(prev.position);
                var vecStatic = transform.InverseTransformDirection(prev.cleanPos - cleanPos);

                cleanDeltaPos = new Vector3(
                    x ? vecDynamic.x : vecStatic.x,
                    y ? vecDynamic.y : vecStatic.y,
                    z ? vecDynamic.z : vecStatic.z
                    );
            }
            AniMorphPlugin.Logger.LogDebug($"[{transform.name}] " +
                $"dynamic({x},{y},{z}) cleanDeltaPos({cleanDeltaPos.x:F3},{cleanDeltaPos.y:F3},{cleanDeltaPos.z:F3})");
            prev.cleanPos = cleanPos;
            prev.position = currPos;

            return cleanDeltaPos;
        }

        private bool IsPosShock(ref MotionConfig cfg, ref PreviousFrame prev, Vector3 cleanDeltaPos, float dtInv, float animLenInv)
        {
            // A simple velocity, based on movements of a transform without our interference.
            var cleanVelDelta = cleanDeltaPos - prev.cleanPosVel;
            
            var projectionDot = Vector3.Dot(cleanVelDelta, prev.cleanPosVelDelta);

            prev.cleanPosVelDelta = cleanVelDelta;

            prev.cleanPosVel = cleanDeltaPos;

            if (prev.bleedTime > 0f) return false;

            // --- Shock detection --- 
            var len = cleanVelDelta.magnitude * dtInv;

            if (len > cfg.posShockThreshold || projectionDot < 0f)
            {
                //var shockPower = Mathf.Pow(Mathf.Sqrt(cleanVelDeltaSqrLen), 1.2f);
                //velocity += cleanVelDelta.normalized * shockPower * shockFactor;
                prev.velocity += cleanVelDelta * cfg.posShockStr;

                // TODO Add slowdown instead of freeze?
                prev.bleedTime = cfg.posBleedLen * animLenInv;

                if (len > cfg.posFreezeThreshold || projectionDot < 0f)
                {
                    prev.shockTime = cfg.posFreezeLen * animLenInv;
#if DEBUG_POS
                    AniMorphPlugin.Logger.LogError($"[{transform.name}] " +
                    $"len[{len:F3}] deltaTimeInv[{deltaTimeInv:F0}]" +
                    $"dot < 0[{projectionDot < 0f}]");
#endif
                }
#if DEBUG_POS
                else
                {
                    AniMorphPlugin.Logger.LogWarning($"[{transform.name}] " +
                    $"len[{len:F3}] deltaTimeInv[{deltaTimeInv:F0}]" +
                    $"dot < 0[{projectionDot < 0f}]");
                }
#endif
                return true;
            }
            return false;
        }

        protected Vector3 GetPosOffset(ref MotionConfig cfg, ref PreviousFrame prev, float dt, float dtInv, float animLenInv,            
            out Vector3 velocity, out float velocityLen, out Vector3 accel)
        {
            var cleanDeltaPos = GetCleanDeltaPos(ref prev);

            // --- Shock state ---
            if (prev.shockTime > 0f)
            {
                prev.shockTime -= dt;
                // Continue tracking of velocities between frames even if we froze for a bit.
                prev.cleanPosVel = cleanDeltaPos;

                velocity = prev.velocity;
                velocityLen = velocity.magnitude;
                accel = Vector3.zero;

                return cleanDeltaPos - velocity;
            }

            // --- Normal state ---
            else if (IsPosShock(ref cfg, ref prev, cleanDeltaPos, dtInv, animLenInv))
            {
                velocity = prev.velocity;
                velocityLen = velocity.magnitude;
                accel = Vector3.zero;

                return cleanDeltaPos - velocity;
            }
            velocity = prev.velocity;
            // --- Bleed velocity ---
            if (prev.bleedTime > 0f)
            {
                prev.bleedTime -= dt;

                velocity *= FastExp(-cfg.posBleedStr * dt);
            }

            //var inheritedVel = cleanDeltaPos * (dtInv * devVelInf);
            var springForce = -cfg.posSpring * dtInv * cleanDeltaPos;
            var dampingForce = -cfg.posDamping * velocity;

            accel = /*inheritedVel +*/ springForce + dampingForce;

            if ((cfg.noiseAff & NoiseAffliction.Pos) != 0)
                accel += GetNoiseVec(cfg.noiseOctaves, cfg.noiseVecPos, cfg.noiseAmplPos * animLenInv, cfg.noiseFreq * dt, out cfg.noiseVecPos);

            accel *= (cfg.massInv * dt);

            velocity += accel;

            //var appliedVelocity = Vector3.Scale(posApplication, velocity);
            // --- Velocity clamp ---


            velocityLen = velocity.magnitude;
            //var maxVelocityLen = config.linearMaxVelocityLen;

            //if (velocityLen > maxVelocityLen)
            //{
            //    velocity = (velocity / velocityLen) * maxVelocityLen;
            //    velocityLen = maxVelocityLen;
            //}

            return cleanDeltaPos - velocity;
        }


#endregion


        #region Rotation


        // (20 .. 40)
        public float slowSmoothing = 25f;
        // (5 .. 12)
        public float fastSmoothing = 8f;
        // (0.1 .. 0.3)
        public float highFrequencyInfluence = 0.2f;

        private Vector3 slowFilteredDelta;
        private Vector3 fastFilteredDelta;

        private Vector3 devPrevRotDelta;
        private float devRotFreeze;
        private float devRotFreezeTime = 0.1f;
        protected virtual Vector3 GetRotOffset(ref MotionConfig cfg, ref PreviousFrame prev, float dt, float dtInv, float animLenInv)
        {
            var currRot = transform.rotation;
            var isDynamic = transform.localRotation != prev.localRot;

            if (!isDynamic)
            {
                currRot = currRot * Quaternion.Inverse(Quaternion.Euler(prev.rotOffset));
            }
            var delta = currRot * Quaternion.Inverse(prev.adjustedRot);


            delta.ToAngleAxis(out var angle, out var axis);

            // --- Filter corruption ---
            var isInfAxis = float.IsInfinity(axis.x);

            if (angle > 180f)
                angle -= 360f;

            var absAngle = Mathf.Abs(angle);

            var accel = Vector3.zero;

            var angVel = prev.torque;

            if (devRotFreeze > 0f)
            {
                devRotFreeze -= dt;

                accel -= cfg.rotDamping * angVel;

                angVel += accel * dt;

                prev.torque = angVel;

                return (Quaternion.Inverse(currRot) * prev.adjustedRot).eulerAngles;
            }

            var angleExp = FastExp(absAngle * dt);

            if (!isInfAxis)
            {
                var deltaAngVel = axis * (angle * angleExp * dtInv);

                var slowLerp = 1f - FastExp(-slowSmoothing * dt);
                var fastLerp = 1f - FastExp(-fastSmoothing * dt);

                slowFilteredDelta = Vector3.Lerp(slowFilteredDelta, deltaAngVel, slowLerp);
                fastFilteredDelta = Vector3.Lerp(fastFilteredDelta, deltaAngVel, fastLerp);

                var highFreqDelta = fastFilteredDelta - slowFilteredDelta;

                var targetVel = Vector3.Lerp(
                    slowFilteredDelta,
                    fastFilteredDelta,
                    highFrequencyInfluence
                );

                //accel = cfg.rotSpring * (targetVel - angVel);
                accel = cfg.rotSpring * targetVel;
            }

            accel -= cfg.rotDamping * FastExp(-absAngle * dt) * angVel;

            if ((cfg.noiseAff & NoiseAffliction.Rot) != 0)
                accel += GetNoiseVec(cfg.noiseOctaves, cfg.noiseVecRot, cfg.noiseAmplRot * animLenInv * angleExp, cfg.noiseFreq * dt, out cfg.noiseVecRot);

            angVel += accel * dt;



            var angVelDot = Vector3.Dot(angVel, prev.torque);

            if (prev.highTorque)
            {
                if (angVelDot < 1f)
                {
                    prev.highTorque = false;
                    var freezeTime = absAngle * (1f / 45f);
                    devRotFreeze = devRotFreezeTime * (freezeTime * freezeTime);
                    //Time.timeScale = 0f;

                    AniMorphPlugin.Logger.LogWarning($"[{transform.name}]: GetRotOffset: " +
                        $"devRotFreeze[{devRotFreeze:F3} absAngle[{absAngle:F3}] angVel{angVel}");
                }
            }
            else if (angVelDot > 1000f)
            {
                prev.highTorque = true;
            }

            //var rot = angVelocityLen == 0f ?
            //    prev.adjustedRot :
            //    Quaternion.AngleAxis(angVelocityLen * dt * cfg.rotRate, angVel * (1f / angVelocityLen)) * prev.adjustedRot;
            var newRot = Quaternion.Euler(angVel * dt * config.rotRate) * prev.adjustedRot;


            //previous.cleanRot = cleanRot
            prev.adjustedRot = newRot;
            prev.torque = angVel;

            var result = Quaternion.Inverse(currRot) * newRot;

            if (absAngle > 45f)
            {
                var resultAngle = Quaternion.Angle(Quaternion.identity, result);

                if (resultAngle > 45f)
                {
                    result = Quaternion.Slerp(Quaternion.identity, result, 45f / resultAngle);
                    prev.adjustedRot = currRot * result;
                    AniMorphPlugin.Logger.LogWarning($"[{transform.name}] - Angle Clamp absAngle[{absAngle:F3}] resultAngle[{resultAngle:F3}]");
                }
                else
                    AniMorphPlugin.Logger.LogError($"[{transform.name}] - NOT Angle Clamp absAngle[{absAngle:F3}] resultAngle[{resultAngle:F3}]");
            }
            var resultEuler = result.eulerAngles;

            AniMorphPlugin.Logger.LogDebug($"[{transform.name}] GetRotOffset: " +
                $"result{resultEuler} angVel{angVel} rotDot[{angVelDot:F3}]"); 

            //if (rotDot < 0f) Time.timeScale = 0f;

            return resultEuler;
        }


        #endregion


        #region Scale

        private class DevScl
        {
            internal Vector3 vel;
            internal Vector3 velNorm;
            internal Vector3 accelVec;
            internal Vector3 absVelNorm;
            internal Vector3 distort;
            internal float accelDot;
            internal float totalAccel;
            internal float totalDecel;
            internal float distortValue;

        }

        private readonly DevScl devScl = new();
        private SquashMode _squashMode;
        private float _squashBlend = 0.5f;
        private Vector3 scaleVelocity;
        private float squashDamping = 10f;
        public float squashStrength = 0.025f;
        public float maxStretch = 1.8f;
        public float maxSquash = 0.6f;
        private Vector3 _initScale;

        private float devSclDeadZone = 0.05f;
        private Vector3 devSclPrevDir;
        private float devSclDicSharpness = 10f;

        protected Vector3 GetSquashOffset(ref MotionConfig config, ref PreviousFrame prev, Vector3 vel, Vector3 accel, float velLen, float deltaTime, float deltaTimeInv)
        {

            var driver = _squashMode switch
            {
                SquashMode.Accel => accel,
                SquashMode.Velocity => vel,
                SquashMode.Blend => Vector3.Lerp(accel, vel, _squashBlend),
                _ => accel
            };

            var magnitude = driver.magnitude;

            magnitude = Mathf.Max(0f, magnitude - devSclDeadZone);

            if (magnitude < (0.0001f))// * 0.0001f))
            {
                return Vector3.SmoothDamp(
                    previous.scale,
                    Vector3.one,
                    ref scaleVelocity,
                    1f / squashDamping
                );
            }
            /* Separate stretch & recovery speeds
             * 
             * float stretchSpeed = 12f;
             * float recoverySpeed = 6f;
             * 
             * float speed = (targetScale.magnitude > currentScale.magnitude)
             * ? stretchSpeed
             * : recoverySpeed;
             * 
             * 
             */


            var dir = Vector3.Lerp(devSclPrevDir, driver, deltaTime * devSclDicSharpness);
            devSclPrevDir = dir;
            dir = dir.normalized;

            //float stretch = 1f + Mathf.Clamp(magnitude * squashStrength, 0f, maxStretch);
            var response = Mathf.Pow(magnitude * squashStrength, 1.3f);
            var stretch = 1f + Mathf.Clamp(response, 0f, maxStretch);
            var squash = Mathf.Clamp(1f / Mathf.Sqrt(stretch), maxSquash, 1f);
            // Exaggerated
            //var squash = Mathf.Clamp(1f / Mathf.Pow(stretch, 0.6f), maxSquash, 1f);


            Vector3 targetScale = GetDirectionalScale(dir, stretch, squash);

            return Vector3.SmoothDamp(
                previous.scale,
                targetScale,
                ref scaleVelocity,
                1f / squashDamping
            );

        }
        Vector3 GetDirectionalScale(Vector3 direction, float stretch, float squash)
        {
            Vector3 localDir = direction.normalized;

            Vector3 scale = new Vector3(
                Mathf.Lerp(squash, stretch, Mathf.Abs(localDir.x)),
                Mathf.Lerp(squash, stretch, Mathf.Abs(localDir.y)),
                Mathf.Lerp(squash, stretch, Mathf.Abs(localDir.z))
            );

            return scale;
        }
        private bool devAccel = true;
        private bool devDecel;

        protected Vector3 GetScaleOffset(ref MotionConfig cfg, ref PreviousFrame prev, Vector3 vel, float velLen, float dt, float dtInv)
        {
            if (velLen == 0f) return Vector3.one;

            var scl = devScl;
            // Normalize velocity
            scl.velNorm = vel / velLen;

            scl.absVelNorm = new Vector3(Mathf.Abs(scl.velNorm.x), Mathf.Abs(scl.velNorm.y), Mathf.Abs(scl.velNorm.z));
            scl.accelVec = (vel - prev.velocity) * dtInv;
            // Project acceleration onto direction,
            // projection dot lesser than 0 means deceleration, otherwise acceleration.
            scl.accelDot = Vector3.Dot(scl.velNorm, scl.accelVec);

            // Init distortion as neutral
            scl.distort = Vector3.one;

            // Proper acceleration with accumulation or without
            // looks worse then a dumb magnitude based implementation.
            // But hey it's an option.

            if (devAccel)
            {
                scl.distortValue = cfg.sclAccelStr;

                if (cfg.sclDumbAccel)
                {
                    scl.distortValue *= velLen * dtInv;
                }
                else
                {
                    // Accumulate acceleration, deltaTime is a passive drain to avoid awkward accumulations in some idle animations.
                    scl.totalAccel = Mathf.Clamp01(cfg.sclTotalAccel + scl.accelDot - (dt * 0.1f));

                    scl.distortValue *= scl.totalAccel;

                    cfg.sclTotalAccel = scl.totalAccel;
                }

                scl.distortValue = Mathf.Clamp(scl.distortValue, 0f, cfg.sclMaxDistortion);
                // Apply distortion amount to velocity axes
                scl.distort += scl.absVelNorm * scl.distortValue;
                // Shrink axes perpendicular to the velocity 
                var perpendicularScale = Vector3.one + Vector3.Scale(scl.absVelNorm - Vector3.one, scl.distortValue * cfg.sclAxisDistribution);
                // Combine vectors
                scl.distort = Vector3.Scale(scl.distort, perpendicularScale);
//#if DEBUG
//                AniMorph.Logger.LogDebug($"Acceleration:distortion({distortion.x:F3},{distortion.y:F3},{distortion.z:F3})" +
//                    $"distortionAmount[{distortionAmount:F3}] velocityMagnitude[{(velocityMagnitude * fps):F5}" +
//                   // $"scale({perpendicularScale.x:F3},{perpendicularScale.y:F3},{perpendicularScale.z:F3})" +
//                    //$"absVelocityNorm({absVelocityNormalized.x:F3},{absVelocityNormalized.y:F3},{absVelocityNormalized.z:F3})"
//                    "");
//#endif
            }
            if (devDecel)
            {
                // Amplify deceleration as it tends to be too small.
                if (scl.accelDot < 0f) scl.accelDot *= 2f;
                // Accumulate deceleration, deltaTime is a passive drain to avoid awkward accumulations in some idle animations.
                scl.totalDecel = Mathf.Clamp01(cfg.sclTotalDecel - scl.accelDot - (dt * 0.1f));
                // Store for the next frame
                cfg.sclTotalDecel = scl.totalDecel;

                if (scl.totalDecel < 0f)
                {
                    // How much scale can deviate
                    var distortionAmount = scl.totalDecel * cfg.sclDecelStr;
                    distortionAmount = Mathf.Clamp(distortionAmount, 0f, cfg.sclMaxDistortion);
                    // Apply distortion amount to velocity axes
                    var decelerationScale = Vector3.one - (scl.absVelNorm * distortionAmount);
                    // Expand axes perpendicular to the velocity
                    var perpendicularScale = Vector3.one + Vector3.Scale((Vector3.one - scl.absVelNorm), distortionAmount * cfg.sclAxisDistribution); // * (squashAmount * 0.5f);
                    // Combine vectors
                    decelerationScale = Vector3.Scale(decelerationScale, perpendicularScale);

                    scl.distort = Vector3.Scale(scl.distort, decelerationScale);
#if DEBUG
                    AniMorphPlugin.Logger.LogDebug($"Deceleration: distortionAmount[{distortionAmount:F3}] " +
                        $"perpendicularScale({perpendicularScale.x:F2},{perpendicularScale.y:F2},{perpendicularScale.z:F2}) " +
                        $"decelerationScale({decelerationScale.x:F2},{decelerationScale.y:F2},{decelerationScale.z:F2})" +
                        $"");
#endif
                }
            }
            if (cfg.sclPreserveVolume)
            {
                // Preserve original volume
                var stretchVolume = scl.distort.x * scl.distort.y * scl.distort.z;
                var volumeCorrection = Mathf.Pow(_baseScaleVolume / stretchVolume, (1f / 3f));
                scl.distort *= volumeCorrection;
            }

            var finalScale = Vector3.Lerp(prev.scale, scl.distort, dt * cfg.sclLerpSpeed);
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


        #region Dots


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


        #endregion


        #region OnHooks

        internal void OnChangeAnimator()
        {
            var bone = transform;

            ref var prev = ref this.previous;
            ref var cfg = ref this.config;
            var pos = bone.position;
            prev.position = pos;
            prev.cleanPos = pos;
            prev.cleanPosVel = Vector3.zero;
            prev.cleanPosVelDelta = Vector3.zero;
            prev.posOffset = Vector3.zero;
            prev.rotOffset = Vector3.zero;
            prev.adjustedRot = bone.rotation;
            //previous.cleanRot = bone.rotation;
            prev.localRot = bone.localRotation;
            prev.velocity = Vector3.zero;
            prev.torque = Vector3.zero;
            prev.scale = Vector3.one;
            cfg.sclTotalAccel = 0f;
            cfg.sclTotalDecel = 0f;
            abmxModifierData.Clear();


            if (_isAnimatedRotation)
            {
                // TODO
                // Cache it?
                var boneController = transform.GetComponentInParent<BoneController>();
                if (boneController != null)
                {
                    // Added in ABMX 5.4+
                    abmxModifier = boneController.CollectBaselineOnUpdate(transform.name, BoneLocation.Unknown, KKABMX.Core.Baseline.Rotation);
                }
            }
            else if (abmxModifier == null)
            {
                var boneController = transform.GetComponentInParent<BoneController>();
                if (boneController != null)
                {
                    abmxModifier = boneController.GetModifier(bone.name, BoneLocation.BodyTop);
                }
            }
            
            if (_combineModifiersCachedReturn == null && abmxModifier != null)
            {
                var traverse = Traverse.Create(abmxModifier);

                _combineModifiersCachedReturn = traverse.Field("_combineModifiersCachedReturn").GetValue<BoneModifierData>();
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

            config.noiseOctaves = pluginConfig.NoiseOctave.Value;
            config.noiseAff = pluginConfig.NoiseAffliction.Value;
            config.noiseAmplPos = pluginConfig.NoiseAmplitudePos.Value;
            config.noiseAmplRot = pluginConfig.NoiseAmplitudeRot.Value * 100f;
            config.noiseAmplScl = pluginConfig.NoiseAmplitudeScl.Value;
            // The backward way algos are setup forces us to have weird coefficient ranges,
            // and I'd rather have ugly values(tiny decimals) hidden and instead expose usual common values,
            // that will be converted here.
            config.posSpring = (float)Math.Round(pluginConfig.PosSpring.Value * (1f / 60f), 3);
            config.posDamping = pluginConfig.PosSpring.Value * pluginConfig.PosDamping.Value;
            config.posShockThreshold = pluginConfig.PosShockThreshold.Value;
            config.posShockStr = pluginConfig.PosShockStr.Value;
            config.posFreezeThreshold = pluginConfig.PosFreezeThreshold.Value;
            config.posFreezeLen = pluginConfig.PosFreezeLen.Value;
            config.posBleedStr = pluginConfig.PosBleedStr.Value;
            config.posBleedLen = pluginConfig.PosBleedLen.Value;

            //config.posGravityForce = new Vector3(0f, pluginConfig.PosGravity.Value, 0f);
            //config.useLinearGravity = pluginConfig.PosGravity.Value != 0f;
            //SetLinearMassMultiplier = pluginConfig.LinearMass.Value;

            config.rotSpring = (float)Math.Round(pluginConfig.RotSpring.Value * (1f / 60f), 3);
            config.rotDamping = pluginConfig.RotSpring.Value * pluginConfig.RotDamping.Value;
            config.rotRate = pluginConfig.RotRate.Value;

            config.sclAccelStr = pluginConfig.ScaleAccelerationFactor.Value;
            config.sclDecelStr = pluginConfig.ScaleDecelerationFactor.Value;
            config.sclLerpSpeed = pluginConfig.ScaleLerpSpeed.Value;
            config.sclMaxDistortion = pluginConfig.ScaleMaxDistortion.Value;
            config.sclAxisDistribution = pluginConfig.ScaleUnevenDistribution.Value;
            config.sclPreserveVolume = pluginConfig.ScalePreserveVolume.Value;
            config.sclDumbAccel = pluginConfig.ScaleDumbAcceleration.Value;

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

            active = config.effects != 0;

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

            // Add seed to the Perlin noise.
            config.noiseVecPos = new(Random.Range(0f, 1000f), Random.Range(0f, 1000f), Random.Range(0f, 1000f));
            config.noiseVecRot = new(Random.Range(0f, 1000f), Random.Range(0f, 1000f), Random.Range(0f, 1000f));
            config.noiseVecScl = new(Random.Range(0f, 1000f), Random.Range(0f, 1000f), Random.Range(0f, 1000f));
            config.noiseFreq = (float)Math.Round(1.5f * Random.Range(0.75f, 1.25f), 2);

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
                    active = false;
                    return;
                }
            }
            var wasActive = active;
            active = pluginConfig.Effects.Value != 0;

            if (!wasActive && active) OnChangeAnimator();
        }


        #endregion


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


        #region Interpolations


        float ApplyInterpolation(float t, Interp interp, float p1, float p2)
        {
            return interp switch
            {
                Interp.Smooth => t * t * (3f - 2f * t),
                Interp.Smoother => t * t * t * (10f - 15f * t + 6f * t * t),
                Interp.Linear => t,
                _ => Bezier(t, p1, p2),
            };
        }
        float GetP1(Interp interp)
        {
            return interp switch
            {
                Interp.SinePlus => 0.5f + Random.value * 0.25f,
                Interp.Sine => 0.25f + Random.value * 0.25f,
                Interp.Double => 0.25f + Random.value * 0.5f,
                _ => 0f
            };
        }
        float GetP2(Interp interp)
        {
            return interp switch
            {
                Interp.SinePlus or Interp.Sine => 1f,
                Interp.Power => Random.value * 0.5f,
                _ => 0f
            };
        }
        float Bezier(float t, float p1, float p2)
        {
            var u = 1 - t;

            return 3 * u * u * t * p1 +
                   3 * u * t * t * p2 +
                   t * t * t;
        }

        float BumpBezier(float t, float p1, float p2)
        {
            var u = 1 - t;

            return 3 * u * u * t * p1 +
                   3 * u * t * t * p2;
        }
        float BumpSmoothStep(float t) => 4f * t * t * (3f - 2f * t) * (1f - t * t * (3f - 2f * t));
        float BumpParabola(float t) => 4f * t * (1f - t);


        #endregion


        #region Types


        [Flags]
        internal enum Effect
        {
            None = 0,
            Pos = 1 << 0,
            Rot = 1 << 1,
            Tether = 1 << 2,
            Scl = 1 << 3,
            GravPos = 1 << 4,
            GravRot = 1 << 5,
            GravScl = 1 << 6,
        }

        internal enum SquashMode
        {
            Accel,
            Velocity,
            Blend,
        }

        // Ordered by the speed of gain.
        private enum Interp
        {
            // Bezier with P1(0.5..0.75) P2(1)
            // Extra steep sine.
            SinePlus,
            // Bezier with P1(0.25..0.5) P2(1)
            // Should be much cheaper then Math.Sin()
            // Comes with plenty of variability that OG sine lacks.
            Sine,
            // SmoothStep
            Smooth,
            // SmootherStep
            Smoother,
            // Plain x = y
            Linear,
            // Bezier with P1(0) P2(0..0.75)
            // Because OG power functions are too rigid in variability.
            Power,
            // Bezier with P1(0.25..0.75) P2(0)
            // Not necessarily a double, can be just an uneven weird one.
            Double,
        }

        protected struct MotionConfig()
        {
            internal Effect effects;

            internal int noiseOctaves;
            internal NoiseAffliction noiseAff;
            internal Vector3 noiseVecPos;
            internal Vector3 noiseVecRot;
            internal Vector3 noiseVecScl;
            internal float noiseAmplPos;
            internal float noiseAmplRot;
            internal float noiseAmplScl;
            internal float noiseFreq;

            internal float posSpring;
            internal float posDamping;
            internal float posShockThreshold;
            internal float posShockStr;
            internal float posFreezeThreshold;
            internal float posFreezeLen;
            internal float posBleedStr;
            internal float posBleedLen;
            //internal float posDamping = 2f * Mathf.Sqrt(posSpring * mass);
            internal float massInv = 1f;
            //internal Vector3 posGravityForce;
            //internal bool useLinearGravity;
            internal Vector3 posLimitPositive;
            internal Vector3 posLimitNegative;

            internal float rotSpring = 30f;
            internal float rotDamping = 5f;
            internal float rotRate = 2f;

            // How much the scale stretches along velocity direction.
            internal float sclAccelStr = 40f; //0.01f;
                                                            // How much to squash along deceleration axis
            internal float sclDecelStr = 0.5f;
            internal Vector3 sclAxisDistribution = new Vector3(0.67f, 0.5f, 0.33f);
            // How fast squash reacts
            internal float sclLerpSpeed = 10f;
            // Max squash on deceleration
            internal float sclMaxDistortion = 0.4f;
            internal bool sclPreserveVolume;
            internal bool sclDumbAccel;
            internal float sclTotalAccel;
            internal float sclTotalDecel;


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

        protected /*struct*/ class PreviousFrame
        {
            internal Vector3 velocity;
            internal Vector3 torque;

            internal Vector3 position;
            internal Vector3 cleanPos;
            internal Vector3 cleanPosVel;
            internal Vector3 cleanPosVelDelta;
            //internal Vector3 localAdjVec;
            internal Vector3 scale;
            // Pseudo previous as we use dead reckoning and have no way to know,
            // Synchronized periodically when animator changes states.
            internal Vector3 posOffset;
            internal Vector3 sclOffset;
            // Clean rotation from that frame
            //internal Quaternion cleanRot;
            //internal Quaternion rotAdjustment;
            //internal Vector3 rotModifier;
            internal Quaternion adjustedRot;
            internal Vector3 rotOffset;
            internal Vector3 localPos;
            internal Quaternion localRot;
            internal float shockTime;
            internal float bleedTime;
            internal bool highTorque;
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
