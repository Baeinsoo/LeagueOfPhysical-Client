using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 탐색이 믿는 x가 <b>재생이 실제로 가는 x</b>와 같은지. 둘이 벌어지면 탐색은 자유라고 보고
    /// 재생은 벽에 걸려, 검사기가 스스로 모순된 답을 낸다(2026-09-23에 실제로 그랬다).
    /// </summary>
    public class ColumnXTableTests
    {
        const float StartX = 0f;
        const float Step = 6.8f * 0.02f;   // 전진 6.8m/s · 틱 0.02s

        [Test]
        public void 재생과_같은_더하기다()
        {
            float[] table = FlappyTickMath.ColumnXTable(StartX, Step, 5000);

            //  커널이 하는 것과 글자 그대로 같은 연산 — 한 자리도 달라선 안 된다.
            float replay = StartX;
            for (int i = 1; i < table.Length; i++)
            {
                replay += Step;
                Assert.AreEqual(replay, table[i], 0f, $"{i}번째 틱에서 갈렸다");
            }
        }

        [Test]
        public void 곱셈으로_구하면_실제로_벌어진다()
        {
            //  <b>이 테스트가 곧 버그의 증거다.</b> 곱셈과 더하기가 같은 값이면 애초에 고칠 것이
            //  없었다는 뜻이므로, 차이가 <i>실재함</i>을 못박아 둔다 — 누가 "어차피 같다"며
            //  곱셈으로 되돌리면 여기가 먼저 말한다.
            float[] table = FlappyTickMath.ColumnXTable(StartX, Step, 4300);

            float multiplied = StartX + Step * 4267;
            float accumulated = table[4267];

            //  커널이 벽에서 띄우는 여유가 0.02m다. 그보다 크게 벌어지므로 스치는 판정이 뒤집힌다.
            Assert.Greater(System.Math.Abs(accumulated - multiplied), 0.02f,
                           "곱셈과 더하기가 0.02m 안쪽이면 이 수정의 전제가 사라진 것이다");
        }

        [Test]
        public void 짧은_코스에서는_거의_같다()
        {
            //  옛 60초 코스(약 3000틱)에서는 어긋남이 여유 안쪽이라 검사가 통과했다 —
            //  코스를 90초로 늘리면서 드러난 버그라는 사실을 숫자로 남긴다.
            float[] table = FlappyTickMath.ColumnXTable(StartX, Step, 3100);

            float drift = System.Math.Abs(table[3000] - (StartX + Step * 3000));

            Assert.Less(drift, 0.02f, "짧은 코스에서도 여유를 넘었다면 옛 통과가 운이었다는 뜻이다");
        }

        [Test]
        public void 첫_칸은_출발점이다()
        {
            Assert.AreEqual(StartX, FlappyTickMath.ColumnXTable(StartX, Step, 10)[0], 0f);
        }
    }
}
