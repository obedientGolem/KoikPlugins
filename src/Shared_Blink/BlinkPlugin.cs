using ADV.Commands.Base;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using KKAPI.Chara;
using KKAPI.Utilities;
using RootMotion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Studio.AnimeGroupList;
using Random = UnityEngine.Random;

namespace Blink
{
    [BepInPlugin(GUID, Name, Version)]

    [BepInProcess(KKAPI.KoikatuAPI.GameProcessName)]
#if KK
    [BepInProcess(KKAPI.KoikatuAPI.GameProcessNameSteam)]
#endif
    [BepInProcess(KKAPI.KoikatuAPI.StudioProcessName)]

    public class BlinkPlugin : BaseUnityPlugin
    {
        public const string GUID = "koik.blink";
        public const string Name = "Blink";
        public const string Version = "1.0.0";

        internal static BlinkPlugin Instance;

        public static ConfigEntry<bool> Enable { get; set; }

        public static ConfigEntry<float> EyebrowMovement { get; set; }

        public static ConfigEntry<bool> Randomize { get; set; }

        public static new ManualLogSource Logger;

        private void Start()
        {
            Instance = this;

            Logger = base.Logger;


            Enable = Config.Bind("", "Enable", true,
                new ConfigDescription("Changes take place immediately.",
                null,
                new ConfigurationManagerAttributes { Order = 100 }));


            Randomize = Config.Bind("", "Randomize", true,
                new ConfigDescription("Add randomization to the length of the blink.",
                null,
                new ConfigurationManagerAttributes { Order = 90}));


            EyebrowMovement = Config.Bind("", "BrowToEyeMovementRatio", 0.2f,
                new ConfigDescription("In-game default = 1, i.e. the brow moves distance ~equivalent to that of the upper eyelid during the blink.",
                new AcceptableValueRange<float>(0f, 1f),
                new ConfigurationManagerAttributes { Order = 80, ShowRangeAsPercent = false }));


            CharacterApi.RegisterExtraBehaviour<BlinkCharaController>(GUID);

            BlinkGui.Register();

            BlinkHooks.TryEnable(Enable.Value);

            Config.SettingChanged += OnSettingChanged;
        }


        private void OnSettingChanged(object sender, EventArgs e)
        {
            var enable = Enable.Value;

            BlinkHooks.TryEnable(enable);
            BlinkCharaController.TryEnable(enable);
        }

        
    }
}