using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>퀘스트 셸(spec 2026-10-30 §7) — 오늘 3칸 · 이번 주 4칸. VM R3를 구독하는 얇은 바인더다.</summary>
    public class QuestView : ShellView
    {
        private const long TickMs = 1000;

        private readonly QuestViewModel _viewModel;
        private IVisualElementScheduledItem _tick;
        private bool _disposed;

        public QuestView(QuestViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        protected override string Title => "퀘스트";

        public override void OnOpen()
        {
            base.OnOpen();

            var dailyList = Root.Q<VisualElement>("quest-daily-list");
            var weeklyList = Root.Q<VisualElement>("quest-weekly-list");
            var message = Root.Q<Label>("quest-message");

            Disposables.Add(_viewModel.Daily.Subscribe(cards => Fill(dailyList, cards)));
            Disposables.Add(_viewModel.Weekly.Subscribe(cards => Fill(weeklyList, cards)));
            Disposables.Add(_viewModel.DailyRemaining.Subscribe(t => Root.Q<Label>("quest-daily-remaining").text = t));
            Disposables.Add(_viewModel.WeeklyRemaining.Subscribe(t => Root.Q<Label>("quest-weekly-remaining").text = t));
            Disposables.Add(_viewModel.RerollText.Subscribe(t => Root.Q<Label>("quest-reroll-left").text = t));
            Disposables.Add(_viewModel.Message.Subscribe(t =>
            {
                message.text = t;
                message.style.display = string.IsNullOrEmpty(t) ? DisplayStyle.None : DisplayStyle.Flex;
            }));

            _viewModel.Tick();
            _tick = Root.schedule.Execute(_viewModel.Tick).Every(TickMs);
        }

        public override void OnClose()
        {
            _tick?.Pause();
            _tick = null;
            base.OnClose();
        }

        private void Fill(VisualElement list, IReadOnlyList<QuestCard> cards)
        {
            list.Clear();
            foreach (var card in cards) list.Add(BuildCard(card));
        }

        private VisualElement BuildCard(QuestCard card)
        {
            var root = new VisualElement();
            root.AddToClassList("kit-panel");
            root.AddToClassList("quest-card");
            root.EnableInClassList("quest-card--done", card.Completed);

            var head = new VisualElement();
            head.AddToClassList("quest-card__head");
            var tier = new Label(card.TierText);
            tier.AddToClassList("quest-card__tier");
            var title = new Label();
            title.AddToClassList("kit-title");
            title.AddToClassList("quest-card__title");
            KitEllipsis.SetText(title, card.Title);
            head.Add(tier);
            head.Add(title);
            root.Add(head);

            //  진행 막대 — 키트 .kit-progress와 같은 마크업(타이틀 화면 부팅 막대).
            var bar = new VisualElement();
            bar.AddToClassList("kit-progress");
            bar.AddToClassList("quest-card__bar");
            var track = new StickerElement();
            track.AddToClassList("kit-progress__track");
            var inner = new VisualElement();
            inner.AddToClassList("kit-progress__inner");
            var fill = new StickerElement();
            fill.AddToClassList("kit-progress__fill");
            KitProgress.SetRatio(fill, card.Ratio);
            inner.Add(fill);
            bar.Add(track);
            bar.Add(inner);
            root.Add(bar);

            var foot = new VisualElement();
            foot.AddToClassList("quest-card__foot");
            var progress = new Label(card.ProgressText);
            progress.AddToClassList("quest-card__progress");
            var reward = new Label(card.RewardText);
            reward.AddToClassList("quest-card__reward");
            foot.Add(progress);
            foot.Add(reward);
            if (card.Completed)
            {
                var stamp = new Label(QuestFormat.Done);
                stamp.AddToClassList("kit-title");
                stamp.AddToClassList("quest-card__stamp");
                foot.Add(stamp);
            }
            else if (card.CanReroll)
            {
                string id = card.Id;
                var reroll = new Button(() => _viewModel.RerollAsync(id).Forget());
                reroll.AddToClassList("kit-press");
                reroll.AddToClassList("kit-btn");
                reroll.AddToClassList("quest-card__reroll");
                var sticker = new StickerElement();
                var label = new Label("바꾸기");
                label.AddToClassList("kit-btn__label");
                sticker.Add(label);
                reroll.Add(sticker);
                foot.Add(reroll);
            }
            root.Add(foot);
            return root;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                //  VContainer는 Transient를 추적하지 않는다 — WindowManager.Close가 View를 dispose하므로 VM 정리는 여기서(상점과 같은 방식).
                if (disposing) _viewModel.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
