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

        private readonly List<Vector2> enemyScratch = new List<Vector2>();

        public KnockbackPadViewModel(PlayerInputManager input, CameraController cameraController,
                                     ActorRegistry actors, IPlayerContext playerContext,
                                     GameFramework.World.EntityRegistry entityRegistry,
                                     KnockbackClientState state, KnockbackConfig config,
                                     GameFramework.Runner.IRunner runner)
        {
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
            var dir = DodgePadViewModel.CameraRelative(drag, cameraController.MainCamera);
            if (dir.sqrMagnitude > 1e-6f)
            {
                input.SetFire(YawOf(dir));
            }
        }

        /// <summary>가장 가까운 살아 있는 적의 그려진 자리로 쏜다. 적이 없으면 아무 일도 없다.</summary>
        public bool FireAuto()
        {
            if (!TryGetMyPosition(out var me))
            {
                return false;
            }
            enemyScratch.Clear();
            foreach (var actor in actors.All)
            {
                if (actor.entityId == playerContext.entityId || state.IsEliminated(actor.entityId) || actor.visualGameObject == null)
                {
                    continue;
                }
                var p = actor.visualGameObject.transform.position;
                enemyScratch.Add(new Vector2(p.x, p.z));
            }
            float? yaw = AutoAimYaw(me, enemyScratch, AutoAimRange);
            if (!yaw.HasValue)
            {
                return false;
            }
            input.SetFire(yaw.Value);
            return true;
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
        {
            float best = maxRange * maxRange;
            Vector2? target = null;
            for (int i = 0; i < enemies.Count; i++)
            {
                var d = enemies[i] - me;
                float sq = d.sqrMagnitude;
                if (sq < 1e-6f || sq > best)
                {
                    continue;
                }
                best = sq;
                target = d;
            }
            return target.HasValue ? YawOf(target.Value) : (float?)null;
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
        }
    }
}
