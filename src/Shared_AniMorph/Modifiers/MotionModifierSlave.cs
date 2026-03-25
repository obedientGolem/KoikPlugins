using KKABMX.GUI;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using static AniMorph.MotionModifier;

namespace AniMorph
{
    internal class MotionModifierSlave : MotionModifier
    {



        internal MotionModifierSlave(
            Transform bone, 
            Transform centeredBone, 
            Mesh bakedMesh, 
            SkinnedMeshRenderer skinnedMesh, 
            KKABMX.Core.BoneModifierData boneModifierData, 
            bool animatedBone) : base(bone, centeredBone, bakedMesh, skinnedMesh, boneModifierData, animatedBone)
        {

        }


        internal void UpdateSlave(Effect effectMask, Vector3 velocity, float deltaTime, float masterDotFwd, float masterDotRight, Vector3 positionModifier, Vector3 rotationModifier, Vector3 scaleModifier)
        {
            if ((effectMask & Effect.Tether) != 0)
                rotationModifier += tether.GetTetheringOffset(velocity, deltaTime);

            if ((effectMask & Effect.GravRot) != 0)
                rotationModifier += GetGravityAngularOffset(masterDotFwd, masterDotRight);

            abmxModifierData.PositionModifier = positionModifier;
            // Remove not allowed axes
            abmxModifierData.RotationModifier = Vector3.Scale(rotationModifier, AngularApplication);
            abmxModifierData.ScaleModifier = scaleModifier;
        }


        //internal override void OnSettingChanged(AniMorph.Body body, ChaControl chara)
        //{
        //    base.OnSettingChanged(body, chara);

        //    UpdateAngularApplication(AniMorph.ConfigDic[body].AngularApplicationSlave.Value);
        //}
    }
}
