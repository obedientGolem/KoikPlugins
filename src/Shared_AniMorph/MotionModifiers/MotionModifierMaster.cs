using ActionGame.Point;
using ADV.Commands.Base;
using KKABMX.Core;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static AniMorph.AniMorphEffector;

namespace AniMorph
{
    /// <summary>
    /// Adjust slaves based on the master calculations, apply partial adjustments to the master.
    /// </summary>
    internal class MotionModifierMaster : MotionModifier
    {
        private readonly MotionModifierSlave[] _slaves;

        internal MotionModifierMaster(
            AniMorphEffector.BaseConfig cfg,
            Transform transform,
            MotionModifierSlave[] slaveModifiers, 
            bool isAnimatedBone
            ) : base(cfg, transform, null, isAnimatedBone)
        {
            _slaves = slaveModifiers;
        }


        internal override void UpdateModifier(float dt, float dtInv, float animLenInv)
        {
            if (!active) return;

            ref var cfg = ref config;
            ref var curr = ref current;
            ref var prev = ref previous;

            var posOffset = Vector3.zero;
            var rotOffset = Vector3.zero;
            var sclOffset = Vector3.one;


            // --- Update Noise Params ---

            curr.noiseAmplFactor = (0.25f + Mathf.Min(0.75f, animLenInv * prev.avgCleanAdjDeltaPosLen * 15f));
            curr.noiseFreq = cfg.noiseFreq * animLenInv * dt;


            // --- Update Offsets ---

            var effects = cfg.effects;

            posOffset = GetPosOffset(ref cfg, ref curr, ref prev, dt, dtInv, animLenInv, out var velocity, out var accel);

            if ((effects & Effect.Pos) == 0)
                posOffset = Vector3.zero;

            if ((effects & Effect.Rot) != 0)
                rotOffset = GetRotOffset(ref cfg, ref curr, ref prev, dt, dtInv, animLenInv);
            else
                // Required for correct application of local position.
                curr.cleanLocalRot = GetCleanLocalRot(ref prev);

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
            rotOffset = Vector3.Scale(rotOffset, cfg.rotApplication);

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

            curr.needNoisePos = cfg.noiseAmplPos > 0f;
            curr.needNoiseRot = cfg.noiseAmplRot > 0f;
            curr.needNoiseScl = cfg.noiseAmplScl > 0f;


            // --- Update Slaves ---

            foreach (var slave in _slaves)
            {
                slave.UpdateSlave(
                    masterEffects: effects, 
                    dotUp:         dotUp, 
                    dotR:          dotR, 
                    dotFwd:        dotFwd, 
                    dt:            dt, 
                    dtInv:         dtInv, 
                    animLenInv:    animLenInv, 
                    posOffset:     posOffset,
                    rotOffset:     rotOffset, 
                    sclOffset:     sclOffset
                    );
            }
        }

        internal override void OnSettingChanged(AniMorphPlugin.Body body, ChaControl chara)
        {
            base.OnSettingChanged(body, chara);

            ref var cfg = ref config;
            // --- Clean-up effects ---
            if (baseConfig.allowedEffects == Effect.DevAnything) return;

            foreach (Effect effect in effects)
            {
                if ((baseConfig.allowedEffects & effect) != 0) continue;

                config.effects &= ~effect;
            }
        }

        internal override void OnUpdate()
        {
            base.OnUpdate();

            foreach (var slave in _slaves)
                slave.OnUpdate();
        }

        internal override void OnAnimationLoopStart(float animLoopFrameCountInv, float dt)
        {
            base.OnAnimationLoopStart(animLoopFrameCountInv, dt);

            foreach (var slave in _slaves)
                slave.OnAnimationLoopStart(animLoopFrameCountInv, dt);
        }
    }
}
