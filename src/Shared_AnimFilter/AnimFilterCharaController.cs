using ADV.Commands.Base;
using KKAPI;
using KKAPI.Chara;
using RootMotion;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace AnimFilter
{
    [DefaultExecutionOrder(20000)]
    internal class AnimFilterCharaController : CharaCustomFunctionController
    {
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private readonly List<AnimationClipPlayable> clips = [];
        private GameObject _clone;
        private Animator _animator;

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {

        }

        protected override void OnReload(GameMode currentGameMode, bool maintainState)
        {
            base.OnReload(currentGameMode, maintainState);

            _animator = ChaControl.animBody;

            if (ChaControl.sex == 0) enabled = false;

        }
        //public struct SmoothJob : IAnimationJob
        //{
        //    public SmoothJob(TransformStreamHandle handle, Transform asset)
        //    {
        //        _handle = handle;

        //        _basePos = asset.localPosition;
        //        _baseRot = asset.localRotation;
        //        _baseScl = asset.localScale;
        //    }

        //    public TransformStreamHandle _handle;

        //    private Vector3 _basePos;
        //    private Quaternion _baseRot;
        //    private Vector3 _baseScl;



        //    public void ProcessAnimation(AnimationStream stream)
        //    {
        //        var currentPos = bone.GetPosition(stream);
        //        var currentRot = bone.GetRotation(stream);


        //    }

        //}


        public bool DevPrepare
        {
            get => _devPrepare;
            set
            {
                _devPrepare = value;

                if (value)
                {
                    Clone();
                    PreparePlayable();
                }
            }
        }
        public bool DevUpdate
        {
            get => _verbotenUpdate;
            set
            {
                _verbotenUpdate = value;
                if (value)
                {
                    var animator = ChaControl.animBody;

                    var state = animator.GetCurrentAnimatorStateInfo(0);

                    _prevNormTime = state.normalizedTime % 1;
                }
            }
        }

        private bool _devPrepare;
        private bool _verbotenUpdate;
        private bool _devLateUpdate;
        private float _prevNormTime;

        protected override void Update()
        {
            base.Update();

            var animator = ChaControl.animBody;

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (DevUpdate)
            {
                
                var nextNormTime = _prevNormTime + ((state.speed * Time.deltaTime) / state.length);
                if (nextNormTime >= 1f)
                    nextNormTime -= 1f;

                animator.Play("WLoop", 0, Power(nextNormTime));
                AnimFilterPlugin.Logger.LogDebug($"" +
                    $"Dev:Update: nextNormTime[{nextNormTime:F3}] normTime[{state.normalizedTime:F3}] fakeNormTime[{_prevNormTime:F3}] smoothed[{Smooth(nextNormTime):F3}] speed[{state.speed:F3}] len[{state.length:F3}]");
                _prevNormTime = nextNormTime;
            }
            else
            {
                AnimFilterPlugin.Logger.LogDebug($"Dev:Update: normTime[{state.normalizedTime:F3}] speed[{state.speed:F3}] len[{state.length:F3}]");
            }

            static float BumpSmoothStep(float t) => 4f * t * t * (3f - 2f * t) * (1f - t * t * (3f - 2f * t));
            static float Smooth(float t) => t * t * (3f - 2f * t);
            static float SmoothStep2(float t)
            {
                if (t < 0.5f)
                {
                    var u = t * 2f;
                    return 0.5f * (u * u * (3f - 2f * u));
                }
                else
                {
                    var u = (t - 0.5f) * 2f;
                    return 0.5f + 0.5f * (u * u * (3f - 2f * u));
                }
            }
            static float Sine(float t)
            {
                if (t < 0.5f)
                {
                    var u = t * 2f;
                    return 0.5f * Mathf.Sin(u * 0.5f * Mathf.PI);
                }
                else
                {
                    return t;
                }
            }
            static float Power(float t)
            {
                return t * t;
                if (t < 0.5f)
                {
                    var u = t * 2f;
                    return 0.5f * (u * u);
                }
                else
                {
                    return t;
                }
            }

        }
        private void LateUpdate()
        {
            //if (_devLateUpdate) EvaluatePlayable();
        }

        private void Clone()
        {
            if (_clone != null) return;

            var prefab = GameObject.Find("p_cf_body_bone");

            if (prefab == null)
                throw new NullReferenceException();

            var clone = Instantiate(prefab);

            clone.SetActive(false);
            clone.name = "[AnimFilterPlugin] – " + ChaControl.name + "'s objAnim clone";
            _clone = clone;
        }

        internal void OnAnimatorStateChange()
        {
            
        }

        private void PreparePlayable()
        {
            clips.Clear();

            var cloneAnimator = _clone.GetComponent<Animator>();

            var graph = PlayableGraph.Create("CustomAnimationGraph");

            var output = AnimationPlayableOutput.Create(graph, "AnimationOutput", cloneAnimator);

            var curClips = _animator.GetCurrentAnimatorClipInfo(0);

            var mixer = AnimationMixerPlayable.Create(graph, curClips.Length);

            for (var i = 0; i < curClips.Length; i++)
            {
                var playable = AnimationClipPlayable.Create(graph, curClips[i].clip);

                graph.Connect(playable, 0, mixer, i);

                clips.Add(playable);
            }
            output.SetSourcePlayable(mixer);

            _graph = graph;
            _mixer = mixer;

            _graph.Play();
        }

        public void EvaluatePlayable()
        {
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            var normTime = state.normalizedTime;

            var curClips = _animator.GetCurrentAnimatorClipInfo(0);

            for (var i = 0; i < curClips.Length; i++)
            {
                var clip = clips[i];
                clip.SetTime(normTime * clip.GetAnimationClip().length);
                clip.SetSpeed(0);
                _mixer.SetInputWeight(i, curClips[i].weight);
            }

            _graph.Evaluate();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            _graph.Destroy();
        }
    }
}
