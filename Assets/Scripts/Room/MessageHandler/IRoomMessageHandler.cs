using System;
using VContainer.Unity;

namespace LOP
{
    /// <summary>룸 스코프 메시지 핸들러 — 컨테이너 엔트리포인트(Initialize=구독, Dispose=해제). 스코프가 자동 구동한다.</summary>
    public interface IRoomMessageHandler : IInitializable, IDisposable
    {
    }
}
