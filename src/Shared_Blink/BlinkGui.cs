using ADV.Commands.Base;
using KKAPI;
using KKAPI.Chara;
using KKAPI.MainGame;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static ADV.CommandController;

namespace Blink
{
    internal class BlinkGui
    {
        //private static object _autoTranslator;

        internal static void Register()
        {
            MakerAPI.RegisterCustomSubCategories += RegisterBlinkCharaConfig;
        }

        private static void RegisterBlinkCharaConfig(object sender, RegisterSubCategoriesEvent e)
        {
            //var category = MakerConstants.Parameter.Character;
            var category = new MakerCategory(MakerConstants.Parameter.ADK.CategoryName, "Behaviour" );
            e.AddSubCategory(category);

            var plugin = BlinkPlugin.Instance;
            var color = new Color(0.7f, 0.7f, 0.7f);

            // ACTION

            var blink = e.AddControl(
                new MakerButton("Blink", category, BlinkPlugin.Instance));

            blink.OnClick.AddListener(OnBlink);


            // LENGTH

            e.AddControl(new MakerText("Range from 0.1 to 1 sec.", category, plugin) { TextColor = color });

            var blinkLen = e.AddControl(
                new MakerSlider(category, "Blink Length", 0.1f, 1f, 0.3f, plugin));

            blinkLen.BindToFunctionController<BlinkCharaController, float>(
                (component) => component.BlinkLength,
                (component, value) => component.BlinkLength = value);


            // FLURRY

            e.AddControl(new MakerText("Chance for consecutive blinking aka flurry.", category, plugin) { TextColor = color });

            var flurry = e.AddControl(
                new MakerSlider(category, "Blink Flurry", 0f, 1f, 0.25f, plugin));

            flurry.BindToFunctionController<BlinkCharaController, float>(
                (component) => component.BlinkFlurry,
                (component, value) => component.BlinkFlurry = value);


            // OPENNESS

            e.AddControl(new MakerText("Vary eye openness between character's max and this value.", category, plugin) { TextColor = color });

            var openness = e.AddControl(
                new MakerSlider(category, "Min Eye Openness", 0f, 1f, 0.8f, plugin));
            
            openness.BindToFunctionController<BlinkCharaController, float>(
                (component) => component.EyeOpenLvl,
                (component, value) => component.EyeOpenLvl = value);

            static void OnBlink()
            {
                var chara = MakerAPI.GetCharacterControl();
                if (chara == null) return;

                var component = chara.GetComponent<BlinkCharaController>();
                if (component == null) return;

                component.Blink();
            }
        }

        //private static bool FindAutoTranslator()
        //{
        //    if (_autoTranslator != null) return true;

        //    var type = FindType("XUnity.AutoTranslator.Plugin.BepInEx.AutoTranslatorPlugin");

        //    _autoTranslator = FindMonoBehaviourInstance(type);

        //    return _autoTranslator != null;



        //    static Type FindType(string typeName)
        //    {
        //        var type = Type.GetType(typeName);

        //        if (type != null) return type;

        //        return AppDomain.CurrentDomain
        //            .GetAssemblies()
        //            .SelectMany(a => a.GetTypes())
        //            .FirstOrDefault(t => t.Name == typeName);
        //    }

        //    static object FindMonoBehaviourInstance(Type type)
        //    {
        //        if (type == null) return null;

        //        // Ensure it's a MonoBehaviour
        //        if (!typeof(MonoBehaviour).IsAssignableFrom(type))
        //            return null;

        //        return UnityEngine.Object.FindObjectOfType(type);
        //    }
        //}

    }
}
