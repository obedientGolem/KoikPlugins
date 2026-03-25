using KK_SpeakSoftly;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KK_VariableVoiceVolume
{
    internal static class SpeakSoftlyGui
    {
        internal static void Register()
        {
            MakerAPI.RegisterCustomSubCategories += RegisterVolumeSlider;
        }

        private static void RegisterVolumeSlider(object sender, RegisterSubCategoriesEvent e)
        {
            var makerSex = MakerAPI.GetMakerSex();
            var pluginState = SpeakSoftlyPlugin.PluginState.Value;

            if ((makerSex == 0 && (pluginState & SpeakSoftlyPlugin.SettingState.Male) == 0) ||
                (makerSex == 1 && (pluginState & SpeakSoftlyPlugin.SettingState.Female) == 0))
                return;

            //var category = MakerConstants.Parameter.Character;
            var category = new MakerCategory(MakerConstants.Parameter.ADK.CategoryName, "Behaviour");
            e.AddSubCategory(category);

            var plugin = SpeakSoftlyPlugin.Instance;
            var color = new Color(0.7f, 0.7f, 0.7f);

            e.AddControl(new MakerText("Actual volume of Voice and Breath is determined by the plugin for each character.", category, plugin) { TextColor = color });

            var voiceVolumeFloor = e.AddControl(new MakerSlider(category, "Volume Min", 0f, 1f, 0.2f, plugin));

            voiceVolumeFloor.BindToFunctionController<SpeakSoftlyCharaController, float>(
                (controller) => (float)controller.VolumeFloor,
                (controller, value) => controller.VolumeFloor = (float)value);

            var voiceVolumeCeiling = e.AddControl(new MakerSlider(category, "Volume Max", 0f, 1f, 1f, plugin));

            voiceVolumeCeiling.BindToFunctionController<SpeakSoftlyCharaController, float>(
                (controller) => (float)controller.VolumeCeiling,
                (controller, value) => controller.VolumeCeiling = (float)value);
        }
    }
}
