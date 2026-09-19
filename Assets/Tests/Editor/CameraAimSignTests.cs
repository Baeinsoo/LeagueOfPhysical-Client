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
    }
}
