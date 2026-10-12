using System;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// PLAY 숨쉬기. USS에는 반복 애니메이션이 없어서, 크기 클래스를 반주기마다 켜고 끄고
    /// 커지고 작아지는 움직임은 USS transition이 채운다.
    /// </summary>
    public sealed class Breather : IDisposable
    {
        private const string BreathClass = "kit-play--breath";
        private readonly VisualElement _target;
        private IVisualElementScheduledItem _item;

        public Breather(VisualElement target, long halfPeriodMs)
        {
            _target = target;
            SetHalfPeriod(halfPeriodMs);
        }

        public void SetHalfPeriod(long ms)
        {
            _item?.Pause();
            _item = _target.schedule.Execute(() => _target.ToggleInClassList(BreathClass)).Every(ms);
        }

        public void Dispose()
        {
            _item?.Pause();
            _target.RemoveFromClassList(BreathClass);
        }
    }
}
