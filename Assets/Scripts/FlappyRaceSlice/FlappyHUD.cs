using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레이스 HUD(런타임 생성 uGUI) — 대시 게이지 바 + 순위 리스트 + 트랙 진행바(내 캐릭터 위치 강조).
/// 씬 직렬화 없이 Start()에서 캔버스를 스스로 구성하고 Update()에서 값만 갱신. 프로토타입용 독립 HUD.
/// </summary>
public class FlappyHUD : MonoBehaviour
{
    public float startX = -3f;
    public float finishX = 632f;
    public float trackWidth = 1040f;

    Font font;
    FlappyPlayer player;
    Image dashBg, dashFill; Text dashLabel;
    Text rankText, timerText;
    RectTransform trackRect;

    class Racer { public Transform t; public bool isPlayer; public Color col; public string label; public RectTransform marker; public Text tag; }
    readonly List<Racer> racers = new List<Racer>();
    float raceTime;

    static readonly Color PLAYER_COL = new Color(0.30f, 1f, 0.45f);

    void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        BuildCanvas();
        CollectRacers();
    }

    // ---------- 생성 ----------
    Transform root;
    void BuildCanvas()
    {
        var cgo = new GameObject("FlappyHUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
        root = cgo.transform;

        // ── 트랙 진행바 (상단 중앙) ──
        var trackBg = MakeImage(root, "Track", new Color(0f, 0f, 0f, 0.42f));
        SetRect(trackBg, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -34), new Vector2(trackWidth + 12, 20));
        trackRect = trackBg.rectTransform;
        // 시작/결승 캡
        var capS = MakeImage(trackRect, "capStart", new Color(1f, 1f, 1f, 0.55f)); SetRect(capS, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(6, 0), new Vector2(4, 20));
        var capF = MakeImage(trackRect, "capFinish", new Color(1f, 0.85f, 0.15f, 0.9f)); SetRect(capF, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(6 + trackWidth, 0), new Vector2(5, 20));
        var fLbl = MakeText(trackRect, "finishLbl", 20, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.15f)); SetRect(fLbl, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12 + trackWidth, 22), new Vector2(80, 24)); fLbl.text = "FINISH";

        // ── 순위 리스트 (우측 상단) ──
        rankText = MakeText(root, "Rank", 30, TextAnchor.UpperRight, Color.white);
        SetRect(rankText, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -74), new Vector2(360, 260));
        rankText.lineSpacing = 1.15f;

        // ── 타이머 (좌측 상단) ──
        timerText = MakeText(root, "Timer", 34, TextAnchor.UpperLeft, Color.white);
        SetRect(timerText, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -74), new Vector2(360, 44));

        // ── 대시 게이지 (좌측 하단) ──
        dashBg = MakeImage(root, "DashBg", new Color(0f, 0f, 0f, 0.55f));
        SetRect(dashBg, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 40), new Vector2(400, 54));
        dashFill = MakeImage(dashBg.rectTransform, "DashFill", new Color(0.30f, 0.75f, 1f, 1f));
        SetRect(dashFill, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(5, 0), new Vector2(0, 44));
        dashLabel = MakeText(dashBg.rectTransform, "DashLbl", 26, TextAnchor.MiddleCenter, Color.white);
        SetRect(dashLabel, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
    }

    void CollectRacers()
    {
        racers.Clear();
        foreach (var b in FindObjectsOfType<FlappyBird>())
        {
            var r = new Racer(); r.t = b.transform;
            r.isPlayer = b.GetComponent<FlappyPlayer>() != null;
            if (r.isPlayer) player = b.GetComponent<FlappyPlayer>();
            var sr = b.GetComponentInChildren<SpriteRenderer>();
            r.col = r.isPlayer ? PLAYER_COL : (sr != null ? sr.color : Color.gray);
            r.label = r.isPlayer ? "YOU" : b.name.Replace("Pacer_", "");
            // 트랙 마커
            r.marker = MakeImage(trackRect, "mk_" + r.label, r.col).rectTransform;
            SetRect(r.marker, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(r.isPlayer ? 16 : 12, r.isPlayer ? 30 : 18));
            if (r.isPlayer)
            {
                r.tag = MakeText(trackRect, "youTag", 20, TextAnchor.MiddleCenter, PLAYER_COL);
                SetRect(r.tag, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 22));
                r.tag.text = "▼YOU"; r.tag.fontStyle = FontStyle.Bold;
            }
            racers.Add(r);
        }
    }

    // ---------- 갱신 ----------
    void Update()
    {
        if (racers.Count == 0) return;
        raceTime += Time.deltaTime;
        float span = Mathf.Max(1f, finishX - startX);

        // 트랙 마커 위치
        foreach (var r in racers)
        {
            if (r.t == null) continue;
            float p = Mathf.Clamp01((r.t.position.x - startX) / span);
            float x = 6f + p * trackWidth;
            r.marker.anchoredPosition = new Vector2(x, r.isPlayer ? 4f : 0f);
            if (r.tag != null) r.tag.rectTransform.anchoredPosition = new Vector2(x, 30f);
        }

        // 순위(X 내림차순)
        racers.Sort((a, b) => (b.t != null ? b.t.position.x : -9999f).CompareTo(a.t != null ? a.t.position.x : -9999f));
        var sb = new System.Text.StringBuilder();
        sb.Append("<b>RANK</b>\n");
        string[] ord = { "1st", "2nd", "3rd", "4th", "5th", "6th", "7th", "8th" };
        for (int i = 0; i < racers.Count; i++)
        {
            var r = racers[i];
            string hex = ColorUtility.ToHtmlStringRGB(r.col);
            string place = i < ord.Length ? ord[i] : (i + 1) + "th";
            string line = place + "  <color=#" + hex + ">" + r.label + "</color>";
            sb.Append(r.isPlayer ? "<b>" + line + "</b>\n" : line + "\n");
        }
        rankText.text = sb.ToString();

        // 타이머 + 내 순위
        int myPlace = 1; for (int i = 0; i < racers.Count; i++) if (racers[i].isPlayer) { myPlace = i + 1; break; }
        timerText.text = "⏱ " + raceTime.ToString("0.0") + "s\n<size=26><color=#4CFF72>" + (myPlace) + "위 / " + racers.Count + "</color></size>";

        // 대시 게이지
        if (player != null)
        {
            float c = player.DashCharge;
            float w = 5f + c * 390f;
            dashFill.rectTransform.sizeDelta = new Vector2(w - 10f, 44f);
            bool ready = c >= 1f;
            dashFill.color = ready ? new Color(1f, 0.8f, 0.2f, 1f) : new Color(0.30f, 0.75f, 1f, 1f);
            dashLabel.text = ready ? "⚡ DASH READY (Shift/D)" : "DASH  " + Mathf.RoundToInt(c * 100) + "%";
        }
    }

    // ---------- UI 헬퍼 ----------
    Image MakeImage(Transform parent, string name, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); img.color = col; img.raycastTarget = false;
        return img;
    }
    Text MakeText(Transform parent, string name, int size, TextAnchor anchor, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>(); t.font = font; t.fontSize = size; t.alignment = anchor; t.color = col;
        t.raycastTarget = false; t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
    void SetRect(Graphic g, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size) { Apply(g.rectTransform, aMin, aMax, pivot, pos, size); }
    void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size) { Apply(rt, aMin, aMax, pivot, pos, size); }
    void Apply(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    { rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size; }
}
