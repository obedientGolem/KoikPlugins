using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using KKAPI.Chara;
using KKAPI.Utilities;
using Koik_LateSwordAiming;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace IKNoise
{
    [BepInPlugin(GUID, Name, Version)]
    internal class IKNoisePlugin : BaseUnityPlugin
    {
        public const string GUID = "koik.iknoise";
        public const string Name = "IKNoise" +
#if DEBUG
            " Debug"
#endif
            ;
        public const string Version = "1.0.0";

        internal static new ManualLogSource Logger;

        public static ConfigEntry<Sex> Enable;
        public static readonly Dictionary<Body, ConfigType> ConfigDic = [];


        private void Start()
        {
            Logger = base.Logger;

            CharacterApi.RegisterExtraBehaviour<IKNoiseCharaController>(GUID);

            BindConfig();

            Config.SettingChanged += (_, _1) => IKNoiseCharaController.OnSettingChanged();

            LateSwordAiming.Enable();
        }

        private void BindConfig()
        {
            Enable = Config.Bind(name, "Enable", Sex.Female,
                new ConfigDescription("Master state of the plugin.", null, new ConfigurationManagerAttributes { Order = 11000 }));

            ConfigDic.Add(Body.Shoulders,
                new ConfigType(
                    body: Body.Shoulders,
                    config: Config,
                    order: 10000,
                    enableScene: Scene.Game,
                    enableSex: Sex.Female,

                    baseFreq: 0.25f,
                    freq: 2f,
                    freqSclRatio: 0.5f,

                    baseAmpl: 0.025f,
                    ampl: 2f,
                    amplSclRatio: 0.5f
                ));

            ConfigDic.Add(Body.Core,
                new ConfigType(
                    body: Body.Core,
                    config: Config,
                    order: 9000,
                    enableScene: Scene.Game,
                    enableSex: Sex.Female,

                    baseFreq: 0.25f,
                    freq: 2f,
                    freqSclRatio: 0.5f,

                    baseAmpl: 0.025f,
                    ampl: 2f,
                    amplSclRatio: 0.5f
                ));

            ConfigDic.Add(Body.Waist,
                new ConfigType(
                    body: Body.Waist,
                    config: Config,
                    order: 8000,
                    enableScene: Scene.Game,
                    enableSex: Sex.Female,

                    baseFreq: 0.25f,
                    freq: 2f,
                    freqSclRatio: 0.5f,

                    baseAmpl: 0.025f,
                    ampl: 2f,
                    amplSclRatio: 0.5f
                ));
        }
    }

    [Flags]
    public enum Scene
    {
        None = 0,
        Maker = 1 << 0,
        Game = 1 << 1,
        //Studio,
    }

    [Flags]
    public enum Sex
    {
        None = 0,
        Male = 1 << 0,
        Female = 1 << 1,
    }
    public enum Body
    {
        Shoulders,
        Core,
        Waist,
    }

    public class ConfigType
    {
        public ConfigType(
            Body body,
            ConfigFile config,
            int order,

            Scene enableScene,
            Sex enableSex,
            
            float baseFreq,
            float freq,
            float freqSclRatio,

            float baseAmpl,
            float ampl,
            float amplSclRatio


            )
        {
            var name = body.ToString();

            EnableScene = config.Bind(name, "Enable Scene", enableScene,
                new ConfigDescription("Run effect in scenes.", null, new ConfigurationManagerAttributes { Order = order }));

            EnableSex = config.Bind(name, "Enable Sex", enableSex,
                new ConfigDescription("Run effect for genders.", null, new ConfigurationManagerAttributes { Order = order - 10 }));



            BaseFreq = config.Bind(name, "Base Frequency", baseFreq,
                new ConfigDescription(
                    "Minimal frequency value, can't go lower then this value, doesn't scale.",
                    new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = order - 20, ShowRangeAsPercent = false }));

            Freq = config.Bind(name, "Frequency", freq,
                new ConfigDescription(
                    "Additional frequency value, added on top of base value, scales with animation and/or velocity.", 
                    null, new ConfigurationManagerAttributes { Order = order - 30 }));

            FreqSclRatio = config.Bind(name, "Frequency Scale Ratio", freqSclRatio,
                new ConfigDescription(
                    "Ratio to scale additional frequency value with animation(0) or velocity(1), can use blend of both.",
                    new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = order - 40, ShowRangeAsPercent = false }));



            BaseAmpl = config.Bind(name, "Base Amplitude", baseAmpl,
                new ConfigDescription(
                    "Minimal amplitude value, can't go lower then this value, doesn't scale.\n",
                    new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = order - 50, ShowRangeAsPercent = false }));

            Ampl = config.Bind(name, "Amplitude", ampl,
                new ConfigDescription(
                    "Additional amplitude value, added on top of base value, scales with animation and/or velocity.",
                    null, new ConfigurationManagerAttributes { Order = order - 60 }));

            AmplSclRatio = config.Bind(name, "Amplitude Scale Ratio", amplSclRatio,
                new ConfigDescription(
                    "Ratio to scale additional amplitude value with animation(0) or velocity(1), can use blend of both.",
                    new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = order - 70, ShowRangeAsPercent = false }));


        }

        public ConfigEntry<Scene> EnableScene;
        public ConfigEntry<Sex> EnableSex;

        public ConfigEntry<float> BaseFreq;
        public ConfigEntry<float> Freq;
        public ConfigEntry<float> FreqSclRatio;


        public ConfigEntry<float> BaseAmpl;
        public ConfigEntry<float> Ampl;
        public ConfigEntry<float> AmplSclRatio;
    }
}
