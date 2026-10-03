namespace LOP.UI
{
    /// <summary>
    /// Dodge 조작 — 이동 하나. 스틱 값을 카메라 기준 방향으로 돌려 입력 매니저로 넘기고,
    /// 내 목숨을 서버 상태에서 읽어 보여 준다.
    /// </summary>
    public class DodgePadViewModel : System.IDisposable
    {
        private readonly PlayerInputManager input;
        private readonly CameraController cameraController;
        private readonly DodgeClientState state;
        private readonly IGameDataStore gameDataStore;
        private readonly GameFramework.Runner.IRunner runner;
        private readonly GameFramework.World.IWorld world;
        private readonly DodgeConfig config;
        private readonly DodgeStageTable stages;
        private readonly DodgeCaptionDirector captionDirector;
        private readonly DodgeCaptionQueue captionQueue = new DodgeCaptionQueue();
        private readonly System.Collections.Generic.List<(string id, bool eliminated)> roster =
            new System.Collections.Generic.List<(string, bool)>();
        private readonly System.Collections.Generic.List<string> newLines = new System.Collections.Generic.List<string>();
        private readonly R3.ReactiveProperty<(int full, int empty, string note)> lives = new R3.ReactiveProperty<(int, int, string)>((0, 0, ""));
        private readonly R3.ReactiveProperty<string> stageText = new R3.ReactiveProperty<string>("");
        private readonly R3.ReactiveProperty<float> stageProgress = new R3.ReactiveProperty<float>(0f);
        private readonly R3.ReactiveProperty<bool> suddenDeath = new R3.ReactiveProperty<bool>(false);
        private readonly R3.ReactiveProperty<string> captionText = new R3.ReactiveProperty<string>("");
        private readonly R3.ReactiveProperty<bool> hitFlash = new R3.ReactiveProperty<bool>(false);
        private bool knewLives;
        private int lastLives;
        private long hitTick = -1;

        public DodgePadViewModel(PlayerInputManager input, CameraController cameraController,
                                 DodgeClientState state, IGameDataStore gameDataStore,
                                 GameFramework.Runner.IRunner runner, GameFramework.World.IWorld world,
                                 DodgeConfig config, DodgeStageTable stages, DodgeCaptionDirector captionDirector)
        {
            this.input = input;
            this.cameraController = cameraController;
            this.state = state;
            this.gameDataStore = gameDataStore;
            this.runner = runner;
            this.world = world;
            this.config = config;
            this.stages = stages;
            this.captionDirector = captionDirector;
        }

        public R3.ReadOnlyReactiveProperty<(int full, int empty, string note)> LivesProperty => lives;
        public R3.ReadOnlyReactiveProperty<string> StageTextProperty => stageText;
        public R3.ReadOnlyReactiveProperty<float> StageProgressProperty => stageProgress;
        public R3.ReadOnlyReactiveProperty<bool> SuddenDeathProperty => suddenDeath;
        public R3.ReadOnlyReactiveProperty<string> CaptionTextProperty => captionText;
        public R3.ReadOnlyReactiveProperty<bool> HitFlashProperty => hitFlash;

        /// <summary>매 프레임 서버 상태에서 내 목숨을 읽는다(연속 상태는 pull). 몸이 사라져도 id는 userEntityId로 남는다.</summary>
        public void Refresh()
        {
            bool known = state.TryGetLife(gameDataStore.userEntityId, out int lives, out long eliminatedTick);
            this.lives.Value = Hearts(known, lives, eliminatedTick, config.Lives);

            // 목숨이 준 순간을 맞은 틱으로 — 서버 상태가 알려 준 뒤라 조금 늦지만 되돌릴 일은 없다(스펙 §5.4).
            long now = runner?.tickUpdater?.tick ?? 0;
            hitTick = DodgeHitFeedback.NextHitTick(knewLives, lastLives, lives, hitTick, now);
            knewLives = known;
            lastLives = lives;
            hitFlash.Value = DodgeHitFeedback.Flashing(now, hitTick);

            // 스테이지는 표와 경기 시작 틱으로 서버와 같은 식을 계산한다 — 와이어로 오지 않는다.
            long tick = runner?.tickUpdater?.tick ?? 0;
            var at = stages.At(tick, world.GameplayStartTick, config);
            var hud = StageHud(at, stages);
            stageText.Value = hud.label;
            stageProgress.Value = hud.progress;
            suddenDeath.Value = hud.suddenDeath;

            // 중계 자막 — 새로 생긴 사건만 줄을 세운다(테마 스펙 §4).
            roster.Clear();
            foreach (var id in state.PlayerIds)
            {
                state.TryGetLife(id, out _, out long out_);
                roster.Add((id, out_ >= 0));
            }
            newLines.Clear();
            captionDirector.Observe(at, roster, gameDataStore.userEntityId, newLines);
            foreach (var line in newLines) captionQueue.Push(line);
            captionText.Value = captionQueue.Tick(tick / (double)DodgeConfig.TicksPerSecond);
        }

        /// <summary>하트를 늘어놓는 최대 개수. 시험용으로 목숨을 크게 잡으면 하트 하나에 숫자를 붙인다.</summary>
        public const int MaxHearts = 8;

        /// <summary>찬 하트·빈 하트(잃은 목숨) 개수와 곁들일 글. 처음 목숨(maxLives)을 넘는 일은 없다.</summary>
        public static (int full, int empty, string note) Hearts(bool known, int lives, long eliminatedTick, int maxLives)
        {
            if (!known) return (0, 0, "");
            if (eliminatedTick >= 0) return (0, 0, "탈락 — 관전 중");
            lives = System.Math.Max(0, lives);
            if (maxLives > MaxHearts) return (1, 0, $"×{lives}");
            return (lives, System.Math.Max(0, maxLives - lives), "");
        }

        /// <summary>위쪽 막대 — 몇 번째 스테이지인지와 얼마나 지났는지. 사건 알림은 중계 자막이 한다.</summary>
        public static (string label, float progress, bool suddenDeath) StageHud(in DodgeStagePoint at, DodgeStageTable stages)
        {
            if (!at.Started)
            {
                return ("", 0f, false);
            }
            if (at.SuddenDeath)
            {
                return ("서든데스", 1f, true);
            }
            return ($"{at.Index + 1}/{stages.Count} {stages[at.Index].Name}", at.Progress, false);
        }

        public void Dispose()
        {
            lives.Dispose();
            stageText.Dispose();
            stageProgress.Dispose();
            suddenDeath.Dispose();
            captionText.Dispose();
            hitFlash.Dispose();
        }

        /// <summary>방향 스틱. −1~1로 정규화된 값이 들어온다. 0을 넘겨야 몸이 선다(held 모델).</summary>
        public void Move(UnityEngine.Vector2 stick)
        {
            var world = CameraRelative(stick, cameraController.MainCamera);
            input.SetMovement(world.x, world.y);
        }

        /// <summary>스틱 → 월드 xz. 식은 SkydivePadViewModel.Move와 같다 — 화면에서 위로 밀면 화면 위쪽으로 간다.</summary>
        public static UnityEngine.Vector2 CameraRelative(UnityEngine.Vector2 stick, UnityEngine.Camera camera)
        {
            // 멈춤은 방향이 없다 — 카메라를 안 읽어야 씬을 내리며 카메라가 먼저 사라져도 닫힐 때 멈출 수 있다.
            if (stick == UnityEngine.Vector2.zero)
            {
                return UnityEngine.Vector2.zero;
            }
            float yAngle = camera.transform.eulerAngles.y;
            var cameraRotation = UnityEngine.Quaternion.Euler(0, yAngle, 0);
            UnityEngine.Vector3 transformed = cameraRotation * new UnityEngine.Vector3(stick.x, 0, stick.y);
            return new UnityEngine.Vector2(transformed.x, transformed.z);
        }

        /// <summary>데스크톱용 WASD. 안 누르면 0을 밀어 선다.</summary>
        public void MoveByKeyboard()
        {
            var dir = UnityEngine.Vector2.zero;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) { dir.y += 1f; }
                if (keyboard.sKey.isPressed) { dir.y -= 1f; }
                if (keyboard.dKey.isPressed) { dir.x += 1f; }
                if (keyboard.aKey.isPressed) { dir.x -= 1f; }
            }

            // 대각선으로 눌러도 빨라지지 않게 — 스틱은 반지름으로 이미 잘린다.
            Move(dir == UnityEngine.Vector2.zero ? UnityEngine.Vector2.zero : dir.normalized);
        }
    }
}
