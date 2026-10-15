using System.Collections.Generic;
using R3;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 선수 카드 셸. 왼쪽 정보 판(이름·레벨·랭크·기록·장착), 오른쪽 최근 전적(판을 누르면 등수 줄 펼침).
    /// 값은 전부 ProfileViewModel 그대로 — 받지 못한 값은 줄째 숨기고 가짜 값으로 채우지 않는다.
    /// </summary>
    public class ProfileView : ShellView
    {
        private readonly ProfileViewModel _viewModel;
        private System.Action _onLocker;

        private Button _renameButton;
        private Button _toLocker;

        public ProfileView(ProfileViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        protected override string Title => "선수 카드";

        /// <summary>[라커룸]을 누르면 부를 동작. 여는 쪽(FrontEndCoordinator)이 셸 교체로 잇는다.</summary>
        public void SetLockerCallback(System.Action onLocker) => _onLocker = onLocker;

        /// <summary>큐 하나의 기록 한 줄. 기록이 없으면 그렇게 말한다.</summary>
        public static string RecordLine(ProfileQueueStats stats) =>
            stats.HasRecord && stats.GamesPlayed > 0
                ? $"{stats.QueueName} {stats.GamesPlayed}전 {stats.FirstPlaces}회 1위 · 평균 {stats.AveragePlacement}등"
                : $"{stats.QueueName} 아직 기록이 없습니다";

        /// <summary>"Lv 3 · XP 120 / 500". 아직 못 받았으면 빈 문자열(줄을 숨긴다).</summary>
        public static string LevelLine(string level, string xp)
        {
            if (string.IsNullOrEmpty(level)) return string.Empty;
            return string.IsNullOrEmpty(xp) ? level : $"{level} · XP {xp}";
        }

        public override void OnOpen()
        {
            base.OnOpen();

            var name = Root.Q<Label>("profile-name");
            var status = Root.Q<Label>("profile-status");
            var level = Root.Q<Label>("profile-level");
            var rank = Root.Q<VisualElement>("profile-rank");
            var records = Root.Q<VisualElement>("profile-records");
            var equipped = Root.Q<Label>("profile-equipped");
            var historyTitle = Root.Q<Label>("profile-history-title");
            var history = Root.Q<ScrollView>("profile-history");

            _renameButton = Root.Q<Button>("profile-rename");
            _renameButton.clicked += OnRenameClicked;
            _toLocker = Root.Q<Button>("profile-to-locker");
            _toLocker.clicked += OnLockerClicked;

            Disposables.Add(_viewModel.Identity.Subscribe(text => KitEllipsis.SetText(name, text)));

            Disposables.Add(_viewModel.Status.Subscribe(text =>
            {
                status.text = text;
                status.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            }));

            Disposables.Add(_viewModel.LevelText.CombineLatest(_viewModel.XpText, LevelLine).Subscribe(text =>
            {
                level.text = text;
                level.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            }));

            //  장착은 슬롯마다 한 줄로 오지만 판이 좁아 " · "로 잇는다.
            Disposables.Add(_viewModel.EquippedText.Subscribe(text =>
                KitEllipsis.SetText(equipped, string.IsNullOrEmpty(text) ? string.Empty : $"장착 중 · {text.Replace("\n", " · ")}")));

            Disposables.Add(_viewModel.Stats.Subscribe(stats =>
            {
                rank.Clear();
                records.Clear();
                if (stats == null) return;

                foreach (var queue in stats)
                {
                    //  랭크 줄은 VM이 랭크 큐에만 준다(배치 중이면 "배치 2/5" 같은 VM 문구 그대로).
                    if (!string.IsNullOrEmpty(queue.RankLine))
                    {
                        var line = new Label(queue.RankLine);
                        line.AddToClassList("kit-title");
                        line.AddToClassList("profile-rank__line");
                        if (!string.IsNullOrEmpty(queue.TierClass)) line.AddToClassList(queue.TierClass);
                        rank.Add(line);
                        if (!string.IsNullOrEmpty(queue.PeakLine))
                        {
                            var peak = new Label(queue.PeakLine);
                            peak.AddToClassList("profile-rank__peak");
                            rank.Add(peak);
                        }
                    }

                    var record = new Label();
                    record.AddToClassList("profile-records__line");
                    KitEllipsis.SetText(record, RecordLine(queue));
                    records.Add(record);
                }
            }));

            Disposables.Add(_viewModel.Matches.Subscribe(matches =>
            {
                history.Clear();

                //  전적이 없거나 못 불러온 판은 제목까지 숨긴다 — 빈 상자만 남기지 않는다.
                bool has = matches != null && matches.Count > 0;
                historyTitle.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
                history.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
                if (!has) return;

                for (int i = 0; i < matches.Count; i++)
                {
                    //  가장 최근 판만 펼쳐 둔다 — 나머지는 머리줄만 보이고 누르면 펼친다.
                    history.Add(BuildMatchCard(matches[i], open: i == 0));
                }
            }));
        }

        public override void OnClose()
        {
            if (_renameButton != null) _renameButton.clicked -= OnRenameClicked;
            if (_toLocker != null) _toLocker.clicked -= OnLockerClicked;

            base.OnClose();
        }

        private void OnRenameClicked() => _viewModel.RequestRename();

        private void OnLockerClicked() => _onLocker?.Invoke();

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    //  VContainer는 Transient를 추적하지 않아 스코프가 죽어도 dispose하지 않는다.
                    //  WindowManager.Close가 View를 dispose하므로 VM 정리는 여기서 한다(StatsView와 같은 방식).
                    _viewModel.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        /// <summary>판 카드 — 머리줄(종목·부제·내 결과·시각)을 누르면 등수 줄(결과판과 같은 표기)이 펼쳐진다.</summary>
        internal static VisualElement BuildMatchCard(ProfileMatchEntry match, bool open)
        {
            var card = new VisualElement();
            card.AddToClassList("profile-match");

            var rows = new VisualElement();
            rows.AddToClassList("profile-match__rows");
            rows.EnableInClassList("profile-match__rows--open", open);

            var head = new Button(() => rows.ToggleInClassList("profile-match__rows--open"));
            head.AddToClassList("profile-match__head");

            var mode = new Label(match.GameModeName);
            mode.AddToClassList("kit-title");
            mode.AddToClassList("profile-match__mode");
            head.Add(mode);

            //  큐·맵을 못 찾으면(마스터데이터가 옛 판의 id를 더 이상 안 갖는 경우) 줄 자체를 안 넣는다.
            if (!string.IsNullOrEmpty(match.Subtitle))
            {
                var subtitle = new Label();
                subtitle.AddToClassList("profile-match__sub");
                KitEllipsis.SetText(subtitle, match.Subtitle);
                head.Add(subtitle);
            }

            if (match.HasMyResult)
            {
                var mine = new Label(match.MyResult);
                mine.AddToClassList("kit-title");
                mine.AddToClassList("profile-match__mine");
                head.Add(mine);
            }

            var date = new Label(match.EndedAt);
            date.AddToClassList("profile-match__date");
            //  내 결과가 없으면 시각이 오른쪽 끝으로 간다.
            if (!match.HasMyResult) date.style.marginLeft = new StyleLength(StyleKeyword.Auto);
            head.Add(date);

            //  "공동 N등" 표기는 그 등수에 몇 명이 몰려 있는지로 정한다 — 결과 화면과 같은 판정.
            var placements = new List<int>(match.Rows.Count);
            foreach (var row in match.Rows) placements.Add(row.Placement);

            foreach (var row in match.Rows)
            {
                var line = new VisualElement();
                line.AddToClassList("profile-row");
                line.EnableInClassList("profile-row--me", row.IsMe);
                line.EnableInClassList("profile-row--gone", row.IsLeft);

                int tiedCount = MatchResultViewModel.TiedCount(placements, row.Placement);
                var placement = new Label(MatchResultViewModel.FormatPlacement(row.Placement, row.IsDraw, tiedCount));
                placement.AddToClassList("kit-title");
                placement.AddToClassList("profile-row__rank");
                line.Add(placement);

                var name = new Label();
                name.AddToClassList("profile-row__name");
                KitEllipsis.SetText(name, row.DisplayName);
                line.Add(name);

                if (row.IsLeft)
                {
                    var left = new Label("나감");
                    left.AddToClassList("profile-row__left");
                    line.Add(left);
                }

                //  점수 없는 모드는 이 자리가 아예 안 보여야 한다 — 빈칸도 "0점"도 아니라 없는 것이다.
                if (row.HasScore)
                {
                    var score = new Label(MatchResultViewModel.FormatScore(row.Score, row.Gained, row.Lost));
                    score.AddToClassList("profile-row__score");
                    line.Add(score);
                }

                rows.Add(line);
            }

            card.Add(head);
            card.Add(rows);
            return card;
        }
    }
}
