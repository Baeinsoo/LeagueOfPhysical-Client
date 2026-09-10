using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// <b>굴려 보기(1단계 전방탐색)가 무엇을 고르는가.</b>
    ///
    /// <para>여기서 쓰는 세계는 <b>일부러 가짜</b>다. 실제 맵과 실제 커널로 재는 것은 맵 검사
    /// 도구가 할 일이고, 이 파일이 지켜야 하는 것은 <b>고르는 규칙</b> 자체다 — "더 오래 사는
    /// 쪽 → 더 멀리 가는 쪽 → 동점이면 기반 정책". 가짜 세계라야 그 세 규칙을 하나씩 따로
    /// 갈라 물어볼 수 있다. (굴려 보기가 실제로는 진짜 커널을 쓴다는 사실은 타입이 강제한다 —
    /// <see cref="IRolloutWorld{TState}"/>가 물리를 갖고 있지 않으므로 부르는 쪽이 넘긴 것을
    /// 그대로 쓸 수밖에 없다.)</para>
    /// </summary>
    public class BotRolloutTests
    {
        //  "사실상 같은 거리"의 폭 — 실제 도구가 쓰는 값(몸 지름 0.45×2)과 같다.
        const float SameReach = 0.9f;

        // ── 세로 속도가 있는 가짜 세계 ────────────────────────────────────────
        //  진짜 플래피처럼 <b>날갯짓이 세로 속도를 덮어쓴다</b>(크기가 하나뿐인 날갯짓).
        //  이 성질이 있어야 두 갈래가 한 틱 뒤에 다시 합쳐지지 않는다 — 높이만 오르내리는
        //  세계에서는 어떤 선택을 해도 몇 틱 만에 같은 궤도로 수렴해서 전방탐색을 잴 수 없다.
        struct Bird
        {
            public int Tick;
            public int Height;
            public int Rise;   // 세로 속도
        }

        sealed class World : IRolloutWorld<Bird>
        {
            //  막힌 칸 (틱, 높이). 이 집합이 곧 지형이다.
            public readonly HashSet<(int, int)> Blocked = new HashSet<(int, int)>();
            public int DecideCalls;
            public int AdvanceCalls;

            //  기반 정책: "가만두면 다음 틱에 바닥 밑으로 간다" 싶을 때만 누른다. 바닥만 보는
            //  탐욕 규칙이라, 앞에 무엇이 있는지는 전혀 모른다 — 굴려 보기가 메워야 할 바로 그 눈이다.
            public BotDecision Decide(in Bird s)
            {
                DecideCalls++;
                bool wants = s.Height + (s.Rise - 1) < 0;
                return new BotDecision(flap: wants, gapFound: true, wantsFlap: wants, ceilingSafe: true);
            }

            public Bird Advance(in Bird s, bool flap)
            {
                AdvanceCalls++;
                int rise = flap ? 3 : s.Rise - 1;
                return new Bird { Tick = s.Tick + 1, Height = s.Height + rise, Rise = rise };
            }

            public bool Touched(in Bird s) => s.Height < 0 || Blocked.Contains((s.Tick, s.Height));
            public bool Finished(in Bird s) => false;
            public float ForwardX(in Bird s) => s.Tick;
        }

        //  기반 정책이 이 상태에서 무엇을 하려 했는지. 굴려 보기는 이 값을 받아서 시작한다.
        static BotDecision BaseDecisionAt(World world, Bird start)
        {
            BotDecision d = world.Decide(start);
            world.DecideCalls = 0;
            return d;
        }

        [Test]
        public void 기반_정책이_열_틱_뒤에_죽는_길을_고르면_굴려_보기가_뒤집는다()
        {
            var world = new World();
            var start = new Bird { Tick = 0, Height = 6, Rise = 0 };

            //  이 자리에서 기반 정책은 "안 누른다"고 한다(바닥까지 아직 여유가 있다).
            BotDecision baseDecision = BaseDecisionAt(world, start);
            Assert.IsFalse(baseDecision.Flap, "전제가 깨졌다 — 기반 정책은 이 자리에서 안 눌러야 한다.");

            //  안 누르고 기반 정책대로 가면 궤적은 t3에 0, t10에 다시 0으로 내려온다.
            //  그 t10의 바닥 자리를 막아 둔다 — <b>열 틱 뒤</b>라 한 틱만 보는 규칙으로는
            //  절대 안 보이는 죽음이다.
            world.Blocked.Add((10, 0));

            RolloutChoice choice = BotRollout.Choose(world, start, baseDecision, horizon: 20, SameReach);

            Assert.IsTrue(choice.Flap,
                "안 누르면 열 틱 뒤에 죽는데도 기반 정책을 그대로 따랐다 — 전방탐색이 안 먹었다.");
            Assert.IsTrue(choice.Deviated, "기반 정책과 다른 선택인데 뒤집었다고 표시하지 않았다.");
            Assert.IsTrue(choice.RolledOut, "굴려서 정했는데 안 굴렸다고 표시했다.");
        }

        [Test]
        public void 앞이_비어_있으면_기반_정책_그대로다()
        {
            //  위 테스트의 짝 — 막힌 칸을 빼면 두 갈래가 다 살아남고, 그때는 기반 정책을 따른다.
            //  이게 없으면 위 테스트는 "굴려 보기가 늘 누른다"로도 초록이 된다.
            var world = new World();
            var start = new Bird { Tick = 0, Height = 6, Rise = 0 };
            BotDecision baseDecision = BaseDecisionAt(world, start);

            RolloutChoice choice = BotRollout.Choose(world, start, baseDecision, horizon: 20, SameReach);

            Assert.AreEqual(baseDecision.Flap, choice.Flap,
                "두 갈래가 다 살아남았는데 기반 정책과 다르게 골랐다.");
            Assert.IsFalse(choice.Deviated);
        }

        // ── 동점 규칙 ────────────────────────────────────────────────────────
        //  결과를 표로 박아 두는 세계. 두 갈래의 "몇 틱 살았나 / 어디까지 갔나"를 직접 지정해
        //  <b>비교 규칙만</b> 따로 시험한다 — 물리로 그런 상황을 만들려 하면 무엇을 재는 테스트인지
        //  흐려진다.
        sealed class ScriptedWorld : IRolloutWorld<int>
        {
            //  상태 = 굴러간 틱 수. 첫 틱의 선택은 부호로 기억한다(+ 누름 / − 안 누름).
            public int FlapAliveTicks, CoastAliveTicks;
            public float FlapReach, CoastReach;

            public BotDecision Decide(in int s)
                //  둘째 틱부터는 아무것도 안 한다 — 이 세계에서 결과는 이미 표로 정해져 있다.
                => new BotDecision(flap: false, gapFound: true, wantsFlap: false, ceilingSafe: true);

            public int Advance(in int s, bool flap)
                => s == 0 ? (flap ? 1 : -1) : (s > 0 ? s + 1 : s - 1);

            public bool Touched(in int s)
            {
                int ticks = s > 0 ? s : -s;
                return s > 0 ? ticks > FlapAliveTicks : ticks > CoastAliveTicks;
            }

            public bool Finished(in int s) => false;
            public float ForwardX(in int s) => s > 0 ? FlapReach : CoastReach;
        }

        static BotDecision Base(bool flap)
            => new BotDecision(flap, gapFound: true, wantsFlap: flap, ceilingSafe: true);

        [TestCase(true)]
        [TestCase(false)]
        public void 완전한_동점이면_기반_정책을_따른다(bool basePolicyFlaps)
        {
            //  두 갈래가 똑같이 살고 똑같이 갔다. 여기서 흔들리면 매 틱 이유 없이 판단이
            //  바뀌어 궤적이 잡음이 된다.
            var world = new ScriptedWorld
            {
                FlapAliveTicks = 20, CoastAliveTicks = 20, FlapReach = 50f, CoastReach = 50f,
            };

            RolloutChoice choice = BotRollout.Choose(world, 0, Base(basePolicyFlaps), horizon: 20, SameReach);

            Assert.AreEqual(basePolicyFlaps, choice.Flap,
                "동점인데 기반 정책을 안 따랐다.");
            Assert.IsFalse(choice.Deviated);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void 도달_차이가_몸_지름_안이면_동점으로_보고_기반_정책을_따른다(bool basePolicyFlaps)
        {
            //  0.9m가 "사실상 같다"의 폭이다 — 그 안쪽은 부동소수 잡음이거나 한 틱 어긋난
            //  것이지 우열이 아니다. <b>딱 경계(0.9)도 동점 쪽</b>이다(> 로만 우열을 가른다).
            //  두 수를 0과 0.9f로 잡는 이유: 차이가 float으로 <i>정확히</i> 0.9f여야 경계를
            //  묻는 것이 된다(50.9f − 50f는 0.9f보다 아주 살짝 커서 다른 것을 묻게 된다).
            var world = new ScriptedWorld
            {
                FlapAliveTicks = 20, CoastAliveTicks = 20, FlapReach = 0.9f, CoastReach = 0f,
            };

            RolloutChoice choice = BotRollout.Choose(world, 0, Base(basePolicyFlaps), horizon: 20, SameReach);

            Assert.AreEqual(basePolicyFlaps, choice.Flap,
                "차이가 딱 0.9m면 동점으로 봐야 하는데 우열을 가렸다.");
        }

        [Test]
        public void 도달_차이가_몸_지름을_넘으면_더_멀리_간_쪽을_고른다()
        {
            var world = new ScriptedWorld
            {
                FlapAliveTicks = 20, CoastAliveTicks = 20, FlapReach = 1f, CoastReach = 0f,
            };

            //  기반 정책은 "안 누른다"고 했지만, 누르는 쪽이 1m 더 간다(폭 0.9m 밖).
            RolloutChoice choice = BotRollout.Choose(world, 0, Base(false), horizon: 20, SameReach);

            Assert.IsTrue(choice.Flap, "더 멀리 가는 쪽을 안 골랐다.");
            Assert.IsTrue(choice.Deviated);
        }

        [Test]
        public void 더_오래_사는_쪽이_더_멀리_가는_쪽보다_우선이다()
        {
            //  누르는 쪽이 훨씬 멀리 가지만 먼저 죽는다. 이 도구가 답하려는 질문이
            //  "지나갈 수 있는가"라서, 사는 것이 거리보다 앞선다.
            var world = new ScriptedWorld
            {
                FlapAliveTicks = 5, CoastAliveTicks = 20, FlapReach = 500f, CoastReach = 50f,
            };

            RolloutChoice choice = BotRollout.Choose(world, 0, Base(true), horizon: 20, SameReach);

            Assert.IsFalse(choice.Flap,
                "먼저 죽는 쪽이 더 멀리 간다는 이유로 뽑혔다 — 규칙 순서가 뒤집혔다.");
            Assert.IsTrue(choice.Deviated);
        }

        // ── 후보 A가 없을 때 ─────────────────────────────────────────────────

        [Test]
        public void 천장_가드가_막으면_굴리지_않고_안_누른다()
        {
            var world = new World();
            var start = new Bird { Tick = 0, Height = 6, Rise = 0 };
            //  천장 가드가 "지금 누르면 올라가다 박는다"고 했다. 누르는 쪽은 후보가 아니므로
            //  고를 것이 하나뿐이고, 그러면 굴려 볼 이유도 없다.
            var blocked = new BotDecision(flap: false, gapFound: true, wantsFlap: true, ceilingSafe: false);

            RolloutChoice choice = BotRollout.Choose(world, start, blocked, horizon: 20, SameReach);

            Assert.IsFalse(choice.Flap, "천장 가드가 막은 자리에서 눌렀다 — 가드가 약해졌다.");
            Assert.IsFalse(choice.RolledOut, "고를 게 하나뿐인데 굴렸다.");
            Assert.IsFalse(choice.Deviated);
            //  <b>가지치기가 실제로 일어났는지</b>를 호출 수로 확인한다. 이 두 단언이 없으면
            //  "굴려 놓고 결과만 버리는" 구현도 초록이 되어, 비용의 대부분을 없애는 이 가지치기가
            //  조용히 사라져도 아무도 모른다.
            Assert.AreEqual(0, world.DecideCalls, "안 굴린다고 해 놓고 기반 정책을 불렀다.");
            Assert.AreEqual(0, world.AdvanceCalls, "안 굴린다고 해 놓고 커널을 굴렸다.");
        }

        // ── 진짜 BotPilot으로 — 가드가 막은 자리에서는 절대 안 누른다 ─────────
        //  위 단언은 가짜 판단으로 물었다. 여기서는 <b>진짜 BotPilot.Decide</b>를 기반 정책으로
        //  세우고, 좁은 회랑을 실제로 날며 "가드가 막은 틱에 누른 적이 있는가"를 전수로 본다.
        //  회랑 게이트(4.4750)가 지키는 천장 훑기의 눈금을 굴려 보기가 <b>약화시키지 않는다</b>는
        //  것이 이 도구의 전제이므로, 그 전제를 코드로 못박는다.

        const float BodyRadius = 0.45f, TickSeconds = 0.02f, Gravity = 70f;
        const float MaxFallSpeed = 30f, FlapImpulse = 23f, ForwardSpeed = 11f;
        const float ScanStep = 0.1f;
        const int TicksToNear = 10;

        struct CorridorBird { public float X, Y, Vy; }

        //  자유 발 구간이 [0, width]인 직선 회랑 — BotCorridorGateTests와 같은 정의다.
        sealed class CorridorWorld : IRolloutWorld<CorridorBird>
        {
            readonly float width;
            readonly bool[] column;
            readonly ExactFreeSpaceProbe free;
            public int GuardBlockedTicks;

            public CorridorWorld(float width)
            {
                this.width = width;
                free = (x, y) => y >= 0f && y <= width;
                int cells = (int)System.Math.Round(12f / ScanStep) + 1;
                column = new bool[cells];
                for (int i = 0; i < cells; i++)
                {
                    column[i] = free(0f, i * ScanStep) == false;
                }
            }

            public BotDecision Decide(in CorridorBird s)
            {
                BotDecision d = BotPilot.Decide(column, 0f, ScanStep, s.X, s.Y, s.Vy, BodyRadius,
                                                FlapImpulse, Gravity, MaxFallSpeed, ForwardSpeed,
                                                TicksToNear, TickSeconds, free);
                if (d.CeilingSafe == false) { GuardBlockedTicks++; }
                return d;
            }

            //  실제 커널과 같은 순서 — 중력을 먼저 깎고 종단속도로 자른 뒤, 날갯짓이면 덮어쓴다.
            public CorridorBird Advance(in CorridorBird s, bool flap)
            {
                float vy = s.Vy - Gravity * TickSeconds;
                if (vy < -MaxFallSpeed) { vy = -MaxFallSpeed; }
                if (flap) { vy = FlapImpulse; }
                return new CorridorBird { X = s.X + ForwardSpeed * TickSeconds, Y = s.Y + vy * TickSeconds, Vy = vy };
            }

            public bool Touched(in CorridorBird s) => free(s.X, s.Y) == false;
            public bool Finished(in CorridorBird s) => false;
            public float ForwardX(in CorridorBird s) => s.X;
        }

        [Test]
        public void 회랑을_날며_천장_가드가_막은_틱에는_한_번도_안_누른다()
        {
            //  4.4750은 회랑 게이트가 못박은 "봇이 버티는 가장 좁은 폭"이다. 그 폭에서는 아치가
            //  들어가는 높이가 아주 얕아 가드가 자주 막으므로, 이 단언이 실제로 여러 번 걸린다.
            var world = new CorridorWorld(4.4750f);
            int guardedAndFlapped = 0;
            int checkedTicks = 0;

            for (int i = 0; i * 0.25f <= 4.4750f; i++)
            {
                var bird = new CorridorBird { X = 0f, Y = i * 0.25f, Vy = 0f };
                for (int tick = 0; tick < 250; tick++)
                {
                    BotDecision d = world.Decide(bird);
                    RolloutChoice choice = BotRollout.Choose(world, bird, d, horizon: 20, BodyRadius * 2f);
                    checkedTicks++;
                    if (d.CeilingSafe == false && choice.Flap) { guardedAndFlapped++; }
                    bird = world.Advance(bird, choice.Flap);
                    if (world.Touched(bird)) { break; }
                }
            }

            Assert.Greater(world.GuardBlockedTicks, 0,
                "가드가 한 번도 안 막았다 — 이 회랑에서는 아무것도 안 재고 있다는 뜻이다.");
            Assert.AreEqual(0, guardedAndFlapped,
                $"천장 가드가 막은 자리에서 {guardedAndFlapped}번 눌렀다 (검사한 틱 {checkedTicks}). "
                + "굴려 보기가 가드를 약화시켰다 — 회랑 게이트(4.4750)의 전제가 깨진다.");
        }
    }
}
