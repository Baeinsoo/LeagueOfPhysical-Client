using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LOP.UI;
using R3;

namespace LOP
{
    /// <summary>출전 연출을 받는 무대(로비 3D). 지휘가 시작·매 프레임 자세·되돌리기를 알린다.</summary>
    public interface IRunOutStage
    {
        void BeginRunOut();
        void PoseRunOut(RunOutPose pose, float t);
        void ResetRunOut();
    }

    public interface IRunOutDirector
    {
        UniTask PlayAsync(CancellationToken ct);
        void Reset();
        ReadOnlyReactiveProperty<bool> Running { get; }
    }

    /// <summary>
    /// 출전 연출 지휘(로비 스코프). 생기자마자 로딩판 문을 걸어 두고(매칭 성사 알림이 앱 쪽에 먼저 와도 로딩판이
    /// 번쩍이지 않게), 연출이 하얀 빛(CoverStart)에 닿으면 푼다. 무대가 없으면 3D 없이 시간만 흐른다.
    /// </summary>
    public sealed class RunOutDirector : IRunOutDirector, IDisposable
    {
        private readonly MatchLoadingViewModel _loading;
        private readonly ReactiveProperty<bool> _running = new(false);
        private IRunOutStage _stage;
        private UniTaskCompletionSource _done;
        private bool _playing;
        private bool _released;
        private float _t;

        public RunOutDirector(MatchLoadingViewModel loading)
        {
            _loading = loading;
            _loading.HoldForRunOut();
        }

        public ReadOnlyReactiveProperty<bool> Running => _running;

        public void Attach(IRunOutStage stage) => _stage = stage;

        public void Detach(IRunOutStage stage)
        {
            if (_stage == stage) _stage = null;
        }

        public bool Begin()
        {
            if (_playing) return false;
            _playing = true;
            _released = false;
            _t = 0f;
            _done = new UniTaskCompletionSource();
            _running.Value = true;
            _stage?.BeginRunOut();
            return true;
        }

        /// <summary>시간을 dt만큼 진행한다. 끝났으면 true.</summary>
        public bool Step(float dt)
        {
            if (!_playing) return true;
            _t += dt;
            var pose = RunOutTimeline.Evaluate(_t, LobbyPlayer.StandPosition.z);
            _stage?.PoseRunOut(pose, _t);
            if (!_released && _t >= RunOutTimeline.CoverStart)
            {
                _released = true;
                _loading.ReleaseRunOut();
            }
            if (!pose.Done) return false;
            _playing = false;
            _done.TrySetResult();
            return true;
        }

        public async UniTask PlayAsync(CancellationToken ct)
        {
            if (!Begin())
            {
                await _done.Task;
                return;
            }
            while (!Step(UnityEngine.Time.deltaTime))
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        public void Reset()
        {
            _playing = false;
            _done?.TrySetResult();
            _running.Value = false;
            _stage?.ResetRunOut();
            _loading.HoldForRunOut();
        }

        public void Dispose()
        {
            //  로비가 내려간다 — 남은 방 입장은 로딩판이 바로 맡는다.
            _loading.ReleaseRunOut();
            _done?.TrySetResult();
            _running.Dispose();
        }
    }
}
