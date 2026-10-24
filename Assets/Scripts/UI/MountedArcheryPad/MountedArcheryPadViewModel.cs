using System.Collections.Generic;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 말 활쏘기 조작판. 조작은 활쏘기와 같은 <see cref="ArcheryBowControls"/>에 맡기고,
    /// 점수·과녁이 없는 모드라 그쪽 표시는 전부 끈다.
    /// </summary>
    public class MountedArcheryPadViewModel : IArcheryPadViewModel
    {
        private static readonly IReadOnlyList<ArcheryImpactLog.Shot> NoImpacts = new List<ArcheryImpactLog.Shot>();
        private static readonly IReadOnlyList<ArcheryRingBand> NoBands = new List<ArcheryRingBand>();

        private readonly CameraController cameraController;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IPlayerContext playerContext;
        private readonly ArcheryBowControls controls;

        public MountedArcheryPadViewModel(PlayerInputManager input, CameraController cameraController,
            GameFramework.World.EntityRegistry entityRegistry, IPlayerContext playerContext,
            MountedArcheryConfig config, MountedAimAssistState assistState)
        {
            this.cameraController = cameraController;
            this.entityRegistry = entityRegistry;
            this.playerContext = playerContext;
            controls = new ArcheryBowControls(input,
                new FrictionAimCamera(cameraController, assistState, config.AimAssistFriction), ReadDrawRatio, null, MountedCameraZoom.WideFov);
        }

        /// <summary>손가락 끌기만 둔하게 한다 — 적이 조준 원뿔 안이면 돌리는 각에 감도를 곱한다.</summary>
        private sealed class FrictionAimCamera : IAimCamera
        {
            private readonly IAimCamera inner;
            private readonly MountedAimAssistState state;
            private readonly float friction;

            public FrictionAimCamera(IAimCamera inner, MountedAimAssistState state, float friction)
            {
                this.inner = inner;
                this.state = state;
                this.friction = friction;
            }

            public float Yaw => inner.Yaw;
            public float Pitch => inner.Pitch;
            public float CameraYawDegrees => inner.CameraYawDegrees;
            public Camera MainCamera => inner.MainCamera;
            public void AimBy(Vector2 degrees) => inner.AimBy(degrees * MountedAimAssist.Friction(state.Locked, friction));
        }

        private float ReadDrawRatio()
        {
            if (playerContext.entityId == null)
            {
                return 0f;
            }
            return entityRegistry.Get(playerContext.entityId)?.Get<ArcheryAim>()?.DrawRatio ?? 0f;
        }

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

        public Camera Camera => cameraController.MainCamera;
        public bool ShowMoveStick => true;
        public float ShakeLevel => 0f;
        public int Score => 0;
        public int ArrowsLeft => -1;
        public IReadOnlyList<ArcheryImpactLog.Shot> Impacts => NoImpacts;
        public int ImpactWave => 0;
        public string PointsPrefix => "";
        public string PopupText(int points) => "";
        public bool ShowImpactPlot => false;
        public bool ShowHitPopups => false;
        public IReadOnlyList<ArcheryRingBand> Bands => NoBands;
        public bool ShowScoreboard => false;
    }
}
