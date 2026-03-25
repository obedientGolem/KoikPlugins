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
#if !DEBUG
    [BepInDependency(KKABMX_Core.GUID, KKABMX_Core.Version)]
#endif

#if KK
    [BepInProcess(KoikatuAPI.GameProcessNameSteam)]
#endif
    internal class AniMorphPlugin : BaseUnityPlugin
    {
        public const string GUID = "AniMorph.ABMX";
        public const string Name = "Anisotropic Morph";
        public const string Version = "0.25";
        internal new static ManualLogSource Logger;

        public static ConfigEntry<Gender> Enable;
        public static ConfigEntry<bool> MaleEnableDB;
        public static ConfigEntry<FilterDeltaTimeKind> FilterDeltaTime;

        public static readonly Dictionary<Body, ConfigType> ConfigDic = [];


        private static readonly Dictionary<Body, Effect> _allowedEffectsDic = new()
        {
            { Body.Breast, Effect.Pos | Effect.Rot | Effect.Tether | Effect.Accel | Effect.Decel | Effect.GravPos | Effect.GravRot | Effect.GravScl },
            { Body.Butt, Effect.Pos | Effect.Rot | Effect.Accel | Effect.Decel },
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
                    order: 1000,
                    effect: Effect.Pos | Effect.Rot | Effect.Tether | Effect.Accel | Effect.Decel | Effect.GravPos | Effect.GravRot | Effect.GravScl,
                    adjustForSize: true,
                    disableWhenClothes: ClothesKind.Top | ClothesKind.Bra,

                    posSpring: 15f,
                    posDamping: 0.5f,
                    posGravity: 0f,

                    rotSpring: 15f,
                    rotDamping: 0.2f,
                    rotMaxAngle: 30f,
                    rotRate: 2f,
                    //AngularApplicationMaster: Axis.Z,
                    //AngularApplicationSlave: Axis.X | Axis.Y,

                    sclAccelerationFactor: 0.35f,
                    sclDecelerationFactor: 0.5f,
                    sclLerpSpeed: 8f,
                    sclMaxDistortion: 0.4f,
                    sclUnevenDistribution: new Vector3(0.6f, 0.5f, 0.4f),
                    sclPreserveVolume: true,
                    sclDumbAcceleration: true,

                    tetherMultiplier: 2f,
                    tetherFrequency: 2f,
                    tetherDamping: 0.3f,
                    tetherMaxAngle: 30f,

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

            ConfigDic.Add(Body.Butt,
                new(
                    body: Body.Butt,
                    config: Config,
                    order: 800,
                    effect: Effect.Pos | Effect.Rot | Effect.Accel | Effect.Decel,
                    adjustForSize: true,
                    disableWhenClothes: ClothesKind.Panty,

                    posSpring: 21f,
                    posDamping: 0.5f,
                    posGravity: 0f,
                    //LinearLimitPositive: new Vector3(1f, 1.33f, 1f),
                    //LinearLimitNegative: new Vector3(1f, 0.67f, 1f),

                    rotSpring: 20f,
                    rotDamping: 0.5f,
                    rotMaxAngle: 30f,
                    rotRate: 3f,
                    //AngularApplicationMaster: (Axis)0,
                    //AngularApplicationSlave: Axis.X | Axis.Y | Axis.Z,

                    sclAccelerationFactor: 0.15f,
                    sclDecelerationFactor: 0.3f,
                    sclLerpSpeed: 8f,
                    sclMaxDistortion: 0.4f,
                    sclUnevenDistribution: new Vector3(0.5f, 0.5f, 0.5f),
                    sclPreserveVolume: true,
                    sclDumbAcceleration: true,

                    tetherMultiplier: -3f,
                    tetherFrequency: 3f,
                    tetherDamping: 0.3f,
                    tetherMaxAngle: 30f,

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

            ConfigDic.Add(Body.Thigh,
                new(
                    body: Body.Thigh,
                    config: Config,
                    order: 600,
                    effect: Effect.Pos | Effect.Accel | Effect.Decel,
                    adjustForSize: true,
                    disableWhenClothes: ClothesKind.None,

                    posSpring: 30f,
                    posDamping: 0.67f,
                    posGravity: 0.1f,
                    //LinearLimitPositive: Vector3.one,
                    //LinearLimitNegative: Vector3.one,

                    rotSpring: 30f,
                    rotDamping: 5f,
                    rotMaxAngle: 45f,
                    rotRate: 2f,
                    //AngularApplicationMaster: Axis.X | Axis.Y | Axis.Z,
                    //AngularApplicationSlave: Axis.Y,

                    sclAccelerationFactor: 40f,
                    sclDecelerationFactor: 0.5f,
                    sclLerpSpeed: 8f,
                    sclMaxDistortion: 0.5f,
                    sclUnevenDistribution: new Vector3(0.4f, 0.5f, 0.6f),
                    sclPreserveVolume: true,
                    sclDumbAcceleration: true,

                    tetherMultiplier: 0.5f,
                    tetherFrequency: 3f,
                    tetherDamping: 0.3f,
                    tetherMaxAngle: 30f,

                    gravityUpUp: Vector3.zero,
                    gravityUpMid: Vector3.zero,
                    gravityUpDown: Vector3.zero,
                    gravityFwdUp: Vector3.zero,
                    gravityFwdMid: Vector3.zero,
                    gravityFwdDown: Vector3.zero,
                    gravityRightUp: Vector3.zero,
                    gravityRightMid: Vector3.zero,
                    gravityRightDown: Vector3.zero));

            ConfigDic.Add(Body.Kokan,
                new(
                    body: Body.Kokan,
                    config: Config,
                    order: 400,
                    effect: Effect.None,
                    adjustForSize: true,
                    disableWhenClothes: ClothesKind.None,
                    posSpring: 15f,
                    posDamping: 0.5f,
                    posGravity: 0f,
                    //LinearLimitPositive: Vector3.one,
                    //LinearLimitNegative: Vector3.one,

                    rotSpring: 20f,
                    rotDamping: 5f,
                    rotMaxAngle: 45f,
                    rotRate: 2f,
                    //AngularApplicationMaster: Axis.Z,
                    //AngularApplicationSlave: Axis.X | Axis.Y,

                    sclAccelerationFactor: 0.35f,
                    sclDecelerationFactor: 0.5f,
                    sclLerpSpeed: 8f,
                    sclMaxDistortion: 0.4f,
                    sclUnevenDistribution: new Vector3(0.6f, 0.5f, 0.4f),
                    sclPreserveVolume: true,
                    sclDumbAcceleration: true,

                    tetherMultiplier: 0.5f,
                    tetherFrequency: 3f,
                    tetherDamping: 0.3f,
                    tetherMaxAngle: 30f,

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

            ConfigDic.Add(Body.Waist01,
                new(
                    body: Body.Waist01,
                    config: Config,
                    order: 200,
                    effect: Effect.None,
                    adjustForSize: true,
                    disableWhenClothes: ClothesKind.None,
                    posSpring: 15f,
                    posDamping: 0.5f,
                    posGravity: 0f,
                    //LinearLimitPositive: Vector3.one,
                    //LinearLimitNegative: Vector3.one,

                    rotSpring: 20f,
                    rotDamping: 5f,
                    rotMaxAngle: 45f,
                    rotRate: 2f,
                    //AngularApplicationMaster: Axis.Z,
                    //AngularApplicationSlave: Axis.X | Axis.Y,

                    sclAccelerationFactor: 0.35f,
                    sclDecelerationFactor: 0.5f,
                    sclLerpSpeed: 8f,
                    sclMaxDistortion: 0.4f,
                    sclUnevenDistribution: new Vector3(0.6f, 0.5f, 0.4f),
                    sclPreserveVolume: true,
                    sclDumbAcceleration: true,

                    tetherMultiplier: 0.5f,
                    tetherFrequency: 3f,
                    tetherDamping: 0.3f,
                    tetherMaxAngle: 30f,

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

            ConfigDic.Add(Body.Waist02,
                new(
                    body: Body.Waist02,
                    config: Config,
                    order: 0,
                    effect: Effect.None,
                    adjustForSize: true,
                    disableWhenClothes: ClothesKind.None,
                    posSpring: 15f,
                    posDamping: 0.5f,
                    posGravity: 0f,
                    //LinearLimitPositive: Vector3.one,
                    //LinearLimitNegative: Vector3.one,

                    rotSpring: 20f,
                    rotDamping: 5f,
                    rotMaxAngle: 45f,
                    rotRate: 2f,
                    //AngularApplicationMaster: Axis.Z,
                    //AngularApplicationSlave: Axis.X | Axis.Y,

                    sclAccelerationFactor: 0.35f,
                    sclDecelerationFactor: 0.5f,
                    sclLerpSpeed: 8f,
                    sclMaxDistortion: 0.4f,
                    sclUnevenDistribution: new Vector3(0.6f, 0.5f, 0.4f),
                    sclPreserveVolume: true,
                    sclDumbAcceleration: true,

                    tetherMultiplier: 0.5f,
                    tetherFrequency: 3f,
                    tetherDamping: 0.3f,
                    tetherMaxAngle: 30f,

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
                if ((_allowedEffectsDic[Body.Butt] & effect) == 0)
                {
                    ConfigDic[Body.Butt].Effects.Value &= ~effect;
                }
            }
        }


        #region Types


        public enum Body
        {
            Breast,
            Thigh,
            Butt,
            Kokan,
            Waist01,
            Waist02
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
                float posSpring,
                float posDamping,
                float posGravity,
                //Vector3 LinearLimitPositive,
                //Vector3 LinearLimitNegative,
                float rotSpring,
                float rotDamping,
                float rotMaxAngle,
                float rotRate,
                //Axis AngularApplicationMaster,
                //Axis AngularApplicationSlave,
                float sclAccelerationFactor,
                float sclDecelerationFactor,
                float sclLerpSpeed,
                float sclMaxDistortion,
                Vector3 sclUnevenDistribution,
                bool sclPreserveVolume,
                bool sclDumbAcceleration,
                float tetherMultiplier,
                float tetherFrequency,
                float tetherDamping,
                float tetherMaxAngle,
                Vector3 gravityUpUp,
                Vector3 gravityUpMid,
                Vector3 gravityUpDown,
                Vector3 gravityFwdUp,
                Vector3 gravityFwdMid,
                Vector3 gravityFwdDown,
                Vector3 gravityRightUp,
                Vector3 gravityRightMid,
                Vector3 gravityRightDown
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

                AdjustForSize = config.Bind(name, "AdjustForSize", adjustForSize,
                    new ConfigDescription("Adjust effects for the breast size\nUpdates after the scene change", null, new ConfigurationManagerAttributes { Order = order - 5 }));

                DisableWhenClothes = config.Bind(name, "DisableClothed", disableWhenClothes,
                    new ConfigDescription("Don't apply effects when particular piece of clothing is fully present", null, new ConfigurationManagerAttributes { Order = order - 10 }));

                PosSpring = config.Bind(name, "PosSpring", posSpring,
                    new ConfigDescription("Strength of positional lag\nBigger value – more effort put out", null, new ConfigurationManagerAttributes { Order = order - 15 }));

                PosDamping = config.Bind(name, "PosDamping", posDamping,
                    new ConfigDescription("Strength of negation of positional lag\nShould be smaller then LinearStrength for any effect", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = order - 20, ShowRangeAsPercent = false }));

                PosGravity = config.Bind(name, "PosGravity", posGravity,
                    new ConfigDescription("Strength of gravity for positional lag\nMost of the time looks better at 0 (disabled)", new AcceptableValueRange<float>(-1f, 1f), new ConfigurationManagerAttributes { Order = order - 25 }));

                //// TODO
                //BreastLinearMass = config.Bind(name, "LinearMass", 1f, new ConfigDescription("Not implemented", null, new ConfigurationManagerAttributes { Order = 79 }));

                //LinearLimitPositive = config.Bind(name, "LinearLimitPositive", defaultLinearLimitPositive,
                //    new ConfigDescription("Axial limitation of movements in positive local space\n0..1..1+ to nullify/set default/amplify axis in positive local space", null, new ConfigurationManagerAttributes { Order = order - 30 }));

                //LinearLimitNegative = config.Bind(name, "LinearLimitNegative", defaultLinearLimitNegative,
                //    new ConfigDescription("Axial limitation of movements in negative local space\n0..1..1+ to nullify/set default/amplify axis in negative local space", null, new ConfigurationManagerAttributes { Order = order - 35 }));

                RotSpring = config.Bind(name, "RotSpring", rotSpring,
                    new ConfigDescription("Strength of rotational lag", null, new ConfigurationManagerAttributes { Order = order - 40 }));

                RotDamping = config.Bind(name, "RotDamping", rotDamping,
                    new ConfigDescription("", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = order - 45, ShowRangeAsPercent = false  }));

                RotMaxAngle = config.Bind(name, "RotMaxAngle", rotMaxAngle,
                    new ConfigDescription("Rotation won't be adjusted beyond this angle.", new AcceptableValueRange<float>(1f, 90f), new ConfigurationManagerAttributes { Order = order - 50 }));

                RotRate = config.Bind(name, "RotRate", rotRate,
                    new ConfigDescription("Rate at which rotation is applied, bigger value – faster application.", new AcceptableValueRange<float>(0f, 20f), new ConfigurationManagerAttributes { Order = order - 46, ShowRangeAsPercent = false }));

                //AngularApplicationMaster = config.Bind(name, "AngularApplyToRoot", defaultAngularApplicationMaster,
                //    new ConfigDescription("Which axes or rotational lag should be applied to the root bone of the breast", null, new ConfigurationManagerAttributes { Order = order - 55 }));

                //AngularApplicationSlave = config.Bind(name, "AngularApplyToBone", defaultAngularApplicationSlave,
                //    new ConfigDescription("Which axes or rotational lag should be applied to the breast bones", null, new ConfigurationManagerAttributes { Order = order - 60 }));

                ScaleAccelerationFactor = config.Bind(name, "ScaleAccelerationFactor", sclAccelerationFactor,
                    new ConfigDescription("Strength of deformation during acceleration", null, new ConfigurationManagerAttributes { Order = order - 65 }));

                ScaleDecelerationFactor = config.Bind(name, "ScaleDecelerationFactor", sclDecelerationFactor,
                    new ConfigDescription("Strength of deformation during deceleration", null, new ConfigurationManagerAttributes { Order = order - 65 }));

                ScaleLerpSpeed = config.Bind(name, "ScaleLerpSpeed", sclLerpSpeed,
                    new ConfigDescription("Speed of scale change\nBigger value – more rapid, less smooth change", null, new ConfigurationManagerAttributes { Order = order - 70 }));

                ScaleMaxDistortion = config.Bind(name, "ScaleMaxDistortion", sclMaxDistortion,
                    new ConfigDescription("Scale deformation won't exceed scale of 1 +- this value", null, new ConfigurationManagerAttributes { Order = order - 75 }));

                ScaleUnevenDistribution = config.Bind(name, "ScaleUnevenDistribution", sclUnevenDistribution,
                    new ConfigDescription("Preferential treatment of scale axes\nDefault value 0.5, can be used with no consideration towards balance", null, new ConfigurationManagerAttributes { Order = order - 80 }));

                ScalePreserveVolume = config.Bind(name, "ScalePreserveVolume", sclPreserveVolume,
                    new ConfigDescription("Keep volume consistent", null, new ConfigurationManagerAttributes { Order = order - 85 }));

                ScaleDumbAcceleration = config.Bind(name, "ScaleDumbAcceleration", sclDumbAcceleration,
                    new ConfigDescription("Dumb looks better anyway", null, new ConfigurationManagerAttributes { Order = order - 90 }));


                TetheringMultiplier = config.Bind(name, "TetheringMultiplier", tetherMultiplier,
                    new ConfigDescription("Strength of tethering", new AcceptableValueRange<float>(-5f, 5f), new ConfigurationManagerAttributes { Order = order - 95, ShowRangeAsPercent = false }));

                TetheringFrequency = config.Bind(name, "TetheringFrequency", tetherFrequency,
                    new ConfigDescription("Ceiling for the amount oscillation per second", null, new ConfigurationManagerAttributes { Order = order - 100 }));

                TetheringDamping = config.Bind(name, "TetheringDamping", tetherDamping,
                    new ConfigDescription("Strength of negation of tethering", new AcceptableValueRange<float>(0f, 2f), new ConfigurationManagerAttributes { Order = order - 105, ShowRangeAsPercent = false }));

                TetheringMaxAngle = config.Bind(name, "TetheringMaxAngle", tetherMaxAngle,
                    new ConfigDescription("Tethering won't exceed this value in degrees ", null, new ConfigurationManagerAttributes { Order = order - 110 }));


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

            public ConfigEntry<float> PosGravity;
            public ConfigEntry<float> PosSpring;
            public ConfigEntry<float> PosDamping;
            //public ConfigEntry<float> LinearMass;
            //public ConfigEntry<Vector3> LinearLimitPositive;
            //public ConfigEntry<Vector3> LinearLimitNegative;

            public ConfigEntry<float> RotSpring;
            public ConfigEntry<float> RotDamping;
            public ConfigEntry<float> RotMaxAngle;
            public ConfigEntry<float> RotRate;
            //public ConfigEntry<Axis> AngularApplicationMaster;
            //public ConfigEntry<Axis> AngularApplicationSlave;

            public ConfigEntry<float> ScaleAccelerationFactor;
            public ConfigEntry<float> ScaleDecelerationFactor;
            public ConfigEntry<float> ScaleLerpSpeed;
            public ConfigEntry<float> ScaleMaxDistortion;
            public ConfigEntry<bool> ScalePreserveVolume;
            public ConfigEntry<bool> ScaleDumbAcceleration;
            public ConfigEntry<Vector3> ScaleUnevenDistribution;

            public ConfigEntry<float> TetheringMultiplier;
            public ConfigEntry<float> TetheringFrequency;
            public ConfigEntry<float> TetheringDamping;
            public ConfigEntry<float> TetheringMaxAngle;

            public ConfigEntry<Vector3> GravityUpUp;
            public ConfigEntry<Vector3> GravityUpMid;
            public ConfigEntry<Vector3> GravityUpDown;
            public ConfigEntry<Vector3> GravityFwdUp;
            public ConfigEntry<Vector3> GravityFwdMid;
            public ConfigEntry<Vector3> GravityFwdDown;
            public ConfigEntry<Vector3> GravityRightUp;
            public ConfigEntry<Vector3> GravityRightMid;
            public ConfigEntry<Vector3> GravityRightDown;
        }

        #endregion
    }
}
