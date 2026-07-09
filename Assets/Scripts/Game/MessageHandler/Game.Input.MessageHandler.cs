using GameFramework;

namespace LOP
{
    public class GameInputMessageHandler : IGameMessageHandler
    {
        public void Initialize()
        {
            EventBus.Default.Subscribe<InputSequenceToC>(nameof(IMessage), OnInputSequenceToC);
        }

        public void Dispose()
        {
            EventBus.Default.Unsubscribe<InputSequenceToC>(nameof(IMessage), OnInputSequenceToC);
        }

        // 서버 인풋 시퀀스 앵커는 더 이상 쓰지 않는다 — 재조정 기준점이 틱 기반 하드 복원(Reconciler)으로
        // 바뀌면서 delta-replay용 앵커 자체가 필요 없어졌다.
        private void OnInputSequenceToC(InputSequenceToC inputSequenceToC)
        {
        }
    }
}
