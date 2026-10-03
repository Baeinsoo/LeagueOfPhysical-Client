using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 중계 자막 문장. 스테이지 해설은 표(TbDodgeStage.caption)에, 스테이지에 속하지 않는 세 줄은 여기 둔다(테마 스펙 §4).
    /// </summary>
    public static class DodgeCaptions
    {
        public const string Opening = "전국 피하기 선수권, 경기 시작합니다.";
        public const string SuddenDeath = "대회 규정에 따라 연장전을 실시합니다.";
        public const string MeEliminated = "아쉽게 탈락했습니다. 관전석으로 모십니다.";

        public static string Stage(int index, string name, string caption) =>
            string.IsNullOrEmpty(caption) ? $"스테이지 {index + 1} — {name}" : caption;

        public static string Eliminated(string playerName) => $"{playerName}, 아쉽게 탈락합니다.";

        /// <summary>"{n}P 선수" — 엔티티 id 서수 순서(나 포함). 어느 클라에서 봐도 같은 사람이 같은 번호다(활쏘기와 같은 규칙).</summary>
        public static string PlayerName(IEnumerable<string> ids, string id)
        {
            var sorted = new List<string>(ids);
            sorted.Sort(string.CompareOrdinal);
            int at = sorted.IndexOf(id);
            return at >= 0 ? $"{at + 1}P 선수" : "선수";
        }
    }

    /// <summary>자막 띠의 줄 서기. 한 줄은 <see cref="LineSeconds"/> 보이고, 밀린 줄이 넘치면 오래된 것부터 버린다.</summary>
    public sealed class DodgeCaptionQueue
    {
        public const double LineSeconds = 2.5;
        public const int MaxPending = 2;

        private readonly Queue<string> pending = new Queue<string>();
        private string current = "";
        private double shownAt = double.NegativeInfinity;

        public void Push(string line)
        {
            pending.Enqueue(line);
            while (pending.Count > MaxPending) pending.Dequeue();
        }

        /// <returns>지금 보일 줄. 없으면 "".</returns>
        public string Tick(double now)
        {
            if (current.Length > 0 && now - shownAt >= LineSeconds)
            {
                current = "";
            }
            if (current.Length == 0 && pending.Count > 0)
            {
                current = pending.Dequeue();
                shownAt = now;
            }
            return current;
        }
    }

    /// <summary>
    /// 매 프레임 스테이지 위치와 선수 탈락을 보고 새로 생긴 사건만 문장으로 낸다. 처음 볼 때 이미 끝난 일(재접속 전 탈락)은 말하지 않는다.
    /// </summary>
    public sealed class DodgeCaptionDirector
    {
        private readonly DodgeStageTable stages;
        private readonly IReadOnlyList<string> stageCaptions;
        private readonly HashSet<string> eliminated = new HashSet<string>();
        private readonly List<string> ids = new List<string>();
        private bool seeded;
        private int lastIndex = -1;

        /// <param name="stageCaptions">스테이지 순서(id 순)의 해설. 비었으면 번호와 이름으로 대신한다.</param>
        public DodgeCaptionDirector(DodgeStageTable stages, IReadOnlyList<string> stageCaptions)
        {
            this.stages = stages;
            this.stageCaptions = stageCaptions;
        }

        public void Observe(in DodgeStagePoint at, IReadOnlyList<(string id, bool eliminated)> players, string myId,
                            List<string> output)
        {
            ids.Clear();
            foreach (var p in players) ids.Add(p.id);
            foreach (var p in players)
            {
                if (!p.eliminated || !eliminated.Add(p.id) || !seeded) continue;
                output.Add(p.id == myId ? DodgeCaptions.MeEliminated : DodgeCaptions.Eliminated(DodgeCaptions.PlayerName(ids, p.id)));
            }
            seeded = true;

            if (!at.Started || at.Index == lastIndex)
            {
                return;
            }
            if (lastIndex < 0 && at.Index == 0)
            {
                output.Add(DodgeCaptions.Opening);
            }
            lastIndex = at.Index;
            if (at.SuddenDeath)
            {
                output.Add(DodgeCaptions.SuddenDeath);
                return;
            }
            string caption = at.Index < stageCaptions.Count ? stageCaptions[at.Index] : "";
            output.Add(DodgeCaptions.Stage(at.Index, stages[at.Index].Name, caption));
        }
    }

    /// <summary>탈락한 선수를 실은 들것이 가장 가까운 벽 밖으로 나가는 길(그림만).</summary>
    public static class DodgeStretcher
    {
        /// <summary>벽 바깥(±10) 너머 — 화면 밖으로 사라진다.</summary>
        public const float Outside = 11f;
        public const float Seconds = 2f;

        public static Vector2 ExitPoint(Vector2 from) =>
            Mathf.Abs(from.x) >= Mathf.Abs(from.y)
                ? new Vector2(from.x >= 0f ? Outside : -Outside, from.y)
                : new Vector2(from.x, from.y >= 0f ? Outside : -Outside);

        /// <summary>천천히 들어 올려 나가다 속도가 붙는다.</summary>
        public static Vector2 Position(Vector2 from, Vector2 exit, float seconds)
        {
            float t = Mathf.Clamp01(seconds / Seconds);
            return Vector2.Lerp(from, exit, t * t);
        }

        public static bool Done(float seconds) => seconds >= Seconds;
    }
}
