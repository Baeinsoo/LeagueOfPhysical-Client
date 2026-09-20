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

        //  가로 16:9 — 세로를 1로 보면 가로가 이만큼이다.
        const float Wide = 16f / 9f;

        [Test]
        public void 누른_자리_아래_취소_자리에서_떼면_취소다()
        {
            var center = Pad.CancelTargetCenter(Press, Wide);

            Assert.IsTrue(Pad.InsideCancelTarget(Press, center, Wide),
                "원 한가운데인데 취소로 안 친다");
        }

        //  이게 이 설계의 계약이다. 깨지면 "겨누다 말고 취소되는" 상태로 돌아간다.
        [Test]
        public void 조준이_쓰는_범위_안에서는_취소가_아니다()
        {
            var aimedDown = Press - new Vector2(0f, AimReach);

            Assert.IsFalse(Pad.InsideCancelTarget(Press, aimedDown, Wide),
                "아래로 3도 겨눈 자리가 취소 원 안에 들어왔다 — 겨누다 취소된다");
        }

        //  아래로 많이 내려가도 **옆으로 벗어나 있으면** 취소가 아니다. 띠(가로 전체)와
        //  원의 차이가 여기다 — 옆으로 겨눈 채 낮게 조준하는 경우를 살려 준다.
        [Test]
        public void 깊이가_같아도_옆으로_벗어나면_취소가_아니다()
        {
            var center = Pad.CancelTargetCenter(Press, Wide);
            var sideways = center + new Vector2(Pad.CancelRadiusInHeights * 2f, 0f);

            Assert.IsFalse(Pad.InsideCancelTarget(Press, sideways, Wide),
                "원 밖인데 취소로 친다 — 가로 판정이 빠졌다");
        }

        //  화면이 그리는 원(중심 + 반지름)과 판정하는 원이 같은 원이어야 한다.
        //  달라지면 "분명히 원 밖에서 뗐는데 안 나간다"가 되고, 화면에 아무 표시가 없어
        //  원인을 짚기 어렵다.
        [Test]
        public void 그리는_원과_판정하는_원이_같다()
        {
            var center = Pad.CancelTargetCenter(Press, Wide);
            float r = Pad.CancelRadiusInHeights;

            Assert.IsTrue(Pad.InsideCancelTarget(Press, center + new Vector2(r * 0.98f, 0f), Wide),
                "반지름 안쪽인데 취소가 아니다 — 판정 원이 그리는 원보다 작다");
            Assert.IsFalse(Pad.InsideCancelTarget(Press, center + new Vector2(r * 1.02f, 0f), Wide),
                "반지름 바깥인데 취소다 — 판정 원이 그리는 원보다 크다");
        }

        //  누른 자리 기준이라는 것의 뜻 — 화면 어디서 눌렀든 **엄지가 갈 거리가 같다.**
        //  옛 띠는 위쪽에서 누르면 멀고 아래쪽에서 누르면 0이었다.
        //  (화면 가장자리에서는 원을 안쪽으로 미느라 조금 늘어난다 — 줄지만 않으면 되고,
        //   그건 위의 sweep 시험이 지킨다.)
        [Test]
        public void 화면_안쪽이면_어디서_눌러도_취소까지_거리가_같다()
        {
            var high = new Vector2(1.2f, 0.85f);
            var low = new Vector2(0.6f, 0.25f);

            float fromHigh = (Pad.CancelTargetCenter(high, Wide) - high).magnitude;
            float fromLow = (Pad.CancelTargetCenter(low, Wide) - low).magnitude;

            Assert.AreEqual(fromHigh, fromLow, 1e-5f,
                "누른 자리에 따라 취소까지 거리가 달라진다 — 화면 기준이 섞여 있다");
        }

        //  화면 어디를 눌러도 원이 화면을 벗어나면 안 된다. 가로로 쥐면 엄지가 낮게 쉬어서
        //  아래쪽을 누르는 일이 잦은데, 거기서 "누른 자리 −31%"는 화면 밖이다.
        //  그리고 벗어나지 않게 옮기더라도 **거리는 줄어들면 안 된다** — 줄면 조준이 쓰는
        //  범위로 파고들어 겨누다 취소된다.
        [Test]
        public void 화면_어디를_눌러도_원이_화면_안에_있고_거리가_안_줄어든다()
        {
            float r = Pad.CancelRadiusInHeights;

            for (int i = 0; i <= 100; i++)
            {
                for (int j = 0; j <= 20; j++)
                {
                    var press = new Vector2(Wide * j / 20f, i / 100f);
                    var center = Pad.CancelTargetCenter(press, Wide);

                    Assert.GreaterOrEqual(center.y, r - 1e-4f, "원이 화면 아래로 나갔다: " + press);
                    Assert.LessOrEqual(center.y, 1f - r + 1e-4f, "원이 화면 위로 나갔다: " + press);
                    Assert.GreaterOrEqual(center.x, r - 1e-4f, "원이 화면 왼쪽으로 나갔다: " + press);
                    Assert.LessOrEqual(center.x, Wide - r + 1e-4f, "원이 화면 오른쪽으로 나갔다: " + press);

                    Assert.GreaterOrEqual((center - press).magnitude,
                        Pad.CancelBelowPressInHeights - 1e-4f,
                        "원이 누른 자리에 더 가까워졌다 — 겨누다 취소된다: " + press);
                }
            }
        }

        //  기본은 **아래**다 — 아래에 자리가 있는 한 위로 올라가면 안 된다.
        //  (아래에 자리가 없을 때만 위로 뒤집는다. 그 경우는 위 시험이 지킨다.)
        [Test]
        public void 아래에_자리가_있으면_아래에_선다()
        {
            var center = Pad.CancelTargetCenter(Press, Wide);

            Assert.Less(center.y, Press.y, "아래에 자리가 넉넉한데 위에 섰다");
        }
    }
}
