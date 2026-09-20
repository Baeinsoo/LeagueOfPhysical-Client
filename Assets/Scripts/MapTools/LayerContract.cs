using System.Collections.Generic;
using System.Text;

namespace LOP.MapTools
{
    /// <summary>검사에 넘기는 블록 하나. 씬 타입을 안 들고 와야 순수 계층에서 잴 수 있다.</summary>
    public readonly struct LayerBlock
    {
        public readonly string Name;
        public readonly float X;
        /// <summary>새의 z대역과 겹치는가 — 즉 닿을 수 있는 자리인가.</summary>
        public readonly bool IsGameplay;
        public readonly bool HasCollider;
        public readonly string MaterialName;

        public LayerBlock(string name, float x, bool isGameplay, bool hasCollider, string materialName)
        {
            Name = name;
            X = x;
            IsGameplay = isGameplay;
            HasCollider = hasCollider;
            MaterialName = materialName;
        }
    }

    public readonly struct LayerViolation
    {
        public readonly string Name;
        public readonly float X;
        public readonly string Reason;

        public LayerViolation(string name, float x, string reason)
        {
            Name = name;
            X = x;
            Reason = reason;
        }
    }

    /// <summary>
    /// <b>층 규약</b> — 닿는 것과 안 닿는 것이 보이는 대로여야 한다는 약속을 기계가 지킨다.
    ///
    /// <para>맵을 세 층(게임 평면 · 중간층 · 배경)으로 가르고 나면 눈으로 못 잡는 버그가 둘
    /// 생긴다: <b>배경인데 부딪힌다</b>(배경에 콜라이더가 남음), <b>장애물인데 배경처럼
    /// 보인다</b>(게임 평면이 배경 재질을 씀). 둘 다 "다음 사람이 블록 하나를 잘못 놓으면"
    /// 조용히 돌아오는 종류라 검사로 막는다.</para>
    ///
    /// <para><b>판단 기준은 콜라이더가 아니라 재질이다.</b> 게임 평면에 있다는 것만으로
    /// 장애물이라고 보면 결승 배너·동전 같은 소품이 전부 위반으로 잡힌다(처음 돌렸을 때
    /// 실제로 26개가 그렇게 나왔다). 그래서 "장애물로 칠해져 있는가"를 먼저 묻고,
    /// 칠해진 것에만 콜라이더를 요구한다.</para>
    ///
    /// <para>배경 <i>재질</i>은 목록으로 강제하지 않는다 — 규약은 "닿는 것"에만 건다.
    /// 아트가 배경을 자유롭게 손대도 이 검사가 방해하지 않아야 한다.</para>
    /// </summary>
    public static class LayerContract
    {
        public static List<LayerViolation> Check(IReadOnlyList<LayerBlock> blocks,
                                                 IReadOnlyCollection<string> gameplayMaterials)
        {
            var bad = new List<LayerViolation>();
            if (blocks == null)
            {
                return bad;
            }
            foreach (LayerBlock b in blocks)
            {
                //  장애물로 <b>칠해져 있는가</b>가 판단 기준이다 — 게임 평면에 있다는 것만으로는
                //  장애물이 아니다(결승 배너·동전 같은 소품도 거기 있다).
                bool painted = IsGameplayMaterial(b.MaterialName, gameplayMaterials);

                if (b.IsGameplay == false)
                {
                    if (b.HasCollider)
                    {
                        bad.Add(new LayerViolation(b.Name, b.X, "배경인데 콜라이더가 있다 — 안 보이는 벽이 된다"));
                    }
                    continue;
                }
                if (b.HasCollider)
                {
                    if (painted == false)
                    {
                        bad.Add(new LayerViolation(b.Name, b.X,
                            $"닿는 것인데 재질이 '{b.MaterialName ?? "없음"}' — 배경처럼 읽힌다"));
                    }
                }
                else if (painted)
                {
                    bad.Add(new LayerViolation(b.Name, b.X,
                        "장애물 재질인데 콜라이더가 없다 — 보이는데 통과된다"));
                }
                //  그 밖(소품 재질 + 콜라이더 없음)은 소품이다. 규약이 막으려는 사고가 아니다.
            }
            return bad;
        }

        private static bool IsGameplayMaterial(string name, IReadOnlyCollection<string> allowed)
        {
            if (string.IsNullOrEmpty(name) || allowed == null)
            {
                return false;
            }
            foreach (string a in allowed)
            {
                if (string.Equals(a, name, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        public static string Section(IReadOnlyList<LayerBlock> blocks,
                                     IReadOnlyCollection<string> gameplayMaterials)
        {
            var text = new StringBuilder();
            text.AppendLine("── 🧱 층 규약 ─────────────────────────");
            text.AppendLine("  (닿는 것은 게임 평면에만 있다. 배경에 콜라이더가 남거나 게임 평면이");
            text.AppendLine("   배경 재질을 쓰면 '보이는 대로 부딪힌다'가 깨진다.)");

            if (blocks == null || blocks.Count == 0)
            {
                text.Append("  훑은 블록이 없다 — 검사가 아무것도 못 봤다");
                return text.ToString();
            }

            int gameplay = 0;
            foreach (LayerBlock b in blocks)
            {
                if (b.IsGameplay) { gameplay++; }
            }
            text.AppendLine($"  게임 평면 {gameplay}개 · 배경 {blocks.Count - gameplay}개");

            List<LayerViolation> bad = Check(blocks, gameplayMaterials);
            if (bad.Count == 0)
            {
                text.Append("  ✅ 위반 없음");
                return text.ToString();
            }
            text.AppendLine($"  ❌ 위반 {bad.Count}개");
            for (int i = 0; i < bad.Count && i < 12; i++)
            {
                text.AppendLine($"     {bad[i].Name} (x={bad[i].X:F1}) — {bad[i].Reason}");
            }
            if (bad.Count > 12)
            {
                text.AppendLine($"     … 그리고 {bad.Count - 12}개 더");
            }
            return text.ToString().TrimEnd();
        }
    }
}
