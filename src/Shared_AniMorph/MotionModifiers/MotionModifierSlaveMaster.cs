using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static AniMorph.AniMorphEffector;
using static AniMorph.MotionModifier;
using static AniMorph.AniMorphPlugin;

namespace AniMorph
{
    internal class MotionModifierSlaveMaster : MotionModifierSlave
    {
        private readonly MotionModifierSlave[] _slaves;

        internal MotionModifierSlaveMaster(BaseConfig cfg, Transform bone, Transform master, MotionModifierSlave[] slaveModifiers)
            : base(cfg, bone, master)
        {
            _slaves = slaveModifiers;
        }

        internal override void UpdateSlave(
            Effect masterEffects, 

            float dotUp, float dotR, float dotFwd, 

            float dt, float dtInv, float animLenInv, 

            Vector3 posOffset, Vector3 devPosOffsetRot, Vector3 rotOffset, Vector3 sclOffset)
        {
            if (!active) return;

            ref var cfg = ref config;
            ref var curr = ref current;
            ref var prev = ref previous;


            // --- Update Noise Params ---

            curr.noiseAmplFactor = (OneThird + Mathf.Min(TwoThirds, animLenInv * prev.avgPosLen * 15f));
            curr.noiseFreq = cfg.noiseFreq * animLenInv * dt;


            // --- Update Offsets ---

            var effects = cfg.effects;

            var slaveMasterPosOffset = GetPosOffset(ref cfg, ref curr, ref prev, dt, dtInv, animLenInv, out var velocity);

            if ((effects & Effect.Pos) != 0)
                posOffset += slaveMasterPosOffset;

            if ((effects & Effect.Rot) != 0)
            {
                rotOffset += GetRotOffset(ref cfg, ref curr, ref prev, dt, dtInv, animLenInv);
            }
            else
            {
                curr.cleanLocalRot = GetCleanLocalRot(ref prev);
                curr.cleanRotInverse = Quaternion.Inverse(transform.rotation);
            }

            if ((effects & Effect.Scl) != 0)
                sclOffset = Vector3.Scale(sclOffset, GetSquashOffset(ref cfg, ref curr, ref prev, velocity, dt));

            // --- Apply Dot Offsets /w Master Dots ---

            if ((effects & Effect.PosOffset) != 0)
                posOffset += GetPosDotOffset(ref cfg, ref curr, dotUp, dotR);

            if ((effects & Effect.SclOffset) != 0)
                sclOffset = Vector3.Scale(sclOffset, GetSclDotOffset(ref cfg, ref curr, dotFwd));

            if ((effects & Effect.RotOffset) != 0)
                rotOffset += GetRotDotOffset(ref cfg, ref curr, dotFwd, dotR);

            // --- Update Dots ---

            dotUp = Vector3.Dot(transform.up, Vector3.up);
            dotR = Vector3.Dot(transform.right, Vector3.up);
            dotFwd = Vector3.Dot(transform.forward, Vector3.up);


            // --- Prepare Application --- 

            var posPositive = cfg.posAppPositive;
            var posNegative = cfg.posAppNegative;

            var posSignScale = new Vector3(
                posOffset.x > 0f ? posPositive.x : posNegative.x,
                posOffset.y > 0f ? posPositive.y : posNegative.y,
                posOffset.z > 0f ? posPositive.z : posNegative.z
                );

            posOffset = Vector3.Scale(posOffset, posSignScale);
            //rotOffset = Vector3.Scale(rotOffset, cfg.rotApplication);

            var sclApp = cfg.sclApplication;

            sclOffset = new Vector3(
                sclApp.x < 1f ? 1f + ((sclOffset.x - 1f) * sclApp.x) : sclOffset.x * sclApp.x,
                sclApp.y < 1f ? 1f + ((sclOffset.y - 1f) * sclApp.y) : sclOffset.y * sclApp.y,
                sclApp.z < 1f ? 1f + ((sclOffset.z - 1f) * sclApp.z) : sclOffset.z * sclApp.z
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

            // --- Update Slaves ---

            devPosOffsetRot = curr.cleanRotInverse * posOffset;
            posOffset = transform.TransformDirection(posOffset);

            foreach (var slave in _slaves)
            {
                slave.UpdateSlave(
                    masterEffects: cfg.effects,
                    dotUp: dotUp,
                    dotR: dotR,
                    dotFwd: dotFwd,
                    dt: dt,
                    dtInv: dtInv,
                    animLenInv: animLenInv,
                    posOffset: posOffset,
                    posOffsetRot: devPosOffsetRot,
                    rotOffset: rotOffset,
                    sclOffset: sclOffset
                    );
            }
        }

        internal override void OnAnimationLoopStart(float animLoopFrameCountInv, float dt)
        {
            base.OnAnimationLoopStart(animLoopFrameCountInv, dt);

            foreach (var slave in _slaves)
                slave.OnAnimationLoopStart(animLoopFrameCountInv, dt);
        }
    }
}
