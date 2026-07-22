using GameFramework;
using MessagePipe;

namespace LOP
{
    public class GameInputMessageHandler : MessageHandlerBase
    {
        private readonly ISubscriber<InputSequenceToC> inputSequenceSubscriber;

        public GameInputMessageHandler(ISubscriber<InputSequenceToC> inputSequenceSubscriber)
        {
            this.inputSequenceSubscriber = inputSequenceSubscriber;
        }

        protected override void Subscribe() => Track(inputSequenceSubscriber.Subscribe(OnInputSequenceToC));

        // 서버 인풋 시퀀스 앵커는 더 이상 쓰지 않는다 — 재조정 기준점이 틱 기반 하드 복원(Reconciler)으로
        // 바뀌면서 delta-replay용 앵커 자체가 필요 없어졌다.
        private void OnInputSequenceToC(InputSequenceToC inputSequenceToC)
        {
        }
    }
}
