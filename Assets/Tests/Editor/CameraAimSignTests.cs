using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    //  조준의 부호 계약을 못 박는다. 2026-09-19에 세로만 반대로 도는 일이 있었다 — 가로는
    //  손가락을 따라가는데 세로는 세상이 따라가는, 어느 게임에도 없는 조합이었다. 원인은
    //  패널 좌표(아래로 갈수록 +y)를 "양수 = 위"인 AimBy에 그대로 넣은 것이었고, 옛 경로에서
    //  그대로 옮겨진 탓에 "부호가 이전과 같은가"만 봐서는 안 잡혔다.
    //
    //  화면 쪽 변환(패널 좌표 뒤집기)은 포인터 이벤트가 필요해 EditMode로 못 잰다. 대신 그
    //  변환이 **의지하는 계약**을 여기서 잰다 — AimBy의 y 양수가 위라는 것.
    public class CameraAimSignTests
    {
        static CameraController NewCamera(out GameObject host)
        {
            host = new GameObject("aim-sign-test");
            return host.AddComponent<CameraController>();
        }

        [Test]
        public void AimBy의_y_양수는_위를_본다()
        {
            var camera = NewCamera(out var host);
            try
            {
                float before = camera.Pitch;
                camera.AimBy(new Vector2(0f, 1f));

                //  유니티의 x 회전은 양수가 **아래**다 — 위를 보라는 뜻이면 값이 줄어야 한다.
                Assert.Less(camera.Pitch, before,
                    "y가 양수인데 아래를 본다 — 화면이 패널 좌표를 안 뒤집고 넣으면 이렇게 된다");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AimBy의_x_양수는_오른쪽을_본다()
        {
            var camera = NewCamera(out var host);
            try
            {
                float before = camera.Yaw;
                camera.AimBy(new Vector2(1f, 0f));

                Assert.Greater(camera.Yaw, before, "x가 양수인데 왼쪽을 본다");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        //  두 축이 같은 관습을 쓰는지가 핵심이다 — 하나만 뒤집혀 있으면 조작이 성립하지 않는다.
        [Test]
        public void 두_축이_같은_관습을_쓴다()
        {
            var camera = NewCamera(out var host);
            try
            {
                camera.AimBy(new Vector2(1f, 1f));

                Assert.Greater(camera.Yaw, 0f, "양수 x가 오른쪽이 아니다");
                Assert.Less(camera.Pitch, 0f, "양수 y가 위가 아니다 — 세로만 반대로 돈다");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    
        //  ArcheryPadViewModel.UpdateAim이 **실제로 부르는** 함수를 잰다. 활을 내려놓을 때
        //  조준을 들기 직전 자리로 되돌리는 계산이고, 부호를 한 번만 헷갈려도 되돌리기는커녕
        //  두 배로 멀어진다 — 취소할 때마다 시야가 아래로 튀는 모양이 된다.
        [Test]
        public void 되돌리기가_목표_자세에_수렴한다()
        {
            var camera = NewCamera(out var host);
            try
            {
                const float targetYaw = 0f, targetPitch = 0f;
                camera.AimBy(new Vector2(12f, -7f));   // 조준이 오른쪽·아래로 끌려간 상황
                Assert.Greater(Mathf.Abs(camera.Pitch - targetPitch), 1f, "준비 자체가 안 됐다");

                //  한 프레임씩 60번 — UpdateAim이 매 프레임 하는 것과 같다.
                for (int i = 0; i < 60; i++)
                {
                    camera.AimBy(CameraController.StepToward(
                        camera.Yaw, camera.Pitch, targetYaw, targetPitch, 0.2f));
                }

                Assert.AreEqual(targetPitch, camera.Pitch, 1e-2f,
                    "위아래가 목표로 안 온다 — 부호가 뒤집혀 있으면 오히려 멀어진다");
                Assert.AreEqual(targetYaw, Mathf.DeltaAngle(0f, camera.Yaw), 1e-2f,
                    "좌우가 목표로 안 온다");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        //  첫 한 걸음이 **가까워지는 방향**이어야 한다. 위 시험은 60번 돌려 수렴을 보지만,
        //  부호가 맞아도 방향이 뒤집힌 변형(예: 목표와 현재를 바꿔 빼기)은 첫 걸음에서 드러난다.
        [Test]
        public void 되돌리기의_첫_걸음이_가까워지는_쪽이다()
        {
            var camera = NewCamera(out var host);
            try
            {
                camera.AimBy(new Vector2(0f, -7f));
                float before = Mathf.Abs(camera.Pitch - 0f);

                camera.AimBy(CameraController.StepToward(camera.Yaw, camera.Pitch, 0f, 0f, 0.3f));

                Assert.Less(Mathf.Abs(camera.Pitch - 0f), before,
                    "한 걸음 갔는데 목표에서 더 멀어졌다");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        //  좌우는 360도를 넘나든다 — 350도에서 10도로 갈 때 최단(+20도)으로 가야지
        //  그냥 빼면 반대로 340도를 돈다.
        [Test]
        public void 좌우를_되돌릴_때_먼_쪽으로_돌지_않는다()
        {
            var step = CameraController.StepToward(350f, 0f, 10f, 0f, 1f);
            Assert.AreEqual(20f, step.x, 1e-3f, "최단 방향(+20도)이 아니다");
        }
    }
}
