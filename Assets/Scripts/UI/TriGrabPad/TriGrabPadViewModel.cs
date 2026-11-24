using UnityEngine;
using TG = LOP.TriGrab;

namespace LOP.UI
{
    /// <summary>
    /// 삼색 그랩 조작 — 왼쪽 이동 스틱, 오른쪽 조준 스틱. 바깥으로 끈 채 떼면 그 방향으로 손을 쏘고,
    /// 톡(끌지 않음)이나 안쪽으로 되돌아와 떼면 취소(스펙 §3). 손이 나가 있거나 기절 중엔 이동을 0으로 보낸다
    /// (서버도 같은 틱에 이동을 막는다 — 다르면 내 몸 예측이 떨린다).
    /// </summary>
    public class TriGrabPadViewModel : System.IDisposable
    {
        /// <summary>뗄 때 이만큼(px) 이상 끌려 있으면 그 방향으로 쏜다. 그보다 안쪽이면 취소.</summary>
        public const float FireDragPx = 14f;

        public enum ReleaseOutcome { Fire, Cancel }

        private readonly PlayerInputManager input;
        private readonly CameraController cameraController;
        private readonly TriGrabClientState state;
        private readonly TriGrabMyHook myHook;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        private Vector2 stick;

        public TriGrabPadViewModel(PlayerInputManager input, CameraController cameraController, TriGrabClientState state,
                                   TriGrabMyHook myHook, IPlayerContext playerContext,
                                   GameFramework.World.EntityRegistry entityRegistry)
        {
            this.input = input;
            this.cameraController = cameraController;
            this.state = state;
            this.myHook = myHook;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
        }

        /// <summary>손을 뗀 순간의 판정 — 뗄 때 끌린 거리만 본다. 톡도, 끌었다 되돌아온 것도 취소다.</summary>
        public static ReleaseOutcome OutcomeOnRelease(float dragPx)
            => dragPx >= FireDragPx ? ReleaseOutcome.Fire : ReleaseOutcome.Cancel;

        /// <summary>쏠 수 있나 — 서버 TryFire와 같은 조건(Live, 기절 끝, 손 쉼 끝, 내 손 없음).</summary>
        public static bool CanFire(TG.TriGrabPhase phase, bool hookOut, long tick, long stunUntilTick, long cooldownUntilTick)
            => phase == TG.TriGrabPhase.Live && !hookOut && tick >= stunUntilTick && tick >= cooldownUntilTick;

        /// <summary>보낼 이동. 손이 나가 있거나 기절 중이면 선다.</summary>
        public static Vector2 MoveFor(Vector2 worldMove, bool blocked) => blocked ? Vector2.zero : worldMove;

        /// <summary>
        /// 화면 위쪽이 가리키는 월드 yaw(도). 바로 위에서 보는 카메라는 앞(forward)이 땅을 향해 방향이 없으니 위(up)로 잰다.
        /// </summary>
        public static float ScreenYaw(Camera camera)
        {
            var t = camera.transform;
            var v = Mathf.Abs(t.forward.y) > 0.99f ? t.up : t.forward;
            return Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
        }

        /// <summary>화면 방향(위 = +y)을 월드 바닥 방향(x, z)으로.</summary>
        public static Vector2 ScreenToWorld(Vector2 screen, float screenYawDegrees)
            => KnockbackPadViewModel.AimDirection(screen, screenYawDegrees);

        /// <summary>입력이 실릴 틱(서버가 그 입력을 처리하는 틱과 같다).</summary>
        private long InputTick => input.NextInputTick;

        private TG.TriGrabPlayerScore Me => state.TryGetPlayer(playerContext.entityId, out var p) ? p : null;

        /// <summary>이번 입력 틱에 서버가 이동을 막는가 — 내 손이 나가 있거나 기절 중.</summary>
        public bool MoveBlocked
        {
            get
            {
                long tick = InputTick;
                var me = Me;
                return myHook.IsOut(playerContext.entityId, tick) || (me != null && tick < me.StunUntilTick);
            }
        }

        public bool Ready
        {
            get
            {
                long tick = InputTick;
                var me = Me;
                //  손 쉼은 서버 소식보다 먼저 안다 — 마지막 손이 돌아온 틱 + 쉼.
                long cooldown = System.Math.Max(me?.CooldownUntilTick ?? 0, LocalCooldownUntil(myHook.LastEndTick));
                return CanFire(state.PhaseAt(tick), myHook.IsOut(playerContext.entityId, tick), tick, me?.StunUntilTick ?? 0, cooldown);
            }
        }

        /// <summary>손이 돌아온 틱에 서버가 정하는 손 쉼 끝 틱.</summary>
        public static long LocalCooldownUntil(long lastEndTick)
            => lastEndTick == long.MinValue ? 0 : lastEndTick + TG.TriGrabConfig.HookCooldownTicks;

        /// <summary>방향 스틱(−1~1). 매 프레임 <see cref="Apply"/>가 막힘을 보고 보낸다.</summary>
        public void Move(Vector2 value) => stick = value;

        /// <summary>매 프레임 — 스틱 값을 화면 기준으로 돌려 보내되, 손이 나가 있거나 기절 중이면 0을 보낸다.</summary>
        public void Apply()
        {
            var camera = cameraController != null ? cameraController.MainCamera : null;
            var world = camera != null && stick != Vector2.zero ? ScreenToWorld(stick, ScreenYaw(camera)) : Vector2.zero;
            var send = MoveFor(world, MoveBlocked);
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

        /// <summary>끌기 방향(화면, 위 = +y)의 월드 yaw.</summary>
        public float? DragYaw(Vector2 screenDrag)
        {
            var camera = cameraController != null ? cameraController.MainCamera : null;
            return camera == null ? (float?)null : KnockbackPadViewModel.AimYaw(screenDrag, ScreenYaw(camera));
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
            return dir.sqrMagnitude > 1e-6f && Fire(TG.TriGrabMath.YawFromDir(new System.Numerics.Vector2(dir.x, dir.y)));
        }

        private bool Fire(float? yaw)
        {
            if (!yaw.HasValue || !Ready || !TryGetMyPosition(out var me))
            {
                return false;
            }
            long fireTick = InputTick;
            input.SetFire(yaw.Value);
            //  쏘는 틱부터 서야 서버가 재는 출발점(그 틱 이동 뒤 자리)이 지금 자리와 같다.
            input.SetMovement(0f, 0f);
            myHook.Fire(playerContext.entityId, me, yaw.Value, fireTick);
            return true;
        }

        /// <summary>조준선 출발점·방향(월드 xz). 끌기가 쏘기 거리 안(떼면 취소)이면 false.</summary>
        public bool TryGetAim(Vector2 screenDrag, float dragPx, out Vector2 origin, out Vector2 dir)
        {
            dir = Vector2.zero;
            if (!TryGetMyPosition(out origin) || OutcomeOnRelease(dragPx) != ReleaseOutcome.Fire) return false;
            var yaw = DragYaw(screenDrag);
            if (!yaw.HasValue) return false;
            var d = TG.TriGrabMath.DirFromYaw(yaw.Value);
            dir = new Vector2(d.X, d.Y);
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
