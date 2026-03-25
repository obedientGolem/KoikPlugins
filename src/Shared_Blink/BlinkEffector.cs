using ActionGame.Point;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static BetterBlink.BlinkCharaController;
using Random = UnityEngine.Random;

namespace BetterBlink
{
    internal class BlinkEffector
    {
        private const int CurveSamples = 128;
        private const float Step = 1f / (CurveSamples - 1);

        private readonly float[] _curve = new float[CurveSamples];
        private static readonly int _interpLen = Enum.GetValues(typeof(Interp)).Length;


        private bool _isBlinking;
        private float _prevRate;

        private float _blinkTime;
        private float _blinkTimeInv;
        private int _blinkFlurryStreak;
        private float _blinkFlurryChance;
        private float _blinkTimeStamp;

        private float _eyeOpenFloor;
        private float _eyeOpenLvl = 1f;
        private float _eyeOpenLvlTimeStamp;


        #region Preparations


        private void PrepareCurve(bool updOpenLvl)
        {
            // TIMINGS

            var rand = BlinkPlugin.Randomize.Value;

            var blinkTime = rand ? _blinkTime * Random.Range(0.5f, 1.5f) : _blinkTime;
            _blinkTimeInv = 1f / blinkTime;
            // Normalized ratios
            var shutPhase = Random.Range(
                1f / (0.45f / 0.025f), 
                1f / (0.45f / 0.075f));

            var closingLen = Random.Range(
                1f / (0.45f / 0.1f), 
                1f / (0.45f / 0.15f));

            var closingLenInv = 1f / closingLen;

            var openingLen = 1f - shutPhase - closingLen;
            var openingLenInv = 1f / openingLen;
            var openingStart = 1f - openingLen;

            if (updOpenLvl)
            {
                // To early to change openness level
                if (_eyeOpenLvlTimeStamp > Time.time)
                {
                    updOpenLvl = false;
                }
                else
                {
                    _eyeOpenLvlTimeStamp = Time.time + Random.Range(5f, 15f);
                }
            }

            var oldLvl = _eyeOpenLvl;
            var newLvl = updOpenLvl ? Random.Range(_eyeOpenFloor, 1f) : oldLvl;
            _eyeOpenLvl = newLvl;

            // CURVE

            var interpIn = (Interp)Random.Range(0, _interpLen);
            var interpOut = (Interp)Random.Range(0, _interpLen);

            var inP1 = GetP1(interpIn);
            var inP2 = GetP2(interpIn);

            var outP1 = GetP1(interpOut);
            var outP2 = GetP2(interpOut);

            var curve = _curve;

            var t = 0f;
            var i = 0;

            var closingEndIdx = (int)(closingLen * (CurveSamples - 1));
            var openingStartIdx = (int)(openingStart * (CurveSamples - 1));

            for (; i < closingEndIdx; i++, t += Step)
            {
                var x = 1f - (t * closingLenInv);
                curve[i] = ApplyInterpolation(x, interpIn, inP1, inP2) * oldLvl;
            }
            for (; i < openingStartIdx; i++, t += Step)
            {
                curve[i] = 0f;
            }
            for (; i < CurveSamples; i++, t += Step)
            {
                // Too few samples makes the Int cast a tad too shaky.
                var x =  Mathf.Max((t - openingStart) * openingLenInv, 0f);

                curve[i] = ApplyInterpolation(x, interpOut, outP1, outP2) * newLvl;
            }

            // LOCAL FUNCTIONS

            static float ApplyInterpolation(float t, Interp interp, float p1, float p2)
            {
                return interp switch
                {
                    Interp.Smooth => t * t * (3f - 2f * t),
                    Interp.Smoother => t * t * t * (10f - 15f * t + 6f * t * t),
                    Interp.Linear => t,
                    _ => Bezier(t, p1, p2),
                };
            }
            static float Bezier(float t, float p1, float p2)
            {
                var u = 1 - t;

                return 3 * u * u * t * p1 +
                       3 * u * t * t * p2 +
                       t * t * t;
            }
            static float GetP1(Interp interp)
            {
                return interp switch
                {
                    Interp.SinePlus => 0.5f + Random.value * 0.25f,
                    Interp.Sine => 0.25f + Random.value * 0.25f,
                    //Interp.Double => 0.25f + Random.value * 0.5f,
                    _ => 0f
                };
            }
            static float GetP2(Interp interp)
            {
                return interp switch
                {
                    Interp.SinePlus or Interp.Sine => 1f,
                    Interp.Power => Random.value * 0.5f,
                    _ => 0f
                };
            }
        }


        #endregion


        #region OnHooks


        internal void OnReload(BlinkCharaController controller)
        {
            _blinkTime = controller.BlinkLength;
            _blinkFlurryChance = controller.BlinkFlurry;

            var oldEyeOpenFloor = _eyeOpenFloor;

            _eyeOpenFloor = controller.EyeOpenLvl;

            if (oldEyeOpenFloor != _eyeOpenFloor) 
                StartBlink(forceOpenLvlUpd: true);
        }

        internal void Blink()
        {
            StartBlink();
        }

        private void StartBlink(bool forceOpenLvlUpd = false)
        {
            _isBlinking = true;
            _blinkTimeStamp = Time.time;
            _blinkFlurryStreak = 0;

            if (forceOpenLvlUpd) _eyeOpenLvlTimeStamp = 0f;

            PrepareCurve(updOpenLvl: true);
        }

        public float OnCalcBlink(float rate)
        {
            var oldRate = rate;

            if (_isBlinking)
            {
                var t = (Time.time - _blinkTimeStamp) * _blinkTimeInv;

                if (t >= 1f)
                {
                    _blinkTimeStamp = Time.time;
                    // Go for consecutive.
                    if (Random.value < _blinkFlurryChance * (1f - (_blinkFlurryStreak * (1f / 4f))))
                    {
                        //BlinkPlugin.Logger.LogDebug($"FlurryStart: chance[{_blinkFlurryChance}] isStreak[{_blinkFlurryStreak < 4}] oldRate[{oldRate:F2}] rate[{rate:F2} flurry[{_blinkFlurryStreak}] lastBlink[{Time.time - _timeStamp}]");
                        _blinkFlurryStreak++;

                        if (Random.value < (1f / 3f))
                            PrepareCurve(false);

                        rate = OnCalcBlink(rate);
                    }
                    else
                    {
                        //BlinkPlugin.Logger.LogDebug($"End: chance[{_blinkFlurryChance}] isStreak[{_blinkFlurryStreak < 4}] oldRate[{oldRate:F2}] rate[{rate:F2} flurry[{_blinkFlurryStreak}] lastBlink[{Time.time - _timeStamp}]");

                        rate = _eyeOpenLvl;
                        _isBlinking = false;
                    } 
                }
                else
                {
                    var f = t * (CurveSamples - 1);
                    var idx = (int)f;
                    var frac = f - idx;
                    rate = Mathf.Lerp(_curve[idx], _curve[Mathf.Min(idx + 1, CurveSamples - 1)], frac);
                }
                //BlinkPlugin.Logger.LogDebug($"t[{t:F2}] oldRate[{oldRate:F2}] rate[{rate:F2} flurry[{_blinkFlurryStreak}] lastBlink[{Time.time - _timeStamp}]");
            }
            else if (_prevRate > oldRate)
            {
                if (_prevRate == 1f && (Time.time - _blinkTimeStamp) > 1f)
                {
                    StartBlink();
                    //BlinkPlugin.Logger.LogDebug($"Start: oldRate[{oldRate:F2}] rate[{rate:F2} flurry[{_blinkFlurryStreak}] lastBlink[{Time.time - _timeStamp}]");
                    rate = OnCalcBlink(rate);
                }
                else
                {
                    rate = _eyeOpenLvl;
                }
            }
            else
            {
                rate = _eyeOpenLvl;
            }
            //BlinkPlugin.Logger.LogDebug($"oldRate[{oldRate:F2}] rate[{rate:F2} flurry[{_blinkFlurryStreak}] lastBlink[{Time.time - _timeStamp}]");
            _prevRate = oldRate;
            return rate;
        }


        #endregion


        #region Types

        // Ordered by the speed of gain.
        private enum Interp
        {
            // Bezier with P1(0.5..0.75) P2(1)
            // Extra steep sine.
            SinePlus,
            // Bezier with P1(0.25..0.5) P2(1)
            // Should be much cheaper then Math.Sin()
            // Comes with plenty of variability that OG sine lacks.
            Sine,
            // SmoothStep
            Smooth,
            // SmootherStep
            Smoother,
            // Plain x = y
            Linear,
            // Bezier with P1(0) P2(0..0.75)
            // Because OG power functions are too rigid in variability.
            Power,
            // Bezier with P1(0.25..0.75) P2(0)
            // Not necessarily a double, can be just an uneven weird one.
            //Double,
        }


        #endregion
    }
}
