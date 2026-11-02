using System.Collections.Generic;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>Knockback 조작 — 이동 스틱과 발사 버튼. 스틱·화면 방향은 카메라 기준 월드 방향으로 돌려 입력 매니저로 넘긴다.</summary>
    public class KnockbackPadViewModel : System.IDisposable
    {
        public enum TapOrDrag { None, Tap, Drag }

        /// <summary>끌기로 치는 최소 거리(버튼 반경 대비 0~1).</summary>
        public const float DragThreshold = 0.3f;
        /// <summary>누른 뒤 이 시간 안에 뗐고 안 끌었으면 톡이다.</summary>
        public const float TapSeconds = 0.25f;
        /// <summary>자동 조준이 보는 거리. 탄 사거리(13)에 몸 크기 여유를 더했다.</summary>
        public const float AutoAimRange = 15f;

        private readonly PlayerInputManager input;
        private readonly CameraController cameraController;
        private readonly ActorRegistry actors;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly KnockbackClientState state;
        private readonly KnockbackConfig config;
        private readonly GameFramework.Runner.IRunner runner;
        private readonly KnockbackAimState aimState;
        private readonly IHaptics haptics;
        // 직전 프레임에 "지금 놓으면"의 결과. 취소 구간에 막 들어간 순간을 알아내려고 든다.
        private ReleaseOutcome previousOutcome = ReleaseOutcome.Nothing;

        private readonly List<Vector2> bodyScratch = new List<Vector2>();
        private readonly List<int> teamScratch = new List<int>();

        public KnockbackPadViewModel(PlayerInputManager input, CameraController cameraController,
                                     ActorRegistry actors, IPlayerContext playerContext,
                                     GameFramework.World.EntityRegistry entityRegistry,
                                     KnockbackClientState state, KnockbackConfig config,
                                     GameFramework.Runner.IRunner runner, KnockbackAimState aimState, IHaptics haptics)
        {
            this.haptics = haptics;
            this.aimState = aimState;
            this.input = input;
            this.cameraController = cameraController;
            this.actors = actors;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.state = state;
            this.config = config;
            this.runner = runner;
        }

        public int AmmoMax => config.AmmoMax;

        /// <summary>지금 쏠 수 있는 탄 수.</summary>
        public int Ammo => MyGun()?.Ammo ?? 0;

        /// <summary>다음 탄이 차기까지 0~1. 가득 차 있으면 0.</summary>
        public float ReloadProgress01
        {
            get
            {
                var gun = MyGun();
                if (gun == null || gun.Ammo >= config.AmmoMax || gun.NextReloadTick == long.MaxValue || runner?.tickUpdater == null)
                {
                    return 0f;
                }
                float total = config.Ticks(config.ReloadSeconds);
                float left = gun.NextReloadTick - runner.tickUpdater.tick;
                return Mathf.Clamp01(1f - left / Mathf.Max(1f, total));
            }
        }

        /// <summary>내 몸이 맞아서 날아가는 중이거나 바닥에 서 있지 않은가. 이동·발사 입력은 월드가 버리니 화면도 쏜 것처럼 보이지 않게 한다.</summary>
        public bool IsStunned
        {
            get
            {
                var entity = string.IsNullOrEmpty(playerContext.entityId) ? null : entityRegistry.Get(playerContext.entityId);
                var body = entity?.Get<KnockbackBody>();
                if (body == null || runner?.tickUpdater == null)
                {
                    return false;
                }
                var ground = entity.Get<GameFramework.World.GroundState>();
                return CannotAct(runner.tickUpdater.tick, body.KnockbackEndTick, ground == null || ground.IsGrounded);
            }
        }

        /// <summary>날려지는 중이거나 떨어지는 중이면 행동 불가 — 월드의 발사 가드와 같은 기준.</summary>
        public static bool CannotAct(long tick, long knockbackEndTick, bool grounded)
            => IsStunnedAt(tick, knockbackEndTick) || !grounded;

        /// <summary>날림 끝 틱 전이면 true(끝나는 그 틱부터는 다시 쏠 수 있다 — 월드와 같은 기준).</summary>
        public static bool IsStunnedAt(long tick, long knockbackEndTick) => tick < knockbackEndTick;

        /// <summary>방향 스틱. −1~1로 정규화된 값이 들어온다. 0을 넘겨야 몸이 선다(held 모델).</summary>
        public void Move(Vector2 stick)
        {
            var world = DodgePadViewModel.CameraRelative(stick, cameraController.MainCamera);
            input.SetMovement(world.x, world.y);
        }

        /// <summary>데스크톱용 WASD. 안 누르면 0을 밀어 선다.</summary>
        public void MoveByKeyboard()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            bool w = false, a = false, s = false, d = false;
            if (keyboard != null)
            {
                w = keyboard.wKey.isPressed;
                a = keyboard.aKey.isPressed;
                s = keyboard.sKey.isPressed;
                d = keyboard.dKey.isPressed;
            }
            Move(Combine(w, a, s, d));
        }

        /// <summary>화면 기준으로 끈 방향(위 = 화면 위쪽, y는 이미 뒤집음)으로 쏜다.</summary>
        public void FireTowardScreenDirection(Vector2 drag)
        {
            float? yaw = AimYaw(drag, cameraController.MainCamera.transform.eulerAngles.y);
            if (yaw.HasValue && !IsStunned)
            {
                input.SetFire(yaw.Value);
            }
        }

        /// <summary>
        /// 화면 끌기 → 월드 방향. 발사와 조준선이 같은 방향을 쓰도록 이 함수 하나만 거친다.
        /// 식은 <see cref="DodgePadViewModel.CameraRelative"/>와 같다(카메라의 좌우 회전만 본다).
        /// </summary>
        public static Vector2 AimDirection(Vector2 drag, float cameraYawDegrees)
        {
            if (drag == Vector2.zero)
            {
                return Vector2.zero;
            }
            Vector3 world = Quaternion.Euler(0f, cameraYawDegrees, 0f) * new Vector3(drag.x, 0f, drag.y);
            return new Vector2(world.x, world.z);
        }

        /// <summary>끈 방향의 yaw. 방향이 없으면 null.</summary>
        public static float? AimYaw(Vector2 drag, float cameraYawDegrees)
        {
            var dir = AimDirection(drag, cameraYawDegrees);
            return dir.sqrMagnitude > 1e-6f ? YawOf(dir) : (float?)null;
        }

        /// <summary>가장 가까운 살아 있는 적의 그려진 자리로 쏜다. 적이 없으면 아무 일도 없다.</summary>
        public bool FireAuto()
        {
            if (IsStunned || !TryFindAutoTarget(out var me, out var target))
            {
                return false;
            }
            input.SetFire(YawOf(target - me));
            return true;
        }

        // FireAuto와 조준 표시가 같은 대상을 고르도록 한 곳에서만 찾는다.
        private bool TryFindAutoTarget(out Vector2 me, out Vector2 target)
        {
            target = default;
            if (!TryGetMyPosition(out me))
            {
                return false;
            }
            bodyScratch.Clear();
            teamScratch.Clear();
            int myTeam = state.TeamCount >= 2 ? state.MyTeamOf(playerContext.entityId) : 0;
            foreach (var actor in actors.All)
            {
                if (actor.entityId == playerContext.entityId || state.IsEliminated(actor.entityId) || actor.visualGameObject == null)
                {
                    continue;
                }
                var p = actor.visualGameObject.transform.position;
                bodyScratch.Add(new Vector2(p.x, p.z));
                teamScratch.Add(state.TeamOf(actor.entityId));
            }
            var offset = AutoAimOffset(me, bodyScratch, teamScratch, myTeam, AutoAimRange);
            if (!offset.HasValue)
            {
                return false;
            }
            target = me + offset.Value;
            return true;
        }

        /// <summary>
        /// 발사 버튼을 잡고 있는 동안 매 프레임 불러 조준 표시 상태를 갱신한다. 손을 떼면 held=false로 불러 끈다.
        /// 표시는 "지금 놓으면 무슨 일이 일어나나"(<see cref="OutcomeOnRelease"/>)를 그대로 보여 준다.
        /// </summary>
        public void UpdateAim(bool held, float heldSeconds, float maxDrag01, Vector2 drag)
        {
            var camera = cameraController.MainCamera;
            if (!held || camera == null || IsStunned)
            {
                previousOutcome = ReleaseOutcome.Nothing;
                aimState.Clear();
                return;
            }
            var outcome = OutcomeOnRelease(heldSeconds, maxDrag01, drag.magnitude);
            if (ShouldPulseOnCancelEntry(previousOutcome, outcome))
            {
                haptics.Play(HapticPulse.Light);
            }
            previousOutcome = outcome;
            bool hasTarget = TryFindAutoTarget(out var me, out var target);
            float? yaw = AimYaw(drag, camera.transform.eulerAngles.y);
            var kind = AimIndicatorState(heldSeconds, maxDrag01, drag.magnitude, hasTarget);
            // 방향이 없으면(끌기 0) 조준선을 그릴 수 없다.
            if (kind == AimKind.Aim && !yaw.HasValue)
            {
                kind = AimKind.None;
            }
            aimState.Kind = kind;
            aimState.AimYaw = yaw ?? 0f;
            aimState.TargetXZ = target;
        }

        public enum AimKind { None, Cancel, Aim, AutoTarget }

        public enum ReleaseOutcome { Nothing, Auto, Fire, Cancel }

        /// <summary>
        /// 지금 손을 떼면 일어날 일. 놓는 처리(OnFireUp)와 조준 표시가 같은 함수를 쓴다.
        /// 끌기(최대 끌기가 임계 이상)면 현재 끌기가 임계 이상일 때만 발사, 아니면 취소.
        /// 안 끌고 짧게 눌렀으면 자동 조준 발사, 오래 눌렀으면 아무 일도 없다.
        /// </summary>
        public static ReleaseOutcome OutcomeOnRelease(float heldSeconds, float maxDrag01, float currentDrag01)
        {
            switch (Classify(heldSeconds, maxDrag01))
            {
                case TapOrDrag.Drag:
                    return currentDrag01 >= DragThreshold ? ReleaseOutcome.Fire : ReleaseOutcome.Cancel;
                case TapOrDrag.Tap:
                    return ReleaseOutcome.Auto;
                default:
                    return ReleaseOutcome.Nothing;
            }
        }

        /// <summary>
        /// 발사 → 취소로 막 바뀐 순간에만 true. 취소 구간에 들어갔다고 손끝으로 알려 주려는 것이다.
        /// 취소 구간에 머무는 동안이나 나올 때는 울리지 않고, 나갔다 다시 들어가면 또 울린다.
        /// </summary>
        public static bool ShouldPulseOnCancelEntry(ReleaseOutcome previous, ReleaseOutcome current)
            => previous == ReleaseOutcome.Fire && current == ReleaseOutcome.Cancel;

        /// <summary>
        /// 발사 영역이 받는 포인터인가. 마우스는 PC 클릭 발사 경로가 따로 처리해서 거르지만,
        /// <paramref name="mouseActsAsTouch"/>(에디터)면 마우스도 손가락처럼 받아 폰 조작을 PC에서 그대로 해 볼 수 있다.
        /// </summary>
        public static bool AcceptsFirePointer(string pointerType, bool mouseActsAsTouch)
            => pointerType != "mouse" || mouseActsAsTouch;

        /// <summary>조준 표시 종류. 놓았을 때의 결과(<see cref="OutcomeOnRelease"/>)를 표시로 옮긴 것.</summary>
        public static AimKind AimIndicatorState(float heldSeconds, float maxDrag01, float currentDrag01, bool hasTarget)
        {
            switch (OutcomeOnRelease(heldSeconds, maxDrag01, currentDrag01))
            {
                case ReleaseOutcome.Fire: return AimKind.Aim;
                case ReleaseOutcome.Cancel: return AimKind.Cancel;
                case ReleaseOutcome.Auto: return hasTarget ? AimKind.AutoTarget : AimKind.None;
                default: return AimKind.None;
            }
        }

        /// <summary>마우스가 가리키는 바닥점 쪽으로 쏜다(PC).</summary>
        public void FireAtGroundPoint(Vector2 screenPosition)
        {
            var camera = cameraController.MainCamera;
            if (camera == null || IsStunned || !TryGetMyPosition(out var me))
            {
                return;
            }
            var ray = camera.ScreenPointToRay(screenPosition);
            if (Mathf.Abs(ray.direction.y) < 1e-4f)
            {
                return;
            }
            float t = -ray.origin.y / ray.direction.y;
            if (t <= 0f)
            {
                return;
            }
            var hit = ray.origin + ray.direction * t;
            var dir = new Vector2(hit.x - me.x, hit.z - me.y);
            if (dir.sqrMagnitude > 1e-6f)
            {
                input.SetFire(YawOf(dir));
            }
        }

        private bool TryGetMyPosition(out Vector2 position)
        {
            position = default;
            string id = playerContext.entityId;
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }
            // 탄은 서버가 재는 자리(시뮬 위치)에서 나가야 하므로 그려진 몸이 아니라 월드 값을 쓴다.
            // 보정 직후 0.1초 동안은 그려진 몸과 조금 떨어져 있을 수 있다(소스 엔진도 같다).
            var body = entityRegistry.Get(id);
            if (body == null)
            {
                return false;
            }
            var p = GameFramework.World.EntityMotionExtensions.GetPosition(body);
            position = new Vector2(p.x, p.z);
            return true;
        }

        private KnockbackGun MyGun()
            => string.IsNullOrEmpty(playerContext.entityId) ? null : entityRegistry.Get(playerContext.entityId)?.Get<KnockbackGun>();

        /// <summary>yaw 0 = +z, 90 = +x (도).</summary>
        internal static float YawOf(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

        /// <summary>사거리 안에서 가장 가까운 적 쪽의 yaw. 없으면 null. 같은 자리의 적은 방향이 없어 거른다.</summary>
        public static float? AutoAimYaw(Vector2 me, IReadOnlyList<Vector2> enemies, float maxRange)
            => AutoAimYaw(me, enemies, null, 0, maxRange);

        /// <summary>
        /// 위와 같되 내 팀(myTeam)과 같은 팀인 몸은 건너뛴다. teams는 bodies와 같은 순서(null = 모두 적).
        /// </summary>
        public static Vector2? AutoAimOffset(Vector2 me, IReadOnlyList<Vector2> bodies, IReadOnlyList<int> teams, int myTeam, float maxRange)
        {
            float best = maxRange * maxRange;
            Vector2? target = null;
            for (int i = 0; i < bodies.Count; i++)
            {
                if (teams != null && KnockbackTeams.SameTeam(myTeam, teams[i]))
                {
                    continue;
                }
                var d = bodies[i] - me;
                float sq = d.sqrMagnitude;
                if (sq < 1e-6f || sq > best)
                {
                    continue;
                }
                best = sq;
                target = d;
            }
            return target;
        }

        public static float? AutoAimYaw(Vector2 me, IReadOnlyList<Vector2> bodies, IReadOnlyList<int> teams, int myTeam, float maxRange)
        {
            var offset = AutoAimOffset(me, bodies, teams, myTeam, maxRange);
            return offset.HasValue ? YawOf(offset.Value) : (float?)null;
        }

        /// <summary>
        /// 버튼에서 뗀 순간의 분류. 한 번이라도 끌었으면 끌기, 안 끌고 짧게 눌렀으면 톡,
        /// 안 끌고 오래 눌렀으면 아무것도 아니다(망설이다 뗀 것이라 쏘지 않는다).
        /// </summary>
        public static TapOrDrag Classify(float heldSeconds, float maxDrag01)
        {
            if (maxDrag01 >= DragThreshold)
            {
                return TapOrDrag.Drag;
            }
            return heldSeconds <= TapSeconds ? TapOrDrag.Tap : TapOrDrag.None;
        }

        /// <summary>누른 키들을 한 방향으로. 대각선도 길이 1이라 빨라지지 않는다.</summary>
        internal static Vector2 Combine(bool w, bool a, bool s, bool d)
        {
            var dir = Vector2.zero;
            if (w) dir.y += 1f;
            if (s) dir.y -= 1f;
            if (d) dir.x += 1f;
            if (a) dir.x -= 1f;
            return dir == Vector2.zero ? Vector2.zero : dir.normalized;
        }

        public void Dispose()
        {
            aimState.Clear();
        }
    }
}
