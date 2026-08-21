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
