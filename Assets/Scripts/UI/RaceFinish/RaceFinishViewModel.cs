using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 결승 도장 값(spec §3). 창이 열리는 순간 = 내가 결승선을 넘은 순간(<see cref="FlappyHudCoordinator"/>가 시뮬의 결승을 보고 연다):
    /// FINISH! 도장 + 결승음·도장음. 0.45초 뒤 순위 도장 + 도장음(1위면 "우승!"). 둘 다 1.8초부터 사라지고, 그 뒤엔 위쪽 작은 판에
    /// "n위 · 완주"가 남는다 — 조각 1이 결승 뒤 HUD 순위 판을 숨기므로 등수를 볼 곳이 필요하다.
    /// <para>View가 매 프레임 <see cref="Refresh"/>를 부른다. 시간은 실제 시간(Time.deltaTime)이다 — 결승 느려짐은 내 새 그림만 늦춘다.</para>
    /// </summary>
    public class RaceFinishViewModel
    {
        private readonly IGameDataStore _gameDataStore;
        private readonly GameFramework.World.EntityRegistry _entityRegistry;
        private readonly IFlappyAudio _audio;

        private float _age = -1f;
        private float _rankAge = -1f;
        private int _placement;

        //  IPlayerContext가 아니라 IGameDataStore를 읽는다. "내 등수"는 정체 질문이라 몸이 사라진 뒤에도 답이 있어야 한다.
        public RaceFinishViewModel(IGameDataStore gameDataStore, GameFramework.World.EntityRegistry entityRegistry, IFlappyAudio audio)
        {
            _gameDataStore = gameDataStore;
            _entityRegistry = entityRegistry;
            _audio = audio;
        }

        public float FinishScale { get; private set; } = FinishStampTimeline.StampFrom;
        public float FinishAlpha { get; private set; }
        public bool RankStampVisible { get; private set; }
        public string RankText { get; private set; } = string.Empty;
        public Color RankColor { get; private set; } = FlappyRacePalette.RankColor(0);
        public float RankScale { get; private set; } = FinishStampTimeline.StampFrom;
        public float RankAlpha { get; private set; }

        /// <summary>1위 — 순위 도장 위에 "우승!".</summary>
        public bool Winner { get; private set; }

        /// <summary>도장이 다 사라진 뒤 위쪽 작은 판을 보이나.</summary>
        public bool ShowSettled { get; private set; }

        /// <summary>작은 판 문구("n위 · 완주"). 보일 때만 채운다.</summary>
        public string PlacementText { get; private set; } = string.Empty;

        public void Refresh(float deltaTime)
        {
            if (_age < 0f)
            {
                //  첫 프레임 = 찍히는 순간. 그 프레임의 dt를 나이에 넣으면 1.8배가 한 프레임 덜 보인다.
                _age = 0f;
                _audio.Play(FlappySfx.Finish);
                _audio.Play(FlappySfx.Stamp);
            }
            else
            {
                _age += deltaTime;
            }

            //  한 번 오면 고정한다 — 판이 끝나며 엔티티가 정리돼도 작은 판의 등수가 사라지지 않게.
            if (_placement == 0)
            {
                _placement = ReadPlacement();
            }

            if (_rankAge < 0f)
            {
                //  등수는 서버 답이라 0.2초쯤 늦다. 0.45초 전에 오면 0.45초에, 더 늦으면 오는 순간 찍는다. 그 전엔 숫자를 지어내지 않는다.
                if (_placement > 0 && _age >= FinishStampTimeline.RankDelaySeconds)
                {
                    _rankAge = 0f;
                    _audio.Play(FlappySfx.Stamp);
                }
            }
            else
            {
                _rankAge += deltaTime;
            }

            FinishScale = FinishStampTimeline.Scale(_age);
            FinishAlpha = FinishStampTimeline.Alpha(_age);
            RankText = _placement > 0 ? $"{_placement}위" : string.Empty;
            RankColor = FlappyRacePalette.RankColor(_placement);
            Winner = _placement == 1;
            RankStampVisible = _rankAge >= 0f && FinishStampTimeline.Gone(_rankAge) == false;
            RankScale = FinishStampTimeline.Scale(_rankAge);
            RankAlpha = FinishStampTimeline.Alpha(_rankAge);
            ShowSettled = _rankAge >= 0f && FinishStampTimeline.Gone(_rankAge);
            PlacementText = ShowSettled ? $"{_placement}위 · 완주" : string.Empty;
        }

        private int ReadPlacement()
        {
            string id = _gameDataStore.userEntityId;
            var entity = string.IsNullOrEmpty(id) ? null : _entityRegistry.Get(id);
            return entity?.Get<FinishPlacement>()?.Value ?? 0;
        }
    }
}
