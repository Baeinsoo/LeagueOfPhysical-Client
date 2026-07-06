# 멀티틱 프레임 입력 기아 수정 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 이동 입력을 "프레임당 일회성 캡처"에서 "held 상태를 매 틱 샘플"로 바꿔, 멀티틱 프레임의 모든 틱이 held 이동을 받게 해 걷기 중 velocity=0 깜빡임을 없앤다.

**Architecture:** `PlayerInputManager`가 held 이동(`heldHorizontal/heldVertical`, 연속)과 pending 액션(`pendingJump/pendingAbilityId`, 이산)을 분리 보유한다. 입력 소스(GamePad)는 매 프레임 현재 이동을 `SetMovement`로 push(뗄 때 0 포함). `ProcessInput`은 매 틱 held 이동 + pending 액션으로 커맨드를 만들어 enqueue·전송·기록하고, 이산 액션만 소비한다. 서버는 tick-키 소비라 무변경.

**Tech Stack:** C# / Unity / Unity Input System / VContainer / Mirror 넷코드. 입력 도메인 타입은 LOP-Shared(`InputCommand`/`InputBufferSystem`).

## Global Constraints

- **범위 = 클라 단독** (LOP-Client). 서버·LOP-Shared·렌더 스무딩 브랜치 무변경. `MovementSystem`(모터) 무변경.
- **이동만 held-per-tick**. 점프/어빌리티는 기존 일회성 유지(이번 범위 밖).
- **`InputCommand` 필드(LOP-Shared, 변경 금지)**: `long SequenceNumber`, `float Horizontal`, `float Vertical`, `bool Jump`, `int AbilityId`.
- **`InputBufferSystem` API(LOP-Shared, 변경 금지)**: `bool Enqueue(InputBuffer, long tick, InputCommand)`(seq/tick dedup), `void SetCurrent(InputBuffer, InputCommand)`, `void TrimToWindow(InputBuffer, int)`.
- **입력 소스는 매 프레임 정확히 한 번 현재 이동을 push**(입력 없으면 `SetMovement(0,0)`) — 이게 held 모델의 핵심 전제(뗄 때 0 안 밀면 캐릭이 안 멈춤).
- **`c188c03` parity 유지**: 진짜 release 틱만 무입력(held=0)이 되어 클·서 둘 다 제동. 걷는 중엔 held 이동이 매 틱 전송돼 서버도 매 틱 걷는다.
- **테스트 제약**: 이 로직은 클라 Assembly-CSharp라 EditMode 유닛 테스트 불가. 검증 = 객관 진단로그(`[INPUT]` 매 walk 틱 hasMovement + `[RUN]` velocity=0 소멸) + 회귀 육안. standalone .NET TDD는 별도 이니셔티브(미설치). `InputBufferSystem`(enqueue/dedup)은 이미 Shared EditMode 테스트 있고 무변경.
- **World 타입은 풀 네임스페이스 한정 / 주석 최소·일상어** (CLAUDE.md).
- **작업 브랜치** `fix/multi-tick-input-starvation` (main 기준). main 직접 커밋 금지.

---

### Task 1: 이동 held 모델 전환 + GamePad 매 프레임 push (+ 임시 진단)

**Files:**
- Modify: `Assets/Scripts/Game/PlayerInputManager.cs` (held 이동 + 매 틱 샘플)
- Modify: `Assets/Scripts/UI/GamePad/GamePadViewModel.cs` (매 프레임 push, 0 포함)
- Modify: `Assets/Scripts/UI/GamePad/GamePadView.cs` (프레임 구동부: 항상 push)
- Modify(임시 진단): `Assets/Scripts/Entity/LOPEntityView.cs` ([RUN] 로그)

**Interfaces:**
- Produces (GamePad→PlayerInputManager): `void SetMovement(float horizontal, float vertical)` — held 이동 갱신. `void SetJump(bool)`, `void SetAbilityId(int)` — pending 이산(기존 유지).
- Consumes: `InputCommand`(필드 위), `InputBufferSystem.Enqueue/SetCurrent/TrimToWindow`, `AbilitySystem.HasActiveMotionEffect(worldEntity)`, `inputHistory.Record(tick, cmd)`, `abilityActivator.TryActivate(entityId, abilityId, tick)`, `Runner.Time.tick`.

> 이 태스크는 EditMode 유닛 테스트가 불가(클라 Assembly-CSharp)하므로 TDD RED/GREEN 단계 대신 **컴파일 통과 + 임시 진단 삽입**으로 마치고, 실제 검증은 Task 2(플레이)가 진단로그로 수행한다.

- [ ] **Step 1: `PlayerInputManager` — held 이동 필드로 교체 + ProcessInput 재작성**

`Assets/Scripts/Game/PlayerInputManager.cs`에서 (a) 필드, (b) `ProcessInput`, (c) 세터를 아래로 교체.

(1a) 기존 필드
```csharp
        private long sequenceNumber;
        private InputCommand captured;   // 틱 사이 UI 입력 캡처(null = 이번 틱 입력 없음)
```
→ 신규
```csharp
        private long sequenceNumber;
        private float heldHorizontal;   // 연속 이동 — 입력 소스가 매 프레임 갱신(뗄 때 0), 틱마다 샘플
        private float heldVertical;
        private bool pendingJump;        // 이산 액션 — 소비 후 리셋
        private int pendingAbilityId;
```

(1b) 기존 `ProcessInput`(전체, `[RunnerListen(typeof(ProcessInput))] private void ProcessInput() { ... }`)를 아래로 교체
```csharp
        [RunnerListen(typeof(ProcessInput))]
        private void ProcessInput()
        {
            if (playerContext.entity == null)
            {
                return;
            }

            var worldEntity = entityRegistry.Get(playerContext.entity.entityId);
            var buffer = worldEntity.Get<InputBuffer>();
            long tick = Runner.Time.tick;

            bool hasMovement = heldHorizontal != 0f || heldVertical != 0f;
            bool hasAction = pendingJump || pendingAbilityId != 0;

            // [INPUT-DIAG 임시] 걷는 중 모든 틱이 이동을 받는지 확인(멀티틱 둘째 틱 포함). Task 2 후 제거.
            UnityEngine.Debug.Log($"[INPUT] frame={UnityEngine.Time.frameCount} tick={tick} hasMove={hasMovement} hasAction={hasAction}");

            if (hasMovement || hasAction)
            {
                var command = new InputCommand
                {
                    Horizontal = heldHorizontal,
                    Vertical = heldVertical,
                    Jump = pendingJump,
                    AbilityId = pendingAbilityId,
                };

                // 대시 등 조작 불가 상태에선 이동 입력을 무시한다(전송·예측 모두 0 → 보정 간섭 방지).
                if (AbilitySystem.HasActiveMotionEffect(worldEntity))
                {
                    command.Horizontal = 0f;
                    command.Vertical = 0f;
                }
                command.SequenceNumber = GenerateSequenceNumber();

                // 스트림에 저장(redundancy 윈도우) + 이번 틱 예측 확정(world.Tick의 MovementSystem이 읽음).
                inputBufferSystem.Enqueue(buffer, tick, command);
                inputBufferSystem.SetCurrent(buffer, command);
                inputBufferSystem.TrimToWindow(buffer, RedundancyWindow);

                SendToServer(buffer, tick, command);

                // 어빌리티 예측 발동(연출 cue는 AbilityActivator가 내부에서 append).
                if (command.AbilityId != 0)
                {
                    abilityActivator.TryActivate(playerContext.entity.entityId, command.AbilityId, tick);
                }

                inputHistory.Record(tick, command);

                // 이산 액션만 소비 — held 이동은 다음 틱까지 유지(연속).
                pendingJump = false;
                pendingAbilityId = 0;
            }
            else
            {
                // 무입력 틱(held=0, 액션 없음): 0 커맨드 확정 → MovementSystem이 수평 속도를 0으로 제동.
                var noInput = new InputCommand();
                inputBufferSystem.SetCurrent(buffer, noInput);
                inputHistory.Record(tick, noInput);

                if (buffer.Commands.Count > 0)
                {
                    // 무입력 틱에도 최근 입력 윈도우를 재전송해 연속 스트림을 유지한다(유실 입력이 1틱 내 재도착해 복구).
                    SendToServer(buffer, tick, null);
                }
            }
        }
```

(1c) 기존 세터 3~4개(`EnsureCaptured`, `SetHorizontal`, `SetVertical`, `SetJump`, `SetAbilityId`)
```csharp
        private InputCommand EnsureCaptured()
        {
            return captured ??= new InputCommand();
        }

        public void SetHorizontal(float horizontal)
        {
            EnsureCaptured().Horizontal = horizontal;
        }

        public void SetVertical(float vertical)
        {
            EnsureCaptured().Vertical = vertical;
        }

        public void SetJump(bool jump)
        {
            EnsureCaptured().Jump = jump;
        }

        public void SetAbilityId(int abilityId)
        {
            EnsureCaptured().AbilityId = abilityId;
        }
```
→ 신규 (이동=held 세터, 액션=pending 세터)
```csharp
        /// <summary>held 이동 갱신 — 입력 소스가 매 프레임 호출(뗄 때 0). 틱마다 샘플된다.</summary>
        public void SetMovement(float horizontal, float vertical)
        {
            heldHorizontal = horizontal;
            heldVertical = vertical;
        }

        public void SetJump(bool jump)
        {
            pendingJump = jump;
        }

        public void SetAbilityId(int abilityId)
        {
            pendingAbilityId = abilityId;
        }
```

- [ ] **Step 2: `GamePadViewModel` — 매 프레임 현재 이동을 push(0 포함)**

`Assets/Scripts/UI/GamePad/GamePadViewModel.cs`에서 `FeedMove`와 `TryFeedKeyboardMove`를 교체.

기존
```csharp
        /// <summary>
        /// 누르고 있는 동안 매 프레임 호출. 엔진이 매 틱 입력을 소비·clear하므로 지속 이동엔 매 프레임 set이 필요.
        /// 입력 벡터를 카메라 Y회전 기준으로 변환해 수평/수직 입력으로 넘긴다.
        /// </summary>
        public void FeedMove()
        {
            if (_moveInput == Vector2.zero)
            {
                return;
            }

            float yAngle = _cameraController.MainCamera.transform.eulerAngles.y;
            Quaternion cameraRotation = Quaternion.Euler(0, yAngle, 0);
            Vector3 transformedInput = cameraRotation * new Vector3(_moveInput.x, 0, _moveInput.y);

            _playerInputManager.SetHorizontal(transformedInput.x);
            _playerInputManager.SetVertical(transformedInput.z);
        }
```
→ 신규
```csharp
        /// <summary>조이스틱 held 이동을 매 프레임 push(센터=0 포함). held 모델: 뗄 때 0을 밀어야 캐릭이 멈춘다.</summary>
        public void FeedMove()
        {
            PushMovement(_moveInput);
        }

        // 원시 이동 벡터를 카메라 Y회전 기준으로 변환해 held 이동으로 넘긴다(0이면 0 그대로 → 정지 신호).
        private void PushMovement(Vector2 rawMove)
        {
            float yAngle = _cameraController.MainCamera.transform.eulerAngles.y;
            Quaternion cameraRotation = Quaternion.Euler(0, yAngle, 0);
            Vector3 transformedInput = cameraRotation * new Vector3(rawMove.x, 0, rawMove.y);

            _playerInputManager.SetMovement(transformedInput.x, transformedInput.z);
        }
```

기존
```csharp
        /// <summary>WASD 키 이동. 눌린 방향이 있으면 단위 벡터로 이동을 피드하고 true를 반환(조이스틱 미사용 시 호출).</summary>
        public bool TryFeedKeyboardMove()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return false;
            }

            Vector2 dir = Vector2.zero;
            if (kb.wKey.isPressed) dir.y += 1f;
            if (kb.sKey.isPressed) dir.y -= 1f;
            if (kb.dKey.isPressed) dir.x += 1f;
            if (kb.aKey.isPressed) dir.x -= 1f;

            if (dir == Vector2.zero)
            {
                return false;
            }

            SetMove(dir.normalized);
            FeedMove();
            return true;
        }
```
→ 신규
```csharp
        /// <summary>WASD held 이동을 매 프레임 push(안 누르면 0). 조이스틱 미사용 시 호출.</summary>
        public void FeedKeyboardMove()
        {
            Vector2 dir = Vector2.zero;
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed) dir.y += 1f;
                if (kb.sKey.isPressed) dir.y -= 1f;
                if (kb.dKey.isPressed) dir.x += 1f;
                if (kb.aKey.isPressed) dir.x -= 1f;
            }

            PushMovement(dir == Vector2.zero ? Vector2.zero : dir.normalized);
        }
```

- [ ] **Step 3: `GamePadView.Tick` — 항상 한 번 push (조이스틱/WASD)**

`Assets/Scripts/UI/GamePad/GamePadView.cs`의 `Tick` 교체.

기존
```csharp
        private void Tick(TimerState _)
        {
            _viewModel.PollKeyboard();
            if (_joystickPointerId != -1)
            {
                _viewModel.FeedMove(); // 조이스틱 우선
            }
            else
            {
                _viewModel.TryFeedKeyboardMove(); // WASD 이동
            }
        }
```
→ 신규 (둘 다 이제 매 프레임 push, 입력 없으면 0)
```csharp
        private void Tick(TimerState _)
        {
            _viewModel.PollKeyboard();
            if (_joystickPointerId != -1)
            {
                _viewModel.FeedMove();         // 조이스틱 (센터=0 포함 push)
            }
            else
            {
                _viewModel.FeedKeyboardMove();  // WASD (안 누르면 0 push)
            }
        }
```

- [ ] **Step 4: `LOPEntityView` — [RUN] 임시 진단 삽입**

`Assets/Scripts/Entity/LOPEntityView.cs`의 `UpdateRunAnimation` 마지막 부분 교체.

기존
```csharp
            const float walkThreshold = 0.01f;
            float horizontalSpeedSquared = entity.velocity.x * entity.velocity.x + entity.velocity.z * entity.velocity.z;
            animator.SetBool("Run", horizontalSpeedSquared > walkThreshold * walkThreshold && entity.IsGrounded());
        }
```
→ 신규
```csharp
            const float walkThreshold = 0.01f;
            float horizontalSpeedSquared = entity.velocity.x * entity.velocity.x + entity.velocity.z * entity.velocity.z;
            bool fast = horizontalSpeedSquared > walkThreshold * walkThreshold;
            bool grounded = entity.IsGrounded();
            bool run = fast && grounded;

            // [RUN-DIAG 임시] velocity=0 깜빡임(가짜 무입력) 소멸 확인용. Task 2 후 제거.
            if (run != _lastRunDiag)
            {
                UnityEngine.Debug.Log($"[RUN] {(run ? "RUN" : "IDLE")} spd={Mathf.Sqrt(horizontalSpeedSquared):F3} fast={fast} grounded={grounded}");
                _lastRunDiag = run;
            }

            animator.SetBool("Run", run);
        }

        private bool _lastRunDiag;
```

- [ ] **Step 5: 컴파일 확인**

UnityMCP `refresh_unity` 후 `read_console`(unity_instance=클라, hash는 `mcpforunity://instances`에서 name=`LeagueOfPhysical-Client`로 resolve — 작성 시 `de70658b9450cbb4`)로 컴파일 에러 0 확인. 특히 `SetHorizontal/SetVertical`·`TryFeedKeyboardMove` 잔존 참조 없는지(grep `SetHorizontal\|SetVertical\|TryFeedKeyboardMove`로 0건 확인).
기대: 컴파일 에러 0.

- [ ] **Step 6: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/PlayerInputManager.cs Assets/Scripts/UI/GamePad/GamePadViewModel.cs Assets/Scripts/UI/GamePad/GamePadView.cs Assets/Scripts/Entity/LOPEntityView.cs
git commit -m "$(cat <<'EOF'
feat(input): 이동을 held 상태로 매 틱 샘플 (멀티틱 프레임 입력 기아 수정)

이동 입력을 프레임당 일회성 캡처→held 상태로 전환, ProcessInput이 매 틱
held 이동으로 커맨드 생성(멀티틱 둘째 틱도). GamePad는 매 프레임 이동 push(0 포함).
액션(점프/어빌리티)은 일회성 유지. [INPUT]/[RUN] 임시 진단 포함(검증 후 제거).

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: 플레이 검증 + 진단로그 제거 (사용자 수행)

**Files:** (검증 후) Modify: `PlayerInputManager.cs`([INPUT] 제거), `LOPEntityView.cs`([RUN] 제거)

클라 Assembly-CSharp라 EditMode 불가 — 진단로그가 객관 검증 도구. RTT는 Mirror LatencySimulation 주입.

- [ ] **Step 1: 두 에디터 접속, RTT 300ms, 콘솔 클리어.**
- [ ] **Step 2: 방향키 꾹 누르고 5~10초 걷기.** `[INPUT]` 로그 확인 — 걷는 동안 **모든 틱이 `hasMove=True`** 여야 함(특히 같은 `frame=`에 틱 2개일 때 **둘째도 True**). 이전엔 둘째가 False였음.
- [ ] **Step 3: `[RUN]` 로그 확인 — 걷는 동안 `IDLE spd=0.000`(가짜 무입력) 이 안 나와야 함.** 육안으로 걷기 위치 멈춤·Idle 애니 깜빡임 소멸.
- [ ] **Step 4: 회귀 확인** — ① 방향키 떼면 즉시 정지(칼정지), ② 대시 중 이동 입력락 유지, ③ 점프/어빌리티 1회만 발동(연타 아님), ④ reconcile 정상(러버밴딩 없음, 걷기 중 seq per-tick 여파 확인).
- [ ] **Step 5: RTT 50ms에서도 재확인.**
- [ ] **Step 6: 진단로그 제거 + 커밋.** `PlayerInputManager.cs`의 `[INPUT]` Debug.Log 한 줄, `LOPEntityView.cs`의 `[RUN]` 블록 + `_lastRunDiag` 필드 제거(원상). 컴파일 확인 후:
```bash
git add Assets/Scripts/Game/PlayerInputManager.cs Assets/Scripts/Entity/LOPEntityView.cs
git commit -m "chore(input): 멀티틱 입력 기아 진단로그 제거 (검증 완료)"
```

기대: 모든 walk 틱 hasMove=True + velocity=0 깜빡임 소멸 + 회귀 없음. 검증 후 `finishing-a-development-branch`로 main 머지.

---

## Self-Review

**1. Spec coverage:**
- 이동 held 필드 + 매 틱 샘플 → Task 1 Step 1. ✅
- 입력 소스 매 프레임 push(0 포함) → Task 1 Step 2/3. ✅
- 액션 일회성 유지 → Task 1 Step 1(pendingJump/pendingAbilityId 소비). ✅
- 서버 무변경 → Global Constraints + 설계상 tick-키 소비. ✅
- 대시 입력락 가드 유지 → Step 1(HasActiveMotionEffect). ✅
- idle 최적화(redundancy 재전송) 유지 → Step 1 else 분기. ✅
- 검증(진단로그 [INPUT]/[RUN]) → Task 1 Step 1/4 + Task 2. ✅
- seq cadence 검토 → Task 2 Step 4(reconcile 정상 확인). ✅

**2. Placeholder scan:** 모든 코드 스텝에 실제 코드. "구체 배선 확인"류 없음(GamePadView.Tick 실제 코드 포함). TBD/TODO 없음. ✅

**3. Type consistency:** `SetMovement(float,float)` — Step 1 정의 ↔ Step 2 `PushMovement`가 호출 일치. `FeedMove()`/`FeedKeyboardMove()` — Step 2 정의 ↔ Step 3 `Tick` 호출 일치(`TryFeedKeyboardMove`는 제거·전부 `FeedKeyboardMove`로). `heldHorizontal/heldVertical/pendingJump/pendingAbilityId` — Step 1 내부 일관. `InputCommand` 필드(Horizontal/Vertical/Jump/AbilityId/SequenceNumber) + `InputBufferSystem`(Enqueue/SetCurrent/TrimToWindow) = LOP-Shared 실제 시그니처와 일치. ✅
