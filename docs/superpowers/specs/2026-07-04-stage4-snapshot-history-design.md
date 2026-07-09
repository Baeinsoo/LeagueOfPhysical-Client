# Stage④ 슬라이스 ② — Snapshot 기록 골격 (SnapshotHistory)

**Date:** 2026-07-04
**Branch (제안):** `feature/stage4-snapshot-history`
**Related:** [netcode-redesign](../../netcode-redesign.md) §6.5 · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [Stage④ 첫 슬라이스(원격 kinematic)](2026-07-04-stage4-remote-kinematic-design.md)

## Goal

내 캐릭터(로컬 플레이어)의 시뮬 상태를 **매 틱 사진처럼 저장**하는 인프라(`SnapshotHistory`)를 만든다. 이후 슬라이스 ③(되감기+입력 재생 롤백)이 이 위에 얹힌다. 이번 슬라이스는 **기록까지만** — 되돌리기/롤백은 하지 않는다.

## 배경 — "저장소 두 개"

클라 예측/롤백에는 서로 다른 저장소 두 개가 필요하다:

| 저장소 | 저장 내용 | 상태 |
|---|---|---|
| **입력 로그** (`InputBuffer`) | "내가 매 틱 뭘 눌렀나" (방향/점프/어빌리티) | **이미 있음** |
| **스냅샷 로그** (이 슬라이스) | "그 결과 내가 매 틱 어디 있었나" = **위치·회전·속도** | 신규 |

되감기(③)는 둘을 함께 쓴다: 서버 스냅이 틱 T로 도착 → 스냅샷 로그에서 T를 꺼내 비교(틀렸나?) → 서버 값으로 되돌림 → **입력 로그**를 T+1..현재까지 재생. **스냅샷 = 상태(위치/회전/속도)이고 입력이 아니다.**

> PhysX 내부 해석 상태(충돌 solver 중간값)는 저장/복원 API가 없어 스냅샷에 담지 못한다. 위치/속도만 되돌리면 미세 오차(드리프트)가 남고, 이는 스무딩으로 덮는다 — A1 방식([첫 슬라이스 spec](2026-07-04-stage4-remote-kinematic-design.md))에서 이미 받아들인 전제.

## 범위 (이 슬라이스가 하는 것 / 안 하는 것)

**한다:** 매 틱 내 캐릭 상태를 `SnapshotHistory`에 기록. 그게 실제로 쌓이는지 관측.

**안 한다 (슬라이스 ③으로):**
- 되돌리기/롤백/replay 트리거 (서버 스냅 도착 시 rewind + 입력 재생).
- `SnapReconciler`(현행 delta-replay)·입력 처리·서버 스냅 핸들러 — **전부 무변경**.
- PhysX rigidbody 복원 동기(`Physics.SyncTransforms` 등) 세부.
- 서버 lag-compensation 자체 (컨테이너를 *재사용 가능하게* 둘 뿐, 서버 기록기/히트판정은 그 슬라이스에서).
- 스냅샷 캡처 앵커를 스무딩과 완전 분리하는 정밀 조정.

## 설계

### 부품

| 부품 | 위치 | 내용 |
|---|---|---|
| `EntitySnapshot` (readonly struct) | **GameFramework** `GameFramework.Netcode` (System.Numerics) | `{ long Tick; Vector3 Position; Quaternion Rotation; Vector3 Velocity }` |
| `SnapshotHistory` (순수 C# 클래스) | **GameFramework** `GameFramework.Netcode` | 틱-키 링버퍼(용량 128). `Record(EntitySnapshot)` / `bool TryGet(long tick, out EntitySnapshot)` / `EntitySnapshot? Latest` / `int Count` |
| 기록 배선 | **클라 `LOPRunner`** | 매 틱 끝에 내 캐릭 스냅샷을 `Record`. **신규 MonoBehaviour 없음** |
| DI 등록 | 클라 `GameLifetimeScope` | `SnapshotHistory` Singleton (`ReconciliationStats` 옆) |
| 관측 | 클라 `DebugHud` | 스냅샷 `Count` + 마지막 기록 틱 표시 |

### 왜 컨테이너를 GameFramework에 두고 공통화하나

- **서버 재사용**: 서버 lag-compensation은 "모든 엔티티의 과거 위치를 읽어 되감기"인데, 밑에 깔리는 저장소(틱별 엔티티 상태 링)는 클라 롤백과 **동일한 자료구조**다. 정책(누가·어떻게 쓰나)만 다르다 → **멍청한 컨테이너만 공통화**하고 정책은 사이드별로 둔다. 서버는 나중에 `엔티티별로 SnapshotHistory 하나씩` 들고 재사용한다.
- **순수 코드 → 단위 테스트 가능**: `SnapshotHistory`는 엔진 비의존(System.Numerics)이라 GameFramework EditMode 테스트가 가능하다 — 클라 Assembly-CSharp라 테스트 못 하던 제약을 이 인프라에 한해 우회.
- **진짜 시뮬 상태를 포착**: Unity 파사드(`entity.position`)가 아니라 `World.Transform`/`World.Velocity`(numerics 진실원본)에서 직접 뜬다 → 더 정확.

### 왜 `World` 코어가 아니라 `Netcode` 하위인가

netcode-redesign §6.5는 *시뮬 코어(`WorldBase`/`IWorld`)에 `Snapshot()`/`Restore()` 메서드를 박지 않는다*고 못박았다. 우리는 `WorldBase`를 손대지 않고 **독립 유틸 컨테이너**만 `GameFramework.Netcode`에 둔다. 시뮬 코어는 깨끗이 유지되고, 스냅샷은 넷코드 정책층이 소비한다. §6.5와 정합.

### 왜 호스트가 직접 기록하나 (전용 Recorder 컴포넌트 없음)

업계 표준(아래 "산업 표준 매핑")에서 "기록"은 별도 Recorder 부품이 아니라 **업데이트 루프/예측 시스템이 History에 직접** 넣는다(DOTS `GhostPredictionHistorySystem`, Unreal CMC, 우리 §6.5 스케치 `history.Record(...)`). 따라서 per-char MonoBehaviour를 새로 붙이지 않고 `LOPRunner`가 직접 기록한다 — "Recorder"라는 어색한 부품 자체가 사라진다.

### 기록 흐름 (`LOPRunner`)

- `EndUpdate`에서 **`End` 이벤트 디스패치 *전에*** 기록한다. 그러면 `SnapReconciler.Reconcile`의 스무딩 보정이 얹히기 *전*의 원본 예측 상태를 포착한다(호스트 직접 기록이라 이 순서를 확정 가능 = 부수 이득).
- 내 캐릭 식별: `IPlayerContext.entity`(로컬 `LOPEntity`). 그 `entityId`로 `EntityRegistry.Get(id)` → `World.Entity`의 `Transform`(Position/Rotation) + `Velocity`(Linear)를 읽어 `EntitySnapshot` 구성.
- 내 캐릭이 아직 없으면(스폰 전) 그 틱은 스킵.
- 정확한 캡처 앵커(스무딩 완전 분리)의 정밀 조정은 소비자가 생기는 ③에서. 지금은 소비자가 없어 기능 영향 0.

### 데이터 흐름 (다이어그램)

```
매 틱 (LOPRunner.UpdateRunner):
  ... world.Tick → SimulatePhysics(+SyncPhysics: rb→World) ...
  EndUpdate:
    ├─ [신규] 내 캐릭 World.Entity 읽기 → EntitySnapshot → SnapshotHistory.Record(tick)
    ├─ DispatchEvent<End>   → SnapReconciler.Reconcile (스무딩; 이 슬라이스 무변경)
    └─ DestroyMarkedEntities

DebugHud:  SnapshotHistory.Count / Latest.Tick 를 pull 해서 표시
```

## 검증

기존 클라 슬라이스와 달리 **핵심 인프라가 순수 GameFramework 코드라 단위 테스트를 넣는다.**

1. **EditMode 테스트** (GameFramework): `Record` 후 `TryGet`으로 조회 / 용량(128) 초과 시 가장 오래된 틱이 밀려나고 `TryGet` 실패 / `Latest`가 마지막 기록 반환 / `Count`가 용량에서 포화.
2. **DebugHud** 표시: 스냅샷 `Count` + 마지막 기록 틱.
3. **플레이 관찰**: `Count`가 128까지 차고 `Latest` 틱이 현재 틱을 따라감 + **이동/보정 무회귀**(소비자가 없으므로 게임 동작은 그대로여야 함).

## 영향 받는 파일 (개략 — 세부는 plan에서)

- **신규 (GameFramework)**: `Runtime/Scripts/Netcode/EntitySnapshot.cs`, `Runtime/Scripts/Netcode/SnapshotHistory.cs`, 대응 EditMode 테스트.
- **수정 (클라)**: `LOPRunner.cs`(EndUpdate에 기록 추가 + `IPlayerContext`/`SnapshotHistory` 주입), `GameLifetimeScope.cs`(`SnapshotHistory` 등록), `DebugHudViewModel.cs`/`DebugHudView`(스냅샷 카운트·틱 표시).
- **무변경**: `SnapReconciler`, `WorldBase`/`IWorld`, 입력·서버 스냅 핸들러, 서버 전체.

## 산업 표준 매핑

이 설계는 새 발명이 아니라 표준 조립이다(2026-07-04 실제 프레임워크 문서 대조):

| 프레임워크 | 데이터 단위 | 저장소 | 기록 주체 |
|---|---|---|---|
| **Unity DOTS Netcode** | 예측 상태 backup/snapshot | prediction history | `GhostPredictionHistorySystem`(예측 루프의 *시스템*이 매 풀틱 후 백업; **에러 감지 + 스무딩 + 복원**에 사용 — 우리와 동일 용도) |
| **Unreal CMC** | `FSavedMove_Character` | `SavedMoves` 버퍼 | 무브먼트 컴포넌트(CMC)가 직접 |
| **Fiedler / 일반** | snapshot | snapshot buffer | 업데이트 루프가 직접 |
| **LOP (본 설계)** | `EntitySnapshot` | `SnapshotHistory` | `LOPRunner`(호스트 루프)가 직접 |

- 데이터="snapshot", 저장소="history/buffer"는 전 프레임워크 공통 어휘.
- **핵심**: 어디에도 독립 "Recorder" 부품은 없다 — 루프/시스템이 History에 직접 기록. 본 설계가 이를 따른다.
- DOTS의 backup 용도(복원 + **에러 감지** + **스무딩**)가 우리 ③의 계획(비교로 롤백 판단 + 드리프트 스무딩)과 1:1.

출처:
- [Unity Netcode for Entities — GhostPredictionHistorySystem](https://docs.unity3d.com/Packages/com.unity.netcode@1.0/api/Unity.NetCode.GhostPredictionHistorySystem.html)
- [Netcode for Entities — Client-Side Prediction (DeepWiki)](https://deepwiki.com/needle-mirror/com.unity.netcode/6.1-client-side-prediction)
- [Unreal — FSavedMove_Character](https://docs.unrealengine.com/4.27/en-US/API/Runtime/Engine/GameFramework/FSavedMove_Character/)

## Out of Scope

- 되감기/롤백/입력 재생 (슬라이스 ③).
- 서버 lag-compensation 구현.
- PhysX rigidbody 복원 동기 세부.
- `SnapReconciler`의 delta-replay → Snapshot/Restore + input replay 전환(③에서 이 컨테이너를 소비하며 낡은 방식 은퇴).

## Open Questions (구현 plan에서 해소)

- `SnapshotHistory` 링버퍼 자료구조: 기존 `CircularBuffer`/`BoundedDictionary` 재사용 vs 전용 구현(틱-키 조회 + eviction 필요).
- GameFramework EditMode 테스트 어셈블리 정확한 위치/이름.
- `DebugHud`에 스냅샷 지표를 붙이는 정확한 바인딩 지점(기존 `ReconciliationStats` 경로 재사용).
- 버퍼 용량 128의 적정성(틱레이트 대비 커버 RTT) — 필요 시 조정.

## 진행

- [x] 브레인스토밍 합의 (범위 B, 컨테이너 GameFramework 공통, 호스트 직접 기록, 이름 확정)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
