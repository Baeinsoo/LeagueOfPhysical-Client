using UnityEngine;
using FlappyRace;

/// <summary>
/// 화면 왼쪽에서 쫓아오는 추격자. 닿으면 탈락.
/// 위치는 "가속선"과 "선두 한 화면 뒤" 중 앞선 쪽 — 앞의 것이 선두도 압박하고,
/// 뒤의 것이 전원을 한 화면에 묶어 박치기·몸싸움 기회를 끝까지 유지한다.
/// </summary>
public class FlappyChaser : MonoBehaviour
{
    [Header("속도 곡선 (상한은 플레이어 전진속도 11보다 반드시 낮게)")]
    public float initialSpeed = 7f;
    public float acceleration = 0.075f;
    public float maxSpeed = 10f;

    [Header("위치")]
    public float startX = -60f;          // 스타트라인(-3) 한참 뒤에서 출발
    public float screenPadding = 4f;     // 화면폭에서 이만큼 당겨 잡아 완전히 화면 밖에서 죽지 않게
    public float fallbackScreenWidth = 57f;   // 카메라를 못 찾을 때(헤드리스 등) 쓰는 값

    [Header("가시화")]
    public float wallHeight = 300f;
    public Color wallColor = new Color(0.85f, 0.15f, 0.15f, 0.65f);

    public float X { get; private set; }
    public float Elapsed { get; private set; }

    /// <summary>잡힌 순간 발행. 정산·관전 전환이 여기 붙는다.</summary>
    public static event System.Action<FlappyBird> Eliminated;
    public static FlappyChaser Instance { get; private set; }

    readonly FlappyChaserCurve curve = new FlappyChaserCurve();
    float curveX;
    Camera cam;

    void Awake() { Instance = this; }

    void OnEnable()
    {
        curveX = X = startX;
        Elapsed = 0f;
        EnsureWall();
    }

    /// <summary>카메라가 비추는 가로 폭(월드 단위). orthographic 전제.</summary>
    public float ScreenWidthWorld()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || !cam.orthographic) return fallbackScreenWidth;
        return cam.orthographicSize * 2f * cam.aspect;
    }

    public float LeaderX()
    {
        float best = float.NegativeInfinity;
        foreach (var b in FlappyBird.All)
            if (b != null && b.transform.position.x > best) best = b.transform.position.x;
        return float.IsNegativeInfinity(best) ? startX : best;
    }

    void Update()
    {
        if (FlappyRaceStart.RaceFrozen) return;   // 카운트다운 중엔 안 움직인다

        float dt = Time.deltaTime;
        Elapsed += dt;

        curve.InitialSpeed = initialSpeed;
        curve.Acceleration = acceleration;
        curve.MaxSpeed = maxSpeed;
        curveX += curve.SpeedAt(Elapsed) * dt;

        X = FlappyChaserPosition.Resolve(curveX, LeaderX(), Mathf.Max(1f, ScreenWidthWorld() - screenPadding));

        var p = transform.position;
        p.x = X;
        p.y = FollowY();
        transform.position = p;

        Catch();
    }

    // 벽이 화면 세로를 덮게 카메라 높이를 따라간다(고도 맵에서도 항상 막힌 것처럼 보이게).
    float FollowY()
    {
        if (cam == null) cam = Camera.main;
        return cam != null ? cam.transform.position.y : transform.position.y;
    }

    void Catch()
    {
        // 역순회 — Eliminated 핸들러가 오브젝트를 끄면 All에서 빠지므로
        for (int i = FlappyBird.All.Count - 1; i >= 0; i--)
        {
            var b = FlappyBird.All[i];
            if (b == null) continue;
            if (b.transform.position.x > X) continue;
            Debug.Log($"[Chaser] {b.name} 탈락 — t={Elapsed:F1}s, x={b.transform.position.x:F1}");
            Eliminated?.Invoke(b);
            if (b != null && b.gameObject.activeSelf) b.gameObject.SetActive(false);
        }
    }

    // 자식으로 붉은 벽 하나. 추격자의 정체(비급 소재)는 아트 단계에서 이 자리에 들어간다.
    void EnsureWall()
    {
        if (transform.Find("ChaserWall") != null) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "ChaserWall";
        var col = go.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);   // 물리 충돌 없음 — 판정은 X 비교로만
        go.transform.SetParent(transform, false);
        go.transform.localScale = new Vector3(2f, wallHeight, 2f);
        var mr = go.GetComponent<MeshRenderer>();
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh != null)
        {
            var m = new Material(sh);
            m.color = wallColor;
            mr.sharedMaterial = m;
        }
    }
}
