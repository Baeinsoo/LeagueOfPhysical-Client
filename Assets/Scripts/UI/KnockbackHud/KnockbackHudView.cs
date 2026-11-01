using R3;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>남은 사람·원 안내(위 가운데)와 내 피해 %(왼쪽 위). 입력을 막지 않는다(모두 picking-mode Ignore).</summary>
    public class KnockbackHudView : UIView
    {
        private readonly KnockbackHudViewModel viewModel;
        private IVisualElementScheduledItem tick;
        private bool disposed;

        public KnockbackHudView(KnockbackHudViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            var aliveText = Root.Q<Label>("alive-text");
            var zoneText = Root.Q<Label>("zone-text");
            var damageText = Root.Q<Label>("damage-text");

            viewModel.AliveText.Subscribe(t => aliveText.text = t).AddTo(Disposables);
            viewModel.ZoneText.Subscribe(t => zoneText.text = t).AddTo(Disposables);
            viewModel.MyDamageText.Subscribe(t => damageText.text = t).AddTo(Disposables);
            viewModel.MyDamageColor.Subscribe(c => damageText.style.color = c).AddTo(Disposables);

            tick = Root.schedule.Execute(_ => viewModel.Tick()).Every(0);
        }

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
