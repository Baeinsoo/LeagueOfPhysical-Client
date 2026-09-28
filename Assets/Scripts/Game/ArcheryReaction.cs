using UnityEngine;

namespace LOP
{
    public enum ArcheryReactionRole { None, Winner, Slump }

    /// <summary>표정·애니를 고를 사건 신호. 몸 튕김(<see cref="ArcheryReactionPose"/>)과 같은 때에 켜진다.</summary>
    public enum ArcheryReactionCue { None, Cheer, Slump }

    public readonly struct ArcheryReactionPose
    {
        /// <summary>몸통을 위로 얼마나 올리나(m). 음수면 가라앉는다.</summary>
        public readonly float Lift;
        /// <summary>옆으로 얼마나 기우나(도).</summary>
        public readonly float TiltDegrees;

        public static readonly ArcheryReactionPose Zero = new ArcheryReactionPose(0f, 0f);

        public ArcheryReactionPose(float lift, float tiltDegrees)
        {
            Lift = lift;
            TiltDegrees = tiltDegrees;
        }
    }

    /// <summary>캐릭터 리액션 — 애니메이션 에셋 없이 몸통을 튕기고 기울인다. 판정과 무관하다.</summary>
    public static class ArcheryReaction
    {
        private const float JumpHeight = 0.25f;
        private const float BullSeconds = 0.9f;
        private const float SlumpDepth = 0.15f;
        private const float SlumpTilt = 12f;

        //  lastRank = 이번 라운드 순위 중 가장 큰 값 — 공동 꼴찌도 다 같이 주저앉는다.
        public static ArcheryReactionRole RoleOf(int rank, int lastRank, bool hit)
        {
            if (hit == false) return ArcheryReactionRole.Slump;
            if (rank == 0) return ArcheryReactionRole.Winner;   // 혼자여도 뛴다
            if (rank >= lastRank) return ArcheryReactionRole.Slump;
            return ArcheryReactionRole.None;
        }

        /// <param name="role">결과 화면이 떠 있을 때의 역할. 안 떠 있으면 None.</param>
        /// <param name="bullAt">마지막 10점 시각. 없으면 음의 무한대.</param>
        public static ArcheryReactionPose PoseAt(float now, float bullAt, ArcheryReactionRole role, float resultOpenedAt)
        {
            if (role == ArcheryReactionRole.Winner)
            {
                return new ArcheryReactionPose(Jump(now - resultOpenedAt, 9f), 0f);
            }
            if (role == ArcheryReactionRole.Slump)
            {
                return new ArcheryReactionPose(-SlumpDepth, SlumpTilt);
            }
            float since = now - bullAt;
            if (since >= 0f && since < BullSeconds)
            {
                return new ArcheryReactionPose(Jump(since, 12f), 0f);
            }
            return ArcheryReactionPose.Zero;
        }

        public static ArcheryReactionCue CueAt(float now, float bullAt, ArcheryReactionRole role)
        {
            if (role == ArcheryReactionRole.Winner) return ArcheryReactionCue.Cheer;
            if (role == ArcheryReactionRole.Slump) return ArcheryReactionCue.Slump;
            float since = now - bullAt;
            return since >= 0f && since < BullSeconds ? ArcheryReactionCue.Cheer : ArcheryReactionCue.None;
        }

        private static float Jump(float t, float speed) => Mathf.Abs(Mathf.Sin(t * speed)) * JumpHeight;
    }

    /// <summary>내 10점의 손맛 — 카메라 흔들림과 하얀 번쩍임. 게임 시계는 건드리지 않는다.</summary>
    public static class ArcheryBullseyeFx
    {
        public const float PopupSeconds = 1.1f;
        private const float ShakeSeconds = 0.3f;
        private const float ShakeMeters = 0.06f;
        private const float FlashSeconds = 0.1f;
        private const float FlashPeak = 0.6f;

        /// <summary>카메라 기준(x 오른쪽, y 위) 흔들림. 시간이 갈수록 줄어 0.3초에 멈춘다.</summary>
        public static Vector3 ShakeOffset(float elapsed)
        {
            if (elapsed < 0f || elapsed >= ShakeSeconds)
            {
                return Vector3.zero;
            }
            float decay = 1f - elapsed / ShakeSeconds;
            return new Vector3(Mathf.Sin(elapsed * 97f + 0.4f), Mathf.Sin(elapsed * 131f + 1.3f), 0f) * (ShakeMeters * decay);
        }

        public static float FlashAlpha(float elapsed)
        {
            if (elapsed < 0f || elapsed >= FlashSeconds)
            {
                return 0f;
            }
            return FlashPeak * (1f - elapsed / FlashSeconds);
        }
    }
}
