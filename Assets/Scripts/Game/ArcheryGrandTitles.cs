using System.Collections.Generic;
using System.Linq;

namespace LOP
{
    /// <summary>
    /// 정색 컷인의 외침과 거창한 이름. 이 글자들로 컷인 글꼴(정적)을 굽는다 — 글자를 바꾸면 <c>LOP/UI/Build CutIn Font</c>를 다시 돌린다
    /// (<see cref="AllGlyphs"/>를 시험이 글꼴과 대조한다).
    /// </summary>
    public static class ArcheryGrandTitles
    {
        private static readonly Dictionary<ArcheryCutInKind, (string shout, string[] names)> Titles =
            new Dictionary<ArcheryCutInKind, (string, string[])>
            {
                [ArcheryCutInKind.Bull] = ("정.중.앙.", new[] { "비전 궁술 / 제3형 / 바람 가르기", "천 년에 한 번 / 과녁의 심장", "무념무상 / 한 점 관통" }),
                [ArcheryCutInKind.RobinHood] = ("로.빈.후.드.", new[] { "숲의 의적 / 화살 위의 화살", "전설의 재현 / 쪼개진 화살", "화살이 화살을 / 꿰뚫다" }),
                [ArcheryCutInKind.Comeback] = ("대.역.전.", new[] { "꼴찌에서 1등까지 / 한 발", "불사조 궁술 / 재의 날개", "운명 역행 / 제1장" }),
                [ArcheryCutInKind.LastPlace] = ("꼴.찌.확.정.", new[] { "관중 전원 / 기립", "패배의 미학 / 완성", "내일의 명사수 / 오늘의 꼴찌" }),
            };

        public static string Shout(ArcheryCutInKind kind) => Titles.TryGetValue(kind, out var t) ? t.shout : string.Empty;

        /// <param name="pick"><c>pick(n)</c>은 [0, n) 정수.</param>
        public static string Name(ArcheryCutInKind kind, System.Func<int, int> pick)
        {
            if (Titles.TryGetValue(kind, out var t) == false) return string.Empty;
            return t.names[pick(t.names.Length)];
        }

        public static string AllGlyphs()
            => new string(Titles.Values.SelectMany(t => t.shout + string.Concat(t.names)).Distinct().ToArray());
    }
}
