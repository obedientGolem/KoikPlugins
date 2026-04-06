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



        internal MotionModifierSlave(
            BoneConfig cfg,
            Transform bone, 
            Transform centeredBone, 
            KKABMX.Core.BoneModifierData boneModifierData, 
            bool animatedBone) : base(cfg, bone, centeredBone, boneModifierData, animatedBone)
        {

        }


        internal void UpdateSlave(float masterDotFwd, float masterDotRight, float dt, float dtInv, float animLenInv, Vector3 posOffset, Vector3 rotOffset, Vector3 sclOffset)
        {
            if (!active) return;

            ref var cfg = ref config;
            ref var curr = ref current;
            ref var prev = ref previous;


            if ((cfg.effects & Effect.Rot) != 0)
                rotOffset = GetRotOffset(ref cfg, ref curr, ref prev, dt, dtInv, animLenInv);

            if ((cfg.effects & Effect.Pos) != 0)
            {
                posOffset += GetPosOffset(ref cfg, ref curr, ref prev, dt, dtInv, animLenInv, out var velocity, /*out var velocityLen,*/ out var accel);


                if ((cfg.effects & Effect.Tether) != 0)
                    rotOffset += tether.GetTetheringOffset(velocity, dt);


                if ((cfg.effects & Effect.Scl) != 0)
                    sclOffset = GetScaleOffset(ref cfg, ref curr, ref prev, velocity, /*velocityLen,*/ dt, dtInv);


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

            var posPositive = posPositiveApp;
            var posNegative = posNegativeApp;

            var posSignScale = new Vector3(
                posOffset.x > 0f ? posPositive.x : posNegative.x,
                posOffset.y > 0f ? posPositive.y : posNegative.y,
                posOffset.z > 0f ? posPositive.z : posNegative.z
                );

            // TODO Include into two above on init once dev phase is over.
            posOffset = Vector3.Scale(posOffset, posApplication);
            posOffset = Vector3.Scale(posOffset, posSignScale);

            var abmxData = abmxModifierData;

            abmxData.PositionModifier = curr.cleanLocalRot * posOffset;
            abmxData.RotationModifier = rotOffset;
            abmxData.ScaleModifier = sclOffset;

            //AniMorph.Logger.LogDebug($"UpdateModifiers: pos[{positionModifier}] rot[{rotationModifier}] scale[{scaleModifier}]");

            // Store current variables as "previous" for the next frame.

            // Positional offset that will be the ABMX,
            // required for calculation of (semi)static bones.
            prev.posOffset = posOffset;
            prev.rotOffset = rotOffset;
            prev.sclOffset = sclOffset;

            //if ((effectMask & Effect.Tether) != 0)
            //    rotModifier += tether.GetTetheringOffset(velocity, deltaTime);

            //if ((effectMask & Effect.GravRot) != 0)
            //    rotModifier += GetGravityAngularOffset(masterDotFwd, masterDotRight);

            //abmxModifierData.PositionModifier = posModifier;
            //// Remove not allowed axes
            //abmxModifierData.RotationModifier = Vector3.Scale(rotModifier, AngularApplication);
            //abmxModifierData.ScaleModifier = sclModifier;
        }


        //internal override void OnSettingChanged(AniMorph.Body body, ChaControl chara)
        //{
        //    base.OnSettingChanged(body, chara);

        //    UpdateAngularApplication(AniMorph.ConfigDic[body].AngularApplicationSlave.Value);
        //}
    }
}
