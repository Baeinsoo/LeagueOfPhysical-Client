namespace LOP
{
    public readonly struct ChibiCue
    {
        /// <summary>애니메이터 트리거. null이면 몸은 평소 동작 그대로.</summary>
        public readonly string Trigger;
        public readonly ChibiExpression Expression;

        public ChibiCue(string trigger, ChibiExpression expression)
        {
            Trigger = trigger;
            Expression = expression;
        }
    }

    /// <summary>리액션 신호를 치비의 애니·표정으로 바꾼다. 사건(환호·좌절)이 조준보다 앞선다.</summary>
    public static class ChibiReaction
    {
        public static ChibiCue Of(ArcheryReactionCue cue, bool drawing) => Of(cue, drawing, surprised: false);

        /// <param name="surprised">로빈 후드·닭 소동 직후(만화 "!"와 같은 때) — 사건 표정 다음, 조준보다 앞.</param>
        public static ChibiCue Of(ArcheryReactionCue cue, bool drawing, bool surprised)
        {
            switch (cue)
            {
                case ArcheryReactionCue.Cheer: return new ChibiCue("Happy", ChibiExpression.Cheer);
                case ArcheryReactionCue.Slump: return new ChibiCue("Sad", ChibiExpression.Despair);
            }
            if (surprised)
            {
                return new ChibiCue(null, ChibiExpression.Surprise);
            }
            return new ChibiCue(null, drawing ? ChibiExpression.Focus : ChibiExpression.Normal);
        }

        /// <summary>트리거는 신호가 바뀔 때 한 번만 — 매 프레임 걸면 동작이 처음부터 다시 돈다.</summary>
        public static string TriggerOnChange(ArcheryReactionCue previous, ArcheryReactionCue next)
            => previous == next ? null : Of(next, false).Trigger;
    }
}
