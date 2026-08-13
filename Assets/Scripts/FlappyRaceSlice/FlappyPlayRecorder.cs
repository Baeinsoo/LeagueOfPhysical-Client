using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캘리브레이션 계측 — 두 가지를 기록한다.
///  (1) 랩당 충돌 수·위치(시뮬과 동일 기준, 유령 진입 상승엣지=충돌 1건, 도넛 제외) → human_runs.csv
///  (2) 행동복제(BC)용 매 프레임 특징 (밴드목표까지 높이차, vy, 게이트까지 시간 → 플랩했나) → human_features.csv
/// 결승 도달 시 자동 리셋→다음 랩. 오토파일럿 끈 채 플레이할 것.
/// </summary>
[RequireComponent(typeof(FlappyPlayer))]
public class FlappyPlayRecorder : MonoBehaviour
{
    public float restartGrace = 0.7f;
    public bool logToFile = true;   // 캘리브레이션 파일 기록(분기 데모 등에선 off)

    float birdR, startX, endX, grace, lapTime;
    int clips;
    int[] heat;
    int nBucket;
    Vector3 startPos;
    bool prevGhost;
    float prevVy;
    readonly List<int> history = new List<int>();
    readonly List<string> featureBuffer = new List<string>();
    System.Reflection.FieldInfo vyField;
    FlappyPlayer player;
    FlappyCourseScan.Scan scan;

    void OnEnable()
    {
        player = GetComponent<FlappyPlayer>();
        vyField = typeof(FlappyPlayer).GetField("vy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var sc = GetComponent<SphereCollider>() ?? GetComponentInChildren<SphereCollider>();
        birdR = sc != null ? sc.radius : 0.448f;
        startPos = transform.position;
        startX = startPos.x;

        scan = FlappyCourseScan.Build(player, startX);   // 판정기와 동일 특징을 위해 공유 스캐너 사용
        endX = scan.endX;

        nBucket = (int)((endX - startX) / 10f) + 2;
        heat = new int[nBucket];
        clips = 0; grace = 0f; prevGhost = false; prevVy = 0f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        bool nowGhost = player != null && player.Ghost;
        var pos = transform.position;
        float vyNow = vyField != null ? (float)vyField.GetValue(player) : 0f;

        if (grace > 0f) { grace -= dt; prevGhost = nowGhost; prevVy = vyNow; return; }
        lapTime += dt;

        // (1) 충돌 = 유령 진입 상승엣지, 딱 1회. 도넛만 겹치면 제외.
        if (nowGhost && !prevGhost)
        {
            bool nonDonut = false;
            foreach (var col in Physics.OverlapSphere(pos, birdR + 0.15f))
            {
                if (!col.enabled || col.GetComponentInParent<FlappyObstacle>() == null) continue;
                if (FlappyCourseScan.IsDonut(col.transform)) continue;
                nonDonut = true; break;
            }
            if (nonDonut) { clips++; int bk = (int)((pos.x - startX) / 10f); if (bk >= 0 && bk < nBucket) heat[bk]++; }
        }

        // (2) BC 특징 — 유령/유예 아닐 때만(사람이 실제 조종 중인 프레임). 플랩=이번 프레임 vy 급상승.
        if (!nowGhost)
        {
            bool flapped = (vyNow - prevVy) > 8f;
            scan.Target(pos.x, pos.y, out float lo, out float hi, out float pLo, out float tGate);
            // 특징엔 '결정 시점' 속도(prevVy)를 사용
            featureBuffer.Add($"{pos.y - pLo:F3},{prevVy:F3},{tGate:F3},{(flapped ? 1 : 0)}");
        }

        prevGhost = nowGhost; prevVy = vyNow;
        if (pos.x >= endX) { LogRun(); RestartLap(); }
    }

    void RestartLap()
    {
        transform.position = startPos;
        if (vyField != null) vyField.SetValue(player, 0f);
        clips = 0; lapTime = 0f;
        for (int i = 0; i < nBucket; i++) heat[i] = 0;
        grace = restartGrace; prevGhost = false; prevVy = 0f;
    }

    void LogRun()
    {
        history.Add(clips);
        var top = new List<int>();
        for (int b = 0; b < nBucket; b++) top.Add(b);
        top.Sort((a, b) => heat[b].CompareTo(heat[a]));
        var hs = new System.Text.StringBuilder();
        for (int k = 0; k < 6 && k < top.Count; k++) { int b = top[k]; if (heat[b] == 0) continue; hs.Append($"x{startX + b * 10:F0}({heat[b]}) "); }
        float avg = 0f; foreach (var v in history) avg += v; avg /= history.Count;
        Debug.Log($"[PlayRun] 랩{history.Count} | ⏱{lapTime:F2}s | 충돌 {clips}회 | 누적평균 {avg:F1}회({history.Count}판) | 특징 {featureBuffer.Count}행 | 핫스팟 {hs}");

        if (!logToFile) return;
        try
        {
            var dir = System.IO.Path.GetDirectoryName(FlappySimJudge.HumanDataPath);
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            var row = new System.Text.StringBuilder(); row.Append(clips);
            for (int b = 0; b < nBucket; b++) { row.Append(','); row.Append(heat[b]); }
            System.IO.File.AppendAllText(FlappySimJudge.HumanDataPath, row.ToString() + "\n");

            if (featureBuffer.Count > 0)
            {
                System.IO.File.AppendAllText(FlappyBC.FeaturesPath, string.Join("\n", featureBuffer) + "\n");
                featureBuffer.Clear();
            }
        }
        catch (System.Exception e) { Debug.LogWarning("[PlayRun] 저장 실패: " + e.Message); }
    }

    public bool showGui = false;   // 정식 HUD 사용 시 캘리브레이션 텍스트 숨김
    void OnGUI()
    {
        if (!showGui) return;
        var s = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        string hist = history.Count > 0 ? "  (지난랩: " + string.Join(",", history) + ")" : "";
        GUI.Label(new Rect(14, 88, 800, 26), $"🎮 ⏱{lapTime:F2}s | 충돌 {clips}회{hist}", s);
        GUI.Label(new Rect(14, 112, 800, 22), "완주하면 자동 리셋+다음 랩. 오토파일럿 꺼야 함", new GUIStyle(GUI.skin.label) { fontSize = 12 });
    }
}
