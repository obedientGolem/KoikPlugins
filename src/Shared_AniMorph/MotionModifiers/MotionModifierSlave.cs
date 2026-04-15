using ADV.Commands.Object;
using KKABMX.GUI;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using static AniMorph.AniMorphEffector;
using static AniMorph.MotionModifier;

namespace AniMorph
{
    internal class MotionModifierSlave : MotionModifier
    {
        private /*readonly*/ Effect _inheritEffects;
        private readonly bool _masterIsParent;
        private Vector3 _localVecToMaster;
        private readonly Transform _master;

        internal MotionModifierSlave(
            AniMorphEffector.BaseConfig cfg,
            Transform bone, 
            Transform master, 
            bool animatedBone) : base(cfg, bone, master, animatedBone)
        {
            _master = master;
            _inheritEffects = cfg.inheritEffects;
            var masterIsParent = false;
            var parent = bone.transform.parent;
            while (parent != null)
            {
                if (master == parent)
                {
                    masterIsParent = true;
                    break;
                }
                parent = parent.parent;
            }
            _masterIsParent = masterIsParent;
        }

        internal void UpdateSlave(Effect masterEffects, float masterDotFwd, float masterDotRight, float dt, float dtInv, float animLenInv, Vector3 posOffset, Vector3 rotOffset, Vector3 sclOffset)
        {
            if (!active) return;

            var inheritEffects = _inheritEffects;

            if ((inheritEffects & Effect.Pos) == 0)
                posOffset = Vector3.zero;
            else if ((masterEffects & Effect.Rot) != 0 && !_masterIsParent)
            {
                posOffset += _localVecToMaster - (Quaternion.Euler(rotOffset) * _localVecToMaster);
            }

            if ((inheritEffects & Effect.Rot) == 0)
                rotOffset = Vector3.zero;

            if ((inheritEffects & Effect.Scl) == 0)
                sclOffset = Vector3.one;


            ref var cfg = ref config;
            ref var curr = ref current;
            ref var prev = ref previous;


            if ((cfg.effects & Effect.Rot) != 0)
                rotOffset = GetRotOffset(ref cfg, ref curr, ref prev, dt, dtInv, animLenInv);
            else
                // Required for correct application of local position.
                curr.cleanLocalRot = GetCleanLocalRot(ref prev);

            if ((cfg.effects & Effect.Pos) != 0)
            {
                posOffset += GetPosOffset(ref cfg, ref curr, ref prev, dt, dtInv, animLenInv, out var velocity, /*out var velocityLen,*/ out var accel);

                if ((cfg.effects & Effect.Tether) != 0)
                    rotOffset += tether.GetTetheringOffset(velocity, dt);

                if ((cfg.effects & Effect.Scl) != 0)
                    sclOffset = GetSquashOffset(ref cfg, ref curr, ref prev, velocity, prev.cleanDeltaPos, dt);
                    //sclOffset = GetScaleOffset(ref cfg, ref curr, ref prev, velocity, /*velocityLen,*/ dt, dtInv);

                prev.velocity = velocity;
            }


            // Not allowed axes are multiplied by zero, allowed by one.
            //rotOffset = Vector3.Scale(rotOffset, rotApplication);

            //var scaleModifier = GetSquashOffset(
            //    ref cfg,
            //    ref prev,
            //    velocity, accel, velocityLen, deltaTime, invDeltaTime
            //    );


            //var dotUp = Vector3.Dot(transform.up, Vector3.up);
            //var dotR = Vector3.Dot(transform.right, Vector3.up);
            //var dotFwd = Vector3.Dot(transform.forward, Vector3.up);

            // Apply gravity position offset
            if ((cfg.effects & Effect.GravRot) != 0)
                rotOffset += GetGravityAngularOffset(masterDotFwd, masterDotRight);
            

            var posPositive = cfg.posPositiveApp;
            var posNegative = cfg.posNegativeApp;

            var posSignScale = new Vector3(
                posOffset.x > 0f ? posPositive.x : posNegative.x,
                posOffset.y > 0f ? posPositive.y : posNegative.y,
                posOffset.z > 0f ? posPositive.z : posNegative.z
                );

            // TODO Include into sign applications on init once dev phase is over.
            posOffset = Vector3.Scale(posOffset, posSignScale);
            sclOffset = Vector3.Scale(sclOffset, cfg.sclApplication);

            var boneModifierData = abmxModifierData;

            boneModifierData.PositionModifier = curr.cleanLocalRot * posOffset;
            boneModifierData.RotationModifier = rotOffset;
            boneModifierData.ScaleModifier = sclOffset;

            prev.posOffset = posOffset;
            prev.rotOffset = rotOffset;
            prev.sclOffset = sclOffset;
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

        internal override void OnChangeAnimator()
        {
            base.OnChangeAnimator();

            if (_master == null) return;

            _localVecToMaster = transform.InverseTransformPoint(_master.position);
        }
    }
}
