#if VR_DIR
using KK.RootMotion.FinalIK;
#else
using RootMotion.FinalIK;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace IKNoise
{
    internal class IKNoiseEffector
    {
        internal const float OneThird = (1f / 3f);
        internal const float TwoThirds = (2f / 3f);

        private readonly Animator _anim;

        private readonly int _len;
        private readonly IKNoiseModifier[] _modifiers;
        private readonly float[] _heightCalcArray;

        private bool _samplePosition;


        #region DicInit


        private readonly static Dictionary<Body, Cfg> _cfgDic = new()
        {
            { 
                Body.Spine, new() 
            },
            {
                Body.Shoulders, new(
                    syncAxisX: true
                    )
            },
            {
                Body.Thighs, new(
                    syncAxisX: true
                    )
            },
#if VR
            {
                Body.Arms, new() 
            },
            {
                Body.Legs, new Cfg(
                    parentAppFactor: 1f
                    )
            },
#endif
        };
        private readonly static Dictionary<Body, BaseCfg> _baseCfgDic = new()
        {
            {
                Body.Spine, new BaseCfg(
                    body: Body.Spine,
                    axisForDotUp: Vector3.forward,

                    appPositive: [new Vector3(1f, TwoThirds, 1f)],
                    appNegative: [new Vector3(1f, TwoThirds, 1f)],

                    appFSideUpPositive: new Vector3(TwoThirds, 1f, 1f),
                    appFSideUpNegative: new Vector3(TwoThirds, 1f, 1f),

                    appFSideDownPositive: new Vector3(1f, 1f, 1f),
                    appFSideDownNegative: new Vector3(1f, 1f, 1f)
                    )
            },
            {
                Body.Shoulders, new BaseCfg(
                    body: Body.Shoulders,
                    axisForDotUp: Vector3.forward,

                    appPositive: [new Vector3(TwoThirds, 1f, 1f), new Vector3(TwoThirds, 1f, 1f)],
                    appNegative: [new Vector3(TwoThirds, 1f, TwoThirds), new Vector3(TwoThirds, 1f, TwoThirds)],

                    appFSideUpPositive: new Vector3(1f, 1f, 1f),
                    appFSideUpNegative: new Vector3(1f, 1f, OneThird),

                    appFSideDownPositive: new Vector3(1f, 1f, 1f),
                    appFSideDownNegative: new Vector3(1f, 1f, 1f)
                    )
            },
            {
                Body.Thighs, new BaseCfg(
                    body: Body.Thighs,
                    axisForDotUp: Vector3.forward,

                    appPositive: [new Vector3(1f, 1f, 1f), new Vector3(1f, 1f, 1f)],
                    appNegative: [new Vector3(1f, 1f, TwoThirds), new Vector3(1f, 1f, TwoThirds)],

                    appFSideUpPositive: new Vector3(TwoThirds, 1f, 1f),
                    appFSideUpNegative: new Vector3(TwoThirds, 1f, OneThird),

                    appFSideDownPositive: new Vector3(1f, 1f, OneThird),
                    appFSideDownNegative: new Vector3(1f, 1f, 1f),

                    angleLimit: 45f
                    )
            },
#if VR
            {
                Body.Arms, new BaseCfg(
                    body: Body.Arms,
                    axisForDotUp: Vector3.down,

                    appPositive: [new Vector3(1f, 1f, 1f), new Vector3(OneThird, 1f, 1f)],
                    appNegative: [new Vector3(OneThird, 1f, 1f), new Vector3(1f, 1f, 1f)],

                    appFSideUpPositive: new Vector3(1f, 1f, 1f),
                    appFSideUpNegative: new Vector3(1f, 1f, 1f),

                    appFSideDownPositive: new Vector3(1f, 0f, 0f),
                    appFSideDownNegative: new Vector3(1f, 0f, 0f)
                    )
            },
            {
                Body.Legs, new BaseCfg(
                    body: Body.Legs,
                    axisForDotUp: Vector3.down,

                    appPositive: [new Vector3(1f, 1f, 1f), new Vector3(1f, 1f, 1f)],
                    appNegative: [new Vector3(1f, 1f, 1f), new Vector3(1f, 1f, 1f)],

                    appFSideUpPositive: new Vector3(1f, 1f, 1f),
                    appFSideUpNegative: new Vector3(1f, 1f, 1f),

                    appFSideDownPositive: new Vector3(0f, 1f, 0f),
                    appFSideDownNegative: new Vector3(0f, 0f, 0f)
                    )
            },
#endif
        };


        #endregion


        internal IKNoiseEffector(Animator anim, FullBodyBipedIK fbbik)
        {
            _anim = anim;

            var len = IKNoisePlugin.enumBodyValues.Length;

            _modifiers = new IKNoiseModifier[len];
            _heightCalcArray = new float[len];
            _len = len;

            _modifiers[(int)Body.Spine] = new IKNoiseModifier(
                [fbbik.solver.effectors[0]], _cfgDic[Body.Spine], _baseCfgDic[Body.Spine]);

            _modifiers[(int)Body.Shoulders] = new IKNoiseModifier(
                [fbbik.solver.effectors[1], fbbik.solver.effectors[2]], _cfgDic[Body.Shoulders], _baseCfgDic[Body.Shoulders]);

            _modifiers[(int)Body.Thighs] = new IKNoiseModifier(
                [fbbik.solver.effectors[3], fbbik.solver.effectors[4]], _cfgDic[Body.Thighs], _baseCfgDic[Body.Thighs]);
#if VR
            _modifiers[(int)Body.Arms] = new IKNoiseModifier(
                [fbbik.solver.effectors[5], fbbik.solver.effectors[6]], _cfgDic[Body.Arms], _baseCfgDic[Body.Arms]);

            _modifiers[(int)Body.Legs] = new IKNoiseModifierLimb(
                [fbbik.solver.effectors[7], fbbik.solver.effectors[8]], _modifiers[(int)Body.Thighs], _cfgDic[Body.Legs], _baseCfgDic[Body.Legs]);
#endif
            OnSetPlay();            
        }

        internal void OnLateUpdate()
        {
            var dt = Time.deltaTime;

            if (dt == 0f) return;

            if (_samplePosition)
            {
                _samplePosition = false;
                SamplePosition();
            }
            var dtInv = 1f / dt;

            var animState = _anim.GetCurrentAnimatorStateInfo(0);

            var animLen = animState.length;
            var animLenInv = animLen == 0f ? 1f : (1f / animLen) * (1f / 2.25f);

            foreach (var modifier in _modifiers)
            {
                modifier.UpdateModifier(dt, dtInv, animLenInv);
            }
        }

        private void SamplePosition()
        {
            for (var i = 0; i < _len; i++)
            {
                var modifier = _modifiers[i];
                var avgDeg = modifier.GetAvgDegVerticalAlignment();
#if DEBUG
                IKNoisePlugin.Logger.LogWarning($"[{(Body)i}] OnSetPlay avgDeg[{avgDeg:F3}]");
#endif
                modifier.LimitMovements(avgDeg);
            }
        }

        //private const int Steps = 512;
        //private const float Step = 1f / (Steps - 1);

        //private void CreateCurve()
        //{
        //    List<double> waveValues = new List<double>();

        //    var step = Step;

        //    // Generate values using the sine wave formula: y = A * sin(2 * π * f * x + φ) + C
            

        //    for (int i = 0; i < Steps; i++)
        //    {
        //        var x = i * step;

        //        var value = amplitude * Math.Sin(2 * Math.PI * frequency * x + phaseShift) + offset;
        //        waveValues.Add(value);
        //    }

        //    return waveValues;
        //}


        #region OnHooks


        internal void OnSettingChanged(int sex, float sceneFactor)
        {
            for (var i = 0;  i < _len; i++)
            {
#if DEBUG
                IKNoisePlugin.Logger.LogDebug($"OnSettingChanges[{(Body)i}] sex[{sex}] sceneFactor[{sceneFactor}]");
#endif
                _modifiers[i].OnSettingChanged(IKNoisePlugin.ConfigDic[(Body)i], sex, sceneFactor);
            }
        }

        internal void OnSetPlay() => _samplePosition = true;


        #endregion


        #region Types


        internal struct Cfg(float parentAppFactor = 0f, bool syncAxisX = false)
        {
            internal bool syncAxisX = syncAxisX;
            internal float parentAppFactor = parentAppFactor;
        }

        internal class BaseCfg(
            Body body, 
            Vector3 axisForDotUp,

            Vector3[] appPositive, 
            Vector3[] appNegative,

            Vector3 appFSideUpPositive,
            Vector3 appFSideUpNegative,

            Vector3 appFSideDownPositive,
            Vector3 appFSideDownNegative,

            float angleLimit = 30f
            )
        {
            // Vertical in a sense when that body part rests on the ground.
            internal readonly Body body = body;
            internal readonly float angleLimit = angleLimit;
            internal readonly Vector3 axisForDotUp = axisForDotUp;
            internal readonly Vector3[] appPositive = appPositive;
            internal readonly Vector3[] appNegative = appNegative;

            internal readonly Vector3 appFSideUpPositive = appFSideUpPositive;
            internal readonly Vector3 appFSideUpNegative = appFSideUpNegative;

            internal readonly Vector3 appFSideDownPositive = appFSideDownPositive;
            internal readonly Vector3 appFSideDownNegative = appFSideDownNegative;

        }


        #endregion
    }
}
