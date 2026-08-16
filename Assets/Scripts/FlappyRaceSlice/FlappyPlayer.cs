using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// FlappyRace 3D 버티컬 슬라이스용 플레이어. 정식 아키텍처(World Core/VContainer) 미통합 — 손맛 확인용 독립 스크립트.
/// 고정 전진 + 플랩/중력 + 다이브 충전 대시 + 충돌 시 유령 정지. 프로토타입(dive-flappy) 튜닝값 이식.
/// </summary>
public class FlappyPlayer : MonoBehaviour
{
    [Header("이동")]
    public float forwardSpeed = 7f;
    public float flapImpulse = 8f;
    public float gravity = 24f;
    public float maxFall = 16f;

    [Header("대시")]
    public float dashMult = 2f;   // 이동거리 절반화(3→2: 추가 전진 4.4→2.2유닛)
    public float dashDur = 0.2f;
    public float dcBase = 0.13f;   // 초당 기본 충전
    public float dcDive = 1.2f;    // 낙하 시 추가 충전 배율

    [Header("경계 / 페널티")]
    public float ceilingY = 14f;
    public float groundY = -8f;
    public float ghostTime = 0.5f;    // 충돌 시 그 자리 정지 지속(멈춤)
    public float invulnTime = 0.5f;   // 정지 후 무적(반투명 깜빡)

    [Header("고도 바닥 (생성기가 자동 세팅) — 지형을 따라가는 하드 바닥")]
    public bool elevationFloor = false;
    public float elevAmp = 7f;
    public float elevWavelength = 120f;
    public float elevStartX = 6f;
    public float floorDrop = 9f;
    public float ceilRise = 9f;    // 천장도 고도 따라감 → 위로 못 넘어감(틈으로만 통과)
    public bool elevSharp;         // 생성기가 세팅: 삼각파(V·W) 여부

    float vy, dashT, ghostT, invuln, tilt;
    bool _wasTouching;   // 충돌 상승엣지 판정 — 계속 닿아있으면 재정지 안 함(무한멈춤 방지)
    float dashCharge = 0.6f;
    float boostT, boostMult = 1f;   // 외부 부스트 존이 주는 일시 전진 배속

    /// <summary>부스트 존이 호출 — dur초 동안 전진속도 mult배(대시와 별개, 지형 보상용).</summary>
    public void AddBoost(float mult, float dur) { boostMult = mult; boostT = dur; }
    public bool Boosting => boostT > 0f;

    [Header("틸트(회전) — 플래피 시그니처")]
    public float faceY = 180f;   // 3D메시=180, 2D스프라이트=0
    public float tiltUp = 28f;
    public float tiltDown = 70f;
    public float tiltPerVy = 3.5f;
    public float tiltLerp = 12f;

    // HUD 노출용
    public float DashCharge => dashCharge;
    public bool Ghost => ghostT > 0f;
    public bool Dashing => dashT > 0f;

    /// <summary>스타트 부스트 — 대시를 발동(전진 버스트 + 고도 유지 + 대시 연출). 충전 무관.</summary>
    public void StartBoost(float dur) { dashCharge = 0f; dashT = dur; vy = 0f; }

    void Update()
    {
        if (FlappyRaceStart.RaceFrozen) return;   // 카운트다운 중 정지
        float dt = Time.deltaTime;
        if (invuln > 0f) invuln -= dt;
        UpdateFlash();   // 정지/무적 동안 반투명 깜빡
        if (ghostT > 0f) { ghostT -= dt; _wasTouching = ResolveObstacles(); vy = FlappyBird.ResolveBirdCollisions(SelfCol, vy); if (ghostT <= 0f) invuln = invulnTime; return; }  // 그 자리 정지(그동안도 벽/새 밖으로)

        var kb = Keyboard.current;
        bool flap = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame))
                 || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
        bool dashPressed = kb != null && (kb.leftShiftKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame);

        if (flap) vy = flapImpulse;
        if (dashPressed && dashT <= 0f && dashCharge >= 1f) { dashCharge = 0f; dashT = dashDur; vy = 0f; }  // 대시 시작 = 수직 속도 리셋(수평 직선)

        // 수직 (대시 중엔 중력 0 → 고도 유지하며 수평으로 쭉)
        vy -= gravity * (dashT > 0f ? 0f : 1f) * dt;
        if (vy < -maxFall) vy = -maxFall;

        var p = transform.position;
        p.y += vy * dt;

        // 전진 (+X 고정, 대시/부스트 시 배속)
        float spd = forwardSpeed * (dashT > 0f ? dashMult : 1f) * (boostT > 0f ? boostMult : 1f);
        if (dashT > 0f) dashT -= dt;
        if (boostT > 0f) boostT -= dt;
        p.x += spd * dt;

        // 천장/바닥 (둘 다 고도를 따라감; 클램프는 무조건, 바닥 유령은 무적 아닐 때만)
        float cy = CeilAt(p.x);
        if (p.y > cy) { p.y = cy; vy = 0f; }
        float fy = FloorAt(p.x);
        if (p.y < fy) { p.y = fy; vy = 0f; }
        transform.position = p;
        bool touching = ResolveObstacles();   // 밀어내기(관통 방지) + 겹침 여부
        if (touching && !_wasTouching && invuln <= 0f) ghostT = ghostTime;  // 새 충돌(상승엣지)에만 0.5s 정지
        _wasTouching = touching;

        vy = FlappyBird.ResolveBirdCollisions(SelfCol, vy);   // 새끼리 몸싸움(유령정지 없음) — 부딪힌 세로 속도를 주고받는다

        // 틸트: 상승(vy>0)=코 위로, 낙하(vy<0)=코 아래로
        float tt = Mathf.Clamp(-vy * tiltPerVy, -tiltUp, tiltDown);
        tilt = Mathf.Lerp(tilt, tt, dt * tiltLerp);
        transform.localRotation = Quaternion.Euler(0f, faceY, tilt);

        // 대시 게이지: 낙하(vy<0)할수록 빨리 참
        if (dashCharge < 1f)
        {
            float add = dcBase + (vy < 0f ? dcDive * Mathf.Min(-vy, maxFall) / maxFall : 0f);
            dashCharge = Mathf.Min(1f, dashCharge + add * dt);
        }
    }

    // 현재 X에서의 바닥 높이 — 고도 프로파일을 따라감(생성기의 지형과 동일 공식). 고도 꺼짐이면 고정 groundY.
    float FloorAt(float x)
    {
        if (!elevationFloor) return groundY;
        return FlappyElevation.Value(x, elevAmp, elevStartX, elevWavelength, elevSharp) - floorDrop;
    }

    // 천장 높이 — 바닥과 대칭으로 고도를 따라감. 위로 넘어가지 못하게 막는다.
    float CeilAt(float x)
    {
        if (!elevationFloor) return ceilingY;
        return FlappyElevation.Value(x, elevAmp, elevStartX, elevWavelength, elevSharp) + ceilRise;
    }

    SpriteRenderer _sr; Color _baseColor; bool _flashInit;
    // 무적/유령 동안 반투명 깜빡. 아니면 원래 색 유지.
    void UpdateFlash()
    {
        if (!_flashInit) { _sr = GetComponentInChildren<SpriteRenderer>(); if (_sr != null) { _baseColor = _sr.color; _baseColor.a = 1f; } _flashInit = true; }
        if (_sr == null) return;
        bool inv = ghostT > 0f || invuln > 0f;
        if (inv)
        {
            float a = (Mathf.Repeat(Time.time * 11f, 1f) < 0.5f) ? 0.28f : 0.62f;   // 반투명 깜빡
            var c = _baseColor; c.a = a; _sr.color = c;
        }
        else if (_sr.color.a < 0.999f) _sr.color = _baseColor;
    }

    Collider _selfCol;
    static readonly Collider[] _pen = new Collider[24];
    Collider SelfCol { get { if (_selfCol == null) _selfCol = GetComponent<Collider>() ?? GetComponentInChildren<Collider>(); return _selfCol; } }

    // 장애물과 실제로 겹치면 밀어내(관통 방지) 겹침 여부를 반환. 정지 판정은 호출부에서 상승엣지로.
    bool ResolveObstacles()
    {
        var col = SelfCol; if (col == null) return false;
        int n = Physics.OverlapSphereNonAlloc(col.bounds.center, col.bounds.extents.magnitude + 0.1f, _pen);
        bool touching = false;
        for (int i = 0; i < n; i++)
        {
            var o = _pen[i];
            if (o == col || o.GetComponentInParent<FlappyObstacle>() == null) continue;
            if (Physics.ComputePenetration(col, col.transform.position, col.transform.rotation,
                                           o, o.transform.position, o.transform.rotation,
                                           out Vector3 dir, out float dist))
            {
                touching = true;
                transform.position += dir * dist;                 // 밖으로 밀어냄(관통 방지)
                if (dir.y > 0.5f && vy < 0f) vy = 0f;
                else if (dir.y < -0.5f && vy > 0f) vy = 0f;
            }
        }
        return touching;
    }

    public bool showDebugGui = false;   // 정식 HUD(FlappyHUD) 사용 시 옛 디버그 텍스트 숨김
    void OnGUI()
    {
        if (!showDebugGui) return;
        GUIStyle s = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(14, 10, 400, 28), "대시 " + Mathf.RoundToInt(dashCharge * 100) + "%" + (dashCharge >= 1f ? "  ★READY (Shift/D)" : ""), s);
        string st = ghostT > 0f ? "💫 충돌 정지!" : (dashT > 0f ? "⚡ 대시!" : "");
        GUI.Label(new Rect(14, 36, 400, 28), st, s);
        GUI.Label(new Rect(14, 62, 500, 24), "Space/클릭 = 플랩,  Shift/D = 대시", new GUIStyle(GUI.skin.label){ fontSize = 13 });
    }
}
