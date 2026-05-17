using BepInEx;
using KKAPI;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AvgDt
{
    [BepInPlugin(GUID, Name, Version)]
    [BepInProcess(KoikatuAPI.GameProcessName)]
#if KK
    [BepInProcess(KoikatuAPI.GameProcessNameSteam)]
#endif
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    [DefaultExecutionOrder(0)]
    public class AvgDtPlugin : BaseUnityPlugin
    {
        public const string GUID = "koik.avgdt";
        public const string Name = "AvgDt";
        public const string Version = "1.0.0";


        public static bool IsFade { get; private set; }
        public static bool IsPause { get; private set; }
        public static bool IsLagSpike { get; private set; }

        public static float DtAvg { get; private set; }
        public static float DtInv { get; private set; }


        private float _dtCeiling = 1f;
        private int _startFrame;
        private float _startT;
        private float _prevNT;


        private void Update()
        {
            var dt = Time.deltaTime;

            if (dt == 0f)
            {
                IsPause = true;
                return;
            }
            else if (IsPause)
            {
                IsPause = false;

                _startT = Time.time;
                _startFrame = Time.frameCount;
            }

            DtInv = 1f / dt;

            IsLagSpike = (dt > _dtCeiling); // && ConsecutiveFrameCounter++ > (int)(dtInv * (3f / 60f)));

            IsFade = SceneApi.GetIsFadeNow();

            var t = Time.time;

            var nt = t - (int)t;

            if (nt < _prevNT)
            {
                var currFrame = Time.frameCount;

                var frames = currFrame - _startFrame;

                var newDtAvg = frames == 0 ? dt : (t - _startT) / frames;

                newDtAvg = Mathf.Min(_dtCeiling, newDtAvg);
                DtAvg = newDtAvg;
                _dtCeiling = newDtAvg * (1f + (1f / 3f));

                _startT = t;
                _startFrame = currFrame;
            }
            _prevNT = nt;
        }

    }
}
