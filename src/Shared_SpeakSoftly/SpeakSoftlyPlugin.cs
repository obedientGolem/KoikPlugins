using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using KK_VariableVoiceVolume;
using KKAPI;
using KKAPI.Chara;
using KKAPI.MainGame;
using KKAPI.Utilities;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using static UnityEngine.UI.Image;

namespace KK_SpeakSoftly
{
    [BepInPlugin(GUID, Name, Version)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    [BepInProcess(KoikatuAPI.GameProcessName)]
    [BepInProcess(KoikatuAPI.VRProcessName)]
#if KK
    [BepInProcess(KoikatuAPI.GameProcessNameSteam)]
    [BepInProcess(KoikatuAPI.VRProcessNameSteam)]
#endif

    public class SpeakSoftlyPlugin : BaseUnityPlugin
    {
        public const string GUID = "koik.speaksoftly" ;
        public const string Name = "Speak Softly"

#if DEBUG
            + " [DEBUG]"
#endif
            ;
        public const string Version = "1.0.0";
        internal new static ManualLogSource Logger;
        public static SpeakSoftlyPlugin Instance;

        internal const float OneThird = (1f / 3f);
        internal const float TwoThirds = (2f / 3f);

        #region Settings

        public static ConfigEntry<Sex> PluginEnabled;
        public static ConfigEntry<SettingState> PluginOptions;
        public static ConfigEntry<float> VolumeFadeLength;
        public static ConfigEntry<float> VolumeCatchUp;

        public static ConfigEntry<float> MouthOpenVoice;
        public static ConfigEntry<float> MouthOpenBreath;
#if DEBUG
        public static ConfigEntry<bool> Debug;
#endif

#endregion

        private Harmony _harmonyPatch;

        private void Awake()
        {
            Logger = base.Logger;

            Instance = this;


            #region ConfigBindings


            PluginEnabled = Config.Bind(
                section: "",
                key: "PluginEnabled",
                defaultValue: Sex.Female,
                new ConfigDescription("",
                null,
                new ConfigurationManagerAttributes { Order = 20 })
                );


            PluginOptions = Config.Bind(
                section: "",
                key: "PluginOptions",
                defaultValue: SettingState.Breath | SettingState.Voice | SettingState.FadeIn | SettingState.FadeOut | SettingState.Randomize,
                new ConfigDescription("Enable – enables fade for characters' breath\n" +
                "Randomize – adds variability to fade",
                null,
                new ConfigurationManagerAttributes { Order = 10 })
                );


            VolumeFadeLength = Config.Bind(
                section: "",
                key: "VolumeFadeLength",
                defaultValue: 1f,
                new ConfigDescription("",
                new AcceptableValueRange<float>(0.5f, 3f),
                new ConfigurationManagerAttributes { Order = 0 })
                );


            //VolumeVoiceMultiplier = Config.Bind(
            //    section: "",
            //    key: "VolumeVoiceMultiplier",
            //    defaultValue: 1.3f,
            //    new ConfigDescription("",
            //    new AcceptableValueRange<float>(0.5f, 3f),
            //    new ConfigurationManagerAttributes { Order = -10 })
            //    );


            VolumeCatchUp = Config.Bind(
                section: "",
                key: "VolumeCatchUp",
                defaultValue: 3f,
                new ConfigDescription("Volume changes over specified period of time (in seconds) after the effects that prompted the change have happened.",
                new AcceptableValueRange<float>(0f, 10f),
                new ConfigurationManagerAttributes { Order = -20, ShowRangeAsPercent = false })
                );

            MouthOpenVoice = Config.Bind(
                section: "",
                key: "MouthOpenFactorVoice",
                defaultValue: TwoThirds,
                new ConfigDescription("",
                new AcceptableValueRange<float>(0f, 1f),
                new ConfigurationManagerAttributes { Order = -30, ShowRangeAsPercent = false })
                );
            MouthOpenBreath = Config.Bind(
                section: "",
                key: "MouthOpenFactorBreath",
                defaultValue: TwoThirds,
                new ConfigDescription("",
                new AcceptableValueRange<float>(0f, 1f),
                new ConfigurationManagerAttributes { Order = -31, ShowRangeAsPercent = false })
                );


            //VolumeBaseValue = Config.Bind(
            //    section: "",
            //    key: "VolumeBaseValue",
            //    defaultValue: 20f,
            //    new ConfigDescription("",
            //    new AcceptableValueRange<float>(0f, 50f),
            //    new ConfigurationManagerAttributes { Order = -30 })
            //    );


            //VolumeCeiling = Config.Bind(
            //    section: "",
            //    key: "VolumeCeiling",
            //    defaultValue: 1f,
            //    new ConfigDescription("",
            //    new AcceptableValueRange<float>(0f, 1f),
            //    new ConfigurationManagerAttributes { Order = -40, ShowRangeAsPercent = false })
            //    );


            //VolumeFloor = Config.Bind(
            //    section: "",
            //    key: "VolumeFloor",
            //    defaultValue: 0.2f,
            //    new ConfigDescription("",
            //    new AcceptableValueRange<float>(0f, 1f),
            //    new ConfigurationManagerAttributes { Order = -50, ShowRangeAsPercent = false })
            //    );
#if DEBUG
            Debug = Config.Bind(
                section: "",
                key: "Debug",
                defaultValue: true,
                new ConfigDescription("Volume changes over specified period of time (in seconds) after the effects that prompted the change have happened.",
                null,
                new ConfigurationManagerAttributes { Order = -20, ShowRangeAsPercent = false })
                );
#endif

#endregion


            Config.SettingChanged += (_, _1) => SpeakSoftlyCharaController.OnSettingChanged();

            PluginOptions.SettingChanged += (_, _1) => TryEnable();

            CharacterApi.RegisterExtraBehaviour<SpeakSoftlyCharaController>(GUID);

            TryEnable();

            SpeakSoftlyGui.Register();
        }

        private void TryEnable()
        {
            var enabled = PluginEnabled.Value != 0;

            if (enabled)
            {
                _harmonyPatch ??= Harmony.CreateAndPatchAll(typeof(KK_SpeakSoftly.SpeakSoftlyHooks));
            }
            else
            {
                _harmonyPatch?.UnpatchSelf();
                _harmonyPatch = null;
            }

            SpeakSoftlyCharaController.OnSettingChanged();
        }

        [Flags]
        public enum Sex
        {
            None = 0,
            Male = 1 << 0,
            Female = 1 << 1,
        }

        [Flags]
        public enum SettingState
        {
            None      = 0,
            Breath    = 1 << 0,
            Voice     = 1 << 1,
            FadeIn    = 1 << 2,
            FadeOut   = 1 << 3,
            Randomize = 1 << 4,
        }
    }
}
