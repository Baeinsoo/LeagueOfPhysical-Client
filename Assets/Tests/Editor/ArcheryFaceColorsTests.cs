using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    //  세계양궁연맹 122cm 과녁은 링이 10개(각 6.1cm)이고, 색이 **두 링씩** 묶인다:
    //  10·9 금 / 8·7 빨강 / 6·5 파랑 / 4·3 검정 / 2·1 흰색.
    //
    //  색을 **띠 번호**로 고르면 띠 개수가 바뀔 때 어긋난다 — 실제로 3개일 땐 맞다가
    //  10개로 늘리자 `번호 % 5`가 되감겨 6번째가 다시 금색이 됐다. 그래서 비율로 고른다.
    public class ArcheryFaceColorsTests
    {
        [Test]
        public void 규정_10링이_두_개씩_같은_색이다()
        {
            //  비율 0.1 = 10점, 0.2 = 9점, … 1.0 = 1점
            var expected = new[]
            {
                (0.1f, ArcheryFaceColors.Gold),  (0.2f, ArcheryFaceColors.Gold),
                (0.3f, ArcheryFaceColors.Red),   (0.4f, ArcheryFaceColors.Red),
                (0.5f, ArcheryFaceColors.Blue),  (0.6f, ArcheryFaceColors.Blue),
                (0.7f, ArcheryFaceColors.Black), (0.8f, ArcheryFaceColors.Black),
                (0.9f, ArcheryFaceColors.White), (1.0f, ArcheryFaceColors.White),
            };

            foreach (var (ratio, color) in expected)
            {
                Assert.AreEqual(color, ArcheryFaceColors.Of(ratio), "비율 " + ratio + "의 색이 규정과 다르다");
            }
        }

        //  띠를 몇 개로 쪼개든 **같은 자리는 같은 색**이어야 한다 — 3D 과녁과 기록판이
        //  서로 다른 개수로 그려도 눈에 같은 그림이 나와야 하기 때문이다.
        [Test]
        public void 띠_개수가_달라도_같은_자리는_같은_색이다()
        {
            Assert.AreEqual(ArcheryFaceColors.Of(0.2f), ArcheryFaceColors.Of(0.15f), "금색 구간 안에서 색이 갈린다");
            Assert.AreEqual(ArcheryFaceColors.Of(1.0f), ArcheryFaceColors.Of(0.95f), "흰색 구간 안에서 색이 갈린다");
        }

        //  **경계가 어디냐**가 이 함수의 계약이다. 안쪽 색이 경계를 포함한다(규정의 링
        //  바깥 반지름이 곧 그 링의 경계).
        //
        //  ⚠️ 처음엔 규정 비율 10개만 재고 이 시험을 통과로 봤는데, **경계를 0.4에서 0.45로
        //  밀어도 그 10개가 전부 그대로라 안 잡혔다.** 재는 지점 사이가 비어 있으면 시험이
        //  아니라 통과 도장이다 — 네 경계를 양쪽에서 못 박는다.
        [Test]
        public void 네_경계가_모두_제자리에_있다()
        {
            const float eps = 1e-4f;
            var boundaries = new[]
            {
                (0.2f, ArcheryFaceColors.Gold,  ArcheryFaceColors.Red),
                (0.4f, ArcheryFaceColors.Red,   ArcheryFaceColors.Blue),
                (0.6f, ArcheryFaceColors.Blue,  ArcheryFaceColors.Black),
                (0.8f, ArcheryFaceColors.Black, ArcheryFaceColors.White),
            };

            foreach (var (at, inner, outer) in boundaries)
            {
                Assert.AreEqual(inner, ArcheryFaceColors.Of(at),
                    "경계 " + at + " 자체는 안쪽 색이어야 한다");
                Assert.AreEqual(outer, ArcheryFaceColors.Of(at + eps),
                    "경계 " + at + "를 넘으면 바깥 색이어야 한다");
                Assert.AreEqual(inner, ArcheryFaceColors.Of(at - eps),
                    "경계 " + at + " 바로 안쪽이 이미 바깥 색이다 — 경계가 밀렸다");
            }
        }

        //  0과 음수(중앙 정중앙)도 안전해야 한다 — 계산 오차로 −0이 들어올 수 있다.
        [Test]
        public void 한가운데도_금색이다()
        {
            Assert.AreEqual(ArcheryFaceColors.Gold, ArcheryFaceColors.Of(0f));
            Assert.AreEqual(ArcheryFaceColors.Gold, ArcheryFaceColors.Of(-0.001f));
        }
    }
}
