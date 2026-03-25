using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using KKAPI.Studio;
using KKAPI.Studio.UI;
using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using static Kokyu.KokyuCharaController;

namespace Kokyu
{
    internal static class KokyuGui
    {
        private const float SettingFloor = 0f;
        private const float SettingCeiling = 2f;
        internal static void Register()
        {
            MakerAPI.RegisterCustomSubCategories += RegisterBreathCharaConfig;
            RegisterStudioControls();
        }

        private static void RegisterBreathCharaConfig(object sender, RegisterSubCategoriesEvent e)
        {
            var makerSex = MakerAPI.GetMakerSex();

            var enableMask = KokyuPlugin.SettingEnableMask.Value;

            // Disabled by the setting.
            if ((makerSex == 0 && (enableMask & KokyuPlugin.Sex.Male) == 0) ||
                (makerSex == 1 && (enableMask & KokyuPlugin.Sex.Female) == 0))
            {
                return;
            }

            //var category = MakerConstants.Parameter.Character;
            var category = new MakerCategory(MakerConstants.Parameter.ADK.CategoryName, "Behaviour");
            e.AddSubCategory(category);

            var plugin = KokyuPlugin.Instance;
            var color = new Color(0.7f, 0.7f, 0.7f);


            // TOGGLE

            var breathState = e.AddControl(
                new MakerToggle(category, "Breath", plugin));
            breathState.BindToFunctionController<KokyuCharaController, bool>(
                (component) => component.BreathEnabled,
                (component, value) => component.BreathEnabled = value);

            // MAGNITUDE

            e.AddControl(new MakerText("Multiplier to adjust overall expansion.", category, plugin) { TextColor = color });

            var breathMagnitude = e.AddControl(
                new MakerSlider(category, "Breath Size", SettingFloor, SettingCeiling, 1f, plugin));
            breathMagnitude.BindToFunctionController<KokyuCharaController, float>(
                (component) => component.BreathMagnitude,
                (component, value) => component.BreathMagnitude = value);

            // SPEED

            e.AddControl(new MakerText("Multiplier to adjust overall speed across all patterns.", category, plugin) { TextColor = color });

            var breathSpeed = e.AddControl(
                new MakerSlider(category, "Breath Speed", SettingFloor, SettingCeiling, 1f, plugin));
            breathSpeed.BindToFunctionController<KokyuCharaController, float>(
                (component) => component.BreathSpeed,
                (component, value) => component.BreathSpeed = value);

            // ROTATION

            e.AddControl(new MakerText("Multiplier to adjust the spine and neck rotations.", category, plugin) { TextColor = color });

            var breathRotation = e.AddControl(
                new MakerSlider(category, "Breath Body Rotation", SettingFloor, SettingCeiling, 1f, plugin));
            breathRotation.BindToFunctionController<KokyuCharaController, float>(
                (component) => component.BreathRotation,
                (component, value) => component.BreathRotation = value);

            // PATTERN

            e.AddControl(new MakerText("Preview various patterns. Doesn't affect anything.", category, plugin) { TextColor = color });

            var breathPattern = e.AddControl(
                new MakerDropdown("Pattern", Enum.GetNames(typeof(Pattern)), category, 0, plugin));
            breathPattern.BindToFunctionController<KokyuCharaController, int>(
                (component) => (int)component.BreathPattern,
                (component, value) => component.BreathPattern = (Pattern)value);
        }

        private static void RegisterStudioControls()
        {
#if DEBUG
            KokyuPlugin.Logger.LogDebug($"RegisterStudioControls: studio[{StudioAPI.InsideStudio}]");
#endif
            if (!StudioAPI.InsideStudio) return;


            // PATTERNS
            var ptnDropdown = new CurrentStateCategoryDropdown(
                "Pattern",
                Enum.GetNames(typeof(Pattern)),
                c => (int)c.GetChaControl().GetComponent<KokyuCharaController>().BreathPattern
                );

            ptnDropdown.Value.Subscribe(SetPtn);


            // TOGGLE
            var toggle = new CurrentStateCategorySwitch(
                "Breath",
                c => c.GetChaControl().GetComponent<KokyuCharaController>().BreathEnabled
                );
            toggle.Value.Subscribe(SetToggle);


            // SPEED
            var speed = new CurrentStateCategorySlider(
                "Speed",
                c => c.GetChaControl().GetComponent<KokyuCharaController>().BreathRotation,
               SettingFloor,
               SettingCeiling
                );
            speed.Value.Subscribe(SetSpeed);


            // MAGNITUDE
            var mag = new CurrentStateCategorySlider(
                "Magnitude",
                c => c.GetChaControl().GetComponent<KokyuCharaController>().BreathRotation,
               SettingFloor,
               SettingCeiling
                );
            mag.Value.Subscribe(SetMagnitude);


            // ROTATION
            var rotation = new CurrentStateCategorySlider(
                "Rotation",
                c => c.GetChaControl().GetComponent<KokyuCharaController>().BreathRotation,
               SettingFloor,
               SettingCeiling
                );
            rotation.Value.Subscribe(SetRotation);


            var cat = StudioAPI.GetOrCreateCurrentStateCategory("Kokyu");
            cat.AddControls([toggle, mag, speed, rotation, ptnDropdown]);






            // LOCAL FUNCTIONS
            static void SetPtn(int i)
            {
                var ptn = (Pattern)i;

                foreach (var component in StudioAPI.GetSelectedControllers<KokyuCharaController>())
                {
                    component.BreathPattern = ptn;
                }
            }
            static void SetToggle(bool state)
            {
                foreach (var component in StudioAPI.GetSelectedControllers<KokyuCharaController>())
                {
                    component.BreathEnabled = state;
                }
            }
            static void SetMagnitude(float value)
            {
                foreach (var component in StudioAPI.GetSelectedControllers<KokyuCharaController>())
                {
                    component.BreathMagnitude = value;
                }
            }
            static void SetSpeed(float value)
            {
                foreach (var component in StudioAPI.GetSelectedControllers<KokyuCharaController>())
                {
                    component.BreathSpeed = value;
                }
            }
            static void SetRotation(float value)
            {
                foreach (var component in StudioAPI.GetSelectedControllers<KokyuCharaController>())
                {
                    component.BreathRotation = value;
                }
            }
        }
    }
}
