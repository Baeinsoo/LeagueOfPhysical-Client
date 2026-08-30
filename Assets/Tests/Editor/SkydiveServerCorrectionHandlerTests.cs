using NUnit.Framework;
using Fixture = LOP.Tests.SkydiveCorrectionFixture;

namespace LOP.Tests
{
    public class SkydiveServerCorrectionHandlerTests
    {
        //  헬퍼(TryGetSavedPosture)만 검사하면 "값은 맞는데 핸들러가 엉뚱하게 비교한다"를 못 잡는다.
        //  Flappy가 FlappyCorrectionFixture로 핸들러 자체를 돌리는 것과 같은 이유로, 여기서도
        //  실물 SkydiveWorld를 굴려 Matches/ApplyAuthoritative를 직접 테스트한다.

        [Test]
        public void 저장이_없는_틱은_비교하지_않고_통과시킨다()
        {
            //  앵커 틱 기록이 없으면 "그때 내가 뭘 예측했는지"를 모른다. 모르는 걸 불일치로
            //  단정하면 매치 시작 직후처럼 기록이 아직 없는 구간에서 매 틱 되돌리게 된다.
            var handler = Fixture.Handler(out _, out var diver);

            //  SaveState를 한 번도 부르지 않았다 → 그 틱 기록이 없다.
            Assert.IsTrue(handler.Matches(10, Fixture.Snap(diver.Id, tick: 10, postureAxis: 0.9f)));
        }

        [Test]
        public void 축_차이가_허용범위_안이면_맞는다()
        {
            var handler = Fixture.Handler(out var world, out var diver);
            diver.Get<InputBuffer>().Current = new InputCommand { Posture = 1f };
            world.Tick(0, Fixture.TickInterval);
            world.SaveState(0);
            float predictedAxis = diver.Get<Posture>().Axis;   // PostureRate=4 → 0.08

            //  0.05 차이 — 한 틱치(0.08)보다도 작다.
            Assert.IsTrue(handler.Matches(0, Fixture.Snap(diver.Id, tick: 0,
                postureAxis: predictedAxis + 0.05f, gliding: false)));
        }

        [Test]
        public void 축_차이가_허용범위_밖이면_틀리다()
        {
            var handler = Fixture.Handler(out var world, out var diver);
            diver.Get<InputBuffer>().Current = new InputCommand { Posture = 1f };
            world.Tick(0, Fixture.TickInterval);
            world.SaveState(0);
            float predictedAxis = diver.Get<Posture>().Axis;

            //  0.15 차이 — 허용치(0.1)를 넘는다.
            Assert.IsFalse(handler.Matches(0, Fixture.Snap(diver.Id, tick: 0,
                postureAxis: predictedAxis + 0.15f, gliding: false)));
        }

        [Test]
        public void Gliding이_다르면_축이_같아도_틀리다()
        {
            //  Gliding은 켜짐/꺼짐 자체가 다른 항력 계수라, 축이 아무리 가까워도 봐주지 않는다.
            var handler = Fixture.Handler(out var world, out var diver);
            world.Tick(0, Fixture.TickInterval);
            world.SaveState(0);
            float predictedAxis = diver.Get<Posture>().Axis;   // 입력 없음 → 0

            Assert.IsFalse(handler.Matches(0, Fixture.Snap(diver.Id, tick: 0,
                postureAxis: predictedAxis, gliding: true)));   // 예측은 false, 스냅은 true
        }

        [Test]
        public void 스태미나가_많이_달라도_Matches는_틀리지_않는다()
        {
            //  스태미나는 연속값이라 넣으면 거의 매 틱 되돌린다 — Matches는 축·활공만 본다.
            var handler = Fixture.Handler(out var world, out var diver);
            world.Tick(0, Fixture.TickInterval);
            world.SaveState(0);
            float predictedAxis = diver.Get<Posture>().Axis;

            Assert.IsTrue(handler.Matches(0, Fixture.Snap(diver.Id, tick: 0,
                postureAxis: predictedAxis, gliding: false, stamina: 999f)));
        }

        [Test]
        public void 비상_잔여시간이_달라도_Matches는_틀리지_않는다()
        {
            //  emergencyRemaining도 스태미나와 같은 이유로 Matches에서 제외한다.
            var handler = Fixture.Handler(out var world, out var diver);
            world.Tick(0, Fixture.TickInterval);
            world.SaveState(0);
            float predictedAxis = diver.Get<Posture>().Axis;

            Assert.IsTrue(handler.Matches(0, Fixture.Snap(diver.Id, tick: 0,
                postureAxis: predictedAxis, gliding: false, emergencyRemaining: 999f)));
        }

        [Test]
        public void ApplyAuthoritative가_자세_스태미나_비상잔여시간을_스냅값으로_덮는다()
        {
            var handler = Fixture.Handler(out _, out var diver);

            handler.ApplyAuthoritative(diver, Fixture.Snap(diver.Id, tick: Fixture.SnapTick,
                postureAxis: 0.7f, gliding: true, stamina: 42f, emergencyRemaining: 0.35f), Fixture.TickInterval);

            Assert.AreEqual(0.7f, diver.Get<Posture>().Axis, 1e-4f);
            Assert.IsTrue(diver.Get<Posture>().Gliding);
            Assert.AreEqual(42f, diver.Get<Stamina>().Current, 1e-4f);
            //  비상 창의 남은 초 — 남에게는 InputBuffer가 없어 TryStartGlide가 절대 안 불리므로
            //  이 값이 서버 스냅으로만 채워진다. 안 덮으면 다음 틱 StaminaSystem.Tick이
            //  "잔고 0, 구제 창도 0"으로 보고 곧바로 접어 버린다(리뷰가 잡은 버그).
            Assert.AreEqual(0.35f, diver.Get<Stamina>().EmergencyRemaining, 1e-4f);
        }

        [Test]
        public void ApplyAuthoritative는_EmergencyUsed를_건드리지_않는다()
        {
            //  남에게는 TryStartGlide가 안 불려 EmergencyUsed가 예측에 영향을 줄 길이 없다 —
            //  그래서 와이어에도 안 싣는다. 이 테스트는 "안 실었다"가 "로컬 값이 조용히
            //  덮인다"로 새지 않았는지를 확인한다.
            var handler = Fixture.Handler(out _, out var diver);
            diver.Get<Stamina>().EmergencyUsed = true;

            handler.ApplyAuthoritative(diver, Fixture.Snap(diver.Id, tick: Fixture.SnapTick,
                postureAxis: 0.5f, gliding: true, stamina: 0f, emergencyRemaining: 0.9f), Fixture.TickInterval);

            Assert.IsTrue(diver.Get<Stamina>().EmergencyUsed, "와이어에 없는 필드라 로컬 값이 그대로 유지돼야 한다");
        }

        [Test]
        public void 컴포넌트가_없는_엔티티에는_아무_일도_안_한다()
        {
            //  Posture/Stamina를 안 붙이는 캐릭터가 같은 배치에 섞여 와도 터지면 안 된다.
            var handler = Fixture.Handler(out _, out _);
            var plain = new GameFramework.World.Entity("no-posture");

            Assert.DoesNotThrow(() => handler.ApplyAuthoritative(plain,
                Fixture.Snap("no-posture", tick: Fixture.SnapTick, postureAxis: 0.5f, gliding: true),
                Fixture.TickInterval));
        }
    }
}
