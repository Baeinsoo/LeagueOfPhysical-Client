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

        private readonly List<Vector2> bodyScratch = new List<Vector2>();
        private readonly List<int> teamScratch = new List<int>();

        public KnockbackPadViewModel(PlayerInputManager input, CameraController cameraController,
                                     ActorRegistry actors, IPlayerContext playerContext,
                                     GameFramework.World.EntityRegistry entityRegistry,
                                     KnockbackClientState state, KnockbackConfig config,
                                     GameFramework.Runner.IRunner runner, KnockbackAimState aimState)
        {
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
            if (yaw.HasValue)
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
            if (!TryFindAutoTarget(out var me, out var target))
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
        /// </summary>
        public void UpdateAim(bool held, Vector2 drag, bool everDragged)
        {
            if (!held)
            {
                aimState.Clear();
                return;
            }
            bool hasTarget = TryFindAutoTarget(out var me, out var target);
            var kind = AimIndicatorState(drag.magnitude, true, hasTarget, everDragged);
            aimState.Kind = kind;
            aimState.AimYaw = AimYaw(drag, cameraController.MainCamera.transform.eulerAngles.y) ?? 0f;
            aimState.TargetXZ = target;
        }

        public enum AimKind { None, Cancel, Aim, AutoTarget }

        /// <summary>이만큼 이상 끌어야 "끌었다"로 본다. 손가락이 살짝 떨리는 것까지 끌기로 치지 않게.</summary>
        public const float AimDeadZone01 = 0.05f;

        /// <summary>
        /// 조준 표시 종류. 놓으면 쏘는 끌기(임계 이상) = Aim, 끌었지만 임계 미만이라 놓으면 취소 = Cancel,
        /// 아직 안 끌고 누르고만 있음 = 자동 조준 대상(없으면 None).
        /// everDragged: 끌었다가 가운데로 되돌려 놓은 경우도 취소라서 Cancel로 둔다.
        /// </summary>
        public static AimKind AimIndicatorState(float drag01, bool held, bool hasTarget, bool everDragged = false)
        {
            if (!held)
            {
                return AimKind.None;
            }
            if (drag01 >= DragThreshold)
            {
                return AimKind.Aim;
            }
            if (everDragged || drag01 > AimDeadZone01)
            {
                return AimKind.Cancel;
            }
            return hasTarget ? AimKind.AutoTarget : AimKind.None;
        }

        /// <summary>마우스가 가리키는 바닥점 쪽으로 쏜다(PC).</summary>
        public void FireAtGroundPoint(Vector2 screenPosition)
        {
            var camera = cameraController.MainCamera;
            if (camera == null || !TryGetMyPosition(out var me))
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
