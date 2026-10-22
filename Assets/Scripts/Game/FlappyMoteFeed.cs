using R3;
using UnityEngine;

namespace LOP
{
    /// <summary>알갱이 연출이 HUD에 띄울 글자의 종류.</summary>
    public enum FlappyMotePopupKind
    {
        /// <summary>"+12알" — 무더기를 지나며 먹은 개수.</summary>
        Count,

        /// <summary>"+18알 듬뿍!" — 많이 먹었을 때(금빛).</summary>
        Plenty,

        /// <summary>처음 두 무더기 앞의 안내 한 줄.</summary>
        Hint,
    }

    public readonly struct FlappyMotePopup
    {
        public readonly Vector3 World;
        public readonly string Text;
        public readonly FlappyMotePopupKind Kind;

        public FlappyMotePopup(Vector3 world, string text, FlappyMotePopupKind kind)
        {
            World = world;
            Text = text;
            Kind = kind;
        }
    }

    /// <summary>
    /// 알갱이 연출(<see cref="FlappyMoteFx"/>, 월드)과 HUD(<see cref="UI.FlapPadView"/>, 화면) 사이의 우편함.
    /// 월드 쪽은 "무엇을 어디에 띄울지"만 알리고, 글꼴·애니메이션은 HUD가 맡는다.
    /// </summary>
    public class FlappyMoteFeed : System.IDisposable
    {
        private readonly Subject<FlappyMotePopup> _popups = new Subject<FlappyMotePopup>();

        public Observable<FlappyMotePopup> Popups => _popups;

        public void Publish(FlappyMotePopup popup) => _popups.OnNext(popup);

        public void Dispose() => _popups.Dispose();
    }
}
