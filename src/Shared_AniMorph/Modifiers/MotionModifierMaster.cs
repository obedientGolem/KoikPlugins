using ActionGame.Point;
using ADV.Commands.Base;
using KKABMX.Core;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AniMorph
{
    /// <summary>
    /// Adjust slaves based on the master calculations, apply partial adjustments to the master.
    /// </summary>
    internal class MotionModifierMaster : MotionModifier
    {
        private readonly MotionModifierSlave[] _slaves;

        internal MotionModifierMaster(
            Transform master,
            MotionModifierSlave[] slaves, 
            BoneModifierData boneModifierData,
            bool animatedBone
            ) : base(master, null, null, null, boneModifierData, animatedBone)
        {
            _slaves = slaves;
        }


        internal override void UpdateModifiers(float deltaTime, float fps)
        {
            if (!Active) return;
            // TODO Decentralize some calculations?

            ref var cfg = ref config;
            ref var prev = ref previous;

            // Apply linear offset, its calculations are necessary to other methods even if the offset itself isn't.
            var posModifier = GetLinearOffset(ref cfg, ref prev, deltaTime, out var velocity, out var velocityMagnitude);

            //// Remove linear offset if setting
            if ((cfg.effects & Effect.Pos) == 0)
            {
                posModifier = Vector3.zero;
            }
            // Apply angular offset
            var rotModifier = (cfg.effects & Effect.Rot) != 0 ? GetAngularOffset(ref cfg, ref prev, deltaTime) : Vector3.zero;

            var sclModifier = Vector3.Scale(
                abmxModifierData.ScaleModifier,
                GetScaleOffset(
                    ref cfg,
                    ref prev,
                    velocity,
                    velocityMagnitude,
                    deltaTime,
                    fps,
                    (cfg.effects & Effect.Accel) != 0,
                    (cfg.effects & Effect.Decel) != 0
                    )
                );

            var dotUp = Vector3.Dot(transform.up, Vector3.up);
            var dotR = Vector3.Dot(transform.right, Vector3.up);
            var dotFwd = Vector3.Dot(transform.forward, Vector3.up);

            // Apply gravity linear offset
            if ((cfg.effects & Effect.GravPos) != 0)
                posModifier += GetGravityPositionOffset(ref cfg, dotUp, dotR);

            // Apply gravity scale offset
            if ((cfg.effects & Effect.GravScl) != 0)
                sclModifier = Vector3.Scale(sclModifier, GetGravityScaleOffset(ref cfg, dotFwd));


            //AniMorph.Logger.LogDebug($"[{transform.name}] - {GetType().Name}.UpdateModifiers: rot({rotModifier}");
            // Discard not allowed axes



            foreach (var slave in _slaves)
            {
                slave.UpdateSlave(cfg.effects, velocity, deltaTime, dotFwd, dotR, posModifier, rotModifier, sclModifier);
            }
            rotModifier = Vector3.Scale(rotModifier, AngularApplication);
            abmxModifierData.RotationModifier = rotModifier;
            // Store current variables as "previous" for the next frame.
            prev.velocity = velocity;
            prev.rotModifier = rotModifier;
            // Master doesn't offset itself, only slaves move.
            //previous.posModifier = posModifier;
        }
    }
}
