using System.Collections.Generic;

namespace LOP
{
    public enum ArcheryLine { Wind, Hush, Bull, Close, Streak, Comeback, NoHit, Win, Final, CrowdHit, Chicken, RobinHood }

    /// <summary>
    /// 해설 자막 한 줄. 떠 있는 자막보다 덜 중요한 말은 버린다 — "대역전!"이 "이번 라운드 가져갑니다"에
    /// 덮이면 안 된다. 같은 말이 반복되지 않게 종류마다 문장 몇 개 중 하나를 고른다.
    /// </summary>
    public sealed class ArcheryCommentary
    {
        //  {n} = 이름, {k} = 숫자. 이름 뒤에 조사(을/를·이/가)를 붙이지 않는다 — 이름이 뭐로 끝날지 모른다.
        private static readonly Dictionary<ArcheryLine, string[]> Lines = new Dictionary<ArcheryLine, string[]>
        {
            [ArcheryLine.Wind] = new[] { "바람이 만만치 않습니다, 조준이 까다롭겠어요", "바람이 {n}쪽으로 붑니다", "관중석 깃발 보세요, 바람이 {n}쪽입니다" },
            [ArcheryLine.Hush] = new[] { "마지막 한 발... 경기장이 조용해집니다", "점수 두 배! 숨을 죽입니다" },
            [ArcheryLine.Bull] = new[] { "{n}, 정중앙!!", "10점! {n} 오늘 컨디션 최고!", "{n}! 이건 교과서입니다!", "{n}, 방금 그건 비전 궁술 제3형입니다" },
            [ArcheryLine.Close] = new[] { "단 {k}cm!! 숨막히는 차이입니다", "사진 판독급! {k}cm!" },
            [ArcheryLine.Streak] = new[] { "{k}연승! 막을 자가 없습니다, {n}!", "{n}! {n}! 이름이 연호됩니다!", "관중석이 연호합니다, {n}!" },
            [ArcheryLine.Comeback] = new[] { "꼴찌에서 1등으로! {n} 대역전!", "{n} 부활합니다!", "꼴찌에서 1등으로, {n} 본인도 놀란 눈치입니다" },
            [ArcheryLine.NoHit] = new[] { "{n}... 이건 조준이 아니라 기도였습니다", "과녁은 저쪽입니다, {n}", "{n}, 바람을 못 읽었네요" },
            [ArcheryLine.Win] = new[] { "이번 라운드 {n} 가져갑니다", "{n}, 깔끔합니다", "이번엔 {n}!" },
            [ArcheryLine.Final] = new[] { "경기 종료! 오늘의 명사수는 {n}!", "{n}, 우승입니다!" },
            [ArcheryLine.CrowdHit] = new[] { "아아— 화살이 관중석으로!", "{n}, 오늘도 관중을 노립니다", "맞은 관객... 아무렇지 않게 응원을 계속합니다", "{n}, 관중과 교감했습니다" },
            [ArcheryLine.Chicken] = new[] { "닭이 제일 놀랐습니다", "{n}, 닭은 과녁이 아닙니다", "오늘 저녁은 치킨...?", "양계장 협회가 항의 성명을 준비 중입니다" },
            [ArcheryLine.RobinHood] = new[] { "로빈 후드!!! 화살이 화살을 쪼갭니다!", "{n}! 이건 전설입니다!", "숲의 의적이 돌아왔습니다, {n}" },
        };

        private readonly System.Func<int, int> pick;
        private float until = float.NegativeInfinity;
        private int priority;

        public string Text { get; private set; } = string.Empty;

        /// <param name="pick"><c>pick(n)</c>은 [0, n) 정수를 준다 — 문장 고르기.</param>
        public ArcheryCommentary(System.Func<int, int> pick)
        {
            this.pick = pick;
        }

        public static int PriorityOf(ArcheryLine line)
        {
            switch (line)
            {
                case ArcheryLine.Final: return 8;
                case ArcheryLine.Hush:
                case ArcheryLine.RobinHood: return 7;
                case ArcheryLine.Streak:
                case ArcheryLine.Comeback: return 6;
                case ArcheryLine.Close: return 5;
                case ArcheryLine.Bull:
                case ArcheryLine.CrowdHit:
                case ArcheryLine.Chicken: return 3;
                case ArcheryLine.NoHit: return 2;
                default: return 1;
            }
        }

        public static int PoolSize(ArcheryLine line) => Lines[line].Length;

        public bool IsShowing(float now) => now < until;

        public bool TrySay(ArcheryLine line, string name, int number, float now, float duration = 2.2f)
        {
            int p = PriorityOf(line);
            if (IsShowing(now) && p < priority)
            {
                return false;
            }
            var pool = Lines[line];
            Text = pool[pick(pool.Length)].Replace("{n}", name).Replace("{k}", number.ToString());
            priority = p;
            until = now + duration;
            return true;
        }
    }
}
