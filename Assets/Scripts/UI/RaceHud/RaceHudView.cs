using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// Flappy 정보 HUD(spec §1) — 위 가운데 진행 막대, 오른쪽 위 순위, 왼쪽 위 기록 시간. 표시 전용이라
    /// 모든 요소가 입력을 통과시킨다(아래 FlapPad가 화면 전체를 입력면으로 쓴다).
    /// <para>Notification이 아니라 Window인 이유는 <see cref="RaceStartView"/>와 같다 — 게임 화면이라 결과 같은
    /// 전체화면 오버레이에 가려져야 한다.</para>
    /// </summary>
    public class RaceHudView : UIView
    {
        private const string DotClass = "race-dot";
        private const string MineDotClass = "race-dot--mine";
        private const string EyeClass = "race-dot__eye";
        //  시제품: 1 + 0.35 × (1 → 0을 0.25초에).
        private const float PopScale = 1.35f;
        private const int PopBackMs = 250;

        private readonly RaceHudViewModel _viewModel;
        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();
        private readonly List<VisualElement> _dots = new List<VisualElement>();

        private VisualElement _dotLayer;
        private IVisualElementScheduledItem _tick;
        private bool _disposed;

        public RaceHudView(RaceHudViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            var clock = Root.Q<Label>("race-clock");
            var progress = Root.Q<VisualElement>("race-progress");
            var fill = Root.Q<VisualElement>("race-progress-fill");
            _dotLayer = Root.Q<VisualElement>("race-progress-dots");
            var rankPlate = Root.Q<VisualElement>("race-rank-plate");
            var rank = Root.Q<Label>("race-rank");
            var total = Root.Q<Label>("race-rank-total");

            _viewModel.ClockText
                .Subscribe(text => clock.text = text)
                .AddTo(_subscriptions);

            //  자리는 남기고 숨긴다(visibility) — 맵이 결승선을 늦게 알려도 판이 흔들리지 않게.
            _viewModel.ShowProgress
                .Subscribe(show => progress.style.visibility = show ? Visibility.Visible : Visibility.Hidden)
                .AddTo(_subscriptions);

            _viewModel.MyProgress
                .Subscribe(ratio => fill.style.width = Length.Percent(ratio * 100f))
                .AddTo(_subscriptions);

            //  순위 판을 보일지는 두 값이 같이 정한다: 내 새가 있어야(Rank > 0) 하고, 아직 결승하지 않아야(ShowRank) 한다.
            //  내가 결승한 뒤엔 결승·결과 화면이 등수를 보여 주는데, 관전 화면의 "나가기" 버튼이 이 판과 같은
            //  오른쪽 위 자리에 겹친다(RaceHud.uss .race-rank-plate vs RaceSpectateView.uss .spectate-leave).
            bool showRank = _viewModel.ShowRank.CurrentValue;
            void UpdateRankPlateVisibility(int rankValue) =>
                rankPlate.style.display = rankValue > 0 && showRank ? DisplayStyle.Flex : DisplayStyle.None;

            _viewModel.Rank
                .Subscribe(value =>
                {
                    //  내 새가 없으면(탈락 뒤 관전) 순위 판을 통째로 숨긴다 — 빈 숫자 판은 고장처럼 보인다.
                    UpdateRankPlateVisibility(value);
                    rank.text = value > 0 ? value.ToString() : string.Empty;
                    rank.style.color = FlappyRacePalette.RankColor(value);
                })
                .AddTo(_subscriptions);

            _viewModel.ShowRank
                .Subscribe(value =>
                {
                    showRank = value;
                    UpdateRankPlateVisibility(_viewModel.Rank.CurrentValue);
                })
                .AddTo(_subscriptions);

            _viewModel.Total
                .Subscribe(value => total.text = $"/ {value}")
                .AddTo(_subscriptions);

            _viewModel.RankPopped
                .Subscribe(_ => Pop(rankPlate))
                .AddTo(_subscriptions);

            // UIView는 MonoBehaviour가 아니라 Update가 없다 — 패널 스케줄러로 매 프레임 돈다(FlapPadView와 같다).
            _tick = Root.schedule.Execute(_ =>
            {
                _viewModel.Refresh();
                DrawDots();
            }).Every(0);
        }

        //  점은 개수가 바뀔 때만 만들고 나머지는 자리·색만 고친다. 목록 순서가 곧 겹침 순서다(내 점이 마지막 = 맨 위).
        private void DrawDots()
        {
            var dots = _viewModel.Dots;
            while (_dots.Count < dots.Count)
            {
                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.AddToClassList(DotClass);
                var eye = new VisualElement { pickingMode = PickingMode.Ignore };
                eye.AddToClassList(EyeClass);
                dot.Add(eye);
                _dotLayer.Add(dot);
                _dots.Add(dot);
            }

            for (int i = 0; i < _dots.Count; i++)
            {
                var dot = _dots[i];
                if (i >= dots.Count)
                {
                    dot.style.display = DisplayStyle.None;
                    continue;
                }
                dot.style.display = DisplayStyle.Flex;
                dot.style.left = Length.Percent(dots[i].Ratio * 100f);
                dot.style.backgroundColor = dots[i].Color;
                dot.EnableInClassList(MineDotClass, dots[i].Mine);
            }
        }

        //  한 번에 1.35배로 키우고, 다음 프레임에 0.25초에 걸쳐 1배로 돌아간다. 0.8초 안의 재변화는
        //  ViewModel(RankChangeDebounce)이 이미 걸렀다.
        private static void Pop(VisualElement plate)
        {
            plate.style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(0, TimeUnit.Millisecond) });
            plate.style.scale = new Scale(new Vector3(PopScale, PopScale, 1f));
            plate.schedule.Execute(() =>
            {
                plate.style.transitionProperty = new StyleList<StylePropertyName>(new List<StylePropertyName> { "scale" });
                plate.style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(PopBackMs, TimeUnit.Millisecond) });
                plate.style.scale = new Scale(Vector3.one);
            }).StartingIn(16);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    _tick?.Pause();
                    _subscriptions.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
