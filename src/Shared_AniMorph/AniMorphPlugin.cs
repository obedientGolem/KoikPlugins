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
        public const string GUID = "Koik.AnisotropicMorph";
        public const string Name = "AnisotropicMorph"
#if DEBUG
            + " (DEBUG)"
#endif
            ;
        public const string Version = "0.9";
        internal new static ManualLogSource Logger;

        public static ConfigEntry<Gender> Enable;
        public static ConfigEntry<bool> MaleEnableDB;
        public static ConfigEntry<FilterDeltaTimeKind> FilterDeltaTime;

        public static readonly Dictionary<Body, ConfigType> ConfigDic = [];


        private static readonly Dictionary<Body, Effect> _allowedEffectsDic = new()
        {
            { Body.Breast, Effect.Pos | Effect.Rot | Effect.Tether | Effect.Scl | Effect.GravPos | Effect.GravRot | Effect.GravScl },
            { Body.Pelvis, Effect.Pos | Effect.Rot | Effect.Scl },
        };


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

        private void BindConfig()
        {
            ConfigDic.Add(Body.Breast,
                new(
                    body: Body.Breast,
                    config: Config,
                    order: 10000,
                    effect: Effect.Pos | Effect.Rot | Effect.Tether | Effect.Scl | Effect.GravPos | Effect.GravRot | Effect.GravScl,
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

                    rotSidewaysDeg: 20,
                    rotSidewaysFaceUpDivider: 3,

                    gravityUpUp: Vector3.zero,
                    gravityUpMid: new Vector3(0f, 0.02f, 0f),
                    gravityUpDown: new Vector3(0f, 0.05f, 0f),
                    gravityFwdUp: new Vector3(0.075f, 0.075f, -0.15f),
                    gravityFwdMid: Vector3.zero,
                    gravityFwdDown: new Vector3(-0.05f, -0.05f, 0.2f),
                    gravityRightUp: new Vector3(-0.025f, -0.02f, 0f),
                    gravityRightMid: Vector3.zero,
                    gravityRightDown: new Vector3(0.025f, -0.02f, 0f)
                ));

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

                    rotSidewaysDeg: 20,
                    rotSidewaysFaceUpDivider: 3,

                    gravityUpUp: Vector3.zero,
                    gravityUpMid: new Vector3(0f, 0.02f, 0f),
                    gravityUpDown: new Vector3(0f, 0.05f, 0f),
                    gravityFwdUp: new Vector3(-0.1f, 0f, 0.15f),
                    gravityFwdMid: Vector3.zero,
                    gravityFwdDown: new Vector3(-0.05f, -0.05f, 0.2f),
                    gravityRightUp: new Vector3(-0.025f, -0.02f, 0f),
                    gravityRightMid: Vector3.zero,
                    gravityRightDown: new Vector3(0.025f, -0.02f, 0f)
                    ));


            ConfigDic.Add(Body.Chest,
                new(
                    body: Body.Chest,
                    config: Config,
                    order: 8000,
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

                    rotSidewaysDeg: 20,
                    rotSidewaysFaceUpDivider: 3,

                    gravityUpUp: Vector3.zero,
                    gravityUpMid: new Vector3(0f, 0.02f, 0f),
                    gravityUpDown: new Vector3(0f, 0.05f, 0f),
                    gravityFwdUp: new Vector3(0.075f, 0.075f, -0.15f),
                    gravityFwdMid: Vector3.zero,
                    gravityFwdDown: new Vector3(-0.05f, -0.05f, 0.2f),
                    gravityRightUp: new Vector3(-0.025f, -0.02f, 0f),
                    gravityRightMid: Vector3.zero,
                    gravityRightDown: new Vector3(0.025f, -0.02f, 0f)
                ));

            ConfigDic.Add(Body.Shoulders,
                new(
                    body: Body.Shoulders,
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

                    rotSidewaysDeg: 20,
                    rotSidewaysFaceUpDivider: 3,

                    gravityUpUp: Vector3.zero,
                    gravityUpMid: new Vector3(0f, 0.02f, 0f),
                    gravityUpDown: new Vector3(0f, 0.05f, 0f),
                    gravityFwdUp: new Vector3(0.075f, 0.075f, -0.15f),
                    gravityFwdMid: Vector3.zero,
                    gravityFwdDown: new Vector3(-0.05f, -0.05f, 0.2f),
                    gravityRightUp: new Vector3(-0.025f, -0.02f, 0f),
                    gravityRightMid: Vector3.zero,
                    gravityRightDown: new Vector3(0.025f, -0.02f, 0f)
                ));

            ConfigDic.Add(Body.Tummy,
                new(
                    body: Body.Tummy,
                    config: Config,
                    order: 6000,
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

                    rotSidewaysDeg: 20,
                    rotSidewaysFaceUpDivider: 3,

                    gravityUpUp: Vector3.zero,
                    gravityUpMid: new Vector3(0f, 0.02f, 0f),
                    gravityUpDown: new Vector3(0f, 0.05f, 0f),
                    gravityFwdUp: new Vector3(0.075f, 0.075f, -0.15f),
                    gravityFwdMid: Vector3.zero,
                    gravityFwdDown: new Vector3(-0.05f, -0.05f, 0.2f),
                    gravityRightUp: new Vector3(-0.025f, -0.02f, 0f),
                    gravityRightMid: Vector3.zero,
                    gravityRightDown: new Vector3(0.025f, -0.02f, 0f)
                ));

            ConfigDic.Add(Body.Thighs,
                new(
                    body: Body.Thighs,
                    config: Config,
                    order: 5000,
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
                    //posGravity: 0.1f,
                    //LinearLimitPositive: Vector3.one,
                    //LinearLimitNegative: Vector3.one,

                    rotSpring: 30f,
                    rotDamping: 5f,
                    rotRate: 2f,
                    //AngularApplicationMaster: Axis.X | Axis.Y | Axis.Z,
                    //AngularApplicationSlave: Axis.Y,

                    sclStr: 40f,
                    sclRate: 8f,
                    sclDistortion: 0.5f,
                    sclPreserveVolume: true,

                    tetherMultiplier: 0.5f,
                    tetherFrequency: 3f,
                    tetherDamping: 0.3f,
                    tetherMaxDeg: 30,

                    rotSidewaysDeg: 20,
                    rotSidewaysFaceUpDivider: 3,

                    gravityUpUp: Vector3.zero,
                    gravityUpMid: Vector3.zero,
                    gravityUpDown: Vector3.zero,
                    gravityFwdUp: Vector3.zero,
                    gravityFwdMid: Vector3.zero,
                    gravityFwdDown: Vector3.zero,
                    gravityRightUp: Vector3.zero,
                    gravityRightMid: Vector3.zero,
                    gravityRightDown: Vector3.zero));


            //ConfigDic.Add(Body.Cheeks,
            //    new(
            //        body: Body.Cheeks,
            //        config: Config,
            //        order: 4000,
            //        effect: Effect.Pos,
            //        adjustForSize: true,
            //        disableWhenClothes: ClothesKind.None,

            //        noiseOctaves: 4,
            //        noiseAffliction: NoiseAffliction.Pos | NoiseAffliction.Rot | NoiseAffliction.Scl,
            //        noiseAmplitudePos: 0.075f,
            //        noiseAmplitudeRot: 0.67f,
            //        noiseAmplitudeScl: 0.15f,


            //        posSpring: 30f,
            //        posDamping: 0.2f,
            //        posShockStr: 1f,
            //        posShockThreshold: 0.15f,
            //        posFreezeThreshold: 0.25f,
            //        posFreezeLen: 0.02f,
            //        posBleedStr: 2f,
            //        posBleedLen: 0.1f,
            //        //posGravity: 0.1f,
            //        //LinearLimitPositive: Vector3.one,
            //        //LinearLimitNegative: Vector3.one,

            //        rotSpring: 30f,
            //        rotDamping: 5f,
            //        rotRate: 2f,
            //        //AngularApplicationMaster: Axis.X | Axis.Y | Axis.Z,
            //        //AngularApplicationSlave: Axis.Y,

            //        sclAccelerationFactor: 40f,
            //        sclDecelerationFactor: 0.5f,
            //        sclLerpSpeed: 8f,
            //        sclMaxDistortion: 0.5f,
            //        sclUnevenDistribution: new Vector3(0.4f, 0.5f, 0.6f),
            //        sclPreserveVolume: true,
            //        sclDumbAcceleration: true,

            //        tetherMultiplier: 0.5f,
            //        tetherFrequency: 3f,
            //        tetherDamping: 0.3f,
            //        tetherMaxAngle: 30f,

            //        gravityUpUp: Vector3.zero,
            //        gravityUpMid: Vector3.zero,
            //        gravityUpDown: Vector3.zero,
            //        gravityFwdUp: Vector3.zero,
            //        gravityFwdMid: Vector3.zero,
            //        gravityFwdDown: Vector3.zero,
            //        gravityRightUp: Vector3.zero,
            //        gravityRightMid: Vector3.zero,
            //        gravityRightDown: Vector3.zero));



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
                    //posGravity: 0.1f,
                    //LinearLimitPositive: Vector3.one,
                    //LinearLimitNegative: Vector3.one,

                    rotSpring: 30f,
                    rotDamping: 5f,
                    rotRate: 2f,
                    //AngularApplicationMaster: Axis.X | Axis.Y | Axis.Z,
                    //AngularApplicationSlave: Axis.Y,

                    sclStr: 40f,
                    sclRate: 8f,
                    sclDistortion: 0.5f,
                    sclPreserveVolume: true,

                    tetherMultiplier: 0.5f,
                    tetherFrequency: 3f,
                    tetherDamping: 0.3f,
                    tetherMaxDeg: 30,

                    rotSidewaysDeg: 20,
                    rotSidewaysFaceUpDivider: 3,

                    gravityUpUp: Vector3.zero,
                    gravityUpMid: Vector3.zero,
                    gravityUpDown: Vector3.zero,
                    gravityFwdUp: Vector3.zero,
                    gravityFwdMid: Vector3.zero,
                    gravityFwdDown: Vector3.zero,
                    gravityRightUp: Vector3.zero,
                    gravityRightMid: Vector3.zero,
                    gravityRightDown: Vector3.zero));
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
#if !DEBUG
            AdjustAllowedEffects();
#endif
            AniMorphCharaController.OnSettingChanged();
        }

        private void AdjustAllowedEffects()
        {
            foreach (Effect effect in Enum.GetValues(typeof(Effect)))
            {
                if ((_allowedEffectsDic[Body.Breast] & effect) == 0)
                {
                    ConfigDic[Body.Breast].Effects.Value &= ~effect;
                }
                if ((_allowedEffectsDic[Body.Pelvis] & effect) == 0)
                {
                    ConfigDic[Body.Pelvis].Effects.Value &= ~effect;
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

                float rotSpring,
                float rotDamping,
                float rotRate,

                float sclStr,
                float sclRate,
                float sclDistortion,
                bool sclPreserveVolume,

                float tetherMultiplier,
                float tetherFrequency,
                float tetherDamping,
                int tetherMaxDeg,

                Vector3 gravityUpUp,
                Vector3 gravityUpMid,
                Vector3 gravityUpDown,
                Vector3 gravityFwdUp,
                Vector3 gravityFwdMid,
                Vector3 gravityFwdDown,
                Vector3 gravityRightUp,
                Vector3 gravityRightMid,
                Vector3 gravityRightDown,

                int rotSidewaysDeg,
                int rotSidewaysFaceUpDivider
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



                RotSpring = config.Bind(name, "Rotation Spring", rotSpring,
                    new ConfigDescription("Strength of the rotational lag.",
                    new AcceptableValueRange<float>(0f, 100f), new ConfigurationManagerAttributes { Order = order - 40, ShowRangeAsPercent = false }));

                RotDamping = config.Bind(name, "Rotation Damping", rotDamping,
                    new ConfigDescription("Strength of negation of the rotational lag.", 
                    new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = order - 45, ShowRangeAsPercent = false  }));

                RotRate = config.Bind(name, "Rotation Interpolation Speed", rotRate,
                    new ConfigDescription("How fast the rotation offset changes.", 
                    new AcceptableValueRange<float>(0f, 10f), new ConfigurationManagerAttributes { Order = order - 50, ShowRangeAsPercent = false }));



                SclStr = config.Bind(name, "Scale Strength", sclStr,
                    new ConfigDescription("How much the velocity influences the scale.", 
                    new AcceptableValueRange<float>(0f, 50f), new ConfigurationManagerAttributes { Order = order - 65, ShowRangeAsPercent = false }));

                SclDistort = config.Bind(name, "Scale Distortion", sclDistortion,
                    new ConfigDescription("How much the scale can change, 1 ± this value.",
                    new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = order - 70, ShowRangeAsPercent = false }));

                SclRate = config.Bind(name, "Scale Interpolation Speed", sclRate,
                    new ConfigDescription("How fast the scale offset changes.", 
                    new AcceptableValueRange<float>(0f, 20f), new ConfigurationManagerAttributes { Order = order - 75, ShowRangeAsPercent = false }));

                SclPreserveVol = config.Bind(name, "Scale Preserve Volume", sclPreserveVolume,
                    new ConfigDescription("Keep volume consistent", null, new ConfigurationManagerAttributes { Order = order - 80 }));



                TetherFactor = config.Bind(name, "TetheringMultiplier", tetherMultiplier,
                    new ConfigDescription("Strength of tethering", new AcceptableValueRange<float>(-5f, 5f), new ConfigurationManagerAttributes { Order = order - 95, ShowRangeAsPercent = false }));

                TetherFreq = config.Bind(name, "TetheringFrequency", tetherFrequency,
                    new ConfigDescription("Ceiling for the amount oscillation per second", null, new ConfigurationManagerAttributes { Order = order - 100 }));

                TetherDamp = config.Bind(name, "TetheringDamping", tetherDamping,
                    new ConfigDescription("Strength of negation of tethering", new AcceptableValueRange<float>(0f, 2f), new ConfigurationManagerAttributes { Order = order - 105, ShowRangeAsPercent = false }));

                TetherMaxDeg = config.Bind(name, "TetheringMaxAngle", tetherMaxDeg,
                    new ConfigDescription("Tethering won't exceed this value in degrees ", 
                    new AcceptableValueRange<int>(10, 60), new ConfigurationManagerAttributes { Order = order - 110 }));


                RotSidewaysDeg = config.Bind(name, "RotSidewaysDeg", rotSidewaysDeg,
                    new ConfigDescription("", 
                    new AcceptableValueRange<int>(10, 60), new ConfigurationManagerAttributes { Order = order - 115, ShowRangeAsPercent = false }));

                RotSidewaysFaceUpDivider = config.Bind(name, "RotSidewaysFaceUpDivider", rotSidewaysFaceUpDivider,
                    new ConfigDescription("", 
                    new AcceptableValueRange<int>(1, 5), new ConfigurationManagerAttributes { Order = order - 120, ShowRangeAsPercent = false }));



                // Not a slightest clue how to explain to users what the hell dots(cosines) do together with transform's directions.
                GravityUpUp = config.Bind(name, "gravityUpUp", gravityUpUp, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = order - 115, IsAdvanced = true }));
                GravityUpMid = config.Bind(name, "gravityUpMid", gravityUpMid, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = order - 116, IsAdvanced = true }));
                GravityUpDown = config.Bind(name, "gravityUpDown", gravityUpDown, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = order - 117, IsAdvanced = true }));
                GravityFwdUp = config.Bind(name, "gravityFwdUp", gravityFwdUp, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = order - 118, IsAdvanced = true }));
                GravityFwdMid = config.Bind(name, "gravityFwdMid", gravityFwdMid, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = order - 119, IsAdvanced = true }));
                GravityFwdDown = config.Bind(name, "gravityFwdDown", gravityFwdDown, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = order - 120, IsAdvanced = true }));
                GravityRightUp = config.Bind(name, "gravityRightUp", gravityRightUp, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = order - 121, IsAdvanced = true }));
                GravityRightMid = config.Bind(name, "gravityRightMid", gravityRightMid, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = order - 122, IsAdvanced = true }));
                GravityRightDown = config.Bind(name, "gravityRightDown", gravityRightDown, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = order - 123, IsAdvanced = true }));

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


            public ConfigEntry<Vector3> GravityUpUp;
            public ConfigEntry<Vector3> GravityUpMid;
            public ConfigEntry<Vector3> GravityUpDown;
            public ConfigEntry<Vector3> GravityFwdUp;
            public ConfigEntry<Vector3> GravityFwdMid;
            public ConfigEntry<Vector3> GravityFwdDown;
            public ConfigEntry<Vector3> GravityRightUp;
            public ConfigEntry<Vector3> GravityRightMid;
            public ConfigEntry<Vector3> GravityRightDown;

            public ConfigEntry<int> RotSidewaysDeg;
            public ConfigEntry<int> RotSidewaysFaceUpDivider;
        }

        #endregion
    }
}
