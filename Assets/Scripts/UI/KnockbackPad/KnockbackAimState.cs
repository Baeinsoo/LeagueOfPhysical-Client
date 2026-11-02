namespace LOP.UI
{
    /// <summary>
    /// 발사 버튼의 조준 표시 상태. 버튼(UI)이 쓰고 바닥 조준선(월드)이 읽는다 — 둘이 서로를 모르게 사이에 둔다.
    /// </summary>
    public class KnockbackAimState
    {
        public KnockbackPadViewModel.AimKind Kind;
        /// <summary>끈 방향(yaw 0 = +z, 90 = +x, 도).</summary>
        public float AimYaw;
        /// <summary>자동 조준 대상의 바닥 위치(x, z).</summary>
        public UnityEngine.Vector2 TargetXZ;

        /// <summary>다음 탄이 충격파 폭탄인가. 조준선 색을 정한다.</summary>
        public bool HasBomb;

        public void Clear() => Kind = KnockbackPadViewModel.AimKind.None;
    }
}
