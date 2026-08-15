using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 코스 스캔 단일 진실원본 — 계측(FlappyPlayRecorder)과 판정(FlappySimJudge)이 *완전히 같은* 개구·목표·특징을 쓰게 한다.
/// 행동복제(BC)에서 로깅 시점과 봇 조종 시점의 특징이 어긋나면 학습이 무의미하므로, 이 계산을 한 곳에 모은다.
/// </summary>
public static class FlappyCourseScan
{
    public class Scan
    {
        public float startX, endX, cdx = 0.2f;
        public int nCol;
        public List<float[]>[] colOpen;
        public bool[] colHasOb, colDonut;
        public float birdR, arc, fwd, flap, grav, maxFall;
        public bool eOn, eSharp;
        public float eAmp, eWl, eSx, eDrop, eCeil, fixFloor, fixCeil;

        public float FloorAt(float x) => eOn ? FlappyElevation.Value(x, eAmp, eSx, eWl, eSharp) - eDrop : fixFloor;
        public float CeilAt(float x) => eOn ? FlappyElevation.Value(x, eAmp, eSx, eWl, eSharp) + eCeil : fixCeil;

        /// <summary>바로 앞(≈0.14s) 틈을 겨냥: py에 가장 가까운 개구 [lo,hi], band 오실레이션 목표 pLo, 다음 게이트까지 시간.</summary>
        public void Target(float px, float py, out float lo, out float hi, out float pLo, out float tGate)
        {
            float aheadX = px + 1.5f;
            int ci = (int)((aheadX - startX) / cdx); if (ci < 0) ci = 0; if (ci >= nCol) ci = nCol - 1;
            lo = FloorAt(aheadX); hi = CeilAt(aheadX); float bd = 1e9f;
            foreach (var g in colOpen[ci]) { float ctr = (g[0] + g[1]) * 0.5f; if (System.Math.Abs(ctr - py) < bd) { bd = System.Math.Abs(ctr - py); lo = g[0]; hi = g[1]; } }
            float span = hi - lo;
            pLo = (span >= arc) ? lo + (span - arc) * 0.5f : lo;
            float gx = aheadX;
            for (float sx = px + 0.3f; sx <= px + fwd * 1.2f; sx += cdx)
            { int k = (int)((sx - startX) / cdx); if (k < 0) k = 0; if (k >= nCol) break; if (colHasOb[k]) { gx = startX + k * cdx; break; } }
            tGate = Mathf.Max(0f, (gx - px) / fwd);
        }

        public int ColAt(float x) { int i = (int)((x - startX) / cdx); if (i < 0) i = 0; if (i >= nCol) i = nCol - 1; return i; }
    }

    public static Scan Build(FlappyPlayer player, float startX)
    {
        Physics.SyncTransforms();
        var s = new Scan();
        s.startX = startX;
        s.fwd = player.forwardSpeed; s.flap = player.flapImpulse; s.grav = player.gravity; s.maxFall = player.maxFall;
        s.arc = s.flap * s.flap / (2f * s.grav);
        s.eOn = player.elevationFloor; s.eAmp = player.elevAmp; s.eWl = player.elevWavelength; s.eSx = player.elevStartX;
        s.eDrop = player.floorDrop; s.eCeil = player.ceilRise; s.eSharp = player.elevSharp;
        s.fixFloor = player.groundY; s.fixCeil = player.ceilingY;
        var sc = player.GetComponent<SphereCollider>() ?? player.GetComponentInChildren<SphereCollider>();
        s.birdR = sc != null ? sc.radius : 0.448f;

        var obs = new List<Collider>();
        var donutRanges = new List<float[]>();
        foreach (var c in Object.FindObjectsOfType<Collider>())
        {
            if (!c.enabled || c.GetComponentInParent<FlappyObstacle>() == null) continue;
            obs.Add(c);
            if (IsDonut(c.transform)) { var b = c.bounds; donutRanges.Add(new float[] { b.min.x, b.max.x }); }
        }
        float maxX = startX + 10f;
        foreach (var c in obs) { var b = c.bounds; if (b.max.x > maxX) maxX = b.max.x; }
        s.endX = maxX + 4f;

        s.nCol = (int)((s.endX - startX) / s.cdx) + 1;
        s.colOpen = new List<float[]>[s.nCol];
        s.colHasOb = new bool[s.nCol];
        s.colDonut = new bool[s.nCol];
        for (int ci = 0; ci < s.nCol; ci++)
        {
            float cx = startX + ci * s.cdx;
            float fy = s.FloorAt(cx), cy = s.CeilAt(cx);
            var nb = new List<Bounds>();
            foreach (var c in obs) { var b = c.bounds; if (cx >= b.min.x - s.birdR && cx <= b.max.x + s.birdR) nb.Add(b); }
            s.colHasOb[ci] = nb.Count > 0;
            foreach (var dr in donutRanges) if (cx >= dr[0] - s.birdR && cx <= dr[1] + s.birdR) { s.colDonut[ci] = true; break; }
            var o = new List<float[]>();
            float rs = float.NaN, prev = fy;
            for (float y = fy; y <= cy; y += 0.05f)
            {
                bool blk = false; foreach (var b in nb) { if (y >= b.min.y - s.birdR && y <= b.max.y + s.birdR) { blk = true; break; } }
                if (!blk) { if (float.IsNaN(rs)) rs = y; }
                else { if (!float.IsNaN(rs)) { if (prev - rs >= s.birdR * 2f) o.Add(new float[] { rs, prev }); rs = float.NaN; } }
                prev = y;
            }
            if (!float.IsNaN(rs) && cy - rs >= s.birdR * 2f) o.Add(new float[] { rs, cy });
            s.colOpen[ci] = o;
        }
        return s;
    }

    public static bool IsDonut(Transform t)
    {
        for (var p = t; p != null; p = p.parent) if (p.name == "Donut") return true;
        return false;
    }
}

/// <summary>
/// 행동복제(BC) 정책 — 사람의 (상태→플랩) 결정을 로지스틱 회귀로 학습해 봇이 사람처럼 조종하게 한다.
/// 특징 = [bias, 밴드목표까지 높이차, vy, 게이트까지 시간] (정규화). P(flap)=sigmoid(w·f).
/// </summary>
public static class FlappyBC
{
    public const int N = 4;

    public static void Features(float dHeight, float vy, float tGate, float[] f)
    {
        f[0] = 1f;
        f[1] = dHeight / 3f;   // py-pLo: 목표보다 얼마나 위/아래(양=위)
        f[2] = vy / 20f;       // 수직속도
        f[3] = tGate;          // 다음 게이트까지 초
    }

    public static float PFlap(float[] w, float[] f)
    {
        float z = 0f; for (int i = 0; i < N; i++) z += w[i] * f[i];
        return 1f / (1f + Mathf.Exp(-z));
    }

    public static string WeightsPath =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "FlappyCalibration", "bc_weights.csv"));

    public static string FeaturesPath =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "FlappyCalibration", "human_features.csv"));

    public static float[] LoadWeights()
    {
        if (!System.IO.File.Exists(WeightsPath)) return null;
        var parts = System.IO.File.ReadAllText(WeightsPath).Trim().Split(',');
        if (parts.Length < N) return null;
        var w = new float[N];
        for (int i = 0; i < N; i++) if (!float.TryParse(parts[i], out w[i])) return null;
        return w;
    }
}
