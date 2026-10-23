using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 조작 패드의 커맨드. 터치 좌표 해석은 View가 하고, 여기서는 그 결과를 시점과 입력으로 넘긴다.
    /// <b>조준은 카메라가 보는 방향</b>이라 각도를 여기서 들고 있지 않다 — 매 프레임 카메라에서
    /// 읽어 입력에 싣는 일은 <see cref="LOP.ArcheryAimView"/>가 한다.
    /// </summary>
    public class ArcheryPadViewModel : IArcheryPadViewModel
    {
        private readonly PlayerInputManager input;
        private readonly CameraController cameraController;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IPlayerContext playerContext;
        private readonly ArcheryImpactLog impactLog;
        private readonly ArcheryConfig config;
        private readonly ArcheryCourse course;
        private readonly ArcheryAimView aimView;
        private readonly ArcheryHaptics haptics;

        private readonly ArcheryBowControls controls;

        public ArcheryPadViewModel(
            PlayerInputManager input,
            CameraController cameraController,
            GameFramework.World.EntityRegistry entityRegistry,
            IPlayerContext playerContext,
            ArcheryImpactLog impactLog,
            ArcheryConfig config,
            ArcheryCourse course,
            ArcheryAimView aimView,
            ArcheryHaptics haptics)
        {
            this.input = input;
            this.cameraController = cameraController;
            this.entityRegistry = entityRegistry;
            this.playerContext = playerContext;
            this.impactLog = impactLog;
            this.config = config;
            this.course = course;
            this.aimView = aimView;
            this.haptics = haptics;
            controls = new ArcheryBowControls(input, cameraController, ReadDrawRatio, armed => haptics?.Released(armed, ArrowsLeft));
        }

        private float ReadDrawRatio()
        {
            var entity = entityRegistry.Get(playerContext.entityId);
            return entity?.Get<ArcheryAim>()?.DrawRatio ?? 0f;
        }

        public bool ShowScoreboard => true;

        public ArcheryAimAccel.Strength AimAccel => controls.AimAccel;
        public string AimAccelLabel => controls.AimAccelLabel;
        public void CycleAimAccel() => controls.CycleAimAccel();
        public void PollKeyboard() => controls.PollKeyboard();
        public bool OverCancelButton => controls.OverCancelButton;
        public float DrawRatio => controls.DrawRatio;
        public bool DrawArmed => controls.DrawArmed;
        public ArcheryPointerRouter.Role PointerDown(int pointerId, ArcheryButtonLayout.Hit hit, float screenWidthInHeights)
            => controls.PointerDown(pointerId, hit, screenWidthInHeights);
        public bool MovesButton(int pointerId) => controls.MovesButton(pointerId);
        public void PointerMove(int pointerId, Vector2 deltaFraction, bool overCancelButton)
            => controls.PointerMove(pointerId, deltaFraction, overCancelButton);
        public void PointerUp(int pointerId) => controls.PointerUp(pointerId);
        public void PointerLost(int pointerId) => controls.PointerLost(pointerId);
        public void SetMove(Vector2 stick) => controls.SetMove(stick);
        public void ClearMove() => controls.ClearMove();
        public void FeedMove() => controls.FeedMove();
        public void AdvanceFingerClock(float unscaledDeltaSeconds) => controls.AdvanceFingerClock(unscaledDeltaSeconds);
        public void Tick(float deltaSeconds) => controls.Tick(deltaSeconds);
        public bool Holding => controls.Holding;
        public bool CancelActive => controls.CancelActive;

        /// <summary>걷기 스틱을 보이나. 걷는 속도가 0인 코스(한 발 승부·투석기·원형)는 숨긴다 —
        /// 그 구석을 스틱이 가져가면 거기서 누른 손가락이 활을 못 든다.</summary>
        public bool ShowMoveStick => course.MoveSpeed > 0f;

        /// <summary>
        /// 지금 화면을 그리는 카메라. 화면이 "+10"을 맞은 자리에 띄우려면 월드→화면 투영이 필요하다.
        /// <b><c>Camera.main</c>을 쓰지 말 것</b> — 조준 카메라가 그 태그를 달고 있다는 보장이 없고,
        /// 빗나가면 팝업이 전부 화면 한가운데로 떨어져 "어디를 맞혔나"가 사라진다.
        /// </summary>
        public Camera Camera => cameraController.MainCamera;

        /// <summary>지금 자리에서 내가 쏜 것들. 화면이 기록판에 점으로 찍는다.</summary>
        public System.Collections.Generic.IReadOnlyList<ArcheryImpactLog.Shot> Impacts => impactLog.Shots;

        /// <summary>기록판이 모으고 있는 자리 번호. 바뀌면 화면이 점을 다시 그린다.</summary>
        public int ImpactWave => impactLog.Wave;

        /// <summary>
        /// 발당 점수 앞에 "+"를 붙이나. 한 발 승부에서 그 값은 <b>띠 점수</b>(1~10)이고 점수로 더해지지
        /// 않는다(라운드 순위 점수만 더한다) — "+10"으로 쓰면 10점을 얻은 것처럼 읽힌다.
        /// </summary>
        public string PointsPrefix => config.CourseKind == ArcheryCourseKind.ShootOff ? "" : "+";

        /// <summary>맞은 자리에 띄우는 글. 한 발 승부의 10점은 "10!!"(점수로 더해지지 않으니 +를 안 붙인다).</summary>
        public static string PopupTextFor(ArcheryCourseKind kind, int points)
        {
            if (kind == ArcheryCourseKind.ShootOff)
            {
                return points == 10 ? "10!!" : points.ToString();
            }
            return "+" + points;
        }

        public string PopupText(int points) => PopupTextFor(config.CourseKind, points);

        /// <summary>착탄 기록판을 보이나. 한 발 승부는 결과 화면의 큰 과녁이, 투석기는 띠가 없어 숨긴다.</summary>
        public static bool ShowsImpactPlot(ArcheryCourseKind kind)
            => kind != ArcheryCourseKind.ShootOff && kind != ArcheryCourseKind.Catapult;

        /// <summary>맞은 자리에 "+N"을 띄우나. 투석기는 HUD가 누가 맞혔는지까지("OO +3") 띄우므로 끈다.</summary>
        public static bool ShowsHitPopups(ArcheryCourseKind kind) => kind != ArcheryCourseKind.Catapult;

        public bool ShowImpactPlot => ShowsImpactPlot(config.CourseKind);

        public bool ShowHitPopups => ShowsHitPopups(config.CourseKind);

        /// <summary>
        /// 기록판에 그릴 점수 띠 — 바깥 비율과 점수의 짝, 중심에서 바깥 순서다.
        /// <b>과녁을 그리는 값과 같은 데이터</b>에서 나오므로 그림과 실제 채점이 갈라질 수 없다.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<ArcheryRingBand> Bands => config.Range.Kind.Bands;

        /// <summary>내가 지금까지 모은 점수. 서버 스냅샷이 채우는 값이라 <b>매 프레임 읽어</b> 쓴다.</summary>
        public int Score
        {
            get
            {
                var entity = entityRegistry.Get(playerContext.entityId);
                return entity?.Get<ArcheryScore>()?.Value ?? 0;
            }
        }

        /// <summary>남은 화살. <b>−1이면 무제한</b>(원형 맵)이라 화면이 아예 안 띄운다.</summary>
        public int ArrowsLeft
        {
            get
            {
                var entity = entityRegistry.Get(playerContext.entityId);
                var quiver = entity?.Get<ArcheryQuiver>();
                return quiver?.Remaining ?? -1;
            }
        }

        /// <summary>
        /// 손떨림이 얼마나 자랐나(0~1). 여유 시간 안이거나 손떨림이 없는 코스면 0. 당김 고리가 만작의
        /// 노란색을 이만큼 빨강 쪽으로 민다(<see cref="ArcheryDrawRing.ShakeLevel"/>).
        /// </summary>
        public float ShakeLevel => ArcheryDrawRing.ShakeLevel(aimView?.MyHeldSeconds ?? 0f, config);
    }
}
