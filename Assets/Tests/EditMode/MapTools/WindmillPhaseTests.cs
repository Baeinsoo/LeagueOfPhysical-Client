using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 리포트 머리말이 "이 검사는 한 위상만 본다"고 말하면서 함께 찍는 수 — 같은 모양이 다시
    /// 나타나기까지의 틱. 이 수가 틀리면 "위상을 전부 보려면 몇 판을 봐야 하나"를 잘못 짚는다.
    /// </summary>
    public class WindmillPhaseTests
    {
        const float TickSeconds = 0.02f;

        [Test]
        public void 십자_날개는_한_바퀴가_아니라_90도마다_같다()
        {
            //  55°/s로 90°를 도는 데 1.6363…초 = 81.8틱. 날개가 걸쳐 있는 틱까지 포함해야
            //  "이만큼 보면 전부 본 것"이 되므로 올린다.
            Assert.AreEqual(82, WindmillPhase.SpaceTicks(55f, arms: 4, TickSeconds));
            //  한 바퀴를 세면 네 배가 나온다 — 대칭을 안 쓰면 필요 없는 판을 세 배 더 돌게 된다.
            Assert.AreEqual(328, WindmillPhase.SpaceTicks(55f, arms: 1, TickSeconds));
        }

        [Test]
        public void 반대로_도는_풍차도_위상_공간은_같다()
        {
            Assert.AreEqual(WindmillPhase.SpaceTicks(55f, arms: 4, TickSeconds),
                            WindmillPhase.SpaceTicks(-55f, arms: 4, TickSeconds));
        }

        [Test]
        public void 안_도는_풍차는_볼_위상이_하나뿐이다()
        {
            Assert.AreEqual(1, WindmillPhase.SpaceTicks(0f, arms: 4, TickSeconds));
        }

        [Test]
        public void 날개를_못_세면_대칭을_쓰지_않고_한_바퀴를_센다()
        {
            Assert.AreEqual(WindmillPhase.SpaceTicks(55f, arms: 1, TickSeconds),
                            WindmillPhase.SpaceTicks(55f, arms: 0, TickSeconds));
        }

        [Test]
        public void 틱_길이가_0이하면_틱_수로_말할_수_없으므로_터뜨린다()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => WindmillPhase.SpaceTicks(55f, arms: 4, tickSeconds: 0f));
        }
    }
}
