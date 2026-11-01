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
            var scroll = Root.Q<VisualElement>("quest-scroll");
            var dailyRemaining = Root.Q<Label>("quest-daily-remaining");
            var weeklyRemaining = Root.Q<Label>("quest-weekly-remaining");
            var rerollLeft = Root.Q<Label>("quest-reroll-left");

            Disposables.Add(_viewModel.Daily.Subscribe(cards => Fill(dailyList, cards)));
            Disposables.Add(_viewModel.Weekly.Subscribe(cards => Fill(weeklyList, cards)));
            Disposables.Add(_viewModel.DailyRemaining.Subscribe(t => dailyRemaining.text = t));
            Disposables.Add(_viewModel.WeeklyRemaining.Subscribe(t => weeklyRemaining.text = t));
            Disposables.Add(_viewModel.RerollText.Subscribe(t => rerollLeft.text = t));
            Disposables.Add(_viewModel.Message.Subscribe(t =>
            {
                message.text = t;
                message.style.display = string.IsNullOrEmpty(t) ? DisplayStyle.None : DisplayStyle.Flex;
            }));
            //  m3: 아직·못 받았으면(일일·주간 둘 다 null) "오늘"·"이번 주" 빈 머리를 안 보인다.
            Disposables.Add(_viewModel.HasData.Subscribe(has => scroll.style.display = has ? DisplayStyle.Flex : DisplayStyle.None));

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

            //  I1: kit-panel은 모양이 없다 — back·face 자식이 바탕을 그린다(ShopView.uxml:28-30과 같은 짜임).
            //  code-built라 자식을 직접 만든다. 내용은 face에 넣는다.
            var back = new VisualElement();
            back.AddToClassList("kit-panel__back");
            root.Add(back);

            var face = new VisualElement();
            face.AddToClassList("kit-panel__face");
            face.AddToClassList("quest-card__face");
            root.Add(face);

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
            face.Add(head);

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
            face.Add(bar);

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
            else if (card.ShowReroll)
            {
                //  m1: 바꾸는 동안·남은 횟수 0이면 "잠김"(spec §7) — 버튼은 그대로 두고 SetEnabled(false) + 흐림으로 잠근다.
                //  끝난 칸만 버튼이 없다(위 if에서 이미 갈렸다).
                string id = card.Id;
                var reroll = new Button(() => _viewModel.RerollAsync(id).Forget());
                reroll.AddToClassList("kit-press");
                reroll.AddToClassList("kit-btn");
                //  사람 확인: --info(또는 --primary) 없이는 스티커 기본 바탕이 크림이라 .kit-btn__label의
                //  흰 글자가 거의 안 보인다(다른 kit-btn은 전부 --primary나 --info를 쓴다). 파랑 바탕 + 흰 글자로.
                reroll.AddToClassList("kit-btn--info");
                reroll.AddToClassList("quest-card__reroll");
                //  .kit-press:disabled(Kit.uss)가 기본 테마의 흐림을 그대로 쓴다 — 버튼은 남고 흐려질 뿐이다.
                reroll.SetEnabled(card.CanReroll);
                var sticker = new StickerElement();
                var label = new Label("바꾸기");
                label.AddToClassList("kit-btn__label");
                sticker.Add(label);
                reroll.Add(sticker);
                foot.Add(reroll);
            }
            face.Add(foot);
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
