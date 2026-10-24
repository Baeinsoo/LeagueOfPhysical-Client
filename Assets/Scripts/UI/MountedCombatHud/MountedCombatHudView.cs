using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>맞힘 ✕, 맞은 방향 호, 킬 피드. 입력을 막지 않는다(모든 요소 picking-mode Ignore).</summary>
    public class MountedCombatHudView : UIView
    {
        private readonly MountedCombatHudViewModel viewModel;

        private VisualElement hitMarker;
        private VisualElement damageArc;
        private VisualElement killFeed;
        private Label zoneText;
        private Label aliveText;
        private MountedMinimapElement minimap;
        private IVisualElementScheduledItem tick;
        private float? shownBearing;

        public MountedCombatHudView(MountedCombatHudViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            hitMarker = Root.Q<VisualElement>("hit-marker");
            damageArc = Root.Q<VisualElement>("damage-arc");
            killFeed = Root.Q<VisualElement>("kill-feed");

            zoneText = Root.Q<Label>("zone-text");
            aliveText = Root.Q<Label>("alive-text");
            minimap = new MountedMinimapElement { style = { flexGrow = 1 } };
            Root.Q<VisualElement>("minimap-host").Add(minimap);

            viewModel.ZoneText.Subscribe(t => zoneText.text = t).AddTo(Disposables);
            viewModel.AliveText.Subscribe(t => aliveText.text = t).AddTo(Disposables);
            viewModel.HitMarker.Subscribe(on => hitMarker.style.display = on ? DisplayStyle.Flex : DisplayStyle.None)
                .AddTo(Disposables);
            viewModel.DamageFromBearing.Subscribe(b =>
            {
                shownBearing = b;
                damageArc.style.display = b.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            }).AddTo(Disposables);
            viewModel.KillFeed.Subscribe(RenderFeed).AddTo(Disposables);

            // 시간 만료와 호의 방향(카메라가 돌면 따라 돈다)은 매 프레임 갱신한다.
            tick = Root.schedule.Execute(_ =>
            {
                viewModel.Tick();
                minimap.Set(viewModel.BuildMinimap(), Camera.main != null ? Camera.main.transform.eulerAngles.y : 0f);
                if (shownBearing.HasValue)
                {
                    float cameraYaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : 0f;
                    float angle = MountedCombatHudViewModel.ScreenAngle(shownBearing.Value, cameraYaw);
                    damageArc.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
                }
            }).Every(0);
        }

        private void RenderFeed(IReadOnlyList<string> lines)
        {
            killFeed.Clear();
            foreach (var line in lines)
            {
                var label = new Label(line) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("kill-line");
                killFeed.Add(label);
            }
        }

        private bool disposed;

        protected override void Dispose(bool disposing)
        {
            if (disposed == false)
            {
                disposed = true;
                if (disposing)
                {
                    tick?.Pause();
                    tick = null;
                    viewModel.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
