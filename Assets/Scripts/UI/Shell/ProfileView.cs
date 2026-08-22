using R3;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>프로필 셸. ViewModel이 받아온 큐별 전적을 그린다.</summary>
    public class ProfileView : ShellView
    {
        private readonly ProfileViewModel _viewModel;

        public ProfileView(ProfileViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        protected override string Title => "프로필";

        public override void OnOpen()
        {
            base.OnOpen();

            var status = Root.Q<Label>("profile-status");
            var queues = Root.Q<VisualElement>("profile-queues");

            Disposables.Add(_viewModel.Status.Subscribe(text =>
            {
                status.text = text;
                status.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            }));

            var historyTitle = Root.Q<Label>("profile-history-title");
            var history = Root.Q<ScrollView>("profile-history");

            Disposables.Add(_viewModel.Matches.Subscribe(matches =>
            {
                history.Clear();

                //  전적이 없거나 못 불러온 판은 제목까지 숨긴다 — 빈 상자만 남기지 않는다.
                bool has = matches != null && matches.Count > 0;
                historyTitle.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
                history.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
                if (!has) return;

                foreach (var match in matches)
                {
                    history.Add(BuildMatchCard(match));
                }
            }));

            Disposables.Add(_viewModel.Stats.Subscribe(stats =>
            {
                queues.Clear();
                if (stats == null) return;

                foreach (var queue in stats)
                {
                    queues.Add(BuildQueue(queue));
                }
            }));
        }

        protected override void Dispose(bool disposing)
        {
            //  VContainer는 Transient를 추적하지 않아 스코프가 죽어도 dispose하지 않는다.
            //  WindowManager.Close가 View를 dispose하므로 VM 정리는 여기서 한다(StatsView와 같은 방식).
            _viewModel.Dispose();
            base.Dispose(disposing);
        }

        private static VisualElement BuildMatchCard(ProfileMatchEntry match)
        {
            var card = new VisualElement();
            card.AddToClassList("profile-match");

            var header = new VisualElement();
            header.AddToClassList("profile-match-header");

            var mode = new Label(match.GameModeName);
            mode.AddToClassList("profile-match-mode");

            var date = new Label(match.EndedAt);
            date.AddToClassList("profile-match-date");

            header.Add(mode);
            header.Add(date);
            card.Add(header);

            //  큐·맵을 못 찾으면(마스터데이터가 옛 판의 id를 더 이상 안 갖는 경우) 줄 자체를 안 넣는다.
            if (!string.IsNullOrEmpty(match.Subtitle))
            {
                var subtitle = new Label(match.Subtitle);
                subtitle.AddToClassList("profile-match-subtitle");
                card.Add(subtitle);
            }

            if (match.HasMyResult)
            {
                var mine = new Label(match.MyResult);
                mine.AddToClassList("profile-match-mine");
                card.Add(mine);
            }

            foreach (var row in match.Rows)
            {
                var line = new VisualElement();
                line.AddToClassList("profile-stat");
                if (row.IsMe) line.AddToClassList("matchresult-row--me");

                var placement = new Label($"{row.Placement}등");
                placement.AddToClassList("profile-stat-label");

                var name = new Label(row.DisplayName);
                name.AddToClassList("profile-stat-value");

                line.Add(placement);
                line.Add(name);
                card.Add(line);
            }

            return card;
        }

        private static VisualElement BuildQueue(ProfileQueueStats stats)
        {
            var block = new VisualElement();
            block.AddToClassList("profile-queue");

            var name = new Label(stats.QueueName);
            name.AddToClassList("profile-queue-name");
            block.Add(name);

            if (!stats.HasRecord)
            {
                var empty = new Label("아직 기록이 없습니다.");
                empty.AddToClassList("profile-stat-label");
                block.Add(empty);
                return block;
            }

            block.Add(BuildStat("전적 점수", stats.Mmr.ToString()));
            block.Add(BuildStat("판수", $"{stats.GamesPlayed}판"));
            block.Add(BuildStat("1등", $"{stats.FirstPlaces}회"));
            block.Add(BuildStat("평균 등수", $"{stats.AveragePlacement}등"));

            return block;
        }

        private static VisualElement BuildStat(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("profile-stat");

            var labelElement = new Label(label);
            labelElement.AddToClassList("profile-stat-label");

            var valueElement = new Label(value);
            valueElement.AddToClassList("profile-stat-value");

            row.Add(labelElement);
            row.Add(valueElement);
            return row;
        }
    }
}
