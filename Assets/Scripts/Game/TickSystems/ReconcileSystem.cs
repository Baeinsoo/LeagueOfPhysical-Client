namespace LOP
{
    /// <summary>구 LOPRunner의 인라인 reconciler.Reconcile 호출 이동. 내 캐릭 예측 상태를 서버 스냅과 대조·보정.</summary>
    public class ReconcileSystem : GameFramework.Runner.ITickSystem
    {
        private readonly Reconciler reconciler;

        public ReconcileSystem(Reconciler reconciler)
        {
            this.reconciler = reconciler;
        }

        public void Tick(long tick, float deltaTime)
        {
            reconciler.Reconcile(tick, deltaTime);
        }
    }
}
