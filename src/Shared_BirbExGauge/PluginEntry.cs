using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Analytics;

namespace BirbExGauge
{
    [BepInPlugin(GUID, Name, Version)]
    [BepInProcess(KoikatuAPI.GameProcessName)]
#if KK
    [BepInProcess(KoikatuAPI.GameProcessNameSteam)]
#endif
    public class BirbExGaugePlugin : BaseUnityPlugin
    {
        public const string GUID = "BirbExGauge";
        public const string Name = "Birb's Excitement Gauge"
#if DEBUG
             + " (Debug)"
#endif
            ;
        public const string Version = "1.1";


        internal new static ManualLogSource Logger;


        public static ConfigEntry<SmoothingType> EnableMale;
        public static ConfigEntry<SmoothingType> EnableFemale;
        public static ConfigEntry<float> MaleFloor;
        public static ConfigEntry<float> FemaleFloor;
        public static ConfigEntry<SceneType> EnabledScenes;

        private void Awake()
        {
            Logger = base.Logger;

            EnableMale = Config.Bind("", "EnableMale", SmoothingType.Cubic, 
                new ConfigDescription("Set function for smooth correlation between the speed of animation and the excitement gauge multiplier", 
                null, 
                new ConfigurationManagerAttributes { Order = 100 }));

            EnableFemale = Config.Bind("", "EnableFemale", SmoothingType.Cubic, 
                new ConfigDescription("Set function for smooth correlation between the speed of animation and the excitement gauge multiplier", 
                null, 
                new ConfigurationManagerAttributes { Order = 99 }));

            MaleFloor = Config.Bind("", "MaleFloor", 0.1f, 
                new ConfigDescription("Minimal possible multiplier", 
                new AcceptableValueRange<float>(0f, 1f), 
                new ConfigurationManagerAttributes { Order = 50, ShowRangeAsPercent = false}));

            FemaleFloor = Config.Bind("", "FemaleFloor", 0.1f, 
                new ConfigDescription("Minimal possible multiplier", 
                new AcceptableValueRange<float>(0f, 1f), 
                new ConfigurationManagerAttributes { Order = 49, ShowRangeAsPercent = false }));

            EnabledScenes = Config.Bind("", "EnabledScenes", SceneType.Intercourse, 
                new ConfigDescription("Apply in those scenes", 
                null, 
                new ConfigurationManagerAttributes { Order = 110 }));

            EnableMale.SettingChanged += (_, _1) => HooksCommon.TryEnable();
            EnableFemale.SettingChanged += (_, _1) => HooksCommon.TryEnable();

            MaleFloor.SettingChanged += (_, _1) => HooksCommon.UpdateConfig();
            FemaleFloor.SettingChanged += (_, _1) => HooksCommon.UpdateConfig();

            EnabledScenes.SettingChanged += (_, _1) => HooksCommon.TryEnable();

            HooksCommon.TryEnable();
        }



        public enum SmoothingType
        {
            Disable,
            Linear,
            Cubic,
            CubicPlateau,
            //Quint,
            //Sine
        }
        [Flags]
        public enum SceneType
        {
            Caress = 1,
            Service = 2,
            Intercourse = 4,
            Lesbian = 8,
        }
    }
}
