using System;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using KKAPI.Chara;

namespace AnimFilter
{
    [BepInPlugin(GUID, Name, Version)]
    internal class AnimFilterPlugin : BaseUnityPlugin
    {
        public const string GUID = "koik.animfilter";
        public const string Name = "AnimFilter" +
#if DEBUG
            " (Debug)"
#endif
            ;
        public const string Version = "1.0.0";

        internal static new ManualLogSource Logger;


        private void Start()
        {
            Logger = base.Logger;

            CharacterApi.RegisterExtraBehaviour<AnimFilterCharaController>(GUID);
        }
    }
}
