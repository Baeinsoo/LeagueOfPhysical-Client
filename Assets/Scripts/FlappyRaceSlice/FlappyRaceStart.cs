using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 레이스 시작 시퀀스 — 카트라이더식 카운트다운(3·2·1·GO) + GO 타이밍 스타트 부스트.
/// 카운트다운 동안 모든 새를 정지(static RaceFrozen). GO 순간 창(goWindow) 안에 입력하면 부스트(대시=전진버스트+고도유지).
/// 플레이어=Space/클릭(GO 전 누르면 실격), 봇=skill 기반 반응 타이밍. 출발선은 같은 X(공평)+작은 대칭 Y레인(겹침 방지).
/// </summary>
public class FlappyRaceStart : MonoBehaviour
{
    public static bool RaceFrozen = false;

    public float countStep = 0.8f;     // 3→2→1 간격
    public float goWindow = 0.45f;     // GO 후 부스트 허용 창
    public float boostDur = 0.5f;      // 부스트 지속

    enum Phase { Count, Window, Run }
    Phase phase = Phase.Count;
    float t, promptClear;

    class R { public FlappyPlayer fp; public FlappyPacer pc; public bool isPlayer; public float botPress; public bool judged; }
    readonly List<R> rs = new List<R>();

    Font font; Text bigText, promptText;

    void Awake() { RaceFrozen = true; phase = Phase.Count; t = 0f; }

    void Start()
    {
        foreach (var p in FindObjectsOfType<FlappyPlayer>()) rs.Add(new R { fp = p, isPlayer = true });
        foreach (var pc in FindObjectsOfType<FlappyPacer>()) rs.Add(new R { pc = pc, isPlayer = false });

        float GO = 3f * countStep;
        foreach (var r in rs)
        {
            if (r.isPlayer) continue;
            float sk = r.pc.skill;
            float mean = Mathf.Lerp(0.30f, 0.10f, Mathf.Clamp01((sk - 0.82f) / 0.23f));   // 잘하는 봇일수록 GO에 가깝게
            float jit  = Mathf.Lerp(0.14f, 0.05f, Mathf.Clamp01((sk - 0.82f) / 0.23f));
            r.botPress = GO + Mathf.Max(0f, mean + Random.Range(-jit, jit));
        }
        BuildUI();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        t += dt;
        float GO = 3f * countStep;

        if (promptClear > 0f) { promptClear -= dt; if (promptClear <= 0f && promptText != null) promptText.text = ""; }

        if (phase == Phase.Count)
        {
            SetBig(t < countStep ? "3" : t < 2f * countStep ? "2" : t < GO ? "1" : "GO!");
            if (PlayerPressed())   // GO 전 = 부정출발
            {
                var pl = rs.Find(x => x.isPlayer);
                if (pl != null && !pl.judged) { pl.judged = true; Flash("너무 빨라! (부스트 실패)", new Color(1f, 0.5f, 0.4f)); }
            }
            if (t >= GO)
            {
                phase = Phase.Window; RaceFrozen = false;
                SetBig("GO!"); Show("지금! Space로 부스트", new Color(1f, 0.9f, 0.3f));
            }
        }
        else if (phase == Phase.Window)
        {
            if (t >= GO + 0.45f) SetBig("");
            if (PlayerPressed())
            {
                var pl = rs.Find(x => x.isPlayer);
                if (pl != null && !pl.judged) { pl.judged = true; Boost(pl); Flash("PERFECT! 부스트!", new Color(0.3f, 1f, 0.5f)); }
            }
            foreach (var r in rs)
                if (!r.isPlayer && !r.judged && t >= r.botPress) { r.judged = true; if (r.botPress <= GO + goWindow) Boost(r); }

            if (t >= GO + goWindow) { phase = Phase.Run; SetBig(""); if (promptClear <= 0f && promptText != null) promptText.text = ""; }
        }
    }

    void Boost(R r)
    {
        if (r.fp != null) r.fp.StartBoost(boostDur);
        if (r.pc != null) r.pc.StartBoost(boostDur);
    }

    bool PlayerPressed()
    {
        var kb = Keyboard.current;
        bool k = kb != null && (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame);
        bool m = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        return k || m;
    }

    // ---------- UI ----------
    void BuildUI()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        var cgo = new GameObject("FlappyRaceStart_Canvas", typeof(Canvas), typeof(CanvasScaler));
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 110;
        var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;

        bigText = MakeText(cgo.transform, "count", 150, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(600, 220));
        promptText = MakeText(cgo.transform, "prompt", 44, new Vector2(0.5f, 0.5f), new Vector2(0, -110), new Vector2(900, 70));
    }

    Text MakeText(Transform parent, string name, int size, Vector2 anchor, Vector2 pos, Vector2 sizeD)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>(); t.font = font; t.fontSize = size; t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        var ol = go.GetComponent<Outline>(); ol.effectColor = new Color(0f, 0f, 0f, 0.6f); ol.effectDistance = new Vector2(3, -3);
        var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = sizeD;
        return t;
    }

    void SetBig(string s) { if (bigText != null && bigText.text != s) bigText.text = s; }
    void Show(string s, Color c) { if (promptText != null) { promptText.text = s; promptText.color = c; } promptClear = 0f; }
    void Flash(string s, Color c) { if (promptText != null) { promptText.text = s; promptText.color = c; } promptClear = 1.6f; }
}
