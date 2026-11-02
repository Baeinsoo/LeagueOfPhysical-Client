using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 카드 러시 카메라(spec §7.2) — 따라갈 몸(관전 대상)을 뒤 위에서 보며 진행 방향을 본다. 회전 조작은 받지 않는다.
    ///
    /// <para><see cref="CameraController.BeforeFollow"/>에서 계산한다(<see cref="FlappyCorridorCamera"/>와 같은 갈고리) —
    /// 그때는 보간기가 이번 프레임 몸 자리를 이미 옮겨 놓았다. 피벗은 몸 자리 + 진행 방향 1.5칸이고, 그 점을
    /// <c>1 − exp(−10·dt)</c>로 따라간다: 0.3초 이동을 따라가되 덜컥이지 않게. 늦는 것은 카메라뿐, 몸은 늦추지 않는다.</para>
    ///
    /// <para>카메라의 대상(<see cref="CameraController.Target"/>)은 바꾸지 않고 <see cref="CameraController.PivotOffset"/>으로
    /// 피벗을 직접 놓는다 — 관전 대상이 바뀌어도 SetTarget이 거리·각을 다시 잡으며 튀지 않고, 같은 따라가기로 넘어간다.</para>
    /// </summary>
    public class CardRushCamera : IStartable, System.IDisposable
    {
        public const float PitchDegrees = 30f;
        public const float Distance = 9f;
        public const float PortraitDistanceScale = 1.35f;
        public const float LookAheadSteps = 1.5f;
        public const float FollowSharpness = 10f;

        private readonly CameraController cameraController;
        private readonly CardRushMapRulesField rules;
        private readonly ISpectateTargets spectate;
        private readonly ActorRegistry actorRegistry;

        private bool hasPivot;
        private Vector3 pivot;

        public CardRushCamera(CameraController cameraController, CardRushMapRulesField rules,
                              ISpectateTargets spectate, ActorRegistry actorRegistry)
        {
            this.cameraController = cameraController;
            this.rules = rules;
            this.spectate = spectate;
            this.actorRegistry = actorRegistry;
        }

        /// <summary>몸 자리에서 본 목표 피벗 — 바로 앞 칸이 화면 가운데보다 조금 위에 온다.</summary>
        public static Vector3 PivotTarget(Vector3 body, Vector3 forward, float stepSpacing)
            => body + CardRushLanes.Flat(forward) * (stepSpacing * LookAheadSteps);

        /// <summary>프레임마다 목표로 다가가는 비율. dt와 무관하게 같은 빠르기로 따라간다.</summary>
        public static Vector3 Follow(Vector3 current, Vector3 target, float deltaTime)
            => Vector3.Lerp(current, target, 1f - Mathf.Exp(-FollowSharpness * deltaTime));

        public static float YawDegrees(Vector3 forward)
        {
            var f = CardRushLanes.Flat(forward);
            return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        /// <summary>세로 화면은 양옆이 좁아 멀리 선다 — 양옆 레인이 함께 보이게.</summary>
        public static float DistanceFor(int screenWidth, int screenHeight)
            => screenHeight > screenWidth ? Distance * PortraitDistanceScale : Distance;

        public void Start()
        {
            if (cameraController != null)
            {
                cameraController.BeforeFollow += OnBeforeFollow;
            }
        }

        private void OnBeforeFollow() => Tick(Time.deltaTime);

        internal void Tick(float deltaTime)
        {
            if (cameraController == null || cameraController.Target == null || rules.HasRules == false)
            {
                return;
            }

            var body = FollowedBody();
            var target = PivotTarget(body.position, rules.Forward, rules.StepSpacing);
            pivot = hasPivot ? Follow(pivot, target, deltaTime) : target;
            hasPivot = true;

            //  카메라의 피벗 = Target 자리 + 위로 PivotHeight + PivotOffset — 그 합이 우리 피벗이 되게 맞춘다.
            cameraController.PivotOffset = pivot - (cameraController.Target.position + Vector3.up * cameraController.PivotHeight);

            //  각은 매 프레임 덮어쓴다 — 회전 입력이 들어와도 남지 않는다.
            cameraController.AimBy(CameraController.StepToward(
                cameraController.Yaw, cameraController.Pitch, YawDegrees(rules.Forward), PitchDegrees, 1f));
            cameraController.DistanceOverride = DistanceFor(Screen.width, Screen.height);
        }

        //  관전 대상이 있으면 그 몸, 아직 몸이 안 붙었으면 카메라가 원래 따라가던 몸(내 몸).
        private Transform FollowedBody()
        {
            string id = spectate?.Current;
            if (string.IsNullOrEmpty(id) == false && actorRegistry != null)
            {
                var visual = actorRegistry.Get(id)?.visualGameObject;
                if (visual != null)
                {
                    return visual.transform;
                }
            }
            return cameraController.Target;
        }

        public void Dispose()
        {
            if (cameraController != null)
            {
                cameraController.BeforeFollow -= OnBeforeFollow;
                cameraController.PivotOffset = Vector3.zero;
                cameraController.DistanceOverride = null;
            }
        }
    }
}
