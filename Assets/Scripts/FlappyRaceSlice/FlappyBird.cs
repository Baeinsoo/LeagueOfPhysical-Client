using UnityEngine;

/// <summary>
/// 새(플레이어/페이서) 마커 + 새끼리 충돌 처리.
/// 규칙 세 줄:
///   1. 대시 중에 남과 겹치면 그 사람이 낙마한다. 박은 쪽은 감속 없이 지나간다.
///   2. 맞대시로 서로 박으면 둘 다 낙마 없이 튕기기만 한다.
///   3. 그 외 접촉은 페널티 없이 속도가 반영된 튕김이다 — 세게 받히면 궤도가 무너져 파이프에 박는다.
/// 각 새가 자기 Update에서 절반씩 밀어내 양쪽 합쳐 완전 분리(대칭).
/// </summary>
public class FlappyBird : MonoBehaviour
{
    /// <summary>활성 레이서 전원. 추격자·카메라·HUD가 선두를 찾는 데 쓴다.</summary>
    public static readonly System.Collections.Generic.List<FlappyBird> All = new System.Collections.Generic.List<FlappyBird>();

    /// <summary>부딪힌 상대 속도 1당 얹을 밀림 세기. 크면 요란하게 튕긴다.</summary>
    public static float KnockbackScale = 0.85f;

    public IFlappyRacer Racer { get; private set; }

    static readonly Collider[] _buf = new Collider[16];

    void Awake() { Racer = GetComponent<IFlappyRacer>(); }
    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    /// <summary>self 새를 겹친 다른 새들 밖으로 절반씩 밀어냄. 수직 밀림 합을 반환(호출부가 vy 상쇄에 사용).</summary>
    public static float ResolveBirdCollisions(Collider self)
    {
        if (self == null) return 0f;
        var selfBird = self.GetComponentInParent<FlappyBird>();
        if (selfBird == null) return 0f;
        float vpush = 0f;

        int n = Physics.OverlapSphereNonAlloc(self.bounds.center, self.bounds.extents.magnitude + 0.05f,
                                              _buf, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var o = _buf[i];
            if (o == self) continue;
            var otherBird = o.GetComponentInParent<FlappyBird>();
            if (otherBird == null || otherBird == selfBird) continue;

            if (!Physics.ComputePenetration(self, self.transform.position, self.transform.rotation,
                                            o, o.transform.position, o.transform.rotation,
                                            out Vector3 dir, out float dist))
                continue;

            var selfR = selfBird.Racer;
            var otherR = otherBird.Racer;
            bool selfDash = selfR != null && selfR.IsDashing;
            bool otherDash = otherR != null && otherR.IsDashing;

            // 규칙 1 — 나만 대시 중이면 상대가 낙마. (규칙 2: 둘 다 대시면 낙마 없음)
            if (selfDash && !otherDash && otherR != null) otherR.Dismount();

            self.transform.position += dir * dist * 0.5f;   // 절반만(상대도 절반 밀어냄)
            vpush += dir.y;

            // 규칙 3 — 대시로 박는 쪽은 감속 없이 지나가고, 그 외에는 다가온 속도만큼 튕긴다
            if (!selfDash && selfR != null)
            {
                Vector2 rel = (otherR != null ? otherR.Velocity : Vector2.zero) - selfR.Velocity;
                float closing = Vector2.Dot(rel, (Vector2)dir);
                if (closing > 0f) selfR.AddKnockback((Vector2)dir * (closing * KnockbackScale));
            }
        }
        return vpush;
    }
}
