using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 맵 밸런싱용 시뮬레이션 판정기. 화면 재생(FlappyAutoPilot)과 별개인 헤드리스 순수 계산.
/// 세 도구를 한 번에 돌려 콘솔에 리포트:
///   A. 통과 가능성 — 상태공간(y,vy) 도달성 탐색. 경로 존재 여부(참 물리 한계, 봇 실력 무관).
///   B. 체감 난이도 — 근사최적 컨트롤러 + 타이밍 지터로 실력 3단 몬테카를로. 충돌=유령정지(시간손실).
///   C. 게이트 구조 — 각 틈 span을 플랩아치와 비교(타이트/트위치/여유). 밸런싱 직독용.
/// 판정 기준: A=경로 존재 필수 / B=실력 그래디언트 존재 & 고수 일부 클린런 / C=타이트·여유 리듬.
/// 인스펙터에서 우클릭 → "Run Sim Judge" (플레이 없이 에디트모드에서 동작).
/// </summary>
public class FlappySimJudge : MonoBehaviour
{
    [Header("시작/코스")]
    public float startX = -3f;
    public float startY = 0f;

    [Header("몬테카를로")]
    public int samplesPerTier = 200;
    public float simDt = 0.02f;
    public float flapCooldown = 0.10f;
    public float invulnTime = 0.6f;
    public int randomSeed = 20260718;

    [Header("실력 3단 (반응지연 / 타이밍지터 ±)")]
    public float[] reactDelay = { 0.20f, 0.13f, 0.08f };
    public float[] timingJitter = { 0.08f, 0.05f, 0.03f };
    static readonly string[] TierNames = { "초보", "중수", "고수" };

    // 실행 중 채워지는 물리/장애물 캐시
    FlappyPlayer player;
    float fwd, flap, grav, maxFall, birdR, arc;
    bool eOn, eSharp;
    float eAmp, eWl, eSx, eDrop, eCeil, fixFloor, fixCeil;
    float endX, cdx = 0.2f;
    int nCol;
    List<float[]>[] colOpen;
    bool[] colHasOb;
    bool[] colDonut;   // 도넛(회전 원형) 컬럼 — 시뮬이 정확히 못 다루므로 난이도 계수에서 제외

    [Header("행동복제(BC) — 실험용(누적오차로 보류). 프록시+변환 노선에선 off")]
    public bool useBCPolicy = false;  // BC 가중치 있으면 band 대신 학습된 정책 사용
    public const float ProxyReact = 0.06f, ProxyJitter = 0.03f;  // 프록시 봇 고정 파라미터(일관성)
    FlappyCourseScan.Scan scan;
    float[] bcWeights;
    readonly float[] bcFeat = new float[FlappyBC.N];

    float FloorAt(float x) => eOn ? FlappyElevation.Value(x, eAmp, eSx, eWl, eSharp) - eDrop : fixFloor;
    float CeilAt(float x) => eOn ? FlappyElevation.Value(x, eAmp, eSx, eWl, eSharp) + eCeil : fixCeil;

    // 도넛 소속 여부 — 조상 트랜스폼에 "Donut" 이름이 있으면 도넛(회전 원형).
    static bool IsDonut(Transform t)
    {
        for (var p = t; p != null; p = p.parent) if (p.name == "Donut") return true;
        return false;
    }

    [ContextMenu("Run Sim Judge")]
    public void RunJudge()
    {
        if (!Prepare()) return;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("======== Flappy Sim Judge ========");
        sb.AppendLine($"물리: fwd={fwd} flap={flap} grav={grav} maxFall={maxFall} birdR={birdR:F3} 플랩아치={arc:F2}");
        sb.AppendLine($"고도: on={eOn} amp={eAmp} wl={eWl} drop={eDrop} ceil={eCeil} sharp={eSharp}  코스 x[{startX}..{endX:F0}]");
        ToolA_Feasibility(sb);
        ToolC_Structure(sb);
        ToolB_Difficulty(sb);
        AppendPrediction(sb);
        sb.AppendLine("==================================");
        Debug.Log(sb.ToString());
    }

    bool Prepare()
    {
        player = Object.FindObjectOfType<FlappyPlayer>();
        if (player == null) { Debug.LogError("FlappySimJudge: FlappyPlayer 없음"); return false; }
        scan = FlappyCourseScan.Build(player, startX);
        // 공유 스캐너 결과를 기존 필드로 복사(Tool A/B/C 무변경) + BC 특징 계산용 scan 보관
        endX = scan.endX; nCol = scan.nCol; cdx = scan.cdx;
        colOpen = scan.colOpen; colHasOb = scan.colHasOb; colDonut = scan.colDonut;
        fwd = scan.fwd; flap = scan.flap; grav = scan.grav; maxFall = scan.maxFall; arc = scan.arc; birdR = scan.birdR;
        eOn = scan.eOn; eAmp = scan.eAmp; eWl = scan.eWl; eSx = scan.eSx; eDrop = scan.eDrop; eCeil = scan.eCeil; eSharp = scan.eSharp;
        fixFloor = scan.fixFloor; fixCeil = scan.fixCeil;
        bcWeights = FlappyBC.LoadWeights();
        return true;
    }

    // A. 통과 가능성 — (y,vy) 상태공간 전방 도달성 탐색
    void ToolA_Feasibility(System.Text.StringBuilder sb)
    {
        float dx = 0.1f, dt = dx / fwd;
        var cur = new Dictionary<long, float[]>();
        cur[(long)System.Math.Round(startY * 10f) * 1000 + 500] = new float[] { startY, 0f };
        int cols = 0, minCount = int.MaxValue; float minX = startX, minSpan = 0f;
        bool reached = false; float x = startX; int safety = 0;
        while (x < endX && safety < 40000)
        {
            safety++; float nx = x + dx;
            float fy = FloorAt(nx), cy = CeilAt(nx);
            var next = new Dictionary<long, float[]>();
            // 근처 장애물 bounds
            var near = new List<Bounds>();
            // colOpen 대신 직접 검사가 필요하므로 개구 이용: nx 컬럼 개구 안이면 통과
            int ci = (int)((nx - startX) / cdx); if (ci < 0) ci = 0; if (ci >= nCol) ci = nCol - 1;
            var open = colOpen[ci];
            foreach (var kv in cur)
            {
                float y = kv.Value[0], vy = kv.Value[1];
                for (int fc = 0; fc < 2; fc++)
                {
                    float nvy = (fc == 0) ? flap : vy;
                    nvy -= grav * dt; if (nvy < -maxFall) nvy = -maxFall;
                    float ny = y + nvy * dt;
                    if (ny > cy) { ny = cy; nvy = 0f; }
                    if (ny < fy) { ny = fy; nvy = 0f; }
                    bool inside = false; foreach (var g in open) { if (ny >= g[0] && ny <= g[1]) { inside = true; break; } }
                    if (!inside) continue;
                    long key = (long)System.Math.Round(ny * 10f) * 1000 + ((long)System.Math.Round(nvy) + 500);
                    if (!next.ContainsKey(key)) next[key] = new float[] { ny, nvy };
                }
            }
            cur = next; cols++;
            if (cur.Count == 0) { reached = false; break; }
            if (cols > 60 && cur.Count < minCount)  // 시작 시드 과도기(앞 6유닛) 제외
            {
                float ylo = 1e9f, yhi = -1e9f;
                foreach (var kv in cur) { if (kv.Value[0] < ylo) ylo = kv.Value[0]; if (kv.Value[0] > yhi) yhi = kv.Value[0]; }
                minCount = cur.Count; minX = nx; minSpan = yhi - ylo;
            }
            x = nx; if (x >= endX) reached = true;
        }
        sb.AppendLine("── A. 통과 가능성 (도달성 탐색) ──");
        sb.AppendLine(reached ? "  ✅ 경로 존재 = 통과 가능" : $"  ❌ 막힘 (x={x:F1}에서 생존 상태 0 → 이 맵은 불통, 리젝)");
        if (minCount != int.MaxValue) sb.AppendLine($"  최협 회랑: x={minX:F0} 생존상태 {minCount} y폭 {minSpan:F2}");
    }

    // C. 게이트 구조 — 각 게이트 최대슬롯 span vs 아치
    void ToolC_Structure(System.Text.StringBuilder sb)
    {
        int tight = 0, twitchy = 0, easy = 0, gateN = 0;
        var spans = new List<float>();
        int i = 0;
        while (i < nCol)
        {
            if (!colHasOb[i]) { i++; continue; }
            int j = i; while (j < nCol && colHasOb[j]) j++;
            float runMin = 1e9f;
            for (int k = i; k < j; k++) { float best = 0f; foreach (var g in colOpen[k]) { float s = g[1] - g[0]; if (s > best) best = s; } if (best < runMin) runMin = best; }
            if (runMin < 1e8f && runMin > 0.2f)
            { gateN++; spans.Add(runMin); if (runMin < arc) tight++; else if (runMin < arc * 1.35f) twitchy++; else easy++; }
            i = j;
        }
        sb.AppendLine("── C. 게이트 구조 (span vs 아치) ──");
        sb.AppendLine($"  게이트 {gateN}개  타이트(<{arc:F1}): {tight}  ◆트위치({arc:F1}~{arc * 1.35f:F1}): {twitchy}  ·여유(≥{arc * 1.35f:F1}): {easy}");
        var line = new System.Text.StringBuilder("  span: ");
        foreach (var s in spans) { string tag = s < arc ? "!" : (s < arc * 1.35f ? "◆" : "·"); line.Append($"{s:F1}{tag} "); }
        sb.AppendLine(line.ToString());
        sb.AppendLine("  (! = 아치보다 좁음: 정점타이밍 필수 / ◆ = 트위치 스윗 / · = 여유)");
    }

    int NBucket => (int)((endX - startX) / 10f) + 1;

    // 한 실력(reaction/jitter)으로 samples판 몬테카를로 → 평균충돌 반환, heat/clean 채움.
    // band 컨트롤러(바로 앞 틈 겨냥 + 아치 여유) + 타이밍 지터. 도넛 컬럼은 계수 제외.
    float SimTier(float reaction, float jitter, int samples, int[] heat, out int cleanRuns)
    {
        cleanRuns = 0; int totalClips = 0;
        for (int i = 0; i < heat.Length; i++) heat[i] = 0;
        int nBucket = heat.Length;
        for (int run = 0; run < samples; run++)
        {
            float px = startX, py = startY, vy = 0f, t = 0f, lastFlap = -1f, intentT = -1f, invuln = 0f;
            int clips = 0, guard = 0;
            while (px < endX && guard < 8000)
            {
                guard++;
                bool wantFlap;
                if (bcWeights != null && useBCPolicy)
                {
                    // 학습된 사람 정책: 특징 → P(flap)>0.5면 플랩 의도
                    scan.Target(px, py, out float _lo, out float _hi, out float _pLo, out float _tg);
                    FlappyBC.Features(py - _pLo, vy, _tg, bcFeat);
                    wantFlap = FlappyBC.PFlap(bcWeights, bcFeat) > 0.5f;
                    // 가드레일: 파이프 임박 시 강제 억제/플랩 → BC 누적오차 드리프트 방지
                    if (py >= _hi - 0.3f) wantFlap = false;
                    else if (py <= _lo + 0.3f) wantFlap = true;
                }
                else
                {
                    // 기본 band 컨트롤러(BC 미학습 시)
                    float aheadX = px + 1.5f;
                    int ci = (int)((aheadX - startX) / cdx); if (ci < 0) ci = 0; if (ci >= nCol) ci = nCol - 1;
                    float lo = FloorAt(aheadX), hi = CeilAt(aheadX), bd = 1e9f;
                    foreach (var g in colOpen[ci]) { float ctr = (g[0] + g[1]) * 0.5f; if (System.Math.Abs(ctr - py) < bd) { bd = System.Math.Abs(ctr - py); lo = g[0]; hi = g[1]; } }
                    float span = hi - lo;
                    float pLo = (span >= arc) ? lo + (span - arc) * 0.5f : lo;
                    wantFlap = py <= pLo;
                }
                if (wantFlap) { if (intentT < 0f) intentT = t + reaction + Random.Range(-jitter, jitter); }
                else intentT = -1f;
                if (intentT >= 0f && t >= intentT && (t - lastFlap) >= flapCooldown) { vy = flap; lastFlap = t; intentT = -1f; }
                vy -= grav * simDt; if (vy < -maxFall) vy = -maxFall;
                py += vy * simDt; px += fwd * simDt; t += simDt;
                float fy = FloorAt(px), cy = CeilAt(px);
                if (py > cy) { py = cy; vy = 0f; } if (py < fy) { py = fy; vy = 0f; }
                if (invuln > 0f) invuln -= simDt;
                int cci = (int)((px - startX) / cdx); if (cci < 0) cci = 0; if (cci >= nCol) cci = nCol - 1;
                bool inside = false; foreach (var g in colOpen[cci]) { if (py >= g[0] && py <= g[1]) { inside = true; break; } }
                if (!inside && invuln <= 0f && !colDonut[cci]) { clips++; invuln = invulnTime; int bk = (int)((px - startX) / 10f); if (bk >= 0 && bk < nBucket) heat[bk]++; }
            }
            totalClips += clips; if (clips == 0) cleanRuns++;
        }
        return (float)totalClips / samples;
    }

    // B. 체감 난이도 — 실력 3단
    void ToolB_Difficulty(System.Text.StringBuilder sb)
    {
        Random.InitState(randomSeed);
        int nBucket = NBucket;
        var heat = new int[nBucket];
        sb.AppendLine($"── B. 체감 난이도 (band컨트롤+타이밍지터, {samplesPerTier}×3단, 도넛 제외) ──");
        for (int tier = 0; tier < 3; tier++)
        {
            int clean;
            float mean = SimTier(reactDelay[tier], timingJitter[tier], samplesPerTier, heat, out clean);
            var top = new List<int>(); for (int b = 0; b < nBucket; b++) top.Add(b);
            top.Sort((a, b) => heat[b].CompareTo(heat[a]));
            var hs = new System.Text.StringBuilder();
            for (int k = 0; k < 5 && k < top.Count; k++) { int b = top[k]; if (heat[b] == 0) continue; hs.Append($"x{startX + b * 10:F0}({100f * heat[b] / samplesPerTier:F0}%) "); }
            sb.AppendLine($"  [{TierNames[tier]}] 반응{reactDelay[tier]}±{timingJitter[tier]} | 평균충돌 {mean:F2}회 | 클린런 {100f * clean / samplesPerTier:F0}% | 핫스팟 {hs}");
        }
    }

    // ===== 자동 캘리브레이션 (사람 로그 → 봇 파라미터 피팅) =====

    /// <summary>사람 플레이 텔레메트리 파일 경로(프로젝트 루트 밖 Assets, import 방지). Recorder가 씀, 여기가 읽음.</summary>
    public static string HumanDataPath =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "FlappyCalibration", "human_runs.csv"));

    [ContextMenu("Clear Human Data")]
    public void ClearHumanData()
    {
        if (System.IO.File.Exists(HumanDataPath)) System.IO.File.Delete(HumanDataPath);
        Debug.Log("[Calibrate] 사람 로그 삭제됨: " + HumanDataPath);
    }

    [ContextMenu("Calibrate To Human")]
    public void CalibrateToHuman()
    {
        if (!Prepare()) return;
        if (!System.IO.File.Exists(HumanDataPath)) { Debug.LogError("[Calibrate] 사람 로그 없음. 플레이(Recorder)로 먼저 몇 랩 뛰어야 함: " + HumanDataPath); return; }

        // 사람 로그 읽기: 각 줄 = clips,b0,b1,...  → 평균 충돌 + 버킷별 평균
        var lines = System.IO.File.ReadAllLines(HumanDataPath);
        int nBucket = NBucket;
        float humanMean = 0f; var humanBucket = new float[nBucket]; int laps = 0;
        foreach (var ln in lines)
        {
            var parts = ln.Split(',');
            if (parts.Length < 1 || !int.TryParse(parts[0], out int c)) continue;
            humanMean += c; laps++;
            for (int b = 1; b < parts.Length && (b - 1) < nBucket; b++) if (int.TryParse(parts[b], out int hv)) humanBucket[b - 1] += hv;
        }
        if (laps == 0) { Debug.LogError("[Calibrate] 유효한 랩 없음"); return; }
        humanMean /= laps; for (int b = 0; b < nBucket; b++) humanBucket[b] /= laps;

        // 그리드 서치: (반응, 지터) 후보를 시뮬해 사람 평균+분포에 가장 가까운 조합 찾기
        Random.InitState(randomSeed);
        float[] rGrid = { 0.03f, 0.04f, 0.05f, 0.06f, 0.07f, 0.08f, 0.10f, 0.12f, 0.15f };
        float[] jGrid = { 0.01f, 0.02f, 0.03f, 0.04f, 0.05f, 0.07f };
        var heat = new int[nBucket];
        float bestLoss = 1e9f, bestR = reactDelay[2], bestJ = timingJitter[2], bestMean = 0f;
        int fitSamples = 200;
        foreach (var r in rGrid)
            foreach (var j in jGrid)
            {
                int clean;
                float mean = SimTier(r, j, fitSamples, heat, out clean);
                float distTerm = 0f;
                for (int b = 0; b < nBucket; b++) distTerm += Mathf.Abs((float)heat[b] / fitSamples - humanBucket[b]);
                float loss = Mathf.Abs(mean - humanMean) + 0.3f * distTerm;   // 평균 우선 + 핫스팟 분포 보조
                if (loss < bestLoss) { bestLoss = loss; bestR = r; bestJ = j; bestMean = mean; }
            }

        // 피팅 결과 = "고수"(=너). 초/중은 실력 스프레드 유지해 재도출.
        reactDelay[2] = bestR; timingJitter[2] = bestJ;
        reactDelay[1] = bestR * 1.6f; timingJitter[1] = bestJ * 1.7f;
        reactDelay[0] = bestR * 2.5f; timingJitter[0] = bestJ * 2.7f;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("======== 자동 캘리브레이션 결과 ========");
        sb.AppendLine($"사람 로그: {laps}랩, 평균충돌 {humanMean:F2}회");
        sb.AppendLine($"피팅된 '고수'(=너): 반응 {bestR:F3}s ± 지터 {bestJ:F3}s → 봇 평균충돌 {bestMean:F2}회 (사람과 오차 {Mathf.Abs(bestMean-humanMean):F2})");
        sb.AppendLine($"재도출 실력단: 초보 반응{reactDelay[0]:F3}±{timingJitter[0]:F3} / 중수 {reactDelay[1]:F3}±{timingJitter[1]:F3} / 고수 {reactDelay[2]:F3}±{timingJitter[2]:F3}");
        sb.AppendLine("→ 이제 Run Sim Judge 하면 이 맵·다른 맵 모두 '너 기준' 난이도로 평가됨.");
        sb.AppendLine("=======================================");
        Debug.Log(sb.ToString());
    }

    [ContextMenu("Fit Behavior Policy (BC)")]
    public void FitBehaviorPolicy()
    {
        if (!System.IO.File.Exists(FlappyBC.FeaturesPath)) { Debug.LogError("[BC] 특징 로그 없음. Recorder로 몇 랩 플레이 먼저: " + FlappyBC.FeaturesPath); return; }
        var lines = System.IO.File.ReadAllLines(FlappyBC.FeaturesPath);
        var X = new List<float[]>(); var Y = new List<int>(); int pos = 0;
        foreach (var ln in lines)
        {
            var p = ln.Split(',');
            if (p.Length < 4) continue;
            if (!float.TryParse(p[0], out float dH) || !float.TryParse(p[1], out float vy) || !float.TryParse(p[2], out float tg) || !int.TryParse(p[3], out int fl)) continue;
            var f = new float[FlappyBC.N];
            FlappyBC.Features(dH, vy, tg, f);
            X.Add(f); Y.Add(fl); if (fl == 1) pos++;
        }
        int M = X.Count;
        if (M < 50 || pos < 5) { Debug.LogError($"[BC] 데이터 부족 (총 {M}행, 플랩 {pos}). 더 플레이 필요."); return; }

        // 로지스틱 회귀 — 배치 경사하강 + L2. 양성 가중은 완만(과예측 방지), 운용점은 아래 threshold 보정으로 맞춤.
        float posW = Mathf.Min(8f, (float)(M - pos) / pos);
        var w = new float[FlappyBC.N];
        var grad = new float[FlappyBC.N];
        float lr = 0.3f, lambda = 0.001f; int epochs = 600;
        for (int e = 0; e < epochs; e++)
        {
            for (int i = 0; i < FlappyBC.N; i++) grad[i] = 0f;
            for (int m = 0; m < M; m++)
            {
                var f = X[m]; int y = Y[m];
                float pp = FlappyBC.PFlap(w, f);
                float err = (pp - y) * (y == 1 ? posW : 1f);
                for (int i = 0; i < FlappyBC.N; i++) grad[i] += err * f[i];
            }
            for (int i = 0; i < FlappyBC.N; i++) w[i] -= lr * (grad[i] / M + lambda * w[i]);
        }

        // 운용 threshold 보정 — 봇 플랩율을 사람 실제율에 맞춰 bias 이동(P>0.5 ⟺ 원래 P>τ)
        var ps = new float[M];
        for (int m = 0; m < M; m++) ps[m] = FlappyBC.PFlap(w, X[m]);
        System.Array.Sort(ps);
        float targetRate = (float)pos / M;
        int idx = Mathf.Clamp((int)((1f - targetRate) * M), 0, M - 1);
        float tau = Mathf.Clamp(ps[idx], 1e-4f, 1f - 1e-4f);
        w[0] -= Mathf.Log(tau / (1f - tau));

        int correct = 0, predFlap = 0;
        for (int m = 0; m < M; m++) { bool pf = FlappyBC.PFlap(w, X[m]) > 0.5f; if (pf) predFlap++; if (pf == (Y[m] == 1)) correct++; }

        System.IO.File.WriteAllText(FlappyBC.WeightsPath, string.Join(",", System.Array.ConvertAll(w, x => x.ToString("F5"))));
        bcWeights = w;
        reactDelay[2] = 0f; timingJitter[2] = 0f;          // 고수 = 순수 BC(=너)
        reactDelay[1] = 0.06f; timingJitter[1] = 0.04f;    // 중수 = 약간 오차
        reactDelay[0] = 0.14f; timingJitter[0] = 0.09f;    // 초보 = 큰 오차

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("======== 행동복제(BC) 학습 결과 ========");
        sb.AppendLine($"학습 데이터: {M}행 (플랩 {pos}, {100f * pos / M:F1}%)");
        sb.AppendLine($"가중치 [bias,높이차,vy,게이트시간] = [{string.Join(", ", System.Array.ConvertAll(w, x => x.ToString("F3")))}]");
        sb.AppendLine($"학습 정확도 {100f * correct / M:F1}% | 예측 플랩율 {100f * predFlap / M:F1}% vs 실제 {100f * pos / M:F1}%");
        sb.AppendLine("→ Run Sim Judge 하면 봇이 이 정책(=너)으로 조종. 고수=순수 너, 초/중=오차 주입.");
        sb.AppendLine("======================================");
        Debug.Log(sb.ToString());
    }

    // ===== 프록시 + 변환곡선 (안정적 band-봇 프록시 → 사람 점수 변환) =====

    public static string TransferPath => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "FlappyCalibration", "transfer_points.csv"));
    public static string TransferFitPath => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "FlappyCalibration", "transfer_fit.csv"));

    float ProxyScore()
    {
        var heat = new int[NBucket]; int clean;
        bool save = useBCPolicy; useBCPolicy = false;   // 프록시는 항상 band(안정적)
        float s = SimTier(ProxyReact, ProxyJitter, 200, heat, out clean);
        useBCPolicy = save;
        return s;
    }

    // 코스 벽(게이트) 수 — 길이 정규화용. 총충돌은 벽수에 비례해 왜곡되므로 벽당(per-wall)으로 변환.
    int WallCount()
    {
        var gen = Object.FindObjectOfType<FlappyCourseGenerator>();
        Transform root = gen != null ? (gen.courseRoot != null ? gen.courseRoot : gen.transform) : null;
        if (root == null) return Mathf.Max(1, nCol / 10);
        int w = 0; foreach (Transform c in root) if (c.name != "Ground" && c.name != "Ceiling") w++;
        return Mathf.Max(1, w);
    }

    float ReadHumanMean(out int laps)
    {
        laps = 0; float sum = 0f;
        if (!System.IO.File.Exists(HumanDataPath)) return -1f;
        foreach (var ln in System.IO.File.ReadAllLines(HumanDataPath))
        { var p = ln.Split(','); if (p.Length >= 1 && int.TryParse(p[0], out int c)) { sum += c; laps++; } }
        return laps > 0 ? sum / laps : -1f;
    }

    [ContextMenu("1. Record Calibration Point")]
    public void RecordCalibrationPoint()
    {
        if (!Prepare()) return;
        int laps; float human = ReadHumanMean(out laps);
        if (human < 0f) { Debug.LogError("[Transfer] 이 맵의 사람 로그 없음. Recorder로 먼저 플레이: " + HumanDataPath); return; }
        float bot = ProxyScore();
        int walls = WallCount();
        var dir = System.IO.Path.GetDirectoryName(TransferPath);
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        System.IO.File.AppendAllText(TransferPath, $"{bot:F3},{human:F3},{walls},{laps}\n");
        System.IO.File.Delete(HumanDataPath);   // 다음 맵을 위해 초기화
        Debug.Log($"[Transfer] 포인트 기록: 봇 {bot:F2} ↔ 너 {human:F2} (벽 {walls}, {laps}랩). human_runs 초기화 → 다음 맵 만들고 플레이.");
    }

    [ContextMenu("2. Fit Transfer & Predict")]
    public void FitTransferAndPredict()
    {
        if (!System.IO.File.Exists(TransferPath)) { Debug.LogError("[Transfer] 포인트 없음. 먼저 'Record Calibration Point'."); return; }
        var pts = new List<float[]>();   // {봇/벽, 너/벽, 벽, 봇총, 너총}
        foreach (var ln in System.IO.File.ReadAllLines(TransferPath))
        {
            var p = ln.Split(',');
            if (p.Length >= 3 && float.TryParse(p[0], out float b) && float.TryParse(p[1], out float h) && float.TryParse(p[2], out float w) && w > 0)
                pts.Add(new float[] { b / w, h / w, w, b, h });
        }
        if (pts.Count == 0) { Debug.LogError("[Transfer] 유효 포인트 없음(벽 정보 필요 — 옛 포맷이면 Clear 후 재기록)"); return; }

        // 벽당 선형 회귀: 너/벽 ≈ a·(봇/벽) + c  (길이 정규화)
        float a, c, r2 = 1f;
        if (pts.Count == 1) { a = pts[0][0] > 0.001f ? pts[0][1] / pts[0][0] : 1f; c = 0f; }
        else
        {
            int n = pts.Count; float sx = 0, sy = 0, sxy = 0, sxx = 0;
            foreach (var p in pts) { sx += p[0]; sy += p[1]; sxy += p[0] * p[1]; sxx += p[0] * p[0]; }
            float denom = n * sxx - sx * sx;
            a = System.Math.Abs(denom) < 1e-6f ? 0f : (n * sxy - sx * sy) / denom;
            c = (sy - a * sx) / n;
            float meanY = sy / n, ssTot = 0, ssRes = 0;
            foreach (var p in pts) { float pred = a * p[0] + c; ssRes += (p[1] - pred) * (p[1] - pred); ssTot += (p[1] - meanY) * (p[1] - meanY); }
            r2 = ssTot > 1e-6f ? 1f - ssRes / ssTot : 1f;
        }
        System.IO.File.WriteAllText(TransferFitPath, $"{a:F5},{c:F5}");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("======== 변환곡선 피팅 (벽당 정규화) ========");
        foreach (var p in pts) { float pred = (a * p[0] + c) * p[2]; sb.AppendLine($"  봇 {p[3]:F2} ↔ 너 {p[4]:F2} (벽{p[2]:F0}) | 재현 {pred:F2} (오차 {pred - p[4]:+0.00;-0.00})"); }
        sb.AppendLine($"변환식: 너/벽 ≈ {a:F3}·(봇/벽) + {c:F3}" + (pts.Count >= 2 ? $"  (R²={r2:F3})" : "  (1점=비례, 2번째 맵 필요)"));
        if (Prepare()) { float bot = ProxyScore(); int w = WallCount(); sb.AppendLine($"현재 맵: 봇 {bot:F2}(벽{w}) → 예상 너 {(a * (bot / w) + c) * w:F2}회"); }
        sb.AppendLine("==========================================");
        Debug.Log(sb.ToString());
    }

    [ContextMenu("Clear Transfer Points")]
    public void ClearTransfer()
    {
        if (System.IO.File.Exists(TransferPath)) System.IO.File.Delete(TransferPath);
        if (System.IO.File.Exists(TransferFitPath)) System.IO.File.Delete(TransferFitPath);
        Debug.Log("[Transfer] 포인트·피팅 삭제됨");
    }

    void AppendPrediction(System.Text.StringBuilder sb)
    {
        if (!System.IO.File.Exists(TransferFitPath)) return;
        var p = System.IO.File.ReadAllText(TransferFitPath).Split(',');
        if (p.Length < 2 || !float.TryParse(p[0], out float a) || !float.TryParse(p[1], out float c)) return;
        float bot = ProxyScore(); int w = WallCount();
        sb.AppendLine($"── 예측(변환곡선) ── 프록시봇 {bot:F2}(벽{w}) → 예상 너 {(a * (bot / w) + c) * w:F2}회");
    }
}
