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
using static AniMorph.AniMorphPlugin;

namespace AniMorph
{
    internal class MotionModifierSlave : MotionModifier
    {
        protected /*readonly*/ bool _masterIsParent;
        protected Vector3 _localVecToMaster;
        protected readonly Transform _master;
        protected bool devRotatedPosOffset;

        internal MotionModifierSlave(BaseConfig baseCfg, Transform slave, Transform master) : base(baseCfg, slave, master)
        {
            _master = master;


            if (baseCfg.overrideMasterIsParent == null)
            {
                var parent = slave.transform.parent;
                while (parent != null)
                {
                    if (master == parent)
                    {
                        _masterIsParent = true;
                        break;
                    }
                    parent = parent.parent;
                }
            }
            else
                _masterIsParent = (bool)baseCfg.overrideMasterIsParent;
        }

        internal virtual void UpdateSlave(
            Effect masterEffects, 

            float dotUp, float dotR, float dotFwd, 

            float dt, float dtInv, float animLen, float animLenInv, 

            Vector3 posOffset, Vector3 posOffsetRot, Vector3 rotOffset, Vector3 sclOffset
            )
        {
            if (!active) return;


            ref var cfg = ref config;
            ref var curr = ref current;
            ref var prev = ref previous;

            var effects = cfg.effects;


            // --- Inherit offsets ---

            var inheritEffects = cfg.inheritEffects;

            if ((inheritEffects & Effect.Pos) != 0)
            {
                posOffset *= cfg.inheritPosF;

                if (devRotatedPosOffset)
                    posOffset = transform.rotation * posOffsetRot;
                else
                    posOffset = transform.InverseTransformDirection(posOffset);

                // A simple trick, works only with bones that constitute one skeletal mass, 
                // e.g. between the bones of ribcage.
                if ((masterEffects & Effect.Rot) != 0 && !_masterIsParent)
                    posOffset += _localVecToMaster - (Quaternion.Euler(rotOffset) * _localVecToMaster);

            }
            else
                posOffset = Vector3.zero;

            if ((inheritEffects & Effect.Rot) == 0)
                rotOffset = Vector3.zero;

            if ((inheritEffects & Effect.Scl) == 0)
                sclOffset = Vector3.one;


            // --- Update Noise Params ---

            curr.noiseAmplFactor = (OneThird + Mathf.Min(TwoThirds, animLenInv * curr.avgPosLen * 15f));
            curr.noiseFreqStep = cfg.noiseFreq * animLenInv * dt;


            // --- Update Offsets ---

            UpdatePosTracking(ref prev, ref curr);
            UpdateVelocityShock(ref cfg, ref curr, ref prev, dt, dtInv, animLen);

            if ((effects & Effect.Pos) != 0)
                posOffset += GetPosOffset(ref cfg, ref curr, ref prev, dt, dtInv, animLenInv);

            if ((effects & Effect.Rot) != 0)
                rotOffset += GetRotOffset(ref cfg, ref curr, ref prev, dt, dtInv, animLen, animLenInv);
            else
                // Required for correct application of local position.
                curr.cleanLocalRot = GetCleanLocalRot(ref prev);

            if ((effects & Effect.Scl) != 0)
                sclOffset = GetSquashOffsetEx(ref cfg, ref curr, ref prev, dt, dtInv);

            if ((effects & Effect.Tether) != 0)
                rotOffset += tether.GetTetheringOffset(prev.velocity, dt);
            

            // --- Update Dots ---
            
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

            OnPostUpdateModifier(ref prev, ref curr, dt);
        }


        internal override void Reset()
        {
            base.Reset();

            if (_master == null) return;

            _localVecToMaster = transform.InverseTransformPoint(_master.position);
        }
    }
}
