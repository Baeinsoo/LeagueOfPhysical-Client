using System;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 추격자를 화면에 세운다. 위치는 시계만 보면 나오므로(<see cref="FlappyChaserCurve"/>)
    /// 서버에서 받는 것이 없다.
    ///
    /// <para><b>어느 시각으로 그릴지</b>가 이 클래스의 핵심이다. 화면엔 시간대가 둘이다 —
    /// 내 새는 예측이라 조금 앞, 남의 새는 지연 보간이라 조금 뒤. 벽은 하나뿐이라 둘 다 맞출 수
    /// 없어서 <b>지금 보고 있는 새</b>의 시각(<see cref="EntityRenderClock"/>)에 맞춘다.
    /// 대가로, 내가 살아 있는 동안 남이 잡히면 그 새가 벽 안으로 3m쯤 들어간 뒤에 사라진다.</para>
    ///
    /// <para>씬에 미리 놓을 대상이 아니고 프레임마다 트랜스폼 하나만 옮기므로 MonoBehaviour가
    /// 아니라 진입점이다. 카메라가 LateUpdate에서 움직이므로 그 뒤에 읽는다.</para>
    /// </summary>
    public class FlappyChaserView : ILateTickable, IDisposable
    {
        private const float WallHeight = 300f;
        private const float WallThickness = 2f;
        //  카메라 쪽으로 뻗는 두께. 앞으로 늘리는 것은 <b>공짜다</b> — 판정면보다 앞이라
        //  틈을 좁아 보이게 만들지 않는다(2026-09-15 판정면 정렬 §③). 그래야 선이 아니라
        //  "다가오는 면"으로 보인다.
        private const float WallDepth = 9f;

        private readonly GameFramework.World.IWorld world;
        private readonly EntityRenderClock renderClock;
        private readonly FlappySpectate spectate;
        private readonly CameraController cameraController;
        private readonly FinishLineBounds finishLine;
        private readonly FlappyConfig config;

        private GameObject wall;

        /// <summary>지금 그려진 벽의 x. HUD가 이 값을 읽어야 숫자와 그림이 어긋나지 않는다.</summary>
        public float X { get; private set; }

        public FlappyChaserView(GameFramework.World.IWorld world,
                                EntityRenderClock renderClock,
                                FlappySpectate spectate,
                                CameraController cameraController,
                                FinishLineBounds finishLine,
                                FlappyConfig config)
        {
            this.world = world;
            this.renderClock = renderClock;
            this.spectate = spectate;
            this.cameraController = cameraController;
            this.finishLine = finishLine;
            this.config = config;
        }

        public void LateTick()
        {
            EnsureWall();

            //  벽은 결승선에서 멈춘다 — 서버의 잡는 판정과 같은 상한을 써야 화면이 맞는다.
            float stopAtX = finishLine.TryGet(out var bounds) ? bounds.min.x : float.MaxValue;
            //  코디네이터가 ITickable에서 Refresh를 이미 돌렸다(Tick이 LateTick보다 먼저다).
            X = FlappyChaserCurve.XAt(config, ElapsedSeconds(spectate.Current), stopAtX);

            Vector3 position = wall.transform.position;
            position.x = X;
            //  세로는 카메라를 따라간다 — 고도가 변하는 맵에서도 화면 세로를 늘 덮게.
            if (cameraController.MainCamera != null)
            {
                position.y = cameraController.MainCamera.transform.position.y;
            }
            //  뒷면을 판정면(z=0)에 걸고 앞으로만 뻗는다. 뒤로 뻗으면 원근이 틈을 좁아 보이게 한다.
            position.z = -WallDepth * 0.5f;
            wall.transform.position = position;
        }

        private float ElapsedSeconds(string watchedEntityId)
        {
            double secondsPerTick = renderClock.SecondsPerTick;
            if (secondsPerTick <= 0d || world.GameplayStartTick == long.MaxValue)
            {
                return 0f;   // 아직 출발 정보가 없다 — 벽은 시작점에 서 있는다
            }
            //  틱이 아니라 연속 시각으로 묻는다. 틱으로 자르면 벽이 0.02초 단위로만 움직여
            //  0.2m씩 점프하고, 60fps 화면에서는 여섯 프레임 중 하나가 제자리에 선다.
            return (float)(renderClock.TimeFor(watchedEntityId) - world.GameplayStartTick * secondsPerTick);
        }

        private void EnsureWall()
        {
            if (wall != null)
            {
                return;
            }

            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "FlappyChaserWall";

            //  물리 몸을 주면 새를 밀어 클·서 시뮬이 갈린다. 판정은 서버의 x 비교뿐이다.
            var collider = wall.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }

            wall.transform.localScale = new Vector3(WallThickness, WallHeight, WallDepth);

            //  붕괴 전선 — 어두운 먼지벽에 앞 가장자리만 잔불. 벽은 런타임 생성물이라 Art 에셋을
            //  물릴 수 없어 색을 여기 둔다. 이 값이 추격자 색의 유일한 출처다.
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var material = new Material(shader);
                material.SetColor("_BaseColor", new Color(0.16f, 0.13f, 0.12f));
                material.SetFloat("_Smoothness", 0f);
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(0.60f, 0.12f, 0.03f));
                wall.GetComponent<MeshRenderer>().sharedMaterial = material;
                AddDust(material);
            }
        }

        //  벽 앞으로 흘러나오는 먼지. 벽이 화면에 들어오는 것은 <b>실패했을 때뿐</b>이라
        //  (잘 날면 5개 화면 뒤에 있다) 값을 비싸지 않게 유지한다.
        private void AddDust(Material material)
        {
            var host = new GameObject("Dust");
            host.transform.SetParent(wall.transform, worldPositionStays: false);
            var dust = host.AddComponent<ParticleSystem>();

            var main = dust.main;
            main.startLifetime = 1.4f;
            main.startSpeed = 3.5f;
            main.startSize = 6f;
            main.startColor = new Color(0.35f, 0.30f, 0.27f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;

            var emission = dust.emission;
            emission.rateOverTime = 45f;

            //  벽이 세로로 300m라 로컬 스케일을 그대로 쓰면 먼지가 화면 밖까지 퍼진다.
            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.5f, 0.12f, 1f);

            host.GetComponent<ParticleSystemRenderer>().material = material;
        }

        public void Dispose()
        {
            if (wall != null)
            {
                UnityEngine.Object.Destroy(wall);
                wall = null;
            }
        }
    }
}
