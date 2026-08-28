using NUnit.Framework;
using Fixture = LOP.Tests.FlappyCorrectionFixture;

namespace LOP.Tests
{
    public class FlappyServerCorrectionHandlerTests
    {
        [Test]
        public void 둘_다_안_멈췄으면_맞는다()
        {
            Assert.IsTrue(FlappyServerCorrectionHandler.StunMatches(
                predictedStun: 0f, predictedInvuln: 0f, snapStunEnd: 0, snapInvulnEnd: 0, tick: 10));
        }

        [Test]
        public void 서버는_멈췄는데_내가_안_멈췄으면_틀리다()
        {
            Assert.IsFalse(FlappyServerCorrectionHandler.StunMatches(
                predictedStun: 0f, predictedInvuln: 0f, snapStunEnd: 50, snapInvulnEnd: 0, tick: 10));
        }

        [Test]
        public void 내가_멈췄는데_서버는_안_멈췄으면_틀리다()
        {
            Assert.IsFalse(FlappyServerCorrectionHandler.StunMatches(
                predictedStun: 0.4f, predictedInvuln: 0f, snapStunEnd: 0, snapInvulnEnd: 0, tick: 10));
        }

        [Test]
        public void 무적_여부가_달라도_틀리다()
        {
            Assert.IsFalse(FlappyServerCorrectionHandler.StunMatches(
                predictedStun: 0f, predictedInvuln: 0f, snapStunEnd: 0, snapInvulnEnd: 30, tick: 10));
        }

        [Test]
        public void 종료틱이_이미_지났으면_안_멈춘_것이다()
        {
            //  같은 값이라도 틱이 지나면 뜻이 뒤집힌다 — 비교는 반드시 같은 시점 기준이어야 한다.
            Assert.IsTrue(FlappyServerCorrectionHandler.StunMatches(
                predictedStun: 0f, predictedInvuln: 0f, snapStunEnd: 50, snapInvulnEnd: 0, tick: 50));
        }

        // ApplyAuthoritative가 얼마나 오래 얼릴지 정하는 계산이다 — 부호와 경계를 틀리면
        // "멈춰야 하는데 안 멈춘다"가 아니라 "멈추는 시간 자체가 잘못된다"로 새서 더 눈에 띈다.

        [Test]
        public void 남은_틱만큼_초로_바뀐다()
        {
            // 끝나는 틱이 지금 틱보다 10틱 앞이고 한 틱이 0.02초면 정확히 0.2초가 남아야 한다.
            Assert.AreEqual(0.2f, FlappyServerCorrectionHandler.RemainingSeconds(
                endTick: 20, tick: 10, interval: 0.02f), 1e-4f);
        }

        [Test]
        public void 종료틱과_지금틱이_같으면_0이다()
        {
            // 경계값 — 막 끝난 순간이지 한 틱 더 남은 게 아니다. 여기서 하나 밀리면(한 틱분 초가
            // 남는 쪽으로) 다음 틱에도 계속 얼어 있는 것처럼 보인다.
            Assert.AreEqual(0f, FlappyServerCorrectionHandler.RemainingSeconds(
                endTick: 10, tick: 10, interval: 0.02f), 1e-4f);
        }

        [Test]
        public void 종료틱이_지금틱보다_과거면_음수_대신_0이다()
        {
            // 경계를 넘어 이미 지난 경우 — 뺄셈 결과가 음수가 나올 수 있는데, 그걸 그대로
            // 돌려주면 "음수 초만큼 얼어 있다"는 값이 새 나간다. 클램프로 막는다.
            Assert.AreEqual(0f, FlappyServerCorrectionHandler.RemainingSeconds(
                endTick: 5, tick: 10, interval: 0.02f), 1e-4f);
        }

        [Test]
        public void 종료틱이_0이면_안_멈춘_것이라_0이다()
        {
            // 0은 와이어 계약상 "그 상태가 아님"이다 — 과거 케이스와 같은 코드 경로를 타지만,
            // 그 사실 자체가 계약을 지킨다는 걸 보여 주려 별도로 남겨 둔다.
            Assert.AreEqual(0f, FlappyServerCorrectionHandler.RemainingSeconds(
                endTick: 0, tick: 10, interval: 0.02f), 1e-4f);
        }

        //  ── 여기부터는 static 헬퍼가 아니라 핸들러 자체(Matches / ApplyAuthoritative)를 돌린다.
        //  헬퍼만 검사하면 "계산은 맞는데 부르는 쪽이 엉뚱한 값을 넘긴다"를 못 잡는다. 실제로
        //  ApplyAuthoritative가 기준 틱을 snap.tick 대신 클라의 현재 틱으로 바꿔도 이 파일은
        //  통째로 초록이었다 — 아래 테스트들이 그 구멍을 막는다.
        //  (그 실수 자체는 이제 구조적으로 불가능하다: 핸들러가 러너를 안 받아 클라의 현재 틱에
        //   손이 닿지 않는다. 그래도 "스냅의 틱을 기준으로 쓴다"는 계약은 여기서 계속 지킨다.)

        [Test]
        public void 저장이_없는_틱은_비교하지_않고_통과시킨다()
        {
            //  앵커 틱 기록이 없으면 "그때 내가 뭘 예측했는지"를 모른다. 모르는 걸 불일치로 단정하면
            //  매치 시작 직후처럼 기록이 아직 없는 구간에서 매 틱 되돌리게 된다.
            var handler = Fixture.Handler(Fixture.NeverHit, out _, out var bird);

            //  SaveState를 한 번도 부르지 않았다 → 그 틱 기록이 없다.
            Assert.IsTrue(handler.Matches(10, Fixture.Snap(bird.Id, tick: 10, stunEndTick: 50)));
        }

        [Test]
        public void 서버는_멈췄다는데_내_예측은_안_멈췄으면_되돌린다()
        {
            var handler = Fixture.Handler(Fixture.NeverHit, out var world, out var bird);
            world.Tick(10, 0.02f);
            world.SaveState(10);
            Assert.AreEqual(0f, bird.Get<FlappyStun>().StunRemaining, 1e-4f);   // 예측: 안 멈춤

            Assert.IsFalse(handler.Matches(10, Fixture.Snap(bird.Id, tick: 10, stunEndTick: 50)));
        }

        [Test]
        public void 내_예측은_멈췄는데_서버는_안_멈췄다면_되돌린다()
        {
            var handler = Fixture.Handler(Fixture.AlwaysHit, out var world, out var bird);
            world.Tick(10, 0.02f);
            world.SaveState(10);
            Assert.Greater(bird.Get<FlappyStun>().StunRemaining, 0f);   // 예측: 멈춤

            Assert.IsFalse(handler.Matches(10, Fixture.Snap(bird.Id, tick: 10, stunEndTick: 0)));
        }

        [Test]
        public void 둘_다_멈췄다고_보면_되돌리지_않는다()
        {
            var handler = Fixture.Handler(Fixture.AlwaysHit, out var world, out var bird);
            world.Tick(10, 0.02f);
            world.SaveState(10);

            Assert.IsTrue(handler.Matches(10, Fixture.Snap(bird.Id, tick: 10, stunEndTick: 50)));
        }

        [Test]
        public void 남은_시간은_스냅_자신의_틱에서_되계산한다()
        {
            //  기준 시점은 스냅이 찍힌 틱이어야 한다. 클라 시계는 서버보다 ~9틱 앞서 달리므로,
            //  기준을 클라의 현재 틱(=109)으로 잡으면 같은 스냅이 0.8초가 아니라 0.62초로 읽힌다
            //  — 스턴이 매번 0.18초씩 짧아지는데 화면상으론 "가끔 덜 멈춘다" 정도로만 보인다.
            var handler = Fixture.Handler(Fixture.NeverHit, out _, out var bird);

            handler.ApplyAuthoritative(bird, Fixture.Snap(bird.Id, tick: FlappyCorrectionFixture.SnapTick, stunEndTick: 140, invulnEndTick: 170), FlappyCorrectionFixture.TickInterval);

            //  (140 − 100) × 0.02 = 0.8초.
            Assert.AreEqual(0.8f, bird.Get<FlappyStun>().StunRemaining, 1e-4f);
            //  (170 − 100) × 0.02 = 1.4초.
            Assert.AreEqual(1.4f, bird.Get<FlappyStun>().InvulnRemaining, 1e-4f);
        }

        [Test]
        public void 서버가_안_멈췄다고_하면_예측해_둔_스턴을_지운다()
        {
            //  덮어쓰기가 "0으로 되돌리기"까지 해야 한다 — 안 그러면 서버가 푼 스턴이 클라에만 남는다.
            var handler = Fixture.Handler(Fixture.NeverHit, out _, out var bird);
            bird.Get<FlappyStun>().StunRemaining = 0.5f;
            bird.Get<FlappyStun>().InvulnRemaining = 0.5f;

            handler.ApplyAuthoritative(bird, Fixture.Snap(bird.Id, tick: FlappyCorrectionFixture.SnapTick, stunEndTick: 0, invulnEndTick: 0), FlappyCorrectionFixture.TickInterval);

            Assert.AreEqual(0f, bird.Get<FlappyStun>().StunRemaining, 1e-4f);
            Assert.AreEqual(0f, bird.Get<FlappyStun>().InvulnRemaining, 1e-4f);
        }

        [Test]
        public void 스턴이_없는_엔티티에는_아무_일도_안_한다()
        {
            //  FlapWang처럼 FlappyStun을 안 붙이는 캐릭터가 같은 배치에 섞여 와도 터지면 안 된다.
            var handler = Fixture.Handler(Fixture.NeverHit, out _, out _);
            var plain = new GameFramework.World.Entity("no-stun");

            Assert.DoesNotThrow(() => handler.ApplyAuthoritative(plain, Fixture.Snap("no-stun", tick: FlappyCorrectionFixture.SnapTick, stunEndTick: 140), FlappyCorrectionFixture.TickInterval));
        }
    }
}
