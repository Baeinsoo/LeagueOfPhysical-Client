namespace LOP
{
    /// <summary>
    /// 사건을 받아 관중 한 명 한 명의 기분과 그 기분이 끝나는 시각을 정한다. 누가 반응할지는 주사위다 —
    /// 연출이라 클라마다 달라도 된다. 관중석에서 화살을 맞은 사람은 끝까지 환호한다(시제품의 웃음 포인트).
    /// </summary>
    public sealed class ArcheryCrowdDirector
    {
        private readonly System.Func<float> roll;
        private readonly ArcheryCrowdMood[] moods;
        private readonly float[] until;
        private readonly bool[] hit;

        public ArcheryCrowdDirector(int count, System.Func<float> roll)
        {
            this.roll = roll;
            moods = new ArcheryCrowdMood[count];
            until = new float[count];
            hit = new bool[count];
            for (int i = 0; i < count; i++)
            {
                until[i] = float.NegativeInfinity;
            }
        }

        public int Count => moods.Length;

        /// <summary>반응이 없을 때의 기분 — 보통 대기, 마지막 라운드 동안은 정적.</summary>
        public ArcheryCrowdMood BaseMood { get; set; } = ArcheryCrowdMood.Idle;

        public bool HasArrow(int index) => hit[index];

        public ArcheryCrowdMood MoodOf(int index, float now)
        {
            if (hit[index])
            {
                return ArcheryCrowdMood.Cheer;
            }
            return now < until[index] ? moods[index] : BaseMood;
        }

        public void React(ArcheryCrowdMood mood, float fraction, float seconds, float now)
        {
            for (int i = 0; i < moods.Length; i++)
            {
                if (hit[i] || roll() >= fraction)
                {
                    continue;
                }
                moods[i] = mood;
                until[i] = now + seconds * (0.75f + 0.5f * roll());
            }
        }

        public void OnBandHit(int points, float now)
        {
            if (points == 10)
            {
                React(ArcheryCrowdMood.Cheer, 0.6f, 1.4f, now);
            }
            else if (points == 9)
            {
                React(ArcheryCrowdMood.Cheer, 0.2f, 1.0f, now);
            }
        }

        public void OnMiss(float now) => React(ArcheryCrowdMood.Laugh, 0.2f, 1.2f, now);

        public void OnCrowdHit(int victim, float now)
        {
            hit[victim] = true;
            React(ArcheryCrowdMood.Laugh, 0.4f, 1.6f, now);
        }

        public void OnResult(ArcheryLine line, bool finalRound, float now)
        {
            if (finalRound)
            {
                React(ArcheryCrowdMood.Cheer, 1f, 2.6f, now);
            }
            switch (line)
            {
                case ArcheryLine.Close: React(ArcheryCrowdMood.Gasp, 0.8f, 1.6f, now); break;
                case ArcheryLine.Streak: React(ArcheryCrowdMood.Chant, 0.7f, 2.4f, now); break;
                case ArcheryLine.Comeback: React(ArcheryCrowdMood.Cheer, 0.9f, 2.0f, now); break;
                case ArcheryLine.NoHit: React(ArcheryCrowdMood.Boo, 0.55f, 1.8f, now); break;
            }
        }

        public void OnRobinHood(float now) => React(ArcheryCrowdMood.Cheer, 1f, 2.0f, now);

        public void OnMatchEnd(float now) => React(ArcheryCrowdMood.Cheer, 1f, 3.0f, now);
    }
}
