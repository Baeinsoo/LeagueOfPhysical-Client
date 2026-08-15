using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 봇 오토파일럿 — 시뮬레이션을 게임 화면에서 눈으로 보게 재생.
/// FlappyPlayer 물리를 복제해 새를 자동 비행(예측 제어로 다음 벽 틈 중심 겨냥). 켜지면 FlappyPlayer 조작은 끔.
/// 장애물과 겹치면(클립) 새가 빨갛게 깜빡. 코스 끝나면 처음부터 반복.
/// </summary>
[RequireComponent(typeof(FlappyPlayer))]
public class FlappyAutoPilot : MonoBehaviour
{
    public Transform courseRoot;   // ---Course---
    public float startX = -3f;
    public float startY = 0f;
    public float flapCooldown = 0.12f;  // 사람 탭 한계(~8/s) — 매 프레임 스팸 방지(시뮬 신뢰도)
    public bool showHud = true;         // 레이스에서 AI는 끔(순위 HUD가 대신)
    public float ghostTime = 0.8f;      // 충돌 시 정지(플레이어와 동일한 페널티=공정 레이스)
    public float invulnTime = 0.6f;
    float ghostT, invuln;
    Color baseColor = Color.white;

    [Header("사람 모사 모드 (끄면 최적 봇 = feasibility 테스터)")]
    public bool humanMode = false;
    public float reactionDelay = 0.15f;  // 틈 바닥에 근접하고 반응하기까지 지연
    public float aimError = 0.8f;        // 조준 오차 — 플랩 높이 랜덤 편차(±)

    float lastFlapT, belowSince = -1f, aimNoise = 0f;

    FlappyPlayer player;
    SpriteRenderer sr;
    float px, py, vy, clipFlash;
    float birdR, fwd, flap, grav, maxFall, eAmp, eWl, eSx, eDrop, eCeil; bool eOn, eSharp;
    float endX;
    readonly List<float> wx = new List<float>();
    readonly List<List<float[]>> openings = new List<List<float[]>>();
    readonly HashSet<int> clipped = new HashSet<int>();
    int lapClips, lapCount;

    void OnEnable()
    {
        player = GetComponent<FlappyPlayer>();
        player.enabled = false;                 // 수동 조작 끔
        var sc = GetComponent<SphereCollider>();
        birdR = sc != null ? sc.radius : 0.45f;
        fwd = player.forwardSpeed; flap = player.flapImpulse; grav = player.gravity; maxFall = player.maxFall;
        eOn = player.elevationFloor; eAmp = player.elevAmp; eWl = player.elevWavelength; eSx = player.elevStartX; eDrop = player.floorDrop; eCeil = player.ceilRise; eSharp = player.elevSharp;
        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
        ScanCourse();
        Restart();
    }

    void OnDisable() { if (player != null) player.enabled = true; }

    void Restart()
    {
        px = startX; py = startY; vy = 0f; lastFlapT = -1f; belowSince = -1f; aimNoise = 0f; ghostT = 0f; invuln = 0f;
        transform.position = new Vector3(px, py, 0f);
        clipped.Clear(); lapClips = 0;
    }

    void ScanCourse()
    {
        wx.Clear(); openings.Clear();
        if (courseRoot == null) return;
        var list = new List<Transform>();
        foreach (Transform c in courseRoot) if (c.name != "Ground" && c.name != "Ceiling") list.Add(c);
        list.Sort((a, b) => a.position.x.CompareTo(b.position.x));
        foreach (var w in list)
        {
            float x = w.position.x; var cols = w.GetComponentsInChildren<Collider>();
            var o = new List<float[]>(); float rs = float.NaN, prev = -40f;
            for (float y = -40f; y <= 40f; y += 0.1f)
            {
                bool blocked = false;
                foreach (var col in cols)
                {
                    if (!col.enabled) continue;
                    var b = col.bounds;
                    if (x >= b.min.x - birdR && x <= b.max.x + birdR && y >= b.min.y - birdR && y <= b.max.y + birdR) { blocked = true; break; }
                }
                if (!blocked) { if (float.IsNaN(rs)) rs = y; }
                else { if (!float.IsNaN(rs)) { if (prev - rs >= birdR * 2f) o.Add(new float[] { rs, prev }); rs = float.NaN; } }
                prev = y;
            }
            if (!float.IsNaN(rs) && 40f - rs >= birdR * 2f) o.Add(new float[] { rs, 40f });
            wx.Add(x); openings.Add(o);
        }
        endX = wx.Count > 0 ? wx[wx.Count - 1] + 6f : 50f;
        lapCount = wx.Count;
    }

    void Update()
    {
        float dt = Time.deltaTime; if (dt > 0.05f) dt = 0.05f;

        // 유령 정지(충돌 페널티) — 플레이어와 동일, 그 자리 멈췄다 복구
        if (ghostT > 0f) { ghostT -= dt; if (ghostT <= 0f) invuln = invulnTime; if (sr != null) sr.color = new Color(0.6f, 0.6f, 0.7f, 0.7f); return; }
        if (invuln > 0f) invuln -= dt;

        // 접근 중인 벽의, 현재 y에 가장 가까운 틈 [lo,hi]를 겨냥
        int ti = -1; float lo = py - 1f, hi = py + 1f;
        for (int i = 0; i < wx.Count; i++)
        {
            if (wx[i] + 0.9f > px)
            {
                ti = i;
                if (openings[i].Count > 0)
                {
                    float bd = 1e9f;
                    foreach (var g in openings[i]) { float ctr = (g[0] + g[1]) * 0.5f; if (Mathf.Abs(ctr - py) < bd) { bd = Mathf.Abs(ctr - py); lo = g[0]; hi = g[1]; } }
                }
                break;
            }
        }

        // 틈 바닥 근처에서 탭해 아치로 통과(center-free 기준). 최적 vs 사람모사 두 모드.
        float safeFloor = lo + 0.5f, safeCeil = hi - 0.5f;
        if (!humanMode)
        {
            // 최적 봇(feasibility): 쿨타임 없음 = 게임 실제 능력(매 프레임 탭 가능, TAS 상한). 틈 바닥에 호버링해 진짜 물리 한계를 봄.
            if (py < safeFloor && py < safeCeil) { vy = flap; lastFlapT = Time.time; }
        }
        else
        {
            // 사람 모사: 틈 하부-중앙(gapLo+0.4폭) 겨냥 + 조준 오차 + 반응 지연 → 큰 틈일수록 오차 여유
            float thr = lo + 0.4f * (hi - lo) + aimNoise;
            if (py < thr && py < safeCeil)
            {
                if (belowSince < 0f) belowSince = Time.time;
                if (Time.time - belowSince >= reactionDelay && Time.time - lastFlapT >= flapCooldown)
                { vy = flap; lastFlapT = Time.time; belowSince = -1f; aimNoise = Random.Range(-aimError, aimError); }
            }
            else belowSince = -1f;
        }

        vy -= grav * dt; if (vy < -maxFall) vy = -maxFall;
        py += vy * dt; px += fwd * dt;

        float sinE = eOn ? FlappyElevation.Value(px, eAmp, eSx, eWl, eSharp) : 0f;
        float fy = eOn ? sinE - eDrop : player.groundY;
        float cy = eOn ? sinE + eCeil : player.ceilingY;
        if (py > cy) { py = cy; vy = 0f; }   // 천장: 위로 못 넘어감
        if (py < fy) { py = fy; vy = 0f; }
        transform.position = new Vector3(px, py, 0f);
        transform.localRotation = Quaternion.Euler(0f, player.faceY, Mathf.Clamp(-vy * 3.5f, -28f, 70f));

        // 클립 감지 → 빨강 깜빡
        bool clip = false;
        foreach (var col in Physics.OverlapSphere(new Vector3(px, py, 0f), birdR))
            if (col.enabled && col.GetComponent<FlappyObstacle>() != null) { clip = true; break; }
        if (clip) { clipFlash = 0.2f; if (ti >= 0) clipped.Add(ti); if (invuln <= 0f) ghostT = ghostTime; }
        if (sr != null) sr.color = clipFlash > 0f ? Color.red : baseColor;
        if (clipFlash > 0f) clipFlash -= dt;

        lapClips = clipped.Count;
        if (px >= endX) Restart();   // 반복
    }

    void OnGUI()
    {
        if (!showHud) return;
        var s = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(14, 10, 500, 28), "🤖 오토파일럿 봇 — " + (humanMode ? "사람 모사(반응지연+조준오차)" : "최적(feasibility)"), s);
        GUI.Label(new Rect(14, 36, 500, 28), "게이트 " + lapCount + " | 스친 벽 " + lapClips + " | 빨강 깜빡 = 클립", s);
        GUI.Label(new Rect(14, 62, 600, 22), "이 컴포넌트를 끄면 수동 플레이", new GUIStyle(GUI.skin.label) { fontSize = 13 });
    }
}
