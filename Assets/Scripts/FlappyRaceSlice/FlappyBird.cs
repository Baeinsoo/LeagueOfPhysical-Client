using UnityEngine;

/// <summary>
/// 새(플레이어/페이서) 마커 + 새끼리 몸싸움 충돌 공용 헬퍼.
/// 맵 장애물(FlappyObstacle)과 달리 유령정지 페널티 없이 순수 위치 밀어내기(자리싸움)만.
/// 각 새가 자기 Update에서 절반씩 밀어내 → 양쪽 합쳐 완전 분리(대칭). 강한 수직 밀림은 vy 상쇄용으로 반환.
/// </summary>
public class FlappyBird : MonoBehaviour
{
    /// <summary>활성 레이서 전원. 추격자·카메라·HUD가 선두를 찾는 데 쓴다.</summary>
    public static readonly System.Collections.Generic.List<FlappyBird> All = new System.Collections.Generic.List<FlappyBird>();

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    static readonly Collider[] _buf = new Collider[16];

    /// <summary>self 새를 겹친 다른 새들 밖으로 절반씩 밀어냄. 수직 밀림(dir.y) 합을 반환(호출부가 vy 상쇄에 사용).</summary>
    public static float ResolveBirdCollisions(Collider self)
    {
        if (self == null) return 0f;
        var selfBird = self.GetComponentInParent<FlappyBird>();
        float vpush = 0f;
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
                self.transform.position += dir * dist * 0.5f;   // 절반만(상대도 절반 밀어냄)
                vpush += dir.y;
            }
        }
        return vpush;
    }
}
