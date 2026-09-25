# RNG 추상화 — `IRandom` 포트 (서버 시뮬, Slice 4)

**Date:** 2026-06-22
**Branch (제안):** `feature/slice4-random-abstraction`
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) (결정론·롤백 — RNG 상태가 스냅샷에 포함돼야 함) · [netcode-redesign](../../netcode-redesign.md) §6.5 (Stage④) · 자매 슬라이스 [물리 시뮬레이션 어댑터](2026-06-21-slice4-physics-simulator-adapter-design.md)

## Goal

서버 시뮬레이션의 `UnityEngine.Random` 직접 호출을 주입된 `IRandom`(GameFramework) 포트 뒤로 격리한다. 구체 구현은 `UnityEngine.Random.Range`를 호출하는 얇은 어댑터(`UnityRandom`)다. **동작 100% 보존**(같은 PRNG, 같은 의미론). 물리 어댑터와 동일한 port/adapter 패턴.

## 배경 / 동기

Stage④ 시뮬 추출의 한 조각. **이 추상화의 진짜 가치는 *나중*에 있다** — 예측·롤백에서 같은 틱을 여러 번 재시뮬할 때 RNG 굴림이 재현돼야 하므로, **시드 기반 RNG의 *상태*가 스냅샷에 포함**돼야 한다(전역 mutable static인 `UnityEngine.Random`으로는 불가). 이 슬라이스는 그 *시드/스냅샷 통합*까지 하지 않고 **seam만** 만든다(wrap-only, 비결정론 그대로). 시드 동기·리셋·스냅샷 연동은 롤백 슬라이스에서 함께 설계한다.

- **범위 = 서버 시뮬 호출부만.** 클라 `DamageFloaterEmitter`의 지터(`Random.Range(-1f,1f)`)는 *뷰 랜덤*(프레젠테이션)이라 시뮬 결정론과 무관 → 추상화 대상 아님(`UnityEngine.Random` 그대로). 회전 ease를 뷰로 분리한 것과 같은 논리 — 뷰 랜덤을 추상화하면 "이것도 결정론 대상"이라는 잘못된 신호를 준다.
- 즉시 이득은 약함(`LOPCombatSystem`에 고정 `IRandom` 주입으로 전투 단위테스트 *가능해짐*뿐). 핵심 이득은 deferred. 사용자가 이를 알고 진행 결정함.

## 현재 상태 (실측)

서버 시뮬 호출부 (전부 인스턴스 컨텍스트 — 주입 필드 접근 가능):

- `Assets/Scripts/CombatSystem/LOPCombatSystem.cs` (`ICombatSystem` DI 싱글톤):
  - `:65` `Random.Range(1.25f, 1.75f)` — 데미지 변동 (float, max 포함)
  - `:105` `Random.Range(0.0f, 1.0f)` — 크리 굴림 (float)
  - `:113` `Random.Range(0.0f, 1.0f)` — 회피 굴림 (float)
- `Assets/Scripts/Game/LOPGame.cs` (`IGame`, `[Inject]` 필드 가진 주입 MonoBehaviour, `RegisterComponent`):
  - `:104` `UnityEngine.Random.Range(0, 3)` — 캐릭터 선택 (int, max 제외)
  - `:249` `UnityEngine.Random.Range(0, 2)` — 몬스터 선택 (int)
  - `:325` `UnityEngine.Random.Range(-20f, 20f)` ×2 — 스폰 위치 (float)

클라: `Assets/Scripts/UI/WorldSpace/DamageFloaterEmitter.cs:122` — **뷰 지터, 범위 밖.**

GameFramework asmdef: `baegames.GameFramework.Runtime`(noEngineReferences=false)에 자매 포트 `IPhysicsSimulator`/`UnityPhysicsSimulator`가 이미 있음.

## 설계

### 신규 타입 (GameFramework)

`baegames.GameFramework.Runtime`, `Runtime/Scripts/Game/`(`IPhysicsSimulator` 옆 — 짝 일관):

- **`IRandom`** — RNG 포트. **실제 쓰이는 2개 메서드만**(YAGNI):
  ```csharp
  namespace GameFramework
  {
      /// <summary>
      /// 난수 굴림 포트. 시뮬레이션이 구체 RNG(UnityEngine.Random 등)에 직결되지 않도록 주입한다.
      /// 현재 구현(UnityRandom)은 비결정론 — 시드 기반 결정론 구현은 롤백 단계에서 drop-in.
      /// </summary>
      public interface IRandom
      {
          /// <summary>[minInclusive, maxInclusive] 범위의 float. (UnityEngine.Random.Range(float,float) 의미론 — max 포함)</summary>
          float Range(float minInclusive, float maxInclusive);

          /// <summary>[minInclusive, maxExclusive) 범위의 int. (UnityEngine.Random.Range(int,int) 의미론 — max 제외)</summary>
          int Range(int minInclusive, int maxExclusive);
      }
  }
  ```
  > float은 max 포함, int은 max 제외 — Unity의 비대칭 의미론을 *그대로* 계승한다(래핑이라 자동 보존). 향후 시드 구현도 이 계약을 지켜야 한다.

- **`UnityRandom`** — Unity 어댑터.
  ```csharp
  namespace GameFramework
  {
      /// <summary>UnityEngine.Random으로 <see cref="IRandom"/>을 구현하는 어댑터(비결정론, 전역 상태).</summary>
      public sealed class UnityRandom : IRandom
      {
          public float Range(float minInclusive, float maxInclusive)
              => UnityEngine.Random.Range(minInclusive, maxInclusive);

          public int Range(int minInclusive, int maxExclusive)
              => UnityEngine.Random.Range(minInclusive, maxExclusive);
      }
  }
  ```
  `UnityEngine.Random`을 풀 네임스페이스로 한정(어댑터가 `namespace GameFramework`라 `Random` 단축 시 모호 없음이나 명시).

### 서버 호출부 교체

- **`LOPCombatSystem`**: 현재 DI 주입 방식(생성자 또는 `[Inject]`)에 `IRandom`을 추가하고 3곳 `Random.Range(...)` → `random.Range(...)`. (정확한 주입 방식은 plan에서 기존 패턴 확인.)
- **`LOPGame`**: `[Inject] private IRandom random;` 추가, 3곳(`:104`/`:249`/`:325`의 2회) `UnityEngine.Random.Range(...)` → `random.Range(...)`.

### DI 등록

서버 `Assets/Scripts/Game/GameLifetimeScope.cs`의 `Configure`에 `builder.Register<GameFramework.IRandom, GameFramework.UnityRandom>(Lifetime.Singleton);` 추가(World Core 등록 부근). **클라 스코프 무변경.**

## 동작 보존 / 검증

- **동작 보존:** 어댑터가 동일 `UnityEngine.Random.Range`를 같은 인자·의미론으로 호출 → 굴림 시퀀스 동일(같은 전역 PRNG, 같은 소비 순서).
- **검증:** 서버 컴파일 클린(`read_console`), 실행 시 전투(데미지 변동/크리/회피)·스폰(캐릭터/몬스터 선택, 위치)이 교체 전과 정상 동작. 클라 무변경이라 클라 영향 0.
- **유닛 테스트:** 어댑터는 정적 래핑이라 단위테스트 부적합. `LOPCombatSystem`은 서버 Assembly-CSharp라 asmdef 테스트 불가. 이 슬라이스는 *동작 보존 슬라이스*라 신규 테스트 없음 — seam의 페이오프는 향후 고정 `IRandom` 주입(전투 결정론 테스트) + 시드 결정론.

## 산업 표준 매핑

- `IRandom`/`UnityRandom` = `IPhysicsSimulator`/`UnityPhysicsSimulator`와 동일한 port/adapter(헥사고날) 패턴, 짝 일관 네이밍.
- 결정론 RNG는 게임 넷코드 표준 요소: Photon Quantum `RNGSession`(시드·결정론), Unity DOTS `Unity.Mathematics.Random`(시드 struct, 스냅샷 가능). 이들은 *시드 기반*인데, 본 슬라이스는 그 단계 전(wrap-only)이라 인터페이스만 그 방향으로 잡아둔다.
- 메서드 시그니처는 `UnityEngine.Random.Range`(float max-inclusive / int max-exclusive)를 미러 — 기존 호출부 의미 보존.

## Out of Scope

- **결정론/시드:** 시드 기반 구현, 시드 동기(클·서), 틱/매치별 리셋, RNG 상태의 스냅샷 통합 — 전부 롤백 슬라이스.
- **클라 뷰 랜덤:** `DamageFloaterEmitter` 지터 — 프레젠테이션, 그대로.
- **인터페이스 확장:** `value`/`Next01`/`insideUnitSphere` 등 — 현재 미사용, 쓸 때 추가(YAGNI).
- **다른 Slice 4 조각:** 시뮬 골격·I/O 어댑터·모션 권위 역전 — 별도.

## Open Questions (구현 plan에서 해소)

- `LOPCombatSystem`의 기존 주입 방식(생성자 주입 vs `[Inject]` 필드) 확인 후 `IRandom` 동일 방식으로 추가.
- `IRandom.cs`/`UnityRandom.cs`를 `Runtime/Scripts/Game/` 직하 vs 하위 폴더(물리 파일 배치 관습 따름).

## 진행

- [x] 브레인스토밍 합의(서버 시뮬만, GameFramework 배치, wrap-only, 결정론 deferred)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
