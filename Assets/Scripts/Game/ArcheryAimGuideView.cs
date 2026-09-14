using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 당기는 동안 화살이 그릴 길을 미리 선으로 보여 준다.
    ///
    /// <para>화살이 <b>포물선</b>으로 날아가서 화면 한가운데가 곧 맞는 자리가 아니다 — 십자선을
    /// 띄워 봐야 거짓말이 된다. 그래서 시뮬이 쓰는 바로 그 함수(<see cref="ArcheryTrajectory"/>)에
    /// 지금 조준·당김을 넣어 길을 뽑는다. <b>보이는 선이 곧 화살이 가는 길</b>이다.</para>
    ///
    /// <para>당긴 정도에 따라 속도가 달라지므로 선도 같이 길어진다 — 얼마나 당겼는지가 화면에
    /// 그대로 보인다.</para>
    /// </summary>
    public class ArcheryAimGuideView : IPostLateTickable, System.IDisposable
    {
        //  길수록 더 멀리까지 보여 주지만, 끝까지 그리면 어디를 노리는지가 오히려 안 읽힌다.
        private const float PreviewSeconds = 1.2f;

        //  선을 잇지 않고 점을 띄엄띄엄 놓는다 — 이어 그으면 굵기가 일정한 띠가 되어 화살표처럼
        //  보이고, 멀고 가까움도 안 읽힌다. 점은 거리에 따라 작아져서 깊이가 그대로 보인다.
        private const int DotCount = 30;
        private const float DotSize = 0.0042f;

        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly CameraController cameraController;

        private readonly List<GameObject> dots = new List<GameObject>();
        private Material _dotMaterial;

        public ArcheryAimGuideView(IPlayerContext playerContext,
                                   GameFramework.World.EntityRegistry entityRegistry,
                                   CameraController cameraController)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.cameraController = cameraController;
        }

        //  PostLateUpdate다 — CameraController(LateUpdate)가 이번 프레임 회전을 쓴 *뒤*에 돈다.
        //  먼저 돌면 이번 프레임 각으로 그린 점을 카메라가 직전 프레임 각으로 따라와 덜덜거린다.
        public void PostLateTick()
        {
            var aim = MyAim();
            //  임계치를 못 넘었으면 아직 시위가 안 걸린 것이다 — 쏘지도 않을 길을 그리지 않는다.
            if (aim == null || aim.Drawing == false || aim.DrawRatio < ArcheryAimSystem.DrawThreshold)
            {
                Hide();
                return;
            }

            var entity = entityRegistry.Get(playerContext.entityId);
            if (entity == null || entity.Has<GameFramework.World.Transform>() == false)
            {
                Hide();
                return;
            }

            var camera = cameraController.MainCamera;
            if (camera == null)
            {
                Hide();
                return;
            }

            //  속도는 시뮬이 쏠 때 쓰는 것과 같은 값에서 나와야 한다 — 선이 거짓말을 하면
            //  조준선이 있으나 마나다. 당김은 손가락이 끈 거리가 정하므로 그 값을 그대로 읽는다.
            float speed = ArcheryAimSystem.SpeedFor(aim.DrawRatio);

            //  방향은 시뮬 값(aim.Yaw/Pitch)이 아니라 *지금 화면에 그려진 카메라 회전*에서 뽑는다.
            //  시뮬 값은 틱(20ms)마다 계단으로 바뀌는데 화면은 매 프레임 도니까, 시뮬 값으로 그리면
            //  점만 계단으로 튀어 덜덜거린다. 쏠 때 서버로 가는 각도 결국 이 회전에서 읽은 값이라
            //  (ArcheryAimView.SetAim) 거짓말이 되지도 않는다.
            float yaw = camera.transform.eulerAngles.y;
            float pitch = -Mathf.DeltaAngle(0f, camera.transform.eulerAngles.x);

            //  발사할 때와 같은 원점·속도로 만든다. 여기가 어긋나면 선이 거짓말을 한다.
            var shot = new ArcheryShot(
                playerContext.entityId,
                0,
                GameFramework.World.EntityMotionExtensions.GetPosition(entity)
                    + new Vector3(0f, ArcheryAimSystem.EyeHeight, 0f),
                ArcheryTrajectory.DirectionFrom(yaw, pitch) * speed);

            Draw(shot, camera);
        }

        private void Draw(in ArcheryShot shot, Camera camera)
        {
            EnsureDots();
            for (int i = 0; i < DotCount; i++)
            {
                //  0초(활 자리)는 건너뛴다 — 발밑에 점이 하나 붙어 있으면 지저분하다.
                float seconds = PreviewSeconds * (i + 1) / DotCount;
                dots[i].transform.position = ArcheryTrajectory.PositionAt(shot, seconds);
                //  화면에서 같은 굵기로 보이게 카메라 거리에 비례해 키운다 — 안 하면 코앞의 첫 점만
                //  크게 부풀어 보인다(원근 때문). 여기에 뒤로 갈수록 가늘어지는 감쇠를 얹어 방향을 준다.
                float distance = Vector3.Distance(dots[i].transform.position, camera.transform.position);
                float taper = Mathf.Lerp(1f, 0.55f, (float)i / (DotCount - 1));
                dots[i].transform.localScale = Vector3.one * (DotSize * distance * taper);
                dots[i].SetActive(true);
            }
        }

        private ArcheryAim MyAim()
        {
            if (playerContext.entityId == null)
            {
                return null;
            }
            return entityRegistry.Get(playerContext.entityId)?.Get<ArcheryAim>();
        }

        private void Hide()
        {
            for (int i = 0; i < dots.Count; i++)
            {
                if (dots[i] != null)
                {
                    dots[i].SetActive(false);
                }
            }
        }

        private void EnsureDots()
        {
            while (dots.Count < DotCount)
            {
                var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(dot.GetComponent<Collider>());   // 그림일 뿐이다
                var renderer = dot.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = DotMaterial();
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                dots.Add(dot);
            }
        }

        private Material DotMaterial()
        {
            if (_dotMaterial == null)
            {
                //  점은 빛을 받을 이유가 없다 — Unlit이라야 어두운 곳에서도 같은 색으로 보인다.
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                //  과녁(노랑)·함정(파랑)·화살(빨강) 어느 것과도 안 겹치게 흰색에 가깝게 둔다.
                _dotMaterial = new Material(shader) { color = new Color(1f, 1f, 1f, 0.85f) };
            }
            return _dotMaterial;
        }

        public void Dispose()
        {
            for (int i = 0; i < dots.Count; i++)
            {
                if (dots[i] != null)
                {
                    Object.Destroy(dots[i]);
                }
            }
            dots.Clear();

            if (_dotMaterial != null)
            {
                Object.Destroy(_dotMaterial);
                _dotMaterial = null;
            }
        }
    }
}
