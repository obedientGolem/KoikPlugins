#if VR
using KK_VR;
#endif

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Utilities;
using Koik_LateSwordAiming;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IKNoise
{
    [BepInPlugin(GUID, Name, Version)]
    [BepInProcess(KoikatuAPI.GameProcessName)]
#if KK
    [BepInProcess(KoikatuAPI.GameProcessNameSteam)]
#endif
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
#if VR
    [BepInDependency(VRPlugin.GUID, VRPlugin.Version)]
#endif
    internal class IKNoisePlugin : BaseUnityPlugin
    {
        public const string GUID = "koik.iknoise" +
#if VR
            ".vr" +
#endif
            "";
        public const string Name = "IKNoise" +
#if VR
            " [VR]" +
#endif
#if DEBUG
            " [Debug]"
#endif
            ;
        public const string Version = "1.0.0";

        internal const float OneThird = (1f / 3f);
        internal const float TwoThirds = (2f / 3f);

        internal static new ManualLogSource Logger;

        public static ConfigEntry<Sex> EnableSex;
        public static ConfigEntry<Scene> EnableScene;
        public static ConfigEntry<float> TalkSceneFactor;
        public static ConfigEntry<float> HSceneFactor;
        public static ConfigEntry<float> AdvSceneFactor;
        public static readonly Dictionary<Body, ConfigType> ConfigDic = [];

        internal static Body[] enumBodyValues = Enum.GetValues(typeof(Body)) as Body[];
        private static readonly List<string> _currLoadedScenes = [];

        private void Awake()
        {
#if VR
            if (VRPlugin.PluginEnabled)
                HandleEnable();
            else
#endif
            SceneManager.sceneLoaded += TryEnable;
        }

        private void TryEnable(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
{
            if (!scene.name.Equals("Title", StringComparison.Ordinal)) return;

            SceneManager.sceneLoaded -= TryEnable;
#if VR
            if (VRPlugin.PluginEnabled) 
                HandleEnable();
            else
                Destroy(this);
#else
            var enable = false;

            var type = AccessTools.TypeByName("KK_VR.VRPlugin");
            if (type == null)
            {
                enable = true;
            }
            else
            {
                var prop = type.GetProperty("PluginEnabled", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                if (prop == null || prop.PropertyType != typeof(bool) || !(bool)prop.GetValue(null, null))
                {
                    enable = true;
                }
            }

            if (enable)
                HandleEnable();
            else
                Destroy(this);
#endif
        }

        private void HandleEnable()
        {
            Logger = base.Logger;

            CharacterApi.RegisterExtraBehaviour<IKNoiseCharaController>(GUID);

            BindConfig();

            Config.SettingChanged += (_, _1) => IKNoiseCharaController.OnSettingChanged();

            IKNoiseHooks.TryEnable();

            LateSwordAiming.Enable();

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        internal static bool IsSceneLoaded(string name) => _currLoadedScenes.Contains(name);
        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode) => _currLoadedScenes.Add(scene.name);
        private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene) => _currLoadedScenes.Remove(scene.name);

        private void BindConfig()
        {
            EnableSex = Config.Bind("", "Enable", Sex.Female,
                new ConfigDescription("Master state of the plugin.", null, new ConfigurationManagerAttributes { Order = 11000 }));

            EnableScene = Config.Bind("", "Enable Scene", Scene.Talk | Scene.HScene,
                new ConfigDescription("Scenes to run the effect.", null, new ConfigurationManagerAttributes { Order = 10900 }));


            AdvSceneFactor = Config.Bind("", "AdvSceneFactor", TwoThirds,
                new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 10800 }));

            TalkSceneFactor = Config.Bind("", "TalkSceneFactor", TwoThirds,
                new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 10700 }));

            HSceneFactor = Config.Bind("", "HSceneFactor", 1f,
                new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 10600 }));


            ConfigDic.Add(Body.Spine,
                new ConfigType(
                    body: Body.Spine,
                    config: Config,
                    order: 10000,

                    baseFreq: 0.25f,
                    freq: 0.25f,
                    freqSclRatio: 0.5f,

                    baseAmpl: 0.025f,
                    ampl: 0.025f,
                    amplSclRatio: 0.5f
                ));

            ConfigDic.Add(Body.Shoulders,
                new ConfigType(
                    body: Body.Shoulders,
                    config: Config,
                    order: 9000,

                    baseFreq: 0.25f,
                    freq: 0.25f,
                    freqSclRatio: 0.5f,

                    baseAmpl: 0.025f,
                    ampl: 0.025f,
                    amplSclRatio: 0.5f
                ));

            ConfigDic.Add(Body.Thighs,
                new ConfigType(
                    body: Body.Thighs,
                    config: Config,
                    order: 8000,

                    baseFreq: 0.25f,
                    freq: 0.25f,
                    freqSclRatio: 0.5f,

                    baseAmpl: 0.025f,
                    ampl: 0.025f,
                    amplSclRatio: 0.5f
                ));
#if VR
            ConfigDic.Add(Body.Arms,
                new ConfigType(
                    body: Body.Arms,
                    config: Config,
                    order: 7000,

                    baseFreq: 0.25f,
                    freq: 0.25f,
                    freqSclRatio: 0.5f,

                    baseAmpl: 0.015f,
                    ampl: 0.015f,
                    amplSclRatio: 0.5f
                ));

            ConfigDic.Add(Body.Legs,
                new ConfigType(
                    body: Body.Legs,
                    config: Config,
                    order: 6000,

                    baseFreq: 0.25f,
                    freq: 0.25f,
                    freqSclRatio: 0.5f,

                    baseAmpl: 0.025f,
                    ampl: 0.025f,
                    amplSclRatio: 0.5f
                ));
#endif
        }
    }

    [Flags]
    public enum Scene
    {
        None   = 0,
        Adv    = 1 << 0,
        Talk   = 1 << 1,
        HScene = 1 << 2,
    }

    [Flags]
    public enum Sex
    {
        None   = 0,
        Male   = 1 << 0,
        Female = 1 << 1,
    }

    public enum Body
    {
        Spine,
        Shoulders,
        Thighs,
#if VR
        Arms,
        Legs
#endif
    }

    public class ConfigType
    {
        public ConfigType(
            Body body,
            ConfigFile config,
            int order,
            
            float baseFreq,
            float freq,
            float freqSclRatio,

            float baseAmpl,
            float ampl,
            float amplSclRatio


            )
        {
            var name = body.ToString();


            BaseFreq = config.Bind(name, "Frequency Base", baseFreq,
                new ConfigDescription(
                    "Minimal frequency value, can't go lower then this value, doesn't scale.",
                    new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = order, ShowRangeAsPercent = false }));

            Freq = config.Bind(name, "Frequency Variable", freq,
                new ConfigDescription(
                    "Additional frequency value, added on top of base value, scales with animation and/or velocity.",
                    new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = order - 10, ShowRangeAsPercent = false }));

            FreqSclRatio = config.Bind(name, "Frequency Ratio", freqSclRatio,
                new ConfigDescription(
                    "Ratio to scale additional frequency value with animation(0) or velocity(1), can use blend of both.",
                    new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = order - 20, ShowRangeAsPercent = false }));


            BaseAmpl = config.Bind(name, "Amplitude Base", baseAmpl,
                new ConfigDescription(
                    "Minimal amplitude value, can't go lower then this value, doesn't scale.\n",
                    new AcceptableValueRange<float>(0f, 0.1f), new ConfigurationManagerAttributes { Order = order - 30, ShowRangeAsPercent = false }));

            Ampl = config.Bind(name, "Amplitude Variable", ampl,
                new ConfigDescription(
                    "Additional amplitude value, added on top of base value, scales with animation and/or velocity.",
                    new AcceptableValueRange<float>(0f, 0.1f), new ConfigurationManagerAttributes { Order = order - 40, ShowRangeAsPercent = false }));

            AmplSclRatio = config.Bind(name, "Amplitude Ratio", amplSclRatio,
                new ConfigDescription(
                    "Ratio to scale additional amplitude value with animation(0) or velocity(1), can use blend of both.",
                    new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = order - 50, ShowRangeAsPercent = false }));
        }
        
        public ConfigEntry<float> BaseFreq;
        public ConfigEntry<float> Freq;
        public ConfigEntry<float> FreqSclRatio;


        public ConfigEntry<float> BaseAmpl;
        public ConfigEntry<float> Ampl;
        public ConfigEntry<float> AmplSclRatio;
    }
}
