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
    [BepInPlugin(GUID, "KK_SpeakSoftly", Version)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    [BepInProcess(KoikatuAPI.GameProcessName)]
    [BepInProcess(KoikatuAPI.VRProcessName)]
#if KK
    [BepInProcess(KoikatuAPI.GameProcessNameSteam)]
    [BepInProcess(KoikatuAPI.VRProcessNameSteam)]
#endif

    public class SpeakSoftlyPlugin : BaseUnityPlugin
    {
        public const string GUID = "KK_SpeakSoftly";
        public const string Version = "1.0.0";
        internal new static ManualLogSource Logger;
        public static SpeakSoftlyPlugin Instance;

        #region Settings

        public static ConfigEntry<SettingState> PluginState;
        public static ConfigEntry<float> VolumeFadeLength;
        public static ConfigEntry<float> VolumeCatchUp;

        #endregion

        private Harmony _harmonyPatch;

        private void Awake()
        {
            Logger = base.Logger;
            Instance = this;


            #region ConfigBindings


            PluginState = Config.Bind(
                section: "",
                key: "PluginState",
                defaultValue: SettingState.Female | SettingState.Breath | SettingState.Voice | SettingState.FadeIn | SettingState.FadeOut | SettingState.Randomize,
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


            #endregion


            VolumeCatchUp.SettingChanged += (_, _1) => SpeakSoftlyCharaController.OnSettingChanged();
            PluginState.SettingChanged += (_, _1) => TryEnable();
            CharacterApi.RegisterExtraBehaviour<SpeakSoftlyCharaController>(GUID);
            TryEnable();
            SpeakSoftlyGui.Register();
        }

        private void TryEnable()
        {
            var setting = (PluginState.Value & SettingState.Female) != 0;
            // Enabled
            if (setting)
            {
                _harmonyPatch ??= Harmony.CreateAndPatchAll(typeof(KK_SpeakSoftly.Hooks));
            }
            // Disabled
            else
            {
                if (_harmonyPatch != null)
                {
                    _harmonyPatch.UnpatchSelf();
                    _harmonyPatch = null;
                }
            }
            foreach (var instance in SpeakSoftlyCharaController.Instances)
            {
                instance.enabled = setting;
            }
        }

        [Flags]
        public enum SettingState
        {
            None      = 0,
            Female    = 1 << 0,
            Male      = 1 << 1,
            Breath    = 1 << 2,
            Voice     = 1 << 3,
            FadeIn    = 1 << 4,
            FadeOut   = 1 << 5,
            Randomize = 1 << 6,
        }
    }
}
