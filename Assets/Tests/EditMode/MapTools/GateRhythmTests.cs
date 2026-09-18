using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 관문을 어떻게 놓아야 하는가의 목표치와, 지형에서 관문을 찾아내는 산술을 못박는다.
    /// 씬도 물리 엔진도 안 쓴다 — 여기서 재는 것은 <b>규칙</b>이지 맵이 아니다.
    /// </summary>
    public class GateRhythmTests
    {
        //  물리 표본 — 규칙을 재려고 얼려 둔 값이다(게임이 날갯짓 23 / 중력 70을 쓰던 시절 수치).
        //  아래 경계값들을 이 수치로 손으로 풀어 놨으니 여기만 고치면 그 값들이 통째로 틀려진다.
        //  지금 게임 값은 MasterData(TbFlappyConfig)에 있고 프로덕션은 거기서 읽어 기준을 그때그때
        //  계산하므로, 이 상수가 게임과 달라도 맵 검사 결과는 옳다.
        const float FlapImpulse = 23f;
        const float Gravity = 70f;
        const float TickSeconds = 0.02f;
        const float BodyHeight = 0.9f;

        static float Target => GateRhythmRule.TargetWindow(FlapImpulse, Gravity, TickSeconds, BodyHeight);

        static PinchColumn Col(float x, float band, bool open = false)
            => new PinchColumn(x, band, bandBottom: 0f, bandTop: band,
                               floor: "Cube", ceiling: "Cube", open);

        // ── 목표치 ──────────────────────────────────────────────

        [Test]
        public void 목표_창은_아치가_빈틈의_90퍼센트를_먹는_높이다()
        {
            //  숫자를 박지 않았다는 증거 — 아치(4.012001) ÷ 0.9 + 몸(0.9) = 5.36.
            Assert.AreEqual(4.012001f / 0.9f + 0.9f, Target, 0.0005f);
        }

        [Test]
        public void 목표_창은_물리적_하한보다_넓다()
        {
            //  하한(아치+몸)은 여유가 0인 폭이라 통과는 되지만 실수를 한 번도 못 한다.
            //  목표는 그보다 넓어야 한다 — 안 그러면 "목표대로 지었는데 아무도 못 지나는" 맵이 된다.
            float floor = ObstaclePlacementRule.RequiredBand(FlapImpulse, Gravity, TickSeconds, BodyHeight);
            Assert.Greater(Target, floor);
        }

        [Test]
        public void 목표_간격은_미터가_아니라_초에서_나온다()
        {
            //  전진 속도가 바뀌면 미터는 따라 움직이고 박자는 그대로여야 한다.
            Assert.AreEqual(1.67f * 11f, GateRhythmRule.TargetSpacing(11f), 1e-4f);
            Assert.AreEqual(1.67f * 6.8f, GateRhythmRule.TargetSpacing(6.8f), 1e-4f);
        }

        // ── 관문 찾기 ───────────────────────────────────────────

        [Test]
        public void 목표의_1_5배_이하로_좁아진_구간이_관문이다()
        {
            float narrow = Target;                       // 관문
            float wide = Target * GateRhythmRule.GateBandFactor + 0.01f;   // 관문 아님
            var columns = new List<PinchColumn>
            {
                Col(0f, wide), Col(1f, narrow), Col(2f, narrow), Col(3f, wide), Col(4f, narrow),
            };

            var gates = GateRhythmRule.Find(columns, Target);

            Assert.AreEqual(2, gates.Count);
            Assert.AreEqual(1f, gates[0].StartX, 1e-4f);
            Assert.AreEqual(2f, gates[0].EndX, 1e-4f);
            Assert.AreEqual(4f, gates[1].CenterX, 1e-4f);
        }

        [Test]
        public void 관문의_창은_그_구간에서_가장_좁은_값이다()
        {
            //  한 자리만 좁아도 거기서 막히므로 최솟값이어야 한다. 평균이나 첫 값을 쓰면
            //  "평균은 넉넉한데 실제로는 못 지나는" 관문을 통과로 보고한다.
            var columns = new List<PinchColumn>
            {
                Col(0f, Target), Col(1f, Target - 0.5f), Col(2f, Target),
            };

            var gates = GateRhythmRule.Find(columns, Target);

            Assert.AreEqual(1, gates.Count);
            Assert.AreEqual(Target - 0.5f, gates[0].Window, 1e-4f);
        }

        [Test]
        public void 트인_칸은_관문이_아니다()
        {
            //  천장이 없으면 밴드가 0으로 나오는데, 그걸 "0m 관문"으로 세면 열린 하늘을
            //  벽으로 보고하게 된다.
            var columns = new List<PinchColumn> { Col(0f, 0f, open: true), Col(1f, 0f, open: true) };

            Assert.AreEqual(0, GateRhythmRule.Find(columns, Target).Count);
        }

        [Test]
        public void 훑은_칸이_없으면_관문도_없다()
        {
            Assert.AreEqual(0, GateRhythmRule.Find(null, Target).Count);
            Assert.AreEqual(0, GateRhythmRule.Find(new List<PinchColumn>(), Target).Count);
        }

        // ── 리포트 ──────────────────────────────────────────────

        static string Section(params Gate[] gates)
            => GateRhythmRule.Section(gates, Target, GateRhythmRule.TargetSpacing(11f), 11f,
                                      courseStartX: 0f, finishX: 200f);

        [Test]
        public void 관문이_없으면_그렇게_적는다()
        {
            //  빈 절을 찍으면 "재 봤더니 괜찮다"로 잘못 읽힌다.
            Assert.That(Section().IndexOf("관문을 하나도 못 찾았다", System.StringComparison.Ordinal),
                        Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void 평균_간격을_목표와_견줘_적는다()
        {
            //  간격 18.37m(목표)로 세 개 — 평균이 목표의 1.0배로 나와야 한다.
            float s = GateRhythmRule.TargetSpacing(11f);
            string text = Section(new Gate(0f, 0f, Target), new Gate(s, s, Target),
                                  new Gate(2 * s, 2 * s, Target));

            Assert.That(text.IndexOf("1.0배", System.StringComparison.Ordinal),
                        Is.GreaterThanOrEqualTo(0), "1.0배");
        }

        [Test]
        public void 가장_긴_공백은_결승까지의_꼬리도_센다()
        {
            //  마지막 관문에서 결승까지 아무것도 없으면 코스가 맥없이 끝난다 — 관문 사이만
            //  보면 그 공백이 안 보인다(우리 맵이 실제로 246m 비어 있었다).
            string text = Section(new Gate(0f, 0f, Target), new Gate(10f, 10f, Target));

            Assert.That(text.IndexOf("가장 긴 공백 190m", System.StringComparison.Ordinal),
                        Is.GreaterThanOrEqualTo(0), "가장 긴 공백 190m");
        }

        [Test]
        public void 목표보다_10퍼센트_넘게_넓은_창은_표시한다()
        {
            string text = Section(new Gate(0f, 0f, Target * 1.2f));

            Assert.That(text.IndexOf("창 넓음", System.StringComparison.Ordinal),
                        Is.GreaterThanOrEqualTo(0), "창 넓음");
        }
    }
}
