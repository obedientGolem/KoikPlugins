using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using KKAPI;
using KKAPI.Chara;
using KKAPI.MainGame;
using KKAPI.Studio;
using KKAPI.Utilities;
using RootMotion;
using Studio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using UnityEngine;

namespace Kokyu
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInProcess(KoikatuAPI.GameProcessName)]
#if KK
    [BepInProcess(KoikatuAPI.GameProcessNameSteam)]
#endif
    [BepInProcess(KoikatuAPI.StudioProcessName)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    [BepInDependency(KKABMX.Core.KKABMX_Core.GUID, KKABMX.Core.KKABMX_Core.Version)]
    public class KokyuPlugin : BaseUnityPlugin
    {
        public const string GUID = "koik.kokyu";
        public const string PluginName = "Kokyu";
        public const string Version = "1.0.0";

        internal static new ManualLogSource Logger;
        internal static KokyuPlugin Instance;


        #region Config


        public static ConfigEntry<Sex> SettingEnableMask { get; set; }
        public static ConfigEntry<float> SettingGlobalRotation { get; set; }
        public static ConfigEntry<float> SettingGlobalMagnitude { get; set; }
        public static ConfigEntry<float> SettingGlobalSpeed { get; set; }


        #endregion


        private void Awake()
        {
            Instance = this;

            Logger = base.Logger;


            #region ConfigBindings


            SettingEnableMask = Config.Bind("", "Enable", Sex.Male | Sex.Female,
                new ConfigDescription("Characters breath in the MainGame and Maker.", 
                null, 
                new ConfigurationManagerAttributes { Order =  100}));


            SettingGlobalRotation = Config.Bind("", "Global rotation factor", 1f,
                new ConfigDescription("",
                new AcceptableValueRange<float>(0f, 2f),
                new ConfigurationManagerAttributes { Order = 90 }));


            SettingGlobalMagnitude = Config.Bind("", "Global magnitude factor", 1f,
                new ConfigDescription("",
                new AcceptableValueRange<float>(0f, 2f),
                new ConfigurationManagerAttributes { Order = 80 }));


            SettingGlobalSpeed = Config.Bind("", "Global speed factor", 1f,
                new ConfigDescription("",
                new AcceptableValueRange<float>(0f, 2f),
                new ConfigurationManagerAttributes { Order = 70 }));


            #endregion


            Config.SettingChanged += OnSettingChanged;

            CharacterApi.RegisterExtraBehaviour<KokyuCharaController>(GUID);

            KokyuGui.Register();
        }

        private void OnSettingChanged(object sender, EventArgs e)
        {
            KokyuCharaController.OnSettingChanged();
        }

        [Flags]
        public enum Sex
        {
            [Description("Dummy1")]
            Male = 1 << 0,
            [Description("Dummy2")]
            Female = 1 << 1,
        }
    }
}
