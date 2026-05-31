using KKABMX.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static ADV.Info;
using static AniMorph.AniMorphEffector;
using static AniMorph.AniMorphPlugin;
using Axis = AniMorph.AniMorphPlugin.Axis;
using Random = UnityEngine.Random;

namespace AniMorph
{
    internal class MotionModifier
    {
        #region Fields


        protected static readonly Effect[] effects = Enum.GetValues(typeof(Effect)) as Effect[];

        protected readonly Transform transform;
        protected readonly Tethering tether;
        protected readonly BoneModifierData abmxModifierData = new();

        // They say it's a generic micro optimization when you access those on every frame few+ times
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
#if DEBUG
        private readonly DebugValues _dev = new();
#endif

        private BoneController _boneController;


        private readonly float _baseScaleVolume;
        private readonly float _baseScaleMagnitude;

        //private BoneModifierData _combineModifiersCachedReturn;

        private float devVelInf = 0.05f;

        private int _prevFrameCount;
#if DEBUG
        private Effect showDebug;
#endif
        #endregion


        #region Init


        internal MotionModifier(BaseConfig baseCfg, Transform bone, Transform centeredBone)
        {
            if (bone == null) 
                throw new ArgumentNullException(nameof(bone));

            baseConfig = baseCfg;

            config = new Config(
                inheritEffects: baseCfg.inheritEffects,

                posAppPositive: baseCfg.posAppPositive,
                posAppNegative: baseCfg.posAppNegative,
                rotApplication: baseCfg.rotApplication,
                sclApplication: baseCfg.sclApplication,

                inheritPosF: baseCfg.inheritPosF
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

            UpdateAnimRot(baseCfg.initAnimRot);

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

        
        #endregion


        #region Update Cycle


        internal virtual void OnUpdate()
        {
            // Grab modified local orientations from previous frame,
            // local orientations are simple fields, extremely cheap to access.
            ref var prev = ref previous;
            prev.localPos = transform.localPosition;
            prev.localRot = transform.localRotation;
        }

        internal virtual void UpdateModifier(float dt, float dtInv, float animLen, float animLenInv)
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

            curr.noiseAmplFactor = (OneThird + Mathf.Min(TwoThirds, animLenInv * curr.avgPosLen * 15f));
            curr.noiseFreqStep = cfg.noiseFreq * animLenInv * dt;


            // --- Update Offsets ---

            UpdatePosTracking(ref prev, ref curr);
            UpdateVelocityShock(ref cfg, ref curr, ref prev, dt, dtInv, animLen);

            if ((effects & Effect.Pos) != 0)
                posOffset = GetPosOffset(ref cfg, ref curr, ref prev, dt, dtInv, animLenInv);

            if ((effects & Effect.Rot) != 0)
                rotOffset = GetRotOffset(ref cfg, ref curr, ref prev, dt, dtInv, animLen, animLenInv);
            else
                curr.cleanLocalRot = GetCleanLocalRot(ref prev);

            if ((effects & Effect.Scl) != 0)
                sclOffset = GetSquashOffsetEx(ref cfg, ref curr, ref prev, dt, dtInv);

            if ((effects & Effect.Tether) != 0)
                rotOffset += tether.GetTetheringOffset(prev.velocity, dt);


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

            prev.posOffset = posOffset;
            prev.rotOffset = rotOffset;
            prev.sclOffset = sclOffset;

            OnPostUpdateModifier(ref prev, ref curr, dt);
        }

        protected void OnPostUpdateModifier(ref Previous prev, ref Current curr, float dt)
        {
            prev.cleanDeltaPos = curr.cleanDeltaPos;
            prev.cleanVelDelta = curr.cleanVelDelta;

            if (curr.shock)
            {
                curr.shockTime -= dt;

                if (curr.shockTime < 0f)
                    curr.shock = false;

                return;
            }
            
            if (curr.bleed)
            {
                curr.bleedTime -= dt;

                if (curr.bleedTime < 0f)
                    curr.bleed = false;

                return;
            }
        }

        protected void UpdatePosTracking(ref Previous prev, ref Current curr)
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
            //    //$"prevLocalRot[{prev.localRot.eulerAngles}] localRot[{transform.localRotation.eulerAngles}] " +
            //    //$"cleanDeltaPos({cleanDeltaPos.x:F3},{cleanDeltaPos.y:F3},{cleanDeltaPos.z:F3}) " +
            //    //$"cleanPos({cleanPos.x:F3},{cleanPos.y:F3},{cleanPos.z:F3}) " +
            //    //$"prevCleanPos({prev.cleanPos.x:F3},{prev.cleanPos.y:F3},{prev.cleanPos.z:F3}) " +
            //    //$"currPos({currPos.x:F3},{currPos.y:F3},{currPos.z:F3}) " +
            //    $"");

            curr.cleanDeltaPos = cleanDeltaPos;

            prev.cleanPos = cleanPos;
            prev.position = currPos;
        }

        protected void UpdateVelocityShock(ref Config cfg, ref Current curr, ref Previous prev, float dt, float dtInv, float animLen)
        {
            // A simple velocity, based on movements of a transform without our interference.
            var cleanVelDelta = curr.cleanDeltaPos - prev.cleanDeltaPos;

            curr.cleanVelDelta = cleanVelDelta;

            if (curr.shock || curr.bleed)
                return;


            // --- Shock detection --- 

            var projectDot = Vector3.Dot(cleanVelDelta, prev.cleanVelDelta);

            var cleanVelDeltaLen = cleanVelDelta.magnitude * dtInv;

            var isDot = projectDot < 0f;

            if (cleanVelDeltaLen > cfg.posShockThreshold || (isDot && (cleanVelDeltaLen > cfg.posShockThreshold * OneThird)))
            {
                //var shockPower = Mathf.Pow(Mathf.Sqrt(cleanVelDeltaSqrLen), 1.2f);
                //velocity += cleanVelDelta.normalized * shockPower * shockFactor;

                //prev.velocity += cleanVelDelta * cfg.posShockStr;
                // Inverse target scale.
                // TODO. Add pseudo vec2 option for thighs and such.
                //prev.sclTarget = vecOne - (prev.sclTarget - vecOne);

                //var fpsFactor = dtInv * (1f / 60f);
                var animFactor = animLen; // * (1f / 2.25f); // * (fpsFactor * fpsFactor);
                // TODO Add slowdown instead of freeze?
                curr.bleedTime = cfg.bleedLen * animFactor;
                curr.bleed = true;

                //if (cleanVelDeltaLen > cfg.posFreezeThreshold || isDot)
                //{
                    curr.shockTime = cfg.freezeLen * animFactor;
                curr.shock = true;

                    //AniMorphPlugin.Logger.LogWarning($"[{transform.name}] " +
                    //    $"Freeze[{curr.shockTime:F4}]! scaleFactor[{scaleFactor:F3}] fpsFactor[{fpsFactor}] projectDot[{projectDot < 0f}]" +
                    //    $"");
                //}
                //else
                //{

                //    AniMorphPlugin.Logger.LogInfo($"[{transform.name}] " +
                //        $"Shock[{curr.shockTime:F4}]! scaleFactor[{scaleFactor:F3}] fpsFactor[{fpsFactor}] projectDot[{projectDot < 0f}]" +
                //        $"");
                //}
            }
        }


        #endregion


        #region Position


        protected Vector3 GetPosOffset(ref Config cfg, ref Current curr, ref Previous prev, float dt, float dtInv, float animLenInv)
        {
            var cleanDeltaPos = curr.cleanDeltaPos;

            var velocity = prev.velocity;


            // --- Shock state ---

            if (curr.shock)
                return cleanDeltaPos - velocity;


            // --- Bleed velocity ---

            if (curr.bleed)
                velocity *= 1f - (cfg.posBleedStr * dt);


            // The higher the velocity the lesser all the consequent accumulation.
            var springVelCoef = 1f + (prev.velocityLen * cfg.devPosFastCoef);
            springVelCoef = 1f / (springVelCoef * springVelCoef);
            springVelCoef = Mathf.MoveTowards(prev.posSpringVelCoef, springVelCoef, dt);


            //var springPosCoef = cleanDeltaPosLen * prev.avgPosLenInv;
            //if (springPosCoef > 1f)
            //{
            //    var a = 1f + (springPosCoef - 1f); 
            //    springPosCoef = -(1f / (a * a)) + 1f;
            //}

            var springF = springVelCoef * (-cfg.posSpring) * cleanDeltaPos;
            var dampingF = -cfg.posDamping * dt * velocity;

            if ((showDebug & Effect.Pos) != 0) 
                AniMorphPlugin.Logger.LogDebug($"[{transform.name}] " +
                    $"velocity({velocity.x:F3},{velocity.y:F3},{velocity.z:F3}) " +
                    $"springF({springF.x:F3},{springF.y:F3},{springF.z:F3}), " +
                    $"dampingF({dampingF.x:F3},{dampingF.y:F3},{dampingF.z:F3}) " +
                    $"velCoef[{springVelCoef:F3}]");
#if DEBUG
            _dev.posSpringVelCoef = springVelCoef;
            //_dev.posSpringPosCoef = springPosCoef;
            _dev.posSpring = springF;
            _dev.posDamping = dampingF;
#endif
            var accel = springF + dampingF;

            if (cfg.noisePosAmpl != 0f)
            {
                accel += Vector3.Scale(cfg.noisePosFactor, GerPerlinVec(
                    cfg.noisePosVec,
                    cfg.noisePosAmpl * curr.noiseAmplFactor,
                    curr.noiseFreqStep,
                    out cfg.noisePosVec
                    ));
            }

            //accel *= (cfg.massInv * dt);

            velocity += accel;

            prev.velocity = velocity;
            prev.velocityLen = velocity.magnitude;
            prev.posSpringVelCoef = springVelCoef;

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

        protected Quaternion GetCleanLocalRot(ref Previous prev)
        {
            // ABMX uses rotOffset to rotate a transform in its local space,
            // i.e. currRot = currRot * offset, therefore prevRot = Inverse(offset) * currRot


            var currLocalRot = transform.localRotation;
            var isDynamic = currLocalRot != prev.localRot;

            if (!isDynamic)
            {
                var inverseOffset = Quaternion.Inverse(Quaternion.Euler(prev.rotOffset));

                // It should be the commented way, but seems like uncommented is what actually works.
                //return inverseOffset * currLocalRot;
                return currLocalRot * inverseOffset;
            }
            else
                return currLocalRot;
        }

        private void UpdateAnimRot(bool state)
        {
            ref var curr = ref current;

            if (curr.animRot != state)
            {
                curr.animRot = state;
                UpdateRotCollectBaseline(state);
            }
        }

        protected virtual Vector3 GetRotOffset(ref Config cfg, ref Current curr, ref Previous prev, float dt, float dtInv, float animLen, float animLenInv)
        {
            var currCleanRot = transform.rotation;
            var currLocalRot = transform.localRotation;
            var isDynamic = currLocalRot != prev.localRot;
                
            if (isDynamic)
            {
                if (!curr.animRot && ConsecutiveFrameCounter++ > (int)((2f / 60f) * dtInv))
                    UpdateAnimRot(true);

                curr.cleanLocalRot = currLocalRot;
            }
            else
            {
                if (curr.animRot && ConsecutiveFrameCounter++ > (int)((2f / 60f) * dtInv))
                    UpdateAnimRot(false);

                var inverseOffset = Quaternion.Inverse(Quaternion.Euler(prev.rotOffset));

                currCleanRot = currCleanRot * inverseOffset;

                // It should be the commented way, but seems like uncommented is what actually works.
                //curr.cleanLocalRot = inverseOffset * currLocalRot;
                curr.cleanLocalRot = currLocalRot * inverseOffset;
            }

            var currCleanRotInverse = Quaternion.Inverse(currCleanRot);

            curr.cleanRotInverse = currCleanRotInverse;

            if (curr.rotFreezeTime > 0f)
            {
                curr.rotFreezeTime -= dt;

                var prevTorque = prev.torque;

                prevTorque += prevTorque * (-cfg.rotDamping * dt);

                prev.torque = prevTorque;

                return (curr.cleanRotInverse * prev.adjustedRot).eulerAngles;
            }

            // Global delta between clean current rotation and rotation we setup in the previous frame.
            var delta = currCleanRot * Quaternion.Inverse(prev.adjustedRot);

            var torque = Vector3.zero;
            var absAngle = 0f;

            if (cfg.rotAxes == Axis.None)
            {
                torque = GetRotTorque(ref cfg, ref curr, ref prev, dt, animLen, delta, out absAngle);


                //var rot = angVelocityLen == 0f ?
                //    prev.adjustedRot :
                //    Quaternion.AngleAxis(angVelocityLen * dt * cfg.rotRate, angVel * (1f / angVelocityLen)) * prev.adjustedRot;
                var adjustedRot = Quaternion.Euler(torque * (dt * config.rotRate)) * prev.adjustedRot;

                prev.adjustedRot = adjustedRot;
                prev.torque = torque;

                var result = currCleanRotInverse * adjustedRot;

                if (absAngle > 45f)
                {
                    var resultAngle = Quaternion.Angle(Quaternion.identity, result);

                    if (resultAngle > 45f)
                    {
                        result = Quaternion.Slerp(Quaternion.identity, result, 45f / resultAngle);
                        prev.adjustedRot = currCleanRot * result;
                    }
                }
                var resultEuler = result.eulerAngles;

                return resultEuler;
            }
            else
            {
                foreach (var axis in axisValue)
                {
                    if ((cfg.rotAxes & axis) != 0)
                    {
                        var idx = (int)axis >> 1;

                        torque[idx] = GetAxialTorque(ref cfg, ref curr, ref prev, idx, dt, animLen, delta, out var tempAbsAngle);
                        
                        absAngle += tempAbsAngle;
                    }
                }

                //absAngle *= cfg.rotActiveAxesInv;


                var adjustedRot = prev.adjustedRot * Quaternion.Euler(torque * (dt * config.rotRate));

                prev.adjustedRot = adjustedRot;
                prev.torque = torque;

                var result = currCleanRotInverse * adjustedRot;

                var resultEuler = result.eulerAngles;

                return resultEuler;
            }
        }

        private Vector3 GetRotTorque(ref Config cfg, ref Current curr, ref Previous prev, float dt, float animLen, Quaternion delta, out float absAngle)
        {
            delta.ToAngleAxis(out var angle, out var axis);

            if (angle > 180f)
                angle -= 360f;

            absAngle = Mathf.Abs(angle);
            var accel = Vector3.zero;
            var torque = prev.torque;


            var angleFactorDec = 1f + (absAngle * dt);
            angleFactorDec = 1f / (angleFactorDec * angleFactorDec);

            var angleFactorInc = FastExp(absAngle * dt);

            // --- Filter corruption & Apply acceleration ---

            if (!float.IsInfinity(axis.x))
            {

                var deltaAngVel = axis * (angle * angleFactorInc);

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

            if (cfg.noiseRotAmpl != 0f)
            {
                accel += Vector3.Scale(cfg.noiseRotFactor, GerPerlinVec(
                    cfg.noiseRotVec,
                    cfg.noiseRotAmpl * curr.noiseAmplFactor,
                    curr.noiseFreqStep,
                    out cfg.noiseRotVec
                    ));
            }
            var dampingF = torque * (-cfg.rotDamping * dt * (-angleFactorDec + 2f));

            torque += accel + dampingF;

            //torque += accel * dt;

            var torqueDot = Vector3.Dot(torque, prev.torque);

            if (curr.highTorque)
            {
                if (torqueDot < 1f)
                {
                    curr.highTorque = false;
                    var freezeAngFactor = absAngle * (1f / 45f);
                    curr.rotFreezeTime = cfg.rotFreezeTime * animLen * freezeAngFactor;
                    //Time.timeScale = 0f;

                    if ((showDebug & Effect.Rot) != 0)
                        AniMorphPlugin.Logger.LogInfo($"[{transform.name}] – GetRotOffset: " +
                            $"RotFreeze! " +
                            $"freezeTime[{curr.rotFreezeTime:F3} absAngle[{absAngle:F3}]");
                }
            }
            else if (torqueDot > 1000f)
            {
                curr.highTorque = true;
            }

            if ((showDebug & Effect.Rot) != 0)
                AniMorphPlugin.Logger.LogDebug($"[{transform.name}] – GetRotOffset: " +
                    $"torqueDot[{torqueDot:F3}] absAngle[{absAngle:F3}] " +
                    $"factorInc[{angleFactorInc:F3}] factorDec[{angleFactorDec:F3}]");

            return torque;
        }

        private float GetAxialTorque(ref Config cfg, ref Current curr, ref Previous prev, int idx, float dt, float animLen, Quaternion delta, out float absAngle)
        {
            var axis = idx switch
            {
                0 => transform.right,
                1 => transform.up,
                2 => transform.forward,
                _ => throw new NotImplementedException(nameof(idx))
            };

            delta = ExtractTwist(delta, axis);

            var deltaEuler = delta.eulerAngles;

            var angle = deltaEuler[idx];

            if (angle > 180f)
                angle -= 360f;

            absAngle = Mathf.Abs(angle);
            var accel = 0f;
            var torque = prev.torque[idx];

            var angleFactorDec = 1f + (absAngle * dt);

            angleFactorDec = 1f / (angleFactorDec * angleFactorDec);

            var angleFactorInc = FastExp(absAngle * dt);


            // --- Filter corruption & Apply acceleration ---

            if (absAngle > 0f)
            {
                var angVel = angle * angleFactorInc;

                //var slowLerp = 1f - FastExp(-cfg.rotSlowSmooth * dt);
                //var fastLerp = 1f - FastExp(-cfg.rotFastSmooth * dt);

                var slowLerp = cfg.rotSlowSmooth * dt;
                var fastLerp = cfg.rotFastSmooth * dt;

                var prevSlowRotDelta = prev.slowRotDelta[idx];
                var prevFastRotDelta = prev.fastRotDelta[idx];

                prevSlowRotDelta = Mathf.Lerp(prevSlowRotDelta, angVel, slowLerp);
                prevFastRotDelta = Mathf.Lerp(prevFastRotDelta, angVel, fastLerp);

                //var highFreqDelta = prev.fastRotDelta - prev.slowRotDelta;

                var targetVel = Mathf.Lerp(
                    prevSlowRotDelta,
                    prevFastRotDelta,
                    cfg.rotHighFreqInf
                );
                //accel = cfg.rotSpring * (targetVel - angVel);
                accel = cfg.rotSpring * targetVel;

                prev.slowRotDelta[idx] = prevSlowRotDelta;
                prev.fastRotDelta[idx] = prevFastRotDelta;
            }

            if (cfg.noiseRotAmpl != 0f)
            {
                accel += cfg.noiseRotFactor[idx] * GerPerlinNum(
                    cfg.noiseRotVec[idx],
                    cfg.noiseRotAmpl * curr.noiseAmplFactor,
                    curr.noiseFreqStep,
                    out var noiseRotNum,
                    idx
                    );

                cfg.noiseRotVec[idx] = noiseRotNum;
            }

            var dampingF = torque * (-cfg.rotDamping * dt * (-angleFactorDec + 2f));

            torque += accel + dampingF;

            var torqueDot = torque * prev.torque[idx];

            if (curr.highTorque)
            {
                if (torqueDot < 1f)
                {
                    curr.highTorque = false;
                    var freezeAngFactor = absAngle * (1f / 45f);
                    curr.rotFreezeTime = cfg.rotFreezeTime * animLen * freezeAngFactor;
                    //Time.timeScale = 0f;

                    if ((showDebug & Effect.Rot) != 0)
                        AniMorphPlugin.Logger.LogInfo($"[{transform.name}] – GetRotOffset: " +
                            $"RotFreeze! " +
                            $"freezeTime[{curr.rotFreezeTime:F3} absAngle[{absAngle:F3}]");
                }
            }
            else if (torqueDot > 500f)
            {
                curr.highTorque = true;
            }

            if ((showDebug & Effect.Rot) != 0)
                AniMorphPlugin.Logger.LogDebug($"[{transform.name}] – GetRotOffset: " +
                    $"torque[{torque:F3}] " +
                    $"torqueDot[{torqueDot:F3}] absAngle[{absAngle:F3}] " +
                    $"factorInc[{angleFactorInc:F3}] factorDec[{angleFactorDec:F3}]");

            return torque;
        }

        private Quaternion ExtractTwist(Quaternion q, Vector3 axis)
        {
            var r = new Vector3(q.x, q.y, q.z);

            var projected = Vector3.Project(r, axis);

            var twist = new Quaternion(
                projected.x,
                projected.y,
                projected.z,
                q.w
            );
#if KK
            return LBUtils.LBUtilsMath.Normalize(twist);
#else
            return Quaternion.Normalize(twist);
#endif
        }


        #endregion


        #region Scale


        //private float devSclDeadZone = 0f; //0.005f;

        //protected Vector3 GetSquashOffset(ref Config cfg, ref Current curr, ref Previous prev, Vector3 vel, float dt)
        //{
        //    var accel = prev.cleanDeltaPos;

        //    var driver = Vector3.Lerp(accel, vel, _dev.devCoef_3); // cfg.sclBlend);

        //    var driverLen = driver.magnitude;

        //    driverLen = Mathf.Max(0f, driverLen - devSclDeadZone);

        //    if (driverLen == 0f)
        //    {
        //        var basicTargetScl = Vector3.one;

        //        if (cfg.noiseSclAmpl != 0f)
        //        {
        //            basicTargetScl += Vector3.Scale(cfg.noiseSclFactor, GetNoiseVec(
        //                cfg.noiseSclVec,
        //                cfg.noiseSclAmpl * curr.noiseAmplFactor,
        //                curr.noiseFreq,
        //                out cfg.noiseSclVec
        //                ));
        //        }

        //        var staticResult = Vector3.Lerp(prev.scale, basicTargetScl, dt * cfg.sclRate);
        //        //var staticResult = Vector3.SmoothDamp(
        //        //    prev.scale,
        //        //    basicTargetScl,
        //        //    ref cfg.sclVelocity,
        //        //    cfg.sclRate
        //        //    );


        //        prev.scale = staticResult;

        //        return staticResult;
        //    }


        //    //var velN = vel.normalized;
        //    //var velDelta = (vel - prev.velocity);

        //    //// dot > 0 means acceleration,
        //    //// dot < 0 means deceleration.
        //    //var projDotVel = Vector3.Dot(velN, velDelta);

        //    //if (projDotVel > 0f)
        //    //    projDotVel += dt * 0.1f;

        //    //prev.sclTotalDecel = Mathf.Clamp01(prev.sclTotalDecel - projDotVel);

        //    /* Separate stretch & recovery speeds
        //     * 
        //     * float stretchSpeed = 12f;
        //     * float recoverySpeed = 6f;
        //     * 
        //     * float speed = (targetScale.magnitude > currentScale.magnitude)
        //     * ? stretchSpeed
        //     * : recoverySpeed;
        //     */

        //    var dir = Vector3.Lerp(prev.sclDir, driver, dt * _dev.devCoef_2);

        //    prev.sclDir = dir;

        //    var dirN = dir.normalized;
        //    //var velAccelFactor = Mathf.Pow(driverLen * squashStrength, 1.3f);
        //    var factor = driverLen * cfg.sclStr;
        //    factor *= FastExp(factor * OneThird);
        //    var stretch = 1f + Mathf.Clamp(factor, 0f, cfg.sclDistort);
        //    var squash = Mathf.Clamp(1f / Mathf.Sqrt(stretch), 1f - cfg.sclDistort, 1f);
        //    // Exaggerated
        //    //var squash = Mathf.Clamp(1f / Mathf.Pow(stretch, 0.6f), maxSquash, 1f);


        //    var targetScale = GetDirectionalScale(dirN, stretch, squash);

        //    if (cfg.noiseSclAmpl != 0f)
        //    {
        //        targetScale += Vector3.Scale(cfg.noiseSclFactor, GetNoiseVec(
        //            cfg.noiseSclVec,
        //            cfg.noiseSclAmpl * curr.noiseAmplFactor,
        //            curr.noiseFreq,
        //            out cfg.noiseSclVec
        //            )); 
        //    }


        //    var result = Vector3.Lerp(prev.scale, targetScale, dt * cfg.sclRate);

        //    //var result = Vector3.SmoothDamp(
        //    //    prev.scale,
        //    //    targetScale,
        //    //    ref cfg.sclVelocity,
        //    //    cfg.sclRate
        //    //    );

        //    if (cfg.sclPreserveVolume)
        //    {
        //        var stretchVolume = result.x * result.y * result.z;
        //        var volumeCorrection = Mathf.Pow(1f / stretchVolume, OneThird);
        //        result *= volumeCorrection;
        //    }

        //    prev.scale = result;

        //    var dev = _dev;

        //    dev.sclVel = vel;
        //    dev.sclAccel = accel;
        //    dev.sclDriverLen = driverLen;
        //    dev.sclFactor = factor;
        //    dev.sclStretch = stretch;
        //    dev.sclSquash = squash;

        //    return result;
        //}

        private Vector3 GetDirectionalScale(Vector3 dirN, float stretch, float squash)
        {
            return new Vector3(
                Mathf.Lerp(squash, stretch, Mathf.Abs(dirN.x)),
                Mathf.Lerp(squash, stretch, Mathf.Abs(dirN.y)),
                Mathf.Lerp(squash, stretch, Mathf.Abs(dirN.z))
            );
        }

        protected Vector3 GetSquashOffsetEx(ref Config cfg, ref Current curr, ref Previous prev, float dt, float dtInv)
        {
            var driver = prev.sclDriver;

            if (curr.shockTime > 0f)
            {
                var shockScl = Vector3.Lerp(prev.scl, prev.sclTarget, dt * cfg.sclRate * _dev.devCoef_4);

                prev.scl = shockScl;

                return shockScl;
            }


            if (curr.bleedTime > 0f)
                driver *= 1f - (cfg.sclBleedStr * dt);


            var springCoef = 1f;

            var springF = springCoef * cfg.sclSpring * curr.cleanDeltaPos;
            var dampingF = -cfg.sclDamping * dt * driver;

            driver += springF + dampingF;
#if DEBUG
            if ((showDebug & Effect.Scl) != 0)
                AniMorphPlugin.Logger.LogDebug($"[{transform.name}] " +
                    $"driver({driver.x:F3},{driver.y:F3},{driver.z:F3}) " +
                    $"springF({springF.x:F3},{springF.y:F3},{springF.z:F3}), " +
                    $"dampingF({dampingF.x:F3},{dampingF.y:F3},{dampingF.z:F3}) " +
                    $"");
#endif

            if (cfg.noiseSclAmpl != 0f)
            {
                driver += Vector3.Scale(cfg.noiseSclFactor, GerPerlinVec(
                    cfg.noiseSclVec,
                    cfg.noiseSclAmpl * curr.noiseAmplFactor,
                    curr.noiseFreqStep,
                    out cfg.noiseSclVec                    
                    ));
            }

            var driverLen = driver.magnitude;

            if (driverLen == 0f)
            {
                var toNormalScl = Vector3.Lerp(prev.scl, vecOne, dt * cfg.sclRate * _dev.devCoef_4);

                prev.scl = toNormalScl;

                return toNormalScl;
            }

            var driverN = driver / driverLen;

            var factor = SmoothStepN(driverLen * _dev.devCoef_5);
            var distort = cfg.sclDistort;

            var stretch = 1f + Mathf.Clamp(factor, 0f, distort);
            // TODO. This is for Vector3, should have a Vector2 implementation too as thighs require it.
            var squash = 1f / Mathf.Sqrt(stretch);

            var targetScl = GetDirectionalScale(driverN, stretch, squash);

            var result = Vector3.Lerp(prev.scl, targetScl, dt * cfg.sclRate);

            if (cfg.sclPreserveVolume)
            {
                var vol = result.x * result.y * result.z;
                var volCorrect = Mathf.Pow(1f / vol, OneThird);
                result *= volCorrect;
            }

            prev.scl = result;
            prev.sclTarget = targetScl;
            prev.sclDriver = driver;

#if DEBUG
            var dev = _dev;

            dev.sclDriver = driver;
            dev.sclDriverN = driverN;
            dev.sclDriverLen = driverLen;
            dev.sclFactor = factor;
            dev.sclStretch = stretch;
            dev.sclSquash = squash;
#endif

            return result;
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

            return result;
        }

        protected Vector3 GetSclDotOffset(ref Config cfg, ref Current curr, float dotFwd)
        {
            var result = Vector3.Lerp(vecOne, dotFwd > 0f ? cfg.sclOffsetFaceUp : cfg.sclOffsetFaceDown, Mathf.Abs(dotFwd));

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


        #region Setting Methods


        internal virtual void Reset()
        {
            var bone = transform;

            UpdateAnimRot(baseConfig.initAnimRot);

            current.Clear();

            abmxModifierData?.Clear();
            

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

        private void UpdateRotCollectBaseline(bool dynamic)
        {
            ConsecutiveFrameCounter = 0;
#if DEBUG
            AniMorphPlugin.Logger.LogWarning($"[{transform.name}] [UpdateDynamicRot] isDynamic[{dynamic}]");
#endif
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

            // --- Clean-up effects ---
            foreach (Effect effect in effects)
            {
                if ((baseConfig.allowedEffects & effect) != 0) continue;

                cfg.effects &= ~effect;
            }

            active = cfg.effects != Effect.None || cfg.inheritEffects != Effect.None;

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

            if (isPos)
            {
                cfg.posSpring = baseCfg.posSpringCfg * pluginConfig.PosSpring.Value;
                cfg.posDamping = baseCfg.posDampingCfg * pluginConfig.PosDamping.Value;
                cfg.posShockThreshold = pluginConfig.PosShockThreshold.Value;
                cfg.posShockStr = pluginConfig.PosShockStr.Value;
                cfg.freezeThreshold = pluginConfig.PosFreezeThreshold.Value;
                cfg.freezeLen = pluginConfig.PosFreezeLen.Value;
                cfg.posBleedStr = pluginConfig.PosBleedStr.Value;
                cfg.bleedLen = pluginConfig.PosBleedLen.Value;
            }

            if (isRot)
            {
                cfg.rotSpring = baseCfg.rotSpringCfg * pluginConfig.RotSpring.Value;
                cfg.rotDamping = baseCfg.rotDampingCfg * pluginConfig.RotDamping.Value;
                cfg.rotRate = baseCfg.rotRateCfg * pluginConfig.RotRate.Value;


                // --- Find Active Axes ---

                cfg.rotAxes = Axis.None;

                var activeAxes = 0;
                var rotAxes = baseCfg.rotApplication;

                for (var i = 0; i < 3; i++)
                {
                    if (rotAxes[i] == 0f) continue;

                    activeAxes++;
                    cfg.rotAxes |= (Axis)(1 << i);
                }

                // All axes are active.
                if (activeAxes == 3)
                {
                    cfg.rotAxes = Axis.None;
                }
                else
                {
                    cfg.rotActiveAxesInv = 1f / activeAxes;
                }
            }

            if (isScl)
            {
                cfg.sclSpring = baseCfg.sclSpringCfg * pluginConfig.SclSpring.Value;
                cfg.sclDamping = baseCfg.sclDampingCfg * pluginConfig.SclDamping.Value;
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

                var dotScl_posFactor = baseCfg.dotScl_pos;
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

            cfg.noisePosAmpl = (isPos && pluginConfig.NoiseAmplitudePos != null) ? baseCfg.noisePosCfg * pluginConfig.NoiseAmplitudePos.Value : 0f;
            cfg.noiseRotAmpl = (isRot && pluginConfig.NoiseAmplitudeRot != null) ? baseCfg.noiseRotCfg * (pluginConfig.NoiseAmplitudeRot.Value * 1000f) : 0f;
            cfg.noiseSclAmpl = (isScl && pluginConfig.NoiseAmplitudeScl != null) ? baseCfg.noiseSclCfg * (pluginConfig.NoiseAmplitudeScl.Value * 10f) : 0f;

            cfg.noisePosFactor = baseCfg.noisePosF;
            cfg.noiseRotFactor = baseCfg.noiseRotF;
            cfg.noiseSclFactor = baseCfg.noiseSclF;


            cfg.noisePosVec = new(Random.Range(0f, 1000f), Random.Range(0f, 1000f), Random.Range(0f, 1000f));
            cfg.noiseRotVec = new(Random.Range(0f, 1000f), Random.Range(0f, 1000f), Random.Range(0f, 1000f));
            cfg.noiseSclVec = new(Random.Range(0f, 1000f), Random.Range(0f, 1000f), Random.Range(0f, 1000f));

            cfg.noiseFreq = (float)Math.Round(0.75f * Random.Range(0.85f, 1.15f), 2);

            cfg.devPosFastCoef = AniMorphPlugin.DevPosFastCoef.Value;

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
            

            static Vector3 ScaleVecFwd(Vector3 vec, float perpAxisFactor)
            {
                if (perpAxisFactor < 0f) throw new ArgumentOutOfRangeException(nameof(perpAxisFactor));

                var j = 0;
                var bigAxis = Mathf.Abs(vec.x);

                for (var i = 1; i < 3; i++)
                {
                    var absAxis = Mathf.Abs(vec[i]);

                    if (absAxis > bigAxis)
                    {
                        j = i;
                        bigAxis = absAxis;
                    }
                }

                // Convert small offset to the scale value.
                vec[j] += 1f;

                var sqrt = Mathf.Sqrt(1f / vec[j]);

                var perpAxis = 1f + ((1f * sqrt - 1f) * perpAxisFactor);

                for (var i = 0; i < 3; i++)
                {
                    if (i == j) continue;

                    vec[i] = perpAxis;
                }

                return vec;
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

        internal void SetMass(float value)
        {
            if (value <= 0f) value = 1f;

            ref var cfg = ref config;

            cfg.mass = value;
            cfg.massInv = 1f / value;
        }

        internal void OnSetClothesState(Body body, ChaControl chara)
        {
            var pluginConfig = ConfigDic[body];

            if (pluginConfig.DisableWhenClothes == null) return;

            var settingValue = pluginConfig.DisableWhenClothes.Value;

            if (settingValue == 0 || chara.objClothes == null) return;

            var slotList = new List<int>();

            foreach (var enumValue in clothesKindValues)
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
            current.OnAnimationLoopStart(animLoopFrameCountInv, dt);
        }


        #endregion


        #region Misc Functions


        private Vector3 GerPerlinVec(Vector3 noiseVec, float ampl, float freq, out Vector3 outNoiseVec)
        {
            outNoiseVec = new(noiseVec.x + freq, noiseVec.y + freq, noiseVec.z + freq);

            var x = 0f;
            var y = 0f;
            var z = 0f;

            for (var i = 0; i < 4; i++)
            {
                x += (Mathf.PerlinNoise(noiseVec.x + freq, 0f) - 0.5f) * ampl;

                y += (Mathf.PerlinNoise(0f, noiseVec.y + freq) - 0.5f) * ampl;

                var zArg = noiseVec.z + freq;
                z += (Mathf.PerlinNoise(zArg, zArg) - 0.5f) * ampl;

                ampl *= 0.5f;
                freq *= 2f;
            }

            noiseVec = new Vector3(x, y, z);

            //AniMorphPlugin.Logger.LogDebug($"[{transform.name}][{Time.frameCount}] " +
            //    $"noiseVec({noiseVec.x:F3},{noiseVec.y:F3},{noiseVec.z:F3}) ampl[{ampl:F3}] freq[{freq:F3}] " +
            //    $"outNoiseVec({outNoiseVec.x:F3},{outNoiseVec.y:F3},{outNoiseVec.z:F3})");

            return noiseVec;
        }
        private float GerPerlinNum(float noiseNum, float ampl, float freq, out float outNoiseNum, float idx = 0)
        {
            outNoiseNum = noiseNum + freq;

            var num = 0f;

            if (idx == 0)
            {
                for (var i = 0; i < 4; i++)
                {
                    num += (Mathf.PerlinNoise(noiseNum + freq, 0f) - 0.5f) * ampl;

                    ampl *= 0.5f;
                    freq *= 2f;
                }
            }
            else if (idx == 1)
            {
                for (var i = 0; i < 4; i++)
                {
                    num += (Mathf.PerlinNoise(0f, noiseNum + freq) - 0.5f) * ampl;

                    ampl *= 0.5f;
                    freq *= 2f;
                }
            }
            else
            {
                for (var i = 0; i < 4; i++)
                {
                    var zArg = noiseNum + freq;
                    num += (Mathf.PerlinNoise(zArg, zArg) - 0.5f) * ampl;

                    ampl *= 0.5f;
                    freq *= 2f;
                }
            }

            return num;
        }

        private float FastExp(float t)
        {
            // at t < 0 stable until t > -0.3
            // at t > 0 stable all the way through but starts rapid ascend a tad later
            var u = t * t;
            return 1f + t + u * (0.5f + u * 0.144f);
        }

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


        #endregion


        #region Interpolations


        float ApplyInterpolation(float t, Interp interp, float p1, float p2)
        {
            return interp switch
            {
                Interp.Smooth => SmoothStepN(t),
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
        float SmoothStepN(float t) => t * t * (3f - 2f * t); 
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

        protected class Config
        {
            internal Config(
                Effect inheritEffects, 

                Vector3 posAppPositive, 
                Vector3 posAppNegative, 
                Vector3 rotApplication, 
                Vector3 sclApplication,
                
                float inheritPosF
                )
            {
                this.inheritEffects = inheritEffects;

                this.posAppPositive = posAppPositive;
                this.posAppNegative = posAppNegative;
                this.rotApplication = rotApplication;
                this.sclApplication = sclApplication;

                this.inheritPosF = inheritPosF;
            }

            internal Effect effects;
            internal Effect inheritEffects;

            internal Vector3 posAppPositive;
            internal Vector3 posAppNegative;
            internal Vector3 rotApplication;
            internal Vector3 sclApplication;

            internal float noiseFreq;
            internal Vector3 noisePosVec;
            internal Vector3 noiseRotVec;
            internal Vector3 noiseSclVec;

            internal float noisePosAmpl;
            internal float noiseRotAmpl;
            internal float noiseSclAmpl;

            internal Vector3 noisePosFactor;
            internal Vector3 noiseRotFactor;
            internal Vector3 noiseSclFactor;

            internal float posSpring;
            internal float posDamping;
            internal float posShockThreshold;
            internal float posShockStr;
            internal float freezeThreshold;
            internal float freezeLen;
            internal float posBleedStr;
            internal float bleedLen;
            internal Vector3 posLimitPositive;
            internal Vector3 posLimitNegative;
            //internal float posDamping = 2f * Mathf.Sqrt(posSpring * mass);

            internal float mass = 1f;
            internal float massInv = 1f;

            internal float rotSpring;
            internal float rotDamping;
            internal float rotRate;
            internal float rotFreezeTime = 0.1f;
            internal Axis  rotAxes;
            internal float rotActiveAxesInv;

            internal float sclSpring;
            internal float sclDamping;
            internal float sclRate;
            internal float sclDistort;
            internal float sclBleedStr = 5f;

            //internal float sclStr; 
            //internal float sclRateInv;

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
            internal float rotSlowSmooth = 25f;
            // (5 .. 12)
            internal float rotFastSmooth = 8f;
            // (0.1 .. 0.3)
            internal float rotHighFreqInf = 0.2f;

            internal float inheritPosF;

            internal float devPosFastCoef;


        }

        protected class Current
        {
            internal bool highTorque;
            internal Axis highTorqueAxial;

            internal float shockTime;
            internal float bleedTime;
            internal float noiseAmplFactor;
            internal float noiseFreqStep;
            internal Quaternion cleanLocalRot;
            internal Quaternion cleanRotInverse;

            internal bool shock;
            internal bool bleed;
            internal bool freeze;

            internal bool animRot;

            internal float avgPosLen;
            internal float avgPosLenInv;


            internal float rotFreezeTime;

            internal Vector3 cleanDeltaPos
            {
                get;
                set
                {
                    field = value;

                    var len = value.magnitude;

                    cleanDeltaPosLen = len;
                    totalPosLen += len;
                }
            }
            internal Vector3 cleanVelDelta;
            internal float cleanDeltaPosLen;

            private float totalPosLen;

            internal void Clear()
            {
                highTorque = false;

                shockTime = 0f;
                bleedTime = 0f;
                totalPosLen = 0f;

                cleanDeltaPos = Vector3.zero;
                cleanDeltaPosLen = 0f;

                cleanVelDelta = Vector3.zero;
            }
            internal void OnAnimationLoopStart(float animLoopFrameCountInv, float dt)
            {
                avgPosLen = Mathf.Lerp(avgPosLen, totalPosLen * animLoopFrameCountInv, dt * 10f);

                if (avgPosLen != 0f)
                    avgPosLenInv = 1f / avgPosLen;

                totalPosLen = 0f;
            }
        }
        
        // TODO class -> struct
        protected /*struct*/ class Previous
        {
            internal Vector3 velocity;
            internal float velocityLen;

            internal Vector3 torque;


            // --- Scale ---

            internal Vector3 scl;
            internal Vector3 sclDriver;
            internal Vector3 sclTarget;


            //

            internal Vector3 position;
            internal Vector3 cleanPos;
            //internal Vector3 localAdjVec;

            internal Vector3 cleanDeltaPos;
            internal float cleanDeltaPosLen;
            internal Vector3 cleanVelDelta;

            internal Vector3 posOffset;
            internal Vector3 rotOffset;
            internal Vector3 sclOffset;

            internal float posSpringVelCoef;

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
                torque = Vector3.zero;
                position = pos;
                cleanPos = pos;
                posOffset = Vector3.zero;
                rotOffset = Vector3.zero;
                adjustedRot = bone.rotation;
                scl = Vector3.one;
                sclDriver = Vector3.zero;
                sclTotalAccel = 0f;
                sclTotalDecel = 0f;

                cleanDeltaPos = Vector3.zero;
                cleanVelDelta = Vector3.zero;
                cleanDeltaPosLen = 0f;
            }

        }

        private class DebugValues
        {
            internal float posSpringVelCoef;
            internal float posSpringPosCoef;
            internal Vector3 posSpring;
            internal Vector3 posDamping;

            internal float devCoef_1 = 20f;
            internal float devCoef_2 = 1f;
            internal float devCoef_3 = 1f;
            internal float devCoef_4 = 1f;
            internal float devCoef_5 = 1f;

            internal Vector3 devRotApp = Vector3.one;

            internal Vector3 sclDriverN;
            internal Vector3 sclDriver;
            internal float sclDriverLen;
            internal float sclFactor;
            internal float sclStretch;
            internal float sclSquash;
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
