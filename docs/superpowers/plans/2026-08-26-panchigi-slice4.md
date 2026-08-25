# 판치기 슬라이스 4 — 턴 루프 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 판치기를 *칠 수는 있지만 이기고 지지는 않는* 상태에서 **끝나는 게임**으로 만든다 — 차례가 돌고, 동전이 멎으면 면을 세고, 다 뒤집히면 그 사람이 이긴다.

**Architecture:** 서버 `PanchigiTurnSystem`이 `End` 페이즈(물리 뒤·스냅샷 앞)에서 매 틱 돌며 정지 판정 → 장외 복귀 → 면 세기 → 전이를 수행한다. 전이 규칙은 순수 POCO `PanchigiTurn`이, 동전 상태 판단은 static `PanchigiCoin`이 맡는다. 상태는 `PanchigiStateToC`(reliable, 바뀔 때만)로 클라에 가고, 클라는 홀더에 담아 UI 한 줄을 그리고 입력을 게이팅한다. 종료는 `IGameRuleSystem.IsMatchOver`로 러너에 알린다.

**Tech Stack:** Unity 6000.3.16f1, VContainer, MessagePipe, R3, UI Toolkit, Protobuf(Google.Protobuf 3.28.x), Luban, Mirror

**Spec:** `docs/superpowers/specs/2026-08-26-panchigi-slice4-turn-loop-design.md`

## Global Constraints

- **판치기 규칙은 전부 LOP-Server에 둔다.** 클라 예측이 없어 서버만 판정한다. 테스트 편의로 LOP-Shared에 밀지 않는다. 와이어(proto)만 LOP-Shared.
- **어셈블리(asmdef)를 새로 만들지 않는다.** 진짜 피처 어셈블리를 만들 수 없으므로(서버 코드가 `Assembly-CSharp` 타입에 붙어 있다) 순수 조각만 떼어내지 않는다.
- **EditMode 테스트를 새로 만들지 않는다.** 검증은 Task 15의 에디터 검증 루틴 + Task 16의 실플레이.
- **이름은 주어를 클래스로, 동사를 메서드로.** `PanchigiCoin.IsFlipped(...)`, `PanchigiStrike.ComputeImpulse(...)`. `Kernel`/`Rules`/`Util`/`Helper`를 쓰지 않는다.
- **World 타입은 항상 풀 네임스페이스로 한정한다** — `GameFramework.World.Entity`, `GameFramework.World.Transform`. `using GameFramework.World;`를 추가하지 않는다(`UnityEngine.Component`와 충돌).
- **주석은 최소로, 일상어로.** 코드로 자명한 것은 달지 않고 *왜* 만 남긴다.
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고 커밋 전 `git status --short`로 확인한다.
- **커밋하지 않는 로컬 픽스처(서버)**: `Assets/AddressableAssetsData/AddressableAssetSettings.asset`, `Assets/DefaultVolumeProfile.asset`, `Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`, `Assets/Scripts/Game/FlapWangRuleSystem.cs`, `Assets/URPDefaultResources/*.asset`. **클라**: `Assets/AddressableAssetsData/**`, `Assets/Art`, `Assets/UI/Theme/Fonts/Jua-Regular SDF.asset`, `ProjectSettings/PackageManagerSettings.asset`, `Assets/Resources/UI/UIRoot.prefab`.
- **컴파일 확인은 `unity` CLI로.** `~/AppData/Local/Unity/bin/unity cmd recompile_status --project-path <경로>` → `failed:false`. 에디터가 안 떠 있으면 `unity open <경로>`로 띄운다.
- **`.meta` 파일은 유니티가 만든 것만 커밋한다.** 직접 만들지 않는다.

---

## 파일 지도

**GameFramework**
- Modify: `Runtime/Scripts/World/PhysicsBody.cs` — 각속도 get/set 추상 메서드

**LeagueOfPhysical-Shared**
- Modify: `Runtime/Scripts/Game/UnityPhysicsBody.cs` — 각속도 구현
- Delete: `Runtime/Scripts/Game/PanchigiStrikeKernel.cs` (+meta)
- Delete: `Tests/EditMode/PanchigiStrikeKernelTests.cs` (+meta)
- Create: `Protos/PanchigiStateToC.proto` — 턴 상태 와이어

**LeagueOfPhysical-Server**
- Create: `Assets/Scripts/Game/PanchigiStrike.cs` — 구 `PanchigiStrikeKernel`(이동·개명)
- Create: `Assets/Scripts/Game/PanchigiCoin.cs` — 면·장외·정지 판정
- Create: `Assets/Scripts/Game/PanchigiTurn.cs` — 전이 POCO
- Create: `Assets/Scripts/Game/PanchigiBoard.cs` — 무대 값(판 경계 + 동전 자리)
- Create: `Assets/Scripts/Game/PanchigiTurnSystem.cs` — 틱·판정 구동·상태 송신
- Create: `Assets/Editor/PanchigiVerification.cs` — 에디터 검증 루틴
- Modify: `Assets/Scripts/Game/PanchigiRuleSystem.cs` — 씬 자리로 스폰 + `IsMatchOver`/`ResolveOutcome`
- Modify: `Assets/Scripts/Game/MessageHandler/PanchigiStrikeMessageHandler.cs` — 턴 게이팅 + 판 조회 교체
- Modify: `Assets/Scripts/Game/PanchigiLifetimeScope.cs` — 신규 등록
- Modify: `Assets/Scripts/Game/IGameRuleSystem.cs` — `IsMatchOver`
- Modify: `Assets/Scripts/Game/LOPRunner.cs` — 종료 조건에 `IsMatchOver` 추가
- Modify: `Assets/Scripts/Game/FlapWangRuleSystem.cs` · `FlappyRaceRuleSystem.cs` — `IsMatchOver => false`
- Modify: `Assets/Scripts/RootLifetimeScope.cs` · `Assets/Scripts/Netcode/*` — 상태 송신 배선
- Modify: `Assets/Scenes/Panchigi.unity` — `PanchigiBoard` + 동전 자리

**LeagueOfPhysical-Client**
- Create: `Assets/Scripts/Game/PanchigiStateStore.cs` — 최신 턴 상태 홀더
- Create: `Assets/Scripts/Game/MessageHandler/PanchigiStateMessageHandler.cs`
- Create: `Assets/Scripts/Game/PanchigiHudCoordinator.cs` — 화면 열기
- Create: `Assets/Scripts/UI/PanchigiTurn/PanchigiTurnView.cs` · `PanchigiTurnViewModel.cs`
- Create: `Assets/UI/PanchigiTurn/PanchigiTurn.uxml` · `PanchigiTurn.uss`
- Modify: `Assets/Scripts/Game/PanchigiStrikeInput.cs` — 입력 게이팅
- Modify: `Assets/Scripts/Game/PanchigiLifetimeScope.cs` — 신규 등록
- Modify: `Assets/Scripts/RootLifetimeScope.cs` · `Assets/Scripts/Network/NetworkMessageDispatcher.cs`

**infrastructure / MasterData**
- Modify: `table/Datas/#PanchigiSetup.xlsx` — `coin_count` 제거 + 4인 행
- 재생성 산출물: MasterData-Client/Server의 `PanchigiSetup.cs` · `tbpanchigisetup.bytes`

---

### Task 1: 물리 포트에 각속도

정지 판정은 각속도가 있어야 성립하고(제자리에서 도는 동전은 선속도가 0), 장외 복귀는 각속도를 0으로 만들어야 한다. 포트에 둘 다 없다.

**Files:**
- Modify: `GameFramework/Runtime/Scripts/World/PhysicsBody.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/UnityPhysicsBody.cs`

**Interfaces:**
- Produces: `GameFramework.World.PhysicsBody.GetAngularVelocity() → System.Numerics.Vector3`, `PhysicsBody.SetAngularVelocity(System.Numerics.Vector3)`

- [ ] **Step 1: 포트에 추상 메서드 두 개 추가**

`GameFramework/Runtime/Scripts/World/PhysicsBody.cs`의 `GetVelocity()` 선언 바로 아래에 넣는다.

```csharp
        /// <summary>제자리에서 도는 몸은 선속도가 0이라, 멎었는지 보려면 이것도 봐야 한다.</summary>
        public abstract Vector3 GetAngularVelocity();

        /// <summary>몸을 어딘가로 되돌릴 때 회전 관성까지 지워야 그 자리에 선다.</summary>
        public abstract void SetAngularVelocity(Vector3 angular);
```

- [ ] **Step 2: Unity 어댑터에 구현**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/UnityPhysicsBody.cs`의 `GetVelocity()` 아래에 넣는다.

```csharp
        public override System.Numerics.Vector3 GetAngularVelocity()
        {
            return _rigidbody == null ? System.Numerics.Vector3.Zero : _rigidbody.angularVelocity.ToNumerics();
        }

        public override void SetAngularVelocity(System.Numerics.Vector3 angular)
        {
            if (_rigidbody != null)
            {
                _rigidbody.angularVelocity = angular.ToUnity();
            }
        }
```

- [ ] **Step 3: 다른 구현체가 없는지 확인**

```bash
grep -rn ": PhysicsBody\|: GameFramework.World.PhysicsBody" \
  ~/workspace/LOP/GameFramework ~/workspace/LOP/LeagueOfPhysical-Shared \
  ~/workspace/LOP/LeagueOfPhysical-Server/Assets ~/workspace/LOP/LeagueOfPhysical-Client/Assets \
  --include=*.cs
```

기대: `UnityPhysicsBody` 하나만. 다른 게 나오면 거기에도 두 메서드를 구현한다(추상이라 안 하면 컴파일 실패).

- [ ] **Step 4: 컴파일 확인**

```bash
~/AppData/Local/Unity/bin/unity cmd recompile_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
```

기대: `failed:false`, `errors:[]`

- [ ] **Step 5: 커밋 (레포 2개)**

```bash
git -C ~/workspace/LOP/GameFramework add Runtime/Scripts/World/PhysicsBody.cs
git -C ~/workspace/LOP/GameFramework commit -m "feat(physics): 몸의 각속도를 읽고 쓸 수 있게 한다"

git -C ~/workspace/LOP/LeagueOfPhysical-Shared add Runtime/Scripts/Game/UnityPhysicsBody.cs
git -C ~/workspace/LOP/LeagueOfPhysical-Shared commit -m "feat(physics): 각속도 get/set 구현"
```

---

### Task 2: `PanchigiStrikeKernel` → 서버로 옮기고 `PanchigiStrike`로 개명

판치기는 클라 예측이 없어 이 계산을 서버만 부른다. LOP-Shared에 있던 것은 EditMode 테스트를 붙이려던 것이고 잘못된 이유였다. **테스트 13개가 사라진다** — 그 자리는 Task 15가 메운다.

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiStrike.cs`
- Delete: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/PanchigiStrikeKernel.cs` (+`.meta`)
- Delete: `LeagueOfPhysical-Shared/Tests/EditMode/PanchigiStrikeKernelTests.cs` (+`.meta`)
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/MessageHandler/PanchigiStrikeMessageHandler.cs`

**Interfaces:**
- Produces: `LOP.PanchigiStrike.StrikeInput(Vector3 strikePoint, Vector3 dragDelta, float holdTime)`, `LOP.PanchigiStrike.StrikeTuning(float forceMultiplier, float horizontalForceMultiplier, float falloffRate)`, `PanchigiStrike.ComputeImpulse(in StrikeInput, in StrikeTuning, Vector3[] liveSamples, int liveCount, int totalSamples) → Vector3`, `PanchigiStrike.BuildSamples(Vector3 coinCenter, float radius, Vector3[] buffer)` (모두 `System.Numerics.Vector3`)

- [ ] **Step 1: 옮길 원본을 읽어 둔다**

```bash
cat ~/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/PanchigiStrikeKernel.cs
```

- [ ] **Step 2: 서버에 새 파일을 만든다**

`LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiStrike.cs` — 원본의 본문(`GoldenAngle` 상수, `ComputeImpulse`, `BuildSamples`, 주석 전부)을 그대로 옮기되 **바깥 껍데기만** 아래처럼 바꾼다. `StrikeInput`/`StrikeTuning`은 중첩 타입으로 들어간다.

```csharp
using System;
using System.Numerics;

namespace LOP
{
    /// <summary>
    /// 판치기 타격의 힘 계산. 클라는 예측하지 않으므로 서버만 부른다.
    ///
    /// 원본(ForceElement)은 동전 밑 접촉점 수천 개마다 힘을 나눠 줬지만, 그 힘을 전부 *같은 지점*에
    /// 걸었기 때문에 합이 임펄스 하나와 수학적으로 같았다. 즉 격자가 만든 것은 회전이 아니라
    /// "동전이 판에 닿은 정도"라는 배수 하나였다. 여기서는 그 배수를 고정 개수 샘플로 직접 잰다.
    /// </summary>
    public static class PanchigiStrike
    {
        /// <summary>한 번의 타격이 무엇이었나 — 판 위 어디를, 어느 방향으로, 얼마나 오래 눌러서.</summary>
        public readonly struct StrikeInput
        {
            public readonly Vector3 StrikePoint;
            public readonly Vector3 DragDelta;
            public readonly float HoldTime;

            public StrikeInput(Vector3 strikePoint, Vector3 dragDelta, float holdTime)
            {
                StrikePoint = strikePoint;
                DragDelta = dragDelta;
                HoldTime = holdTime;
            }
        }

        /// <summary>타격 세기를 정하는 값들. 마스터데이터에서 온다.</summary>
        public readonly struct StrikeTuning
        {
            public readonly float ForceMultiplier;
            public readonly float HorizontalForceMultiplier;
            public readonly float FalloffRate;

            public StrikeTuning(float forceMultiplier, float horizontalForceMultiplier, float falloffRate)
            {
                ForceMultiplier = forceMultiplier;
                HorizontalForceMultiplier = horizontalForceMultiplier;
                FalloffRate = falloffRate;
            }
        }

        // ↓ 여기부터 원본의 GoldenAngle · ComputeImpulse · BuildSamples를 주석까지 그대로 옮긴다.
    }
}
```

- [ ] **Step 3: Shared에서 원본과 테스트를 지운다**

```bash
cd ~/workspace/LOP/LeagueOfPhysical-Shared
git rm Runtime/Scripts/Game/PanchigiStrikeKernel.cs Runtime/Scripts/Game/PanchigiStrikeKernel.cs.meta
git rm Tests/EditMode/PanchigiStrikeKernelTests.cs Tests/EditMode/PanchigiStrikeKernelTests.cs.meta
```

- [ ] **Step 4: 호출부를 고친다**

`PanchigiStrikeMessageHandler.cs`에서 세 군데를 바꾼다.

```csharp
// 이전
var input = new StrikeInput(strikePoint.ToNumerics(), dragDelta.ToNumerics(), holdTime);
var tuning = new StrikeTuning(
    config.ForceMultiplier, config.HorizontalForceMultiplier, config.FalloffRate);
// 이후
var input = new PanchigiStrike.StrikeInput(strikePoint.ToNumerics(), dragDelta.ToNumerics(), holdTime);
var tuning = new PanchigiStrike.StrikeTuning(
    config.ForceMultiplier, config.HorizontalForceMultiplier, config.FalloffRate);
```

```csharp
PanchigiStrikeKernel.BuildSamples(...)   →   PanchigiStrike.BuildSamples(...)
PanchigiStrikeKernel.ComputeImpulse(...) →   PanchigiStrike.ComputeImpulse(...)
```

- [ ] **Step 5: 옛 이름이 남지 않았는지 확인**

```bash
grep -rn "PanchigiStrikeKernel\|StrikeTuning\|StrikeInput" \
  ~/workspace/LOP/LeagueOfPhysical-Shared ~/workspace/LOP/LeagueOfPhysical-Server/Assets \
  ~/workspace/LOP/LeagueOfPhysical-Client/Assets --include=*.cs
```

기대: `PanchigiStrike.StrikeInput`/`PanchigiStrike.StrikeTuning` 형태만 서버 핸들러에 남는다.

- [ ] **Step 6: 컴파일 확인**

`recompile_status`로 서버 `failed:false`. Shared 패키지 파일을 지웠으므로 stale CS2001이 뜨면 유니티를 다시 임포트한다(`[[deleting-package-files-cs2001]]`).

- [ ] **Step 7: 커밋 (레포 2개)**

```bash
git -C ~/workspace/LOP/LeagueOfPhysical-Shared commit -m "refactor(panchigi): 타격 힘 계산을 서버로 넘긴다"
git -C ~/workspace/LOP/LeagueOfPhysical-Server add Assets/Scripts/Game/PanchigiStrike.cs Assets/Scripts/Game/PanchigiStrike.cs.meta Assets/Scripts/Game/MessageHandler/PanchigiStrikeMessageHandler.cs
git -C ~/workspace/LOP/LeagueOfPhysical-Server commit -m "refactor(panchigi): PanchigiStrike — 힘 계산을 서버로 옮기고 주어 이름으로"
```

---

### Task 3: `PanchigiCoin` — 면·장외·정지 판정

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiCoin.cs`

**Interfaces:**
- Produces: `LOP.PanchigiCoin.IsFlipped(System.Numerics.Quaternion) → bool`, `PanchigiCoin.IsOutOfBoard(System.Numerics.Vector3, UnityEngine.Bounds) → bool`, `PanchigiCoin.IsAtRest(System.Numerics.Vector3 linear, System.Numerics.Vector3 angular, float speedEpsilon, float angularEpsilon) → bool`

- [ ] **Step 1: 파일을 만든다**

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 동전 하나의 상태를 보고 참/거짓을 내는 판정들. 계산이 아니라 판단이라 값만 받는다.
    /// 판치기는 클라 예측이 없어 이 판정을 서버만 한다.
    /// </summary>
    public static class PanchigiCoin
    {
        /// <summary>
        /// 뒤집혔나. 동전은 전부 같은 면(+up)으로 놓이므로 윗면이 아래를 보면 뒤집힌 것이다.
        /// 모로 선 동전(내적 ≈ 0)은 뒤집힌 것으로 치지 않는다 — 실제로 나오는 자세라 미리 정해 둔다.
        /// </summary>
        public static bool IsFlipped(System.Numerics.Quaternion rotation)
        {
            Quaternion q = new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);
            return Vector3.Dot(q * Vector3.up, Vector3.up) < 0f;
        }

        /// <summary>판을 벗어났나. 판 위에서 x·z가 벗어났거나 판 아래로 떨어진 경우.</summary>
        public static bool IsOutOfBoard(System.Numerics.Vector3 position, Bounds board)
        {
            if (position.X < board.min.x || position.X > board.max.x) { return true; }
            if (position.Z < board.min.z || position.Z > board.max.z) { return true; }
            return position.Y < board.min.y;
        }

        /// <summary>
        /// 이 한 틱만 놓고 볼 때 멎어 있나. 튀어 오른 동전은 정점에서 속도가 순간 0을 지나므로
        /// 이것만으로 "멎었다"고 하면 안 된다 — 연속 몇 틱인지는 부르는 쪽이 센다.
        /// 제자리에서 도는 동전은 선속도가 0이라 각속도도 같이 본다.
        /// </summary>
        public static bool IsAtRest(System.Numerics.Vector3 linear, System.Numerics.Vector3 angular,
            float speedEpsilon, float angularEpsilon)
        {
            return linear.Length() <= speedEpsilon && angular.Length() <= angularEpsilon;
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인 + 커밋**

```bash
~/AppData/Local/Unity/bin/unity cmd recompile_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git -C ~/workspace/LOP/LeagueOfPhysical-Server add Assets/Scripts/Game/PanchigiCoin.cs Assets/Scripts/Game/PanchigiCoin.cs.meta
git -C ~/workspace/LOP/LeagueOfPhysical-Server commit -m "feat(panchigi): 동전 상태 판정 — 면·장외·정지"
```

---

### Task 4: `PanchigiTurn` — 전이 POCO

유니티도 마스터데이터도 모른다. 조준 마감 *시각*은 여기 없다 — 그건 타이밍이라 시스템이 든다.

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiTurn.cs`

**Interfaces:**
- Produces: `LOP.PanchigiPhase { Settling, Aiming, Over }`, `LOP.PanchigiTurn(IReadOnlyList<string> playerEntityIds, int turnLimit)`, 프로퍼티 `Phase`/`CurrentEntityId`/`TurnCount`/`WinnerEntityId`, 메서드 `OnRested(bool allFlipped)`/`OnStruck(string entityId)`/`OnAimTimeout()`

- [ ] **Step 1: 파일을 만든다**

```csharp
using System.Collections.Generic;

namespace LOP
{
    public enum PanchigiPhase
    {
        Settling,
        Aiming,
        Over,
    }

    /// <summary>
    /// 판치기 한 판의 진행. 물리도 시계도 모르고 "무슨 일이 있었나"만 받아 다음 국면을 정한다.
    /// </summary>
    public class PanchigiTurn
    {
        private readonly IReadOnlyList<string> players;
        private readonly int turnLimit;

        private int nextIndex;
        private string lastStriker;

        public PanchigiPhase Phase { get; private set; } = PanchigiPhase.Settling;

        /// <summary>지금 칠 차례인 사람. <see cref="PanchigiPhase.Aiming"/>이 아니면 null.</summary>
        public string CurrentEntityId { get; private set; }

        /// <summary>친 것과 패스한 것을 모두 센다 — 안 그러면 전원이 계속 패스해 판이 안 끝난다.</summary>
        public int TurnCount { get; private set; }

        /// <summary>이긴 사람. 아직 안 끝났거나 무승부면 null.</summary>
        public string WinnerEntityId { get; private set; }

        public PanchigiTurn(IReadOnlyList<string> playerEntityIds, int turnLimit)
        {
            players = playerEntityIds;
            this.turnLimit = turnLimit;
        }

        /// <summary>동전이 모두 멎었다. 판 시작 직후에도 한 번 온다(그땐 아무도 안 쳐서 allFlipped가 거짓).</summary>
        public void OnRested(bool allFlipped)
        {
            if (Phase != PanchigiPhase.Settling) { return; }

            if (allFlipped)
            {
                WinnerEntityId = lastStriker;   // 그 상태를 만든 사람
                Phase = PanchigiPhase.Over;
                return;
            }

            if (TurnCount > turnLimit)
            {
                Phase = PanchigiPhase.Over;     // 무승부 — WinnerEntityId는 null
                return;
            }

            EnterAiming();
        }

        public void OnStruck(string entityId)
        {
            if (Phase != PanchigiPhase.Aiming) { return; }

            lastStriker = entityId;
            TurnCount++;
            CurrentEntityId = null;
            Phase = PanchigiPhase.Settling;
        }

        /// <summary>조준 시간을 넘겼다 — 그냥 패스한다. 물리를 안 건드리므로 Settling을 거치지 않는다.</summary>
        public void OnAimTimeout()
        {
            if (Phase != PanchigiPhase.Aiming) { return; }

            TurnCount++;

            if (TurnCount > turnLimit)
            {
                CurrentEntityId = null;
                Phase = PanchigiPhase.Over;
                return;
            }

            EnterAiming();
        }

        private void EnterAiming()
        {
            if (players.Count == 0)
            {
                Phase = PanchigiPhase.Over;
                return;
            }

            CurrentEntityId = players[nextIndex];
            nextIndex = (nextIndex + 1) % players.Count;
            Phase = PanchigiPhase.Aiming;
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인 + 커밋**

```bash
~/AppData/Local/Unity/bin/unity cmd recompile_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git -C ~/workspace/LOP/LeagueOfPhysical-Server add Assets/Scripts/Game/PanchigiTurn.cs Assets/Scripts/Game/PanchigiTurn.cs.meta
git -C ~/workspace/LOP/LeagueOfPhysical-Server commit -m "feat(panchigi): 턴 전이 — 국면·차례·종료 판정"
```

---

### Task 5: `PanchigiStateToC` 와이어

**Files:**
- Create: `LeagueOfPhysical-Shared/Protos/PanchigiStateToC.proto`
- 재생성: `Runtime.Generated/Scripts/Protobuf/PanchigiStateToC.cs`(+`.IMessage.cs`), `MessageIds.cs`, `MessageInitializer.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/RootLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/RootLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Network/NetworkMessageDispatcher.cs`

**Interfaces:**
- Produces: `LOP.PanchigiStateToC { int32 Phase; string CurrentEntityId; int64 AimDeadlineTick; }`

- [ ] **Step 1: proto를 쓴다**

`LeagueOfPhysical-Shared/Protos/PanchigiStateToC.proto`:

```proto
syntax = "proto3";

package LOP;

// 판치기 턴 상태(서버 → 클라). 바뀔 때만 reliable로 간다.
// 남은 시간은 마감을 *틱*으로 보내 클라가 공유 틱 시계로 직접 그린다 — 주기 전송이 필요 없다.
message PanchigiStateToC {
  // 0 = Settling(동전이 구르는 중), 1 = Aiming(누군가 조준 중)
  int32 phase = 1;
  // 지금 칠 차례인 플레이어 엔티티. Settling이면 빈 문자열
  string current_entity_id = 2;
  // 조준 마감 틱. Aiming일 때만 유효
  int64 aim_deadline_tick = 3;
}
```

- [ ] **Step 2: 서브스크립트를 개별 실행한다**

부모 스크립트(`generate_protos.sh`)는 `MessageIds.cs`를 지웠다 다시 만들어 **기존 번호가 밀린다**(`[[proto-message-id-regen-gotcha]]`). 개별로 돌린다.

```bash
cd ~/workspace/LOP/LeagueOfPhysical-Shared
cp Runtime.Generated/Scripts/MessageIds.cs /tmp/MessageIds.before.cs
bash Scripts/compile_protos.sh
bash Scripts/generate_imessage.sh
bash Scripts/generate_message_ids.sh
bash Scripts/generate_message_initializer.sh
```

- [ ] **Step 3: 기존 번호가 안 밀렸는지 눈으로 확인**

```bash
diff /tmp/MessageIds.before.cs Runtime.Generated/Scripts/MessageIds.cs
```

기대: `PanchigiStateToC` **한 줄만 추가**. 기존 메시지의 번호가 하나라도 바뀌면 멈추고 보고한다 — 와이어가 깨진다.

- [ ] **Step 4: 서버는 등록할 것이 없다**

서버는 이 메시지를 *보내기만* 한다(`session.Send(message)`). 브로커 등록과 디스패처 배선은
**받는 쪽에만** 필요하므로 서버 `RootLifetimeScope`는 건드리지 않는다.

`WorldEventSink`가 `WorldEventBatchToC`를 보낼 때 아무 등록도 안 하는 것과 같다 — 확인:

```bash
grep -n "WorldEventBatchToC" ~/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/RootLifetimeScope.cs
```

기대: 출력 없음(= 송신 전용 메시지는 등록하지 않는다).

- [ ] **Step 5: 클라 브로커 등록**

`LeagueOfPhysical-Client/Assets/Scripts/RootLifetimeScope.cs`의 `RegisterOrderedMessageBroker<InputTimingToC>();` 옆에 추가:

```csharp
            builder.RegisterOrderedMessageBroker<PanchigiStateToC>();
```

- [ ] **Step 6: 클라 디스패처 배선**

`LeagueOfPhysical-Client/Assets/Scripts/Network/NetworkMessageDispatcher.cs`에 생성자 파라미터와 등록을 더한다. 기존 `IPublisher<InputTimingToC> inputTiming` 옆에:

```csharp
            IPublisher<PanchigiStateToC> panchigiState,
```

그리고 `Register(inputTiming);` 옆에:

```csharp
            Register(panchigiState);
```

- [ ] **Step 7: 컴파일 확인 + 커밋**

```bash
~/AppData/Local/Unity/bin/unity cmd recompile_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
~/AppData/Local/Unity/bin/unity cmd recompile_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
```

```bash
git -C ~/workspace/LOP/LeagueOfPhysical-Shared add Protos/PanchigiStateToC.proto Protos/PanchigiStateToC.proto.meta Runtime.Generated
git -C ~/workspace/LOP/LeagueOfPhysical-Shared commit -m "feat(wire): 판치기 턴 상태 메시지"
git -C ~/workspace/LOP/LeagueOfPhysical-Client add Assets/Scripts/RootLifetimeScope.cs Assets/Scripts/Network/NetworkMessageDispatcher.cs
git -C ~/workspace/LOP/LeagueOfPhysical-Client commit -m "feat(panchigi): 턴 상태 메시지 수신 배선"
```

---

### Task 6: 마스터데이터 — `coin_count` 제거 + 4인 행

`coin_count`는 대형의 점 개수와 같은 것을 두 번 말한다(4개짜리 `FourInLine`). 그리고 규칙은 2~4명인데 **4인 행이 없어** 4인 매치가 잡히면 방이 죽는다.

**Files:**
- Modify: `infrastructure/table/Datas/#PanchigiSetup.xlsx`
- 재생성: MasterData-Client/Server의 `PanchigiSetup.cs`, `tbpanchigisetup.bytes`

**Interfaces:**
- Produces: `LOP.MasterData.PanchigiSetup { int Id; string Formation; }` (Id = 인원수)

- [ ] **Step 1: 현재 표를 확인한다**

```bash
cd ~/workspace/LOP/infrastructure && python -c "
import zipfile, re
z = zipfile.ZipFile('table/Datas/#PanchigiSetup.xlsx')
s = z.read('xl/worksheets/sheet1.xml').decode('utf-8')
for row in re.findall(r'<row[^>]*>.*?</row>', s, re.S):
    print(re.findall(r'<c r=\"([A-Z]+\d+)\"[^>]*>(?:<is><t[^>]*>([^<]*)</t></is>|<v>([^<]*)</v>)?</c>', row))
"
```

기대: `B=id, C=coin_count, D=formation` / 5행 `2,4,FourInLine` / 6행 `3,6,SixInLine`

- [ ] **Step 2: 엑셀을 고친다**

`C` 열(`coin_count`)을 삭제해 `formation`이 `C` 열로 오게 하고, 4인 행(`4`, `EightInLine`)을 7행에 더한다. 결과:

```
##var   id      formation
##type  int     string
##group
##      id      formation
        2       FourInLine
        3       SixInLine
        4       EightInLine
```

파이썬으로 sheet1.xml을 직접 편집하거나 엑셀에서 연다. 어느 쪽이든 **`##var`/`##type`/`##group`/`##` 네 줄 머리**를 유지해야 Luban이 읽는다.

- [ ] **Step 3: 재생성**

```bash
cd ~/workspace/LOP/infrastructure/table && bash gen.sh 2>&1 | tail -5
```

- [ ] **Step 4: 바이트를 디코딩해 확인**

```bash
cd ~/workspace/LOP/LeagueOfPhysical-MasterData-Client && python -c "
import struct
d = open('Runtime.Generated/StreamingAssets/MasterData/tbpanchigisetup.bytes','rb').read()
print('len', len(d), d.hex())
"
```

`PanchigiSetup.cs`에 `CoinCount` 필드가 없고 `Formation`만 있는지, 행이 3개인지 본다.

- [ ] **Step 5: 커밋 (레포 3개)**

```bash
git -C ~/workspace/LOP/infrastructure add "table/Datas/#PanchigiSetup.xlsx"
git -C ~/workspace/LOP/infrastructure commit -m "feat(masterdata): 판치기 대형이 동전 개수를 대신한다 + 4인 구성"
git -C ~/workspace/LOP/LeagueOfPhysical-MasterData-Client add Runtime.Generated
git -C ~/workspace/LOP/LeagueOfPhysical-MasterData-Client commit -m "feat(masterdata): 판치기 구성 재생성"
git -C ~/workspace/LOP/LeagueOfPhysical-MasterData-Server add Runtime.Generated
git -C ~/workspace/LOP/LeagueOfPhysical-MasterData-Server commit -m "feat(masterdata): 판치기 구성 재생성"
```

---

### Task 7: `PanchigiBoard` — 무대 값을 씬으로

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiBoard.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scenes/Panchigi.unity`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/MessageHandler/PanchigiStrikeMessageHandler.cs`

**Interfaces:**
- Produces: `LOP.PanchigiBoard.Bounds → UnityEngine.Bounds`, `PanchigiBoard.TryGetSlots(string formation, out IReadOnlyList<UnityEngine.Transform> slots) → bool`

- [ ] **Step 1: 컴포넌트를 만든다**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 판치기의 무대. 판 경계와 동전 자리를 씬이 들고 있게 해 코드에 좌표가 박히지 않게 한다 —
    /// 스폰과 장외 복귀가 같은 값을 본다.
    /// </summary>
    public class PanchigiBoard : MonoBehaviour
    {
        [Serializable]
        public class Formation
        {
            public string name;
            public Transform[] slots;
        }

        [SerializeField] private Collider boardCollider;
        [SerializeField] private Formation[] formations;

        public Bounds Bounds => boardCollider != null ? boardCollider.bounds : default;

        private void Awake()
        {
            if (boardCollider == null)
            {
                boardCollider = GetComponent<Collider>();
            }
        }

        public bool TryGetSlots(string formation, out IReadOnlyList<Transform> slots)
        {
            if (formations != null)
            {
                foreach (Formation f in formations)
                {
                    if (f != null && f.name == formation && f.slots != null && f.slots.Length > 0)
                    {
                        slots = f.slots;
                        return true;
                    }
                }
            }

            slots = null;
            return false;
        }
    }
}
```

- [ ] **Step 2: 씬에 붙이고 자리를 만든다**

`unity` CLI로 서버 씬을 열고, `Board`에 컴포넌트를 붙인 뒤 자리 오브젝트를 만든다.

```bash
U=~/AppData/Local/Unity/bin/unity
S=C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
$U cmd open_scene --project-path $S -- --path "Assets/Scenes/Panchigi.unity"
$U cmd add_component --project-path $S -- --target "Board" --component "LOP.PanchigiBoard"
```

자리는 `Board` 아래 빈 오브젝트로 만든다. **8개**(4인 대형이 가장 크다)를 만들고, 판(10×10) 안에 겹치지 않게 둔다. 동전 반지름 0.3이므로 간격은 0.7 이상.

권장 배치(모두 `y = 1.5` — 떨어뜨려 놓는다):

```
Slot0 (-1.05, 1.5, 0)   Slot1 (-0.35, 1.5, 0)   Slot2 (0.35, 1.5, 0)   Slot3 (1.05, 1.5, 0)
Slot4 (-1.05, 1.5, 0.7) Slot5 (-0.35, 1.5, 0.7) Slot6 (0.35, 1.5, 0.7) Slot7 (1.05, 1.5, 0.7)
```

`create_gameobject`/`set_transform`/`set_parent`로 만들고, `set_component_properties`로 `PanchigiBoard`의 `formations`를 채운다:

- `FourInLine` → Slot0~3
- `SixInLine` → Slot0~5
- `EightInLine` → Slot0~7

`boardCollider`는 `Board`의 `BoxCollider`.

- [ ] **Step 3: 씬을 저장한다**

```bash
$U cmd save_scene --project-path $S
```

- [ ] **Step 4: 타격 핸들러가 이 컴포넌트를 쓰게 한다**

`PanchigiStrikeMessageHandler.cs`에서 `boardBounds`/`boardFound` 필드와 `TryGetBoardBounds` 메서드를 **지우고**, 생성자 주입으로 바꾼다.

```csharp
        private readonly PanchigiBoard board;
```

생성자에 `PanchigiBoard board` 파라미터를 더하고 `this.board = board;`. `OnStrike` 첫머리를 바꾼다:

```csharp
            if (board == null)
            {
                Debug.LogWarning("[Panchigi] 판을 찾지 못했다 — 타격을 버린다.");
                return;
            }

            Bounds boardBounds = board.Bounds;
```

이후 `board` 지역변수를 쓰던 곳(`ContainsXZ(board, ...)`, `ApplyStrike(..., board, ...)`)을 `boardBounds`로 바꾼다.

- [ ] **Step 5: 스코프에 등록한다**

`PanchigiLifetimeScope.cs`(서버)에 `[SerializeField] private PanchigiBoard board;`를 더하고 `ConfigureGame`에서 `builder.RegisterComponent(board);`. 씬의 `GameLifetimeScope` 오브젝트에서 `board` 슬롯에 `Board`를 물린다.

- [ ] **Step 6: 컴파일 + 커밋**

```bash
~/AppData/Local/Unity/bin/unity cmd recompile_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git -C ~/workspace/LOP/LeagueOfPhysical-Server status --short
git -C ~/workspace/LOP/LeagueOfPhysical-Server add Assets/Scripts/Game/PanchigiBoard.cs Assets/Scripts/Game/PanchigiBoard.cs.meta Assets/Scripts/Game/PanchigiLifetimeScope.cs Assets/Scripts/Game/MessageHandler/PanchigiStrikeMessageHandler.cs Assets/Scenes/Panchigi.unity
git -C ~/workspace/LOP/LeagueOfPhysical-Server commit -m "feat(panchigi): 무대 값을 씬으로 — 판 경계와 동전 자리"
```

---

### Task 8: 씬 자리로 스폰

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiRuleSystem.cs`

**Interfaces:**
- Consumes: `PanchigiBoard.TryGetSlots`, `LOP.MasterData.PanchigiSetup.Formation`
- Produces: `PanchigiRuleSystem.PlayerEntityIds → IReadOnlyList<string>`, `PanchigiRuleSystem.CoinEntityIds → IReadOnlyList<string>` (자리 인덱스 = 목록 인덱스)

- [ ] **Step 1: 생성자에 `PanchigiBoard`를 더한다**

```csharp
        private readonly PanchigiBoard board;

        private readonly List<string> playerEntityIds = new();
        private readonly List<string> coinEntityIds = new();

        public IReadOnlyList<string> PlayerEntityIds => playerEntityIds;
        public IReadOnlyList<string> CoinEntityIds => coinEntityIds;
```

- [ ] **Step 2: 플레이어 스폰에서 엔티티 id를 기록한다**

기존 루프에서 `entitySpawner.GenerateEntityId()` 결과를 변수로 받아 `CharacterCreationData.entityId`에 넣고 `playerEntityIds.Add(id)`. **`playerList` 순서가 곧 턴 순서**다.

- [ ] **Step 3: 동전 스폰을 씬 자리로 바꾼다**

`setup.CoinCount` 루프를 지우고:

```csharp
            if (board.TryGetSlots(setup.Formation, out IReadOnlyList<Transform> slots) == false)
            {
                //  조용히 넘기면 판이 빈 채로 시작하고 왜인지 런타임에 추적해야 한다.
                throw new System.InvalidOperationException(
                    $"씬의 PanchigiBoard에 '{setup.Formation}' 대형이 없다 — 자리를 채워야 한다.");
            }

            for (int i = 0; i < slots.Count; i++)
            {
                string coinId = entitySpawner.GenerateEntityId();
                entitySpawner.Spawn(new CoinCreationData
                {
                    entityId = coinId,
                    visualId = CoinVisualId,
                    position = slots[i].position,
                    //  자리의 회전은 쓰지 않는다 — 동전은 전부 같은 면(+up)으로 놓인다는 것이
                    //  종료 조건의 전제다. 자리를 돌려 놓으면 그 전제가 조용히 깨진다.
                    rotation = Vector3.zero,
                    velocity = Vector3.zero,
                });
                coinEntityIds.Add(coinId);
            }
```

> **자리의 회전은 무시한다.** 위치만 읽는다. 장외 복귀(Task 9)도 같은 규칙을 따라야 하므로
> 두 곳이 어긋나지 않게 확인한다.

- [ ] **Step 4: 컴파일 + 커밋**

```bash
~/AppData/Local/Unity/bin/unity cmd recompile_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git -C ~/workspace/LOP/LeagueOfPhysical-Server add Assets/Scripts/Game/PanchigiRuleSystem.cs
git -C ~/workspace/LOP/LeagueOfPhysical-Server commit -m "feat(panchigi): 씬의 자리로 동전을 놓는다"
```

---

### Task 9: `PanchigiTurnSystem` — 틱과 상태 송신

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiTurnSystem.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiLifetimeScope.cs`

**Interfaces:**
- Consumes: `PanchigiTurn`, `PanchigiCoin`, `PanchigiBoard`, `PanchigiRuleSystem.PlayerEntityIds`/`CoinEntityIds`
- Produces: `PanchigiTurnSystem.Begin(IReadOnlyList<string> playerEntityIds, IReadOnlyList<string> coinEntityIds)`, `CanStrike(string userId) → bool`, `NotifyStruck(string userId)`, `IsOver → bool`, `WinnerEntityId → string`

- [ ] **Step 1: 시스템을 만든다**

```csharp
using System.Collections.Generic;
using GameFramework;
using GameFramework.Runner;
using LOP.Event.LOPRunner.Update;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 판치기 진행(서버). End 페이즈에서 돈다 — 물리가 돈 뒤·스냅샷 송신 전이라
    /// "이번 틱 결과를 보고 턴을 정한 뒤 그 상태를 같이 보낸다"가 한 틱 안에 끝난다.
    /// </summary>
    public class PanchigiTurnSystem : ITickSystem
    {
        private readonly IRunner runner;
        private readonly IRoomDataStore roomDataStore;
        private readonly ISessionManager sessionManager;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly LOP.MasterData.LOPMasterData masterData;
        private readonly PanchigiBoard board;

        private PanchigiTurn turn;
        private IReadOnlyList<string> coinIds;
        private readonly Dictionary<string, string> userToEntity = new();

        private int restTicks;
        private long aimDeadlineTick;
        private PanchigiPhase sentPhase = PanchigiPhase.Over;   // 첫 틱에 반드시 한 번 보내도록
        private string sentEntityId;

        public bool IsOver => turn != null && turn.Phase == PanchigiPhase.Over;
        public string WinnerEntityId => turn?.WinnerEntityId;

        public PanchigiTurnSystem(IRunner runner, IRoomDataStore roomDataStore, ISessionManager sessionManager,
            GameFramework.World.EntityRegistry entityRegistry, LOP.MasterData.LOPMasterData masterData,
            PanchigiBoard board)
        {
            this.runner = runner;
            this.roomDataStore = roomDataStore;
            this.sessionManager = sessionManager;
            this.entityRegistry = entityRegistry;
            this.masterData = masterData;
            this.board = board;

            runner.RegisterSystem<End>(this);
        }

        public void Begin(IReadOnlyList<string> playerEntityIds, IReadOnlyList<string> coinEntityIds)
        {
            coinIds = coinEntityIds;

            var config = masterData.Tables.TbPanchigiConfig.GetOrDefault(1);
            turn = new PanchigiTurn(playerEntityIds, config != null ? config.MatchTurnLimit : 60);

            //  차례는 엔티티로 돌지만 타격은 userId로 온다 — 한 번만 이어 둔다.
            string[] playerList = roomDataStore.match.playerList;
            for (int i = 0; i < playerList.Length && i < playerEntityIds.Count; i++)
            {
                userToEntity[playerList[i]] = playerEntityIds[i];
            }
        }

        public bool CanStrike(string userId)
        {
            return turn != null
                && turn.Phase == PanchigiPhase.Aiming
                && userToEntity.TryGetValue(userId, out string entityId)
                && entityId == turn.CurrentEntityId;
        }

        public void NotifyStruck(string userId)
        {
            if (userToEntity.TryGetValue(userId, out string entityId))
            {
                turn?.OnStruck(entityId);
            }
        }

        public void Tick(long tick, float deltaTime)
        {
            if (turn == null || turn.Phase == PanchigiPhase.Over)
            {
                return;
            }

            var config = masterData.Tables.TbPanchigiConfig.GetOrDefault(1);
            if (config == null)
            {
                return;
            }

            if (turn.Phase == PanchigiPhase.Settling)
            {
                TickSettling(config);
            }
            else if (tick >= aimDeadlineTick)
            {
                turn.OnAimTimeout();
            }

            BroadcastIfChanged(tick, config);
        }

        private void TickSettling(LOP.MasterData.PanchigiConfig config)
        {
            if (AllAtRest(config) == false)
            {
                restTicks = 0;
                return;
            }

            if (++restTicks < config.RestTicks)
            {
                return;
            }

            restTicks = 0;
            ReturnOutOfBoardCoins();
            turn.OnRested(AllFlipped());
        }

        private bool AllAtRest(LOP.MasterData.PanchigiConfig config)
        {
            foreach (string id in coinIds)
            {
                var body = entityRegistry.Get(id)?.Get<GameFramework.World.PhysicsBody>();
                if (body == null) { continue; }

                if (PanchigiCoin.IsAtRest(body.GetVelocity(), body.GetAngularVelocity(),
                        config.RestSpeedEpsilon, config.RestAngularEpsilon) == false)
                {
                    return false;
                }
            }
            return true;
        }

        private bool AllFlipped()
        {
            foreach (string id in coinIds)
            {
                var body = entityRegistry.Get(id)?.Get<GameFramework.World.PhysicsBody>();
                if (body == null) { continue; }

                if (PanchigiCoin.IsFlipped(body.GetRotation()) == false)
                {
                    return false;
                }
            }
            return true;
        }

        private void ReturnOutOfBoardCoins()
        {
            var setup = masterData.Tables.TbPanchigiSetup.GetOrDefault(roomDataStore.match.playerList.Length);
            if (setup == null || board.TryGetSlots(setup.Formation, out IReadOnlyList<Transform> slots) == false)
            {
                return;
            }

            Bounds bounds = board.Bounds;
            for (int i = 0; i < coinIds.Count && i < slots.Count; i++)
            {
                var body = entityRegistry.Get(coinIds[i])?.Get<GameFramework.World.PhysicsBody>();
                if (body == null || PanchigiCoin.IsOutOfBoard(body.GetPosition(), bounds) == false)
                {
                    continue;
                }

                //  동전은 dynamic이라 PhysX가 진실원본이다 — World.Transform에 쓰면 다음 틱에 덮어써진다.
                //  자세는 자리의 회전이 아니라 시작 면(+up)으로 되돌린다 — 스폰과 같은 규칙이어야
                //  "초기 세팅으로 복귀"가 성립한다.
                body.SetPosition(slots[i].position.ToNumerics());
                body.SetRotation(System.Numerics.Quaternion.Identity);
                body.SetVelocity(System.Numerics.Vector3.Zero);
                body.SetAngularVelocity(System.Numerics.Vector3.Zero);
            }
        }

        private void BroadcastIfChanged(long tick, LOP.MasterData.PanchigiConfig config)
        {
            if (turn.Phase == PanchigiPhase.Over)
            {
                return;   // 종료는 기존 매치 종료 경로가 알린다
            }

            if (turn.Phase == sentPhase && turn.CurrentEntityId == sentEntityId)
            {
                return;
            }

            if (turn.Phase == PanchigiPhase.Aiming)
            {
                double interval = runner.tickUpdater?.interval ?? 0;
                long window = interval > 0 ? (long)(config.AimTimeoutSec / interval) : 0;
                aimDeadlineTick = tick + window;
            }

            sentPhase = turn.Phase;
            sentEntityId = turn.CurrentEntityId;

            var message = new PanchigiStateToC
            {
                Phase = turn.Phase == PanchigiPhase.Aiming ? 1 : 0,
                CurrentEntityId = turn.CurrentEntityId ?? string.Empty,
                AimDeadlineTick = aimDeadlineTick,
            };

            foreach (var session in sessionManager.GetAllSessions())
            {
                session.Send(message);
            }
        }
    }
}
```

> 시그니처는 확인해 둔 것들이다 — `ISessionManager.GetAllSessions()`(`WorldEventSink.cs`),
> `ITickSystem.Tick(long tick, float deltaTime)`(GameFramework), `runner.RegisterSystem<End>(this)`
> (`LOPAIController.cs`). `IRoomDataStore.match.playerList`는 `PanchigiRuleSystem`이 이미 쓰고 있다.

- [ ] **Step 2: 스코프에 등록하고 룰 시스템이 `Begin`을 부르게 한다**

`PanchigiLifetimeScope.cs`(서버) `ConfigureGame`에:

```csharp
            builder.Register<PanchigiTurnSystem>(Lifetime.Singleton);
```

`PanchigiRuleSystem`에 `PanchigiTurnSystem`을 주입하고 `Initialize` 끝에서:

```csharp
            turnSystem.Begin(playerEntityIds, coinEntityIds);
```

**방향은 한쪽이다** — 턴 시스템은 룰 시스템을 참조하지 않는다.

- [ ] **Step 3: 컴파일 + 커밋**

```bash
~/AppData/Local/Unity/bin/unity cmd recompile_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git -C ~/workspace/LOP/LeagueOfPhysical-Server add Assets/Scripts/Game/PanchigiTurnSystem.cs Assets/Scripts/Game/PanchigiTurnSystem.cs.meta Assets/Scripts/Game/PanchigiLifetimeScope.cs Assets/Scripts/Game/PanchigiRuleSystem.cs
git -C ~/workspace/LOP/LeagueOfPhysical-Server commit -m "feat(panchigi): 턴 시스템 — 정지·복귀·면 세기·상태 송신"
```

---

### Task 10: 타격에 턴 게이팅

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/MessageHandler/PanchigiStrikeMessageHandler.cs`

**Interfaces:**
- Consumes: `PanchigiTurnSystem.CanStrike`, `NotifyStruck`

- [ ] **Step 1: 생성자에 턴 시스템을 더한다**

```csharp
        private readonly PanchigiTurnSystem turnSystem;
```

- [ ] **Step 2: 참가자 확인 바로 뒤에 차례 확인을 넣는다**

```csharp
            if (turnSystem.CanStrike(userId) == false)
            {
                Debug.LogWarning($"[Panchigi] 차례가 아닌 타격 — {userId}");
                return;
            }
```

**기하 검증보다 앞**이다 — 남의 차례에 온 타격은 값이 멀쩡해도 버린다.

- [ ] **Step 3: 임펄스를 준 뒤 통지한다**

`ApplyStrike(...)` 호출 다음 줄에:

```csharp
            turnSystem.NotifyStruck(userId);
```

- [ ] **Step 4: 컴파일 + 커밋**

```bash
~/AppData/Local/Unity/bin/unity cmd recompile_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git -C ~/workspace/LOP/LeagueOfPhysical-Server add Assets/Scripts/Game/MessageHandler/PanchigiStrikeMessageHandler.cs
git -C ~/workspace/LOP/LeagueOfPhysical-Server commit -m "feat(panchigi): 자기 차례에만 칠 수 있다"
```

---

### Task 11: 종료 신호와 등수

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/IGameRuleSystem.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/LOPRunner.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/FlapWangRuleSystem.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyRaceRuleSystem.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiRuleSystem.cs`

**Interfaces:**
- Produces: `IGameRuleSystem.IsMatchOver → bool`

> ⚠️ `FlapWangRuleSystem.cs`는 **커밋하지 않는 로컬 픽스처 목록에 있다**(스폰 개수). 이 태스크는 그 파일에 한 줄을 더해야 하므로, **커밋 전에 `git diff`로 스폰 개수 변경이 섞이지 않았는지 반드시 확인**한다. 섞였으면 그 줄은 스테이지하지 않는다(`git add -p`).

- [ ] **Step 1: 인터페이스에 프로퍼티를 더한다**

```csharp
        /// <summary>이 판이 끝났나. 러너가 매 프레임 물어본다 — 종료 시점은 게임마다 다르다.</summary>
        bool IsMatchOver { get; }
```

- [ ] **Step 2: 다른 두 게임에 스텁**

`FlapWangRuleSystem`과 `FlappyRaceRuleSystem`에 각각:

```csharp
        //  아직 자기만의 종료 조건이 없다 — 러너의 시간 상한으로 끝난다.
        public bool IsMatchOver => false;
```

- [ ] **Step 3: 러너가 물어보게 한다**

`LOPRunner.LateUpdate`:

```csharp
            if (initialized && (gameRuleSystem.IsMatchOver || tickUpdater.elapsedTime > 60 * 5))
            {
                EndMatch();
            }
```

- [ ] **Step 4: 판치기가 답하게 한다**

`PanchigiRuleSystem`:

```csharp
        public bool IsMatchOver => turnSystem.IsOver;

        public MatchOutcome ResolveOutcome()
        {
            var outcome = new MatchOutcome();
            string winnerEntityId = turnSystem.WinnerEntityId;

            foreach (string userId in roomDataStore.match.playerList)
            {
                //  승자 1등 / 나머지 공동 꼴등. 무승부(승자 없음)면 전원 1등.
                int placement = winnerEntityId == null || IsWinner(userId, winnerEntityId) ? 1 : 2;
                outcome.placements.Add(new MatchPlacement { userId = userId, placement = placement });
            }

            return outcome;
        }

        private bool IsWinner(string userId, string winnerEntityId)
        {
            var entity = entityRegistry.Get(winnerEntityId);
            var ownership = entity?.Get<GameFramework.World.Ownership>();
            return ownership != null && ownership.OwnerId == userId;
        }
```

`PanchigiRuleSystem`에 `GameFramework.World.EntityRegistry entityRegistry`를 주입해야 한다.

- [ ] **Step 5: 컴파일 + 커밋**

```bash
~/AppData/Local/Unity/bin/unity cmd recompile_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git -C ~/workspace/LOP/LeagueOfPhysical-Server diff Assets/Scripts/Game/FlapWangRuleSystem.cs
git -C ~/workspace/LOP/LeagueOfPhysical-Server status --short
```

`FlapWangRuleSystem.cs`의 diff가 `IsMatchOver` 한 줄뿐인지 확인한 뒤:

```bash
git -C ~/workspace/LOP/LeagueOfPhysical-Server add Assets/Scripts/Game/IGameRuleSystem.cs Assets/Scripts/Game/LOPRunner.cs Assets/Scripts/Game/FlappyRaceRuleSystem.cs Assets/Scripts/Game/PanchigiRuleSystem.cs
git -C ~/workspace/LOP/LeagueOfPhysical-Server commit -m "feat(game): 게임이 언제 끝나는지 스스로 말한다"
```

---

### Task 12: 클라 — 상태 홀더와 수신

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/PanchigiStateStore.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/MessageHandler/PanchigiStateMessageHandler.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/PanchigiLifetimeScope.cs`

**Interfaces:**
- Produces: `LOP.PanchigiStateStore.Phase → ReadOnlyReactiveProperty<int>`, `.CurrentEntityId → ReadOnlyReactiveProperty<string>`, `.AimDeadlineTick → ReadOnlyReactiveProperty<long>`, `.Set(int phase, string currentEntityId, long aimDeadlineTick)`

- [ ] **Step 1: 홀더를 만든다**

```csharp
using R3;

namespace LOP
{
    /// <summary>
    /// 최신 판치기 턴 상태(클라). 메시지가 UI보다 먼저 도착해도 잃지 않도록 여기 담아 둔다 —
    /// reliable은 *도착*을 보장하지만 받을 준비까지 보장하지 않는다.
    /// </summary>
    public class PanchigiStateStore
    {
        private readonly ReactiveProperty<int> phase = new(0);
        private readonly ReactiveProperty<string> currentEntityId = new(string.Empty);
        private readonly ReactiveProperty<long> aimDeadlineTick = new(0);

        public ReadOnlyReactiveProperty<int> Phase => phase;
        public ReadOnlyReactiveProperty<string> CurrentEntityId => currentEntityId;
        public ReadOnlyReactiveProperty<long> AimDeadlineTick => aimDeadlineTick;

        public void Set(int phase, string currentEntityId, long aimDeadlineTick)
        {
            this.phase.Value = phase;
            this.currentEntityId.Value = currentEntityId;
            this.aimDeadlineTick.Value = aimDeadlineTick;
        }
    }
}
```

- [ ] **Step 2: 메시지 핸들러를 만든다**

```csharp
using MessagePipe;

namespace LOP
{
    public class PanchigiStateMessageHandler : MessageHandlerBase
    {
        private readonly PanchigiStateStore store;
        private readonly ISubscriber<PanchigiStateToC> subscriber;

        public PanchigiStateMessageHandler(PanchigiStateStore store, ISubscriber<PanchigiStateToC> subscriber)
        {
            this.store = store;
            this.subscriber = subscriber;
        }

        protected override void Subscribe() => Track(subscriber.Subscribe(OnState));

        private void OnState(PanchigiStateToC message)
        {
            store.Set(message.Phase, message.CurrentEntityId, message.AimDeadlineTick);
        }
    }
}
```

- [ ] **Step 3: 스코프에 등록한다**

`PanchigiLifetimeScope.cs`(클라) `ConfigureGame`에:

```csharp
            builder.Register<PanchigiStateStore>(Lifetime.Singleton);
            builder.RegisterEntryPoint<PanchigiStateMessageHandler>();
```

- [ ] **Step 4: 컴파일 + 커밋**

```bash
~/AppData/Local/Unity/bin/unity cmd recompile_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git -C ~/workspace/LOP/LeagueOfPhysical-Client add Assets/Scripts/Game/PanchigiStateStore.cs Assets/Scripts/Game/PanchigiStateStore.cs.meta Assets/Scripts/Game/MessageHandler/PanchigiStateMessageHandler.cs Assets/Scripts/Game/MessageHandler/PanchigiStateMessageHandler.cs.meta Assets/Scripts/Game/PanchigiLifetimeScope.cs
git -C ~/workspace/LOP/LeagueOfPhysical-Client commit -m "feat(panchigi): 턴 상태를 받아 담는다"
```

---

### Task 13: 클라 — 내 차례 UI 한 줄

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/UI/PanchigiTurn/PanchigiTurn.uxml`
- Create: `LeagueOfPhysical-Client/Assets/UI/PanchigiTurn/PanchigiTurn.uss`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/UI/PanchigiTurn/PanchigiTurnViewModel.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/UI/PanchigiTurn/PanchigiTurnView.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/PanchigiHudCoordinator.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/PanchigiLifetimeScope.cs`

**Interfaces:**
- Consumes: `PanchigiStateStore`, `IPlayerContext.entityId`, `IRunner.tickUpdater`
- Produces: `LOP.UI.PanchigiTurnViewModel.Label → string` (매 프레임 계산), `LOP.UI.PanchigiTurnView : UIView`

- [ ] **Step 1: UXML**

`Assets/UI/PanchigiTurn/PanchigiTurn.uxml` — `FlapPad.uxml`을 열어 루트 구조와 USS 참조 방식을 따라 쓴다.

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <Style src="PanchigiTurn.uss" />
    <ui:VisualElement name="panchigi-turn" class="panchigi-turn">
        <ui:Label name="turn-label" class="turn-label" text="" />
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: USS**

```css
.panchigi-turn {
    position: absolute;
    top: 24px;
    left: 0;
    right: 0;
    align-items: center;
}

.turn-label {
    font-size: 28px;
    color: rgb(255, 255, 255);
    -unity-text-align: middle-center;
}
```

- [ ] **Step 3: ViewModel**

```csharp
using GameFramework.Runner;
using R3;

namespace LOP.UI
{
    /// <summary>
    /// 내 차례인지와 남은 시간. 남은 시간은 서버가 보내 준 *마감 틱*에서 매 프레임 계산한다 —
    /// 초마다 메시지를 받을 필요가 없다.
    /// </summary>
    public class PanchigiTurnViewModel
    {
        private const int AimingPhase = 1;

        private readonly PanchigiStateStore store;
        private readonly IPlayerContext playerContext;
        private readonly IRunner runner;

        public PanchigiTurnViewModel(PanchigiStateStore store, IPlayerContext playerContext, IRunner runner)
        {
            this.store = store;
            this.playerContext = playerContext;
            this.runner = runner;
        }

        public string Label()
        {
            if (store.Phase.CurrentValue != AimingPhase)
            {
                return "동전이 멈추는 중";
            }

            if (store.CurrentEntityId.CurrentValue != playerContext.entityId)
            {
                return "다른 사람 차례";
            }

            return $"내 차례 · {RemainingSeconds()}";
        }

        private int RemainingSeconds()
        {
            double interval = runner.tickUpdater?.interval ?? 0;
            if (interval <= 0)
            {
                return 0;
            }

            long left = store.AimDeadlineTick.CurrentValue - runner.tickUpdater.tick;
            return left <= 0 ? 0 : (int)System.Math.Ceiling(left * interval);
        }
    }
}
```

- [ ] **Step 4: View**

```csharp
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>내 차례 한 줄. 남은 시간이 매 프레임 변하므로 스케줄러로 갱신한다.</summary>
    public class PanchigiTurnView : UIView
    {
        private readonly PanchigiTurnViewModel _viewModel;

        private IVisualElementScheduledItem _tick;

        public PanchigiTurnView(PanchigiTurnViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            var label = Root.Q<Label>("turn-label");
            _tick = Root.schedule.Execute(_ => label.text = _viewModel.Label()).Every(0);
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    _tick?.Pause();
                }
            }

            base.Dispose(disposing);
        }
    }
}
```

- [ ] **Step 5: 코디네이터 — 화면을 연다**

`FlappyHudCoordinator.cs`를 본떠 만든다. 판치기는 아바타가 없어 `EntityCreated`로 내 캐릭을 기다릴 수 없으므로, **첫 상태 메시지가 오면 연다.**

```csharp
using MessagePipe;

namespace LOP
{
    public class PanchigiHudCoordinator : MessageHandlerBase
    {
        private readonly IWindowManager windowManager;
        private readonly ISubscriber<PanchigiStateToC> subscriber;

        private bool opened;

        public PanchigiHudCoordinator(IWindowManager windowManager, ISubscriber<PanchigiStateToC> subscriber)
        {
            this.windowManager = windowManager;
            this.subscriber = subscriber;
        }

        protected override void Subscribe() => Track(subscriber.Subscribe(_ => Open()));

        private void Open()
        {
            if (opened)
            {
                return;
            }

            windowManager.Open<LOP.UI.PanchigiTurnView>();
            opened = true;
        }
    }
}
```

- [ ] **Step 6: 스코프에 등록한다**

`PanchigiLifetimeScope.cs`(클라):

```csharp
            builder.Register<LOP.UI.PanchigiTurnViewModel>(Lifetime.Transient);
            builder.Register<LOP.UI.PanchigiTurnView>(Lifetime.Transient);
            builder.RegisterEntryPoint<PanchigiHudCoordinator>();
```

그리고 `FlappyRaceLifetimeScope`처럼 `RegisterViewFactories`를 오버라이드한다:

```csharp
        protected override void RegisterViewFactories(
            IObjectResolver container, IWindowManager windowManager, List<IDisposable> sink)
        {
            sink.Add(windowManager.RegisterViewFactory<LOP.UI.PanchigiTurnView>(
                () => container.Resolve<LOP.UI.PanchigiTurnView>()));
        }
```

- [ ] **Step 7: 컴파일 + 커밋**

```bash
~/AppData/Local/Unity/bin/unity cmd recompile_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git -C ~/workspace/LOP/LeagueOfPhysical-Client status --short
```

`Assets/UI/PanchigiTurn/**`, `Assets/Scripts/UI/PanchigiTurn/**`, `Assets/Scripts/Game/PanchigiHudCoordinator.cs`, `PanchigiLifetimeScope.cs`를 `.meta`와 함께 add 후 커밋:

```bash
git -C ~/workspace/LOP/LeagueOfPhysical-Client commit -m "feat(panchigi): 내 차례를 한 줄로 보여준다"
```

---

### Task 14: 클라 — 입력 게이팅

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/PanchigiStrikeInput.cs`

- [ ] **Step 1: 홀더를 주입받는다**

```csharp
        [Inject] private PanchigiStateStore stateStore;
```

- [ ] **Step 2: `BeginAim` 첫머리에 가드를 넣는다**

```csharp
        private const int AimingPhase = 1;

        private void BeginAim(Vector2 screenPosition)
        {
            //  내 차례가 아니면 조준을 시작하지 않는다 — 조준선이 안 뜨는 것이 곧 안내다.
            if (stateStore.Phase.CurrentValue != AimingPhase
                || stateStore.CurrentEntityId.CurrentValue != playerContext.entityId)
            {
                return;
            }

            if (TryBoardPoint(screenPosition, out Vector3 point) == false)
            {
                return;
            }
            ...
```

- [ ] **Step 3: 차례가 끝나면 조준을 접는다**

`Update` 첫머리에 넣어, 조준 중에 차례가 넘어가면 조준선이 남지 않게 한다.

```csharp
            if (aiming
                && (stateStore.Phase.CurrentValue != AimingPhase
                    || stateStore.CurrentEntityId.CurrentValue != playerContext.entityId))
            {
                aiming = false;
                SetAimLineVisible(false);
            }
```

- [ ] **Step 4: 컴파일 + 커밋**

```bash
~/AppData/Local/Unity/bin/unity cmd recompile_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git -C ~/workspace/LOP/LeagueOfPhysical-Client add Assets/Scripts/Game/PanchigiStrikeInput.cs
git -C ~/workspace/LOP/LeagueOfPhysical-Client commit -m "feat(panchigi): 내 차례에만 조준한다"
```

---

### Task 15: 에디터 검증 루틴

EditMode 테스트를 만들지 않기로 했으므로 **여기가 유일한 자동 검증**이다. `Assembly-CSharp-Editor`는 `Assembly-CSharp`를 참조할 수 있어 asmdef 없이 판치기 코드에 닿는다.

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Editor/PanchigiVerification.cs`

- [ ] **Step 1: 루틴을 만든다**

```csharp
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 판치기 판정·전이를 표로 찍는다. 눈으로 틀린 걸 알기 어려운 것들이라 값을 직접 본다.
    /// unity CLI의 menu 명령으로 헤드리스 재실행이 된다.
    /// </summary>
    public static class PanchigiVerification
    {
        [MenuItem("LOP/판치기 검증")]
        public static void Run()
        {
            var sb = new StringBuilder();
            Flip(sb);
            OutOfBoard(sb);
            Rest(sb);
            Turn(sb);
            Coverage(sb);
            Debug.Log(sb.ToString());
        }

        private static void Flip(StringBuilder sb)
        {
            sb.AppendLine("[면] 기울기 → 뒤집힘");
            foreach (float deg in new[] { 0f, 45f, 89f, 90f, 91f, 135f, 180f })
            {
                Quaternion q = Quaternion.Euler(deg, 0f, 0f);
                bool flipped = PanchigiCoin.IsFlipped(
                    new System.Numerics.Quaternion(q.x, q.y, q.z, q.w));
                sb.AppendLine($"  {deg,5:F0}도 → {flipped}");
            }
            sb.AppendLine("  기대: 0/45/89/90 = False, 91/135/180 = True");
        }

        private static void OutOfBoard(StringBuilder sb)
        {
            var board = new Bounds(new Vector3(0f, -0.05f, 0f), new Vector3(10f, 0.1f, 10f));
            sb.AppendLine("[장외] 위치 → 판 밖");
            foreach (var p in new[]
            {
                new Vector3(0f, 0.02f, 0f), new Vector3(4.9f, 0.02f, 0f), new Vector3(5.1f, 0.02f, 0f),
                new Vector3(0f, 0.02f, -5.1f), new Vector3(0f, -1f, 0f),
            })
            {
                bool outside = PanchigiCoin.IsOutOfBoard(
                    new System.Numerics.Vector3(p.x, p.y, p.z), board);
                sb.AppendLine($"  {p} → {outside}");
            }
            sb.AppendLine("  기대: 판 안 2개 False, 나머지 True");
        }

        private static void Rest(StringBuilder sb)
        {
            sb.AppendLine("[정지] (선속도, 각속도) → 멎음");
            foreach (var v in new[] { (0f, 0f), (1f, 0f), (0f, 1f), (0.01f, 0.01f) })
            {
                bool rest = PanchigiCoin.IsAtRest(
                    new System.Numerics.Vector3(v.Item1, 0f, 0f),
                    new System.Numerics.Vector3(0f, v.Item2, 0f), 0.05f, 0.1f);
                sb.AppendLine($"  ({v.Item1}, {v.Item2}) → {rest}");
            }
            sb.AppendLine("  기대: (0,0)·(0.01,0.01) True, 나머지 False — 각속도만 커도 안 멎은 것");
        }

        private static void Turn(StringBuilder sb)
        {
            sb.AppendLine("[전이]");
            var players = new[] { "P1", "P2" };

            var t = new PanchigiTurn(players, 60);
            t.OnRested(false);
            sb.AppendLine($"  첫 정지 → {t.Phase} / {t.CurrentEntityId} (기대: Aiming / P1)");

            t.OnStruck("P1");
            sb.AppendLine($"  타격 → {t.Phase} / 턴 {t.TurnCount} (기대: Settling / 1)");

            t.OnRested(false);
            sb.AppendLine($"  정지 → {t.Phase} / {t.CurrentEntityId} (기대: Aiming / P2)");

            t.OnAimTimeout();
            sb.AppendLine($"  패스 → {t.Phase} / {t.CurrentEntityId} / 턴 {t.TurnCount} (기대: Aiming / P1 / 2)");

            t.OnStruck("P1");
            t.OnRested(true);
            sb.AppendLine($"  다 뒤집힘 → {t.Phase} / 승자 {t.WinnerEntityId} (기대: Over / P1)");

            var limited = new PanchigiTurn(players, 1);
            limited.OnRested(false);
            limited.OnAimTimeout();
            limited.OnAimTimeout();
            sb.AppendLine($"  상한 초과 → {limited.Phase} / 승자 {limited.WinnerEntityId ?? "없음"} (기대: Over / 없음)");
        }

        private static void Coverage(StringBuilder sb)
        {
            //  잃어버린 EditMode 테스트 자리 — 샘플이 몇 개 살았느냐에 세기가 비례하는지 본다.
            sb.AppendLine("[덮임] 살아있는 샘플 수 → 임펄스 y");
            var input = new PanchigiStrike.StrikeInput(
                System.Numerics.Vector3.Zero, System.Numerics.Vector3.Zero, 1f);
            var tuning = new PanchigiStrike.StrikeTuning(40f, 20f, 4f);

            const int total = 13;
            var samples = new System.Numerics.Vector3[total];
            PanchigiStrike.BuildSamples(System.Numerics.Vector3.Zero, 0.3f, samples);

            foreach (int live in new[] { 0, 1, 7, 13 })
            {
                var impulse = PanchigiStrike.ComputeImpulse(input, tuning, samples, live, total);
                sb.AppendLine($"  {live,2}/{total} → y={impulse.Y:F3}");
            }
            sb.AppendLine("  기대: 0이면 0, 살아있는 수가 늘수록 단조 증가, 13/13이 최대");
        }
    }
}
```

- [ ] **Step 2: 돌려서 표를 본다**

```bash
~/AppData/Local/Unity/bin/unity cmd menu --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server -- --path "LOP/판치기 검증"
~/AppData/Local/Unity/bin/unity cmd console --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server -- --limit 5
```

각 절의 "기대"와 실제가 다르면 **구현이 틀린 것**이다 — 기대를 고치지 말고 구현을 고친다.

- [ ] **Step 3: 커밋**

```bash
git -C ~/workspace/LOP/LeagueOfPhysical-Server add Assets/Editor/PanchigiVerification.cs Assets/Editor/PanchigiVerification.cs.meta
git -C ~/workspace/LOP/LeagueOfPhysical-Server commit -m "test(panchigi): 판정·전이를 표로 찍는 에디터 검증"
```

---

### Task 16: 배포와 실플레이 검증

> **푸시·배포는 사용자 승인을 받고 진행한다.**

**Files:** 없음(배포·검증)

- [ ] **Step 1: 8레포 상태 확인**

```bash
for r in GameFramework LeagueOfPhysical-Shared LeagueOfPhysical-Client LeagueOfPhysical-Server \
         LeagueOfPhysical-MasterData-Client LeagueOfPhysical-MasterData-Server infrastructure lop-backend; do
  d=~/workspace/LOP/$r
  git -C "$d" fetch origin -q
  echo "$r: $(git -C "$d" rev-parse --abbrev-ref HEAD) unpushed=$(git -C "$d" rev-list --count origin/main..HEAD)"
done
```

- [ ] **Step 2: 레포마다 푸시 규약을 밟는다 (한 줄씩, `&&`로 잇지 않는다)**

```bash
git -C <repo> fetch origin
git -C <repo> rebase --autostash origin/main
git -C <repo> checkout main
git -C <repo> merge --ff-only origin/main
git -C <repo> merge --no-ff <feature>
git -C <repo> push origin main
```

`--force`/`--force-with-lease` 금지. 거절되면 다시 `fetch` → 리베이스 → 재시도.

- [ ] **Step 3: 게임서버 배포**

```bash
gh api repos/Baeinsoo/LeagueOfPhysical-Server/actions/runners --jq '.runners[] | "\(.name) \(.status)"'
gh workflow run gameserver-deploy.yml --repo Baeinsoo/LeagueOfPhysical-Server --ref main -f environment=local
```

- [ ] **Step 4: 에셋 배포가 필요한지 판단한다**

이 슬라이스는 씬을 고쳤다. 판치기 씬이 어드레서블 원격 그룹에 걸려 있으면 `content-deploy`가 필요하다.

```bash
grep -rn "Panchigi" ~/workspace/LOP/LeagueOfPhysical-Client/Assets/AddressableAssetsData/AssetGroups/*.asset
```

걸려 있으면:

```bash
gh workflow run content-deploy.yml --repo Baeinsoo/LeagueOfPhysical-Client --ref main -f target=gameserver
```

`backend-deploy`는 불필요하다 — 판치기 노브는 Luban group `m`에 안 닿는다. 진행 전 `lop-backend`가 정말 무변경인지 확인한다.

- [ ] **Step 5: ArgoCD 롤아웃 확인**

```bash
kubectl -n argocd annotate application backend argocd.argoproj.io/refresh=hard --overwrite
kubectl get cm -o jsonpath='{range .items[*]}{.metadata.name}{" "}{.data.GAME_SERVER_IMAGE}{"\n"}{end}' | grep -i game
```

- [ ] **Step 6: 두 클라 실플레이**

메인 에디터와 MPPM 클론(`Library/VP/mppm61234b75`)을 띄워 판치기를 잡고 확인한다.

| 확인 | 기대 |
|---|---|
| 내 차례가 아닐 때 화면 | "다른 사람 차례" — 눌러도 조준선이 안 뜬다 |
| 내 차례 | "내 차례 · N" — N이 매초 줄어든다 |
| 안 치고 기다림 | 20초 뒤 다음 사람에게 넘어간다 |
| 동전을 다 뒤집음 | 그 사람이 결과 화면에 1등으로 뜬다 |
| 동전을 판 밖으로 냄 | 다음 정지 때 제자리로 돌아오고 **다시 칠 수 있다** |
| 서버 로그 | `Exception` 0건 |

```bash
POD=$(kubectl get pods -o name | grep room-pod | head -1)
kubectl logs "$POD" 2>&1 | grep -c "Exception"
```

- [ ] **Step 7: ROADMAP 갱신**

`docs/ROADMAP.md`의 "다음에 할 것"에서 슬라이스 4 줄을 내리고, Done 원장에 항목을 더한다. 검증 결과(위 표)와 잠긴 결정(규칙은 서버 것 / 스폰 위치는 씬 / 테스트 대신 에디터 검증)을 같이 남긴다. 피처 브랜치에서 커밋해 푸시 규약대로 머지한다.
