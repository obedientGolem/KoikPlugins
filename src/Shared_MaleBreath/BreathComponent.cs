using System;
using System.Collections.Generic;
using UnityEngine;
using static KK_MaleBreath.LoadGameVoice;
using Random = UnityEngine.Random;
using System.Collections;
using HarmonyLib;
using System.Reflection;

namespace KK_MaleBreath
{
    internal class BreathComponent : MonoBehaviour
    {
        internal static HFlag hFlag;
        internal static List<ChaControl> lstFemale;
        internal static ChaControl male;
        internal static HandCtrl handCtrl;


        internal static readonly List<BreathComponent> instances = [];
        private Transform _voiceTransform;
        private ChaControl _chara;
        private BreathType _currentBreath;
        private VoiceType _currentVoice;
        private bool _inTransition;
        private bool _clickPending;
        private bool _ownVoiceActive;
        private bool _partnerVoiceWasActive;
        private Transform _mouth;
        // Saved reference for VRCamera to fallback to when in PovMode of this character.
        private Transform _VRCameraTransform;
        // Cached anim type to play voice after
        private ClimaxKind _climaxKind;

        private HFlag.EMode _eMode;

        private bool _charaActive;
        private static AnimState _animState;

        private int _voiceCooldown;
        private bool[] _encroachingVoices;
        private Transform GetMouth
        {
            get
            {
                if (_mouth == null)
                {
                    UpdateMouthTransform();
                    // KK(S)_VR might have late init, substitute meanwhile.
                    if (_mouth == null) return this.transform;
                }
                return _mouth;
            }
        }

        internal void UpdateMouthTransform()
        {
            _charaActive = _chara.objTop != null && _chara.objTop.activeSelf && _chara.visibleAll;

            if (MaleBreathController.IsVR)
            {
                if (_charaActive && (!MaleBreathController.IsPov || MaleBreathController.GetPovChara != _chara))
                {
                    _mouth = _chara.dictRefObj[ChaReference.RefObjKey.a_n_mouth].transform;
                }
                else
                {
                    FindVRCamera();
                }
            }
            else
            {
                _mouth = _charaActive ? _chara.dictRefObj[ChaReference.RefObjKey.a_n_mouth].transform : hFlag.ctrlCamera.transform;
            }
        }

        internal static void OnSetPlay(string animName)
        {
            foreach (var instance in instances)
            {
                instance.UpdateAnimState(animName);
            }
        }

        private void UpdateAnimState(string animName)
        {
            _animState = animName switch
            {
                var s when s.EndsWith("InsertIdle", StringComparison.Ordinal) => AnimState.IdleInside,
                var s when s.EndsWith("Insert", StringComparison.Ordinal) => AnimState.Insert,
                "Idle" => AnimState.IdleOutside,
                var s when s.EndsWith("WLoop", StringComparison.Ordinal) => AnimState.WeakLoop | AnimState.ActionLoop,
                var s when s.EndsWith("SLoop", StringComparison.Ordinal) => AnimState.StrongLoop | AnimState.ActionLoop,
                var s when s.EndsWith("OLoop", StringComparison.Ordinal) => AnimState.OrgasmLoop | AnimState.ActionLoop,
                var s when s.StartsWith("K_L", StringComparison.Ordinal) => AnimState.KissLoop,
                var s when s.EndsWith("M_IN_Start", StringComparison.Ordinal) => AnimState.OwnClimax,
                var s when s.EndsWith("M_OUT_Start", StringComparison.Ordinal) => AnimState.OwnClimax,
                var s when s.EndsWith("WF_IN_Start", StringComparison.Ordinal) => AnimState.PartnerClimax,
                var s when s.EndsWith("SF_IN_Start", StringComparison.Ordinal) => AnimState.PartnerClimax,
                var s when s.EndsWith("WS_IN_Start", StringComparison.Ordinal) => AnimState.BothClimax,
                var s when s.EndsWith("SS_IN_Start", StringComparison.Ordinal) => AnimState.BothClimax,
                var s when s.EndsWith("IN_A", StringComparison.Ordinal) => AnimState.AfterClimaxInside,
                var s when s.EndsWith("OUT_A", StringComparison.Ordinal) => AnimState.AfterClimaxOutside,
                var s when s.EndsWith("Oral", StringComparison.Ordinal) => AnimState.AfterClimaxInMouth,
                _ => AnimState.IdleOutside,
            };

#if DEBUG
            MaleBreath.Logger.LogDebug($"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}:animName[{animName}] animState[{_animState}]");
#endif
            switch (_animState)
            {
                case AnimState.OwnClimax:
                    _climaxKind = ClimaxKind.Self;
                    _currentVoice = VoiceType.ClimaxSelf;
                    SetVoiceCooldown(10);
                    break;
                case AnimState.PartnerClimax:
                    _climaxKind = ClimaxKind.Partner;
                    _currentVoice = VoiceType.ClimaxPartner;
                    SetVoiceCooldown();
                    break;
                case AnimState.BothClimax:
                    _climaxKind = ClimaxKind.Both;
                    _currentVoice = VoiceType.ClimaxBoth;
                    SetVoiceCooldown();
                    break;
                case AnimState.Insert:
                    DoVoice();
                    break;
            }
        }

        private bool IsAnimState(AnimState animState) => (_animState & animState) != 0;

        private void FindVRCamera()
        {
            if (_VRCameraTransform == null)
            {
                var type = AccessTools.TypeByName("VRGIN.Core.VR");
                if (type != null)
                {
                    var getter = AccessTools.PropertyGetter(type, "Camera");
                    if (getter != null)
                    {
                        var func = AccessTools.MethodDelegate<Func<MonoBehaviour>>(getter);
                        _VRCameraTransform = func?.Invoke().transform;
                    }
                }
            }
            _mouth = _VRCameraTransform.Find("MouthGuide");
        }

        internal bool IsChara(ChaControl chara) => chara == _chara;



        internal static void OnChangeAnimator()
        {
            foreach (var instance in instances)
            {
                instance.OnChangeAnimatorUpdate();
            }
        }

        // Not static because instance fields are faster to access
        private void OnChangeAnimatorUpdate()
        {
            _eMode = hFlag.mode switch
            {
                HFlag.EMode.sonyu3P or HFlag.EMode.sonyu3PMMF => HFlag.EMode.sonyu,
                HFlag.EMode.houshi3P or HFlag.EMode.houshi3PMMF => HFlag.EMode.houshi,
                _ => hFlag.mode
            };
            UpdateMouthTransform();
        }

        internal static bool IsFast
        {
            get
            {
                // Will fail if aibuMaxSpeed is modified.
                return hFlag.mode switch
                {
                    HFlag.EMode.aibu => hFlag.speed > 0.75f,
                    _ => hFlag.speedCalc > 0.5f
                };
            }
        }

        internal static bool IsPartnerVoiceActive
        {
            get
            {
                foreach (var chara in lstFemale)
                {
                    if (chara.asVoice != null && !chara.asVoice.name.StartsWith("h_ko_", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private void Awake()
        {
            instances.Add(this);

            _chara = GetComponent<ChaControl>();
            UpdateMouthTransform();

            _encroachingVoices = new bool[lstFemale.Count];

            StartCoroutine(OncePerSecondCo());

            OnChangeAnimatorUpdate();
        }

        private void OnDestroy()
        {
            instances.Remove(this);
            if (_voiceTransform != null)
            {
                Destroy(_voiceTransform.gameObject);
            }
        }

        private HFlag.TimeWait GetTimeWait()
        {
            if (hFlag == null) return null;
            return _eMode switch
            {
                HFlag.EMode.aibu => hFlag.voice.timeAibu,
                HFlag.EMode.houshi => hFlag.voice.timeHoushi,
                HFlag.EMode.sonyu => hFlag.voice.timeSonyu,

                // Deeply disturbing experience.
                //case HFlag.EMode.masturbation:
                //    return _hFlag.timeMasturbation.timeIdle - _hFlag.timeMasturbation.timeIdleCalc;
                //case HFlag.EMode.lesbian:
                //    return hFlag.timeLesbian.timeIdle - hFlag.timeLesbian.timeIdleCalc;
                _ => null,
            };
        }

        /// <summary>
        /// Check if lstFemale[0] voice has more then half of the time to fire on its timer.
        /// </summary>
        private bool IsPartnerVoiceRecent()
        {
            var timeWait = GetTimeWait();
            if (timeWait == null) return true;
            return timeWait.timeIdle - timeWait.timeIdleCalc > timeWait.timeIdle * 0.5f;
        }

        private bool IsOwnVoiceRecent => _voiceCooldown > MaleBreath.AverageVoiceCooldown.Value * 0.5f;

        /// <summary>
        /// We increase hFlag's TimeWait if it gets too close to firing voiceProc.
        /// </summary>
        private void PostponeVoice()
        {
            var timeWait = GetTimeWait();
            if (timeWait == null) return;
            if (timeWait.timeIdle - timeWait.timeIdleCalc < 2f)
            {
                _encroachingVoices[0] = true;
                timeWait.timeIdleCalc = timeWait.timeIdle - 2f;
            }
        }

        private void PlayPartnerVoice()
        {
            // Immediate response feels unnatural. 
            var timeWait = GetTimeWait();
            if (timeWait == null) return;
            timeWait.timeIdleCalc = timeWait.timeIdle - (0.25f + Random.value * 0.5f);
        }

        private bool IsOverlap() => KKAPI.SceneApi.GetIsOverlap();

        private IEnumerator OncePerSecondCo()
        {
            // Here we once a second update breathType.
            // And count cooldown of our voice.
            var waitForSecond = new WaitForSeconds(1f);
            while (true)
            {
                yield return waitForSecond;

                if (IsOverlap()) continue;

                if (!_ownVoiceActive)
                {
                    if (IsPartnerVoiceActive)
                    {
                        _partnerVoiceWasActive = true;
                    }
                    else if (_voiceCooldown-- < 0 && SetVoice())
                    {
                        continue;
                    }
                    SetBreath();
                }
                else
                {
                    PostponeVoice();
                }
            }
        }

        private void Update()
        {
            // Here we wait for a special event if it's on the horizon,
            // and immediately set voice if previous has ended.
            //if (_waitForTrigger && CatchTrigger())
            //{
            //    DoVoice();
            //    _waitForTrigger = false;
            //}
            if (_voiceTransform == null && !IsOverlap())
            {
                if (RecoverTransform()) return;

                if (_partnerVoiceWasActive && !IsPartnerVoiceActive)
                {
                    _partnerVoiceWasActive = false;
#if DEBUG
                    MaleBreath.Logger.LogDebug($"Set:OwnResponse:{!IsOwnVoiceRecent}");
#endif
                    if (!IsOwnVoiceRecent && SetVoice()) return;
                }

                if (_inTransition)
                {
                    _inTransition = false;
                    SetBreath();
                }
                else
                {
                    DoBreath();
                }
            }
        }

        private bool RecoverTransform()
        {
            if (_chara.asVoice != null)
            {
                _voiceTransform = _chara.asVoice.transform;
                return true;
            }
            return false;
        }

        private void SetVoiceCooldown(int value = 0)
        {
            var avg = value != 0 ? value : MaleBreath.AverageVoiceCooldown.Value;
            _ownVoiceActive = true;
            _voiceCooldown = Random.Range(Mathf.Abs(avg - 5), avg + 6);
        }

        private void DoClick()
        {
            if (_clickPending)
            {
                _clickPending = false;
                MaleBreathController.Click();
            }
        }

        private void DeactivateVoice()
        {
            if (_ownVoiceActive)
            {
                _ownVoiceActive = false;
                for (var i = 0; i < _encroachingVoices.Length; i++)
                {
                    if (_encroachingVoices[i])
                    {
                        _encroachingVoices[i] = false;
                        PlayPartnerVoice();
                        return;
                    }
                }
                if (Random.value < 0.67f && !IsPartnerVoiceRecent()) PlayPartnerVoice();
#if DEBUG
                MaleBreath.Logger.LogDebug($"Set:PartnerResponse:{!IsPartnerVoiceRecent()}");
#endif
            }
        }

        private void DoBreath()
        {
            DoClick();
            _voiceTransform = _chara.objTop.activeSelf && _chara.visibleAll ? PlayBreath(_currentBreath, chara: _chara) 
                : PlayBreath(_currentBreath, breathTransform: GetMouth);
            DeactivateVoice();
        }

        private bool DoVoice()
        {
            DoClick();
            var transform = _chara.objTop.activeSelf && _chara.visibleAll ? PlayVoice(_currentVoice, chara: _chara)
                : PlayVoice(_currentVoice, voiceTransform: GetMouth); // VR.Camera.transform);
            if (transform != null)
            {
                _voiceTransform = transform;
                SetVoiceCooldown();
                return true;
            }
            return false;
        }

        /// <returns>False if no voice was found.</returns>
        public bool SetTriggerVoice(VoiceType voiceType)
        {
#if DEBUG
            MaleBreath.Logger.LogDebug($"SetSpecialVoice:{voiceType}");
#endif
            CorrectVoice(ref voiceType);
            _currentVoice = voiceType;
            var result = DoVoice();
            _clickPending = true;
            return result;
        }

        private void CorrectVoice(ref VoiceType voiceType)
        {
            if (voiceType == VoiceType.BeforeClimaxSelf)
            {
                if (hFlag.finish == HFlag.FinishKind.orgW || hFlag.finish == HFlag.FinishKind.orgS)
                {
                    voiceType = VoiceType.BeforeClimaxPartner;
                }
                else if (hFlag.finish == HFlag.FinishKind.sameW || hFlag.finish == HFlag.FinishKind.sameS)
                {
                    voiceType = VoiceType.BeforeClimaxBoth;
                }
            }
        }

        private bool SetVoice()
        {
            var voiceType = VoiceType.Idle;
            switch (_eMode)
            {
                //case HFlag.EMode.aibu:
                //    if (IsKissAnim(animName)) ManageKiss();
                //    break;
                //case HFlag.EMode.houshi:
                //case HFlag.EMode.houshi3P:
                //case HFlag.EMode.houshi3PMMF:
                //    if (IsWeakLoop(animName))
                //    {
                //        breathType = IsFast ? BreathType.ResistWeakFast : BreathType.ResistWeakSlow;
                //    }
                //    else if (IsStrongLoop(animName) || IsOrgasmLoop(animName))
                //    {
                //        breathType = IsFast ? BreathType.ResistStrongFast : BreathType.ResistStrongSlow;
                //    }
                //    else if (IsEndInMouth(animName) || IsEndOutside(animName))
                //    {
                //        breathType = BreathType.Strained;
                //    }
                //    break;
                case HFlag.EMode.sonyu:
                    if (IsAnimState(AnimState.AfterClimaxInside) || IsAnimState(AnimState.AfterClimaxOutside))
                    {
                        voiceType = _climaxKind switch
                        {
                            ClimaxKind.Self => VoiceType.AfterClimaxSelf,
                            ClimaxKind.Partner => VoiceType.AfterClimaxPartner,
                            _ => VoiceType.AfterClimaxBoth
                        };
                    }
                    else if (IsAnimState(AnimState.IdleInside))
                    {
                        voiceType = hFlag.isAnalPlay ? VoiceType.InsertIdleAnal : VoiceType.InsertIdle;
                    }
                    else if (IsAnimState(AnimState.ActionLoop))
                    {
                        // Grab SensH ceiling.
                        
                        if (hFlag.gaugeMale > 70f && hFlag.gaugeFemale > 70f)
                        {
                            voiceType = IsFast ? VoiceType.LateLoopBothFast : VoiceType.LateLoopBothSlow;
                        }
                        else if (hFlag.gaugeMale > 70f)
                        {
                            voiceType = IsFast ? VoiceType.LateLoopSelfFast : VoiceType.LateLoopSelfSlow;
                        }
                        else if (hFlag.gaugeFemale > 70f)
                        {
                            voiceType = IsFast ? VoiceType.LateLoopPartnerFast : VoiceType.LateLoopPartnerSlow;
                        }
                        else
                        {
                            voiceType = IsFast ? VoiceType.LoopFast : VoiceType.LoopSlow;
                        }

                    }
                    break;
            }
            _currentVoice = voiceType;
            return DoVoice();
        }

        private void SetBreath()
        {
            var breathType = BreathType.Normal;
            switch (_eMode)
            {
                case HFlag.EMode.aibu:
                    if (handCtrl.IsKissAction())
                    {
                        breathType = _currentBreath != BreathType.KissExclamation && Random.value < 0.2f ? BreathType.KissExclamation 
                            : IsFast ? BreathType.KissFast : BreathType.KissSlow;
                    }
                    else if (handCtrl.GetUseAreaItemActive() != -1 && handCtrl.useItems[2] != null)
                    {
                        breathType = IsFast ? BreathType.LickFast : BreathType.LickSlow;
                    }
                    break;
                case HFlag.EMode.houshi:
                    if (IsAnimState(AnimState.WeakLoop))
                    {
                        breathType = IsFast ? BreathType.ResistWeakFast : BreathType.ResistWeakSlow;
                    }
                    // Skip weak loop
                    else if (IsAnimState(AnimState.ActionLoop))
                    {
                        breathType = IsFast ? BreathType.ResistStrongFast : BreathType.ResistStrongSlow;
                    }
                    else if (IsAnimState(AnimState.AfterClimaxInMouth) || IsAnimState(AnimState.AfterClimaxOutside))
                    {
                        breathType = BreathType.Strained;
                    }
                    break;
                case HFlag.EMode.sonyu:
                    if (IsAnimState(AnimState.WeakLoop))
                    {
                        if (handCtrl.IsKissAction())
                        {
                            breathType = _currentBreath != BreathType.KissExclamation && Random.value < 0.2f ? BreathType.KissExclamation 
                                : IsFast ? BreathType.KissLoopWeakFast : BreathType.KissLoopWeakSlow;
                        }
                        else if (handCtrl.GetUseAreaItemActive() != -1 && handCtrl.useItems[2] != null)
                        {
                            breathType = IsFast ? BreathType.SuckWeakFast : BreathType.SuckWeakSlow;
                        }
                        else
                        {
                            // Grab SensH ceiling.
                            if (hFlag.gaugeMale > 70f)
                            {
                                breathType = IsFast ? BreathType.LateLoopWeakFast : BreathType.LateLoopWeakSlow;
                            }
                            else
                            {
                                breathType = IsFast ? BreathType.LoopWeakFast : BreathType.LoopWeakSlow;
                            }
                        }
                    }
                    // Skip weak loop
                    else if (IsAnimState(AnimState.ActionLoop))
                    {
                        if (handCtrl.IsKissAction())
                        {
                            breathType = _currentBreath != BreathType.KissExclamation && Random.value < 0.2f ? BreathType.KissExclamation 
                                : IsFast ? BreathType.KissLoopStrongFast : BreathType.KissLoopStrongSlow;
                        }
                        else if (handCtrl.GetUseAreaItemActive() != -1 && handCtrl.useItems[2] != null)
                        {
                            breathType = IsFast ? BreathType.SuckStrongFast : BreathType.SuckStrongSlow;
                        }
                        else
                        {
                            if (hFlag.gaugeMale > 70f)
                            {
                                breathType = IsFast ? BreathType.LateLoopStrongFast : BreathType.LateLoopStrongSlow;
                            }
                            else
                            {
                                breathType = IsFast ? BreathType.LoopStrongFast : BreathType.LoopStrongSlow;
                            }
                        }
                        
                    }
                    //else if (IsAfterClimaxInside(animName) || IsAfterClimaxOutside(animName))
                    //{
                    //    breathType = BreathType.Strained;
                    //}
                    else
                    {
                        if (handCtrl.IsKissAction())
                        {
                            breathType = _currentBreath != BreathType.KissExclamation && Random.value < 0.2f ? BreathType.KissExclamation
                                : BreathType.KissSlow;
                        }
                        else if (handCtrl.GetUseAreaItemActive() != -1 && handCtrl.useItems[2] != null)
                        {
                            breathType = BreathType.LickSlow;
                        }
                    }
                    break;
            }
            if (breathType == _currentBreath) return;
            if (_inTransition)
            {
                // Instead of jumping from non-normal voice back to normal, we go:
                // non-normal -> strained -> semi-strained -> normal.
                // The other way we go straight to non-normal.
#if DEBUG
                MaleBreath.Logger.LogDebug($"Transition:from[{_currentBreath}] to[{breathType}]");
#endif
                if (breathType > _currentBreath)
                {
#if DEBUG
                    MaleBreath.Logger.LogDebug($"Transition:OverrideWithHigherVoice");
#endif
                    _currentBreath = breathType;
                    _inTransition = false;
                    DoBreath();
                }
                return;
            }
            ApplyTransition(ref breathType);
            _currentBreath = breathType;
            DoBreath();
        }

        private void ApplyTransition(ref BreathType targetType)
        {
#if DEBUG
            MaleBreath.Logger.LogDebug($"Transition:Attempt from[{_currentBreath}] to[{targetType}]");
#endif
            var original = targetType;
            if (targetType == BreathType.Normal)
            {
                if (_currentBreath > BreathType.Strained)
                {
                    targetType = BreathType.Strained;
                }
                else if (_currentBreath == BreathType.Strained)
                {
                    targetType = BreathType.StrainedToNormal;
                }
            }
            if (targetType != original)
            {
#if DEBUG
                MaleBreath.Logger.LogDebug($"Transition:Success from[{_currentBreath}] to[{targetType}]");
#endif
                _inTransition = true;
            }
        }

        private enum ClimaxKind
        {
            Self,
            Partner,
            Both
        }

        [Flags]
        private enum AnimState
        {
            None = 0,
            Insert = 1 << 0,
            IdleInside = 1 << 1,
            IdleOutside = 1 << 2,
            ActionLoop = 1 << 3,
            WeakLoop = 1 << 4,
            StrongLoop = 1 << 5,
            OrgasmLoop = 1 << 6,
            KissLoop = 1 << 7,
            OwnClimax = 1 << 8,
            PartnerClimax = 1 << 9,
            BothClimax = 1 << 10,
            AfterClimaxInside = 1 << 11,
            AfterClimaxOutside = 1 << 12,
            AfterClimaxInMouth = 1 << 13,
        }
    }
}
