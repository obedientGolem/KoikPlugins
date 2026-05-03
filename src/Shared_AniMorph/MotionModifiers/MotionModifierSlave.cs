using ActionGame.Point;
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

        internal MotionModifierSlave(BaseConfig cfg, Transform bone, Transform master) : base(cfg, bone, master)
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

        internal void UpdateSlave(Effect masterEffects, float dotUp, float dotR, float dotFwd, float dt, float dtInv, float animLenInv, Vector3 posOffset, Vector3 rotOffset, Vector3 sclOffset)
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


            // --- Update Noise Params ---

            curr.noiseAmplFactor = (0.25f + Mathf.Min(0.75f, animLenInv * prev.avgCleanAdjDeltaPosLen * 15f));
            curr.noiseFreq = cfg.noiseFreq * animLenInv * dt;


            // --- Update Offsets ---

            var effects = cfg.effects;

            if ((effects & Effect.Rot) != 0)
                rotOffset = GetRotOffset(ref cfg, ref curr, ref prev, dt, dtInv, animLenInv);
            else
                // Required for correct application of local position.
                curr.cleanLocalRot = GetCleanLocalRot(ref prev);

            if ((effects & Effect.Pos) != 0)
            {
                posOffset += GetPosOffset(ref cfg, ref curr, ref prev, dt, dtInv, animLenInv, out var velocity, /*out var velocityLen,*/ out var accel);

                if ((effects & Effect.Tether) != 0)
                    rotOffset += tether.GetTetheringOffset(velocity, dt);

                if ((effects & Effect.Scl) != 0)
                    sclOffset = GetSquashOffset(ref cfg, ref curr, ref prev, velocity, prev.cleanDeltaPos, dt);

                prev.velocity = velocity;
            }

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

            prev.posOffset = posOffset;
            prev.rotOffset = rotOffset;
            prev.sclOffset = sclOffset;

            curr.needNoisePos = cfg.noiseAmplPos > 0f;
            curr.needNoiseRot = cfg.noiseAmplRot > 0f;
            curr.needNoiseScl = cfg.noiseAmplScl > 0f;
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

        internal override void Reset()
        {
            base.Reset();

            if (_master == null) return;

            _localVecToMaster = transform.InverseTransformPoint(_master.position);
        }
    }
}
