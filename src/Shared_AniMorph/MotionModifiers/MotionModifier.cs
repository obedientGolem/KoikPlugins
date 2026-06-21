using ADV.Commands.Base;
using KKABMX.Core;
using LBUtils;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Policy;
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

        private BoneController _boneController;


        private readonly float _baseScaleVolume;
        private readonly float _baseScaleMagnitude;

        //private BoneModifierData _combineModifiersCachedReturn;

        private int _prevFrameCount;
#if DEBUG
        private readonly DebugValues _dev = new();

        protected Effect showDebug_1;
        protected Effect showDebug_2;
        protected Effect showDebug_3;

        protected Effect advDebug_1;
        protected Effect advDebug_2;
        protected Effect advDebug_3;
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
            prev.pos = pos;
            prev.posClean = pos;
            //previous.cleanRot = bone.rotation;
            prev.localRot = bone.localRotation;
            prev.adjustedRot = Quaternion.identity;
            _baseScaleVolume = bone.localScale.x * bone.localScale.y * bone.localScale.z;
            _baseScaleMagnitude = bone.localScale.magnitude;

            UpdateAnimRot(baseCfg.initAnimRot);

            if (centeredBone != null)
            {
                tether = new Tethering(centeredBone, prev.pos);

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

        private bool devPosOffsetRemap;

        internal virtual void UpdateModifier(float dt, float dtInv, float animSpeed, float animSpeedInv, float animSpeedF)
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

            curr.noiseAmplFactor = animSpeedF * Mathf.Min(1f, curr.posAvgLen * (1f + TwoThirds));
            curr.noiseFreqStep = cfg.noiseFreq * animSpeedF * dt;


            // --- Update Offsets ---

            UpdatePosTracking(ref prev, ref curr, dtInv);
            //UpdateVelocityShock(ref cfg, ref curr, ref prev, dt, dtInv);

            if ((effects & Effect.Pos) != 0)
                posOffset = GetPosOffset(ref cfg, ref curr, ref prev, dt, dtInv, animSpeed, animSpeedInv);

            if ((effects & Effect.Rot) != 0)
                rotOffset = GetRotOffset(ref cfg, ref curr, ref prev, dt, dtInv, animSpeed, animSpeedInv);
            else
                curr.rotCleanLocal = GetCleanLocalRot(ref prev);

            if ((effects & Effect.Scl) != 0)
                sclOffset = GetSquashOffsetEx(ref cfg, ref curr, ref prev, dt, dtInv);

            if ((effects & Effect.Tether) != 0)
                rotOffset += tether.GetTetheringOffset(prev.vel, dt);


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

            if (devPosOffsetRemap)
            {
                posOffset = new Vector3(
                    posSignScale.x < 1f ? ((1f - posSignScale.x) * -posOffset.x) + posSignScale.x * posOffset.x : posSignScale.x * posOffset.x,
                    posSignScale.y < 1f ? ((1f - posSignScale.y) * -posOffset.y) + posSignScale.y * posOffset.y : posSignScale.y * posOffset.y,
                    posSignScale.z < 1f ? ((1f - posSignScale.z) * -posOffset.z) + posSignScale.z * posOffset.z : posSignScale.z * posOffset.z
                    );
            }
            else
            {
                posOffset = Vector3.Scale(posOffset, posSignScale);
            }

            


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

            boneModifierData.PositionModifier = curr.rotCleanLocal * posOffset;
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
            prev.posCleanDelta = curr.posCleanDelta;
            prev.velCleanDelta = curr.velCleanDelta;


            // --- Position ---

            var posSubFx = curr.posSubFx;

            if ((posSubFx & SubFx.Freeze) != 0)
            {
                if (curr.posTimeFreeze < dt)
                    curr.posSubFx &= ~SubFx.Freeze;
                else
                    curr.posTimeFreeze -= dt;
            }
            else if ((posSubFx & SubFx.Slowdown) != 0)
            {
                if (curr.posTimeSlowdown < dt)
                    curr.posSubFx &= ~SubFx.Slowdown;
                else
                    curr.posTimeSlowdown -= dt;
            }
            else if ((posSubFx & SubFx.Impulse) != 0)
            {
                if (curr.posTimeImpulse < dt)
                    curr.posSubFx &= ~SubFx.Impulse;
                else
                    curr.posTimeImpulse -= dt;
            }

            if ((posSubFx & SubFx.Bleed) != 0)
            {
                if (curr.posTimeBleed < dt)
                    curr.posSubFx &= ~SubFx.Bleed;
                else
                    curr.posTimeBleed -= dt;
            }


            // --- Rotation ---

            if (curr.rotFreeze)
            {
                if (curr.rotFreezeTime < dt)
                    curr.rotFreeze = false;
                else
                    curr.rotFreezeTime -= dt;
            }
            else if (curr.rotBleed)
            {
                if (curr.rotBleedTime < dt)
                    curr.rotBleed = false;
                else
                    curr.rotBleedTime -= dt;
            }
        }

        protected void UpdatePosTracking(ref Previous prev, ref Current curr, float dtInv)
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

            Vector3 cleanDelta;
            Vector3 cleanPos;
            var currPos = transform.position;


            // --- Delta position ---


            // --- Reset by the animator ---

            if (x && y && z)
            {
                cleanPos = currPos;
                cleanDelta = transform.InverseTransformPoint(prev.posClean);
            }

            // --- Isn't reset by the animator ---

            else if (!x && !y && !z)
            {
                cleanPos = transform.TransformPoint(-prev.posOffset);
                cleanDelta = transform.InverseTransformDirection(prev.posClean - cleanPos);
            }

            // --- The Illusion way ---

            else
            {
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

                var vecDynamic = transform.InverseTransformPoint(prev.pos);
                var vecStatic = transform.InverseTransformDirection(prev.posClean - cleanPos);

                cleanDelta = new Vector3(
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

            var cleanDeltaLen = cleanDelta.magnitude;

            curr.posCleanDelta = cleanDelta;
            curr.posCleanDeltaLen = cleanDeltaLen;
            curr.posCleanDeltaTotalLen += cleanDeltaLen * dtInv;

            prev.posClean = cleanPos;
            prev.pos = currPos;
        }

        //protected void UpdateVelocityShock(ref Config cfg, ref Current curr, ref Previous prev, float dt, float dtInv)
        //{
        //    // A simple velocity, based on movements of a transform without our interference.
        //    var velDelta = curr.posCleanDelta - prev.posCleanDelta;

        //    curr.velCleanDelta = velDelta;

        //    if (curr.posFreeze || curr.posImpulse || curr.posBleed)
        //        return;


        //    // --- Shock detection --- 

        //    var velDot = Vector3.Dot(velDelta, prev.velCleanDelta);

        //    var velDeltaLen = velDelta.magnitude * dtInv;

        //    var isDot = velDot < 0f;

        //    if ((showDebug_2 & Effect.Pos) != 0)
        //        AniMorphPlugin.Logger.LogDebug($"[{transform.name}] – " +
        //            $"UpdateVelocityShock: " +
        //            $"velDot[{velDot:F5}] " +
        //            $"velDeltaLen[{velDeltaLen:F3}] " +
        //            $"velDot[{velDot:F3}] < 0 [{velDot < 0f}] " +
        //            $"");
        //    if ((advDebug_2 & Effect.Pos) != 0 && velDot < 0f)
        //        Time.timeScale = 0f;

        //    //if (velDeltaLen > cfg.posShockThreshold || (isDot && (velDeltaLen > cfg.posShockThreshold * OneThird)))
        //    //{
        //    //    //var shockPower = Mathf.Pow(Mathf.Sqrt(cleanVelDeltaSqrLen), 1.2f);
        //    //    //velocity += cleanVelDelta.normalized * shockPower * shockFactor;

        //    //    //prev.velocity += cleanVelDelta * cfg.posShockStr;
        //    //    // Inverse target scale.
        //    //    // TODO. Add pseudo vec2 option for thighs and such.
        //    //    //prev.sclTarget = vecOne - (prev.sclTarget - vecOne);

        //    //    //var fpsFactor = dtInv * (1f / 60f);
        //    //    var animFactor = animLen; // * (1f / 2.25f); // * (fpsFactor * fpsFactor);
        //    //    // TODO Add slowdown instead of freeze?
        //    //    curr.posBleedTime = cfg.bleedLen * animFactor;
        //    //    curr.posBleed = true;

        //    //    //if (cleanVelDeltaLen > cfg.posFreezeThreshold || isDot)
        //    //    //{
        //    //        curr.posShockTime = cfg.freezeLen * animFactor;
        //    //    curr.posShock = true;

        //    //        //AniMorphPlugin.Logger.LogWarning($"[{transform.name}] " +
        //    //        //    $"Freeze[{curr.shockTime:F4}]! scaleFactor[{scaleFactor:F3}] fpsFactor[{fpsFactor}] projectDot[{projectDot < 0f}]" +
        //    //        //    $"");
        //    //    //}
        //    //    //else
        //    //    //{

        //    //    //    AniMorphPlugin.Logger.LogInfo($"[{transform.name}] " +
        //    //    //        $"Shock[{curr.shockTime:F4}]! scaleFactor[{scaleFactor:F3}] fpsFactor[{fpsFactor}] projectDot[{projectDot < 0f}]" +
        //    //    //        $"");
        //    //    //}
        //    //}
        //}


        #endregion


        #region Position


        protected Vector3 GetPosOffset(ref Config cfg, ref Current curr, ref Previous prev, float dt, float dtInv, float animSpeed, float animSpeedInv)
        {
            var posDeltaLen = curr.posCleanDeltaLen;
            var posDelta = curr.posCleanDelta;
            //var posDeltaClean = posDelta;
            //var animFactor = curr.posAvgLen * animSpeedInv;

            if (cfg.noiseDeltaPosAmpl != 0f)
            {
                var noiseVec = curr.noiseVelVec;
                var freqStep = curr.noiseFreqStep;
                var euler = GetPerlinVec3(noiseVec, cfg.noiseDeltaPosAmpl, freqStep);

                posDelta = Quaternion.Euler(euler) * posDelta;

                curr.noiseVelVec = new Vector3(noiseVec.x + freqStep, noiseVec.y + freqStep, noiseVec.z + freqStep);
                _dev.q1e = euler;
            }

            var velOffset = prev.velOffset + posDelta;
            var velOffsetSqLen = velOffset.sqrMagnitude;


            if (velOffsetSqLen > (OneThird * OneThird))
            {
                //var t = (mag - threshold) / (max - threshold);
                //t = t / (1f + t);
                //var newLen = Mathf.Lerp(threshold, max, t);

                // (1..∞)
                var t = Mathf.Sqrt(velOffsetSqLen) * (1f / OneThird);

                // (1..0)
                var newLenFactor = 2f / (1f + t);

                velOffset *= newLenFactor;
                posDeltaLen *= newLenFactor;
            }

            var prevVel = prev.vel;


            //// --- Bleed velocity ---

            //if (!isFreeze && curr.posBleed)
            //    prevVel *= 1f - (cfg.posBleedStr * dt);


            // The higher the velocity the lesser all the consequent accumulation.
            //var springVelCoef = 1f + (prev.velLen * cfg.devPosFastCoef);
            //springVelCoef = 1f / (springVelCoef * springVelCoef);
            //springVelCoef = Mathf.MoveTowards(prev.posSpringVelCoef, springVelCoef, dt);


            //var springPosCoef = cleanDeltaPosLen * prev.avgPosLenInv;
            //if (springPosCoef > 1f)
            //{
            //    var a = 1f + (springPosCoef - 1f); 
            //    springPosCoef = -(1f / (a * a)) + 1f;
            //}

            //var springFactor = 1f / (1f + prevVelLen * _dev.posExpSpringF);
            //var dampingFactor = Mathf.Min(10f, ExpApprox(posDeltaLen * _dev.posExpDampingF));

            //var springF = cleanDelta  * (cfg.posSpring * springFactor);
            //var dampingF = prevVel * (-cfg.posDamping * dt * dampingFactor);

            // Exp(posDelta(fps_independent) * coef(10) * Ln(2))
            //var posDeltaLenFactor = ExpApprox(posDeltaLen * dtInv * _dev.velExpFactor * 0.69314718056f);

            var t1 = posDeltaLen * dtInv * (1f + TwoThirds);

            // (0..2), visual – https://www.desmos.com/calculator/tmboeo8fdh
            var posDeltaLenFactor = 2f - (2f / (1f + t1));

            var velFactor = prev.velFactor;
            velFactor = Mathf.MoveTowards(velFactor, posDeltaLenFactor, dt * (1f + velFactor));

            var springF = velOffset * -((cfg.velSpringBase + (cfg.velSpringScl * velFactor)) * dt);

            if ((curr.posSubFx & SubFx.Impulse) != 0)
            {
                springF *= (1f + (curr.posTimeImpulse * cfg.posFxStrImpulse * curr.posAvgLen * (animSpeedInv * animSpeedInv)));
            }

            var dampingF = prevVel * ((cfg.velDampingBase + (cfg.velDampingScl * velFactor)) * dt);

            if (cfg.noiseVelAmpl != 0f)
            {
                var noiseVec = curr.noiseVelDampVec;
                var freqStep = curr.noiseFreqStep;
                var euler = GetPerlinVec3(noiseVec, cfg.noiseVelAmpl, freqStep);

                dampingF = Quaternion.Euler(euler) * dampingF;

                curr.noiseVelDampVec = new Vector3(noiseVec.x + freqStep, noiseVec.y + freqStep, noiseVec.z + freqStep);
                _dev.q2e = euler;
            }


            var accel = springF - dampingF;

            if (cfg.noisePosAmpl != 0f)
            {
                var noiseVec = curr.noisePosVec;
                var freqStep = curr.noiseFreqStep;

                accel += Vector3.Scale(cfg.noisePosFactor, GetPerlinVec3(
                    noiseVec,
                    cfg.noisePosAmpl * curr.noiseAmplFactor,
                    freqStep
                    ));

                curr.noisePosVec = new Vector3(noiseVec.x + freqStep, noiseVec.y + freqStep, noiseVec.z + freqStep);
            }

            //accel *= (cfg.massInv * dt);

            var vel = prevVel + accel;



            //  --- Velocity Reverse ---

            var velDot = Vector3.Dot(vel, prevVel);

            var dotIncrease = velDot > prev.velDot;

            var cfgSubFx = cfg.posFx;

            if (dotIncrease && cfgSubFx != 0 && !prev.velDotInc)
            {
                var animFactorSq = curr.posAvgLen * (animSpeedInv * animSpeedInv);

                var isFreeze   = (cfgSubFx & SubFx.Freeze)   != 0;
                var isSlowdown = (cfgSubFx & SubFx.Slowdown) != 0;
                var isImpulse  = (cfgSubFx & SubFx.Impulse)  != 0;

                var fxLen = cfg.posFxLen * animFactorSq;

                if (isFreeze)
                {
                    curr.posSubFx |= SubFx.Freeze;
                    curr.posTimeFreeze = (isSlowdown || isImpulse) ? fxLen * _dev.factor_1 : fxLen;
                }

                if (isSlowdown)
                {
                    curr.posSubFx |= SubFx.Slowdown;
                    curr.posTimeFreeze = (isFreeze || isImpulse) ? fxLen * _dev.factor_2 : fxLen;
                }

                if (isImpulse)
                {
                    curr.posSubFx |= SubFx.Impulse;
                    curr.posTimeFreeze = (isFreeze || isSlowdown) ? fxLen * _dev.factor_3 : fxLen;
                }
#if DEBUG
                if ((cfgSubFx & SubFx.Bleed) != 0)
                {
#endif
                    curr.posSubFx |= SubFx.Bleed;
                    curr.posTimeBleed = curr.posTimeImpulse + curr.posTimeSlowdown + curr.posTimeFreeze + (cfg.posFxBleedLen * animFactorSq);
#if DEBUG
                }
#endif


                if ((showDebug_2 & Effect.Pos) != 0)
                    AniMorphPlugin.Logger.LogInfo($"[{transform.name}] – GetPosOffset: " +
                        $"Velocity Reverse " +
                        $"Freeze[{isFreeze}] " +
                        $"Slowdown[{isSlowdown}] " +
                        $"Impulse[{isImpulse}] " +
                        $"Bleed[{(curr.posSubFx & SubFx.Bleed) != 0}] " +
                        $"");

            }

            var currSubFx = curr.posSubFx;
#if DEBUG
            if ((showDebug_2 & Effect.Pos) != 0)
            {
                if ((currSubFx & SubFx.Freeze) != 0)
                {
                    AniMorphPlugin.Logger.LogDebug($"[{transform.name}] – GetPosOffset: " +
                        $"FreezeFrame " +
                        $"time[{curr.posTimeFreeze:F3}] " +
                        $"bleed[{curr.posTimeBleed:F3}] " +
                        $"bleedFactor[{(1f / (1f + (curr.posTimeBleed * cfg.posFxStrBleed))):F3}] " +
                        $"");
                }
                else if ((currSubFx & SubFx.Slowdown) != 0)
                {
                    AniMorphPlugin.Logger.LogDebug($"[{transform.name}] – GetPosOffset: " +
                        $"SlowFrame " +
                        $"time[{curr.posTimeSlowdown:F3}] " +
                        $"factor[{(1f / (1f + (curr.posTimeSlowdown * cfg.posFxStrSlowdown))):F3}] " +
                        $"bleed[{curr.posTimeBleed:F3}] " +
                        $"bleedFactor[{(1f / (1f + (curr.posTimeBleed * cfg.posFxStrBleed))):F3}] " +
                        $"");
                }
                else if ((currSubFx & SubFx.Impulse) != 0)
                {
                    AniMorphPlugin.Logger.LogDebug($"[{transform.name}] – GetPosOffset: " +
                        $"ImpulseFrame " +
                        $"time[{curr.posTimeImpulse:F3}] " +
                        $"factor[{(1f + (curr.posTimeImpulse * cfg.posFxStrImpulse * curr.posAvgLen * animSpeedInv)):F3}] " +
                        $"bleed[{curr.posTimeBleed:F3}] " +
                        $"bleedFactor[{(1f / (1f + (curr.posTimeBleed * cfg.posFxStrBleed))):F3}] " +
                        $"");
                }
                else if ((currSubFx & SubFx.Bleed) != 0)
                {
                    AniMorphPlugin.Logger.LogDebug($"[{transform.name}] – GetPosOffset: " +
                        $"BleedFrame " +
                        $"bleed[{curr.posTimeBleed:F3}] " +
                        $"bleedFactor[{(1f / (1f + (curr.posTimeBleed * cfg.posFxStrBleed))):F3}] " +
                        $"");
                }
            }
             
#endif


            // --- Bleed velocity ---

            if ((currSubFx & SubFx.Bleed) != 0)
                vel *= 1f / (1f + (curr.posTimeBleed * cfg.posFxStrBleed));

            // TODO. Move Impulse into spring calculations.

            var result =
                (currSubFx & SubFx.Freeze) != 0   ? velOffset :
                (currSubFx & SubFx.Slowdown) != 0 ? velOffset + vel * (dt * (1f + velFactor) * (1f / (1f + (curr.posTimeSlowdown * cfg.posFxStrSlowdown)))) :
                                                    velOffset + vel * (dt * (1f + velFactor));


            //var result = currSubFx switch
            //{
            //    var fx when (fx & SubFx.Freeze) != 0 => velOffset,
            //    var fx when (fx & SubFx.Slowdown) != 0 => ,
            //    var fx when (fx & SubFx.Impulse) != 0 => velOffset + vel * (dt * (1f + velFactor) * (1f + (curr.posTimeImpulse * cfg.posFxStrImpulse * curr.posAvgLen * animSpeedInv))),
            //    _ => 
            //};

                //curr.posFreeze ? velOffset : 
                //curr.posSlowdown ?
                //// (dt / (dt + time * str),
                //// 'time' can't be negative and therefore the output caps at 1.
                //// (~0..1), visual – https://www.desmos.com/calculator/zlrglfe79g
                //velOffset + vel * (dt * (1f + velFactor) * (1f / (1f + (curr.posTimeSlowdown * cfg.posStrSlowdown)))) :
                //velOffset + vel * (dt * (1f + velFactor));



            prev.vel = vel;
            prev.velOffset = result;
            prev.velFactor = velFactor;
            prev.velDot = velDot;
            prev.velDotInc = currSubFx != 0 || dotIncrease;
#if DEBUG
            if ((showDebug_1 & Effect.Pos) != 0)
                AniMorphPlugin.Logger.LogDebug($"[{transform.name}] – GetPosOffset: " +
                    $"velOffset({velOffset.x:F3},{velOffset.y:F3},{velOffset.z:F3}) " +
                    $"result({result.x:F3},{result.y:F3},{result.z:F3}) " +
                    $"vel({vel.x:F3},{vel.y:F3},{vel.z:F3}) " +
                    $"springF({springF.x:F3},{springF.y:F3},{springF.z:F3}), " +
                    $"dampingF({dampingF.x:F3},{dampingF.y:F3},{dampingF.z:F3}) " +
                    $"velFactor[{(velFactor):F3}] " +
                    $"posDeltaLen[{posDeltaLen:F3}] " +
                    $"velDot[{velDot:F5}] " +
                    $"Freeze[{(currSubFx & SubFx.Freeze) != 0}] " +
                    $"Slow[{(currSubFx & SubFx.Slowdown) != 0}] " +
                    $"Impulse[{(currSubFx & SubFx.Impulse) != 0}] " +
                    $"Bleed[{(currSubFx & SubFx.Bleed) != 0}] " +
                    $"");
#endif
            return result;
        }


        #endregion


        #region Rotation


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

            if (curr.isAnimRot != state)
            {
                curr.isAnimRot = state;
                UpdateRotCollectBaseline(state);
            }
        }

        protected virtual Vector3 GetRotOffset(ref Config cfg, ref Current curr, ref Previous prev, float dt, float dtInv, float animSpeed, float animSpeedInv)
        {
            // After god knows how many iterations,
            // the current sophisticated and hard to understand one works like a charm,
            // with all the elegance and responsiveness I want from it,
            // tried to return back to the most basic, dumbed down one but it glitches and ugly,
            // therefore !!!We Stick With This One!!!, even though modifying it is a huge pain.

            var currCleanRot = transform.rotation;

            var currLocalRot = transform.localRotation;
            var prevLocalRot = prev.localRot;

            // We want to know if anybody has done anything to this quaternion,
            // not just ~approx orientation comparison.
            var isStatic = currLocalRot.x == prevLocalRot.x
                && currLocalRot.y == prevLocalRot.y
                && currLocalRot.z == prevLocalRot.z
                && currLocalRot.w == prevLocalRot.w;

            if (isStatic)
            {
                if (curr.isAnimRot && ConsecutiveFrameCounter++ > (int)((2f / 60f) * dtInv))
                    UpdateAnimRot(false);

                var inverseOffset = Quaternion.Inverse(Quaternion.Euler(prev.rotOffset));

                currCleanRot = currCleanRot * inverseOffset;

                // It should be the commented way, but seems like uncommented is what actually works.
                //curr.cleanLocalRot = inverseOffset * currLocalRot;
                curr.rotCleanLocal = currLocalRot * inverseOffset;
            }
            else
            {
                if (!curr.isAnimRot && ConsecutiveFrameCounter++ > (int)((2f / 60f) * dtInv))
                    UpdateAnimRot(true);

                curr.rotCleanLocal = currLocalRot;
            }

            var currCleanRotInv = Quaternion.Inverse(currCleanRot);
            curr.rotCleanInv = currCleanRotInv;

            var absAngle = 0f;


            // --- Freeze ---

            if (curr.rotFreeze)
            {
                prev.torque += prev.torque * (-cfg.rotDampingBase * dt);

                return (currCleanRotInv * prev.adjustedRot).eulerAngles;
            }


            // --- Normal Rotation ---

            if (cfg.rotAxes == Axis.None)
            {
                // Global delta between clean current rotation and rotation we set in the previous frame.
                var delta = currCleanRot * Quaternion.Inverse(prev.adjustedRot);

                var absDeltaW = Mathf.Abs(delta.w);

                // Angle is bigger then allowed.
                if (absDeltaW < cfg.rotMaxCos)
                {
                    var oldDelta = delta;

                    var angle = Mathf.Acos(absDeltaW) * 2f;

                    var t = angle * cfg.rotMaxRadInv;
                    // (1..0)
                    t = 2f / (1f + t);
#if KK
                    delta = LBUtilsMath.Normalize(
#else
                    delta = Quaternion.Normalize(
#endif
                    new Quaternion(
                            delta.x * t,
                            delta.y * t,
                            delta.z * t,
                            1f + (delta.w - 1f) * t
                            )
                        );

#if DEBUG
                    if ((showDebug_2 & Effect.Rot) != 0)
                    {
                        AniMorphPlugin.Logger.LogDebug($"[{transform.name}] – GetRotOffset – Limit Angle: " +
                        $"angle({angle * Mathf.Rad2Deg:F2}) " +
                        $"t({t:F2}) " +
                        $"delta({delta.eulerAngles}) " +
                        $"oldDelta({oldDelta.eulerAngles}) " +
                        $"");
                    }
#endif
                }

                var torque = GetRotTorque(ref cfg, ref curr, ref prev, dt, animSpeedInv, delta, out absAngle);

                //var rot = angVelocityLen == 0f ?
                //    prev.adjustedRot :
                //    Quaternion.AngleAxis(angVelocityLen * dt * cfg.rotRate, angVel * (1f / angVelocityLen)) * prev.adjustedRot;
                var adjustedRot = Quaternion.Euler(torque * dt * cfg.rotRate) * prev.adjustedRot;


                prev.adjustedRot = adjustedRot;
                prev.torque = torque;

                var result = currCleanRotInv * adjustedRot;
                var resultEuler = result.eulerAngles;

#if DEBUG
                if ((showDebug_1 & Effect.Rot) != 0)
                {
                    var devAdjRotEuler = adjustedRot.eulerAngles;
                    devAdjRotEuler = new Vector3(
                        devAdjRotEuler.x > 180f ? devAdjRotEuler.x - 360f : devAdjRotEuler.x,
                        devAdjRotEuler.y > 180f ? devAdjRotEuler.y - 360f : devAdjRotEuler.y,
                        devAdjRotEuler.z > 180f ? devAdjRotEuler.z - 360f : devAdjRotEuler.z
                        );

                    var devTorque = torque * (dt * cfg.rotRate);
                    devTorque = new Vector3(
                        devTorque.x > 180f ? devTorque.x - 360f : devTorque.x,
                        devTorque.y > 180f ? devTorque.y - 360f : devTorque.y,
                        devTorque.z > 180f ? devTorque.z - 360f : devTorque.z
                        );

                    var devCleanRotEuler = currCleanRot.eulerAngles;
                    devCleanRotEuler = new Vector3(
                        devCleanRotEuler.x > 180f ? devCleanRotEuler.x - 360f : devCleanRotEuler.x,
                        devCleanRotEuler.y > 180f ? devCleanRotEuler.y - 360f : devCleanRotEuler.y,
                        devCleanRotEuler.z > 180f ? devCleanRotEuler.z - 360f : devCleanRotEuler.z
                        );

                    var devResultEuler = new Vector3(
                        resultEuler.x > 180f ? resultEuler.x - 360f : resultEuler.x,
                        resultEuler.y > 180f ? resultEuler.y - 360f : resultEuler.y,
                        resultEuler.z > 180f ? resultEuler.z - 360f : resultEuler.z
                        );

                    AniMorphPlugin.Logger.LogDebug($"[{transform.name}] – GetRotOffset: " +
                        $"torqueF({devTorque.x:F2}, {devTorque.y:F2}, {devTorque.z:F2}) " +
                        $"adjustedRot({devAdjRotEuler.x:F2}, {devAdjRotEuler.y:F2}, {devAdjRotEuler.z:F2}) " +
                        $"cleanRot({devCleanRotEuler.x:F2}, {devCleanRotEuler.y:F2}, {devCleanRotEuler.z:F2}) " +
                        $"result({devResultEuler.x:F2}, {devResultEuler.y:F2}, {devResultEuler.z:F2}) " +
                        $"absAngle[{absAngle:F3}] " +
                        $"");
                }
#endif

                return resultEuler;
            }
            else
            {
                // Global delta between clean current rotation and rotation we setup in the previous frame.
                var delta = currCleanRot * Quaternion.Inverse(prev.adjustedRot);

                foreach (var axis in axisValue)
                {
                    if ((cfg.rotAxes & axis) != 0)
                    {
                        delta = GetAxialTorque(ref cfg, ref curr, ref prev, delta, axis, dt, animSpeedInv);
                    }
                }

                var adjustedRot = delta * prev.adjustedRot;

                var result = currCleanRotInv * adjustedRot;

                var resultEuler = result.eulerAngles;
#if DEBUG
                if ((showDebug_1 & Effect.Rot) != 0)
                {
                    var devAdjRotEuler = adjustedRot.eulerAngles;
                    devAdjRotEuler = new Vector3(
                        devAdjRotEuler.x > 180f ? devAdjRotEuler.x - 360f : devAdjRotEuler.x,
                        devAdjRotEuler.y > 180f ? devAdjRotEuler.y - 360f : devAdjRotEuler.y,
                        devAdjRotEuler.z > 180f ? devAdjRotEuler.z - 360f : devAdjRotEuler.z
                        );

                    var devCleanRotEuler = currCleanRot.eulerAngles;
                    devCleanRotEuler = new Vector3(
                        devCleanRotEuler.x > 180f ? devCleanRotEuler.x - 360f : devCleanRotEuler.x,
                        devCleanRotEuler.y > 180f ? devCleanRotEuler.y - 360f : devCleanRotEuler.y,
                        devCleanRotEuler.z > 180f ? devCleanRotEuler.z - 360f : devCleanRotEuler.z
                        );

                    var devResultEuler = new Vector3(
                        resultEuler.x > 180f ? resultEuler.x - 360f : resultEuler.x,
                        resultEuler.y > 180f ? resultEuler.y - 360f : resultEuler.y,
                        resultEuler.z > 180f ? resultEuler.z - 360f : resultEuler.z
                        );

                    var prevAdjRotEuler = prev.adjustedRot.eulerAngles;
                    prevAdjRotEuler = new Vector3(
                        prevAdjRotEuler.x > 180f ? prevAdjRotEuler.x - 360f : prevAdjRotEuler.x,
                        prevAdjRotEuler.y > 180f ? prevAdjRotEuler.y - 360f : prevAdjRotEuler.y,
                        prevAdjRotEuler.z > 180f ? prevAdjRotEuler.z - 360f : prevAdjRotEuler.z
                        );

                    var deltaEuler = delta.eulerAngles;
                    deltaEuler = new Vector3(
                        deltaEuler.x > 180f ? deltaEuler.x - 360f : deltaEuler.x,
                        deltaEuler.y > 180f ? deltaEuler.y - 360f : deltaEuler.y,
                        deltaEuler.z > 180f ? deltaEuler.z - 360f : deltaEuler.z
                        );

                    AniMorphPlugin.Logger.LogDebug($"[{transform.name}] – GetRotOffset: " +
                        $"adjustedRot({devAdjRotEuler.x:F2}, {devAdjRotEuler.y:F2}, {devAdjRotEuler.z:F2}) " +
                        $"cleanRot({devCleanRotEuler.x:F2}, {devCleanRotEuler.y:F2}, {devCleanRotEuler.z:F2}) " +
                        $"deltaEuler({deltaEuler.x:F2}, {deltaEuler.y:F2}, {deltaEuler.z:F2}) " +
                        $"delta({delta.x:F2}, {delta.y:F2}, {delta.z:F2}, {delta.w:F2}) " +
                        $"prevAdjRot({prevAdjRotEuler.x:F2}, {prevAdjRotEuler.y:F2}, {prevAdjRotEuler.z:F2}) " +
                        $"");
                }
#endif
                prev.adjustedRot = adjustedRot;

                return resultEuler;
            }
        }

        private Vector3 GetRotTorque(ref Config cfg, ref Current curr, ref Previous prev, float dt, float animSpeedInv, Quaternion delta, out float absAngle)
        {
            delta.ToAngleAxis(out var angle, out var axis);

            if (angle > 180f)
                angle -= 360f;

            absAngle = Mathf.Abs(angle);

            var accel = Vector3.zero;

            var torque = prev.torque;

            if (curr.rotBleed)
                torque *= 1f - (cfg.rotBleedStr * dt);

            var angleFactor = absAngle * cfg.rotAngleFactor * dt;


            // --- Filter corruption & Apply acceleration ---

            if (!float.IsInfinity(axis.x))
            {
                var deltaAngVel = axis * (angle * ExpApprox(angleFactor));

                //var slowLerp = 1f - FastExp(-cfg.rotSlowSmooth * dt);
                //var fastLerp = 1f - FastExp(-cfg.rotFastSmooth * dt);

                var slowLerp = cfg.rotSlowSmooth * dt;
                var fastLerp = cfg.rotFastSmooth * dt;

                prev.rotSlowDelta = Vector3.Lerp(prev.rotSlowDelta, deltaAngVel, slowLerp);
                prev.rotFastDelta = Vector3.Lerp(prev.rotFastDelta, deltaAngVel, fastLerp);

                //var highFreqDelta = prev.fastRotDelta - prev.slowRotDelta;

                var targetVel = Vector3.Lerp(
                    prev.rotSlowDelta,
                    prev.rotFastDelta,
                    cfg.rotHighFreqInf
                );

                accel = cfg.rotSpringBase * targetVel;
            }

            if (cfg.noiseRotAmpl != 0f)
            {
                var freqStep = curr.noiseFreqStep;
                var noiseVec = curr.noiseRotVec;

                accel += Vector3.Scale(cfg.noiseRotFactor, GetPerlinVec3(
                    noiseVec,
                    cfg.noiseRotAmpl * curr.noiseAmplFactor, 
                    freqStep
                    ));

                curr.noiseRotVec = new Vector3(noiseVec.x + freqStep, noiseVec.y + freqStep, noiseVec.z + freqStep);
            }

            angleFactor += 1;
            var dampingF = torque * (-cfg.rotDampingBase * dt * (1f / (angleFactor * angleFactor)));

            torque += accel + dampingF;

            var torqueDot = Vector3.Dot(torque, prev.torque);


            if (curr.rotHighTorque)
            {
                if (torqueDot < 1f)
                {
                    curr.rotHighTorque = false;
                    var shockFactor = absAngle * (1f / 45f);
                    curr.rotFreezeTime = cfg.rotFreezeTime * animSpeedInv * shockFactor;
                    curr.rotFreeze = (cfg.rotSubEffects & SubFx.Freeze) != 0;
                    curr.rotBleed = (cfg.rotSubEffects & SubFx.Bleed) != 0;
#if DEBUG
                    //if ((showDebug_1 & Effect.Rot) != 0)
                    AniMorphPlugin.Logger.LogInfo($"[{transform.name}] – GetRotOffset: " +
                            $"Shock! " +
                            $"shockTime[{curr.rotFreezeTime:F3} absAngle[{absAngle:F3}]");

                    if ((advDebug_1 & Effect.Rot) != 0)
                        Time.timeScale = 0f;
#endif
                }
            }
            else if (torqueDot > 10000f)
            {
                curr.rotHighTorque = true;
            }

            if ((showDebug_3 & Effect.Rot) != 0)
                AniMorphPlugin.Logger.LogDebug($"[{transform.name}] – GetRotTorque: " +
                    $"torqueDot[{torqueDot:F0}] " +
                    $"");

            return torque;
        }

        private Quaternion GetAxialTorque(ref Config cfg, ref Current curr, ref Previous prev, Quaternion delta, Axis axis, float dt, float animSpeedInv) //, out float absAngle)
        {
            var idx = (int)axis >> 1;
            var prevTorque = prev.torqueAxial[idx];

            if (curr.rotBleed)
                prevTorque *= 1f - (cfg.rotBleedStr * dt);

            var twistAxis = axis switch
            {
                Axis.X => transform.right,
                Axis.Y => transform.up,
                Axis.Z => transform.forward,
                _ => throw new NotImplementedException(nameof(axis))
            };

            var twist = ExtractTwist(delta, twistAxis);
            var swing = delta * Quaternion.Inverse(twist);


//            // --- Shock ---

//            if (curr.axisShockTime[idx] > 0f)
//            {
//                curr.axisShockTime[idx] -= dt;

//                return
//                    // Normalization is necessary as quaternions often are very tiny,
//                    // and multiplying them consecutively will result in precision errors over time,
//                    // which quickly leads to the corruption.
//#if KK
//                    LBUtilsMath.Normalize(swing * Quaternion.Euler(prevTorque * (dt * cfg.rotRate)));
//#else
//                    Quaternion.Normalize(swing * Quaternion.Euler(prevTorque * (dt * cfg.rotRate)));
//#endif
//            }



            // --- Normal ---

            twist.ToAngleAxis(out var angle, out twistAxis);

            if (angle > 180f)
                angle -= 360f;

            var absAngle = Mathf.Abs(angle);

            var angleFactor = absAngle * cfg.rotAngleFactor * dt;

            var accel = Vector3.zero;

            if (!float.IsInfinity(twistAxis.x))
            {
                // The bigger the angle, the bigger the contribution to the torque.
                var angVel = twistAxis * (angle * ExpApprox(angleFactor));

                var slowDelta = Vector3.Lerp(prev.rotAxialSlowDelta[idx], angVel, cfg.rotSlowSmooth * dt);
                var fastDelta = Vector3.Lerp(prev.rotAxialFastDelta[idx], angVel, cfg.rotFastSmooth * dt);

                var targetVel = Vector3.Lerp(slowDelta, fastDelta, cfg.rotHighFreqInf);

                accel = cfg.rotSpringBase * targetVel;

                prev.rotAxialSlowDelta[idx] = slowDelta;
                prev.rotAxialFastDelta[idx] = fastDelta;
            }

            if (cfg.noiseRotAmpl != 0f)
            {
                var freqStep = curr.noiseFreqStep;

                accel += twistAxis * GetPerlinNum(
                    curr.noiseRotVec[idx],
                    cfg.noiseRotAmpl * curr.noiseAmplFactor,
                    freqStep
                    );

                curr.noiseRotVec[idx] += freqStep;
            }

            angleFactor += 1f;
            var dampingF = prevTorque * (-cfg.rotDampingBase * dt * (1f / (angleFactor * angleFactor)));

            var torque = prevTorque + (accel + dampingF);

            prev.torqueAxial[idx] = torque;

            var torqueDot = Vector3.Dot(torque, prevTorque);

            if ((curr.rotAxisHighTorque & axis) != 0)
            {
                if (torqueDot < 1f)
                {
                    curr.rotAxisHighTorque &= ~axis;
                    var shockAngF = absAngle * (1f / 45f);
                    curr.rotAxisFreezeTime[idx] = cfg.rotFreezeTime * animSpeedInv * shockAngF;
                    curr.rotFreeze = (cfg.rotSubEffects & SubFx.Freeze) != 0;
                    curr.rotBleed = (cfg.rotSubEffects & SubFx.Bleed) != 0;
#if DEBUG
                    if ((showDebug_1 & Effect.Rot) != 0)
                        AniMorphPlugin.Logger.LogInfo($"[{transform.name}] – GetRotOffset: " +
                            $"RotFreeze! " +
                            $"freezeTime[{curr.rotFreezeTime:F3} absAngle[{absAngle:F3}]");

                    if ((advDebug_1 & Effect.Rot) != 0)
                        Time.timeScale = 0f;
#endif
                }
            }
            else if (torqueDot > 10000f)
            {
                curr.rotAxisHighTorque |= axis;
            }

            var result =
                // Normalization is necessary as quaternions quite often very tiny,
                // and multiplying them consecutively will result in precision errors over time,
                // which quickly leads to the corruption.
#if KK
                LBUtilsMath.Normalize(swing * Quaternion.Euler(torque * (dt * cfg.rotRate)));
#else
                Quaternion.Normalize(swing * Quaternion.Euler(torque * (dt * cfg.rotRate)));
#endif

#if DEBUG
            if ((showDebug_2 & Effect.Rot) != 0)
            {
                var twistEuler = twist.eulerAngles;
                var swingEuler = swing.eulerAngles;
                var deltaEuler = delta.eulerAngles;
                var resultEuler = result.eulerAngles;

                AniMorphPlugin.Logger.LogDebug($"[{transform.name}] – GetAxialTorque: " +
                    $"twistEuler({twistEuler.x:F2}, {twistEuler.y:F2}, {twistEuler.z:F2}) " +
                    $"twist({twist.x:F2}, {twist.y:F2}, {twist.z:F2}, {twist.w:F2}) " +
                    $"swingEuler({swingEuler.x:F2}, {swingEuler.y:F2}, {swingEuler.z:F2}) " +
                    $"swing({swing.x:F2}, {swing.y:F2}, {swing.z:F2}, {swing.w:F2}) " +
                    $"deltaEuler({deltaEuler.x:F2}, {deltaEuler.y:F2}, {deltaEuler.z:F2}) " +
                    $"torque({torque.x:F2}, {torque.y:F2}, {torque.z:F2}) " +
                    $"twistAxis({twistAxis.x:F2}, {twistAxis.y:F2}, {twistAxis.z:F2}) " +
                    $"resultEuler({resultEuler.x:F2}, {resultEuler.y:F2}, {resultEuler.z:F2}) " +
                    $"result({result.x:F2}, {result.y:F2}, {result.z:F2}, {result.w:F2}) " +
                    $"angle[{angle:F2}] " +
                    $"");
            }
#endif
            return result;
        }

        private void SwingTwistDecomposition(Quaternion q, Vector3 axis, out Quaternion swing, out Quaternion twist)
        {
            twist = ExtractTwist(q, axis);

            swing = q * Quaternion.Inverse(twist);
        }

        private Quaternion ExtractTwist(Quaternion q, Vector3 axis)
        {
            var r = new Vector3(q.x, q.y, q.z);

            var proj = Vector3.Project(r, axis);

            var twist = new Quaternion(
                proj.x,
                proj.y,
                proj.z,
                q.w
            );
#if KK
            return LBUtilsMath.Normalize(twist);
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

            //if (curr.posSubFxTime > 0f)
            //{
            //    var shockScl = Vector3.Lerp(prev.scl, prev.sclTarget, dt * cfg.sclRate * _dev.factor_4);

            //    prev.scl = shockScl;

            //    return shockScl;
            //}


            //if (curr.posSubBleedTime > 0f)
            //    driver *= 1f - (cfg.sclBleedStr * dt);


            var springCoef = 1f;

            var springF = springCoef * cfg.sclSpring * curr.posCleanDelta;
            var dampingF = -cfg.sclDamping * dt * driver;

            driver += springF + dampingF;
#if DEBUG
            if ((showDebug_1 & Effect.Scl) != 0)
                AniMorphPlugin.Logger.LogDebug($"[{transform.name}] " +
                    $"driver({driver.x:F3},{driver.y:F3},{driver.z:F3}) " +
                    $"springF({springF.x:F3},{springF.y:F3},{springF.z:F3}), " +
                    $"dampingF({dampingF.x:F3},{dampingF.y:F3},{dampingF.z:F3}) " +
                    $"");
#endif

            if (cfg.noiseSclAmpl != 0f)
            {
                var noiseVec = curr.noiseSclVec;
                var freqStep = curr.noiseFreqStep;

                driver += Vector3.Scale(cfg.noiseSclFactor, GetPerlinVec3(
                    noiseVec,
                    cfg.noiseSclAmpl * curr.noiseAmplFactor,
                    freqStep
                    ));

                curr.noiseSclVec = new Vector3(noiseVec.x + freqStep, noiseVec.y + freqStep, noiseVec.z + freqStep);
            }

            var driverLen = driver.magnitude;

            if (driverLen == 0f)
            {
                var toNormalScl = Vector3.Lerp(prev.scl, vecOne, dt * cfg.sclRate * _dev.factor_4);

                prev.scl = toNormalScl;

                return toNormalScl;
            }

            var driverN = driver / driverLen;

            var factor = SmoothStepN(driverLen * _dev.factor_5);
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
            AniMorphPlugin.Logger.LogInfo($"[{transform.name}] [UpdateDynamicRot] isDynamic[{dynamic}]");
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
            ref var curr = ref current;
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
            var isRot = (cfg.effects & Effect.Rot) != 0;
            var isScl = (cfg.effects & Effect.Scl) != 0;
            var isTether = (cfg.effects & Effect.Tether) != 0;
            var isPosOffset = (cfg.effects & Effect.PosOffset) != 0;
            var isRotOffset = (cfg.effects & Effect.RotOffset) != 0;
            var isSclOffset = (cfg.effects & Effect.SclOffset) != 0;
            var isNoise = (pluginConfig.NoiseType != null);

            cfg.velSpringBase  = pluginConfig.PosSpring.Value  * baseCfg.velSpring * 100f;
            cfg.velDampingBase = pluginConfig.PosDamping.Value * baseCfg.velDamping * 10f;
            cfg.velSpringScl   = cfg.velSpringBase  * baseCfg.velSpringScl;
            cfg.velDampingScl  = cfg.velDampingBase * baseCfg.velDampingScl;
            cfg.velRate        = baseCfg.velRate;
            cfg.posFx = pluginConfig.PosSubFx.Value;
            cfg.posFxLen = pluginConfig.PosSubFxLen.Value;
            cfg.posFxStrImpulse = pluginConfig.PosSubFxStr.Value;
            cfg.posFxStrSlowdown = pluginConfig.PosSubFxStr.Value;
            cfg.posFxStrBleed = pluginConfig.PosSubFxStr.Value;

            // TODO. Remove this field after solid coefficient is established. 
            cfg.posFxBleedLen = cfg.posFxLen * TwoThirds;


            if (isRot)
            {
                cfg.rotSpringBase = baseCfg.rotSpringCfg * pluginConfig.RotSpring.Value;
                cfg.rotDampingBase = baseCfg.rotDampingCfg * pluginConfig.RotDamping.Value;
                cfg.rotRate = baseCfg.rotRateCfg * pluginConfig.RotRate.Value;
                //cfg.rotFreezeThreshold = 1000f * cfg.rotSpring * cfg.rotRate;
                // TODO
                cfg.rotBleedStr = cfg.rotDampingBase * 1f;
                cfg.rotMaxRadInv = 1f / (pluginConfig.RotMaxDeg.Value * Mathf.Deg2Rad);
                cfg.rotMaxCos =  Mathf.Cos(pluginConfig.RotMaxDeg.Value * 0.5f * Mathf.Deg2Rad);

                // --- Find Active Axes ---

                cfg.rotAxes = Axis.None;

                var rotAxes = baseCfg.rotApplication;
                if (rotAxes != vecOne)
                {
                    var activeAxes = 0;

                    for (var i = 0; i < 3; i++)
                    {
                        // Deactivated axis
                        if (rotAxes[i] == 0f) continue;

                        activeAxes++;
                        cfg.rotAxes |= (Axis)(1 << i);
                    }

                    // Can't happen, but just in case.
                    if (activeAxes == 0)
                        cfg.effects &= ~Effect.Rot;
                    else
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


            if (isNoise)
            {
                var noisePos = pluginConfig.NoiseAmplitudePos != null ? baseCfg.noisePosCfg * pluginConfig.NoiseAmplitudePos.Value : 0f;
                var noiseType = pluginConfig.NoiseType.Value;

                cfg.noisePosAmpl = (noiseType & NoiseType.Pos) != 0 ? noisePos * 0.1f : 0f;
                cfg.noiseRotAmpl = pluginConfig.NoiseAmplitudeRot != null ? baseCfg.noiseRotCfg * (pluginConfig.NoiseAmplitudeRot.Value * 1000f) : 0f;
                cfg.noiseSclAmpl = pluginConfig.NoiseAmplitudeScl != null ? baseCfg.noiseSclCfg * (pluginConfig.NoiseAmplitudeScl.Value * 10f) : 0f;

                cfg.noiseDeltaPosAmpl = (noiseType & NoiseType.DeltaPos) != 0 ? (noisePos * 100f) : 0f;
                cfg.noiseVelAmpl = (noiseType & NoiseType.Velocity) != 0 ? (noisePos * 100f) : 0f;

                if ((noiseType & NoiseType.DeltaPos) != 0 && 
                    (noiseType & NoiseType.Velocity) != 0)
                {
                    cfg.noiseDeltaPosAmpl *= TwoThirds;
                    cfg.noiseVelAmpl *= TwoThirds;
                }

                cfg.noisePosFactor = baseCfg.noisePosF;
                cfg.noiseRotFactor = baseCfg.noiseRotF;
                cfg.noiseSclFactor = baseCfg.noiseSclF;


                curr.noisePosVec = new(Random.Range(0f, 1000f), Random.Range(0f, 1000f), Random.Range(0f, 1000f));
                curr.noiseRotVec = new(Random.Range(0f, 1000f), Random.Range(0f, 1000f), Random.Range(0f, 1000f));
                curr.noiseSclVec = new(Random.Range(0f, 1000f), Random.Range(0f, 1000f), Random.Range(0f, 1000f));

                curr.noiseVelVec = new(Random.Range(0f, 1000f), Random.Range(0f, 1000f));
                curr.noiseTorqueVec = new(Random.Range(0f, 1000f), Random.Range(0f, 1000f));

                // Because default the length of looping H clips in Koik is ~1.33.
                cfg.noiseFreq = (float)Math.Round((1f + OneThird) * Random.Range(0.85f, 1.15f), 2);
            }

            cfg.devPosFastCoef = AniMorphPlugin.DevPosFastCoef.Value;

            cfg.rotAngleFactor = baseCfg.rotAngleCfg;

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


        private Vector3 GetPerlinVec3(Vector3 noiseVec, float ampl, float freqStep)
        {
            var x = 0f;
            var y = 0f;
            var z = 0f;

            for (var i = 0; i < 4; i++)
            {
                x += (Mathf.PerlinNoise(noiseVec.x + freqStep, 0f) - 0.5f) * ampl;

                y += (Mathf.PerlinNoise(0f, noiseVec.y + freqStep) - 0.5f) * ampl;

                var zArg = noiseVec.z + freqStep;
                z += (Mathf.PerlinNoise(zArg, zArg) - 0.5f) * ampl;

                ampl *= 0.5f;
                freqStep *= 2f;
            }

            return new Vector3(x, y, z);
        }

        private Vector3 GetPerlinVec2(Vector2 noiseVec, float ampl, float freqStep)
        {
            var x = 0f;
            var y = 0f;

            for (var i = 0; i < 4; i++)
            {
                x += (Mathf.PerlinNoise(noiseVec.x + freqStep, 0f) - 0.5f) * ampl;

                y += (Mathf.PerlinNoise(0f, noiseVec.y + freqStep) - 0.5f) * ampl;

                ampl *= 0.5f;
                freqStep *= 2f;
            }

            return new Vector3(x, y, 0f);
        }

        private float GetPerlinNum(float noiseNum, float ampl, float freqStep)
        {
            var num = 0f;

            for (var i = 0; i < 4; i++)
            {
                var zArg = noiseNum + freqStep;
                num += (Mathf.PerlinNoise(zArg, zArg) - 0.5f) * ampl;

                ampl *= 0.5f;
                freqStep *= 2f;
            }

            return num;
        }

        private float ExpApprox(float t)
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

            internal float noisePosAmpl;
            internal float noiseRotAmpl;
            internal float noiseSclAmpl;
            internal float noiseDeltaPosAmpl;
            internal float noiseDeltaRotAMpl;
            internal float noiseVelAmpl;
            internal float noiseTorqueAmpl;

            internal Vector3 noisePosFactor;
            internal Vector3 noiseRotFactor;
            internal Vector3 noiseSclFactor;

            internal float velSpringBase;
            internal float velSpringScl;
            internal float velDampingBase;
            internal float velDampingScl;
            internal float velRate;

            internal float posFxStrImpulse;
            internal float posFxStrSlowdown;
            internal float posFxStrBleed;
            internal float posFxLen;
            internal float posFxBleedLen;
            internal Vector3 posLimitPositive;
            internal Vector3 posLimitNegative;
            //internal float posDamping = 2f * Mathf.Sqrt(posSpring * mass);
            internal SubFx posFx;


            internal float mass = 1f;
            internal float massInv = 1f;

            internal float rotSpringBase;
            internal float rotDampingBase;
            internal float rotRate;
            internal float rotFreezeTime = 0.1f;
            internal Axis  rotAxes;
            internal float rotActiveAxesInv;
            internal float rotAngleFactor;
            internal float rotBleedStr;
            internal float rotMaxCos;
            internal float rotMaxRadInv;
            //internal float rotFreezeThreshold;
            internal SubFx rotSubEffects;

            internal float sclSpring;
            internal float sclDamping;
            internal float sclRate;
            internal float sclDistort;
            internal float sclBleedStr = 5f;
            internal SubFx sclSubEffects;

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

            internal float velSpringAnimFactor;
            internal float velDampingAnimFactor;

        }

        protected class Current
        {

            internal float posTimeImpulse;
            internal float posTimeSlowdown;
            internal float posTimeFreeze;
            internal float posTimeBleed;

            internal SubFx posSubFx;


            internal float posAvgLen;
            internal float posAvgLenInv;


            internal bool isAnimRot;

            internal Quaternion rotCleanLocal;
            internal Quaternion rotCleanInv;

            internal bool rotFreeze;
            internal bool rotBleed;

            internal bool rotHighTorque;
            internal float rotFreezeTime;
            internal float rotBleedTime;

            internal Axis rotAxisHighTorque;
            internal float[] rotAxisFreezeTime = new float[3];
            internal float[] rotAxisBleedTime = new float[3];

            /// <summary> (0..3) </summary>
            internal float noiseAmplFactor;
            internal float noiseFreqStep;

            internal Vector3 noisePosVec;
            internal Vector3 noiseRotVec;
            internal Vector3 noiseSclVec;

            internal Vector3 noiseVelVec;
            internal Vector3 noiseTorqueVec;
            internal Vector3 noiseVelDampVec;

            // Delta between current and previous positions without our modifications.
            internal Vector3 posCleanDelta;
            internal float posCleanDeltaLen;
            internal float posCleanDeltaTotalLen;

            // Delta between current and previous 'posCleanDelta'.
            internal Vector3 velCleanDelta;


            internal void Clear()
            {
                rotHighTorque = false;

                posTimeImpulse = 0f;
                posTimeSlowdown = 0f;
                posTimeFreeze = 0f;
                posTimeBleed = 0f;

                posCleanDeltaTotalLen = 0f;

                posCleanDelta = Vector3.zero;
                posCleanDeltaLen = 0f;

                velCleanDelta = Vector3.zero;
            }
            internal void OnAnimationLoopStart(float animLoopFrameCountInv, float dt)
            {
                posAvgLen = Mathf.MoveTowards(posAvgLen, posCleanDeltaTotalLen * animLoopFrameCountInv, dt * 10f);

                if (posAvgLen != 0f)
                    posAvgLenInv = 1f / posAvgLen;

                posCleanDeltaTotalLen = 0f;
            }
        }
        
        // TODO class -> struct
        protected /*struct*/ class Previous
        {
            internal Vector3 vel;
            //internal float velLen;
            internal Vector3 velOffset;
            internal float velFactor = 1f;
            internal float velDampingFactor;
            internal float velDot;
            internal bool velDotInc;

            internal Vector3 torque;
            internal Vector3[] torqueAxial = new Vector3[3];


            // --- Scale ---

            internal Vector3 scl;
            internal Vector3 sclDriver;
            internal Vector3 sclTarget;


            //

            internal Vector3 pos;
            internal Vector3 posClean;
            //internal Vector3 localAdjVec;

            internal Vector3 posCleanDelta;
            internal Vector3 velCleanDelta;

            internal Vector3 posOffset;
            internal Vector3 rotOffset;
            internal Vector3 sclOffset;

            internal float posSpringVelCoef;

            // Clean rotation from that frame
            //internal Quaternion cleanRot;
            //internal Quaternion rotAdjustment;
            //internal Vector3 rotModifier;
            internal Quaternion adjustedRot;
            internal bool rotOffsetIsEmpty;
            internal Vector3 localPos;
            internal Quaternion localRot;

            //internal float sclTotalAccel;
            //internal float sclTotalDecel;

            internal Vector3 rotSlowDelta;
            internal Vector3 rotFastDelta;

            internal Vector3[] rotAxialSlowDelta = new Vector3[3];
            internal Vector3[] rotAxialFastDelta = new Vector3[3];


            internal void Clear(Transform bone)
            {
                var pos = bone.position;

                vel = Vector3.zero;
                //velLen = 0f;
                velOffset = Vector3.zero;
                torque = Vector3.zero;
                this.pos = pos;
                posClean = pos;
                posOffset = Vector3.zero;
                rotOffset = Vector3.zero;
                adjustedRot = bone.rotation;
                scl = Vector3.one;
                sclDriver = Vector3.zero;
                //sclTotalAccel = 0f;
                //sclTotalDecel = 0f;

                posCleanDelta = Vector3.zero;
                velCleanDelta = Vector3.zero;
            }

        }

        private class DebugValues
        {
            internal float posSpringVelCoef;
            internal float posSpringPosCoef;
            internal Vector3 posSpring;
            internal Vector3 posDamping;

            internal bool state_1;
            internal bool state_2;
            internal bool state_3;
            internal bool state_4;
            internal bool state_5;

            internal float factor_1 = 1f;
            internal float factor_2 = 1f;
            internal float factor_3 = 1f;
            internal float factor_4 = 1f;
            internal float factor_5 = 1f;

            internal Vector3 devRotApp = Vector3.one;

            internal Vector3 sclDriverN;
            internal Vector3 sclDriver;
            internal float sclDriverLen;
            internal float sclFactor;
            internal float sclStretch;
            internal float sclSquash;

            internal Vector3 v1;
            internal Vector3 v2;
            internal Vector3 v3;

            internal Vector3 q1e;
            internal Vector3 q2e;
            internal Vector3 q3e;

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
