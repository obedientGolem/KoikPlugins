using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using KKAPI.Utilities;
using SceneAssist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine;

namespace KK_Things
{
    [BepInPlugin(GUID, Name, Version)]
    public class DevFiddlePlugin : BaseUnityPlugin
    {
        public const string GUID = "koik.dev.fiddle";
        public const string Version = "1.0.0";
        public const string Name = "DevFiddle";

        internal new ManualLogSource Logger;

        public ConfigEntry<bool> Enabled;
        public ConfigEntry<AmbientMode> AmbientMode;

        private ConfigEntry<Vector3> AmbientLight;
        private ConfigEntry<Vector3> AmbientSkyColor;
        private ConfigEntry<Vector3> AmbientGroundColor;
        private ConfigEntry<Vector3> AmbientEquatorColor;

        private void Awake()
        {
            Logger = base.Logger;

            Enabled = Config.Bind(name, "Enabled", true,
                    new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 100 }));

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

            Config.SettingChanged += TweakLight;

            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Logger.LogInfo($"OnSceneLoaded[{scene.name}][{mode}] ambientMode[{RenderSettings.ambientMode}]");

            if (Enabled.Value) TweakLight(null, null);
        }

        private void TweakLight(object sender, SettingChangedEventArgs e)
        {
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

            TweakDirLight();
        }

        private Light _dirLight;
        private void TweakDirLight()
        {
            if (_dirLight == null) _dirLight = FindObjectsOfType<Light>()
                    .Where(c => c.transform.name.Equals("Directional Light") && c.isActiveAndEnabled)
                    .FirstOrDefault();

            if (_dirLight == null) return;

            _dirLight.color = new Color(0.75f, 0.75f, 0.75f);
        }

        private void Update()
        {
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.S))
            {
                Debug.Log("Ctrl + S pressed");
            }
        }
    }
}
