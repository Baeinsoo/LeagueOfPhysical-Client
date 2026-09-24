using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// <b>내 화살이 과녁 어디에 꽂혔나</b>를 지금 자리에 대해서만 모아 둔다. 화면이 이걸 읽어
    /// 작은 과녁 그림에 점을 찍는다.
    ///
    /// <para><b>왜 필요한가</b>: 이 게임의 실력은 리드 추정이고, 리드는 *빗나간 걸 보고 고치면서*
    /// 는다. 그런데 90m 과녁은 당긴 화면에서 지름이 <b>38픽셀</b>, 10점 링이 <b>7.6픽셀</b>이라
    /// 화살이 10에 꽂혔는지 8에 꽂혔는지 <b>눈으로 분간이 안 된다.</b> 점수도 누적 한 덩어리만
    /// 떠서 어느 발이 몇 점이었는지 연결되지 않는다. 그 둘이 겹치면 학습 신호가 0이라,
    /// 실제로 실력이 늘어도 <b>운처럼 느껴진다.</b></para>
    ///
    /// <para><b>자리마다 비운다</b> — "이 자리에서 내가 어디로 빠지고 있나"가 읽혀야 그 자리 안에서
    /// 바로 고칠 수 있다. 판 전체를 쌓으면 거리마다 리드가 달라 점이 섞인다.</para>
    /// </summary>
    public class ArcheryImpactLog
    {
        public readonly struct Shot
        {
            /// <summary>과녁 중심에서 얼마나 벗어났나. <b>면 반지름을 1로 본 값</b>이고 x는 오른쪽, y는 위다.</summary>
            public readonly Vector2 FaceOffset;

            /// <summary>서버가 확정한 점수.</summary>
            public readonly int Points;

            /// <summary>꽂힌 자리(월드). 화면이 "+10"을 거기서 띄우는 데 쓴다.</summary>
            public readonly Vector3 WorldPosition;

            public Shot(Vector2 faceOffset, int points, Vector3 worldPosition)
            {
                FaceOffset = faceOffset;
                Points = points;
                WorldPosition = worldPosition;
            }
        }

        private readonly List<Shot> shots = new List<Shot>();

        /// <summary>지금 모으고 있는 자리(웨이브) 번호. 아직 하나도 없으면 −1.</summary>
        public int Wave { get; private set; } = -1;

        public IReadOnlyList<Shot> Shots => shots;

        public void Add(int wave, in Shot shot)
        {
            if (wave != Wave)
            {
                shots.Clear();
                Wave = wave;
            }
            shots.Add(shot);
        }

        /// <summary>
        /// 이미 화면에 띄운 것이 <paramref name="shownWave"/>의 <paramref name="shownCount"/>발일 때,
        /// 지금 목록(<paramref name="wave"/> / <paramref name="count"/>)에서 <b>새로 띄울 첫 번째 번호</b>.
        ///
        /// <para><b>발수만 보면 안 된다</b> — 자리가 바뀌면 목록이 비워져 발수가 1로 되돌아가는데,
        /// 앞 자리에서도 1발을 띄웠으면 "이미 띄웠다"로 착각해 <b>그 뒤로 영영 안 뜬다.</b>
        /// (2026-09-22 실측: 첫 명중만 뜨고 다른 거리 과녁을 맞혀도 안 떴다.)</para>
        /// </summary>
        public static int FirstUnshown(int shownWave, int shownCount, int wave, int count)
        {
            if (wave != shownWave || count < shownCount)
            {
                return 0;   // 자리가 바뀌었거나 목록이 줄었다 — 처음부터 다시
            }
            return shownCount;
        }

        /// <summary>
        /// 꽂힌 자리를 <b>과녁 면 위의 좌표</b>로 바꾼다 — 면 반지름을 1로 보고, 사수가 보는 기준으로
        /// x는 오른쪽 y는 위.
        ///
        /// <para><paramref name="facing"/>은 과녁이 바라보는 쪽, 즉 <b>사수를 향한</b> 방향이다.
        /// 그래서 사수가 보는 앞쪽은 그 반대다 — 부호를 한 번만 헷갈려도 점이 좌우로 뒤집혀
        /// "왼쪽으로 빠진다"를 오른쪽으로 읽게 된다.</para>
        /// </summary>
        public static Vector2 ToFaceOffset(Vector3 offsetFromTarget, Vector3 facing, float radius)
        {
            return ArcheryFaceCoords.ToFaceOffset(offsetFromTarget, facing, radius);
        }
    }
}
