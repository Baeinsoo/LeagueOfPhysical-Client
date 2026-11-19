using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 삼색 그랩 조작 — 왼쪽 이동 스틱, 오른쪽 쏘기. 톡 = 회전판 가운데로, 끌었다 놓기 = 끈 방향(카메라 기준),
    /// 크게 끌었다 제자리로 돌아와 놓기 = 취소. 내 갈고리가 나가 있는 동안은 이동을 0으로 보낸다(스펙 §5).
    /// </summary>
    public class TriGrabPadViewModel : System.IDisposable
    {
        /// <summary>놓을 때 이만큼(px) 넘게 끌려 있으면 그 방향으로 쏜다.</summary>
        public const float FireDragPx = 14f;
        /// <summary>한 번이라도 이만큼(px) 넘게 끌었다가 제자리에서 놓으면 취소. 그보다 덜 끌었으면 톡.</summary>
        public const float CancelDragPx = 30f;

        public enum ReleaseOutcome { Tap, Fire, Cancel }

        private readonly PlayerInputManager input;
        private readonly CameraController cameraController;
        private readonly TriGrabClientState state;
        private readonly TriGrabMyHook myHook;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.Runner.IRunner runner;

        private Vector2 stick;

        public TriGrabPadViewModel(PlayerInputManager input, CameraController cameraController, TriGrabClientState state,
                                   TriGrabMyHook myHook, IPlayerContext playerContext,
                                   GameFramework.World.EntityRegistry entityRegistry, GameFramework.Runner.IRunner runner)
        {
            this.input = input;
            this.cameraController = cameraController;
            this.state = state;
            this.myHook = myHook;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.runner = runner;
        }

        /// <summary>손을 뗀 순간의 판정. 놓을 때 끌린 거리가 먼저다 — 크게 끌었어도 놓을 때 끌려 있으면 쏜다.</summary>
        public static ReleaseOutcome OutcomeOnRelease(float maxDragPx, float dragPx)
        {
            if (dragPx > FireDragPx) return ReleaseOutcome.Fire;
            return maxDragPx < CancelDragPx ? ReleaseOutcome.Tap : ReleaseOutcome.Cancel;
        }

        /// <summary>쏠 수 있나 — 서버 TryFire와 같은 세 조건(Live, 내 갈고리 없음, 재장전 끝).</summary>
        public static bool CanFire(TriGrabPhase phase, bool hookOut, long tick, long reloadUntilTick)
            => phase == TriGrabPhase.Live && !hookOut && tick >= reloadUntilTick;

        /// <summary>보낼 이동. 갈고리가 나가 있으면 선다.</summary>
        public static Vector2 MoveFor(Vector2 worldMove, bool hookOut) => hookOut ? Vector2.zero : worldMove;

        /// <summary>톡 = 내 자리에서 회전판 가운데(원점) 쪽 yaw. 가운데에 서 있으면 방향이 없다.</summary>
        public static float? TapYaw(Vector2 myPosition)
        {
            var dir = -myPosition;
            return dir.sqrMagnitude > 1e-6f ? TriGrabMath.YawFromDir(dir) : (float?)null;
        }

        /// <summary>화면에 그리는 틱(정수). 쏘기 막기·버튼 흐림이 화면에 보이는 것과 같은 틱을 쓴다.</summary>
        public long RenderTick
        {
            get
            {
                var t = runner?.tickUpdater;
                if (t == null || t.interval <= 0d) return 0;
                return (long)System.Math.Floor((t.elapsedTime - t.interval) / t.interval);
            }
        }

        public bool HookOut => myHook.IsOut(playerContext.entityId, input.NextInputTick);

        public bool Ready
        {
            get
            {
                long tick = RenderTick;
                long reload = state.TryGetPlayer(playerContext.entityId, out var score) ? score.ReloadUntilTick : 0;
                return CanFire(state.PhaseAt(tick), myHook.IsOut(playerContext.entityId, tick), tick, reload);
            }
        }

        /// <summary>방향 스틱(−1~1). 매 프레임 <see cref="Apply"/>가 갈고리 상태를 보고 보낸다.</summary>
        public void Move(Vector2 value) => stick = value;

        /// <summary>매 프레임 — 스틱 값을 카메라 기준으로 돌려 보내되, 갈고리가 나가 있으면 0을 보낸다.</summary>
        public void Apply()
        {
            var camera = cameraController != null ? cameraController.MainCamera : null;
            var world = camera != null ? DodgePadViewModel.CameraRelative(stick, camera) : Vector2.zero;
            var send = MoveFor(world, HookOut);
            input.SetMovement(send.x, send.y);
        }

        /// <summary>데스크톱 WASD·화살표. 스틱을 잡고 있지 않을 때만 부른다.</summary>
        public void MoveByKeyboard()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var dir = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) dir.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) dir.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) dir.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) dir.x -= 1f;
            }
            Move(dir == Vector2.zero ? Vector2.zero : dir.normalized);
        }

        /// <summary>끌기 방향(화면, 위 = +y)의 월드 yaw. 넉백 조작판과 같은 식.</summary>
        public float? DragYaw(Vector2 screenDrag)
        {
            var camera = cameraController != null ? cameraController.MainCamera : null;
            return camera == null ? (float?)null : KnockbackPadViewModel.AimYaw(screenDrag, camera.transform.eulerAngles.y);
        }

        public bool FireTap()
        {
            return TryGetMyPosition(out var me) && Fire(TapYaw(me));
        }

        public bool FireDrag(Vector2 screenDrag) => Fire(DragYaw(screenDrag));

        /// <summary>마우스가 가리키는 바닥점 쪽으로 쏜다(PC).</summary>
        public bool FireAtGroundPoint(Vector2 screenPosition)
        {
            var camera = cameraController != null ? cameraController.MainCamera : null;
            if (camera == null || !TryGetMyPosition(out var me)) return false;
            var ray = camera.ScreenPointToRay(screenPosition);
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return false;
            float t = -ray.origin.y / ray.direction.y;
            if (t <= 0f) return false;
            var hit = ray.origin + ray.direction * t;
            var dir = new Vector2(hit.x - me.x, hit.z - me.y);
            return dir.sqrMagnitude > 1e-6f && Fire(TriGrabMath.YawFromDir(dir));
        }

        private bool Fire(float? yaw)
        {
            if (!yaw.HasValue || !Ready || !TryGetMyPosition(out var me))
            {
                return false;
            }
            long fireTick = input.NextInputTick;
            input.SetFire(yaw.Value);
            //  쏘는 틱부터 서야 서버가 재는 출발점(그 틱 이동 뒤 자리)이 지금 자리와 같다.
            input.SetMovement(0f, 0f);
            myHook.Fire(playerContext.entityId, me, yaw.Value, fireTick);
            return true;
        }

        /// <summary>조준선 출발점·방향(월드 xz). 끌기가 쏘기 거리 안이면 false.</summary>
        public bool TryGetAim(Vector2 screenDrag, float dragPx, out Vector2 origin, out Vector2 dir)
        {
            dir = Vector2.zero;
            if (!TryGetMyPosition(out origin) || dragPx <= FireDragPx) return false;
            var yaw = DragYaw(screenDrag);
            if (!yaw.HasValue) return false;
            dir = TriGrabMath.DirFromYaw(yaw.Value);
            return true;
        }

        private bool TryGetMyPosition(out Vector2 position)
        {
            position = default;
            string id = playerContext.entityId;
            var body = string.IsNullOrEmpty(id) ? null : entityRegistry.Get(id);
            if (body == null) return false;
            //  서버가 재는 자리(시뮬 위치)에서 나가야 하므로 그려진 몸이 아니라 월드 값을 쓴다.
            var p = GameFramework.World.EntityMotionExtensions.GetPosition(body);
            position = new Vector2(p.x, p.z);
            return true;
        }

        public void Dispose()
        {
            stick = Vector2.zero;
            input.SetMovement(0f, 0f);
        }
    }
}
