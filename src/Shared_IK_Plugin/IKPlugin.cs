using System;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using KKAPI.Chara;

namespace IKPlugin
{
    [BepInPlugin(GUID, Name, Version)]
    internal class IKNoisePlugin : BaseUnityPlugin
    {
        public const string GUID = "koik.ikplugin";
        public const string Name = "IKPlugin" +
#if DEBUG
            " Debug"
#endif
            ;
        public const string Version = "1.0.0";

        internal static new ManualLogSource Logger;


        private void Start()
        {
            Logger = base.Logger;

            CharacterApi.RegisterExtraBehaviour<IKPluginCharaController>(GUID);
        }
    }
}
