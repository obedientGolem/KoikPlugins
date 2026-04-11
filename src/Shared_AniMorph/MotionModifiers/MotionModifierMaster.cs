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
            BaseConfig cfg,
            Transform transform,
            MotionModifierSlave[] slaveModifiers, 
            BoneModifierData masterModifierData,
            bool isAnimatedBone
            ) : base(cfg, transform, null, masterModifierData, isAnimatedBone)
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

            var effects = cfg.effects;

            posOffset = GetPosOffset(ref cfg, ref curr, ref prev, dt, dtInv, animLenInv, out var velocity, out var accel);

            if ((effects & Effect.Pos) == 0)
                posOffset = Vector3.zero;

            if ((effects & Effect.Rot) != 0)
                rotOffset = GetRotOffset(ref cfg, ref curr, ref prev, dt, dtInv, animLenInv);
            else
                // Required for correct application of local position.
                curr.cleanLocalRot = GetCleanLocalRot(ref prev);


            //var sclModifier = Vector3.Scale(
            //    abmxModifierData.ScaleModifier,
            //    GetScaleOffset(
            //        ref cfg,
            //        ref prev,
            //        velocity,
            //        velocityLen,
            //        deltaTime,
            //        deltaTimeInv,
            //        (cfg.effects & Effect.Accel) != 0,
            //        (cfg.effects & Effect.Decel) != 0
            //        )
            //    );
            //var sclModifier = 
            //    GetSquashOffset(
            //        ref cfg,
            //        ref prev,
            //        velocity,
            //        accel,
            //        velocityLen,
            //        deltaTime,
            //        deltaTimeInv
            //    );

            var dotUp = Vector3.Dot(transform.up, Vector3.up);
            var dotR = Vector3.Dot(transform.right, Vector3.up);
            var dotFwd = Vector3.Dot(transform.forward, Vector3.up);

            if ((effects & Effect.GravPos) != 0)
                posOffset += GetGravityPositionOffset(ref cfg, dotUp, dotR);

            if ((effects & Effect.GravScl) != 0)
                sclOffset = Vector3.Scale(sclOffset, GetGravityScaleOffset(ref cfg, dotFwd));


            var posPositive = cfg.posPositiveApp;
            var posNegative = cfg.posNegativeApp;

            var posSignScale = new Vector3(
                posOffset.x > 0f ? posPositive.x : posNegative.x,
                posOffset.y > 0f ? posPositive.y : posNegative.y,
                posOffset.z > 0f ? posPositive.z : posNegative.z
                );

            // TODO Include into sign applications on init once dev phase is over.
            posOffset = Vector3.Scale(posOffset, posSignScale);
            rotOffset = Vector3.Scale(rotOffset, cfg.rotApplication);
            sclOffset = Vector3.Scale(sclOffset, cfg.sclApplication);

            var boneModifierData = abmxModifierData;

            boneModifierData.PositionModifier = curr.cleanLocalRot * posOffset;
            boneModifierData.RotationModifier = rotOffset;
            boneModifierData.ScaleModifier = sclOffset;

            prev.velocity = velocity;
            prev.posOffset = posOffset;
            prev.rotOffset = rotOffset;
            prev.sclOffset = sclOffset;

            foreach (var slave in _slaves)
            {
                slave.UpdateSlave(effects, dotFwd, dotR, dt, dtInv, animLenInv, posOffset, rotOffset, sclOffset);
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
