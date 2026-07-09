# 확정 게이트 — 재생 억제 (commit gate via replay suppression)

**Date:** 2026-07-09
**Branch:** `feature/commit-gate-replay-suppression`
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) (Deferred / 확정 게이트) · [netcode-redesign](../../netcode-redesign.md) (Stage④) · [ROADMAP](../../ROADMAP.md) (Next #1)

## Goal

내 캐릭 롤백 **재생(replay)** 이 과거 틱을 다시 계산할 때 만들어내는 **연출 이벤트가 화면으로 새어나가지 않도록** 구조적으로 막는다. 지금은 `Reconciler`가 연출 이벤트를 만드는 정식 통로(`AbilityActivator`)를 **우회**해서 손으로 막고 있는데, 이 손-회피를 **`WorldEventBuffer`의 억제 스코프**로 대체한다.

한 줄 요약: **"재생 도는 동안은 연출 이벤트를 버린다"** 를 버퍼 레벨의 한 스위치로 만든다.

## 배경 / 동기

### 라이브 vs 재생

클라는 서버를 기다리지 않고 내 캐릭을 **먼저 시뮬(라이브)** 한다. 서버 스냅이 도착해 예측과 어긋나면, `Reconciler`가 앵커 틱으로 하드 복원한 뒤 **과거 틱(anchor+1 ~ current-1)을 다시 계산(재생)** 해 위치·상태를 바로잡는다.

- **라이브**: 그 틱을 처음, 실시간으로, **화면에 보여주며** 실행.
- **재생**: 나중에 그 틱들을 **다시 계산**(위치 교정용). 화면엔 이미 라이브 때 보여줬으므로 **연출을 또 내보내면 안 된다.**

### 문제 — 재생이 연출을 다시 만든다

재생은 위치만 고치는 게 아니라 **그 틱에 있었던 일을 통째로 다시 계산**한다. 스킬을 눌렀던 틱이면 **스킬 발동도 다시** 계산한다(스킬이 velocity·위치에 영향을 주므로 위치 교정에 필요). 그런데 스킬을 다시 발동하면 그 **발동 연출 cue(`AbilityActivatedEvent`)도 딸려서 또** 만들어진다 → 같은 연출을 두 번 보게 됨.

즉 재생 틱에서 필요한 것과 아닌 것이 갈린다:

| 부분 | 재생 때 다시? |
|---|---|
| 스킬 발동 **계산** (velocity·위치) | ✅ 해야 함 |
| 발동 **연출 cue** (화면·소리) | ❌ 이미 라이브에 보여줌 |

### 지금의 손-회피 (제거 대상)

`Reconciler.cs:129-138` 은 cue를 만드는 정식 통로 `AbilityActivator.TryActivate`(발동 계산 + cue `Append`) 를 안 쓰고, 그 **안쪽의 발동 계산만**(`AbilitySystem.TryActivate`) 직접 호출한다 → 계산은 되고 cue Append는 건너뛴다.

```csharp
// 현재: AbilityActivator를 우회, 발동 계산만 직접 호출 (cue Append 스킵)
abilitySystem.TryActivate(worldEntity, data, worldEntity, t);
```

이 방식의 냄새:
- **같은 발동인데 경로가 둘** — 라이브=`AbilityActivator`, 재생=`AbilitySystem` 직접.
- 발동에 무언가(타깃 해석, 다른 cue 등)를 붙이면 **두 곳을 다 맞춰야** 하고 하나 까먹으면 재생이 미묘하게 어긋난다. (`Reconciler.cs:135-136` 주석이 *"향후 타깃 예측 어빌리티 추가 시 여기서도 맞출 것"* 이라고 이미 경고.)

### 클라에서 재생이 재생성하는 연출 = 현재 cue 하나뿐

- `DamageDealtEvent` 는 클라에선 **와이어산**(`GameDamageMessageHandler`) — 클라 `AbilityEffectExecutor` 에 `DamageEffectHandler` 가 없어 로컬 시뮬이 데미지를 안 만든다.
- 그래서 재생 루프가 버퍼에 넣을 수 있는 **로컬 시뮬 연출 = `AbilityActivatedEvent`(cue)** 뿐. 다만 설계는 "무엇이든 재생이 만든 이벤트"에 일반적으로 동작해야 한다(향후 status apply 등이 붙어도 안전).

## 결정

### 방식 1 — 재생 억제 스코프 (채택)

재생이 도는 동안 `WorldEventBuffer` 를 **억제 모드**로 둔다. 이 동안의 `Append` 는 **no-op**(버림). 재생이 끝나면 해제. 재생 코드는 **정식 통로(`AbilityActivator`)를 그대로** 쓰고, 억제가 cue를 버려준다 → 손-회피 제거.

### 왜 방식 1인가 (방식 2·3 대비)

세 방식은 **같은 표준 정책**("연출은 라이브만, 재생 땐 스킵")의 서로 다른 구현이다:

| | 구현 | 지금 적합도 |
|---|---|---|
| **1. 재생 억제 (채택)** | 재생이 만든 이벤트를 버린다 | 🟢 재생이 **이미 라이브에 보여준 틱만** 다시 도는 우리 경우엔 이게 정확·최소 |
| 2. 틱 워터마크 | 이벤트에 틱 도장, "이미 낸 최고 틱" 이하 스킵 | 🟡 와이어 이벤트 예외 태그가 추가로 필요, 이득 대비 부품 과다 |
| 3. 해시 dedup (Quantum) | 재생이 다시 만들되 (내용+틱) 해시로 중복 제거 | 🔴 **예측이 틀려 결과가 달라지는 경우**까지 커버 = B 영역. 지금은 클라 예측 이벤트 생산자가 없어 쓸 데 없음(YAGNI) |

방식 1은 **Quantum non-synced 이벤트 정책의 경량 구현**이고, **B(예측/확정)로 갈 때 방식 3으로 확장**된다 — 방식 1이 깐 "단일 egress 제어점 + 라이브만/재생 스킵 baseline"을 B가 dedup+취소로 얹는다. `WorldEvent` 틱 도장은 방식 1엔 불필요하므로 **지금 넣지 않고 B에서** 추가한다(YAGNI, ROADMAP Next #2에 박제).

### 통합 fan-out 은 드롭

조사 결과 클라 egress는 이미 설계대로 올바르게 분리돼 있다 — durable 상태(HP/MP/Lv/Stat)=스냅샷 pull, 생명주기(Created/Destroyed)=뷰-스포너 별개 축, 시뮬 연출=버퍼 드레인. 이들을 하나로 "통합"하면 오히려 아키텍처를 훼손한다. 문서가 말한 "통합 fan-out"에 가까운 실제 잔여물은 **서버쪽 backlog #2**(despawn cascade가 egress에서 새 사실 생성)이며 이는 서버 레포 + 별개 슬라이스다.

## 설계

작은 두 조각. 각 조각은 독립적으로 이해·테스트 가능하다.

### 1) `GameFramework.World.WorldEventBuffer` — 억제 스코프

버퍼에 "억제 중이면 Append를 무시"하는 기능을 추가한다. 억제는 **스코프(IDisposable)** 로 열고 닫아 예외에도 안전하게 복원한다(중첩 대비 카운터).

```csharp
public class WorldEventBuffer
{
    private readonly List<WorldEvent> _events = new();
    private int _suppressDepth;   // 0 = 정상, >0 = 억제 중

    /// <summary>억제 중이면 Append는 무시된다(재생 등 확정 전 재시뮬 구간용).</summary>
    public void Append(WorldEvent e)
    {
        if (e == null) throw new ArgumentNullException(nameof(e));
        if (_suppressDepth > 0) return;   // 억제 중 — 버림
        _events.Add(e);
    }

    /// <summary>이 스코프 동안의 Append를 버린다. Dispose 시 해제(중첩 안전).</summary>
    public IDisposable Suppress() => new SuppressScope(this);

    private sealed class SuppressScope : IDisposable
    {
        private WorldEventBuffer _b;
        public SuppressScope(WorldEventBuffer b) { _b = b; _b._suppressDepth++; }
        public void Dispose() { if (_b != null) { _b._suppressDepth--; _b = null; } }
    }

    // Snapshot / Clear / Count 그대로
}
```

- **버퍼는 "왜" 억제하는지 모른다** — "재생"이라는 개념은 호출자(`Reconciler`)의 것이고, 버퍼는 dumb하게 "억제 스위치"만 제공. (레이어 책임 분리)
- 서버는 재생이 없어 `Suppress()` 를 절대 호출하지 않는다 → 서버 동작 무변경.
- 억제는 **이미 버퍼에 있는 이벤트를 건드리지 않고**, 억제 스코프 안에서의 **새 Append만** 무시한다.

### 2) `Reconciler` — 재생 루프를 억제 스코프로 감싸고 손-회피 제거

```csharp
[Inject] private GameFramework.World.WorldEventBuffer worldEventBuffer;
[Inject] private AbilityActivator abilityActivator;   // abilitySystem 직접 호출 대신

// 재생 루프
var inputBuffer = worldEntity.Get<InputBuffer>();   // 입력 버퍼 (WorldEventBuffer 아님 — 이름 구분)
using (worldEventBuffer.Suppress())
{
    for (long t = anchorTick + 1; t < currentTick; t++)
    {
        var cmd = inputHistory.TryGet(t, out var rec) ? rec : null;
        inputBuffer.Current = cmd;

        // 발동 재현: 이제 정식 통로를 그대로 쓴다. cue Append는 억제 스코프가 버린다.
        if (cmd != null && cmd.AbilityId != 0)
            abilityActivator.TryActivate(worldEntity.Id, cmd.AbilityId, t);

        movementSystem.Tick(worldEntity, t, deltaTime);
        abilitySystem.Tick(worldEntity, t);
        statusEffectSystem.Tick(worldEntity, t);
        abilityEffectExecutor.DriveActiveEntity(worldEntity, entityManager, t);
        // ... 물리/스냅샷 기록 동일
    }
}
```

- 재생이 `AbilityActivator.TryActivate`(발동 계산 + cue Append)를 부르지만, **억제 스코프 안이라 cue Append가 no-op** → 계산은 되고 연출은 안 나감.
- `Reconciler` 가 더 이상 `abilityDataProvider` 로 ability를 미리 조회하거나 `abilitySystem.TryActivate` 를 직접 부르지 않아도 된다(정식 통로가 내부에서 처리) → 경로가 하나로 합쳐지고 `Reconciler.cs:129-138` 의 우회 주석/코드가 사라진다.
- 억제는 **재생 루프에만** 걸린다. 루프 밖(하드 복원, 스냅샷 기록 등)과 이 프레임의 다른 지점은 영향 없음.

## 데이터 흐름 — 억제는 "시간"이 아니라 "호출 스택"

핵심 안전성 근거: 게임 로직은 **단일 스레드**로 한 줄씩 실행되고, 재생 루프는 **중간에 멈추지 않는(await 없는) 동기 블록**이다. 따라서 억제 스코프 동안엔 **다른 코드가 끼어들 수 없다** — 억제는 "벽시계 시간 창"이 아니라 "재생 루프가 동기적으로 만든 Append"만 잡는다.

```
한 프레임 (위 → 아래 순서 실행):

 [네트워크 메시지 처리]   서버 연출(원격 cue/데미지)을 버퍼에 Append  ← 억제 OFF, 살아남음 ✅
       ↓
 [Reconcile]  using(Suppress()) { 재생 루프 }   재생이 만든 cue → no-op 🗑️
       ↓
 [world.Tick] 현재 틱 라이브 이벤트 Append       ← 억제 OFF ✅
       ↓
 [eventSink.Emit(buffer.Snapshot); buffer.Clear()]  서버 연출 + 라이브 이벤트만 나감 ✅
```

- 서버가 보낸 연출은 재생 **밖**(메시지 핸들러)에서 Append되므로 억제와 무관 → 정상 방출.
- 재생이 만든 연출만 콕 집어 버려진다.

## 테스트 (EditMode — 순수 C#, `GameFramework.Tests`)

`WorldEventBuffer` 는 GameFramework의 순수 C#이라 Unity 없이 단위 테스트한다.

- **Suppress 중 Append는 무시**: `using(buffer.Suppress()) { buffer.Append(e); }` 후 `Count == 0`.
- **Suppress 밖 Append는 정상**: 스코프 종료 후 `Append(e)` → `Count == 1`.
- **이미 든 이벤트는 보존**: `Append(a)` → `using(Suppress()) { Append(b); }` → Snapshot에 `a`만.
- **스코프 예외 안전**: `Suppress()` 스코프 안에서 예외가 나도 `Dispose` 로 억제 해제(이후 Append 정상). `try/using` 으로 검증.
- **중첩 안전**: `Suppress()` 두 번 중첩 후 하나 Dispose → 여전히 억제(depth 카운터). 둘 다 Dispose → 정상.
- **(회귀 의도 명시)** "재생 밖에서 Append된 이벤트는 재생 억제와 무관하게 살아남는다"를 위 세 번째 케이스가 대표한다 — 사용자가 우려한 "비슷한 타이밍의 서버 연출이 막히지 않는가"의 단위테스트 형태.

> `Reconciler` 자체는 Unity 의존(Physics/Inject)이라 EditMode 단위테스트가 어렵다. 억제의 **정확성은 버퍼 단위테스트로** 못박고, `Reconciler` 배선(정식 통로 사용 + 스코프 래핑)은 **플레이 검증**(스킬 쓰며 롤백 유발 → cue 1회만)으로 확인한다.

## 산업 표준 매핑

- **정책**: "연출(오디오·비주얼)은 게임플레이 상태에 영향 없는 한 방향 부수효과 — 라이브 프레임에서만 재생, 재시뮬(resim) 프레임에선 skip/suppress." — [SnapNet Rollback](https://www.snapnet.dev/blog/netcode-architectures-part-2-rollback/), [Ask a Game Dev](https://askagamedev.tumblr.com/post/669208695229562881/over-the-last-two-or-three-years-rollback-netcode). GGPO/격겜 롤백의 기본형.
- **Quantum 대응**: 방식 1 = **non-synced 이벤트 정책의 경량 구현**(라이브 방출 후 resim 재발화 안 함). Quantum은 이를 (내용+ID+틱) **해시 dedup**으로 구현(우리 방식 3=B). "확정될 때까지 지연"은 Quantum **synced** 이벤트(우리는 반응성 위해 미채택). — [Quantum Events](https://doc.photonengine.com/quantum/current/manual/quantum-ecs/game-events).
- **버퍼 스코프 명명**: `Suppress()` 는 mechanism을 서술(버퍼는 "왜"를 모름). 스코프 idiom은 `EditorGUI.DisabledScope`/ECS command-buffer playback 창과 동형. "재생/resim"이라는 이유는 호출자(`Reconciler`)에 남긴다.
- **문서 내 근거**: `world-core-connection-architecture.md` "Deferred" 절이 이미 *"재시뮬 틱의 버퍼는 **버리거나** 중복 제거"* 로 방식 1(버리기)·방식 3(dedup)을 함께 승인.

## Out of Scope

- **방식 3 (해시 dedup) · 틱 도장 · 예측 이벤트 생산자 · 취소 방향** — 전부 **B**(ROADMAP Next #2). 클라 예측 전투 생성이 선결.
- **통합 fan-out** — 클라는 이미 올바르게 분리됨(할 것 없음). 서버 egress 청소(backlog #2)는 별개 슬라이스.
- **`fix/reconciler-tick-guard` 의 `[임시]` 틱 가드 제거** — Stage④ 스냅샷 타임라인 재설계 때.
- **서버 측 변경** — 서버는 재생이 없어 `Suppress()` 를 안 부른다. 무변경.

## Open Questions (구현 plan에서 해소)

- `AbilityActivator.TryActivate(string casterEntityId, ...)` 에 넘길 caster id를 `worldEntity.Id` 로 얻는 정확한 타입·경로 확인(현 `Reconciler` 는 `worldEntity` 를 들고 있음).
- 재생 루프에서 `AbilityActivator` 로 전환 시, 기존에 미리 조회하던 `abilityDataProvider.TryGet` 제거로 인한 로그/가드 차이 없는지(정식 통로가 동일 조회 수행).
- `DriveActiveEntity`·`statusEffectSystem.Tick` 등 재생 중 다른 시스템이 이벤트를 Append하는 경우가 실제로 있는지(있어도 억제가 일괄 처리하므로 정합) — 구현 시 관찰만.

## 진행

- [x] 브레인스토밍 (A/B 범위, 방식 1 확정, 통합 fan-out 드롭, 웹 표준 검증)
- [x] 이 spec 작성
- [x] spec self-review (버퍼/입력버퍼 이름 모호성 정정)
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans` 로 구현 plan 작성
