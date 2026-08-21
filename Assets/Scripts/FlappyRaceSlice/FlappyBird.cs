using UnityEngine;

/// <summary>
/// 새(플레이어/페이서) 마커 + 새끼리 몸싸움 충돌 공용 헬퍼.
/// 맵 장애물(FlappyObstacle)과 달리 유령정지 페널티 없이 자리싸움만 한다.
/// 각 새가 자기 Update에서 절반씩 밀어내 양쪽 합쳐 완전 분리(대칭)하고, 부딪힌 세로 속도를 서로 주고받는다.
/// </summary>
public class FlappyBird : MonoBehaviour
{
    /// <summary>활성 레이서 전원. 추격자·카메라·HUD가 선두를 찾는 데 쓴다.</summary>
    public static readonly System.Collections.Generic.List<FlappyBird> All = new System.Collections.Generic.List<FlappyBird>();

    /// <summary>반발 계수 — 0이면 부딪힌 자리에 얹히고, 1이면 온전히 튕겨 나간다.</summary>
    public static float Restitution = 0.35f;

    /// <summary>허용 겹침. 딱 붙는 지점까지 밀어내면 다음 프레임에 또 파고들어 떨린다.</summary>
    public static float Slop = 0.01f;

    /// <summary>이 새의 세로 속도. 상대가 얼마나 빠르게 다가오는지 알아야 튕김을 계산할 수 있어 공개한다.</summary>
    public float Vy;

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    static readonly Collider[] _buf = new Collider[16];

    /// <summary>self 새를 겹친 다른 새들 밖으로 절반씩 밀어내고, 충돌 후의 세로 속도를 반환한다.</summary>
    public static float ResolveBirdCollisions(Collider self, float vy)
    {
        if (self == null) return vy;
        var selfBird = self.GetComponentInParent<FlappyBird>();
        if (selfBird == null) return vy;
        selfBird.Vy = vy;   // 상대가 이번 충돌을 계산할 때 쓸 값 — 튕기기 전 속도를 공개한다

        // 위치를 스크립트로 직접 옮기는데 이 프로젝트는 물리 자동 동기화가 꺼져 있다(m_AutoSyncTransforms: 0).
        // 맞춰주지 않으면 겹침 검사가 한 프레임 전 자리에서 이뤄져, 더 깊이 파고든 뒤에야 발견되고 그만큼 크게 튕긴다.
        Physics.SyncTransforms();

        float newVy = vy;
        int n = Physics.OverlapSphereNonAlloc(self.bounds.center, self.bounds.extents.magnitude + 0.05f, _buf, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var o = _buf[i];
            if (o == self) continue;
            var otherBird = o.GetComponentInParent<FlappyBird>();
            if (otherBird == null || otherBird == selfBird) continue;
            if (Physics.ComputePenetration(self, self.transform.position, self.transform.rotation,
                                           o, o.transform.position, o.transform.rotation,
                                           out Vector3 dir, out float dist))
            {
                self.transform.position += dir * (Mathf.Max(0f, dist - Slop) * 0.5f);   // 절반만(상대도 절반 밀어냄)
                newVy = LOP.FlappyBounce.ResolveVy(newVy, otherBird.Vy, dir.y, Restitution);
            }
        }
        return newVy;
    }
}
