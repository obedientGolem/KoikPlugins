using ExtensibleSaveFormat;
using HarmonyLib;
using KK_SpeakSoftly;
using KKAPI;
using KKAPI.Chara;
using KKAPI.MainGame;
using KKAPI.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Random = UnityEngine.Random;

namespace KK_SpeakSoftly
{
    internal class SpeakSoftlyCharaController : CharaCustomFunctionController
    {
        
        internal static List<SpeakSoftlyCharaController> Instances => _instances;
        internal float VolumeFloor { get; set; }
        internal float VolumeCeiling { get; set; }

        private static readonly List<SpeakSoftlyCharaController> _instances = [];
        private static HFlag _hFlag;
        private static float _volumeAnimFloor;
        private static float _volumeAnimCeiling;
        private static float _volumeAnimRange;
        // WLoop, SLoop
        private static bool _hVariableSpeed;
        // WLoop, SLoop, OLoop
        private static bool _noFade;

        private AudioSource _audioSource;
        private State _state;

        private float _volumeStepMultiplier;
        private float _volumeTarget;
        private float _volumeCurrent;
        private float _volumeSmoothDampVelocity;
        private float _volumeSmoothDampTime;
        // Default volume affected by the in-game settings used as a ceiling.
        private float _volumeDefault = 1f;
        //// Cached settings to avoid accessing static property on every single frame.
        //private float _volumeBaseValue;
        private float _volumeSceneVoiceMultiplier;
        private float _volumeFloor;
        private float _volumeRange;

        // Timestamp to start fade out.
        private float _fadeOutTimestamp;
        // Character stats' influence value.
        private float _statsWeight;


        private bool IsState(State state) => (_state & state) != 0;
        private void AddState(State state) => _state |= state;
        private void RemoveState(State state) => _state &= ~state;

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            var data = new PluginData
            {
                version = 1
            };

            data.data.Add("VolumeFloor", VolumeFloor);
            data.data.Add("VolumeCeiling", VolumeCeiling);

            SetExtendedData(data);
        }

        protected override void OnReload(GameMode currentGameMode, bool maintainState)
        {
            base.OnReload(currentGameMode, maintainState);

            // Defaults
            VolumeFloor = 0.2f;
            VolumeCeiling = 1.0f;

            var data = GetExtendedData();

            if (data != null)
            {
                if (data.data.TryGetValue("VolumeFloor", out var value))
                    VolumeFloor = (float)value;

                if (data.data.TryGetValue("VolumeCeiling", out var value1))
                    VolumeCeiling = (float)value1;
            }

            //var pluginState = SpeakSoftlyPlugin.PluginState.Value;

            //// Same chara can change genders on reload? who knows.
            //if ((ChaControl.sex == 0 && (pluginState & SpeakSoftlyPlugin.SettingState.Male) == 0) ||
            //    (ChaControl.sex == 1 && (pluginState & SpeakSoftlyPlugin.SettingState.Female) == 0))
            //{
            //    Destroy(this);
            //    return;
            //}
            
            OnSettingChangedIntern();
            UpdateStats();
            UpdateTargetVolume();
            _volumeCurrent = _volumeTarget;
        }


        protected override void Awake()
        {
            base.Awake();

            _instances.Add(this);
        } 

        private void Destroy()
        {
            _instances.Remove(this);
        }

        protected override void Update()
        {
            base.Update();

            if (IsState(State.Active))
            {
                var aSource = _audioSource;
                if (aSource == null)
                {
                    _state = State.None;
                    return;
                }

                // Continuously update volume in H.
                if (_hFlag != null)
                    UpdateTargetVolume();

                if (IsState(State.Breath))
                {
                    // Continuously adjust volume
                    _volumeCurrent = Mathf.SmoothDamp(_volumeCurrent, _volumeTarget, ref _volumeSmoothDampVelocity, _volumeSmoothDampTime);

                    if (IsState(State.FadeIn))
                    {
                        aSource.volume += Time.deltaTime * _volumeStepMultiplier;

                        if (aSource.volume >= _volumeCurrent)
                        {
                            aSource.volume = _volumeCurrent;
                            RemoveState(State.FadeIn);
                        }
                    }
                    else if (IsState(State.FadeOut))
                    {
                        aSource.volume -= Time.deltaTime * _volumeStepMultiplier;
                    }

                    else if (aSource.time >= _fadeOutTimestamp)
                    {
                        AddState(State.FadeOut);
                    }
                    else
                    {
                        aSource.volume = _volumeCurrent;
                    }
                }
                else if (IsState(State.Voice))
                {
                    _volumeCurrent = Mathf.SmoothDamp(_volumeCurrent, _volumeTarget, ref _volumeSmoothDampVelocity, _volumeSmoothDampTime);
                    aSource.volume = Mathf.Clamp01(_volumeCurrent * _volumeSceneVoiceMultiplier);
                }
#if DEBUG
                SpeakSoftlyPlugin.Logger.LogDebug($"[{ChaControl.name}]: volume[{aSource.volume}]");
#endif
            }
            else if (IsState(State.Loading))
            {
                if (_audioSource.clip != null)
                {
                    OnAudioStart();
                }
            }
        }

        internal void OnAudioStart()
        {
            var audioSource = ChaControl.asVoice;
            var pluginState = SpeakSoftlyPlugin.PluginState.Value;

            // Bad state, wait for next call from the hook.
            if (audioSource == null || 
                (ChaControl.sex == 0 && (pluginState & SpeakSoftlyPlugin.SettingState.Male) == 0) ||
                (ChaControl.sex == 1 && (pluginState & SpeakSoftlyPlugin.SettingState.Female) == 0))
            {
                _state = State.None;
                return;
            }

            _audioSource = audioSource;
            // Wait until audioClip is loaded in async (few frames).
            if (audioSource.clip == null)
            {
                _state = State.Loading;
                return;
            }
            // Store volume provided by the game as default volume.
            _volumeDefault = audioSource.volume;
            var clipName = audioSource.clip.name;

            // If Voice or ShortGasp.
            if (!clipName.StartsWith("h_ko", StringComparison.Ordinal) || clipName.Contains("_005_") || clipName.Contains("_006_"))
            {
                // Disabled by the setting.
                if ((pluginState & SpeakSoftlyPlugin.SettingState.Voice) == 0)
                {
                    _state = State.None;
                    return;
                }
                _state = State.Active | State.Voice;
                audioSource.volume = Mathf.Clamp01(_volumeCurrent * _volumeSceneVoiceMultiplier);
                return;
            }
            // Disabled by the setting.
            if ((pluginState & SpeakSoftlyPlugin.SettingState.Breath) == 0)
            {
                _state = State.None;
                return;
            }

            // Setup breath.
            _state = State.Active | State.Breath;

            // Add FadeIn if enabled by the setting.
            if ((pluginState & SpeakSoftlyPlugin.SettingState.FadeIn) != 0)
            {
                _state |= State.FadeIn;
            }

            // Write fade length from the setting and apply (or not) randomization.
            var breathFadeLength =
                SpeakSoftlyPlugin.VolumeFadeLength.Value *
                ((pluginState & SpeakSoftlyPlugin.SettingState.Randomize) != 0 ?
                0.5f + Random.value : 1f);

            // Custom start for fade during intense animation loops.
            var floor = _noFade ? 0.2f : 0f;
            _volumeStepMultiplier = _volumeTarget * (1f / breathFadeLength);
            // Set floor
            audioSource.volume = floor;
            _fadeOutTimestamp = audioSource.clip.length - breathFadeLength;

            // Disable fade out during intense animation loops and
            // if an audioClip is somehow too short.
            if (_noFade || (pluginState & SpeakSoftlyPlugin.SettingState.FadeOut) == 0 || _fadeOutTimestamp < breathFadeLength) 
                _fadeOutTimestamp = audioSource.clip.length;
        }

        private void UpdateStats()
        {
            if (ChaControl == null) return;
            // Range (0.0 .. 1.0)

            _statsWeight = 0f;
            // Attributes net (-0.35 .. 0.4)
            var attribute = ChaControl.chaFile.parameter.attribute;
            // In all honesty I'd put +50 here for authenticity.
            if (attribute.bitch) _statsWeight += 20f;
            if (attribute.choroi) _statsWeight += 20f;
            if (attribute.hitori || attribute.kireizuki || attribute.dokusyo) _statsWeight -= 20f;
            if (attribute.majime) _statsWeight -= 15f;

            var heroine = ChaControl.GetHeroine() ?? (_hFlag != null ? _hFlag.GetLeadingHeroine() : null);

            if (heroine != null)
            {
                // From -20 to 40
                _statsWeight = ((int)heroine.HExperience - 1) * 20f;

                if (heroine.isGirlfriend)
                    _statsWeight += 20f;
#if KKS
                // Can't remember how OG Koik does this.
                else if (heroine. isFriend)
                    _statsWeight += 10f;
#endif
            }
            _statsWeight = Mathf.Clamp01(_statsWeight * 0.01f);
        }

        private void UpdateTargetVolume()
        {
            // Called continuously from Update() if audioClip is loaded.

            var hInfluence = _hFlag != null ?

                // Range (-0.3 .. 0.0)
                (Mathf.Clamp(_hFlag.GetOrgCount(), 0, 4) * -0.075f) +

                // Range (0.0 .. 0.3)
                (_hFlag.gaugeFemale * 0.003f) +

                // Range (0.? .. 0.7)
                ((_hVariableSpeed ? 
                _volumeAnimFloor + (_volumeAnimRange * (_hFlag.mode == HFlag.EMode.aibu ? _hFlag.motion : _hFlag.speedCalc)) :
                _volumeAnimCeiling) * 0.7f) :     

                // No hFlag – not an H scene
                0f;

            // Apply default volume provided by the game as a final modifier. It can be changed by the in-game settings for personalities' volume.
            _volumeTarget = (_volumeFloor + (_volumeRange * _statsWeight * hInfluence)) * _volumeDefault;
        }

        internal static void OnSetPlay(HFlag hFlag, string animName)
        {
            // Called when animator changes state (animation).
            // We analyze the next animation and set it value that will affect the volume.
#if DEBUG
            SpeakSoftlyPlugin.Logger.LogDebug($"{typeof(SpeakSoftlyCharaController).Name}.{MethodInfo.GetCurrentMethod().Name}:animName[{animName}]");
#endif
            if (_hFlag == null && hFlag != null)
            {
                _hFlag = hFlag;
                OnSettingChanged();
            }
            
            AnimImpact animImpact;
            if (hFlag.mode == HFlag.EMode.aibu)
            {
                animImpact = animName switch
                {
                    var s when s.StartsWith("Org", StringComparison.Ordinal) => AnimImpact.Max,
                    // Plays only for a moment, will cause small spike in the volume increase.
                    var s when s.EndsWith("_Touch", StringComparison.Ordinal) => AnimImpact.Max,
                    var s when s.EndsWith("_Idle", StringComparison.Ordinal) => AnimImpact.Medium | AnimImpact.VariableSpeed,
                    var s when s.StartsWith("K_", StringComparison.Ordinal) => AnimImpact.High | AnimImpact.VariableSpeed,
                    _ => AnimImpact.Low,
                };
            }
            else
            {
                animImpact = animName switch
                {
                    var s when s.EndsWith("InsertIdle", StringComparison.Ordinal) => AnimImpact.Low,
                    // Insert animation is momentary, and as volume catches up over time, the change is barely noticeable if at all.
                    var s when s.EndsWith("Insert", StringComparison.Ordinal) => AnimImpact.Max,
                    var s when s.EndsWith("WLoop", StringComparison.Ordinal) => AnimImpact.Low | AnimImpact.VariableSpeed | AnimImpact.NoFade,
                    var s when s.EndsWith("SLoop", StringComparison.Ordinal) => AnimImpact.Medium | AnimImpact.VariableSpeed | AnimImpact.NoFade,
                    var s when s.EndsWith("OLoop", StringComparison.Ordinal) => AnimImpact.High | AnimImpact.NoFade,
                    // PartnerClimax
                    var s when s.Contains("M_IN") => AnimImpact.High,
                    // PartnerClimax
                    var s when s.Contains("M_OUT") => AnimImpact.Medium,
                    // OwnClimax
                    var s when s.Contains("WF_IN") => AnimImpact.Max,
                    // OwnClimax
                    var s when s.Contains("SF_IN") => AnimImpact.Max,
                    // BothClimax
                    var s when s.Contains("WS_IN") => AnimImpact.Max,
                    // BothClimax
                    var s when s.Contains("SS_IN") => AnimImpact.Max,
                    // AfterClimaxInside
                    var s when s.EndsWith("IN_A", StringComparison.Ordinal) => AnimImpact.Low,
                    // AfterClimaxOutside
                    var s when s.EndsWith("OUT_A", StringComparison.Ordinal) => AnimImpact.Min,
                    // AfterClimaxInMouth
                    var s when s.EndsWith("Oral", StringComparison.Ordinal) => AnimImpact.Medium,
                    _ => AnimImpact.Min,
                };
            }

            // Base (0.2) + no attributes (0) + 慣れ (0.1) + girlfriend (0.1) = 0.4
            // Idle/InsertIdle = 5
            // Insert/Climax = 50 momentary spike (won't even be reached probably because of SmoothDamp)
            // WLoop = 0..15
            // SLoop = 0..25
            // OLoop = 40
            // AfterClimaxInside = 15
            // AfterClimaxOutside = 5
            // AfterClimaxMouth = 25
            _volumeAnimFloor = GetAnimInfluenceValue(animImpact - 1);
            _volumeAnimCeiling = GetAnimInfluenceValue(animImpact);
            _volumeAnimRange = _volumeAnimCeiling - _volumeAnimFloor;

            _hVariableSpeed = (animImpact & AnimImpact.VariableSpeed) != 0;
            _noFade = (animImpact & AnimImpact.NoFade) != 0;

            static float GetAnimInfluenceValue(AnimImpact anim)
            {
                // Don't want to expose them in the settings because bloat.
                return anim switch
                {
                    _ when (anim & AnimImpact.None) != 0 => 0f,
                    _ when (anim & AnimImpact.Min) != 0 => 0.1f,
                    _ when (anim & AnimImpact.Low) != 0 => 0.25f,
                    _ when (anim & AnimImpact.Medium) != 0 => 0.5f,
                    _ when (anim & AnimImpact.High) != 0 => 0.75f,
                    _ when (anim & AnimImpact.Max) != 0 => 1f,
                    _ => 0f
                };
            }
        }

        internal static void OnSettingChanged()
        {
            foreach (var instance in _instances)
            {
                instance.OnSettingChangedIntern();
            }
        }

        private void OnSettingChangedIntern()
        {
            // Stores value from static properties in instanced field, because quicker?
            // Atleast it becomes easier to read.

            _volumeSmoothDampTime = SpeakSoftlyPlugin.VolumeCatchUp.Value;
            // Double the voice multiplier for TalkScene.
            _volumeSceneVoiceMultiplier = _hFlag != null ? 1f : 2f;
            _volumeFloor = VolumeFloor;
            _volumeRange = Mathf.Clamp(VolumeCeiling - _volumeFloor, _volumeFloor, 1f);
        }

        [Flags]
        enum State
        {
            /// <summary>
            /// Bad state, wait for next call from a hook.
            /// </summary>
            None         = 0,
            /// <summary>
            /// AudioSource is there but audioClip is being loaded in async and will be there in few frames.
            /// </summary>
            Loading      = 1 << 0,
            /// <summary>
            /// AudioClip is being played.
            /// </summary>
            Active       = 1 << 1,
            /// <summary>
            /// Type of currently played audioClip is Voice or ShortGasp.
            /// </summary>
            Voice        = 1 << 2,
            /// <summary>
            /// Type of currently played audioClip is Breath.
            /// </summary>
            Breath = 1 << 3,
            /// <summary>
            /// Currently played audioClip is being Faded In.
            /// </summary>
            FadeIn       = 1 << 4,
            /// <summary>
            /// Currently played audioClip is being Faded Out.
            /// </summary>
            FadeOut = 1 << 5,
        }

        [Flags]
        private enum AnimImpact
        {
            None = 0,
            Min = 1 << 0,
            Low = 1 << 1,
            Medium = 1 << 2,
            High = 1 << 3,
            Max = 1 << 4,
            /// <summary>
            /// A way to track animations with variable speed.
            /// </summary>
            VariableSpeed = 1 << 5,
            /// <summary>
            /// Requires alternative fadeIn setup and no fadeOut.
            /// </summary>
            NoFade = 1 << 6,

        }
    }
}
