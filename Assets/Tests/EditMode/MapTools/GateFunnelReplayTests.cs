using System;
using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 깔때기가 낸 <b>조작열</b>을 커널로 되굴려 통과 여부를 다시 내는 부분을 못박는다.
    /// 씬도 물리 엔진도 안 쓴다 — 여기서 재는 것은 규칙이지 맵이 아니다.
    ///
    /// <para><b>왜 이 계층이 생겼나.</b> 깔때기는 "지나는 조작열이 있다"만 답할 뿐 그 조작열을
    /// 보여 주지 않았다. 그래서 깔때기가 통과라 한 것과 천장 가드가 막는다고 한 것이 서로
    /// 반대일 때, 어느 쪽이 틀렸는지를 <b>잴 수가 없었다</b>. 조작열을 꺼내 같은 커널로
    /// 되굴리면 그 대조가 숫자가 된다(2026-09-12 실측에서 실제로 갈랐다).</para>
    /// </summary>
    public class GateFunnelReplayTests
    {
        //  실제 Flappy 값. 여기를 고치면 다른 숫자를 재는 것이다.
        const float ForwardSpeed = 11f;
        const float Gravity = 70f;
        const float MaxFallSpeed = 30f;
        const float FlapImpulse = 23f;
        const float TickSeconds = 0.02f;
        const float BodyHeight = 0.9f;

        static FlightKernel Kernel => new FlightKernel(ForwardSpeed, Gravity, MaxFallSpeed,
                                                       FlapImpulse, TickSeconds, BodyHeight);

        static GateWindow Window(float bottom, float top) => new GateWindow(bottom, top, "밑", "위");

        static List<GateColumn> Gate(params GateWindow[] windows)
        {
            var columns = new List<GateColumn>();
            for (int i = 0; i <= 6; i++)
            {
                columns.Add(new GateColumn(i * 0.5f, windows));
            }
            return columns;
        }

        //  창 하나짜리 높은 회랑(0~10). 한 틱은 0.22m 나아가므로 끝 열 x=3.0을 넘는 데 14틱이 든다.
        static List<GateColumn> TallGate() => Gate(Window(0f, 10f));

        //  날갯짓 한 틱이 0.46m를 올리는데 여유가 0.4m뿐이라, 어디서 눌러도 천장을 뚫는다.
        static List<GateColumn> LowGate() => Gate(Window(0f, 1.3f));

        static bool[] Coast(int ticks) => new bool[ticks];

        //  관문 뒤를 볼 수 없는 경우 — 이 파일은 관문 안의 규칙과 재생이 갈리지 않는지만 본다.
        static readonly List<GateColumn> NoRunout = null;

        // ── 세 갈래가 옳게 갈리나 ───────────────────────────────────────────

        //  손으로 푼 값: 날갯짓이 없으면 y_k = y0 − 0.014·k(k+1)이다.
        //  y0=5면 14틱 뒤 5 − 0.014·14·15 = 2.06 — 바닥 0 위, 머리 2.96도 천장 10 아래다.
        [Test]
        public void 창_안에_머무는_조작열은_통과로_갈린다()
        {
            FunnelReplay replay = GateFunnelReplayRule.Replay(5f, 0f, Coast(14), TallGate(), Kernel);

            Assert.IsTrue(replay.Passed, "회랑이 충분히 높으니 가만 있어도 끝까지 간다.");
            Assert.AreEqual(13, replay.Tick, "14번째 틱(0부터 세어 13)에 x=3.08로 끝 열을 넘는다.");
            Assert.AreEqual(3.08f, replay.X, 0.01f);
            Assert.IsNull(replay.Hit, "안 막혔으면 막은 면도 없다.");
        }

        //  y0=0.5면 6틱 뒤 0.5 − 0.014·6·7 = −0.088로 창 바닥(0)을 지나친다.
        //  그 앞 5틱 뒤는 0.5 − 0.014·5·6 = 0.08로 아직 창 안이다.
        [Test]
        public void 창_바닥에_못_미치면_바닥에_막힌다()
        {
            FunnelReplay replay = GateFunnelReplayRule.Replay(0.5f, 0f, Coast(14), TallGate(), Kernel);

            Assert.IsFalse(replay.Passed);
            Assert.AreEqual(5, replay.Tick, "여섯 번째 틱(0부터 세어 5)에 바닥을 지나친다.");
            Assert.IsFalse(replay.AboveCeiling, "아래로 빠진 것이다.");
            Assert.AreEqual("밑", replay.Hit);
            Assert.AreEqual(0.088f, replay.Overshoot, 0.005f, "바닥을 0.088m 지나친다.");
        }

        //  날갯짓은 세로 속도를 23으로 <b>덮어쓰므로</b> 한 틱에 꼭 0.46m 오른다.
        //  0.2 + 0.46 = 0.66, 머리는 1.56 — 천장 1.3을 0.26m 뚫는다.
        [Test]
        public void 천장을_뚫는_조작열은_천장에_막힌다()
        {
            FunnelReplay replay = GateFunnelReplayRule.Replay(
                0.2f, 0f, new[] { true, false, false }, LowGate(), Kernel);

            Assert.IsFalse(replay.Passed);
            Assert.AreEqual(0, replay.Tick, "첫 틱에 이미 뚫는다.");
            Assert.IsTrue(replay.AboveCeiling);
            Assert.AreEqual("위", replay.Hit);
            Assert.AreEqual(0.26f, replay.Overshoot, 0.005f, "천장을 0.26m 넘긴다.");
        }

        [Test]
        public void 조작열이_관문_끝까지_못_데려가면_통과가_아니다()
        {
            FunnelReplay replay = GateFunnelReplayRule.Replay(5f, 0f, Coast(3), TallGate(), Kernel);

            Assert.IsFalse(replay.Passed, "안 막혔어도 끝 열을 못 넘었으면 지난 것이 아니다.");
            Assert.AreEqual(3, replay.Tick);
            Assert.IsNull(replay.Hit, "막힌 것이 아니므로 막은 면이 없다.");
        }

        // ── 깔때기와 재생이 같은 답을 내나 ──────────────────────────────────

        [Test]
        public void 깔때기가_통과라_한_조작열은_재생해도_통과한다()
        {
            //  합성 상황만 쓴다 — 실제 맵을 쓰면 씬에 의존해 규칙이 아니라 맵을 재게 된다.
            var gates = new[] { TallGate(), LowGate(), Gate(Window(0f, 4f), Window(6f, 12f)) };
            int passes = 0;
            for (int g = 0; g < gates.Length; g++)
            {
                for (float y = 0f; y <= 11f; y += 0.5f)
                {
                    for (float vy = -MaxFallSpeed; vy <= FlapImpulse; vy += 4f)
                    {
                        List<bool> flaps;
                        bool rolls = GateFunnelRule.TryRolls(y, vy, gates[g], NoRunout, Kernel, out flaps);
                        FunnelReplay replay = GateFunnelReplayRule.Replay(y, vy, flaps, gates[g], Kernel);
                        Assert.AreEqual(rolls, replay.Passed,
                                        $"관문 {g}, 진입 y={y} vy={vy} — 깔때기와 재생이 갈렸다"
                                        + $" (조작열 {flaps.Count}틱).");
                        if (rolls)
                        {
                            passes++;
                        }
                    }
                }
            }
            Assert.Greater(passes, 0, "통과가 하나도 없으면 이 성질은 아무것도 안 지킨다.");
        }

        [Test]
        public void 못_지나는_진입에는_조작열이_없다()
        {
            List<bool> flaps;
            //  창 바닥 아래에서 출발하면 어떤 조작으로도 못 지난다.
            Assert.IsFalse(GateFunnelRule.TryRolls(-5f, 0f, TallGate(), NoRunout, Kernel, out flaps));
            Assert.AreEqual(0, flaps.Count, "못 지났으면 보여 줄 조작열도 없다.");
        }

        [Test]
        public void 조작열을_내는_굴려보기는_안_내는_것과_같은_답이다()
        {
            var gate = Gate(Window(0f, 4f), Window(6f, 12f));
            for (float y = 0f; y <= 11f; y += 0.25f)
            {
                for (float vy = -MaxFallSpeed; vy <= FlapImpulse; vy += 2f)
                {
                    List<bool> flaps;
                    Assert.AreEqual(GateFunnelRule.Rolls(y, vy, gate, NoRunout, Kernel),
                                    GateFunnelRule.TryRolls(y, vy, gate, NoRunout, Kernel, out flaps),
                                    $"진입 y={y} vy={vy}");
                }
            }
        }
    }
}
