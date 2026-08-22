# 슬라이스 B2 — Flappy Race 게임플레이

게임 모드 축(`2026-08-15-game-mode-axis-design.md`)의 슬라이스 B2. B1이 "축이 갈라지는가"를
증명했다면, B2는 그 축 위에서 **실제로 날고 부딪히고 서로 밀치는** 게임을 만든다.

> **끝났다는 기준**: 여러 명이 클라 예측 위에서 실제로 난다. 새가 파이프·바닥에 막히고,
> 새끼리 부딪히면 서로 밀려난다. FlapWang은 이전과 똑같이 동작한다.

---

## 1. 무엇을 만드나

| 넣는 것 | 빼는 것 (D 이후 또는 범위 밖) |
|---|---|
| 고정 전진 + 플랩 + 중력 | 결승선 통과·순위·종료 판정 (슬라이스 D) |
| 파이프·바닥 충돌 (막히고 미끄러짐) | 추격자, 대시, 부스트존 |
| 새끼리 몸싸움 (밀어내기 + 세로속도 교환) | 유령정지 페널티 |
| 클라 예측 / 롤백 재생 | 맵 씬에 남은 프로토타입 오브젝트(`Player`/`Pacer_*`) 정리 |

---

## 2. 슬라이스 순서

```
B2-a   콘텐츠 경로 뚫기      서버가 맵·새 프리팹을 받는다          ✅ 완료 (2026-08-17, §3 결과)
B2-c   넷코드 게임 비종속화   되감기가 게임 규칙을 모르게 한다        ✅ 완료 (2026-08-21, §4 결과)
B2-b   시뮬 코어             전진·플랩·중력·몸싸움                  ✅ 완료 (2026-08-22, §5 결과)
B2-d1  맵을 진짜 코스로       씬 정리 + 콜라이더 솔리드화 + 스폰 지점
B2-d2  클라가 자기 새를 난다   예측 켜기 + 몸 규격 통일 + 플랩 UI + 런타임 검증
```

**B2-d를 둘로 나눈 이유 (2026-08-22).** 원래 한 덩어리였는데 조사해 보니 7번째 저장소(아트
서브모듈)의 씬 수술과, 클라 예측·입력 UI가 한 계획에 들어가야 했다. 중간에 검증 지점이 없어
뭔가 틀렸을 때 "씬 탓인지 예측 탓인지"를 가릴 수 없다. **d1이 끝나면 "서버에서 새가 파이프에
실제로 막힌다"로 확인**되고, d2는 그 위에서 클라를 켠다.

**B2-c를 게임플레이보다 앞에 둔다.** 넷코드 구조를 건드리므로 회귀 위험이 있는데, Flappy
게임플레이가 아직 없는 상태에서 하면 **FlapWang만으로 회귀를 검증**할 수 있어 원인 분리가 깔끔하다.
게임플레이를 먼저 만들면 "예측이 이상한 게 새 이동 탓인지 넷코드 개편 탓인지" 구분이 안 된다.

---

## 3. B2-a — 서버 콘텐츠 경로

### 지금 상태 (2026-08-17 실측)

배선은 **이미 다 있다.** 서버는 부팅 시 원격 카탈로그를 받아 쓰도록 빌드돼 있다:

```
서버 이미지 /app/lop-server_Data/StreamingAssets/aa/settings.json
  AddressablesMainContentCatalogRemoteHash
    → https://lop-assets.s3.ap-northeast-2.amazonaws.com/dev/StandaloneLinux64/catalog_0.1.hash
  buildTarget = StandaloneLinux64
```

그 S3 경로에 콘텐츠도 실재한다(HTTP 200). 그래서 **FlapWang은 서버에서도 맵·캐릭터 비주얼이
정상 로드되고 있다** — 카탈로그에 `FlapWangMap`·`Knight`·`Archer`가 들어 있다.

문제는 **그 카탈로그가 2026-06-22자**라는 것이다. 8월에 승격한 `FlappyRaceMap`과 새 프리팹이
들어 있을 수가 없다. B1에서 서버가 낸 `ChainOperation failed`·`The Object you want to
instantiate is null`은 여기서 온다.

> ⚠️ **B1 검증 절의 진단 정정.** "서버 프로젝트에 아트가 없어서 실패한다"고 적었는데 틀렸다.
> 아트는 Addressables 원격 번들로 이미 서버에 도달하고 있고, 실패 원인은 **그 번들이 낡아
> Flappy 자산이 없기 때문**이다. 서버에 Art 서브모듈을 붙일 필요는 없다.

### 빠진 것

`content-deploy.yml`이 **Android만** 굽고 올린다(`BuildAndroidContentUpdate` →
`s3://lop-assets/dev/Android`). 게임서버가 쓰는 **StandaloneLinux64** 경로는 CI에 없어서
6월 로컬 산출물 이후 갱신이 끊겼다.

### 할 일

1. 클라 `Assets/Editor/BuildScript.cs`에 **Linux64 콘텐츠 빌드** 메서드 추가 (기존
   `BuildAndroidContentFull`/`BuildAndroidContentUpdate`와 짝을 맞춘 이름)
2. `content-deploy.yml`에 Linux 타깃 잡 추가 → `ServerData/StandaloneLinux64` →
   `s3://lop-assets/dev/StandaloneLinux64`
3. `FlappyRaceMap` 씬과 **새 프리팹**을 Addressable 엔트리로 등록 (`Scene`·`Character` 그룹)
4. 한 번 굽고 올린 뒤 서버 파드에서 확인

**끝났다는 기준**: 서버 파드 로그에서 맵 로드 성공 + 새 비주얼 인스턴스화 성공. 즉 B1에서 본
`ChainOperation failed`가 사라진다.

### 결과 (2026-08-17, 완료)

머지 `7e02a74` (`feature/flappy-b2a-server-content`). 커밋 4개:

| 커밋 | 내용 |
|---|---|
| `f06e432` | `BuildScript.BuildContentFull()` — 타깃을 코드에 박지 않고 **에디터의 활성 빌드 타깃**을 그대로 쓴다. 기존 `BuildAndroidContentFull`은 이 메서드를 부르는 껍데기로 남겼다 |
| `3d43dd3` | `content-deploy.yml`에 게임서버(Linux) full 콘텐츠 잡 추가 |
| `795b45a` | 실행 시 대상을 고르는 `target` 입력 추가 |
| `cd993e4` | 스탠드얼론 3플랫폼 매트릭스로 확장 |

위 "할 일" 3번(`FlappyRaceMap`·`Bird.prefab`을 Addressable 엔트리로 등록)은 **B1(`8d10d76`)에서 이미
돼 있었다** — 이번엔 작업이 없다. 이것이 "빠진 건 굽는 경로뿐"이라는 진단의 전제이기도 하다.

CI `content-deploy` run **31982225461**, `target=all`. S3 네 경로가 모두 갱신됐고 각 카탈로그에서
`FlappyRaceMap`·`Bird.prefab`을 확인했다:

| 경로 | 방식 | 갱신 |
|---|---|---|
| `dev/StandaloneLinux64` | full | 00:34 |
| `dev/StandaloneOSX` | full | 00:35 |
| `dev/StandaloneWindows64` | full | 00:37 |
| `dev/Android` | **증분(update)** | 00:32 |

**런타임 검증** — 로비에서 플래피 레이스를 골라 입장, 파드 `room-pod-0fc71969…`:

| 확인 | 결과 |
|---|---|
| `ChainOperation failed` | **0건** (B1에선 발생) |
| `The Object you want to instantiate is null` | **0건** |
| `[World] Registered flappy bird` | 있음 |
| 파드 이미지 | `9418e2c` — **B1 때와 같은 이미지** |

마지막 줄이 진단의 증명이다. 서버 코드를 한 줄도 바꾸지 않고 **콘텐츠만 갱신해서** 해결됐으므로,
원인이 "서버에 아트가 없어서"가 아니라 "S3 콘텐츠가 6월에 멈춰 있어서"였음이 확정됐다.

#### 새로 드러난 것 — 서버가 맵을 진짜로 읽기 시작하면서

같은 로그에 전에 없던 신호가 나왔다:

```
The referenced script (Unknown) on this Behaviour is missing!   ← 588건
NullReferenceException
  at GameFramework.Extensions.InjectSceneObjects → FindGameObjectsWithAttribute
  (LOP.GameLifetimeScope:OnSceneLoaded 에서 호출)
```

맵 씬에 **클라에만 있는 프로토타입 스크립트**(`Assets/Scripts/FlappyRaceSlice/`의 `FlappyPlayer`·
`FlappyPacer`·`FlappyObstacle`·`FlappyCourseGenerator` 등)가 붙어 있는데 서버 프로젝트엔 그 타입이
없다. 예전엔 맵 자체를 못 받아 안 보이던 것이 이제 드러난 것이다. 그리고 씬 주입 코드가 그 null
컴포넌트에서 NRE를 던져 **주입이 중간에 끊긴다.**

지금은 치명적이지 않다(주입 뒤 새는 스폰됐고 서버는 계속 대기). 다만 맵에 주입이 필요한 오브젝트
(스폰 마커 등)를 쓰기 시작하면 즉시 문제가 된다 → **B2-b/d의 선결 과제**(§6의 "맵 씬 프로토타입
오브젝트 정리"에 포함).

#### 개발 머신에 남는 부작용 — 콘텐츠 빌드가 활성 빌드 타깃을 바꾼다

`BuildContentFull`은 에디터의 활성 타깃을 쓰므로, `-buildTarget StandaloneLinux64`로 돌리면
**그 전환이 프로젝트에 남는다**(실측: `EditorUserBuildSettings.activeBuildTarget=StandaloneLinux64`).
그 뒤 에디터를 열면 유니티가 Linux 툴체인(`com.unity.sdk.linux-*`, `com.unity.toolchain.macos-arm64-linux`)과
실험판 `com.unity.pipeline`을 manifest에 자동 추가하고 URP 퀄리티 에셋의 셰이더 스트리핑 설정을 지운다.

**이 변경들은 커밋하지 않는다.** 되돌려도 에디터가 즉시 재생성하므로, 타깃이 Linux인 동안은
`git status`가 지저분한 채로 두고 커밋 시 경로를 명시한다. 작업 타깃(클라 앱은 Android)으로
되돌린 뒤 한 번에 정리한다.

#### `target` 기본값이 `gameserver`인 진짜 이유

커밋 메시지에는 "클라 앱 잡은 baseline이 있어야 성공하는 증분 빌드라 실수 실행이 위험"이라고 적었는데,
그건 부차적이다. **진짜 이유는 운영 중인 dev 클라 콘텐츠(`dev/Android`)를 의도치 않게 덮어쓰는 것**이다.
예전엔 수동 실행이 두 잡을 모두 돌려, 게임서버 자산만 갱신하려 해도 클라 콘텐츠가 함께 바뀌었다.

#### 안드로이드를 매트릭스에 넣지 않은 이유

스탠드얼론 셋은 플레이어를 매번 새로 빌드하므로 이미 깔린 설치본과의 호환을 지킬 필요가 없다 → full.
안드로이드는 **배포된 APK가 로컬 번들을 들고 있어서**, full로 다시 구우면 그 번들과 카탈로그가 어긋나
기존 설치본이 콘텐츠를 못 받는다. 그래서 `build-deploy`(증분) 잡으로 남겼다. (근거: `Vfx` 그룹이
Local — `pathPairIndex=0`. Character/Item/Scene은 Remote.)

#### 계획 문서 정정

플랜의 `.gitignore` `ServerData` 규칙 위치가 75줄이 아니라 **80줄**이다(앞서 다른 규칙을 추가해 밀렸다).
기능 영향 없음.

#### 남은 후속 (deferred)

- 워크플로 레벨 `concurrency`는 런(run)만 직렬화하고 잡 사이는 아니다. 지금은 `client` 라벨 러너가
  1대라 안전하지만, 러너가 늘면 두 잡이 같은 체크아웃·형제 UPM 레포에서 경합한다 → `needs:` 또는
  잡 스코프 concurrency로 굳힐 것.
- **`target`의 네 갈래 중 실제로 돈 것은 `all` 하나뿐이다.** 기본값 `gameserver`를 포함한 나머지
  셋은 아직 실행 이력이 없다(로직은 리뷰에서 추적해 확인). 다음에 기본값으로 돌리는 사람이 첫 실행이 된다.
- full 업로드는 `--delete` 없는 additive라 다시 구울 때마다 옛 번들이 S3에 쌓인다. 서버는 현재
  카탈로그만 보므로 동작 문제는 없고, `--delete`는 로딩 중인 파드를 깰 수 있다 → 지우려면 S3
  라이프사이클 규칙 쪽이 맞다.

---

## 4. B2-c — 넷코드를 게임 비종속으로

### 문제

되감기(`Reconciler`)가 **게임 규칙을 알고 있다.** 실측으로 두 군데다:

```
Reconciler
 ├─ 상태 저장/복원
 │    snapshotHistory               위치·속도 — 게임 무관, 정상
 │    predictedAbilityStateHistory  스킬·상태이상·스탯·마나가 넷코드에 박힘   ❌
 └─ 재생 중 입력 적용
      world.Tick(t, dt)             정상
      abilityActivator.TryActivate  넷코드가 스킬을 직접 발동              ❌
```

그래서 스킬이 없는 게임(Flappy)은 `Reconciler`를 만들 수조차 없다 — 생성자가 `AbilityActivator`와
`SequenceBuffer<PredictedAbilityState>`를 반드시 요구한다. B1이 게임별 DI 분리를 포기하고
`GameplayInstaller` 한 벌로 간 이유가 이것이다.

### 정석 모양 — 저장·복원은 시뮬레이션의 일

세 엔진이 같은 말을 한다: **롤백 기계는 상태의 내용을 모른다. 시뮬이 자기 상태를 저장·복원한다.**

| 표준 | 어떻게 |
|---|---|
| **GGPO** (격투게임 롤백의 사실상 표준) | `save_game_state` / `load_game_state` — 엔진은 불투명한 바이트 덩어리로만 받는다 |
| **Photon Quantum** | 프레임 전체를 통짜 스냅샷. 시뮬(`Frame`)이 자기 상태를 소유 |
| **Unreal** `FSavedMove_Character` | 베이스가 위치·속도를 담고, **게임이 서브클래스로 자기 데이터를 얹는다.** 네트워크 코드는 재생만 |

Unreal의 모양을 그대로 가져온다 — 베이스가 엔진이 아는 부분(위치·회전·속도)을 담고,
각 게임 월드가 자기 것을 덧붙인다. **호출자에겐 저장이 한 번, 복원이 한 번이다.**

```csharp
// GameFramework.World — GGPO save_game_state / load_game_state 대응
public interface IWorld
{
    EntityRegistry EntityRegistry { get; }
    WorldEventBuffer EventBuffer { get; }
    void Tick(long tick, float deltaTime);

    void SaveState(long tick);                    // Simulated 엔티티의 이번 틱 상태를 보관
    bool LoadState(long tick);                    // 그 틱으로 되돌린다. 기록 없으면 false
    long? OldestSavedTick { get; }                // "아직 안 살았던 틱"과 "밀려난 틱"을 가르는 데 쓴다
    bool TryGetSavedMotion(long tick, string entityId, out Netcode.EntitySnapshot motion);
}
```

`TryGetSavedMotion`이 인터페이스에 있는 이유: 되감기는 **예측이 서버와 얼마나 어긋났는지** 재야
하는데 그 값은 위치다. 위치는 게임과 무관하므로 이걸 노출해도 넷코드가 게임을 아는 게 아니다.
(Unreal도 `SavedMove->SavedLocation`을 보정 코드가 읽는다.)

`WorldBase`가 위치·속도 부분을 구현하고, 게임별 추가분은 훅으로 연다:

```csharp
protected virtual void SaveGameState(long tick) { }
protected virtual bool LoadGameState(long tick) => true;
```

- `LOPWorld` — 여기서 스킬·상태이상·스탯·마나를 담는다. 지금의 `PredictedAbilityState`가 이 안으로 흡수된다
- `FlappyWorld` — **아무것도 덧붙이지 않는다.** 위치·속도만으로 충분하다

### 정석 모양 — 입력은 데이터, 해석은 시뮬

| 표준 | 어떻게 |
|---|---|
| **Photon Quantum** `PollInput` | 입력은 프레임에 실리는 데이터. 시뮬이 읽어 해석 |
| **Unity NetCode for Entities** `ICommandData` | 같음 — 입력은 버퍼에 놓이는 데이터 |
| **Unreal** SavedMove의 compressed flags | 네트워크 코드는 플래그를 되돌려 놓기만, 해석은 이동 컴포넌트가 |

**롤백 기계가 게임플레이 함수를 직접 부르는 엔진은 없다.** 그러므로 재생 루프는 이렇게 된다:

```
지금:  inputBuffer.Current = cmd;  if (cmd.AbilityId != 0) abilityActivator.TryActivate(...);  world.Tick(t)
바꾼 뒤: inputBuffer.Current = cmd;  world.Tick(t)
```

발동은 `LOPWorld.Mutation`의 **첫 페이즈**로 들어간다(라이브에서 `PlayerInputManager`가 이동 계산보다
먼저 발동시키고 있으므로 순서가 같아야 한다 — 대시 발동 틱의 입력 게이트 타이밍이 이 순서에 걸려 있다).

이걸 하려면 `AbilityActivator`가 클·서 공용이어야 하는데, **지금 그 파일은 클라와 서버에 주석 한 줄만
다른 채로 복제돼 있다**(실측). LOP-Shared로 올리면 사본 하나가 사라진다. 서버 AI(`EnemyBrain`)의
슬롯 발동은 입력이 아니라 의도이므로 그대로 둔다.

단 그냥 옮길 수는 없다 — `AbilityActivator`가 쓰는 `AbilityDataProvider`는 **사이드별 마스터데이터
패키지**(클·서 상호 비참조)를 읽으므로 공용이 될 수 없다. 그래서 조회를 **델리게이트로 받는다**:

```csharp
public AbilityActivator(AbilitySystem abilitySystem,
                        Func<int, AbilityData?> resolveAbility,   // 사이드별 마스터데이터 어댑터
                        EntityRegistry entityRegistry,
                        WorldEventBuffer worldEventBuffer)
```

이는 새 발명이 아니라 **이 코드베이스가 이미 쓰는 방식**이다 — `StatusEffectApplyEffectHandler`가
`id => provider.Get(id)`를 받는 것과 같다. "시뮬은 구체 공유, 인터페이스는 사이드가 달라야 하는
I/O 어댑터에만"이라는 규약에도 맞는다: 마스터데이터 projection은 설계상 사이드가 다르다.

### 서버 보정의 게임 고유 부분 — 클라 훅

되감기에는 세 번째 게임 의존이 남는다: 서버 스냅의 **상태이상 목록**이다.

- 게이트 — 위치가 맞아도 서버 상태이상 목록이 다르면 되돌려야 한다(남이 건 슬로우는 내가 예측 못 함)
- 적용 — 서버 목록이 진실이므로 복원 후 덮어쓴다

`EntitySnap`은 클라 전용 타입이라 LOP-Shared의 월드가 볼 수 없다(서버는 이 타입을 쓰지 않으므로
토폴로지상 Shared로 올려서도 안 된다). 그래서 이 부분만 **클라 쪽 게임별 훅**으로 뺀다 —
Unreal의 `ServerMoveHandleClientError` / `OnClientCorrectionReceived`가 게임의 이동 컴포넌트에
있는 것과 같은 모양이다.

```csharp
// 클라 Assets/Scripts/Netcode/
public interface IServerCorrectionHandler
{
    bool Matches(long tick, EntitySnap snap);              // 위치 말고 게임 고유 값이 맞는가
    void ApplyAuthoritative(Entity entity, EntitySnap snap); // 서버가 진실인 부분을 덮어쓴다
}
```

- `LOPServerCorrectionHandler` — 상태이상 비교·적용. `LOPWorld`(구체)를 직접 참조해 앵커 틱의
  예측 상태이상을 읽는다. 같은 게임 안이므로 구체 참조가 맞다
- `FlappyRace` — 비교할 것도 덮어쓸 것도 없다. 아무 일도 안 하는 구현체를 등록

### 바꾼 뒤 되감기의 전체 모습

```
스냅 도착
 ├ world.TryGetSavedMotion(anchor, id) → 오차 기록 + 위치 게이트
 ├ correction.Matches(anchor, snap)    → 게임 고유 게이트
 └ 어긋났으면:
      world.LoadState(anchor)                  ← 위치·속도·스킬·상태이상 전부 한 번에
      (스냅의 권위 값 덮어쓰기: 위치·회전·속도·외력)
      correction.ApplyAuthoritative(entity, snap)
      for t in anchor+1 .. now-1:
          inputBuffer.Current = inputHistory[t]
          world.Tick(t, dt)
          world.SaveState(t)
```

**넷코드 코드에서 "스킬"·"상태이상"이라는 단어가 사라진다.**

### ⚠️ 이 슬라이스는 기존 결정을 뒤집는다

`netcode-redesign.md` §6.5와 `world-core-connection-architecture.md`가 이렇게 못박아 두었다:

> `Snapshot()`/`Restore(snap)` 메서드는 **코어에 두지 않는다** — 보관·복원 정책은 클라 외각의 책임

당시 근거는 "서버는 전체 롤백을 안 하니 YAGNI"였다. 그런데 **그 결정이 지금 문제의 원인**이다.
외각이 상태의 모양을 소유하니, 외각(=넷코드)이 "LOP엔 스킬이 있다"를 알아야만 했다.

뒤집는 근거는 위 세 엔진 전부다. YAGNI 우려도 실체가 없다 — 서버는 `SaveState`를 부르지 않으면
그만이고, 인터페이스에 메서드가 있다고 비용이 생기지 않는다. **두 문서를 이 슬라이스에서 함께 고친다.**

### 결과 (2026-08-21, 완료)

4개 레포에 머지·푸시 완료. 계획: `docs/superpowers/plans/2026-08-19-flappy-b2c-netcode-agnostic.md`.

| 레포 | main 머지 |
|---|---|
| GameFramework | `9169de7` — `IWorld.SaveState`/`LoadState` + `WorldBase`가 위치·속도 보관 |
| LOP-Shared | `31e6ba6` — `LOPWorld`가 게임 상태 보관, 입력에서 발동, `AbilityActivator` 공용화 |
| LOP-Client | `8c1644b` — `Reconciler` 슬림화, `IServerCorrectionHandler` 도입 |
| LOP-Server | `a6ad515` — 입력 처리에서 발동 호출 제거, 사본 삭제 |

**목표 달성의 기계적 증거** — `Reconciler.cs`에서 다음 grep이 아무것도 출력하지 않는다:

```bash
grep -nE "Ability|StatusEffect|LOPSavedState" Assets/Scripts/Netcode/Reconciler.cs
```

#### 검증

| 층위 | 결과 |
|---|---|
| 단위 테스트 | LOP-Shared EditMode **497/497**, GameFramework **275/275**. 새 테스트마다 *일부러 깨뜨려* 실패를 확인 |
| 회귀 가드 | `AbilityReplayDeterminismTests`(재생==라이브를 못 박은 테스트) 통과, 수정 없음 |
| 런타임 — Flappy | 서버 이미지 `a6ad515`. `FlappyWorld` + `NoServerCorrection` 배선, `SaveState` 매 틱, 클라 에러 0 |
| 런타임 — FlapWang(2인) | `LOPWorld` + `LOPServerCorrectionHandler` 배선, `SaveState` 매 틱, 게임 에러 0 |
| **체감 회귀** | **사용자가 직접 플레이해 "이전 경험과 거의 비슷하다" 확인** — 이 슬라이스에 존재하는 유일한 baseline |
| `[ReconSpike]` 진단 | `input[h=… v=… jump=… ability=…]` 정상 출력 — `InputCommand.ToString()`으로 되살린 필드가 런타임에서 확인됨 |

#### 검증의 한계 (정직하게)

- **개편 전 수치가 없다.** "reconciliation distance가 개편 전 수준"이라는 완료 기준은 *숫자로* 대조할 수 없었다. 진짜 baseline을 재려면 4개 레포를 모두 이전 커밋으로 되돌려야 한다(클라만 되돌리면 새 패키지와 안 맞아 컴파일 실패). 그래서 **사람의 체감**이 그 자리를 대신했다.
- 계측한 리컨 수치(정지 20초에 스파이크 61건, 평균 0.166 m)는 **회귀 판정에 쓸 수 없다** — 플랩왕 맵은 넉백이 돌고, 넉백은 설계상 클라가 예측하지 않으므로(`Reconciler`가 스냅에서 복원) 정지 중에도 오차가 난다. 수치로 회귀를 가리려면 *넉백 없는 조건*을 먼저 만들어야 한다.
- FlapWang 2인 검증은 서버 이미지 `cb6f1e4`(우리 커밋 + 매치 결과 트랙 2커밋)에서 돌았다. 우리 변경은 모두 포함돼 있으나 **단독 통제 실험은 아니었다.**

#### 새로 드러난 것 — 클라의 Flappy 새에 `Simulated`가 없다

`FlappyBirdCreator`(클라)가 `InputBuffer`는 붙이면서 `Simulated`는 붙이지 않는다. 그 결과:

1. `WorldBase.SaveState`가 `Simulated`만 저장 → 내 새가 한 번도 저장되지 않는다
2. `Reconciler.TryGetSavedMotion`이 항상 실패 → **오차 게이트 블록 전체를 건너뛴다**
3. `reconciliationStats.Record`가 아예 안 불려 **`Average=0`이 "예측이 완벽"이 아니라 "기록이 없음"** 을 뜻하게 된다(실측: `CorrectionCount=7361`인데 `Average=0`)

이는 이 슬라이스가 만든 차이다 — 옛 `LocalSnapshotSystem`은 `Simulated`와 무관하게 *내 엔티티*를 무조건 기록했다. FlapWang은 내 캐릭이 `Simulated`라 무영향이고 **Flappy만 해당**된다.

**B2-d에서 해소한다**(§6이 이미 새에 `Simulated` 추가를 계획). 함께 결정할 것: *시뮬하지 않는 엔티티는 되감기 대상에서 제외할 것인가* — 지금은 예측도 안 하면서 매 스냅 하드 보정이 돈다.

#### 환경에서 배운 것 (다음 검증자를 위해)

- **검증 중 매치 파드를 지우지 말 것.** 클라는 같은 방으로 재입장하므로 서버가 사라지면 붙지 못한다.
- **플랩왕은 `MinPlayers=2`**(플래피는 1). 혼자서는 매칭이 안 잡힌다. 2번째 클라는 MPPM으로 띄운다 — 유니티 6.3에서 MPPM은 에디터 내장이고(`Unity.Multiplayer.PlayMode.Editor.MultiplayerPlaymode.PlayerTwo.Activate(out error)`), 가상 플레이어는 CLI로 조작할 수 없어 자동 큐잉용 임시 스크립트가 필요했다.
- **`kubectl logs --timestamps`의 시각을 근거로 쓰지 말 것.** 유니티 서버 로그는 1900여 줄이 같은 마이크로초대로 찍힌다 — 수집 시각이지 발생 시각이 아니다.
- 로컬에서 클라가 서버에 못 붙으면 **도커의 UDP 포트 바인딩**을 먼저 의심한다. 실제로 도커가 7001~7009만 잡고 7000을 빠뜨린 적이 있었고, `docker restart lop-control-plane`으로 해소됐다. hostPort DNAT 룰은 *파드가 있을 때만* 존재하므로, 파드를 지운 뒤 iptables를 보고 배선 문제로 오진하지 말 것.

---

### 건드리는 곳과 위험

| 레포 | 무엇 |
|---|---|
| GameFramework | `IWorld`에 저장/복원 추가, `WorldBase`가 위치·속도 부분 구현 |
| LOP-Shared | `LOPWorld`가 게임 상태 저장/복원 + 입력에서 발동, `AbilityActivator` 이전, `FlappyWorld`는 그대로 |
| LOP-Client | `Reconciler` 슬림화, `IServerCorrectionHandler` 2종, `LocalSnapshotSystem`이 `world.SaveState` 호출 |
| LOP-Server | `ServerInputSystem`의 발동 호출 제거, 중복 `AbilityActivator` 삭제 |

가장 큰 위험은 **FlapWang 회귀**다. 지금 잘 도는 예측·롤백을 건드린다.
→ **완료 기준에 "FlapWang이 이전과 똑같이 동작한다"를 명시적으로 넣는다**(공중 점프 시나리오 +
`DebugHud`의 reconciliation distance가 개편 전 수준). 서버까지 켜서 확인한다.

---

## 5. B2-b — 시뮬 코어 (LOP-Shared)

### `TbFlappyConfig` (마스터데이터 신규)

`TbCombatConfig`와 같은 **단일 행 설정 테이블**. 단 group을 비워 **클·서 양쪽 패키지**에 나가게
한다(`TbCombatConfig`는 서버 전용 `s`지만, 이 값은 클라 예측이 같은 값을 써야 한다).

| 컬럼 | 뜻 |
|---|---|
| `forward_speed` | 고정 전진 속도 |
| `flap_impulse` | 플랩 시 세로 속도 |
| `gravity` | 중력 가속도 |
| `max_fall_speed` | 낙하 속도 상한 |
| `body_radius` / `body_height` | 새 캡슐 — **프리팹 콜라이더와 반드시 일치** |
| `restitution` | 몸싸움 반발계수 |

시작값은 프로토타입 씬의 검증된 값(전진 11, 플랩 23, 중력 70, 최대 낙하 30)에서 가져온다.

### `FlappyMoveSystem`

무상태 DI 시스템(`*System` 관례). 한 틱:

```
플랩 입력 소비 → vy += gravity·dt (max_fall_speed로 clamp) → 플랩이면 vy = flap_impulse
전진 속도는 상수로 고정 (vx = forward_speed)
```

### `FlappyBounce`

프로토타입의 순수 함수를 **그대로 이식**한다(반발계수 기반 세로속도 교환, 절반씩 밀어내기).
EditMode 테스트 8개도 함께 옮긴다. 지금은 클라 프로토타입 폴더에 있고, LOP-Shared에는 테스트
어셈블리가 이미 있다.

### `FlappyWorld.Mutation`

`LOPWorld`와 같은 페이즈 구조를 따른다:

```
① 입력·속도 갱신        FlappyMoveSystem
② 새끼리 몸싸움          분리 + FlappyBounce 세로속도 교환   ← 전원 이동 전 페이즈 배리어
③ 이동                   KinematicMover.Move (collide-and-slide) → Transform/Velocity 기록
```

물리 브릿지(`IMotionBridge`)는 `LOPWorld`와 같은 것을 쓴다 — 스크립트로 옮긴 위치를 물리에
반영(`SyncTransforms`), 시작 겹침 해소(`Depenetrate`), Rigidbody 팔로워에 반영(`PushMotion`)은
게임 규칙이 아니라 엔진 연동이라 게임별로 다를 이유가 없다.

**충돌은 기존 `KinematicMoveSystem`을 쓰지 않고 공유 커널 `KinematicMover.Move`를 직접 부른다.**
`KinematicMoveSystem`은 중력 −19.62·캡슐 0.35/1.5가 상수로 박혀 있어 Flappy와 맞지 않는다.
`KinematicMover.Move(in KinematicMoveInput, ICollisionQuery)`는 위치·속도·반지름·높이·dt·
레이어마스크를 전부 인자로 받는 순수 static 커널이라, 그대로 재사용하면 **FlapWang 회귀 위험이 0**이다.

**몸싸움 스케줄링은 프로토타입과 다르다.** 프로토타입은 부딪히기 전 모든 새의 세로 속도를
먼저 얼려 두고, 그 얼어붙은 값으로 이웃 전부를 동시에 풀었다. `FlappyBodyCollisionSystem.Resolve`는
쌍을 **순차로** 푼다 — 새가 3마리 이상이면 나중 쌍이 앞선 쌍이 이미 바꾼 값을 보고 계산한다.
결정론은 유지된다(쌍 안은 순서 무관, 쌍 사이는 id 정렬로 고정돼 클·서가 항상 같은 순서로 품).
다만 "프로토타입 규칙을 그대로 이식한다"는 위 §8의 결정은 *셈법*(반발계수 공식)에 한정되고
*스케줄링*(동시 vs 순차)까지 이식한 것은 아니다.

### 결과 (2026-08-22, 완료)

여섯 저장소에 머지됨:

| 저장소 | 머지 커밋 | 담긴 것 |
|---|---|---|
| infrastructure | `beae4d9` | `#FlappyConfig.xlsx`, `__tables__.xlsx` 행 추가, `gen.sh` .NET 우회 |
| MasterData-Client | `7a17484` | `TbFlappyConfig` 생성물 + 로더 등록 + 값 회귀 테스트 |
| MasterData-Server | `84f444c` | 같음 |
| LOP-Shared | `3125e2b` | `FlappyConfig`·`FlappyBounce`·`FlappyMoveSystem`·`FlappyBodyOverlap`·`FlappyBodyCollisionSystem`·`FlappyWorld` + 테스트 |
| LOP-Client | `718dc01` | `FlappyConfigProvider`, 스코프 조립, 프로토타입 참조 정리 |
| LOP-Server | `04fd98c` | `FlappyConfigProvider`, 스코프 조립 |

**테스트**: 클라 EditMode 518/518, 서버 EditMode 501/501. 새로 넣은 테스트는 하나하나
*일부러 깨뜨려 실패하는 것*을 확인한 뒤 되돌렸다 — 통과만으로 검증됐다고 하지 않는다.
FlapWang 회귀 테스트(`KinematicMoverTests`, `AbilityReplayDeterminismTests` 등)도 전부 통과.

#### 스펙 §5·§6의 전제를 하나 뒤집었다 — 몸싸움을 물리엔진에 묻지 않는다

스펙은 새끼리 겹침을 **프리팹 캡슐 콜라이더 + `ComputePenetration`** 으로 찾는 것을 전제했다.
구현은 **거리 산수로 직접 구하는 쪽**으로 갔다(`FlappyBodyOverlap`). 근거:

1. **되감기가 라이브와 어긋난다.** `WorldBase.SaveState`는 `Simulated` 엔티티만 기록하고,
   클라에서 그건 *내 새 하나*다. 물리엔진에 물으면 재생할 때 남의 새의 **지금** GameObject 위치를
   보게 된다 — PhysX가 비트결정론이냐 아니냐와 **무관하게** 재생 ≠ 라이브가 된다. (이 근거는
   구현 후 리뷰에서 나왔고, 원래 들었던 "PhysX는 재현을 보장하지 않는다"보다 강하다.)
2. **콜라이더 없이 시험할 수 있다** — 겹침 테스트가 GameObject도 PhysX 씬도 없이 돈다.
3. **스펙 §6의 경고가 사라진다.** "프리팹 콜라이더 크기가 `body_radius`와 어긋나면 예측이
   깨진다"는 위험이 없어졌다 — 몸 규격이 `TbFlappyConfig` 한 곳에만 존재한다. 맵 sweep도
   `KinematicMover.Move`에 config 값을 넘기므로 프리팹 콜라이더를 보지 않는다.

**맵(파이프·지형)은 그대로 물리엔진 sweep을 쓴다** — 임의 메시라 산수로 못 풀고, 정적
지오메트리라 같은 위치에서 같은 답이 나온다.

업계 표준 매핑: 되감기 넷코드는 게임플레이 충돌을 호스트 물리엔진에 맡기지 않는다 —
Photon Quantum이 자체 결정론 물리를 따로 두는 이유, 격투게임의 pushbox, 오버워치의 캡슐 산수가
모두 같은 이유다. **단, 위험이 사라진 게 아니라 자리를 옮겼다** — 콜라이더 *크기*에서
콜라이더 *레이어*로. §6의 선결 과제 참고.

#### 검증의 한계 (정직하게)

- **런타임 검증을 하지 않았다.** 서버 새는 이미 `Simulated`가 있어 머지 즉시 돌지만, §6의
  선결 과제(트리거 콜라이더) 때문에 **새가 파이프를 그냥 통과한다.** 지금 켜서 "제대로 나는지"를
  볼 수 있는 상태가 아니다. 끝-끝 확인은 B2-d 몫이다.
- **클라는 아직 자기 새를 시뮬하지 않는다**(`Simulated` 없음 — B2-d). 그래서 이 슬라이스의
  코드는 지금 **서버에서만** 돈다. 예측·되감기 경로는 아직 한 번도 실행되지 않았다.
- **플랩을 누를 수단이 없다.** `PlayerInputManager`에 입력을 넣어 주는 건 FlapWang 스코프가
  등록하는 화면 게임패드 UI뿐이고, FlappyRace 스코프엔 게임 UI가 없다. 이것도 B2-d.
- **DI 조립은 테스트되지 않는다.** 컨테이너를 실제로 빌드해야 드러나고 이 저장소에 그런 관례가
  없다(FlapWang의 `LOPWorld` 조립도 마찬가지). 다만 `FlappyWorld` 생성자 8인자가 전부 서로 다른
  타입이라 **순서를 틀리면 런타임이 아니라 컴파일에서** 깨지고, 남은 위험은 "등록이 빠졌나"뿐이라
  씬을 켜는 순간 크게 실패한다.
- **남긴 테스트 공백 하나**: 맵 충돌 테스트가 sweep 캡슐의 *반지름*과 레이어마스크는 단언하는데
  *높이*는 단언하지 않는다(스텁이 캡슐 양 끝점을 받고 버린다). 코드는 `_config.BodyHeight`를
  올바르게 넘기는 것이 확인됐으므로 그물 쪽 구멍이다. B2-d가 이 영역을 다시 만질 때 함께 메운다.

#### 실행하며 배운 것 (다음 사람을 위해)

- **`gen.sh`가 이 맥에서 그냥 죽는다** — .NET 8 런타임이 없다. `DOTNET_ROLL_FORWARD=LatestMajor`를
  `gen.sh`에 넣어 해결했다. 이번에 무해함이 실증됐다(기존 테이블 생성물이 전부 바이트 동일하게 재생성).
- **새 Luban 테이블은 수기 로더 목록에도 넣어야 한다** — 양쪽 패키지 `LOPMasterData.cs`의
  `TableFiles`. 빠뜨리면 게임이 시작 단계에서 `KeyNotFoundException`으로 죽는다. 다행히
  `TableFileManifestTests`가 그물이라 빨강→초록으로 확인할 수 있다.
- **컴파일은 엑셀 열 순서를 못 잡는다.** 중력 칸에 플랩 값이 들어가 있어도 통과한다. 그래서
  배포된 `.bytes`를 직접 열어 일곱 숫자를 확인하는 테스트를 양쪽 패키지에 넣었다 — 1회성 수동
  확인이 아니라 회귀 그물로.
- **유니티 batchmode 경로에 와일드카드를 쓰지 말 것** — 이 맥에 유니티가 8개 설치돼 있어
  엉뚱한 버전을 집는다. 프로젝트의 `ProjectVersion.txt` 값을 쓴다.

---

## 6. B2-d1 — 맵을 진짜 코스로

> **2026-08-22 조사로 §6의 전제 두 개가 틀렸다는 게 드러났다.** 아래 "정정" 절에 남긴다.
> 원래 한 덩어리였던 B2-d는 d1/d2로 나뉘었다(§2).

### 목표 상태 — FlapWang 맵과 같은 모양

맵 씬은 **기하와 콜라이더만** 담는 것이 이 프로젝트의 규약이다. 실측:

| 씬 | MonoBehaviour | 카메라 | 라이트 |
|---|---|---|---|
| `FlapWangMap.unity` (정상) | 0 | 0 | 0 |
| `FlappyRaceMap.unity` (지금) | **147** | 1 | 1 |

Flappy 맵만 프로토타입 씬에서 그대로 승격돼 정리가 안 됐다. 게임 씬(`FlappyRace.unity`)이 이미
카메라·라이트·오디오리스너를 갖고 있으므로 맵 쪽 것은 중복이자 충돌이다.

### 할 일

- **프로토타입 스크립트를 씬에서 걷어낸다.** 클라에만 있는 스크립트라 서버에서 missing script가
  되고, 그 null 컴포넌트가 씬 주입을 NRE로 끊는다(B2-a 실측, §3). 실측 내역:

  | 스크립트 | 개수 | 붙은 곳 | 처리 |
  |---|---|---|---|
  | `FlappyObstacle` | 118 | `Cube`×72, `ArmN/S/E/W_marker` 등 | **스크립트만 제거**(빈 마커 클래스, 기하는 남긴다) |
  | `FlappyWindmill` | 8 | `Windmill`, `FillWindmill` | 스크립트만 제거 → 정적 장애물이 된다 |
  | `FlappyBird` | 4 | `Player`, `Pacer_*` | 오브젝트째 삭제 |
  | `FlappyPacer` | 3 | `Pacer_Cyan/Red/Yellow` | 오브젝트째 삭제 |
  | `FlappyIris` | 2 | `Iris`, `FillIris` | 스크립트만 제거 → 정적 |
  | `FlappyPlayer`·`FlappyAutoPilot`·`FlappyPlayRecorder`·`FlappyDashFx` | 4 | 전부 `Player` | 오브젝트째 삭제 |
  | `FlappyCameraFollow` | 1 | `Main Camera` | 오브젝트째 삭제(게임 씬에 카메라가 있다) |
  | `FlappyHUD`·`FlappyRaceManager`·`FlappySimJudge`·`FlappyRaceStart`·`FlappyChaser` | 5 | 각자 동명 오브젝트 | 오브젝트째 삭제 |
  | `FlappyCourseGenerator` | 1 | `---Course---` | **스크립트만 제거**(코스 루트라 오브젝트는 남긴다) |
  | `FlappyBoostZone` | 1 | `BoostHole` | 스크립트만 제거 |

  `FlappyCourseGenerator`는 `ContextMenu`로 도는 **에디터 전용 도구**이고 코스 기하는 이미 씬에
  구워져 있다 — 지워도 런타임에 잃는 것이 없다. 인스펙터에 넣어 둔 생성 설정(구간 리스트·고도·
  틈 크기)은 **git 히스토리에 남아** 있으므로 나중에 코스를 다시 굽고 싶으면 옛 커밋의 씬에서
  값을 꺼내 오면 된다.

- **맵 콜라이더를 트리거에서 솔리드로 바꾼다.** `BoxCollider` 119개가 전부 `m_IsTrigger: 1`이다
  (프로토타입이 `OnTrigger`로 유령정지를 만들던 시절의 흔적). 그런데
  `UnityCollisionQuery.CapsuleCast`가 `QueryTriggerInteraction.Ignore`로 트리거를 걸러 버리므로,
  지금 씬 그대로면 §5 phase ③의 sweep이 **영영 아무것도 맞지 않는다.**

- **맵 씬의 카메라·라이트를 지운다.** 게임 씬이 이미 갖고 있다(FlapWang 맵도 안 갖고 있다).

- **스폰 지점을 서버가 읽게 한다.** `PlayerSpawn_1~4`가 있으나 **비활성**(`m_IsActive: 0`)이고
  서버 룰은 무시한 채 x=0에 세로 2칸 간격으로 세운다. 이 프로젝트엔 스폰 지점 규약이 아직 없다
  (FlapWang은 스폰 마커를 안 쓴다).
  - **마커 컴포넌트를 LOP-Shared에 둔다.** 이름으로 찾는 방식(`GameObject.Find`)은 비활성
    오브젝트를 못 찾고 오타에 약하다. 마커를 **양쪽 프로젝트가 참조하는 패키지**에 두면 GUID가
    같아 missing script가 되지 않는다 — 지금 고치고 있는 그 문제를 다시 만들지 않는 유일한 방법이다.
    (문제는 "맵 씬에 MonoBehaviour가 있는 것"이 아니라 "**한쪽에만 있는** 스크립트가 있는 것"이다.)
  - 산업 표준 매핑: Unreal `APlayerStart`(스폰 지점을 액터 클래스로 두고 게임모드가 찾아 쓴다).

### 정정 — §6의 원래 전제 두 개가 틀렸다 (2026-08-22 실측)

1. ~~"새 프리팹에 캡슐 콜라이더를 붙인다"~~ → **붙일 곳이 아니다.** 콜라이더는 아트 프리팹이
   아니라 코드가 만든다 — `PhysicsFollower.Initialize`가 엔티티마다 액터 루트에 `Rigidbody` +
   `CapsuleCollider`를 붙인다. `Bird.prefab`에는 콜라이더가 **0개**이고, 그것은 액터의 자식으로
   붙는 겉모습일 뿐이다(`LOPEntityView`가 `Instantiate(prefab, transform)`).

2. ~~"새를 `Default` 레이어에서 빼내라"~~ → **이미 나와 있다.** `PhysicsFollower`가
   `gameObject.layer = LayerMask.NameToLayer("Character")`를 무조건 건다. 즉 §5가 넘기는 sweep
   마스크 `Default`는 지금 이미 맞고, 새끼리 sweep으로 막는 일도 없다. B2-b 최종 리뷰가 이걸
   위험으로 지적했는데, 그 판단은 *겉모습 프리팹*의 레이어를 본 것이었고 그 프리팹은 콜라이더가
   없어 물리에 존재하지 않는다. **양쪽 `FlappyRaceLifetimeScope`에 달린 "B2-d 숙제" 주석도 이
   사실에 맞게 고쳐야 한다.**

3. **대신 진짜 어긋난 곳이 있다 — 몸 규격이 두 곳에 다른 값으로 있다.** (→ B2-d2)

   | | 반지름 | 높이 |
   |---|---|---|
   | `FlappyConfig`(시뮬: sweep·몸싸움) | 0.45 | 0.9 |
   | `PhysicsFollower`(물리 팔로워가 만드는 몸) | **0.35** | **1.5** |

   `PhysicsFollower`는 FlapWang 캐릭터 치수를 하드코딩하고 있다. 스펙이 경고했던 "두 곳이
   어긋나면 깨진다"가 프리팹이 아니라 **코드**에 실재한다. 지금은 `Depenetrate`(지형에 박힌 것
   빼내기)와 *남의* sweep이 이 몸을 볼 때만 영향이 있어 급하지 않지만, d2에서 통일한다.

---

## 6-2. B2-d2 — 클라가 자기 새를 난다

- 클·서 `FlappyBirdCreator`에 **`Simulated`** 추가 — 서버는 모든 새, 클라는 **내 새만**.
  남의 새는 예측하지 않고 스냅샷 보간(`RemoteEntityInterpolator`)에 맡긴다. 나머지 컴포넌트
  조합은 B1 그대로(`Transform`/`Velocity`/`EntityKind`/`Appearance`/`MotionContributions`/`InputBuffer`)
  - **선결 확인**: 지금 클라 새에 `Simulated`가 없어서 `WorldBase.SaveState`가 새를 기록하지
    않는다 → 되감기 통계의 `Average=0`은 "예측이 완벽"이 아니라 **"기록이 없음"** 을 뜻한다
    (B2-c 결과, §4). 켜고 나서야 그 숫자가 의미를 갖는다. 함께 결정할 것: *시뮬하지 않는
    엔티티는 되감기 대상에서 제외할 것인가* — 지금은 예측도 안 하면서 매 스냅 하드 보정이 돈다.
- **몸 규격을 한 곳으로 통일한다** — 위 §6 "정정 3". `PhysicsFollower`가 게임별 캡슐 치수를
  받게 하고, Flappy는 `TbFlappyConfig`의 `body_radius`/`body_height`를 넘긴다.
- **플랩을 누를 수단을 만든다.** `PlayerInputManager`에 입력을 넣어 주는 것은 FlapWang 스코프가
  등록하는 화면 게임패드 UI뿐이고, FlappyRace 스코프엔 게임 UI 등록이 하나도 없다 — 즉 지금은
  **사람이 플랩을 시킬 방법이 자체가 없다.** 스펙 §6의 "입력은 새로 만들 게 없다"는 *와이어와
  서버 버퍼*에 대한 말이었고, 누를 것이 없다는 문제는 빠져 있었다.
  - **결정(2026-08-22): Flappy 전용 플랩 UI를 새로 만든다.** 버튼 하나짜리 작은 화면.
    FlapWang 게임패드를 재사용하면 배선은 짧지만 이 게임이 안 쓰는 이동 스틱이 화면에 남는다.
  - 플랩 = 기존 `InputCommand.Jump`. 와이어 포맷·서버 입력 버퍼·유실 대비 재전송은 그대로 재사용.

**런타임 검증**(§7): 로비에서 FlappyRace 선택 → 날고 파이프에 막히는지, MPPM 2인으로 몸싸움.
## 7. 테스트와 검증

| 대상 | 방법 |
|---|---|
| `FlappyBounce` 속도 교환 | EditMode (이식한 8개) |
| `FlappyMoveSystem` 속도 갱신 | EditMode — 순수 계산이라 단위 테스트 가능 |
| **FlapWang 회귀** (B2-c 직후) | 2에디터 + 공중 점프, reconciliation distance가 개편 전 수준 |
| 서버 콘텐츠 (B2-a 직후) | 서버 파드 로그에 맵 로드·비주얼 성공 |
| 나는 감각·충돌 | 로비에서 FlappyRace 선택 → 날고 파이프에 막히는지 |
| 몸싸움 | MPPM 가상 플레이어 2인 |

---

## 8. 확정된 결정

| 항목 | 결정 | 이유 |
|---|---|---|
| 파이프·바닥 충돌 | **막히고 미끄러진다**(collide-and-slide) | 별도 상태가 없어 예측·롤백과 가장 잘 맞는다. 전진이 막혀 뒤처지는 것 자체가 페널티 |
| 튜닝값 | **마스터데이터 신규 테이블** | 기획이 엑셀로 조정. 클·서가 같은 생성물을 본다 |
| 몸싸움 | **프로토타입 규칙 이식** | 순수 함수 + 테스트가 이미 있고 손맛이 검증됨 |
| 예측 범위 | 서버=모든 새 / 클라=내 새만 | 기존 `Simulated` 마커 규약 그대로 |
| 서버 콘텐츠 | **Addressables 원격 번들**(이미 동작) | 서버에 아트 소스를 붙일 필요가 없다 |
| 되감기 상태 | **월드가 저장·복원**(`SaveState`/`LoadState`) | GGPO·Quantum·Unreal 모두 시뮬이 자기 상태를 소유한다. 사진첩을 둘로 나누는 건 우리만의 변형이었다 |
| 재생 중 발동 | **입력을 놓고 `world.Tick` 한 번** | 롤백 기계가 게임플레이 함수를 부르는 엔진은 없다(Quantum `PollInput`, NetCode `ICommandData`) |
| 몸싸움 겹침 계산 | **해석적(캡슐 산수)** — 물리엔진 미사용 | 되감기는 `Simulated` 엔티티만 복원하므로, 물리에 물으면 재생 때 남의 새의 *현재* 위치를 본다 → 재생≠라이브. 맵(임의 메시)만 sweep (§5 결과) |
| 스폰 지점 | **마커 컴포넌트를 LOP-Shared에** | 양쪽이 참조하는 패키지라 GUID가 같아 missing script가 안 된다. Unreal `APlayerStart` 대응 (§6) |
| 플랩 입력 | **Flappy 전용 플랩 UI 신규** | FlapWang 게임패드 재사용은 배선이 짧지만 안 쓰는 이동 스틱이 남는다 (§6-2) |

---

## 9. Open Decisions

- [x] ~~`world.Capture`/`RestoreTo`의 정확한 시그니처~~ → **해소(§4)**. `IWorld`에 직접
  `SaveState`/`LoadState`로 붙인다(GGPO `save_game_state`/`load_game_state` 대응). 별도 포트로 빼지
  않는 이유: 예측하는 클라만 쓰는 능력이지만, 안 부르면 그만이라 서버에 비용이 없다.
  이는 `netcode-redesign.md` §6.5의 "시뮬 코어에 두지 않는다"를 **뒤집는 것**이다 — 그 결정이
  넷코드가 게임을 알게 만든 원인이었다. 두 아키텍처 문서를 이 슬라이스에서 함께 고친다.
- [x] ~~**맵 콜라이더 레이어** — 새 sweep이 쓸 레이어마스크. 프로토타입은 `~0`(전부)였다.~~ →
  **해소.** `LayerMask.GetMask("Default")`로 정했다. 근거: `FlappyRaceMap.unity`의 `m_Layer`가
  228개 전부 0(Default) — 지금 맵엔 새를 걸러낼 다른 레이어가 없어 `~0`과 `Default` 사이에
  차이가 없다. 단 이 결정은 **새가 계속 Default 밖에 있어야** 유효하다 — B2-d가 새를 전용
  레이어로 옮기지 않으면 이 마스크가 새 자신도 맞혀 버린다(§6 선결 과제 참고).
- [ ] **`TbFlappyConfig` 이름** — 게임이 늘면 `TbGameConfig`+게임모드 키가 나을 수 있다. 지금은
  게임이 둘뿐이라 단순한 쪽으로 간다.
- [ ] **콘텐츠 빌드 주기** — Linux 콘텐츠를 매 배포마다 굽을지, 자산이 바뀔 때만 수동으로 굽을지.

---

## 10. 관련 문서

- `docs/superpowers/specs/2026-08-15-game-mode-axis-design.md` — 게임 모드 축 전체, B1/C 결과
- `docs/netcode-redesign.md` — 예측·보정 구조, §4d 입력 소스 표준
- `docs/world-core-connection-architecture.md` — 월드 코어, `Simulated`, 키네마틱 이동 substrate
- `docs/superpowers/specs/2026-07-09-shared-kinematic-character-controller-design.md` — `KinematicMover` 커널
