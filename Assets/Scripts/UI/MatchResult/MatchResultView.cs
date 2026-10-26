using System.Collections.Generic;
using R3;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 결과판(키트). 왼쪽 판에 머리띠·등수표, 아래에 랭크·보상 중계 자막 줄, 오른쪽 아래 확인. 오른쪽 절반은 3D 시상대 자리.
    /// 하얀 덮개를 쓴 채 열리고, 결과 단계가 <see cref="BeginReveal"/>을 부르면 덮개가 걷히며 시각표대로 차례로 붙는다.
    /// 화면 아무 데나 누르면 남은 등장을 건너뛴다(첫 탭은 건너뛰기, 확인은 그다음).
    /// 아래 줄 다음엔 종목이 실어 준 "내 기록" 카드와 경신 도장(spec §7).
    /// </summary>
    public class MatchResultView : UIView
    {
        private const string Out = "--out";

        private readonly MatchResultViewModel _viewModel;
        private readonly List<System.IDisposable> _subscriptions = new List<System.IDisposable>();
        private readonly List<VisualElement> _rows = new List<VisualElement>();

        // LOP.Action(MonoBehaviour 컴포넌트)이 System.Action을 가리므로 풀 한정한다.
        private System.Action _onConfirm;
        private System.Action _onReact;
        private Button _confirmButton;
        private VisualElement _root;
        private VisualElement _board;
        private VisualElement _rankLine;
        private VisualElement _rewardLine;
        private readonly IResultSound _sound;
        private VisualElement _recordLine;
        private VisualElement _newBest;
        private bool _stamped;
        private ResultReveal _reveal;
        private float _startTime = -1f;
        private bool _skipped;
        private bool _reacted;
        private bool _finished;

        public MatchResultView(MatchResultViewModel viewModel) : this(viewModel, null)
        {
        }

        [VContainer.Inject]
        public MatchResultView(MatchResultViewModel viewModel, IResultSound sound)
        {
            _viewModel = viewModel;
            _sound = sound;
        }

        public override UILayer Layer => UILayer.Window;

        //  뒤의 3D 시상대가 보여야 한다.
        public override bool DimsBackground => false;

        public void SetConfirmCallback(System.Action onConfirm) => _onConfirm = onConfirm;

        /// <summary>등장 중 선수 반응을 걸 시각에 부른다(결과 단계 지휘자).</summary>
        public void SetReactCallback(System.Action onReact) => _onReact = onReact;

        public override void OnOpen()
        {
            base.OnOpen();

            _root = Root.Q("matchresult-root");
            _board = Root.Q("matchresult-board");
            _rankLine = Root.Q("matchresult-rank");
            _rewardLine = Root.Q("matchresult-reward");
            _recordLine = Root.Q("matchresult-record");
            _newBest = Root.Q("matchresult-newbest");

            _root.EnableInClassList("ranked", _viewModel.IsRanked);
            Root.Q<Label>("matchresult-tag").text = _viewModel.IsRanked ? "RANKED" : "LIVE";
            Root.Q<Label>("matchresult-title").text = _viewModel.IsRanked ? "리그 경기 결과" : "경기 결과";
            Root.Q("matchresult-draw").style.display = _viewModel.IsDraw ? DisplayStyle.Flex : DisplayStyle.None;

            _confirmButton = Root.Q<Button>("confirm-button");
            _confirmButton.clicked += OnConfirmClicked;
            _confirmButton.SetEnabled(false);
            _root.RegisterCallback<PointerDownEvent>(OnTap, TrickleDown.TrickleDown);

            BuildRows();
            BuildRecords();
            Bind();
            _reveal = new ResultReveal(_viewModel.Rows.Count, _viewModel.Records.Count > 0, _viewModel.HasNewBest);
        }

        public override void OnClose()
        {
            if (_confirmButton != null) _confirmButton.clicked -= OnConfirmClicked;
            _root?.UnregisterCallback<PointerDownEvent>(OnTap, TrickleDown.TrickleDown);
            foreach (var s in _subscriptions) s.Dispose();
            _subscriptions.Clear();
            base.OnClose();
        }

        /// <summary>선수가 준비되면 부른다 — 하얀 덮개가 걷히며 시각표가 시작된다.</summary>
        public void BeginReveal()
        {
            if (_startTime >= 0f || Root == null) return;
            _startTime = UnityEngine.Time.unscaledTime;
            Root.Q("matchresult-flash").RemoveFromClassList("matchresult-flash--on");
            Root.schedule.Execute(Tick).Every(16).Until(() => _finished);
        }

        private float Elapsed() => _startTime < 0f ? 0f : UnityEngine.Time.unscaledTime - _startTime;

        private void Tick()
        {
            float t = Elapsed();
            if (t >= ResultReveal.Board) Show(_board, "matchresult-board");
            if (t >= ResultReveal.React) React();
            for (int i = 0; i < _rows.Count; i++)
            {
                if (t >= _reveal.RowAt(i)) Show(_rows[i], "matchresult-row");
            }
            if (t >= _reveal.LineAt(0)) Show(_rankLine, "matchresult-lt");
            if (t >= _reveal.LineAt(1)) Show(_rewardLine, "matchresult-lt");
            if (t >= _reveal.RecordAt) Show(_recordLine, "matchresult-lt");
            if (_reveal.NewBestShown(t, _skipped)) StampNewBest();
            if (_reveal.ConfirmEnabled(t, _skipped)) FinishConfirm();
        }

        //  첫 탭은 남은 등장을 건너뛴다 — 그 탭이 확인 버튼 위였어도 이번엔 확인이 되지 않게 이벤트를 멈춘다.
        private void OnTap(PointerDownEvent evt)
        {
            if (_startTime < 0f || _finished) return;
            _skipped = true;
            Show(_board, "matchresult-board");
            foreach (var row in _rows) Show(row, "matchresult-row");
            Show(_rankLine, "matchresult-lt");
            Show(_rewardLine, "matchresult-lt");
            Show(_recordLine, "matchresult-lt");
            if (_reveal.NewBestShown(0f, skipped: true)) StampNewBest();
            React();
            FinishConfirm();
            evt.StopImmediatePropagation();
        }

        private void FinishConfirm()
        {
            if (_finished) return;
            _finished = true;
            _confirmButton.RemoveFromClassList("matchresult-confirm" + Out);
            _confirmButton.SetEnabled(true);
        }

        private void React()
        {
            if (_reacted) return;
            _reacted = true;
            _onReact?.Invoke();
        }

        //  도장은 한 번만 — 건너뛰기와 시각표가 둘 다 부를 수 있다. 소리도 한 번.
        private void StampNewBest()
        {
            if (_stamped) return;
            _stamped = true;
            _newBest?.RemoveFromClassList("matchresult-newbest--out");
            _sound?.PlayNewBest();
        }

        //  "내 기록" 카드 — 종목이 선언한 지표마다 한 줄. 기록이 없는 판(다른 종목·못 적음)은 카드를 아예 숨긴다.
        private void BuildRecords()
        {
            var records = _viewModel.Records;
            if (_recordLine == null) return;
            _recordLine.style.display = records.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            var container = Root.Q<VisualElement>("matchresult-record-rows");
            foreach (var record in records)
            {
                var row = new VisualElement();
                row.AddToClassList("matchresult-record__row");

                var label = new Label(record.Label);
                label.AddToClassList("matchresult-record__label");
                row.Add(label);

                var value = new Label(record.ValueText);
                value.AddToClassList("kit-title");
                value.AddToClassList("matchresult-record__value");
                row.Add(value);

                //  비교 못 한 판(종목·맵 모름)은 차이 칸이 없다 — 빈칸도 "+0"도 아니라 없는 것이다.
                if (!string.IsNullOrEmpty(record.DiffText))
                {
                    var diff = new Label(record.DiffText);
                    diff.AddToClassList("matchresult-record__diff");
                    diff.EnableInClassList("matchresult-record__diff--better", record.Improved);
                    row.Add(diff);
                }
                container.Add(row);
            }
        }

        private static void Show(VisualElement e, string block) => e?.RemoveFromClassList(block + Out);

        private void BuildRows()
        {
            var container = Root.Q<VisualElement>("matchresult-rows");

            //  보고가 실패한 판은 등수가 없다 — "경기 종료"만 크게 남긴다.
            if (_viewModel.Rows.Count == 0)
            {
                container.style.display = DisplayStyle.None;
                Root.Q("matchresult-empty").style.display = DisplayStyle.Flex;
                return;
            }

            //  "공동 N" 표기는 그 등수에 몇 명이 몰려 있는지로 정한다 — 줄 전체를 한 번 훑어야 안다.
            var placements = new List<int>(_viewModel.Rows.Count);
            foreach (var row in _viewModel.Rows) placements.Add(row.Placement);

            foreach (var row in _viewModel.Rows)
            {
                var line = new VisualElement();
                line.AddToClassList("matchresult-row");
                line.AddToClassList("matchresult-row" + Out);
                if (row.IsMe) line.AddToClassList("matchresult-row--me");
                if (row.IsLeft) line.AddToClassList("matchresult-row--gone");

                int tiedCount = MatchResultViewModel.TiedCount(placements, row.Placement);
                var rank = new Label(MatchResultViewModel.FormatRank(row.Placement, row.IsDraw, tiedCount));
                rank.AddToClassList("kit-title");
                rank.AddToClassList("matchresult-row__rank");
                line.Add(rank);

                //  긴 이름은 말줄임 — USS 말줄임은 쓰지 않는다(글꼴에 '…'가 없으면 에디터가 멈춘다).
                var name = new Label();
                name.AddToClassList("matchresult-row__name");
                line.Add(name);
                KitEllipsis.SetText(name, row.DisplayName);

                //  나간 사람은 왜 꼴찌인지 보이게 한다.
                if (row.IsLeft)
                {
                    var left = new Label("나감");
                    left.AddToClassList("matchresult-row__left");
                    line.Add(left);
                }

                //  점수 없는 모드는 이 자리가 아예 안 보여야 한다 — 빈칸도 "0점"도 아니라 없는 것이다.
                if (row.HasScore)
                {
                    var score = new Label(MatchResultViewModel.FormatScore(row.Score, row.Gained, row.Lost));
                    score.AddToClassList("kit-title");
                    score.AddToClassList("matchresult-row__score");
                    line.Add(score);
                }

                container.Add(line);
                _rows.Add(line);
            }
        }

        //  부제·랭크 줄·보상 줄은 로비 조회(GetMyMatch)가 끝나야 채워질 수 있다. 빈 동안은 숨긴다.
        private void Bind()
        {
            var subtitle = Root.Q<Label>("matchresult-subtitle");
            _subscriptions.Add(_viewModel.Subtitle.Subscribe(text => subtitle.text = text));

            var rankTag = Root.Q<Label>("matchresult-rank-tag");
            var rankBody = Root.Q<Label>("matchresult-rank-body");
            _subscriptions.Add(_viewModel.RankCaption.Subscribe(caption =>
            {
                bool has = !string.IsNullOrEmpty(caption.tag);
                _rankLine.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
                rankTag.text = caption.tag;
                rankBody.text = caption.body;
                rankTag.EnableInClassList("matchresult-lt__tag--promoted", caption.tag == "승급");
                rankTag.EnableInClassList("matchresult-lt__tag--demoted", caption.tag == "강등");
            }));

            var rewardBody = Root.Q<Label>("matchresult-reward-body");
            _subscriptions.Add(_viewModel.RewardLine.Subscribe(line =>
            {
                rewardBody.text = line;
                _rewardLine.style.display = string.IsNullOrEmpty(line) ? DisplayStyle.None : DisplayStyle.Flex;
            }));
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    //  VContainer는 Transient를 추적하지 않는다 — WindowManager.Close가 View를 dispose하므로 VM 정리는 여기서
                    //  (ProfileView와 같은 방식). 닫힌 뒤 도착한 응답이 죽은 화면에 쓰지 않게 조회도 끊는다.
                    _viewModel?.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        private void OnConfirmClicked() => _onConfirm?.Invoke();
    }
}
