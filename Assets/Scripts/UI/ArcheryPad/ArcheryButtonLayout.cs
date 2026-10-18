using System.Globalization;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 활 버튼·시위 풀기 버튼이 <b>어디에 얼마나 크게</b> 있나. 손가락 판정(<see cref="ArcheryPointerRouter"/>로
    /// 넘기는 <see cref="Hit"/>)과 화면 그림(<see cref="ArcheryPadView"/>)이 모두 이 한 곳에서 자리를 읽는다 —
    /// 둘이 따로 재면 보이는 자리와 눌리는 자리가 갈라진다.
    ///
    /// <para>버튼마다 오른쪽 위 가장자리에 작은 <b>손잡이</b>가 붙어 있고, 손잡이를 끌면 버튼이 옮겨진다.
    /// 자리는 기기마다 PlayerPrefs에 <b>안전 영역(노치·홈 막대를 뺀 곳) 기준 0~1 비율</b>로 남는다 —
    /// 픽셀로 남기면 해상도가 바뀌거나 폰을 반대로 쥐었을 때(노치가 반대편) 엉뚱한 곳에 선다.</para>
    ///
    /// <para>좌표는 모두 패널 좌표다(왼쪽 위가 0, 아래로 갈수록 y가 커진다). 크기는 화면 <b>세로</b>에 비례한다 —
    /// 가로 폰에서 엄지가 덮는 넓이는 세로 길이가 정한다.</para>
    /// </summary>
    public sealed class ArcheryButtonLayout
    {
        public enum Which { Bow, Cancel }

        /// <summary>손가락이 닿은 자리. 손잡이가 버튼 몸통·화면보다 먼저다.</summary>
        public enum Hit { None, Bow, Cancel, BowHandle, CancelHandle }

        /// <summary>활 버튼 지름 = 화면 세로의 24%. 폰 가로에서 엄지 하나가 넉넉히 덮는다.</summary>
        public const float BowDiameterOfHeight = 0.24f;

        /// <summary>시위 풀기 버튼 지름 = 세로의 18%. 활 버튼보다 작다 — 덜 쓰는 버튼이다.</summary>
        public const float CancelDiameterOfHeight = 0.18f;

        /// <summary>누르는 자리는 보이는 원보다 조금 넓다 — 가장자리를 살짝 빗겨 눌러도 먹는다.</summary>
        public const float HitSlack = 1.15f;

        /// <summary>손잡이 지름 = 세로의 5.5%. 작아서 누르는 자리는 보이는 원보다 더 넉넉히 잡는다.</summary>
        public const float HandleDiameterOfHeight = 0.055f;
        public const float HandleHitSlack = 1.3f;

        //  손잡이 한가운데가 버튼 테두리에서 바깥으로 나간 정도(손잡이 반지름 대비). 테두리에 걸쳐 앉는다.
        private const float HandleOutset = 0.35f;

        //  안전 영역 가장자리, 그리고 두 버튼 사이에 남길 틈(패널 픽셀).
        private const float EdgeGap = 8f;
        private const float ButtonGap = 8f;

        //  기본 자리(안전 영역 기준). 활은 오른손 엄지가 쉬는 곳 — 10-18 폰 판 "버튼을 쥔 채 끌 자리가
        //  없다"로 구석(86%·74%)에서 왼쪽 위로 당겼고, 같은 날 "너무 안쪽"으로 가로만 78% → 80%.
        //  시위 풀기는 왼손 엄지가 닿는 왼쪽 아래.
        public static readonly Vector2 DefaultBow = new Vector2(0.80f, 0.62f);
        public static readonly Vector2 DefaultCancel = new Vector2(0.12f, 0.74f);

        private Vector2 panel;
        private Rect safe;

        //  사람이 고른 자리(안전 영역 비율). 화면이 바뀌면 여기서 다시 계산해 가둔다 —
        //  가두며 깎인 값을 여기에 덮어쓰면 한 번 좁은 화면을 거친 것만으로 고른 자리를 잃는다.
        private Vector2 bowWish = DefaultBow;
        private Vector2 cancelWish = DefaultCancel;

        private Vector2 bowCenter;
        private Vector2 cancelCenter;

        public ArcheryButtonLayout(Vector2 panelSize, Rect safeArea)
        {
            SetGeometry(panelSize, safeArea);
        }

        public Vector2 PanelSize => panel;
        public Rect SafeArea => safe;

        /// <summary>화면 크기나 안전 영역이 바뀌었다(회전·해상도). 고른 자리를 새 화면에 다시 놓는다.</summary>
        public void SetGeometry(Vector2 panelSize, Rect safeArea)
        {
            panel = panelSize;
            safe = safeArea.width > 0f && safeArea.height > 0f ? safeArea : new Rect(Vector2.zero, panelSize);
            Settle();
        }

        //  ── 크기 ───────────────────────────────────────────────────

        public float FaceRadius(Which w) =>
            panel.y * (w == Which.Bow ? BowDiameterOfHeight : CancelDiameterOfHeight) * 0.5f;

        public float HitRadius(Which w) => FaceRadius(w) * HitSlack;

        public float HandleRadius => panel.y * HandleDiameterOfHeight * 0.5f;

        public float HandleHitRadius => HandleRadius * HandleHitSlack;

        //  버튼 한가운데에서 손잡이 한가운데까지. 오른쪽 위 45°(패널은 위가 −y).
        private Vector2 HandleOffset(Which w)
        {
            float d = (FaceRadius(w) + HandleRadius * HandleOutset) * 0.70710678f;
            return new Vector2(d, -d);
        }

        /// <summary>버튼과 손잡이를 다 덮는 원의 반지름. 두 버튼은 이 원끼리 안 겹친다.</summary>
        public float Footprint(Which w) => Mathf.Max(HitRadius(w), HandleOffset(w).magnitude + HandleHitRadius);

        //  ── 자리·판정 ───────────────────────────────────────────────

        public Vector2 Center(Which w) => w == Which.Bow ? bowCenter : cancelCenter;

        public Vector2 HandleCenter(Which w) => Center(w) + HandleOffset(w);

        public bool OnBody(Which w, Vector2 p) => (p - Center(w)).sqrMagnitude <= Sq(HitRadius(w));

        public bool OnHandle(Which w, Vector2 p) => (p - HandleCenter(w)).sqrMagnitude <= Sq(HandleHitRadius);

        /// <summary>
        /// 손가락이 닿은 자리. <b>손잡이가 먼저</b>다 — 손잡이는 버튼 테두리에 걸쳐 있어 몸통의 누르는 원과
        /// 겹치는데, 거기서 몸통이 이기면 손잡이를 잡으려던 손가락이 활을 든다. 시위 풀기 버튼이 숨어
        /// 있어도 그 손잡이와 몸통 자리는 그대로 답한다(숨은 버튼을 어떻게 받을지는 라우터가 정한다).
        /// </summary>
        public Hit HitTest(Vector2 p)
        {
            if (OnHandle(Which.Bow, p)) { return Hit.BowHandle; }
            if (OnHandle(Which.Cancel, p)) { return Hit.CancelHandle; }
            if (OnBody(Which.Bow, p)) { return Hit.Bow; }
            if (OnBody(Which.Cancel, p)) { return Hit.Cancel; }
            return Hit.None;
        }

        //  ── 옮기기 ─────────────────────────────────────────────────

        /// <summary>
        /// 버튼을 <paramref name="desired"/>로 옮긴다. 안전 영역 밖으로 나가면 안으로 가두고, 다른 버튼과
        /// 겹치면 <b>옮기는 쪽만</b> 밀어낸다(가만있던 버튼은 그대로). 밀어낼 곳이 없으면 그 자리에 선다.
        /// </summary>
        public void MoveTo(Which w, Vector2 desired)
        {
            Vector2 c = Resolve(w, desired, Center(w));
            SetCenter(w, c);
            SetWish(w, ToNormalized(c));
        }

        public void ResetToDefault(Which w) => MoveTo(w, FromNormalized(Default(w)));

        public static Vector2 Default(Which w) => w == Which.Bow ? DefaultBow : DefaultCancel;

        //  안전 영역 안으로 가둔다. 손잡이가 오른쪽 위로 튀어나와 있어 그쪽 두 변은 손잡이까지 잰다.
        private Vector2 ClampToSafe(Which w, Vector2 c)
        {
            float hit = HitRadius(w);
            Vector2 off = HandleOffset(w);
            float hh = HandleHitRadius;
            float right = Mathf.Max(hit, off.x + hh);
            float top = Mathf.Max(hit, -off.y + hh);

            return new Vector2(
                ClampSpan(c.x, safe.xMin + hit + EdgeGap, safe.xMax - right - EdgeGap),
                ClampSpan(c.y, safe.yMin + top + EdgeGap, safe.yMax - hit - EdgeGap));
        }

        //  화면이 버튼보다 좁으면 가운데에 둔다.
        private static float ClampSpan(float v, float min, float max) =>
            min <= max ? Mathf.Clamp(v, min, max) : (min + max) * 0.5f;

        private float MinApart => Footprint(Which.Bow) + Footprint(Which.Cancel) + ButtonGap;

        private bool Apart(Vector2 a, Vector2 b) => (a - b).magnitude >= MinApart - 1e-3f;

        //  가둔 뒤 다른 버튼과 겹치면 그 버튼에서 바깥쪽으로 밀고 다시 가둔다. 벽에 밀려 도로 겹칠 수
        //  있어 몇 번 되풀이하고, 그래도 겹치면 fallback(겹치지 않던 마지막 자리)에 선다.
        private Vector2 Resolve(Which w, Vector2 desired, Vector2 fallback)
        {
            Vector2 other = Center(Other(w));
            Vector2 c = ClampToSafe(w, desired);
            for (int i = 0; i < 6 && Apart(c, other) == false; i++)
            {
                Vector2 away = c - other;
                Vector2 dir = away.sqrMagnitude > 1e-6f ? away.normalized
                    : (w == Which.Bow ? Vector2.right : Vector2.left);
                c = ClampToSafe(w, other + dir * MinApart);
            }
            return Apart(c, other) ? c : fallback;
        }

        //  고른 자리를 지금 화면에 놓는다. 저장된 둘이 겹치면(다른 비율 화면에서 고른 자리 등)
        //  시위 풀기 쪽을 밀어 보고, 그래도 안 되면 둘 다 기본 자리로 간다.
        private void Settle()
        {
            bowCenter = ClampToSafe(Which.Bow, FromNormalized(bowWish));
            cancelCenter = ClampToSafe(Which.Cancel, FromNormalized(cancelWish));
            if (Apart(bowCenter, cancelCenter))
            {
                return;
            }
            Vector2 cancelDefault = ClampToSafe(Which.Cancel, FromNormalized(DefaultCancel));
            cancelCenter = Resolve(Which.Cancel, cancelCenter, cancelDefault);
            if (Apart(bowCenter, cancelCenter) == false)
            {
                bowCenter = ClampToSafe(Which.Bow, FromNormalized(DefaultBow));
                cancelCenter = cancelDefault;
            }
        }

        private static Which Other(Which w) => w == Which.Bow ? Which.Cancel : Which.Bow;

        private void SetCenter(Which w, Vector2 c)
        {
            if (w == Which.Bow) { bowCenter = c; } else { cancelCenter = c; }
        }

        private void SetWish(Which w, Vector2 n)
        {
            if (w == Which.Bow) { bowWish = n; } else { cancelWish = n; }
        }

        private Vector2 FromNormalized(Vector2 n) => safe.position + Vector2.Scale(n, safe.size);

        private Vector2 ToNormalized(Vector2 c) =>
            new Vector2((c.x - safe.xMin) / safe.width, (c.y - safe.yMin) / safe.height);

        private static float Sq(float v) => v * v;

        //  ── 저장 ───────────────────────────────────────────────────

        public static string PrefsKey(Which w) => w == Which.Bow ? "lop.archery.layout.bow" : "lop.archery.layout.cancel";

        /// <summary>지금 자리를 저장 문자열로. 안전 영역 기준 비율이다.</summary>
        public string Serialize(Which w) => Format(ToNormalized(Center(w)));

        /// <summary>저장 문자열 둘을 읽는다. 없거나 깨졌으면 그 버튼은 기본 자리다.</summary>
        public void Load(string bow, string cancel)
        {
            bowWish = TryParse(bow, out Vector2 b) ? b : DefaultBow;
            cancelWish = TryParse(cancel, out Vector2 c) ? c : DefaultCancel;
            Settle();
        }

        /// <summary>
        /// "x,y". <b>문화권과 무관하게</b> 소수점은 점이다 — 독일어 기기에서 "0,8,0,62"로 쓰면 쉼표가 갈라 읽힌다.
        /// </summary>
        public static string Format(Vector2 n) =>
            n.x.ToString("0.#####", CultureInfo.InvariantCulture) + "," + n.y.ToString("0.#####", CultureInfo.InvariantCulture);

        public static bool TryParse(string s, out Vector2 n)
        {
            n = default;
            if (string.IsNullOrWhiteSpace(s))
            {
                return false;
            }
            string[] parts = s.Split(',');
            if (parts.Length != 2
                || float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) == false
                || float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y) == false
                || float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y))
            {
                return false;
            }
            n = new Vector2(x, y);
            return true;
        }
    }

    /// <summary>
    /// 손잡이를 잡은 손가락 하나. 끌면 버튼이 따라오고(잡은 자리와 버튼 한가운데의 차이를 유지),
    /// 움직이지 않고 1초 누르면 기본 자리로 되돌린다.
    /// </summary>
    public sealed class ArcheryHandleDrag
    {
        public const float LongPressSeconds = 1f;

        /// <summary>이만큼(세로 대비) 안의 떨림은 옮긴 것으로 안 친다 — 길게 누르려는 엄지는 가만있지 못한다.</summary>
        public const float StillSlopOfHeight = 0.02f;

        private readonly Vector2 start;
        private readonly Vector2 grabOffset;
        private readonly float slop;
        private float held;

        public ArcheryHandleDrag(ArcheryButtonLayout.Which button, Vector2 fingerAt, Vector2 buttonCenter, float panelHeight)
        {
            Button = button;
            start = fingerAt;
            grabOffset = buttonCenter - fingerAt;
            slop = panelHeight * StillSlopOfHeight;
        }

        public ArcheryButtonLayout.Which Button { get; }

        /// <summary>떨림 범위를 넘어 끌기 시작했다.</summary>
        public bool Moved { get; private set; }

        /// <summary>길게 눌러 기본 자리로 되돌렸다.</summary>
        public bool WasReset { get; private set; }

        /// <summary>저장할 것이 있나.</summary>
        public bool Changed => Moved || WasReset;

        /// <summary>손가락이 움직였다. 버튼을 옮길 자리를 주면 true. 떨림 범위 안이거나 이미 되돌렸으면 false.</summary>
        public bool TryMove(Vector2 fingerAt, out Vector2 center)
        {
            center = default;
            if (WasReset)
            {
                return false;
            }
            if (Moved == false && (fingerAt - start).sqrMagnitude <= slop * slop)
            {
                return false;
            }
            Moved = true;
            center = fingerAt + grabOffset;
            return true;
        }

        /// <summary>시간이 흘렀다(실제 시간). 이번에 길게 누르기가 됐으면 true — 한 번만.</summary>
        public bool Tick(float unscaledDeltaSeconds)
        {
            if (Moved || WasReset)
            {
                return false;
            }
            held += unscaledDeltaSeconds;
            if (held < LongPressSeconds)
            {
                return false;
            }
            WasReset = true;
            return true;
        }
    }
}
