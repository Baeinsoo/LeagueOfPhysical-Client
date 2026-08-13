using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 봇 레이스 MVP — 플레이어(수동) + AI 새(FlappyAutoPilot 사람모드) 여러 마리가 같은 코스를 동시 레이스.
/// netcode 없이 싱글+봇. 결승선 통과 순서로 순위. AI마다 실력(조준오차·반응) 랜덤 차등.
/// </summary>
public class FlappyRaceManager : MonoBehaviour
{
    public GameObject playerBird;   // 원본(플레이어 새). 이걸 복제해 AI 생성.
    public Transform courseRoot;
    public int aiCount = 3;
    public Color[] aiColors = new Color[] {
        new Color(1f,0.4f,0.4f), new Color(0.4f,0.6f,1f), new Color(0.7f,0.4f,1f), new Color(1f,0.7f,0.3f)
    };

    class Racer { public Transform t; public string name; public bool finished; public float time; }
    readonly List<Racer> racers = new List<Racer>();
    float raceTime, finishX;
    bool allDone;

    void Start()
    {
        // 결승선 = 마지막 게이트 X + 4
        finishX = 50f;
        foreach (Transform c in courseRoot)
            if (c.name != "Ground" && c.name != "Ceiling") finishX = Mathf.Max(finishX, c.position.x);
        finishX += 4f;

        // 플레이어 = 수동 (오토파일럿 끄고 FlappyPlayer 켜기)
        var pilot = playerBird.GetComponent<FlappyAutoPilot>();
        if (pilot != null) pilot.enabled = false;
        var fp = playerBird.GetComponent<FlappyPlayer>();
        if (fp != null) fp.enabled = true;
        Vector3 spawn = playerBird.transform.position;
        racers.Add(new Racer { t = playerBird.transform, name = "YOU" });

        // AI 스폰 (플레이어 새 복제 — 이때 클론의 오토파일럿은 template 따라 disabled 상태)
        for (int i = 0; i < aiCount; i++)
        {
            var go = Instantiate(playerBird);
            go.name = "AI_" + (i + 1);
            var mgr = go.GetComponent<FlappyRaceManager>(); if (mgr != null) Destroy(mgr); // 복제된 매니저 제거
            var bot = go.GetComponent<FlappyAutoPilot>();
            bot.courseRoot = courseRoot;
            bot.humanMode = true;
            bot.showHud = false;   // AI는 개별 HUD 끔(순위 HUD가 대신). FlappyPlayer는 오토파일럿이 비활성화
            bot.reactionDelay = Random.Range(0.10f, 0.20f);
            bot.aimError = Random.Range(0.5f, 1.1f);
            float sy = spawn.y + (i - aiCount * 0.5f) * 0.4f;   // 살짝 어긋난 시작 높이(구분)
            bot.startX = spawn.x; bot.startY = sy;
            go.transform.position = new Vector3(spawn.x, sy, 0f);
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && i < aiColors.Length) sr.color = aiColors[i];
            bot.enabled = true;   // 여기서 OnEnable → 올바른 startY로 Restart
            racers.Add(new Racer { t = go.transform, name = go.name });
        }
    }

    void Update()
    {
        if (!allDone) raceTime += Time.deltaTime;
        allDone = true;
        foreach (var r in racers)
        {
            if (!r.finished && r.t.position.x >= finishX) { r.finished = true; r.time = raceTime; }
            if (!r.finished) allDone = false;
        }
    }

    void OnGUI()
    {
        // 진행 순위(x 내림차순)
        racers.Sort((a, b) => b.t.position.x.CompareTo(a.t.position.x));
        var s = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(Screen.width - 230, 10, 220, 24), "🏁 레이스 순위", s);
        int rank = 1;
        foreach (var r in racers)
        {
            float prog = Mathf.Clamp01(r.t.position.x / finishX) * 100f;
            string line = rank + ". " + r.name + (r.finished ? "  ✔ " + r.time.ToString("F1") + "s" : "  " + prog.ToString("F0") + "%");
            var st = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = r.name == "YOU" ? FontStyle.Bold : FontStyle.Normal };
            GUI.Label(new Rect(Screen.width - 230, 10 + rank * 22, 220, 22), line, st);
            rank++;
        }
        if (allDone) GUI.Label(new Rect(Screen.width / 2 - 60, 40, 200, 30), "🏁 레이스 종료!", new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold });
    }
}
