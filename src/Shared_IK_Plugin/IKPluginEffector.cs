using KKAPI;
using KKAPI.Chara;
using KKAPI.MainGame;
#if VR
using KK.RootMotion;
using KK.RootMotion.FinalIK;
#elif NOVR
using RootMotion;
using RootMotion.FinalIK;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UniRx;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace IKPlugin
{
    internal class IKPluginEffector
    {
        const float OneThird = 1f / 3f;
        const float TwoThirds = 2f / 3f;

        private readonly Dictionary<IKEffector, IKPluginAmplifier> _ikTargets = [];
        private readonly FullBodyBipedIK _fbbik;
        private readonly Animator _animator;
        private readonly Transform _waistF;

        private float _weight = 1f;
        private float _prevNormTime;

        private bool _trySampleDist;
        private bool _sampleDist;
        private int _distEvaluationStep;
        private readonly float[] _distances = new float[3];
        private readonly int[] _subNormIdxs = [0, 1, 2];

        private readonly DistanceSampler _distanceSampler;
        private readonly VelocitySampler _velocitySampler;
        private VelocityTracker _velocityTracker;

        private bool _devFemaleLead;


        internal IKPluginEffector(ChaControl chara)
        {
            if (chara == null) throw new ArgumentNullException();

            var fbbik = chara.objAnim.GetComponent<FullBodyBipedIK>();

            var animator = chara.animBody;

            var effectors = fbbik.solver.effectors;

            for (var i = 0; i < effectors.Length; i++)
            {
                //if (i != (int)EffectorType.ThighR && i != (int)EffectorType.ThighL) continue;

                var effector = effectors[i];

                _ikTargets.Add(effector, new(effector.bone, fbbik.transform, GetWeight(i)));
            }

            _fbbik = fbbik;
            _animator = animator;

            var proc = Component.FindObjectOfType<HSceneProc>();
            var lead = GameAPI.GetCurrentHeroine();

            if (proc == null || lead == null) return;
            if (lead.chaCtrl != chara) return;

            fbbik.solver.OnPreUpdate += OnLateUpdate;

            var waistM = proc.male.GetComponentsInChildren<Transform>()
                .Where(t => t.name.Equals("cf_j_waist02"))
                .FirstOrDefault();

            _waistF = chara.GetComponentsInChildren<Transform>()
                .Where(t => t.name.Equals("cf_j_waist02"))
                .FirstOrDefault();

            //_velocitySampler = new([waistM, _waistF], 30);
            _velocityTracker = new([waistM, _waistF], 100f, 25f);
            _distanceSampler = new(waistM, _waistF, 30);

            static float GetWeight(int i)
            {
                return (EffectorType)i switch
                {
                    EffectorType.Spine => (2f / 3f),
                    EffectorType.ShldrL or EffectorType.ShldrR => (1f / 3f),
                    _ => 1f
                };
            }
        }

        public void UpdateAmps(float damp)
        {
            foreach (var p in _ikTargets)
            {
                p.Value._defaultDamping = damp;
            }
        }
        private Vector3 _devWeights = new Vector3(1f, TwoThirds, 1f);

        private void TrySampleDistance() => _trySampleDist = true;

        private PhaseState _state;
        private float _timeCeil = OneThird;
        private float _timeFloor = 0f;
        internal void OnLateUpdate()
        {
            var deltaTime = Time.deltaTime;
            var weight = _weight;

            if (deltaTime == 0f || weight == 0f) return;

            var state = _animator.GetCurrentAnimatorStateInfo(0);

            var f = state.normalizedTime;
            var t = f - Mathf.Floor(f);

            // Run distance sampling for a full cycle,
            // starting from the very beginning.
            if (_trySampleDist && t < _prevNormTime)
            {
                _trySampleDist = false;
                _sampleDist = true;
            }
            _prevNormTime = t;

            //var deltaTimeInv = 1f / deltaTime;


            // 3 Phases: In, Mid, Out.
            // Aggregate velocities while moving In,
            // then add their avg to the ik handlers
            if (t > _timeFloor && t < _timeCeil)
            {
                if (_state == PhaseState.Outside)
                {
                    IKPlugin.Logger.LogInfo($"Dev:OnLateUpdate:In:Reset");

                    _state = PhaseState.Inside;
                    //_velocitySampler.Clear();
                    _velocityTracker.Clear();
                }
                else
                // Reciprocal because the values are tiny with deltaTime
                // can't work with values written in scientific notation.
                //_velocitySampler.Sample(deltaTimeInv);
                _velocityTracker.Update(deltaTime);
            }
            else if (_state == PhaseState.Inside)
            {

                _state = PhaseState.Outside;

                //var vec = Vector3.zero;
                //foreach (var v in _velocitySampler.GetAverages())
                //{
                //    IKPlugin.Logger.LogDebug($"Dev:OnLateUpdate:Velocity: v[{v:F3}]");
                //    vec += v;
                //}

                var velocities = _velocityTracker.GetVelocities();
                var vec = velocities[_devFemaleLead ? 1 : 0];
                //var vec = _velocitySampler.GetAverages()[_devFemaleLead ? 1 : 0];

                IKPlugin.Logger.LogInfo($"Dev:OnLateUpdate:Out:Reset " +
                    $"vel0({velocities[0].x:F3},{velocities[0].y:F3},{velocities[0].z:F3}) " +
                    $"vel1({velocities[1].x:F3},{velocities[1].y:F3},{velocities[1].z:F3})");
                //vec = _waistF.up * vec.magnitude;

                foreach (var kv in _ikTargets)
                {
                    kv.Value.AddVelocity(vec);
                }
            }

            //var t0 = SegmentValue(t, _subNormIdxs[(int)SubTime.In]);
            var t1 = SegmentValue(t, _subNormIdxs[(int)SubTime.Mid]);
            //var t2 = SegmentValue(t, _subNormIdxs[(int)SubTime.Out]);

            IKPlugin.Logger.LogDebug($"Dev:OnLateUpdate:Out: t[{t:F2}] t1[{t1:F2}]");

            foreach (var kv in _ikTargets)
            {
                var ikHandler = kv.Value;
                kv.Key.positionOffset += Vector3.Scale(ikHandler.GetPosOffsetDamping(deltaTime, t1), _devWeights);
            }

            if (_sampleDist) SampleDistance(t);

            static float SegmentValue(float t, int segmentIdx)
            {
                // Shift so this segment starts at 0
                var start = segmentIdx * OneThird;
                var local = (t - start) * (1f / OneThird);

                // Scale to [0, 3] → we only care about [0, 2]
                var x = Mathf.Clamp(local, 0f, 2f);

                // Ping-pong shape: 0→1→0
                return x <= 1f ? x : 2f - x;
            }
            //float SegmentValueNoLimit(float t, int idx, int segmentCount)
            //{
            //    float segLen = 1f / segmentCount;
            //    float local = (t - idx * segLen) / segLen;

            //    float x = Mathf.Clamp(local, 0f, 2f);
            //    return x <= 1f ? x : 2f - x;
            //}

            //float Smooth(float t)
            //{
            //    return _smoothing switch
            //    {
            //        Smoothing.Linear => t,
            //        Smoothing.Parabola => ParabolaBump(t),
            //        Smoothing.Power => PowerBump(t),
            //        Smoothing.Smooth => SmoothStepBump(t),
            //        _ => throw new NotImplementedException()
            //    };
            //}


            //static float PowerBump(float t)
            //{
            //    if (t < 0.5f)
            //    {
            //        return 4f * (t * t);
            //    }
            //    else
            //    {
            //        var u = 1f - t;
            //        return 4f * (u * u);
            //    }
            //}
            //static float ParabolaBump(float t) => 4f * t * (1f - t);

            //static float SmoothStepBump(float t) => 4f * t * t * (3f - 2f * t) * (1f - t * t * (3f - 2f * t));
        }

        private void SampleDistance(float t)
        {
            // Divide animation cycle in 3 parts and sample avg distance
            // between charas, evaluate it, find the closes part,
            // and make preceding one the part of velocity accumulation.

            if (_distEvaluationStep == 0)
            {
                if (t >= OneThird)
                {
                    _distances[0] = _distanceSampler.GetAverage();
                    _distEvaluationStep = 1;
                }
                else
                    _distanceSampler.Sample();
            }
            else if (_distEvaluationStep == 1)
            {
                if (t >= TwoThirds)
                {
                    _distances[1] = _distanceSampler.GetAverage();
                    _distEvaluationStep = 2;
                }
                else
                    _distanceSampler.Sample();
            }
            else
            {
                // If we are at 1 or overshoot and back to 0.
                if (t < TwoThirds || t >= 1f)
                {
                    _distances[2] = _distanceSampler.GetAverage();

                    var minIdx = 0;
                    for (var i = 0; i < _distances.Length; i++)
                    {
                        if (_distances[i] < _distances[minIdx])
                            minIdx = i;
                    }
                    IKPlugin.Logger.LogInfo($"Dev:OnLateUpdate:Lens 0[{_distances[0]:F6}] 1[{_distances[1]:F6}] 2[{_distances[2]:F6}] minIdx[{minIdx}]");

                    _distEvaluationStep = 0;

                    var len = _distances.Length;

                    static int Mod(int x, int m) => (x % m + m) % m;

                    _subNormIdxs[(int)SubTime.In] = Mod(minIdx - 1, len);
                    _subNormIdxs[(int)SubTime.Mid] = minIdx;
                    _subNormIdxs[(int)SubTime.Out] = Mod(minIdx + 1, len);


                    _timeCeil = minIdx == 0 ? 1f : minIdx * (1f / 3f);
                    _timeFloor = _subNormIdxs[(int)SubTime.In] * (1f / 3f);
                    _sampleDist = false;

                    _distanceSampler.Clear();
                }
                else
                    _distanceSampler.Sample();
            }
        }

        internal void SetSpeed(float t)
        {
            foreach (var kv in _ikTargets)
            {
                kv.Value.SetSpeed(t);
            }
        }

        private struct IKPair
        {
            internal IKPair(IKEffector effector, IKPluginAmplifier amp)
            {
                this.effector = effector;
                this.amp = amp;
            }

            internal readonly IKEffector effector;
            internal IKPluginAmplifier amp;
        }

        internal void OnDestroy()
        {
            if (_fbbik == null || _fbbik.solver == null) return;

            _fbbik.solver.OnPreUpdate -= OnLateUpdate;
        }

        // Can't remember where RootMotion.FinalIk stores its own enum for navigations.
        private enum EffectorType
        {
            Spine,
            ShldrL,
            ShldrR,
            ThighL,
            ThighR,
            ArmL,
            ArmR,
            LegL,
            LegR
        }

        private enum Smoothing
        {
            None,
            Linear,
            Parabola,
            Power,
            Smooth,
        }

        private enum SubTime
        {
            In,
            Mid,
            Out,
        }

        private enum PhaseState
        {
            Outside,
            Inside,
        }

        private readonly struct DistanceSampler
        {
            internal DistanceSampler(Transform from, Transform to, int queSize)
            {
                this.from = from;
                this.to = to;
                this.queSize = queSize;

                que = new Queue<float>(queSize);
            }

            private readonly Transform from;
            private readonly Transform to;

            // Why queue instead of an array? 
            // So that it can be a readonly struct
            // with the indexer inside of queue on the heap.
            // Not sure if it's better memory wise though.
            private readonly Queue<float> que;
            private readonly int queSize;

            internal void Sample()
            {
                var len = (to.position - from.position).sqrMagnitude;

                // Avoid allocations
                if (que.Count >= queSize)
                    que.Dequeue();

                que.Enqueue(len);
            }

            internal float GetAverage()
            {
                var len = 0f;

                if (que.Count == 0) return len;

                foreach (var l in que)
                {
                    len += l;
                }
                return len / que.Count;
            }
            internal void Clear() => que.Clear();
        }

        private struct VelocityTracker
        {
            private float spring;
            private float damping;
            private readonly Vector3[] velocity;
            private readonly Vector3[] prevPos;
            private readonly Transform[] transform;
            private readonly int len;

            internal VelocityTracker(Transform[] transform, float spring, float damping)
            {
                this.len = transform.Length;
                this.transform = transform;
                this.spring = spring;
                this.damping = damping;
                this.prevPos = new Vector3[len];
                this.velocity = new Vector3[len];
            }

            internal readonly void Update(float deltaTime)
            {
                for (var i  = 0; i < len; i++)
                {
                    var currPos = transform[i].position;
                    var vel = velocity[i];
                    var vec = currPos - prevPos[i];

                    var f = vec * spring + vel * -damping;

                    velocity[i] = vel + f * deltaTime;
                    prevPos[i] = currPos;
                }
            }

            internal readonly Vector3[] GetVelocities() => velocity;

            internal readonly void Clear()
            {
                var vecZero = Vector3.zero;

                for (var i = 0; i < len; i++)
                {
                    velocity[i] = vecZero;
                    prevPos[i] = transform[i].position;
                }
            }
        }


        private readonly struct VelocitySampler
        {
            private readonly Queue<Vector3>[] ques;
            private readonly int queSize;
            private readonly float queSizeInv;

            private readonly Transform[] transforms;
            private readonly Vector3[] prevPositions;
            private readonly int len;

            internal VelocitySampler(Transform[] transforms, int queSize)
            {
                this.queSize = queSize;
                this.transforms = transforms;
                queSizeInv = 1f / queSize;

                len = transforms.Length;
                ques = new Queue<Vector3>[len];
                for (var i = 0; i < len; i++)
                {
                    ques[i] = new Queue<Vector3>(queSize);
                }
                prevPositions = new Vector3[len];
            }

            internal void Sample(float deltaTime)
            {
                for (var i = 0; i < len; i++)
                {
                    var currPos = transforms[i].position;

                    var vec = (currPos - prevPositions[i]) * deltaTime;

                    var que = ques[i];

                    if (que.Count >= queSize)
                        que.Dequeue();

                    que.Enqueue(vec);

                    prevPositions[i] = currPos;
                }
            }

            internal Vector3[] GetAverages()
            {
                for (var i = 0; i < len; i++)
                {
                    var vec = Vector3.zero;
                    var que = ques[i];

                    if (que.Count == 0)
                    {
                        prevPositions[i] = vec;
                        continue;
                    }

                    foreach (var v in ques[i])
                    {
                        vec += v;
                    }
                    // Reuse pre allocated array to return values
                    // The caller will use values immediately and won't store the array ref.
                    prevPositions[i] = vec / ques[i].Count;
                    
                }
                return prevPositions;
            }

            internal void Clear()
            {
                for (var i = 0; i < len; i++)
                {
                    prevPositions[i] = transforms[i].position;
                    ques[i].Clear();
                }
            }
        }
    }
}
