namespace LOP
{
    /// <summary>되감기에서 서버 스냅의 이동 축(위치·회전·속도·접지·순간이동 카운터)을 엔티티에 덮는다.</summary>
    public static class AuthoritativeSnap
    {
        public static void ApplyMotion(GameFramework.World.Entity target, EntitySnap snap)
        {
            GameFramework.World.EntityMotionExtensions.SetPosition(target, snap.position);
            GameFramework.World.EntityMotionExtensions.SetRotation(target, snap.rotation);
            GameFramework.World.EntityMotionExtensions.SetVelocity(target, snap.velocity);

            //  접지도 서버 값으로 — 되돌린 예측 접지가 남으면, 접지에 따라 속도를 계산하는 기준이 바뀌는 게임(스카이다이브의
            //  도는 판: 서 있으면 판 기준으로 걷는다)에서 착지 틱이 한 틱만 어긋나도 재생이 판 속도만큼 갈린다(PR Shared#1 리뷰 4차).
            var ground = target.Get<GameFramework.World.GroundState>();
            if (ground != null)
            {
                ground.IsGrounded = snap.grounded;
            }

            //  판단은 teleportTracker가 이미 했다 — 여기선 클라 쪽 카운터를 서버 값에 맞춰
            //  두 사이드가 같은 값을 들고 있게만 한다(안 맞추면 나중에 읽는 쪽이 헷갈린다).
            var transform = target.Get<GameFramework.World.Transform>();
            if (transform != null)
            {
                transform.TeleportCount = snap.teleportCount;
            }
        }
    }
}
