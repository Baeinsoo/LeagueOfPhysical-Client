using UnityEngine;

/// <summary>
/// 4인 레이스용 페이서 봇(로컬 — 넷코드 미통합 슬라이스). 물리는 플레이어와 동일 골격:
/// 중력+플랩(코리도 중심을 겨냥)으로 비행하고, 장애물과 겹치면 플레이어와 똑같이 밀어내기(관통 방지)+
/// 0.5초 경직(유령 정지)+반투명 깜빡. 겨냥 대상(코리도 중심)만 스캔으로 구하고 회피는 완벽하지 않아
/// skill 낮을수록 자주 부딪혀 경직 → 공정한 레이스. 사람이 못 구하니 트랩 방지 복구망만 봇 전용.
/// </summary>
public class FlappyPacer : MonoBehaviour
{
    [Header("이동(플레이어와 맞춤)")]
    public float forwardSpeed = 10.5f;
    public float flapImpulse = 23f;
    public float gravity = 70f;
    public float maxFall = 30f;
    public float faceY = 0f;
    public float endX = 636f;

    [Header("AI")]
    public float skill = 1.0f;         // 0.8~1.1: 플랩 반응 정확도(높을수록 덜 부딪힘)
    public float lookAhead = 3.0f;     // 전방 이 거리의 코리도 중심을 겨냥(틈에 미리 정렬 → 과도한 충돌 방지)
    public float scanLo = -74f;
    public float scanHi = 28f;

    [Header("페널티(플레이어와 동일)")]
    public float ghostTime = 0.5f;
    public float invulnTime = 0.5f;

    public float startBoostMult = 2f;   // 스타트 부스트 전진 배속(플레이어 대시와 맞춤)

    float birdR = 0.45f, vy, curY, lastCenter, lastFlap, invuln, ghostT, stuckT, boostT;
    bool _wasTouching;
    Vector3 _startPos;
    static readonly Collider[] _buf = new Collider[24];
    Collider _selfCol;
    Collider SelfCol { get { if (_selfCol == null) _selfCol = GetComponent<Collider>() ?? GetComponentInChildren<Collider>(); return _selfCol; } }

    void OnEnable()
    {
        _startPos = transform.position;
        curY = lastCenter = transform.position.y;
        var sc = GetComponent<SphereCollider>() ?? GetComponentInChildren<SphereCollider>();
        if (sc != null) birdR = sc.radius;
    }

    /// <summary>스타트 부스트 — dur초 동안 전진 배속 + 고도 유지(플레이어 대시와 동일 감각).</summary>
    public void StartBoost(float dur) { boostT = dur; vy = 0f; }

    void Update()
    {
        if (FlappyRaceStart.RaceFrozen) return;   // 카운트다운 중 정지
        float dt = Time.deltaTime; if (dt > 0.05f) dt = 0.05f;
        if (invuln > 0f) invuln -= dt;
        UpdateFlash();

        // 경직 중: 그 자리 정지(전진X). 벽/새 밖으로만 밀어냄 — 플레이어와 동일.
        if (ghostT > 0f)
        {
            ghostT -= dt;
            _wasTouching = ResolveObstacles(); curY = transform.position.y;
            FlappyBird.ResolveBirdCollisions(SelfCol); curY = transform.position.y;
            if (ghostT <= 0f) invuln = invulnTime;
            ApplyTilt();
            return;
        }

        var p = transform.position;
        p.x += forwardSpeed * (boostT > 0f ? startBoostMult : 1f) * dt;   // 부스트 중 전진 배속

        float lo, hi;
        float center = LiveCorridor(p.x + lookAhead, curY, out lo, out hi);   // 전방 정렬

        // 중력 + 중심 아래로 처지면 플랩(포물선 아크). skill = 조준 정밀도/반응. 부스트 중엔 고도 유지(수평 직선).
        if (boostT > 0f) { boostT -= dt; vy = 0f; }
        else
        {
            float aim = Mathf.Lerp(1.6f, 0.5f, Mathf.Clamp01((skill - 0.8f) / 0.3f));
            float cd  = Mathf.Lerp(0.16f, 0.09f, Mathf.Clamp01((skill - 0.8f) / 0.3f));
            vy -= gravity * dt; if (vy < -maxFall) vy = -maxFall;
            if (curY < center - aim && Time.time - lastFlap >= cd) { vy = flapImpulse; lastFlap = Time.time; }
        }
        curY += vy * dt;
        curY = Mathf.Clamp(curY, scanLo + 2f, scanHi - 2f);   // 맵 밖 완전 이탈만 방지(장애물 회피 아님)
        p.y = curY;
        transform.position = p;

        // 장애물 충돌 — 플레이어와 동일: 밀어내기 + 새 충돌(상승엣지)에 0.5초 경직
        bool touching = ResolveObstacles(); curY = transform.position.y;
        if (touching && !_wasTouching && invuln <= 0f) ghostT = ghostTime;

        // 트랩 방지(봇 전용): 2초 넘게 계속 끼면 코리도 중심으로 스냅해 탈출
        if (touching) { stuckT += dt; if (stuckT > 2f) { curY = (hi > lo) ? (lo + hi) * 0.5f : center; transform.position = new Vector3(p.x, curY, 0f); vy = 0f; stuckT = 0f; ghostT = 0f; touching = false; } }
        else stuckT = 0f;
        _wasTouching = touching;

        float vpush = FlappyBird.ResolveBirdCollisions(SelfCol);   // 새끼리 몸싸움
        if (Mathf.Abs(vpush) > 0.01f) { curY = transform.position.y; if (vpush > 0.5f && vy < 0f) vy = 0f; else if (vpush < -0.5f && vy > 0f) vy = 0f; }

        ApplyTilt();
        if (p.x >= endX) { transform.position = _startPos; curY = lastCenter = _startPos.y; vy = 0f; _wasTouching = false; stuckT = 0f; ghostT = 0f; invuln = 0f; boostT = 0f; }
    }

    void ApplyTilt()
    {
        float tilt = Mathf.Clamp(-vy * 3.5f, -28f, 70f);
        transform.localRotation = Quaternion.Euler(0f, faceY, tilt);
    }

    // 장애물과 겹치면 밀어내(관통 방지) 겹침 여부 반환 — FlappyPlayer.ResolveObstacles와 동일.
    bool ResolveObstacles()
    {
        var col = SelfCol; if (col == null) return false;
        int n = Physics.OverlapSphereNonAlloc(col.bounds.center, col.bounds.extents.magnitude + 0.1f, _buf, ~0, QueryTriggerInteraction.Collide);
        bool touching = false;
        for (int i = 0; i < n; i++)
        {
            var o = _buf[i];
            if (o == col || o.GetComponentInParent<FlappyObstacle>() == null) continue;
            if (Physics.ComputePenetration(col, col.transform.position, col.transform.rotation,
                                           o, o.transform.position, o.transform.rotation,
                                           out Vector3 dir, out float dist))
            {
                touching = true;
                transform.position += dir * dist;
                if (dir.y > 0.5f && vy < 0f) vy = 0f;
                else if (dir.y < -0.5f && vy > 0f) vy = 0f;
            }
        }
        return touching;
    }

    SpriteRenderer _sr; Color _baseColor; bool _flashInit;
    void UpdateFlash()
    {
        if (!_flashInit) { _sr = GetComponentInChildren<SpriteRenderer>(); if (_sr != null) { _baseColor = _sr.color; _baseColor.a = 1f; } _flashInit = true; }
        if (_sr == null) return;
        bool inv = ghostT > 0f || invuln > 0f;
        if (inv)
        {
            float a = (Mathf.Repeat(Time.time * 11f, 1f) < 0.5f) ? 0.28f : 0.62f;
            var c = _baseColor; c.a = a; _sr.color = c;
        }
        else if (_sr.color.a < 0.999f) _sr.color = _baseColor;
    }

    // 현재 x열의 '열린 슬롯'(위·아래가 벽으로 닫힌 밴드) 중 y를 포함하는 것, 없으면 가장 가까운 것. 없으면 마지막 중심.
    float LiveCorridor(float x, float y, out float lo, out float hi)
    {
        float step = 0.5f;
        float runStart = float.NaN;
        float bestLo = 0f, bestHi = 0f; float best = 1e9f; bool found = false, contains = false;
        for (float sy = scanLo; sy <= scanHi + step * 0.5f; sy += step)
        {
            bool blocked = Blocked(x, sy);
            if (!blocked)
            {
                if (float.IsNaN(runStart)) runStart = sy;
            }
            else if (!float.IsNaN(runStart))
            {
                float bLo = runStart, bHi = sy - step;
                bool boundedBelow = Blocked(x, bLo - step);
                if (boundedBelow && (bHi - bLo) >= 1.2f)
                {
                    bool inside = y >= bLo && y <= bHi;
                    float c = (bLo + bHi) * 0.5f, d = Mathf.Abs(c - y);
                    if ((inside && !contains) || ((inside == contains) && d < best))
                    { best = d; bestLo = bLo; bestHi = bHi; found = true; contains = inside || contains; }
                }
                runStart = float.NaN;
            }
        }
        if (found) { lo = bestLo; hi = bestHi; lastCenter = (bestLo + bestHi) * 0.5f; return lastCenter; }
        lo = 0f; hi = 0f; return lastCenter;
    }

    bool Blocked(float x, float y)
    {
        int n = Physics.OverlapSphereNonAlloc(new Vector3(x, y, 0f), 0.2f, _buf, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++) if (_buf[i].GetComponentInParent<FlappyObstacle>() != null) return true;
        return false;
    }
}
