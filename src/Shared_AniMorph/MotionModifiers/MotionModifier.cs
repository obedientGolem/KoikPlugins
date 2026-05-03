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
using static RootMotion.FinalIK.AimPoser;
using static RootMotion.FinalIK.Grounding;
using static RootMotion.FinalIK.IKSolver;
using Random = UnityEngine.Random;

namespace AniMorph
{
    internal class MotionModifier
    {

        protected static readonly Effect[] effects = Enum.GetValues(typeof(Effect)) as Effect[];

        protected readonly Transform transform;
        protected readonly Tethering tether;
        protected readonly BoneModifierData abmxModifierData = new();

        protected readonly Vector3 vecZero = Vector3.zero;
        protected readonly Vector3 vecOne = Vector3.one;


        internal BoneModifierData GetBoneModifierData => abmxModifierData;

        protected bool active;
        protected BoneModifier _abmxModifier;
        // Big struct, only ref access.
        protected Config config;
        // Class, rare access.
        protected BaseConfig baseConfig;
        // Big struct, only ref access.
        protected Previous previous;
        // Big struct, only ref access.
        protected Current current;

        private BoneController _boneController;


        private readonly float _baseScaleVolume;
        private readonly float _baseScaleMagnitude;
        private bool _animRot;
        private int _animRotFrameCount;

        //private BoneModifierData _combineModifiersCachedReturn;

        private float devVelInf = 0.05f;


        internal void SetMass(float value)
        {
            if (value <= 0f) value = 0.01f;

            ref var cfg = ref config;
            cfg.mass = value;
            cfg.massInv = 1f / value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centeredBone">A centered bone with normal orientation on the body. Required for setup only.</param>
        /// <param name="bone">Bone that will be modified.</param>
        /// <param name="bakedMesh">Baked skinned mesh</param>
        /// <param name="skinnedMesh"></param>
        internal MotionModifier(BaseConfig baseCfg, Transform bone, Transform centeredBone)
        {
            if (bone == null) 
                throw new ArgumentNullException(nameof(bone));

            baseConfig = baseCfg;

            //baseConfig = new BaseConfig(
            //    allowedEffects: baseCfg.allowedEffects,
            //    posFactor: baseCfg.posFactor,
            //    rotFactor: baseCfg.rotFactor,
            //    sclFactor: baseCfg.sclFactor,
            //    dotFlipSign: baseCfg.dotFlipSign,
            //    dotScl_posFactor: baseCfg.dotScl_posFactor
            //    );

            config = new Config(
                posApplication: baseCfg.posApplication,
                posAppPositive: baseCfg.posAppPositive,
                posAppNegative: baseCfg.posAppNegative,
                rotApplication: baseCfg.rotApplication,
                sclApplication: baseCfg.sclApplication
                );


            current = new();
            previous = new();

            ref var cfg = ref config;
            ref var prev = ref previous;

            transform = bone;
            var pos = bone.position;
            prev.position = pos;
            prev.cleanPos = pos;
            //previous.cleanRot = bone.rotation;
            prev.localRot = bone.localRotation;
            prev.adjustedRot = Quaternion.identity;
            _baseScaleVolume = bone.localScale.x * bone.localScale.y * bone.localScale.z;
            _baseScaleMagnitude = bone.localScale.magnitude;


            if (centeredBone != null)
            {
                tether = new Tethering(centeredBone, prev.position);

                var localBonePosition = centeredBone.InverseTransformPoint(bone.position);
                var divider = Mathf.Abs(localBonePosition.x) + Mathf.Abs(localBonePosition.z);
                var result = divider == 0f ? (0f) : (localBonePosition.x / divider);
                cfg.isLeftSide = result < 0f;
            }
            else
            {
                cfg.isLeftSide = baseCfg.isLeft;
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


        #region Update Cycle


        internal virtual void OnUpdate()
        {
            // Grab modified local orientations from previous frame,
            // local orientations are simple fields, extremely cheap to access.
            ref var prev = ref previous;
            prev.localPos = transform.localPosition;
            prev.localRot = transform.localRotation;
        }

        internal virtual void UpdateModifier(float dt, float dtInv, float animLenInv)
        {
            if (!active) return;

            ref var cfg = ref config;
            ref var curr = ref current;
            ref var prev = ref previous;

            var effects = cfg.effects;

            var posOffset = Vector3.zero;
            var rotOffset = Vector3.zero;
            var sclOffset = Vector3.one;


            // --- Update Noise Params ---

            curr.noiseAmplFactor = (0.25f + Mathf.Min(0.75f, animLenInv * prev.avgCleanAdjDeltaPosLen * 15f));
            curr.noiseFreq = cfg.noiseFreq * animLenInv * dt;


            // --- Update Offsets ---

            posOffset = GetPosOffset(ref cfg, ref curr, ref prev, dt, dtInv, animLenInv, out var velocity, out var accel);

            if ((effects & Effect.Pos) == 0)
                posOffset = Vector3.zero;

            if ((effects & Effect.Rot) != 0)
                rotOffset = GetRotOffset(ref cfg, ref curr, ref prev, dt, dtInv, animLenInv);
            else
                curr.cleanLocalRot = GetCleanLocalRot(ref prev);

            if ((effects & Effect.Scl) != 0)
                sclOffset = GetSquashOffset(ref cfg, ref curr, ref prev, velocity, prev.cleanDeltaPos, dt);

            if ((effects & Effect.Tether) != 0)
                rotOffset += tether.GetTetheringOffset(velocity, dt);


            var dotUp = Vector3.Dot(transform.up, Vector3.up);
            var dotR = Vector3.Dot(transform.right, Vector3.up);
            var dotFwd = Vector3.Dot(transform.forward, Vector3.up);

            if ((effects & Effect.PosOffset) != 0)
                posOffset += GetPosDotOffset(ref cfg, ref curr, dotUp, dotR);

            if ((effects & Effect.SclOffset) != 0)
                sclOffset = Vector3.Scale(sclOffset, GetSclDotOffset(ref cfg, ref curr, dotFwd));

            if ((cfg.effects & Effect.RotOffset) != 0)
                rotOffset += GetRotDotOffset(ref cfg, ref curr, dotFwd, dotR);


            // --- Prepare Application --- 

            var posPositive = cfg.posAppPositive;
            var posNegative = cfg.posAppNegative;

            var posSignScale = new Vector3(
                posOffset.x > 0f ? posPositive.x : posNegative.x,
                posOffset.y > 0f ? posPositive.y : posNegative.y,
                posOffset.z > 0f ? posPositive.z : posNegative.z
                );

            posOffset = Vector3.Scale(posOffset, posSignScale);
            rotOffset = Vector3.Scale(rotOffset, cfg.rotApplication);

            var sclApp = cfg.sclApplication;
            sclOffset = new Vector3(
                sclApp.x < 1f ? 1f + ((sclOffset.x - 1f) * sclApp.x) : sclOffset.x * sclApp.x,
                sclApp.y < 1f ? 1f + ((sclOffset.y - 1f) * sclApp.y) : sclOffset.y * sclApp.y,
                sclApp.z < 1f ? 1f + ((sclOffset.z - 1f) * sclApp.z) : sclOffset.z * sclApp.z
                );

            if (cfg.dotScl_isPosFactor)
                posOffset += Vector3.Scale(
                    dotFwd > 0f ? cfg.dotScl_posFactorFaceUp : cfg.dotScl_posFactorFaceDown,
                    sclOffset - vecOne
                    );


            // --- Write Offsets ---

            var boneModifierData = abmxModifierData;

            boneModifierData.PositionModifier = curr.cleanLocalRot * posOffset;
            boneModifierData.RotationModifier = rotOffset;
            boneModifierData.ScaleModifier = sclOffset;


            // --- Prepare For Next Frame ---

            prev.velocity = velocity;
            prev.posOffset = posOffset;
            prev.rotOffset = rotOffset;
            prev.sclOffset = sclOffset;

            curr.needNoisePos = cfg.noiseAmplPos > 0f;
            curr.needNoiseRot = cfg.noiseAmplRot > 0f;
            curr.needNoiseScl = cfg.noiseAmplScl > 0f;
        }


        #endregion


        #region Shared Functions


        private Vector3 GetNoiseVec(int octaves, Vector3 noiseVec, float ampl, float freq, out Vector3 cfgNoiseVec)
        {
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
            // at t < 0 stable until t > -0.3
            // at t > 0 stable all the way through but starts rapid ascend a tad later
            var u = t * t;
            return 1f + t + u * (0.5f + u * 0.144f);
        }


        #endregion


        #region Position


        protected Vector3 GetCleanDeltaPos(ref Previous prev)
        {
            // Orientations can be contaminated if the bone isn't reset by animator on each frame,
            // here we figure out what kind of a bone we are dealing with
            // and extract clean measurements without our contaminations.

            // If someone else will try to adjust dynamically the same bone,
            // those calculations will fall apart for bones that aren't reset by the animator.
            // Can be fixed with increased sampling through out the frame,
            // but currently there is no such need, so we avoid extra convolution.

            // On second thought, even if there will be another plugin like this,
            // for as long as that plugin does the same -
            // checks out for contamination and removes own influence, we can coexists in a perfect harmony.


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
                // --- Reset by the animator ---
                cleanPos = currPos;
                cleanDeltaPos = transform.InverseTransformPoint(prev.cleanPos);
            }
            else if (!x && !y && !z)
            {
                // --- Isn't reset by the animator ---
                cleanPos = transform.TransformPoint(-prev.posOffset);
                cleanDeltaPos = transform.InverseTransformDirection(prev.cleanPos - cleanPos);
            }
            else
            {
                // --- The Illusion way ---
                var posOffset = new Vector3(
                    x ? 0f : prev.posOffset.x,
                    y ? 0f : prev.posOffset.y,
                    z ? 0f : prev.posOffset.z
                    );

                cleanPos = transform.TransformPoint(-posOffset);

                //cleanPos = new Vector3(
                //    x ? currPos.x : cleanPos.x,
                //    y ? currPos.y : cleanPos.y,
                //    z ? currPos.z : cleanPos.z
                //    );

                var vecDynamic = transform.InverseTransformPoint(prev.position);
                var vecStatic = transform.InverseTransformDirection(prev.cleanPos - cleanPos);

                cleanDeltaPos = new Vector3(
                    x ? vecDynamic.x : vecStatic.x,
                    y ? vecDynamic.y : vecStatic.y,
                    z ? vecDynamic.z : vecStatic.z
                    );
            }
            //AniMorphPlugin.Logger.LogDebug($"[{transform.name}] " +
            //    $"dynamic({x},{y},{z}) dynamicRot[{prev.localRot != transform.localRotation}) " +
            //    $"prevLocalRot[{prev.localRot.eulerAngles}] localRot[{transform.localRotation.eulerAngles}] " +
            //    //$"cleanDeltaPos({cleanDeltaPos.x:F3},{cleanDeltaPos.y:F3},{cleanDeltaPos.z:F3}) " +
            //    //$"cleanPos({cleanPos.x:F3},{cleanPos.y:F3},{cleanPos.z:F3}) " +
            //    //$"prevCleanPos({prev.cleanPos.x:F3},{prev.cleanPos.y:F3},{prev.cleanPos.z:F3}) " +
            //    //$"currPos({currPos.x:F3},{currPos.y:F3},{currPos.z:F3}) " +
            //    $"");

            prev.cleanPos = cleanPos;
            prev.position = currPos;

            return cleanDeltaPos;
        }

        private bool IsPosShock(ref Config cfg, ref Current curr, ref Previous prev, Vector3 cleanDeltaPos, float dtInv, float animLenInv)
        {
            // A simple velocity, based on movements of a transform without our interference.
            var cleanVelDelta = cleanDeltaPos - prev.cleanDeltaPos;
            
            var projectDot = Vector3.Dot(cleanVelDelta, prev.cleanVelDelta);

            prev.cleanVelDelta = cleanVelDelta;

            prev.cleanDeltaPos = cleanDeltaPos;

            if (curr.bleedTime > 0f) return false;

            // --- Shock detection --- 
            var len = cleanVelDelta.magnitude * dtInv;

            if (len > cfg.posShockThreshold || projectDot < 0f)
            {
                //var shockPower = Mathf.Pow(Mathf.Sqrt(cleanVelDeltaSqrLen), 1.2f);
                //velocity += cleanVelDelta.normalized * shockPower * shockFactor;

                prev.velocity += cleanVelDelta * cfg.posShockStr;

                var fpsFactor = Mathf.Min(1f, dtInv * (1f / 60f));
                var scaleFactor = Mathf.Min(1f, animLenInv * (1f / 2.25f)) * (fpsFactor * fpsFactor);
                // TODO Add slowdown instead of freeze?
                curr.bleedTime = cfg.posBleedLen * scaleFactor;

                if (len > cfg.posFreezeThreshold || projectDot < 0f)
                {
                    curr.shockTime = cfg.posFreezeLen * scaleFactor;

                    //AniMorphPlugin.Logger.LogDebug($"[{transform.name}] " +
                    //    $"Shock[{curr.shockTime:F4}]!" +
                    //    $"");
                }
                return true;
            }
            return false;
        }

        protected Vector3 GetPosOffset(ref Config cfg, ref Current curr, ref Previous prev, float dt, float dtInv, float animLenInv,            
            out Vector3 velocity, out Vector3 accel)
        {
            var cleanDeltaPos = GetCleanDeltaPos(ref prev);

            prev.cleanAdjDeltaPosLen = cleanDeltaPos.magnitude * dtInv;

            // --- Shock state ---
            if (curr.shockTime > 0f)
            {
                curr.shockTime -= dt;
                // Continue position tracking between frames even if we froze for a bit.
                prev.cleanDeltaPos = cleanDeltaPos;

                velocity = prev.velocity;
                accel = Vector3.zero;

                return cleanDeltaPos - velocity;
            }

            // --- Normal state ---
            else if (IsPosShock(ref cfg, ref curr, ref prev, cleanDeltaPos, dtInv, animLenInv))
            {
                velocity = prev.velocity;
                accel = Vector3.zero;

                return cleanDeltaPos - velocity;
            }
            velocity = prev.velocity;
            // --- Bleed velocity ---
            if (curr.bleedTime > 0f)
            {
                curr.bleedTime -= dt;

                velocity *= FastExp(-cfg.posBleedStr * dt);
            }

            var springF = Mathf.Exp(-(prev.velocityLen * 20f)) * (-cfg.posSpring * dtInv) * cleanDeltaPos;
            var dampingF = /*FastExp(prev.velocityLen * 10f) * */-cfg.posDamping * velocity;

            accel = springF + dampingF;

            if (curr.needNoisePos)
            {
                curr.needNoisePos = false;

                accel += GetNoiseVec(
                    cfg.noiseOctaves,
                    cfg.noiseVecPos,
                    cfg.noiseAmplPos * curr.noiseAmplFactor,
                    curr.noiseFreq,
                    out cfg.noiseVecPos);
            }

            accel *= (cfg.massInv * dt);

            velocity += accel;

            prev.velocityLen = velocity.magnitude;

            //AniMorphPlugin.Logger.LogDebug($"[{transform.name}] " +
            //    $"velocity({velocity.x:F3},{velocity.y:F3},{velocity.z:F3}) " +
            //    $"avgLoopLen[{prev.avgCleanAdjDeltaPosLen:F3}] " +
            //    $"velocityLen[{prev.velocityLen:F3}] " +
            //    //$"currPos({currPos.x:F3},{currPos.y:F3},{currPos.z:F3}) " +
            //    $"");

            return cleanDeltaPos - velocity;
        }


        #endregion


        #region Rotation


        private Vector3 devPrevRotDelta;
        private float devRotFreeze;
        private float devRotFreezeTime = 0.1f;

        protected Quaternion GetCleanLocalRot(ref Previous prev)
        {
            var currLocalRot = transform.localRotation;

            if (currLocalRot == prev.localRot)
            {
                var inverseOffset = Quaternion.Inverse(Quaternion.Euler(prev.rotOffset));

                return currLocalRot * inverseOffset;
            }
            else
                return currLocalRot;
        }

        private int _prevFrameCount;
        private int ConsecutiveFrameCounter
        {
            get => field;

            set
            {
                var frameCount = Time.frameCount;

                if (value == 0 || frameCount - _prevFrameCount > 1)
                {
                    field = 0;
                }
                else
                {
                    field += value;
                }
                _prevFrameCount = frameCount;
            }
        }

        protected virtual Vector3 GetRotOffset(ref Config cfg, ref Current curr, ref Previous prev, float dt, float dtInv, float animLenInv)
        {
            var currRot = transform.rotation;
            var currLocalRot = transform.localRotation;
            var isDynamic = currLocalRot != prev.localRot;

            if (isDynamic)
            {
                if (!_animRot && ConsecutiveFrameCounter++ > (int)((2f / 60f) * dtInv))
                {
                    UpdateDynamicRot(true);
                }
            }
            else if (_animRot && ConsecutiveFrameCounter++ > (int)((2f / 60f) * dtInv))
            {
                UpdateDynamicRot(false);
            }

            if (!isDynamic)
            {
                var inverseOffset = Quaternion.Inverse(Quaternion.Euler(prev.rotOffset));

                currRot = currRot * inverseOffset;

                curr.cleanLocalRot = currLocalRot * inverseOffset;
            }
            else
                curr.cleanLocalRot = currLocalRot;

            var delta = currRot * Quaternion.Inverse(prev.adjustedRot);

            delta.ToAngleAxis(out var angle, out var axis);


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


            // --- Filter corruption ---
            if (!float.IsInfinity(axis.x))
            {
                var angleExp = FastExp(absAngle * dt);

                var deltaAngVel = axis * (angle * angleExp * dtInv);

                //var slowLerp = 1f - FastExp(-cfg.rotSlowSmooth * dt);
                //var fastLerp = 1f - FastExp(-cfg.rotFastSmooth * dt);

                var slowLerp = cfg.rotSlowSmooth * dt;
                var fastLerp = cfg.rotFastSmooth * dt;

                prev.slowRotDelta = Vector3.Lerp(prev.slowRotDelta, deltaAngVel, slowLerp);
                prev.fastRotDelta = Vector3.Lerp(prev.fastRotDelta, deltaAngVel, fastLerp);

                //var highFreqDelta = prev.fastRotDelta - prev.slowRotDelta;

                var targetVel = Vector3.Lerp(
                    prev.slowRotDelta,
                    prev.fastRotDelta,
                    cfg.rotHighFreqInf
                );
                //accel = cfg.rotSpring * (targetVel - angVel);
                accel = cfg.rotSpring * targetVel;
            }

            accel -= cfg.rotDamping * Mathf.Exp(-absAngle * dt) * angVel;

            if (curr.needNoiseRot)
            {
                curr.needNoiseRot = false;

                accel += GetNoiseVec(
                    cfg.noiseOctaves,
                    cfg.noiseVecRot,
                    cfg.noiseAmplRot * curr.noiseAmplFactor,
                    curr.noiseFreq,
                    out cfg.noiseVecRot);
            }

            angVel += Vector3.Scale(accel, cfg.rotApplication) * dt;

            //angVel += accel * dt;

            var angVelDot = Vector3.Dot(angVel, prev.torque);

            if (curr.highTorque)
            {
                if (angVelDot < 1f)
                {
                    curr.highTorque = false;
                    var freezeTime = absAngle * (1f / 45f);
                    devRotFreeze = devRotFreezeTime * (freezeTime * freezeTime);
                    //Time.timeScale = 0f;

                    //AniMorphPlugin.Logger.LogWarning($"[{transform.name}]: GetRotOffset: " +
                    //    $"devRotFreeze[{devRotFreeze:F3} absAngle[{absAngle:F3}] angVel{angVel}");
                }
            }
            else if (angVelDot > 1000f)
            {
                curr.highTorque = true;
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
                    //AniMorphPlugin.Logger.LogWarning($"[{transform.name}] - Angle Clamp absAngle[{absAngle:F3}] resultAngle[{resultAngle:F3}]");
                }
                //else
                //    AniMorphPlugin.Logger.LogError($"[{transform.name}] - NOT Angle Clamp absAngle[{absAngle:F3}] resultAngle[{resultAngle:F3}]");
            }
            var resultEuler = result.eulerAngles;

            //AniMorphPlugin.Logger.LogDebug($"[{transform.name}] GetRotOffset: " +
            //    $"result{resultEuler} angVel{angVel} rotDot[{angVelDot:F3}]"); 

            //if (rotDot < 0f) Time.timeScale = 0f;

            return resultEuler;
        }


        #endregion


        #region Scale


        private float devSclDeadZone = 0f; //0.005f;

        protected Vector3 GetSquashOffset(ref Config cfg, ref Current curr, ref Previous prev, Vector3 vel, Vector3 accel, float dt)
        {
            var driver = Vector3.Lerp(accel, vel, 0.5f); // cfg.sclBlend);

            var driverLen = driver.magnitude;

            driverLen = Mathf.Max(0f, driverLen - devSclDeadZone);

            if (driverLen == 0f)
            {
                var basicTargetScl = Vector3.one;

                if (curr.needNoiseScl)
                    curr.needNoiseScl = false;

                    basicTargetScl += GetNoiseVec(
                        4,
                        cfg.noiseVecScl,
                        cfg.noiseAmplScl * curr.noiseAmplFactor,
                        curr.noiseFreq,
                        out cfg.noiseVecScl
                        );

                var staticResult = Vector3.SmoothDamp(
                    prev.scale,
                    basicTargetScl,
                    ref cfg.sclVelocity,
                    cfg.sclRate
                    );


                prev.scale = staticResult;

                return staticResult;
            }


            //var velN = vel.normalized;
            //var velDelta = (vel - prev.velocity);

            //// dot > 0 means acceleration,
            //// dot < 0 means deceleration.
            //var projDotVel = Vector3.Dot(velN, velDelta);

            //if (projDotVel > 0f)
            //    projDotVel += dt * 0.1f;

            //prev.sclTotalDecel = Mathf.Clamp01(prev.sclTotalDecel - projDotVel);

            /* Separate stretch & recovery speeds
             * 
             * float stretchSpeed = 12f;
             * float recoverySpeed = 6f;
             * 
             * float speed = (targetScale.magnitude > currentScale.magnitude)
             * ? stretchSpeed
             * : recoverySpeed;
             */

            var dir = Vector3.Lerp(prev.sclDir, driver, dt * cfg.sclRate);

            prev.sclDir = dir;

            var dirN = dir.normalized;
            //var velAccelFactor = Mathf.Pow(driverLen * squashStrength, 1.3f);
            var factor = driverLen * cfg.sclStr;
            factor *= FastExp(factor * (1f / 3f));
            var stretch = 1f + Mathf.Clamp(factor, 0f, cfg.sclDistort);
            var squash = Mathf.Clamp(1f / Mathf.Sqrt(stretch), 1f - cfg.sclDistort, 1f);
            // Exaggerated
            //var squash = Mathf.Clamp(1f / Mathf.Pow(stretch, 0.6f), maxSquash, 1f);


            var targetScale = GetDirectionalScale(dirN, stretch, squash);

            if (curr.needNoiseScl)
            {
                curr.needNoiseScl = false;

                targetScale += GetNoiseVec(
                    cfg.noiseOctaves,
                    cfg.noiseVecScl,
                    cfg.noiseAmplScl * curr.noiseAmplFactor,
                    curr.noiseFreq,
                    out cfg.noiseVecScl
                    );
            }
            
            var result = Vector3.SmoothDamp(
                prev.scale,
                targetScale,
                ref cfg.sclVelocity,
                cfg.sclRate
                );

            if (cfg.sclPreserveVolume)
            {
                // Preserve original volume
                var stretchVolume = result.x * result.y * result.z;
                var volumeCorrection = Mathf.Pow(1f / stretchVolume, (1f / 3f));
                result *= volumeCorrection;
            }

            prev.scale = result;

            return result;
        }

        private Vector3 GetDirectionalScale(Vector3 dir, float stretch, float squash)
        {
            return new Vector3(
                Mathf.Lerp(squash, stretch, Mathf.Abs(dir.x)),
                Mathf.Lerp(squash, stretch, Mathf.Abs(dir.y)),
                Mathf.Lerp(squash, stretch, Mathf.Abs(dir.z))
            );
        }


        //private bool devSclShock;

        //protected Vector3 GetScaleOffset(ref Config cfg, ref Current curr, ref Previous prev, Vector3 vel, float dt, float dtInv)
        //{
        //    if (devSclShock && curr.shockTime > 0f) return prev.sclOffset;

        //    var velLen = prev.velocityLen;

        //    if (velLen == 0f) return Vector3.one;

        //    // Normalize velocity
        //    var velN = vel / velLen;

        //    var absVelN = new Vector3(
        //        Mathf.Abs(velN.x), 
        //        Mathf.Abs(velN.y), 
        //        Mathf.Abs(velN.z));

        //    var accelVec = (vel - prev.velocity) * dtInv;

        //    // dot > 0 means acceleration,
        //    // dot < 0 means deceleration.
        //    var projectionDot = Vector3.Dot(velN, accelVec);

        //    var distortVec = Vector3.one;


        //    //if (devAccel)
        //    //{
        //        // Accumulate acceleration, deltaTime is a passive drain to avoid awkward accumulations in some idle animations.
        //    var sclTotalAccel = Mathf.Clamp01(prev.sclTotalAccel + projectionDot - (dt * 0.1f));
        //    prev.sclTotalAccel = sclTotalAccel;

        //    if (projectionDot < 0f) projectionDot *= 2f;

        //    var sclTotalDecel = Mathf.Clamp01(prev.sclTotalDecel - projectionDot - (dt * 0.1f));

        //    prev.sclTotalDecel = sclTotalDecel;

        //    if (sclTotalDecel > sclTotalAccel)
        //    {
        //        var distortValue = Mathf.Clamp(sclTotalDecel * cfg.sclDecelStr, 0f, cfg.sclMaxDistortion);

        //        distortVec = Vector3.one - (absVelN * distortValue);

        //        var perpendicularScale = Vector3.one + Vector3.Scale((Vector3.one - absVelN), distortValue * cfg.sclAxisDistribution);

        //        distortVec = Vector3.Scale(distortVec, perpendicularScale);
        //    }
        //    else
        //    {
        //        var distortValue = Mathf.Clamp(sclTotalAccel * cfg.sclAccelStr, 0f, cfg.sclMaxDistortion);

        //        distortVec += absVelN * distortValue;

        //        var perpendicularScale = Vector3.one + Vector3.Scale(absVelN - Vector3.one, distortValue * cfg.sclAxisDistribution);

        //        distortVec = Vector3.Scale(distortVec, perpendicularScale);
        //    }




        //    AniMorphPlugin.Logger.LogDebug($"[{transform.name}] Acceleration: " +
        //        $"dot[{projectionDot:F3}] " +
        //        $"distortVec({distortVec.x:F3},{distortVec.y:F3},{distortVec.z:F3}) " +
        //        "");

        //    if (cfg.sclPreserveVolume)
        //    {
        //        // Preserve original volume
        //        var stretchVolume = distortVec.x * distortVec.y * distortVec.z;
        //        var volumeCorrection = Mathf.Pow(_baseScaleVolume / stretchVolume, (1f / 3f));
        //        distortVec *= volumeCorrection;
        //    }

        //    var finalScale = Vector3.Lerp(prev.scale, distortVec, dt * cfg.sclInterpSpeed);
        //    //#if DEBUG
        //    //            if (!acceleration && !deceleration)
        //    //            {
        //    //                AniMorph.Logger.LogDebug($"stretch({finalScale.x:F3},{finalScale.y:F3},{finalScale.z:F3})" +
        //    //                //    $"stretchVolume[{stretch.x + stretch.y + stretch.z}] " +
        //    //                //    $"finalScale({finalScale.x:F3},{finalScale.y:F3},{finalScale.z:F3}) " +
        //    //                //    $"finalVolume[{finalScale.x + finalScale.y + finalScale.z}]");
        //    //                "");
        //    //            }
        //    //#endif
        //    prev.scale = finalScale;
        //    return finalScale;
        //}


        #endregion


        #region Dots


        protected Vector3 GetPosDotOffset(ref Config cfg, ref Current curr, float dotUp, float dotR)
        {
            var result = Vector3.Lerp(cfg.posPitchOffsetFaceDown, dotUp > 0f ? Vector3.zero : cfg.posPitchOffsetUpsideDown, Mathf.Abs(dotUp));

            result += Vector3.Lerp(vecZero, dotR > 0f ? cfg.posRollOffsetR : cfg.posRollOffsetL, Mathf.Abs(dotR));

            //if (curr.needNoisePos)
            //{
            //    curr.needNoisePos = false;
            //    result += GetNoiseVec(
            //        cfg.noiseOctaves,
            //        cfg.noiseVecPos,
            //        cfg.noiseAmplPos * curr.noiseAmplFactor,
            //        curr.noiseFreq,
            //        out cfg.noiseVecPos
            //        );
            //}

            return result;
        }

        protected Vector3 GetSclDotOffset(ref Config cfg, ref Current curr, float dotFwd)
        {
            var result = Vector3.Lerp(vecOne, dotFwd > 0f ? cfg.sclOffsetFaceUp : cfg.sclOffsetFaceDown, Mathf.Abs(dotFwd));

            if (curr.needNoiseScl)
            {
                curr.needNoiseScl = false;
                result += GetNoiseVec(
                    cfg.noiseOctaves,
                    cfg.noiseVecScl,
                    cfg.noiseAmplScl * curr.noiseAmplFactor,
                    curr.noiseFreq,
                    out cfg.noiseVecScl
                    );
            }
            return result;
        }

        protected Vector3 GetRotDotOffset(ref Config cfg, ref Current curr, float dotFwd, float dotR)
        {
            var angleLimit = cfg.rotSidewaysDeg;

            if (!cfg.isLeftSide) dotR = -dotR;

            //AniMorphPlugin.Logger.LogDebug($"[{transform.name}]: dotFwd[{dotFwd:F3}] dotR[{dotR:F3}] dotSum[{dotFwd + dotR:F3}]");

            //var dotDriver = absDotFwd > absDotR ? dotFwd : dotR;

            // A way to reduce angle spread when lying face up.
            if (dotFwd > 0f) 
                dotFwd *= cfg.rotSidewaysFaceUpFactor;

            var result = new Vector3(0f, angleLimit * (dotFwd + dotR), 0f);

            if (curr.needNoiseRot)
            {
                curr.needNoiseRot = false;
                result += GetNoiseVec(
                    cfg.noiseOctaves,
                    cfg.noiseVecRot,
                    cfg.noiseAmplRot * curr.noiseAmplFactor,
                    curr.noiseFreq,
                    out cfg.noiseVecRot
                    );
            }

            //var boneUp = Bone.up;

            ////var boneRotation = Bone.rotation;

            ////var deltaEuler = (boneRotation * Quaternion.Inverse(_upRotation)).eulerAngles;

            ////var deltaAngleY = Mathf.DeltaAngle(0f, deltaEuler.y);

            ////var deviationY = Mathf.Min(_angleLimitRad, Mathf.Abs(deltaAngleY));

            ////if (deltaAngleY < 0f) deviationY = -deviationY;

            //var lookRot = Quaternion.LookRotation(-Vector3.up, boneUp);
            ////var result = new Vector3(0f, deviationY * masterFwdDot, 0f);
            return result;
        }


        #endregion


        #region OnHooks


        internal virtual void Reset()
        {
            var bone = transform;

            abmxModifierData?.Clear();
            current.Clear();
            previous.Clear(bone);
            tether?.Clear();

            if (_abmxModifier == null)
            {
                var boneController = bone.GetComponentInParent<BoneController>();
                if (boneController != null)
                {
                    _abmxModifier = boneController.GetModifier(bone.name, BoneLocation.BodyTop);
                }
            }
            
            //if (_combineModifiersCachedReturn == null && abmxModifier != null)
            //{
            //    var traverse = Traverse.Create(abmxModifier);

            //    _combineModifiersCachedReturn = traverse.Field("_combineModifiersCachedReturn").GetValue<BoneModifierData>();
            //}
        }

        private void UpdateDynamicRot(bool dynamic)
        {
            ConsecutiveFrameCounter = 0;
            _animRot = dynamic;

            AniMorphPlugin.Logger.LogWarning($"[{transform.name}] [UpdateDynamicRot] isDynamic[{dynamic}]");

            if (_boneController == null)
            {
                var boneController = transform.GetComponentInParent<BoneController>();

                if (boneController == null) return;

                _boneController = boneController;
            }
            
            if (_abmxModifier == null)
            {
                var abmxModifier = _boneController.GetModifier(transform.name, BoneLocation.BodyTop);

                if (abmxModifier == null) return;

                _abmxModifier = abmxModifier;
            }

            if (dynamic)
            {
                _abmxModifier.AddCollectPartialBaseline(Baseline.Rotation);
            }
            else
            {
                _abmxModifier.RemoveCollectPartialBaseline(Baseline.Rotation);

                _boneController.NeedsBaselineUpdate = true;
            }
            
        }

        internal virtual void OnSettingChanged(Body body, ChaControl chara)
        {
#if DEBUG
            AniMorphPlugin.Logger.LogDebug($"[{transform.name}] - {GetType().Name}.OnSettingChanged: [{chara.name}:{body}]");
#endif
            ref var cfg = ref config;
            var baseCfg = baseConfig;

            var pluginConfig = AniMorphPlugin.ConfigDic[body];

            cfg.effects = pluginConfig.Effects.Value;

            active = cfg.effects != 0;

            if (!active)
            {
                Reset();
                return;
            }

            var isPos = (cfg.effects & Effect.Pos) != 0;
            var isRot = ((cfg.effects & Effect.Rot) != 0);
            var isScl = ((cfg.effects & Effect.Scl) != 0);
            var isTether = ((cfg.effects & Effect.Tether) != 0);
            var isPosOffset = ((cfg.effects & Effect.PosOffset) != 0);
            var isRotOffset = ((cfg.effects & Effect.RotOffset) != 0);
            var isSclOffset = ((cfg.effects & Effect.SclOffset) != 0);

            cfg.noiseOctaves = pluginConfig.NoiseOctaves.Value;
            // The backward way algos are setup forces us to have weird coefficient ranges,
            // and I'd rather have ugly values(tiny decimals) hidden and instead expose usual common values,
            // that will be converted here.
            cfg.posSpring = (float)Math.Round(baseCfg.posFactor * pluginConfig.PosSpring.Value * (1f / 60f), 3);
            cfg.posDamping = (baseCfg.posFactor * pluginConfig.PosSpring.Value) * pluginConfig.PosDamping.Value;
            cfg.posShockThreshold = pluginConfig.PosShockThreshold.Value;
            cfg.posShockStr = pluginConfig.PosShockStr.Value;
            cfg.posFreezeThreshold = pluginConfig.PosFreezeThreshold.Value;
            cfg.posFreezeLen = pluginConfig.PosFreezeLen.Value;
            cfg.posBleedStr = pluginConfig.PosBleedStr.Value;
            cfg.posBleedLen = pluginConfig.PosBleedLen.Value;

            if (isRot)
            {
                cfg.rotSpring = (float)Math.Round(baseCfg.rotFactor * pluginConfig.RotSpring.Value * (1f / 60f), 3);
                cfg.rotDamping = (baseCfg.rotFactor * pluginConfig.RotSpring.Value) * pluginConfig.RotDamping.Value;
                cfg.rotRate = pluginConfig.RotRate.Value;
            }

            if (isScl)
            {
                cfg.sclStr = pluginConfig.SclStr.Value;
                cfg.sclRate = pluginConfig.SclRate.Value;
                cfg.sclDistort = pluginConfig.SclDistort.Value;
                cfg.sclPreserveVolume = pluginConfig.SclPreserveVol.Value;
            }

            if (isTether && tether != null)
            {
                tether.multiplier = -1000 * pluginConfig.TetherFactor.Value;
                tether.frequency = pluginConfig.TetherFreq.Value;
                tether.damping = pluginConfig.TetherDamp.Value;
                tether.maxAngle = pluginConfig.TetherMaxDeg.Value;
            }

            if (isRotOffset)
            {
                cfg.rotSidewaysDeg = pluginConfig.RotOffsetRollDeg.Value;

                if (baseCfg.dotFlipSign)
                    cfg.rotSidewaysDeg = -cfg.rotSidewaysDeg;
                if (cfg.isLeftSide)
                    cfg.rotSidewaysDeg = -cfg.rotSidewaysDeg;

                cfg.rotSidewaysFaceUpFactor = pluginConfig.RotOffsetRollFaceUpFactor.Value;
            }

            if (isPosOffset)
            {
                cfg.posPitchOffsetFaceDown = new Vector3(0f, pluginConfig.PosOffsetPitchFaceDown.Value, 0f);
                cfg.posPitchOffsetUpsideDown = new Vector3(0f, pluginConfig.PosOffsetPitchUpsideDown.Value, 0f);

                var posRollVec = pluginConfig.PosOffsetRoll.Value;
                cfg.posRollOffsetL = posRollVec;
                cfg.posRollOffsetR = new Vector3(-posRollVec.x, posRollVec.y, posRollVec.z);
            }

            if (isSclOffset)
            {
                cfg.sclOffsetFaceUp =
                    ScaleVecFwd(
                        pluginConfig.SclOffsetFaceUp.Value,
                        pluginConfig.SclOffsetFaceUpPerpAxesFactor.Value
                        );
                cfg.sclOffsetFaceUpF = cfg.sclOffsetFaceUp - vecOne;

                // Half scaled because:
                // Only half of added Z volume are perpendicular axes, another half is subcutaneous fat from all around.
                cfg.sclOffsetFaceDown =
                    ScaleVecFwd(
                        pluginConfig.SclOffsetFaceDown.Value,
                        pluginConfig.SclOffsetFaceDownPerpAxesFactor.Value
                        );
                cfg.sclOffsetFaceDownF = cfg.sclOffsetFaceDown - vecOne;

                var dotFlipSign = baseCfg.dotFlipSign;
                if (dotFlipSign)
                {
                    var a = cfg.sclOffsetFaceUp;
                    cfg.sclOffsetFaceUp = cfg.sclOffsetFaceDown;
                    cfg.sclOffsetFaceDown = a;
                }

                var dotScl_posFactor = baseCfg.dotScl_posFactor;
                var dotScl_isPosFactor = dotScl_posFactor != Vector3.zero;

                cfg.dotScl_isPosFactor = dotScl_isPosFactor;

                if (dotScl_isPosFactor)
                {
                    var dotScl_PosFactorFaceUp = Vector3.Scale(InvertVec(cfg.posAppNegative), dotScl_posFactor);
                    var dotScl_PosFactorFaceDown = Vector3.Scale(InvertVec(cfg.posAppPositive), dotScl_posFactor);

                    dotScl_PosFactorFaceUp.z = -dotScl_PosFactorFaceUp.z;

                    cfg.dotScl_posFactorFaceUp = Vector3.Scale(dotScl_PosFactorFaceUp, cfg.sclOffsetFaceUp);
                    cfg.dotScl_posFactorFaceDown = Vector3.Scale(dotScl_PosFactorFaceDown, cfg.sclOffsetFaceDown);
                }
            }

            cfg.noiseAmplPos = (isPos) ? baseCfg.posFactor * pluginConfig.NoiseAmplitudePos.Value : 0f;
            cfg.noiseAmplRot = (isRot || isRotOffset) ? baseCfg.rotFactor * (pluginConfig.NoiseAmplitudeRot.Value * 100f) : 0f;
            cfg.noiseAmplScl = (isScl || isSclOffset) ? baseCfg.sclFactor * (pluginConfig.NoiseAmplitudeScl.Value * 10f) : 0f;


            Reset();
            OnSetClothesState(body, chara);


            switch (body)
            {
                case Body.Pelvis:
                    cfg.posLimitPositive = new Vector3(1f, 1.33f, 1f);
                    cfg.posLimitNegative = new Vector3(1f, 0.67f, 1f);
                    break;
                default:
                    cfg.posLimitPositive = Vector3.one;
                    cfg.posLimitNegative = Vector3.one;
                    break;

            }

            // Add seed to the Perlin noise.
            cfg.noiseVecPos = new(Random.Range(0f, 1000f), Random.Range(0f, 1000f), Random.Range(0f, 1000f));
            cfg.noiseVecRot = new(Random.Range(0f, 1000f), Random.Range(0f, 1000f), Random.Range(0f, 1000f));
            cfg.noiseVecScl = new(Random.Range(0f, 1000f), Random.Range(0f, 1000f), Random.Range(0f, 1000f));

            cfg.noiseFreq = (float)Math.Round(0.75f * Random.Range(0.85f, 1.15f), 2);
            

            static Vector3 ScaleVecFwd(float z, float perpAxisFactor)
            {
                if (perpAxisFactor < 0f) throw new ArgumentOutOfRangeException(nameof(perpAxisFactor));

                // Convert small offset to the scale value.
                z += 1f;

                var sqrt = Mathf.Sqrt(1f / z);

                var perpAxis = 1f + ((1f * sqrt - 1f) * perpAxisFactor);

                return new Vector3(perpAxis, perpAxis, z);
            }
            static Vector3 InvertVec(Vector3 v)
            {
                return new Vector3(
                    v.x == 0f ? 0f : (1f / v.x),
                    v.y == 0f ? 0f : (1f / v.y),
                    v.z == 0f ? 0f : (1f / v.z)
                    );
            }
        }


        internal void OnSetClothesState(Body body, ChaControl chara)
        {
            var pluginConfig = ConfigDic[body];

            var settingValue = pluginConfig.DisableWhenClothes.Value;

            if (settingValue == 0 || chara.objClothes == null) return;

            var slotList = new List<int>();

            foreach (var enumValue in ClothesKindValues)
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

            if (!wasActive && active) Reset();
        }

        internal virtual void OnAnimationLoopStart(float animLoopFrameCountInv, float dt)
        {
            previous.OnAnimationLoopStart(animLoopFrameCountInv, dt);
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
                Interp.Smooth => SmoothStep(t),
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
        float SmoothStep(float t) => t * t * (3f - 2f * t); 
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
            Scl = 1 << 2,
            Tether = 1 << 3,
            PosOffset = 1 << 4,
            RotOffset = 1 << 5,
            SclOffset = 1 << 6,
            DevAnything = 1 << 7,
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
        
        //protected class BaseConfig
        //{
        //    // No point making it a struct as it is rarely accessed.
        //    internal BaseConfig(Effect allowedEffects, float posFactor, float rotFactor, float sclFactor, bool dotFlipSign, Vector3 dotScl_posFactor)
        //    {
        //        this.allowedEffects = allowedEffects;
        //        this.posFactor = posFactor;
        //        this.rotFactor = rotFactor;
        //        this.sclFactor = sclFactor;
        //        this.dotFlipSign = dotFlipSign;
        //        this.dotScl_posFactor = dotScl_posFactor;
        //    }

        //    internal Effect allowedEffects;
        //    internal float posFactor;
        //    internal float rotFactor;
        //    internal float sclFactor;

        //    internal bool dotFlipSign;
        //    internal Vector3 dotScl_posFactor;
        //}

        protected struct Config
        {
            internal Config(Vector3 posApplication, Vector3 posAppPositive, Vector3 posAppNegative, Vector3 rotApplication, Vector3 sclApplication)
            {
                this.posAppPositive = Vector3.Scale(posApplication, posAppPositive);
                this.posAppNegative = Vector3.Scale(posApplication, posAppNegative);
                this.rotApplication = rotApplication;
                this.sclApplication = sclApplication;
            }

            internal Effect effects;
            internal Vector3 posAppPositive;
            internal Vector3 posAppNegative;
            internal Vector3 rotApplication;
            internal Vector3 sclApplication;

            internal int noiseOctaves;
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
            internal Vector3 posLimitPositive;
            internal Vector3 posLimitNegative;
            //internal float posDamping = 2f * Mathf.Sqrt(posSpring * mass);

            internal float mass = 1f;
            internal float massInv = 1f;

            internal float rotSpring = 30f;
            internal float rotDamping = 5f;
            internal float rotRate = 2f;

            // How much the scale stretches along velocity direction.
            internal float sclStr = 40f; //0.01f;
                                                            // How much to squash along deceleration axis
            //internal float sclDecelStr = 0.5f;
            //internal Vector3 sclAxisDistribution = new Vector3(0.67f, 0.5f, 0.33f);
            // How fast squash reacts
            internal float sclRate;
            // Max squash on deceleration
            internal float sclDistort;
            internal bool sclPreserveVolume;
            internal Vector3 sclVelocity;
            internal float sclBlend;

            internal float rotSidewaysDeg;
            internal float rotSidewaysFaceUpFactor;

            internal Vector3 posPitchOffsetFaceDown;
            internal Vector3 posPitchOffsetUpsideDown;
            internal Vector3 posRollOffsetL;
            internal Vector3 posRollOffsetR;

            internal Vector3 sclOffsetFaceUp;
            internal Vector3 sclOffsetFaceDown;
            internal Vector3 sclOffsetFaceUpF;
            internal Vector3 sclOffsetFaceDownF;

            internal bool dotScl_isPosFactor;
            internal Vector3 dotScl_posFactorFaceUp;
            internal Vector3 dotScl_posFactorFaceDown;


            internal bool isLeftSide;
            // (20 .. 40)
            public float rotSlowSmooth = 25f;
            // (5 .. 12)
            public float rotFastSmooth = 8f;
            // (0.1 .. 0.3)
            public float rotHighFreqInf = 0.2f;

        }

        protected struct Current
        {
            internal bool highTorque;
            internal float shockTime;
            internal float bleedTime;
            internal float noiseAmplFactor;
            internal float noiseFreq;
            internal Quaternion cleanLocalRot;

            internal bool animRot;

            internal bool needNoisePos;
            internal bool needNoiseRot;
            internal bool needNoiseScl;

            internal void Clear()
            {
                highTorque = false;

                shockTime = 0f;
                bleedTime = 0f;

                animRot = false;
            }
        }
        
        // TODO class -> struct
        protected /*struct*/ class Previous
        {
            internal Vector3 velocity;
            internal float velocityLen;
            internal float cleanAdjDeltaPosLen
            {
                get => field;
                set
                {
                    field = value;
                    _animLoopVelLen += value;
                }
            }

            private float _animLoopVelLen;
            internal float avgCleanAdjDeltaPosLen;

            internal Vector3 torque;

            internal Vector3 position;
            internal Vector3 cleanPos;
            internal Vector3 cleanDeltaPos;
            internal Vector3 cleanVelDelta;
            //internal Vector3 localAdjVec;
            internal Vector3 scale;
            internal Vector3 sclDir;

            internal Vector3 posOffset;
            internal Vector3 rotOffset;
            internal Vector3 sclOffset;

            // Clean rotation from that frame
            //internal Quaternion cleanRot;
            //internal Quaternion rotAdjustment;
            //internal Vector3 rotModifier;
            internal Quaternion adjustedRot;
            internal Vector3 localPos;
            internal Quaternion localRot;

            internal float sclTotalAccel;
            internal float sclTotalDecel;

            internal Vector3 slowRotDelta;
            internal Vector3 fastRotDelta;

            internal void Clear(Transform bone)
            {
                var pos = bone.position;

                velocity = Vector3.zero;
                velocityLen = 0f;
                _animLoopVelLen = 0f;
                torque = Vector3.zero;
                position = pos;
                cleanPos = pos;
                cleanDeltaPos = Vector3.zero;
                cleanVelDelta = Vector3.zero;
                posOffset = Vector3.zero;
                rotOffset = Vector3.zero;
                adjustedRot = bone.rotation;
                scale = Vector3.one;
                sclTotalAccel = 0f;
                sclTotalDecel = 0f;
            }

            internal void OnAnimationLoopStart(float animLoopFrameCountInv, float dt)
            {
                avgCleanAdjDeltaPosLen = Mathf.Lerp(avgCleanAdjDeltaPosLen, cleanAdjDeltaPosLen * animLoopFrameCountInv, dt * 10f);

                _animLoopVelLen = 0f;
            }
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
