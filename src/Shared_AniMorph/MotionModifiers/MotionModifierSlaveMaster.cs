using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static AniMorph.AniMorphEffector;

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

            Vector3 posOffset, Vector3 posOffsetRot, Vector3 rotOffset, Vector3 sclOffset)
        {
            base.UpdateSlave(masterEffects, dotUp, dotR, dotFwd, dt, dtInv, animLenInv, posOffset, posOffsetRot, rotOffset, sclOffset);

            ref var cfg = ref config;
            ref var curr = ref current;
            
            // Master's dots for slaves, not master's master's
            dotUp = Vector3.Dot(transform.up, Vector3.up);
            dotR = Vector3.Dot(transform.right, Vector3.up);
            dotFwd = Vector3.Dot(transform.forward, Vector3.up);

            if ((cfg.effects & Effect.Rot) == 0)
            {
                curr.cleanRotInverse = Quaternion.Inverse(transform.rotation);
            }

            // --- Update Slaves ---

            posOffsetRot = curr.cleanRotInverse * posOffset;
            posOffset = transform.TransformDirection(posOffset);

            foreach (var slave in _slaves)
            {
                slave.UpdateSlave(
                    masterEffects: masterEffects | cfg.effects,
                    dotUp: dotUp,
                    dotR: dotR,
                    dotFwd: dotFwd,
                    dt: dt,
                    dtInv: dtInv,
                    animLenInv: animLenInv,
                    posOffset: posOffset,
                    posOffsetRot: posOffsetRot,
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
