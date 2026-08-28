namespace LOP
{
    /// <summary>
    /// 서버 스냅 중 <b>게임마다 다른 부분</b>을 다룬다. 위치·속도는 게임과 무관해 되감기가 직접 처리하고,
    /// 여기엔 그 게임에만 있는 것(예: 상태이상)이 온다.
    /// Unreal의 <c>ServerMoveHandleClientError</c>가 게임의 이동 컴포넌트에 있는 것과 같은 자리.
    /// </summary>
    public interface IServerCorrectionHandler
    {
        /// <summary>그 틱 내 예측이 서버와 맞는가. false면 위치가 맞아도 되돌린다.</summary>
        bool Matches(long tick, EntitySnap snap);

        /// <summary>
        /// 서버가 진실인 부분을 덮어쓴다. 되돌린 직후에 불린다.
        /// <paramref name="deltaTime"/>는 한 틱의 길이(초)다 — 되감기를 구동하는 쪽이 이미 들고
        /// 있는 값이라 여기로 넘긴다. 핸들러가 러너에서 직접 꺼내면 DI에 고리가 생긴다
        /// (러너 → ReconcileSystem → Reconciler → 핸들러 → 러너).
        /// </summary>
        void ApplyAuthoritative(GameFramework.World.Entity entity, EntitySnap snap, float deltaTime);
    }
}
