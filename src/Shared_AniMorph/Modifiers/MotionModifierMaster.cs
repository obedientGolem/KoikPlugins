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
        private readonly Effect _masterEffects;

        internal MotionModifierMaster(
            BoneConfig cfg,
            Transform transform,
            MotionModifierSlave[] slaveModifiers, 
            BoneModifierData masterModifierData,
            bool isAnimatedBone
            ) : base(cfg, transform, null, masterModifierData, isAnimatedBone)
        {
            _slaves = slaveModifiers;
            _masterEffects = cfg.effects;
        }


        internal override void UpdateModifier(float deltaTime, float deltaTimeInv, float animLenInv)
        {
            if (!active) return;

            ref var prev = ref previous;
            ref var cfg = ref config;

            var posOffset = Vector3.zero;
            var rotOffset = Vector3.zero;
            var sclOffset = Vector3.one;

            //var freeze = FreezeTimer > 0f;

            //if (freeze)
            //    FreezeTimer -= deltaTime;

            // TODO Decentralize some calculations?


            // Apply linear offset, its calculations are necessary to other methods even if the offset itself isn't.
            //var posModifier = GetPosOffset(ref cfg, ref prev, deltaTime, deltaTimeInv, animLenInv, out var velocity, out var velocityLen, out var accel);

            ////// Remove linear offset if setting
            //if ((cfg.effects & Effect.Pos) == 0)
            //{
            //    posModifier = Vector3.zero;
            //}
            // Apply angular offset
            //var rotModifier = (cfg.effects & Effect.Rot) != 0 ? GetAngularOffset(ref cfg, ref prev, deltaTime) : Vector3.zero;

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

            // Apply gravity linear offset
            if ((cfg.effects & Effect.GravPos) != 0)
                posOffset += GetGravityPositionOffset(ref cfg, dotUp, dotR);

            // Apply gravity scale offset
            if ((cfg.effects & Effect.GravScl) != 0)
                sclOffset = Vector3.Scale(sclOffset, GetGravityScaleOffset(ref cfg, dotFwd));

            foreach (var slave in _slaves)
            {
                slave.UpdateSlave(dotFwd, dotR, deltaTime, deltaTimeInv, animLenInv, posOffset, rotOffset, sclOffset);
            }

            //AniMorphPlugin.Logger.LogDebug($"[{transform.name}] UpdateModifiers: " +
            //    $"velocity({velocity.x:F3},{velocity.y:F3},{velocity.z:F3}) " +
            //    $"accel({accel.x:F3},{accel.y:F3},{accel.z:F3}) ");
            // Discard not allowed axes

            //AniMorphPlugin.Logger.LogDebug($"[{transform.name}] posModifier[{posModifier.magnitude:F3}]");

            //if (!freeze)
            //{
            //foreach (var slave in _slaves)
            //    {
            //        slave.UpdateSlave(cfg.effects, velocity, deltaTime, dotFwd, dotR, posModifier, rotModifier, sclModifier);
            //    }
            //}

            //rotModifier = Vector3.Scale(rotModifier, AngularApplication);
            //if (!freeze)
                //abmxModifierData.RotationModifier = rotModifier;


            // Store current variables as "previous" for the next frame.
            //prev.velocity = velocity;
            //prev.rotModifier = rotModifier;
            // Master doesn't offset itself, only slaves move.
            //previous.posModifier = posModifier;
        }

        internal override void OnSettingChanged(AniMorphPlugin.Body body, ChaControl chara)
        {
            base.OnSettingChanged(body, chara);

            foreach (Effect effect in Enum.GetValues(typeof(Effect)))
            {
                if ((_masterEffects & effect) != 0) continue;

                config.effects &= ~effect;
            }
        }

        internal override void OnUpdate()
        {
            base.OnUpdate();

            foreach (var slave in _slaves)
                slave.OnUpdate();
        }
    }
}
