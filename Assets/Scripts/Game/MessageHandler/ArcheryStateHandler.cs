using GameFramework;
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// 지금 웨이브에서 어느 과녁이 사라졌는지를 서버에게서 받는다.
    ///
    /// <para><b>사건이 아니라 상태로 받는 이유:</b> 적중 사건은 reliable로 가지만 미러는 새 연결에
    /// 지난 메시지를 다시 틀어 주지 않는다 — 끊겼다 돌아오면 이미 먹힌 과녁이 살아 있는 것으로
    /// 보이고, 스스로는 그게 틀렸다는 것조차 알 수 없다. 상태는 서버가 "아직 못 받은 세션"에
    /// 다시 보내 주므로 돌아온 사람도 한 번이면 맞춰진다.</para>
    /// </summary>
    public class ArcheryStateHandler : MessageHandlerBase
    {
        private readonly ArcheryConsumed consumed;
        private readonly ISubscriber<ArcheryStateToC> subscriber;

        public ArcheryStateHandler(ArcheryConsumed consumed, ISubscriber<ArcheryStateToC> subscriber)
        {
            this.consumed = consumed;
            this.subscriber = subscriber;
        }

        protected override void Subscribe() => Track(subscriber.Subscribe(OnArcheryStateToC));

        private void OnArcheryStateToC(ArcheryStateToC message)
        {
            consumed.ApplyState(message.WaveIndex, message.ConsumedMask);
        }
    }
}
