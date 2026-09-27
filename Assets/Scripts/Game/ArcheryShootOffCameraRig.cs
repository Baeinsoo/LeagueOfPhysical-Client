using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 한 발 승부 카메라: 평소·결과 때는 내 캐릭터 뒤 3인칭(나와 옆 사람이 쏘고 반응하는 게 보인다),
    /// 활을 당기는 동안만 눈 앞 1인칭으로 들어간다. 조준은 카메라의 각도만 읽으므로(위치 아님) 3인칭이어도
    /// 조준 감각은 그대로다. 내 몸의 리액션(튕김)은 카메라 중심에서 빼서 시야가 출렁이지 않게 한다.
    /// <para>리액션을 얹은 뒤(2900)·카메라가 그리기 전(3000)에 돈다 — <see cref="ArcheryShootOffCameraRigDriver"/>.</para>
    /// </summary>
    public class ArcheryShootOffCameraRig : IStartable, System.IDisposable
    {
        public const float BlendSeconds = 0.25f;
        public const float ThirdPersonDistance = 3f;
        public const float ThirdPersonRaise = 0.8f;

        private readonly ArcheryCourse course;
        private readonly CameraController cameraController;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IPlayerContext playerContext;
        private readonly ArcheryShootOffReactionView reactionView;

        private ArcheryShootOffCameraRigDriver driver;
        private float blend;
        private float firstPersonDistance = float.NaN;

        public ArcheryShootOffCameraRig(ArcheryCourse course, CameraController cameraController,
                                        GameFramework.World.EntityRegistry entityRegistry,
                                        IPlayerContext playerContext, ArcheryShootOffReactionView reactionView)
        {
            this.course = course;
            this.cameraController = cameraController;
            this.entityRegistry = entityRegistry;
            this.playerContext = playerContext;
            this.reactionView = reactionView;
        }

        /// <summary>0 = 3인칭, 1 = 1인칭. 당기는 동안 1로, 아니면 0으로 <see cref="BlendSeconds"/>에 걸쳐 간다.</summary>
        public static float StepBlend(float current, bool drawing, float deltaTime)
            => Mathf.MoveTowards(current, drawing ? 1f : 0f, deltaTime / BlendSeconds);

        public static float DistanceAt(float blend, float firstPersonDistance)
            => Mathf.Lerp(ThirdPersonDistance, firstPersonDistance, Ease(blend));

        public static float RaiseAt(float blend) => Mathf.Lerp(ThirdPersonRaise, 0f, Ease(blend));

        private static float Ease(float x) => x * x * (3f - 2f * x);

        public void Start()
        {
            driver = new GameObject(nameof(ArcheryShootOffCameraRigDriver)).AddComponent<ArcheryShootOffCameraRigDriver>();
            driver.Rig = this;
        }

        public void Dispose()
        {
            if (driver != null)
            {
                driver.Rig = null;
                Object.Destroy(driver.gameObject);
                driver = null;
            }
            if (cameraController != null)
            {
                cameraController.DistanceOverride = null;
                cameraController.PivotOffset = Vector3.zero;
            }
        }

        public void Apply()
        {
            if (course.IsShootOff == false || cameraController == null)
            {
                return;
            }
            if (float.IsNaN(firstPersonDistance))
            {
                firstPersonDistance = cameraController.Distance;   // 씬에 맞춰 둔 1인칭 거리(눈 앞)
            }

            var aim = MyAim();
            blend = StepBlend(blend, aim != null && aim.Drawing, Time.deltaTime);
            cameraController.DistanceOverride = DistanceAt(blend, firstPersonDistance);
            cameraController.PivotOffset = Vector3.up * (RaiseAt(blend) - reactionView.LocalLift);
        }

        private ArcheryAim MyAim()
        {
            string me = playerContext.entityId;
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Id == me)
                {
                    return entity.Get<ArcheryAim>();
                }
            }
            return null;
        }
    }
}
