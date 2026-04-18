using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using KKABMX.Core;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static AniMorph.MotionModifier;

namespace AniMorph
{
    [BepInPlugin(GUID, Name, Version)]
    [BepInProcess(KoikatuAPI.GameProcessName)]
    [BepInProcess(KoikatuAPI.StudioProcessName)]
    [BepInDependency(KKABMX_Core.GUID, KKABMX_Core.Version)]

#if KK
    [BepInProcess(KoikatuAPI.GameProcessNameSteam)]
#endif
    internal class AniMorphPlugin : BaseUnityPlugin
    {
        public const string GUID = "koik.anisotropicmorph";
        public const string Name = "Anisotropic Morph"
#if DEBUG
            + " [DEBUG]"
#endif
            ;
        public const string Version = "0.9";
        internal new static ManualLogSource Logger;

        public static ConfigEntry<Gender> Enable;
        public static ConfigEntry<bool> MaleEnableDB;
        public static ConfigEntry<FilterDeltaTimeKind> FilterDeltaTime;


        public static readonly Dictionary<Body, ConfigType> ConfigDic = [];

        private static readonly Effect[] _effects = Enum.GetValues(typeof(Effect)) as Effect[];
        private float _settingChangedTimestamp;

        private void Awake()
        {
            Logger = base.Logger;

            Enable = Config.Bind("", "Enable", Gender.Male | Gender.Female, new ConfigDescription("Choose none to disable", null, new ConfigurationManagerAttributes { Order = 100 }));


            CharacterApi.RegisterExtraBehaviour<AniMorphCharaController>(GUID);

            BindConfig();

            var handler = new EventHandler(OnSettingChanged);

            foreach (Body eVal in Enum.GetValues(typeof(Body)))
            {
                if (ConfigDic.TryGetValue(eVal, out var value))
                    AddSettingChangedParam(value, handler);
            }


            MaleEnableDB = Config.Bind("", "MaleEnableDB", true, new ConfigDescription("Force enable Dynamic Bones on males in Main Game as they are usually turned off", null, new ConfigurationManagerAttributes { Order = 99 }));
            FilterDeltaTime = Config.Bind("", "FilterFPS", FilterDeltaTimeKind.OnlyInGame, new ConfigDescription("Filter lags and stutters of the frame rate to avoid visual glitches", null, new ConfigurationManagerAttributes { Order = 98 }));

            MaleEnableDB.SettingChanged += (_, _) => HooksMaleEnableDB.ApplyHooks();
            FilterDeltaTime.SettingChanged += OnSettingChanged;
            // Avoid hooking this one up for UpdateConfig().

            Hooks.ApplyHooks();
            HooksMaleEnableDB.ApplyHooks();
        }

        private void Update()
        {
            if (_settingChangedTimestamp != 0f && (Time.time - _settingChangedTimestamp) > 0.2f)
            {
                _settingChangedTimestamp = 0f;
                AniMorphCharaController.OnSettingChanged();
            }
        }

        private void BindConfig()
        {
            #region Breast

            ConfigDic.Add(Body.Breast,
                new(
                    body: Body.Breast,
                    config: Config,
                    order: 10000,
                    effect: Effect.Pos | Effect.Rot | Effect.Tether | Effect.Scl | Effect.PosOffset | Effect.RotOffset | Effect.SclOffset,
                    adjustForSize: true,
                    disableWhenClothes: ClothesKind.Top | ClothesKind.Bra,

                    noiseOctaves: 4,
                    noiseAmplitudePos: 0.15f,
                    noiseAmplitudeRot: 0.67f,
                    noiseAmplitudeScl: 0.15f,


                    posSpring: 21f,
                    posDamping: 0.2f,
                    posShockStr: 1f,
                    posShockThreshold: 0.15f,
                    posFreezeThreshold: 0.25f,
                    posFreezeLen: 0.05f,
                    posBleedStr: 5f,
                    posBleedLen: 0.1f,
                    //posGravity: 0f,

                    rotSpring: 15f,
                    rotDamping: 0.2f,
                    rotRate: 2f,
                    //AngularApplicationMaster: Axis.Z,
                    //AngularApplicationSlave: Axis.X | Axis.Y,

                    sclStr: 0.35f,
                    sclRate: 8f,
                    sclDistortion: 0.4f,
                    sclPreserveVolume: true,

                    tetherMultiplier: 2f,
                    tetherFrequency: 2f,
                    tetherDamping: 0.3f,
                    tetherMaxDeg: 30,

                    rotOffsetRollDeg: 20,
                    rotOffsetRollFaceUpFactor: 0.2f,

                    posOffsetPitchFaceDown: 0.0175f,
                    posOffsetPitchUpsideDown: 0.05f,
                    posOffsetRoll: new Vector3(0.0175f, -0.02f, 0f),

                    sclOffsetFaceUp: -0.15f,
                    sclOffsetFaceUpPerpAxesFactor: 0.85f,
                    sclOffsetFaceDown: 0.2f,
                    sclOffsetFaceDownPerpAxesFactor: 1.4f
                    )
                );

            #endregion

            #region Pelvis

            ConfigDic.Add(Body.Pelvis,
                new(
                    body: Body.Pelvis,
                    config: Config,
                    order: 9000,
                    effect: Effect.Pos | Effect.Rot | Effect.Scl,
                    adjustForSize: true,
                    disableWhenClothes: ClothesKind.Panty,

                    noiseOctaves: 4,
                    noiseAmplitudePos: 0.15f,
                    noiseAmplitudeRot: 0.67f,
                    noiseAmplitudeScl: 0.15f,


                    posSpring: 21f,
                    posDamping: 0.5f,
                    posShockStr: 1f,
                    posShockThreshold: 0.15f,
                    posFreezeThreshold: 0.25f,
                    posFreezeLen: 0.05f,
                    posBleedStr: 5f,
                    posBleedLen: 0.1f,
                    //posGravity: 0f,
                    //LinearLimitPositive: new Vector3(1f, 1.33f, 1f),
                    //LinearLimitNegative: new Vector3(1f, 0.67f, 1f),

                    rotSpring: 20f,
                    rotDamping: 0.5f,
                    rotRate: 3f,
                    //AngularApplicationMaster: (Axis)0,
                    //AngularApplicationSlave: Axis.X | Axis.Y | Axis.Z,

                    sclStr: 30f,
                    sclRate: 10f,
                    sclDistortion: 0.4f,
                    sclPreserveVolume: true,

                    tetherMultiplier: -3f,
                    tetherFrequency: 3f,
                    tetherDamping: 0.3f,
                    tetherMaxDeg: 30,

                    rotOffsetRollDeg: 20,
                    rotOffsetRollFaceUpFactor: 0.15f,

                    posOffsetPitchFaceDown: 0.0175f,
                    posOffsetPitchUpsideDown: 0.05f,
                    posOffsetRoll: new Vector3(0.0175f, -0.02f, 0f),

                    sclOffsetFaceUp: -0.075f,
                    sclOffsetFaceUpPerpAxesFactor: 1f,
                    sclOffsetFaceDown: 0.2f,
                    sclOffsetFaceDownPerpAxesFactor: 1f
                    )
                );

            #endregion

            #region Thighs

            ConfigDic.Add(Body.Thighs,
                new(
                    body: Body.Thighs,
                    config: Config,
                    order: 8000,
                    effect: Effect.Pos,
                    adjustForSize: true,
                    disableWhenClothes: ClothesKind.None,

                    noiseOctaves: 4,
                    noiseAmplitudePos: 0.125f,
                    noiseAmplitudeRot: 0.125f,
                    noiseAmplitudeScl: 0f,


                    posSpring: 30f,
                    posDamping: 0.2f,
                    posShockStr: 1f,
                    posShockThreshold: 0.15f,
                    posFreezeThreshold: 0.25f,
                    posFreezeLen: 0.02f,
                    posBleedStr: 2f,
                    posBleedLen: 0.1f,

                    rotSpring: null,
                    rotDamping: null,
                    rotRate: null,

                    sclStr: 40f,
                    sclRate: 8f,
                    sclDistortion: 0.5f,
                    sclPreserveVolume: true,

                    tetherMultiplier: null,
                    tetherFrequency: null,
                    tetherDamping: null,
                    tetherMaxDeg: null,

                    rotOffsetRollDeg: 15,
                    rotOffsetRollFaceUpFactor: 0.5f,

                    posOffsetPitchFaceDown: 0.0175f,
                    posOffsetPitchUpsideDown: 0.05f,
                    posOffsetRoll: new Vector3(0.035f, 0f, 0f),

                    sclOffsetFaceUp: 0.2f,
                    sclOffsetFaceUpPerpAxesFactor: 1f,
                    sclOffsetFaceDown: 0.2f,
                    sclOffsetFaceDownPerpAxesFactor: 1f
                    )
                );

            #endregion

            #region Chest

            ConfigDic.Add(Body.Chest,
                new(
                    body: Body.Chest,
                    config: Config,
                    order: 7000,
                    effect: Effect.Pos,
                    adjustForSize: true,
                    disableWhenClothes: ClothesKind.None,

                    noiseOctaves: 4,
                    noiseAmplitudePos: 0.15f,
                    noiseAmplitudeRot: 0.67f,
                    noiseAmplitudeScl: 0.15f,


                    posSpring: 21f,
                    posDamping: 0.2f,
                    posShockStr: 1f,
                    posShockThreshold: 0.15f,
                    posFreezeThreshold: 0.25f,
                    posFreezeLen: 0.05f,
                    posBleedStr: 5f,
                    posBleedLen: 0.1f,
                    //posGravity: 0f,

                    rotSpring: 15f,
                    rotDamping: 0.2f,
                    rotRate: 2f,
                    //AngularApplicationMaster: Axis.Z,
                    //AngularApplicationSlave: Axis.X | Axis.Y,

                    sclStr: 0.35f,
                    sclRate: 8f,
                    sclDistortion: 0.4f,
                    sclPreserveVolume: true,

                    tetherMultiplier: 2f,
                    tetherFrequency: 2f,
                    tetherDamping: 0.3f,
                    tetherMaxDeg: 30,

                    rotOffsetRollDeg: 20,
                    rotOffsetRollFaceUpFactor: 3,

                    posOffsetPitchFaceDown: 0.0175f,
                    posOffsetPitchUpsideDown: 0.05f,
                    posOffsetRoll: new Vector3(0.0175f, -0.02f, 0f),

                    sclOffsetFaceUp: -0.15f,
                    sclOffsetFaceUpPerpAxesFactor: 1f,
                    sclOffsetFaceDown: 0.2f,
                    sclOffsetFaceDownPerpAxesFactor: 1f
                    )
                );

            #endregion

            #region Shoulders

            ConfigDic.Add(Body.Shoulders,
                new(
                    body: Body.Shoulders,
                    config: Config,
                    order: 6000,
                    effect: Effect.Pos,
                    adjustForSize: true,
                    disableWhenClothes: ClothesKind.None,

                    noiseOctaves: 4,
                    noiseAmplitudePos: 0.15f,
                    noiseAmplitudeRot: 0.67f,
                    noiseAmplitudeScl: 0.15f,


                    posSpring: 21f,
                    posDamping: 0.2f,
                    posShockStr: 1f,
                    posShockThreshold: 0.15f,
                    posFreezeThreshold: 0.25f,
                    posFreezeLen: 0.05f,
                    posBleedStr: 5f,
                    posBleedLen: 0.1f,
                    //posGravity: 0f,

                    rotSpring: 15f,
                    rotDamping: 0.2f,
                    rotRate: 2f,
                    //AngularApplicationMaster: Axis.Z,
                    //AngularApplicationSlave: Axis.X | Axis.Y,

                    sclStr: 0.35f,
                    sclRate: 8f,
                    sclDistortion: 0.4f,
                    sclPreserveVolume: true,

                    tetherMultiplier: 2f,
                    tetherFrequency: 2f,
                    tetherDamping: 0.3f,
                    tetherMaxDeg: 30,

                    rotOffsetRollDeg: 20,
                    rotOffsetRollFaceUpFactor: 3,

                    posOffsetPitchFaceDown: 0.0175f,
                    posOffsetPitchUpsideDown: 0.05f,
                    posOffsetRoll: new Vector3(0.0175f, -0.02f, 0f),

                    sclOffsetFaceUp: -0.15f,
                    sclOffsetFaceUpPerpAxesFactor: 1f,
                    sclOffsetFaceDown: 0.2f,
                    sclOffsetFaceDownPerpAxesFactor: 1f
                    )
                );

            #endregion

            #region Tummy

            ConfigDic.Add(Body.Tummy,
                new(
                    body: Body.Tummy,
                    config: Config,
                    order: 5000,
                    effect: Effect.Pos | Effect.Rot,
                    adjustForSize: true,
                    disableWhenClothes: ClothesKind.None,

                    noiseOctaves: 4,
                    noiseAmplitudePos: 0.45f,
                    noiseAmplitudeRot: 5f,
                    noiseAmplitudeScl: 0.15f,


                    posSpring: 21f,
                    posDamping: 0.5f,
                    posShockStr: 1f,
                    posShockThreshold: 0.15f,
                    posFreezeThreshold: 0.25f,
                    posFreezeLen: 0.05f,
                    posBleedStr: 5f,
                    posBleedLen: 0.1f,
                    //posGravity: 0f,

                    rotSpring: 15f,
                    rotDamping: 0.2f,
                    rotRate: 2f,
                    //AngularApplicationMaster: Axis.Z,
                    //AngularApplicationSlave: Axis.X | Axis.Y,

                    sclStr: 0.35f,
                    sclRate: 8f,
                    sclDistortion: 0.4f,
                    sclPreserveVolume: true,

                    tetherMultiplier: 2f,
                    tetherFrequency: 2f,
                    tetherDamping: 0.3f,
                    tetherMaxDeg: 30,

                    rotOffsetRollDeg: 20,
                    rotOffsetRollFaceUpFactor: 3,

                    posOffsetPitchFaceDown: 0.0175f,
                    posOffsetPitchUpsideDown: 0.05f,
                    posOffsetRoll: new Vector3(0.0175f, -0.02f, 0f),

                    sclOffsetFaceUp: -0.15f,
                    sclOffsetFaceUpPerpAxesFactor: 1f,
                    sclOffsetFaceDown: 0.2f,
                    sclOffsetFaceDownPerpAxesFactor: 1f
                    )
                );

            #endregion

            #region Head

            ConfigDic.Add(Body.Head,
                new(
                    body: Body.Head,
                    config: Config,
                    order: 3000,
                    effect: Effect.Pos,
                    adjustForSize: true,
                    disableWhenClothes: ClothesKind.None,

                    noiseOctaves: 4,
                    noiseAmplitudePos: 0.075f,
                    noiseAmplitudeRot: 0.67f,
                    noiseAmplitudeScl: 0.15f,


                    posSpring: 30f,
                    posDamping: 0.2f,
                    posShockStr: 1f,
                    posShockThreshold: 0.15f,
                    posFreezeThreshold: 0.25f,
                    posFreezeLen: 0.02f,
                    posBleedStr: 2f,
                    posBleedLen: 0.1f,

                    rotSpring: 30f,
                    rotDamping: 5f,
                    rotRate: 2f,

                    sclStr: 40f,
                    sclRate: 8f,
                    sclDistortion: 0.5f,
                    sclPreserveVolume: true,

                    tetherMultiplier: 0.5f,
                    tetherFrequency: 3f,
                    tetherDamping: 0.3f,
                    tetherMaxDeg: 30,

                    rotOffsetRollDeg: null,
                    rotOffsetRollFaceUpFactor: null,

                    posOffsetPitchFaceDown: null,
                    posOffsetPitchUpsideDown: null,
                    posOffsetRoll: null,

                    sclOffsetFaceUp: null,
                    sclOffsetFaceUpPerpAxesFactor: null,
                    sclOffsetFaceDown: null,
                    sclOffsetFaceDownPerpAxesFactor: null
                    )
                );

            #endregion
        }


        private void AddSettingChangedParam(object obj, EventHandler handler)
        {
            var fields = obj.GetType()
                .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

            var configEntryType = typeof(ConfigEntry<>);

            foreach (var field in fields)
            {
                var fieldType = field.FieldType;

                if (fieldType.IsGenericType &&
                    fieldType.GetGenericTypeDefinition() == configEntryType)
                {
                    var instance = field.GetValue(obj);

                    if (instance == null)
                        continue;

                    var eventInfo = fieldType.GetEvent("SettingChanged");

                    if (eventInfo == null)
                        continue;

                    eventInfo.AddEventHandler(instance, handler);
                }
            }
        }

        private void OnSettingChanged(object sender, EventArgs e)
        {
            _settingChangedTimestamp = Time.time;

            AdjustAllowedEffects();
        }

        private void AdjustAllowedEffects()
        {
            foreach (var kv in ConfigDic)
            {
                var allowedEffects = kv.Value.allowedEffects;

                foreach (var effect in _effects)
                {
                    if ((allowedEffects & effect) == 0)
                    {
                        kv.Value.Effects.Value &= ~effect;
                    }
                }
            }
        }


        #region Types


        public enum Body
        {
            //Cheeks,
            Head,
            Shoulders,
            Chest,
            Breast,
            Tummy,
            Pelvis,
            Thighs,
        }

        public enum FilterDeltaTimeKind
        {
            Disable,
            Enable,
            OnlyInStudio,
            OnlyInGame,
        }

        [Flags]
        public enum Axis
        {
            None = 0,
            X = 1 << 0,
            Y = 1 << 1,
            Z = 1 << 2,
        }

        [Flags]
        public enum Gender
        {
            None = 0,
            Male = 1 << 0,
            Female = 1 << 1,
        }
        public static readonly ClothesKind[] ClothesKindValues = Enum.GetValues(typeof(ClothesKind)) as ClothesKind[];

        [Flags]
        public enum ClothesKind
        {
            None = 0,
            Top = 1 << 0,
            Bot = 1 << 1,
            Bra = 1 << 2,
            Panty = 1 << 3,
            Gloves = 1 << 4,
            Pantyhose = 1 << 5,
            Socks = 1 << 6,
#if KK
            ShoesIn = 1 << 7,
            ShoesOut = 1 << 8,
#elif KKS
            Shoes = 1 << 7,
#endif
        }

        public class ConfigType
        {
            public ConfigType(
                Body body,
                ConfigFile config,
                int order,

                Effect effect,
                bool adjustForSize,
                ClothesKind disableWhenClothes,

                int noiseOctaves,
                float noiseAmplitudePos,
                float noiseAmplitudeRot,
                float noiseAmplitudeScl,

                float posSpring,
                float posDamping,
                float posShockStr,
                float posShockThreshold,
                float posFreezeThreshold,
                float posFreezeLen,
                float posBleedStr,
                float posBleedLen,

                float? rotSpring,
                float? rotDamping,
                float? rotRate,

                float? sclStr,
                float? sclRate,
                float? sclDistortion,
                bool? sclPreserveVolume,

                float? tetherMultiplier,
                float? tetherFrequency,
                float? tetherDamping,
                int? tetherMaxDeg,

                int? rotOffsetRollDeg,
                float? rotOffsetRollFaceUpFactor,

                float? posOffsetPitchFaceDown,
                float? posOffsetPitchUpsideDown,
                Vector3? posOffsetRoll,

                float? sclOffsetFaceUp,
                float? sclOffsetFaceUpPerpAxesFactor,
                float? sclOffsetFaceDown,
                float? sclOffsetFaceDownPerpAxesFactor

                )
            {
                var name = body.ToString();

                Effects = config.Bind(name, "Effects",
                effect,
                new ConfigDescription("Select effects to apply to the breast (bone)\nOnly appropriate effects can be selected\n" +
                "Linear – position of the bone is adjusted as if the bone was attached by an elastic band\n" +
                "Angular – rotation of the bone is adjusted as if the bone was a rod with an elastic attachment point, uses rotation of the bone\n" +
                "Tethering – rotation of the bone is adjusted as if the bone was a rod with an elastic attachment point, uses 'Linear' effect, synergies with 'Angular' effect\n" +
                "Acceleration – scale of the bone is extended along the axis of velocity and shrunk along perpendicular ones\n" +
                "Deceleration – scale of the bone is shrunk along the axis of deceleration and extended along perpendicular ones\n" +
                "gravityLinear – position of the bone is adjusted based on the rotation of the bone in the world space as imitation of the gravity\n" +
                "gravityAngular – rotation of the bone is adjusted based on the rotation of the bone in the world space as imitation of the gravity\n" +
                "gravityScale – scale of the bone is adjusted based on the rotation of the bone in the world space as imitation of the gravity",
                null, new ConfigurationManagerAttributes { Order = order }));

                DisableWhenClothes = config.Bind(name, "DisableClothed", disableWhenClothes,
                    new ConfigDescription("Don't apply effects when particular piece of clothing is fully present", null, new ConfigurationManagerAttributes { Order = order - 5 }));

                AdjustForSize = config.Bind(name, "AdjustForSize", adjustForSize,
                    new ConfigDescription("Adjust effects for the breast size\nUpdates after the scene change", null, new ConfigurationManagerAttributes { Order = order - 6 }));

                // ExtraChaos Rot 
                // RotAxes
                // RotFactor
                //ExtraChaos = config.Bind(name, "AdjustForSize", adjustForSize,
                //    new ConfigDescription("Adjust effects for the breast size\nUpdates after the scene change", null, new ConfigurationManagerAttributes { Order = order - 6 }));


                var isRotation = rotSpring != null && rotDamping != null && rotRate != null;
                var isScl = sclStr != null && sclDistortion != null && sclRate != null && sclPreserveVolume != null;
                var isTether = tetherMultiplier != null && tetherFrequency != null && tetherDamping != null && tetherMaxDeg != null;
                var isPosOffset = posOffsetPitchFaceDown != null && posOffsetPitchUpsideDown != null && posOffsetRoll != null;
                var isRotOffset = rotOffsetRollDeg != null && rotOffsetRollFaceUpFactor != null;
                var isSclOffset = sclOffsetFaceUp != null && sclOffsetFaceUpPerpAxesFactor != null && sclOffsetFaceDown != null && sclOffsetFaceDownPerpAxesFactor != null;

                NoiseOctaves = config.Bind(name, "NoiseOctaves", noiseOctaves,
                    new ConfigDescription("", new AcceptableValueRange<int>(1, 4), new ConfigurationManagerAttributes { Order = order - 7 }));

                NoiseAmplitudePos = config.Bind(name, "NoiseAmplitudePos", noiseAmplitudePos,
                    new ConfigDescription("", new AcceptableValueRange<float>(0f, 5f), new ConfigurationManagerAttributes { Order = order - 12, ShowRangeAsPercent = false }));

                NoiseAmplitudeRot = config.Bind(name, "NoiseAmplitudeRot", noiseAmplitudeRot,
                    new ConfigDescription("", new AcceptableValueRange<float>(0f, 5f), new ConfigurationManagerAttributes { Order = order - 13, ShowRangeAsPercent = false }));

                NoiseAmplitudeScl = config.Bind(name, "NoiseAmplitudeScl", noiseAmplitudeScl,
                    new ConfigDescription("", new AcceptableValueRange<float>(0f, 5f), new ConfigurationManagerAttributes { Order = order - 14, ShowRangeAsPercent = false }));



                PosSpring = config.Bind(name, "Position Spring", posSpring,
                    new ConfigDescription("Strength of the positional lag.",
                    new AcceptableValueRange<float>(0f, 100f), new ConfigurationManagerAttributes { Order = order - 15, ShowRangeAsPercent = false }));

                PosDamping = config.Bind(name, "Position Damping", posDamping,
                    new ConfigDescription("Strength of negation of the positional lag.", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = order - 20, ShowRangeAsPercent = false }));

                PosShockStr = config.Bind(name, "PosShockStr", posShockStr,
                    new ConfigDescription("Shock introduces huge velocity impacts that quickly bleed out.\n" +
                    "Warning: as koik animations can get rather fast (0.44sec for full cycle) at low fps (< 45) it might start to look ugly. Set to 0 to disable.", 
                    null, new ConfigurationManagerAttributes { Order = order - 21 }));

                PosShockThreshold = config.Bind(name, "PosShockThreshold", posShockThreshold,
                    new ConfigDescription("Shock allows for extreme offsets when velocities change drastically. Set 0 to disable.", null, new ConfigurationManagerAttributes { Order = order - 22 }));

                PosFreezeThreshold = config.Bind(name, "PosFreezeThreshold", posFreezeThreshold,
                    new ConfigDescription("", new AcceptableValueRange<float>(0f, 100f), new ConfigurationManagerAttributes { Order = order - 23, ShowRangeAsPercent = false }));

                PosFreezeLen = config.Bind(name, "PosFreezeLen", posFreezeLen,
                    new ConfigDescription("", new AcceptableValueRange<float>(0f, 100f), new ConfigurationManagerAttributes { Order = order - 24, ShowRangeAsPercent = false }));

                // (2-3 .. 8-12)
                PosBleedStr = config.Bind(name, "PosBleedStr", posBleedStr,
                    new ConfigDescription("", new AcceptableValueRange<float>(0f, 15f), new ConfigurationManagerAttributes { Order = order - 25, ShowRangeAsPercent = false }));

                PosBleedLen = config.Bind(name, "PosBleedLen", posBleedLen,
                    new ConfigDescription("", new AcceptableValueRange<float>(0f, 100f), new ConfigurationManagerAttributes { Order = order - 26, ShowRangeAsPercent = false }));


                if (isRotation)
                {
                    RotSpring = config.Bind(name, "Rotation Spring", (float)rotSpring,
                        new ConfigDescription("Strength of the rotational lag.",
                        new AcceptableValueRange<float>(0f, 100f), new ConfigurationManagerAttributes { Order = order - 40, ShowRangeAsPercent = false }));

                    RotDamping = config.Bind(name, "Rotation Damping", (float)rotDamping,
                        new ConfigDescription("Strength of negation of the rotational lag.",
                        new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = order - 45, ShowRangeAsPercent = false }));

                    RotRate = config.Bind(name, "Rotation Interpolation Speed", (float)rotRate,
                        new ConfigDescription("How fast the rotation offset changes.",
                        new AcceptableValueRange<float>(0f, 10f), new ConfigurationManagerAttributes { Order = order - 50, ShowRangeAsPercent = false }));
                }


                if (isScl)
                {
                    SclStr = config.Bind(name, "Scale Strength", (float)sclStr,
                        new ConfigDescription("How much the velocity influences the scale.",
                        new AcceptableValueRange<float>(0f, 50f), new ConfigurationManagerAttributes { Order = order - 65, ShowRangeAsPercent = false }));

                    SclDistort = config.Bind(name, "Scale Distortion", (float)sclDistortion,
                        new ConfigDescription("How much the scale can change, 1 ± this value.",
                        new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = order - 70, ShowRangeAsPercent = false }));

                    SclRate = config.Bind(name, "Scale Interpolation Speed", (float)sclRate,
                        new ConfigDescription("How fast the scale offset changes.",
                        new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = order - 75, ShowRangeAsPercent = false }));

                    SclPreserveVol = config.Bind(name, "Scale Preserve Volume", (bool)sclPreserveVolume,
                        new ConfigDescription("Keep volume consistent", null, new ConfigurationManagerAttributes { Order = order - 80 }));
                }
                

                if (isTether)
                {
                    TetherFactor = config.Bind(name, "TetheringMultiplier", (float)tetherMultiplier,
                        new ConfigDescription("Strength of tethering", new AcceptableValueRange<float>(-5f, 5f),
                        new ConfigurationManagerAttributes { Order = order - 95, ShowRangeAsPercent = false }));

                    TetherFreq = config.Bind(name, "TetheringFrequency", (float)tetherFrequency,
                        new ConfigDescription("Ceiling for the amount oscillation per second",
                        null, new ConfigurationManagerAttributes { Order = order - 100 }));

                    TetherDamp = config.Bind(name, "TetheringDamping", (float)tetherDamping,
                        new ConfigDescription("Strength of negation of tethering",
                        new AcceptableValueRange<float>(0f, 2f), new ConfigurationManagerAttributes { Order = order - 105, ShowRangeAsPercent = false }));

                    TetherMaxDeg = config.Bind(name, "TetheringMaxAngle", (int)tetherMaxDeg,
                        new ConfigDescription("Tethering won't exceed this value in degrees ",
                        new AcceptableValueRange<int>(10, 60), new ConfigurationManagerAttributes { Order = order - 110 }));
                }


                if (isRotOffset)
                {
                    RotOffsetRollDeg = config.Bind(name, "RotOffsetRollDeg", (int)rotOffsetRollDeg,
                    new ConfigDescription("",
                    new AcceptableValueRange<int>(10, 30), new ConfigurationManagerAttributes { Order = order - 115, ShowRangeAsPercent = false }));

                    RotOffsetRollFaceUpFactor = config.Bind(name, "RotOffsetRollFaceUpFactor", (float)rotOffsetRollFaceUpFactor,
                        new ConfigDescription("",
                        new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = order - 120, ShowRangeAsPercent = false }));
                }


                if (isPosOffset)
                {
                    PosOffsetPitchFaceDown = config.Bind(name, "PosOffsetPitchFaceDown", (float)posOffsetPitchFaceDown,
                        new ConfigDescription("",
                        new AcceptableValueRange<float>(0f, 0.1f), new ConfigurationManagerAttributes { Order = order - 125, ShowRangeAsPercent = false }));

                    PosOffsetPitchUpsideDown = config.Bind(name, "PosOffsetPitchUpsideDown", (float)posOffsetPitchUpsideDown,
                        new ConfigDescription("",
                        new AcceptableValueRange<float>(0f, 0.1f), new ConfigurationManagerAttributes { Order = order - 130, ShowRangeAsPercent = false }));

                    PosOffsetRoll = config.Bind(name, "PosOffsetRoll", (Vector3)posOffsetRoll,
                        new ConfigDescription("",
                        null, new ConfigurationManagerAttributes { Order = order - 135, ShowRangeAsPercent = false }));
                }


                if (isSclOffset)
                {
                    SclOffsetFaceUp = config.Bind(name, "SclOffsetFaceUp", (float)sclOffsetFaceUp,
                        new ConfigDescription("",
                        new AcceptableValueRange<float>(-1f, 1f), new ConfigurationManagerAttributes { Order = order - 140, ShowRangeAsPercent = false }));

                    SclOffsetFaceUpPerpAxesFactor = config.Bind(name, "SclOffsetFaceUpPerpAxesFactor", (float)sclOffsetFaceUpPerpAxesFactor,
                        new ConfigDescription("",
                        new AcceptableValueRange<float>(0f, 3f), new ConfigurationManagerAttributes { Order = order - 145, ShowRangeAsPercent = false }));

                    SclOffsetFaceDown = config.Bind(name, "SclOffsetFaceDown", (float)sclOffsetFaceDown,
                        new ConfigDescription("",
                        new AcceptableValueRange<float>(-1f, 1f), new ConfigurationManagerAttributes { Order = order - 150, ShowRangeAsPercent = false }));

                    SclOffsetFaceDownPerpAxesFactor = config.Bind(name, "SclOffsetFaceDownPerpAxesFactor", (float)sclOffsetFaceDownPerpAxesFactor,
                        new ConfigDescription("",
                        new AcceptableValueRange<float>(0f, 3f), new ConfigurationManagerAttributes { Order = order - 155, ShowRangeAsPercent = false }));
                }

                allowedEffects |= Effect.Pos;

                if (isRotation) allowedEffects |= Effect.Rot;
                if (isScl) allowedEffects |= Effect.Scl;
                if (isTether) allowedEffects |= Effect.Tether;
                if (isPosOffset) allowedEffects |= Effect.PosOffset;
                if (isRotOffset) allowedEffects |= Effect.RotOffset;
                if (isSclOffset) allowedEffects |= Effect.SclOffset;
            }

            public ConfigEntry<Effect> Effects;
            public ConfigEntry<bool> AdjustForSize;
            public ConfigEntry<ClothesKind> DisableWhenClothes;

            public ConfigEntry<int> NoiseOctaves;
            public ConfigEntry<float> NoiseAmplitudePos;
            public ConfigEntry<float> NoiseAmplitudeRot;
            public ConfigEntry<float> NoiseAmplitudeScl;


            //public ConfigEntry<float> PosGravity;
            public ConfigEntry<float> PosSpring;
            public ConfigEntry<float> PosDamping;
            public ConfigEntry<float> PosShockStr;
            public ConfigEntry<float> PosShockThreshold;
            public ConfigEntry<float> PosFreezeThreshold;
            public ConfigEntry<float> PosFreezeLen;
            public ConfigEntry<float> PosBleedStr;
            public ConfigEntry<float> PosBleedLen;
            //public ConfigEntry<float> LinearMass;
            //public ConfigEntry<Vector3> LinearLimitPositive;
            //public ConfigEntry<Vector3> LinearLimitNegative;


            public ConfigEntry<float> RotSpring;
            public ConfigEntry<float> RotDamping;
            public ConfigEntry<float> RotRate;
            //public ConfigEntry<Axis> AngularApplicationMaster;
            //public ConfigEntry<Axis> AngularApplicationSlave;


            public ConfigEntry<float> SclStr;
            public ConfigEntry<float> SclRate;
            public ConfigEntry<float> SclDistort;
            public ConfigEntry<bool> SclPreserveVol;


            public ConfigEntry<float> TetherFactor;
            public ConfigEntry<float> TetherFreq;
            public ConfigEntry<float> TetherDamp;
            public ConfigEntry<int> TetherMaxDeg;


            public ConfigEntry<int> RotOffsetRollDeg;
            public ConfigEntry<float> RotOffsetRollFaceUpFactor;

            public ConfigEntry<float> PosOffsetPitchFaceDown;
            public ConfigEntry<float> PosOffsetPitchUpsideDown;
            public ConfigEntry<Vector3> PosOffsetRoll;

            public ConfigEntry<float> SclOffsetFaceUp;
            public ConfigEntry<float> SclOffsetFaceUpPerpAxesFactor;
            public ConfigEntry<float> SclOffsetFaceDown;
            public ConfigEntry<float> SclOffsetFaceDownPerpAxesFactor;

            internal Effect allowedEffects;

        }

        #endregion
    }
}
