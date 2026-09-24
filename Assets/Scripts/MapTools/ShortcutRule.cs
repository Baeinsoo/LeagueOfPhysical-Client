using System.Collections.Generic;
using System.Text;

namespace LOP.MapTools
{
    /// <summary>검사기가 한 길을 증명한 결과. 순수 계층에서 절을 찍으려고 씬 타입 없이 들고 온다.</summary>
    public readonly struct ShortcutProof
    {
        public readonly string Label;
        public readonly float X0, X1;
        /// <summary>탐색이 경로를 찾았나.</summary>
        public readonly bool Found;
        /// <summary>그 경로를 진짜 커널로 재생해도 안 닿았나 — ✅의 근거.</summary>
        public readonly bool Verified;
        public readonly float BlockedX;

        public ShortcutProof(string label, float x0, float x1, bool found, bool verified, float blockedX)
        {
            Label = label; X0 = x0; X1 = x1; Found = found; Verified = verified; BlockedX = blockedX;
        }
    }

    /// <summary>
    /// 지름길 두 길을 따로 증명하기 위한 규칙. 검사기는 탐색의 한 틱 판정을 이 금지 영역으로 감싸
    /// "막힌 것으로 취급"한다 — 탐색 코드는 그대로 두고 길만 강제한다(경로 탐색의 무한대 비용 칸과 같다).
    ///
    /// <para><b>왜 가운데 절반만 막나.</b> 입구·출구 근처는 지름길 띠가 회랑 천장과 겹친다. 거기까지
    /// 막으면 계곡으로 내려가는 새가 벽 위쪽을 스칠 때 거짓으로 막힌다. 가운데 절반은 혀가 두 길을
    /// 완전히 가르는 굴이라(테스트로 못박는다), 어느 길이든 반드시 여기를 지난다.</para>
    /// </summary>
    public static class ShortcutRule
    {
        public static bool InCore(ShortcutRect r, float x)
        {
            float quarter = r.Length * 0.25f;
            return x >= r.X0 + quarter && x <= r.X1 - quarter;
        }

        public static bool ForbidsShortcut(IReadOnlyList<ShortcutRect> all, float x, float y)
        {
            for (int i = 0; i < all.Count; i++)
            {
                ShortcutRect r = all[i];
                if (InCore(r, x) && y >= r.Y0 && y <= r.Y1) { return true; }
            }
            return false;
        }

        public static bool ForbidsValley(ShortcutRect r, float x, float y) => InCore(r, x) && y < r.Y0;

        /// <summary>
        /// 지름길 안 패드 자리. 부스트(대시 = 조종 안 되는 수평 직선)가 <b>출구 <paramref name="exitClear"/>m
        /// 전에 끝나야</b> 한다 — 09-24에 앞이 막힌 패드 6개를 배포한 교훈이다. 자리가 안 나오면 null.
        /// 패드는 굴 뒤 곧은 길에만 선다 — 굴 안은 호를 따라 쳐야 하는 자리다.
        /// </summary>
        public static float? PadCenterX(ShortcutRect r, float boostSpan, float padWidth, float exitClear)
        {
            float right = r.X1 - exitClear - boostSpan;
            float center = right - padWidth * 0.5f;
            if (center - padWidth * 0.5f <= r.ChannelEnd) { return null; }
            return center;
        }

        public static string Section(ShortcutProof safeRoute, IReadOnlyList<ShortcutProof> shortcuts)
        {
            var text = new StringBuilder();
            text.AppendLine("── 🔀 지름길 ──────────────────────────");
            if (shortcuts == null || shortcuts.Count == 0)
            {
                text.AppendLine("  지름길이 없다.");
                return text.ToString().TrimEnd();
            }
            text.AppendLine("  " + Line(safeRoute, "지름길을 막고 완주", "안전한 길이 없다 — 맵이 불가능하다"));
            foreach (ShortcutProof p in shortcuts)
            {
                text.AppendLine($"  x={p.X0:F0}~{p.X1:F0}  "
                              + Line(p, "들어가서 완주", "지름길이 아니라 함정이다(못 들어가거나 못 나온다)"));
            }
            text.AppendLine("  (✅는 탐색이 찾은 길을 진짜 커널로 다시 날려 안 닿은 것이다. 🟡는 찾았지만 재생이 어긋났다.)");
            return text.ToString().TrimEnd();
        }

        static string Line(ShortcutProof p, string okText, string failText)
        {
            string head = string.IsNullOrEmpty(p.Label) ? "" : p.Label + "  ";
            if (p.Found == false) { return $"{head}❌ 탐색 x={p.BlockedX:F1}에서 막힘 — {failText}"; }
            if (p.Verified == false) { return $"{head}🟡 {okText} — 탐색은 찾았으나 재생이 어긋남"; }
            return $"{head}✅ {okText} (재생 확인)";
        }
    }
}
