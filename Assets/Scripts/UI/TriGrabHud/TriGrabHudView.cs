using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>삼색 그랩 HUD 화면. 입력을 막지 않는다(전부 picking-mode Ignore). 값은 ViewModel이 매 프레임 계산한다.</summary>
    public class TriGrabHudView : UIView
    {
        private readonly TriGrabHudViewModel viewModel;
        private readonly ActorRegistry actors;
        private readonly CameraController cameraController;

        private VisualElement ruleBox;
        private Label ruleText;
        private Label roundText;
        private Label prepTitle;
        private Label prepRule;
        private VisualElement prepBox;
        private VisualElement toastBox;
        private Label toast;
        private Label toastSub;
        private Label toastLead;
        private VisualElement timeBar;
        private VisualElement timeFill;
        private Label wrongText;
        private Label gainText;
        private VisualElement edgeFlash;
        private VisualElement scoreboard;
        private readonly List<(VisualElement row, VisualElement dot, Label name, Label place, Label score)> rowElements =
            new List<(VisualElement, VisualElement, Label, Label, Label)>();
        private IVisualElementScheduledItem tick;
        private bool disposed;

        public TriGrabHudView(TriGrabHudViewModel viewModel, ActorRegistry actors, CameraController cameraController)
        {
            this.viewModel = viewModel;
            this.actors = actors;
            this.cameraController = cameraController;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();
            ruleBox = Root.Q<VisualElement>("rule-box");
            ruleText = Root.Q<Label>("rule-text");
            roundText = Root.Q<Label>("round-text");
            prepBox = Root.Q<VisualElement>("prep-box");
            prepTitle = Root.Q<Label>("prep-title");
            prepRule = Root.Q<Label>("prep-rule");
            toastBox = Root.Q<VisualElement>("toast-box");
            toast = Root.Q<Label>("toast-text");
            toastSub = Root.Q<Label>("toast-sub");
            toastLead = Root.Q<Label>("toast-lead");
            timeBar = Root.Q<VisualElement>("time-bar");
            timeFill = Root.Q<VisualElement>("time-fill");
            wrongText = Root.Q<Label>("wrong-text");
            gainText = Root.Q<Label>("gain-text");
            edgeFlash = Root.Q<VisualElement>("edge-flash");
            scoreboard = Root.Q<VisualElement>("scoreboard");
            tick = Root.schedule.Execute(_ => Refresh()).Every(0);
        }

        private void Refresh()
        {
            viewModel.Tick();
            bool on = viewModel.Received && viewModel.RuleText.Length > 0;
            ruleBox.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (!on) return;

            if (ruleText.text != viewModel.RuleText) ruleText.text = viewModel.RuleText;
            if (roundText.text != viewModel.RoundText) roundText.text = viewModel.RoundText;

            float left = viewModel.TimeFraction;
            timeBar.style.display = left >= 0f ? DisplayStyle.Flex : DisplayStyle.None;
            if (left >= 0f)
            {
                timeFill.style.width = Length.Percent(left * 100f);
                timeFill.EnableInClassList("time-fill--low", left < TriGrabHudViewModel.TimeLowFraction);
            }

            bool prep = viewModel.PrepTitle.Length > 0;
            prepBox.style.display = prep ? DisplayStyle.Flex : DisplayStyle.None;
            if (prep)
            {
                prepTitle.text = viewModel.PrepTitle;
                prepRule.text = viewModel.RuleText;
            }

            bool hasToast = viewModel.ToastText.Length > 0;
            toastBox.style.display = hasToast ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasToast)
            {
                toast.text = viewModel.ToastText;
                toast.style.color = viewModel.ToastColor;
                bool sub = viewModel.ToastSub.Length > 0;
                toastSub.style.display = sub ? DisplayStyle.Flex : DisplayStyle.None;
                if (sub) toastSub.text = viewModel.ToastSub;
                bool lead = viewModel.ToastLead.Length > 0;
                toastLead.style.display = lead ? DisplayStyle.Flex : DisplayStyle.None;
                if (lead) toastLead.text = viewModel.ToastLead;
            }

            RefreshScoreboard();
            PlaceWrong();
            PlaceGain();
            RefreshEdge();
        }

        private void RefreshEdge()
        {
            if (edgeFlash == null) return;
            float alpha = viewModel.EdgeAlpha;
            edgeFlash.style.opacity = alpha;
            if (alpha <= 0f) return;
            var c = viewModel.EdgeColor;
            edgeFlash.style.borderTopColor = c;
            edgeFlash.style.borderBottomColor = c;
            edgeFlash.style.borderLeftColor = c;
            edgeFlash.style.borderRightColor = c;
        }

        // 내 정답 점수는 내 몸 머리 위에서 크게 떠오른다(오답 글과 같은 자리 규칙).
        private void PlaceGain()
        {
            if (gainText == null) return;
            float alpha = viewModel.GainAlpha;
            var camera = cameraController != null ? cameraController.MainCamera : null;
            if (alpha <= 0f || camera == null || Root.panel == null || !viewModel.TryGetMyHead(actors, out var head))
            {
                gainText.style.display = DisplayStyle.None;
                return;
            }
            var p = RuntimePanelUtils.CameraTransformWorldToPanel(Root.panel, head, camera);
            gainText.style.display = DisplayStyle.Flex;
            gainText.style.opacity = alpha;
            if (gainText.text != viewModel.GainText) gainText.text = viewModel.GainText;
            gainText.style.left = p.x - 100f;
            gainText.style.top = p.y - 50f - (1f - alpha) * 50f;
        }

        private void RefreshScoreboard()
        {
            var rows = viewModel.Rows;
            while (rowElements.Count < rows.Count)
            {
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("score-row");
                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.AddToClassList("score-dot");
                var name = new Label { pickingMode = PickingMode.Ignore };
                name.AddToClassList("score-name");
                var place = new Label { pickingMode = PickingMode.Ignore };
                place.AddToClassList("score-place");
                var score = new Label { pickingMode = PickingMode.Ignore };
                score.AddToClassList("score-value");
                row.Add(dot);
                row.Add(name);
                row.Add(place);
                row.Add(score);
                scoreboard.Add(row);
                rowElements.Add((row, dot, name, place, score));
            }
            for (int i = 0; i < rowElements.Count; i++)
            {
                var e = rowElements[i];
                bool shown = i < rows.Count;
                e.row.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (!shown) continue;
                var r = rows[i];
                e.dot.style.backgroundColor = r.Seat;
                if (e.name.text != r.Name) e.name.text = r.Name;
                e.name.style.color = r.Seat;
                if (e.place.text != r.Place) e.place.text = r.Place;
                if (e.score.text != r.Score) e.score.text = r.Score;
                e.row.EnableInClassList("score-row--mine", r.Mine);
            }
        }

        // 내 오답 글은 내 몸 머리 위 화면 자리에 띄운다.
        private void PlaceWrong()
        {
            float alpha = viewModel.WrongAlpha;
            var camera = cameraController != null ? cameraController.MainCamera : null;
            if (alpha <= 0f || camera == null || Root.panel == null || !viewModel.TryGetMyHead(actors, out var head))
            {
                wrongText.style.display = DisplayStyle.None;
                return;
            }
            var p = RuntimePanelUtils.CameraTransformWorldToPanel(Root.panel, head, camera);
            wrongText.style.display = DisplayStyle.Flex;
            wrongText.style.opacity = alpha;
            //  떠오르며 사라진다.
            wrongText.style.left = p.x - 80f;
            wrongText.style.top = p.y - 30f - (1f - alpha) * 30f;
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                disposed = true;
                if (disposing)
                {
                    tick?.Pause();
                    viewModel.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
