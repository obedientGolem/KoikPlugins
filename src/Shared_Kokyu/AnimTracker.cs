using ExtensibleSaveFormat;
using HarmonyLib;
using KKABMX.Core;
using KKAPI;
using KKAPI.Chara;
using KKAPI.MainGame;
using KKAPI.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static Kokyu.KokyuCharaController;

namespace Kokyu
{
    internal class AnimTracker
    {
        private static readonly List<AnimTracker> _instances = [];
        private static HFlag _hFlag;
        private static Harmony _harmonyPatch;
        private readonly KokyuCharaController _customChaCtrl;

        internal static bool IsHScene => KKAPI.SceneApi.GetAddSceneName().Equals("HProc");


        internal AnimTracker(KokyuCharaController customChaCtrl)
        {
            _customChaCtrl = customChaCtrl;
            _instances.Add(this);
            TryEnable();
        }

        internal static void TryEnable()
        {
            _harmonyPatch ??= Harmony.CreateAndPatchAll(typeof(AnimTracker));
        }

        internal void OnReload(ChaControl chara)
        {
            if (chara == null) throw new ArgumentNullException();

            if (_hFlag == null)
                _hFlag = Component.FindObjectOfType<HFlag>();
        }

        private void OnSetPlay(string stateName)
        {
            var anim = stateName switch
            {
                var s when s.Equals("Idle", StringComparison.Ordinal) => Anim.Idle,
                var s when s.Equals("A_Idle", StringComparison.Ordinal) => Anim.Idle,

                // Caress
                var s when s.EndsWith("_Touch", StringComparison.Ordinal) => Anim.CaressTouch,
                var s when s.EndsWith("_Idle", StringComparison.Ordinal) => Anim.CaressIdle,
                var s when s.EndsWith("_Dislikes", StringComparison.Ordinal) => Anim.CaressDislike,

                // Rest
                var s when s.EndsWith("InsertIdle", StringComparison.Ordinal) => Anim.IdleInside,
                var s when s.EndsWith("Insert", StringComparison.Ordinal) => Anim.Insert,
                var s when s.EndsWith("WLoop", StringComparison.Ordinal) => Anim.WeakLoop,
                var s when s.EndsWith("SLoop", StringComparison.Ordinal) => Anim.StrongLoop,
                var s when s.EndsWith("OLoop", StringComparison.Ordinal) => Anim.OrgasmLoop,

                var s when s.Contains("M_IN") => Anim.OrgasmMale,
                var s when s.Contains("M_OUT") => Anim.OrgasmMale,

                var s when s.Contains("WF_IN") => Anim.OrgasmFemale,
                var s when s.Contains("SF_IN") => Anim.OrgasmFemale,

                var s when s.Contains("WS_IN") => Anim.OrgasmBoth,
                var s when s.Contains("SS_IN") => Anim.OrgasmBoth,

                var s when s.EndsWith("IN_A", StringComparison.Ordinal) => Anim.AfterOrgasmIn,
                var s when s.EndsWith("OUT_A", StringComparison.Ordinal) => Anim.AfterOrgasmOut,

                var s when s.EndsWith("Pull", StringComparison.Ordinal) => Anim.Pull,
                var s when s.EndsWith("Drop", StringComparison.Ordinal) => Anim.Pull,
                _ => Anim.Idle,
            };


//            var chara = _component.ChaControl;
//            var statsWeight = 0f;
//            // Attributes net (-35..40)
//            var attribute = chara.chaFile.parameter.attribute;
//            // In all honesty I'd put +50 here for authenticity.
//            if (attribute.bitch) statsWeight += 20f;
//            if (attribute.choroi) statsWeight += 20f;
//            if (attribute.hitori || attribute.kireizuki || attribute.dokusyo) statsWeight -= 20f;
//            if (attribute.majime) statsWeight -= 15f;

//            var heroine = chara.GetHeroine() ?? (_hFlag != null ? _hFlag.GetLeadingHeroine() : null);

//            // (-15..100)
//            if (heroine != null)
//            {
//                // From -15 to 30
//                statsWeight = ((int)heroine.HExperience - 1) * 15f;

//                // (0..45)
//                if (heroine.isGirlfriend)
//                    statsWeight += 30f;
//#if KKS
//                // Can't remember how OG Koik does this.
//                else if (heroine.isFriend)
//                    statsWeight += 15f;

//                statsWeight += (heroine.favor * 0.15f);
//#endif
//                statsWeight += heroine.lewdness * 0.25f;
//            }

//            statsWeight *= 0.01f;



            var pattern = anim switch
            {
                Anim.WeakLoop or Anim.CaressIdle => Pattern.ActivityLight,
                Anim.StrongLoop or Anim.OrgasmMale or Anim.AfterOrgasmOut or Anim.CaressTouch => Pattern.ActivityModerate,
                Anim.OrgasmLoop or Anim.OrgasmFemale or Anim.OrgasmBoth => Pattern.ActivityHeavy,
                Anim.AfterOrgasmIn => Pattern.Exhaustion,
                _ => Pattern.Anxiety,
            };
            _customChaCtrl.OnAnimatorStateChange(pattern);
        }

        [HarmonyPostfix]
#if KK
        [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.setPlay))]
        public static void OnChaControl_setPlayPostfix(string _strAnmName, ChaControl __instance)
        {
            var animName = _strAnmName;
#elif KKS
        [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.syncPlay), [typeof(string), typeof(int), typeof(float)])]
        public static void OnChaControl_setPlayPostfix(string _strameHash, ChaControl __instance)
        {
            var animName = _strameHash;
#endif
#if DEBUG
            KokyuPlugin.Logger.LogDebug($"AnimatorPlayPostfix: stateName[{animName}]");
#endif
            foreach (var inst in _instances)
            {
                if (inst?._customChaCtrl.ChaControl == __instance)
                {
                    inst.OnSetPlay(animName);
                    break;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSceneProc), nameof(HSceneProc.ChangeAnimator))]
        public static void OnChangeAnimatorPostfix(HSceneProc.AnimationListInfo _nextAinmInfo)
        {
#if DEBUG
            KokyuPlugin.Logger.LogDebug($"OnChangeAnimatorPostfix:AnimInfo: mode[{_nextAinmInfo.mode}]" +
                $"kindHoushi[{_nextAinmInfo.kindHoushi}]");
#endif
            // If not service tit related position – false.
            var boobsPos = _nextAinmInfo.mode == HFlag.EMode.houshi && _nextAinmInfo.kindHoushi == 2;

            // Should give the HSceneProc.lstFemale[0].
            var mainHeroine = GameAPI.GetCurrentHeroine();
            if (mainHeroine == null || mainHeroine.chaCtrl == null) return;

            foreach (var inst in _instances)
            {
                if (inst._customChaCtrl != null && inst._customChaCtrl.ChaControl == mainHeroine.chaCtrl)
                    inst._customChaCtrl.UpdateCaress(boobsPos, boobsPos);
            }
        }

        internal enum Anim
        {
            Idle,
            CaressTouch,
            CaressIdle,
            CaressOrgasm,
            CaressDislike,
            IdleInside,
            Insert,
            WeakLoop,
            StrongLoop,
            OrgasmLoop,
            OrgasmMale,
            OrgasmFemale,
            OrgasmBoth,
            AfterOrgasmIn,
            AfterOrgasmOut,
            Pull,
            Oral,
        }
    }
}
