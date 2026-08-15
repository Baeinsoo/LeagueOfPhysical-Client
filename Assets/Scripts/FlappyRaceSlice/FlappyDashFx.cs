using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대시 연출 3종 — ① 새 스프라이트 잔상(afterimage) 트레일, ② 화면 가로 스피드라인(스크린 오버레이),
/// ③ 대시 시작 순간 카메라 흔들림. 에셋/파티클 불필요, 런타임 생성. FlappyPlayer.Dashing을 읽어 동작.
/// </summary>
[RequireComponent(typeof(FlappyPlayer))]
public class FlappyDashFx : MonoBehaviour
{
    [Header("잔상")]
    public Color trailColor = new Color(0.35f, 0.9f, 1f, 0.55f);
    public Color flashColor = new Color(0.9f, 1f, 1f, 0.9f);
    public float interval = 0.022f;
    public float life = 0.30f;

    [Header("스피드라인")]
    public int lineCount = 18;
    public Color lineColor = new Color(1f, 1f, 1f, 0.5f);

    [Header("카메라 흔들림")]
    public float shakeAmp = 0.6f;
    public float shakeDur = 0.18f;

    FlappyPlayer player;
    SpriteRenderer sr;
    FlappyCameraFollow cam;
    float lastSpawn;
    bool wasDashing;

    class Ghost { public SpriteRenderer sr; public float t; public float life; public Color c0; }
    readonly List<Ghost> ghosts = new List<Ghost>();

    // 스피드라인
    RectTransform[] lines;
    float[] lineY, lineW, lineSpeed;
    float canvasW = 1920f, canvasH = 1080f;
    float dashVis;   // 0..1 부드러운 대시 표시량(페이드)

    void Awake() { player = GetComponent<FlappyPlayer>(); sr = GetComponentInChildren<SpriteRenderer>(); }

    void Start()
    {
        cam = Camera.main != null ? Camera.main.GetComponent<FlappyCameraFollow>() : null;
        BuildSpeedLines();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        bool dashing = player != null && player.Dashing;

        // 대시 시작(상승엣지): 섬광 잔상 + 화면 흔들림
        if (dashing && !wasDashing)
        {
            if (sr != null) Spawn(flashColor);
            if (cam != null) cam.Shake(shakeAmp, shakeDur);
        }
        if (dashing && sr != null && Time.time - lastSpawn >= interval) { Spawn(trailColor); lastSpawn = Time.time; }
        wasDashing = dashing;

        UpdateGhosts(dt);
        UpdateSpeedLines(dt, dashing);
    }

    // ---------- 잔상 ----------
    void Spawn(Color col)
    {
        var go = new GameObject("DashGhost");
        var gsr = go.AddComponent<SpriteRenderer>();
        gsr.sprite = sr.sprite; gsr.flipX = sr.flipX; gsr.flipY = sr.flipY;
        gsr.sortingLayerID = sr.sortingLayerID; gsr.sortingOrder = sr.sortingOrder - 1;
        var t = sr.transform;
        go.transform.position = t.position; go.transform.rotation = t.rotation; go.transform.localScale = t.lossyScale;
        gsr.color = col;
        ghosts.Add(new Ghost { sr = gsr, t = 0f, life = life, c0 = col });
    }

    void UpdateGhosts(float dt)
    {
        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            var g = ghosts[i]; g.t += dt;
            float k = 1f - g.t / g.life;
            if (k <= 0f || g.sr == null) { if (g.sr != null) Destroy(g.sr.gameObject); ghosts.RemoveAt(i); continue; }
            var c = g.c0; c.a = g.c0.a * k; g.sr.color = c;
        }
    }

    // ---------- 스피드라인 ----------
    void BuildSpeedLines()
    {
        var cgo = new GameObject("FlappyDashFx_Canvas", typeof(Canvas), typeof(CanvasScaler));
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
        var scaler = cgo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(canvasW, canvasH); scaler.matchWidthOrHeight = 0.5f;

        lines = new RectTransform[lineCount];
        lineY = new float[lineCount]; lineW = new float[lineCount]; lineSpeed = new float[lineCount];
        for (int i = 0; i < lineCount; i++)
        {
            var go = new GameObject("spd" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(cgo.transform, false);
            var img = go.GetComponent<Image>(); img.color = lineColor; img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
            // 화면 가장자리(위/아래)에 몰아 배치 — 가운데(새)는 비워 시야 확보
            float edge = (i % 2 == 0) ? 1f : -1f;
            float band = Mathf.Lerp(0.12f, 0.5f, (i / (float)lineCount));
            lineY[i] = edge * band * canvasH;
            lineW[i] = Random.Range(120f, 380f);
            lineSpeed[i] = Random.Range(2600f, 4200f);   // px/s, 왼쪽으로 흐름
            float h = Random.Range(3f, 6f);
            rt.sizeDelta = new Vector2(lineW[i], h);
            rt.anchoredPosition = new Vector2(Random.Range(0f, canvasW), lineY[i]);
            lines[i] = rt;
        }
    }

    void UpdateSpeedLines(float dt, bool dashing)
    {
        // 대시 시 빠르게 나타나고(0.06s), 끝나면 천천히 사라짐(0.18s)
        dashVis = Mathf.MoveTowards(dashVis, dashing ? 1f : 0f, dt / (dashing ? 0.06f : 0.18f));
        if (lines == null) return;
        for (int i = 0; i < lines.Length; i++)
        {
            var rt = lines[i];
            var pos = rt.anchoredPosition;
            pos.x -= lineSpeed[i] * dt;                          // 왼쪽으로 흐름 = 전진감
            if (pos.x + lineW[i] < 0f) { pos.x = canvasW + Random.Range(0f, 300f); }  // 화면 밖이면 오른쪽에서 재등장
            rt.anchoredPosition = pos;
            var img = rt.GetComponent<Image>();
            var c = lineColor; c.a = lineColor.a * dashVis;      // 대시량만큼 보임
            img.color = c;
        }
    }
}
