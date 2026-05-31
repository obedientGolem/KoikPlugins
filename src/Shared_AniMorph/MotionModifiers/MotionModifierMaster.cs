using ActionGame.Point;
using ADV.Commands.Base;
using KKABMX.Core;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static AniMorph.AniMorphEffector;
using static AniMorph.MotionModifier;
using static AniMorph.AniMorphPlugin;

namespace AniMorph
{
    /// <summary>
    /// Adjust slaves based on the master calculations, apply partial adjustments to the master.
    /// </summary>
    internal class MotionModifierMaster : MotionModifier
    {
        private readonly MotionModifierSlave[] _slaves;

        internal MotionModifierMaster(BaseConfig baseCfg, Transform master, MotionModifierSlave[] slaveModifiers) 
            : base(baseCfg, master, null)
        {
            _slaves = slaveModifiers;
        }


        internal override void UpdateModifier(float dt, float dtInv, float animLen, float animLenInv)
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
            {
                rotOffset = GetRotOffset(ref cfg, ref curr, ref prev, dt, dtInv, animLen, animLenInv);
            }
            else
            {
                curr.cleanLocalRot = GetCleanLocalRot(ref prev);
                curr.cleanRotInverse = Quaternion.Inverse(transform.rotation);
            }

            if ((effects & Effect.Scl) != 0)
                sclOffset = GetSquashOffsetEx(ref cfg, ref curr, ref prev, dt, dtInv);


            // --- Update Dots ---

            var dotUp = Vector3.Dot(transform.up, Vector3.up);
            var dotR = Vector3.Dot(transform.right, Vector3.up);
            var dotFwd = Vector3.Dot(transform.forward, Vector3.up);

            if ((effects & Effect.PosOffset) != 0)
                posOffset += GetPosDotOffset(ref cfg, ref curr, dotUp, dotR);

            if ((effects & Effect.SclOffset) != 0)
                sclOffset = Vector3.Scale(sclOffset, GetSclDotOffset(ref cfg, ref curr, dotFwd));

            if ((effects & Effect.RotOffset) != 0)
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


            // --- Write Offsets ---

            var boneModifierData = abmxModifierData;

            boneModifierData.PositionModifier = curr.cleanLocalRot * posOffset;
            boneModifierData.RotationModifier = rotOffset;
            boneModifierData.ScaleModifier = sclOffset;


            // --- Prepare For Next Frame ---

            prev.posOffset = posOffset;
            prev.rotOffset = rotOffset;
            prev.sclOffset = sclOffset;


            // --- Update Slaves ---

            var devPosOffsetRot = curr.cleanRotInverse * posOffset;
            posOffset = transform.TransformDirection(posOffset);

            foreach (var slave in _slaves)
            {
                slave.UpdateSlave(
                    masterEffects: effects, 
                    dotUp:         dotUp, 
                    dotR:          dotR, 
                    dotFwd:        dotFwd, 
                    dt:            dt, 
                    dtInv:         dtInv, 
                    animLen:       animLen,
                    animLenInv:    animLenInv,
                    posOffset:     posOffset,
                    posOffsetRot:  devPosOffsetRot,
                    rotOffset:     rotOffset,
                    sclOffset:     sclOffset
                    );
            }

            OnPostUpdateModifier(ref prev, ref curr, dt);
        }
    }
}
