using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Utilities;
using SceneAssist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace KK_Things
{
    [BepInPlugin(GUID, Name, Version)]
    public class DevFiddlePlugin : BaseUnityPlugin
    {
        public const string GUID = "koik.dev.fiddle";
        public const string Version = "1.0.0";
        public const string Name = "DevFiddle";

        internal new ManualLogSource Logger;

        public ConfigEntry<bool> TweakAmbientLight;
        public ConfigEntry<bool> TweakDirLight;
        public ConfigEntry<AmbientMode> AmbientMode;

        private ConfigEntry<Vector3> AmbientLight;
        private ConfigEntry<Vector3> AmbientSkyColor;
        private ConfigEntry<Vector3> AmbientGroundColor;
        private ConfigEntry<Vector3> AmbientEquatorColor;

        private void Awake()
        {
            Logger = base.Logger;


            TweakAmbientLight = Config.Bind(name, "Enabled", true,
                    new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 100 }));

            TweakDirLight = Config.Bind(name, "EnabledExtraDirLight", true,
                    new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 99 }));

            AmbientMode = Config.Bind(name, "AmbientMode", UnityEngine.Rendering.AmbientMode.Trilight,
                    new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 90 }));



            AmbientLight = Config.Bind("", "AmbientLight", new Vector3(0.5f, 0.5f, 0.5f),
                    new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 50 }));

            AmbientSkyColor = Config.Bind("", "AmbientSkyColor", new Vector3(0.4f, 0.4f, 0.4f),
                    new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 40 }));

            AmbientGroundColor = Config.Bind("", "AmbientGroundColor", new Vector3(0.3f, 0.3f, 0.3f),
                new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 30 }));

            AmbientEquatorColor = Config.Bind("", "AmbientEquatorColor", new Vector3(0.6f, 0.6f, 0.6f),
                new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 20 }));

            Config.SettingChanged += OnSettingChanged;

            CharacterApi.RegisterExtraBehaviour<DevFiddleCharaController>(GUID);

            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;

            DevFiddleHooks.HandleEnable();
        }

        private void OnSettingChanged(object sender, EventArgs e)
        {
            if (TweakAmbientLight.Value)
                NeedUpdateAmbientLight = true;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Logger.LogInfo($"OnSceneLoaded[{scene.name}][{mode}] ambientMode[{RenderSettings.ambientMode}] fade[{SceneApi.GetIsFadeNow()}]");

            NeedUpdateAmbientLight = TweakAmbientLight.Value;

            NeedUpdateDirLight = TweakDirLight.Value;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            Logger.LogInfo($"OnSceneUnloaded[{scene.name}] ambientMode[{RenderSettings.ambientMode}] fade[{SceneApi.GetIsFadeNow()}]");
        }


        private bool NeedUpdateAmbientLight
        {
            get => field;
            set
            {
                _updateRequired = value || NeedUpdateDirLight;

                field = value;
            }
        }

        private bool NeedUpdateDirLight
        {
            get => field;
            set
            {
                _updateRequired = value || NeedUpdateAmbientLight;
                
                field = value;
            }
        }

        private bool _updateRequired;
        private Light _dirLight;
        private Light _dirLightCopy;

        private const string ExtraDirLightName = "Reverse DirLight (Extra)";
        private void UpdateDirLight()
        {
            NeedUpdateDirLight = false;

            // If we still hold ref on dir light and it's active then most likely we're good.
            if (_dirLight != null && _dirLight.isActiveAndEnabled && _dirLightCopy != null) return;

            var lights = FindObjectsOfType<Light>();

            var dirLight = lights
                .Where(l => l.isActiveAndEnabled && l.type == LightType.Directional && l.name.Equals("Directional Light"))
                .FirstOrDefault();

            if (dirLight == null)
            {
#if DEBUG
                Logger.LogWarning($"UpdateDirLight: Couldn't find directional light");
#endif
                return;
            }

            var child = dirLight.transform.GetComponentsInChildren<Transform>()
                .Where(t => t.name.Equals(ExtraDirLightName))
                .FirstOrDefault();

            var copy = child != null ? child.GetComponent<Light>() : null;

            if (copy == null)
            {
                copy = Instantiate(dirLight, dirLight.transform);
                copy.gameObject.name = ExtraDirLightName;
                copy.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }


            //dirLight.color = new Color(0.9f, 0.85f, 0.85f);
            dirLight.intensity = 0.4f;

            copy.color = new Color(0.55f, 0.5f, 0.6f);
            copy.intensity = 0.4f;

            _dirLight = dirLight;
            _dirLightCopy = copy;
#if DEBUG
            Logger.LogWarning($"UpdateDirLight: Successfully updated!");
#endif
        }

        private static List<string> _skybox2_assets =
        [
            "cloudy_drinking",
            "go_above",
            "grimm_night",
            "ice_world",
            "interstellar",

            "miramar",
            "peace_water",
            "stormy_days",
            "toon_star",
            "violent_days",
            "clouds",
            "forest",
            "mossy_mountains",
            "sunset",
        ];

        private void LoadSkybox()
        {
            var skybox = CommonLib.LoadAsset<GameObject>("studio/az/skybox2.unity3d", "clouds", clone: true);
        }

        private void UpdateAmbientLight()
        {
            NeedUpdateAmbientLight = false;

            var ambientMode = AmbientMode.Value;

            RenderSettings.ambientMode = ambientMode;

            switch (ambientMode)
            {
                case UnityEngine.Rendering.AmbientMode.Trilight:
                    RenderSettings.ambientSkyColor = new Color(AmbientSkyColor.Value.x, AmbientSkyColor.Value.y, AmbientSkyColor.Value.z);
                    RenderSettings.ambientGroundColor = new Color(AmbientGroundColor.Value.x, AmbientGroundColor.Value.y, AmbientGroundColor.Value.z);
                    RenderSettings.ambientEquatorColor = new Color(AmbientEquatorColor.Value.x, AmbientEquatorColor.Value.y, AmbientEquatorColor.Value.z);
                    break;
                case UnityEngine.Rendering.AmbientMode.Flat:
                    RenderSettings.ambientLight = new Color(AmbientLight.Value.x, AmbientLight.Value.y, AmbientLight.Value.z);
                    break;
            }
#if DEBUG
            Logger.LogWarning($"UpdateAmbientLight: Successfully updated!");
#endif
        }

        private void Update()
        {
            if (_updateRequired && (Manager.Scene.Instance == null || !Manager.Scene.Instance.IsFadeNow ||
                (Manager.Scene.Instance.sceneFade != null &&
                Manager.Scene.Instance.sceneFade._Fade == SimpleFade.Fade.Out)))
            {
                if (NeedUpdateDirLight)
                    UpdateDirLight();

                if (NeedUpdateAmbientLight)
                    UpdateAmbientLight();
            }
        }
    }
}
