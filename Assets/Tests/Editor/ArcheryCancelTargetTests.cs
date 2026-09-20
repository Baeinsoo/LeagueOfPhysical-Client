using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    using Pad = LOP.UI.ArcheryPadViewModel;

    //  내려놓기 자리는 **누른 자리 기준**이다(화면 아래 절대 위치가 아니라).
    //
    //  <para>이게 성립하려면 딱 하나가 지켜져야 한다 — **조준이 실제로 쓰는 범위 밖**에 있어야
    //  한다는 것. 당긴 상태(화각 22°)에서 조준이 도는 거리는 홀드오버 1.12° + 미세조정 ±3°
    //  정도라 화면 세로의 14%를 안 넘는다. 원의 안쪽 가장자리가 그보다 가까우면 **겨누다 취소된다.**</para>
    //
    //  <para>옛 띠는 높이가 코드와 USS 두 곳에 적혀 있어 어긋날 수 있었고, 그걸 대조하는 시험이
    //  따로 있었다. 지금은 화면이 그리는 원을 ViewModel의 중심·반지름에서 그대로 받아 가므로
    //  값이 하나뿐이다 — 대신 **그 중심과 판정이 같은 원인지**를 아래에서 잰다.</para>
    public class ArcheryCancelTargetTests
    {
        //  화면 세로를 1로 본 좌표. y는 양수가 위 — AimBy와 같은 관습이다.
        static Vector2 Press => new Vector2(1.5f, 0.55f);

        //  당긴 상태에서 조준이 실제로 쓰는 범위. 3도 ÷ 화각 22도.
        const float AimReach = 3f / 22f;

        [Test]
        public void 누른_자리_아래_취소_자리에서_떼면_취소다()
        {
            var center = Pad.CancelTargetCenter(Press);

            Assert.IsTrue(Pad.InsideCancelTarget(Press, center),
                "원 한가운데인데 취소로 안 친다");
        }

        //  이게 이 설계의 계약이다. 깨지면 "겨누다 말고 취소되는" 상태로 돌아간다.
        [Test]
        public void 조준이_쓰는_범위_안에서는_취소가_아니다()
        {
            var aimedDown = Press - new Vector2(0f, AimReach);

            Assert.IsFalse(Pad.InsideCancelTarget(Press, aimedDown),
                "아래로 3도 겨눈 자리가 취소 원 안에 들어왔다 — 겨누다 취소된다");
        }

        //  아래로 많이 내려가도 **옆으로 벗어나 있으면** 취소가 아니다. 띠(가로 전체)와
        //  원의 차이가 여기다 — 옆으로 겨눈 채 낮게 조준하는 경우를 살려 준다.
        [Test]
        public void 깊이가_같아도_옆으로_벗어나면_취소가_아니다()
        {
            var center = Pad.CancelTargetCenter(Press);
            var sideways = center + new Vector2(Pad.CancelRadiusInHeights * 2f, 0f);

            Assert.IsFalse(Pad.InsideCancelTarget(Press, sideways),
                "원 밖인데 취소로 친다 — 가로 판정이 빠졌다");
        }

        //  화면이 그리는 원(중심 + 반지름)과 판정하는 원이 같은 원이어야 한다.
        //  달라지면 "분명히 원 밖에서 뗐는데 안 나간다"가 되고, 화면에 아무 표시가 없어
        //  원인을 짚기 어렵다.
        [Test]
        public void 그리는_원과_판정하는_원이_같다()
        {
            var center = Pad.CancelTargetCenter(Press);
            float r = Pad.CancelRadiusInHeights;

            Assert.IsTrue(Pad.InsideCancelTarget(Press, center + new Vector2(r * 0.98f, 0f)),
                "반지름 안쪽인데 취소가 아니다 — 판정 원이 그리는 원보다 작다");
            Assert.IsFalse(Pad.InsideCancelTarget(Press, center + new Vector2(r * 1.02f, 0f)),
                "반지름 바깥인데 취소다 — 판정 원이 그리는 원보다 크다");
        }

        //  누른 자리 기준이라는 것의 뜻 — 화면 어디서 눌렀든 **엄지가 갈 거리가 같다.**
        //  옛 띠는 위쪽에서 누르면 멀고 아래쪽에서 누르면 0이었다.
        [Test]
        public void 어디서_눌러도_취소까지_거리가_같다()
        {
            var high = new Vector2(1.2f, 0.85f);
            var low = new Vector2(1.8f, 0.25f);

            float fromHigh = (Pad.CancelTargetCenter(high) - high).magnitude;
            float fromLow = (Pad.CancelTargetCenter(low) - low).magnitude;

            Assert.AreEqual(fromHigh, fromLow, 1e-5f,
                "누른 자리에 따라 취소까지 거리가 달라진다 — 화면 기준이 섞여 있다");
        }

        //  취소 자리는 누른 자리보다 **아래**다. 위로 두면 홀드오버(늘 위로 겨눈다)와 정면충돌한다.
        [Test]
        public void 취소_자리는_누른_자리보다_아래다()
        {
            var center = Pad.CancelTargetCenter(Press);

            Assert.Less(center.y, Press.y, "취소 자리가 누른 자리보다 위에 있다");
        }
    }
}
