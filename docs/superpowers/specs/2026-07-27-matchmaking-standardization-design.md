# 매치메이킹 표준화 — 개념 모델 + 풀 기반 매칭 전환

LOP는 **여러 미니게임을 경쟁하는 게임**이다. 코어(캐릭터·어빌리티·물리)는 공유하고 **규칙만 바뀐다.**
그런데 현재 코드의 어휘는 이 구조를 반대로 표현하고 있고, 매칭 구조에는 실제 버그와 구조적
취약점이 있다. 이 문서는 **어휘를 업계 표준으로 바로잡고, 매칭을 표준 배치로 전환하는** 설계다.

> 범위: 어휘·개념 모델(A) + 매칭 구조(B). **매치 결과(C)·레이팅(D)·로비 UI(E)는 별도 spec** —
> §11 참고.

## 1. 배경 — 무엇이 문제인가

### 1-1. 이름이 실제 의미와 반대로 붙어 있다

| 지금 이름 | 실제로 가리키는 것 | 업계 표준 이름 |
|---|---|---|
| `GameMode { Normal, Ranked }` | 경쟁성 — 매칭 풀을 가르는 축 | **Queue** (Riot `queueType`) / Playlist (Halo·Destiny) |
| `subGameId` (`"Dodgeball"`) | **진짜 game mode** — 무슨 규칙으로 노는가 | **GameMode** (Riot `gameMode`, 언리얼 `AGameMode`) |
| `matchType` vs `gameMode` | **같은 enum인데 필드 이름이 둘** (매치·티켓·대기방 vs 전적) | 하나로 |
| `mapId` | 진실원본 없음. 아무도 검증 안 하고 게임 서버는 **무시**(하드코딩) | Map |

즉 `GameMode`라는 이름이 **가장 game mode가 아닌 것**에 붙어 있고, 진짜 game mode는
`subGame`이라는 비표준 조어를 쓴다. [Wikipedia](https://en.wikipedia.org/wiki/Minigame)에 따르면
`subgame`은 minigame의 동의어로 존재하긴 하나 *"더 큰 게임 안에 들어 있는 짧은 곁가지"* 라는
뜻을 품는다 — LOP의 게임들은 곁가지가 아니라 제품 자체라 맞지 않는다.

### 1-2. 매칭에 구조적 결함이 있다

| | 내용 | 위치 |
|---|---|---|
| 1 | **후보 대기방을 고를 때 큐·게임·맵을 안 본다.** 레이팅과 정원만 확인 → 랭크 닷지볼이 일반 플랩왕 방에 들어감 | `waitingRoom.service.ts:158-190` |
| 2 | **먼저 온 사람이 방 조건을 정한다.** `targetRating`이 첫 입장자 레이팅으로 고정 | `waitingRoom.service.ts:194` |
| 3 | **재시작하면 기존 방이 영원히 멈춘다.** `WaitingRoomUpdater`는 방 생성 시에만 만들어져 **메모리 Set**에 등록됨. 서버가 죽으면 DB엔 방이 남지만 돌봐 줄 주체가 없음. 다중 인스턴스도 불가 | `waitingRoom.service.ts:88`, `updater.ts:29` |
| 4 | `maxWaitingTime`에 **`5` 하드코딩** + `// ?` 주석. 단위는 초 | `waitingRoom.service.ts:199` |
| 5 | `WaitingRoomStatus` enum에 **값이 `None` 하나뿐** — 죽은 enum | `schema.prisma` |
| 6 | `findAll()` 후 JS 루프 + 방마다 티켓 재조회(N+1) | `waitingRoom.service.ts:167-172` |
| 7 | **파티 개념 없음** — 티켓의 `creator`가 단일 유저 | `matchmakingTicket.dto.ts` |

### 1-3. 마스터데이터의 진실원본이 갈라져 있다

게임 목록·정원은 매치메이킹 서버의 **자체 XML**(`master_data/sub_game_data/*.xml`)에만 있다.
클라는 로비에서 무엇을 고를 수 있는지 그려야 하는데 알 방법이 없어, `"FlapWang"`이
클라에 하드코딩돼 있다(`MatchmakingViewModel.Play()`).

## 2. 확정된 개념 모델

```
Queue          친선전 / 랭킹전 — 무엇을 고를 수 있는지를 스스로 선언
  └ Match      한 판 = 최종 결과 1개
       └ Round  1..N  (목록으로 짓되 지금은 항상 원소 1개)
            ├ GameMode   닷지볼, 타겟슈팅 — "이 판의 규칙", 정원(min/max) 소유
            └ Map        게임에 종속
```

| 개념 | 정의 |
|---|---|
| `Queue` | 매칭 풀을 가르는 단위. 실력 폭·허용 게임·선택 정책을 데이터로 선언 |
| `GameMode` | 무슨 규칙으로 노는가. 코어는 공유하고 규칙만 다름. **정원(min/max)을 소유** |
| `Map` | 어디서. `GameMode`에 종속 |
| `Match` | 한 판. 결과 1개 |
| `Round` | 판 안의 한 게임. **목록으로 짓고 원소 1개로 시작** (§2 하단) |
| `MatchmakingTicket` | 매칭 요청 1건. **이미 표준 어휘라 유지** |
| `WaitingRoom` | — **삭제** (§7) |

### `Round` — 원소 1개짜리 목록으로 짓는다 (확정)

현재는 한 매치 = 한 게임이라 라운드가 항상 1개다. 다만 *"여러 독립 게임을 연속하되 최종 결과는
하나"*(철인 3종식)가 계획에 있으므로, **`Match`는 게임·맵을 직접 들지 않고 라운드 목록을 통해서만
가진다.**

```
Match {
    id, queueId, playerList, createdAt
    rounds: [ { index: 0, gameModeId, mapId } ]     // 지금은 항상 원소 1개
}
```

**컬럼으로 직접 박지 않는 이유:** 이건 DB 스키마라 나중에 바꾸는 비용이 코드와 다르다. 컬럼으로
두면 확장 시 마이그레이션 + 3개 DB + 읽는 쪽(클라·게임서버·매칭서버)을 전부 손봐야 한다. 지금
목록으로 두는 비용은 테이블 하나와 조인 한 번이고, 확장할 때 하는 일은 **배열에 원소를 더 넣는
것뿐**이다 — 스키마도 읽는 쪽도 그대로다.

C(매치 결과)에서도 이 자리가 쓰인다: 라운드별 점수는 각 원소에, 최종 결과 하나는 `Match`에 붙는다.

**게임 서버는 `rounds[0]`을 읽어** 맵을 로드한다(현재 `LOPRunner.MapId` 하드코딩을 대체).

## 3. 이름 변경 대응표

| 지금 | 바뀔 이름 | 비고 |
|---|---|---|
| `GameMode { Normal, Ranked }` (enum) | **`Queue`** (마스터데이터 행) | enum이 데이터가 됨 |
| 필드 `matchType` | `queueId` | 매치·티켓·요청 전부 |
| 필드 `gameMode` (`UserStats`) | `queueId` | 같은 개념 두 이름이던 것 통일 |
| `subGameId` | **`gameModeId`** | |
| `SubGameData` (XML) | **`TbGameMode`** (Luban) | |
| — | **`TbQueue`**, **`TbMap`** 신설 | |
| `UserLocation.WaitingRoom` | `Matchmaking` | 방이 없어지니 "매칭 중"이 정확 |
| FSM `InWaitingRoom` | `Matchmaking` | 클라 |
| `MatchmakingTicket.creator: string` | `userIds: string[]` | 파티 대비. 지금은 항상 1명 |

새 마스터데이터 테이블의 기본키는 **정수 `id`** + `code`/`name` 컬럼
(`masterdata-key-convention` — TrinityCore·Luban 관용). 기존 문자열 키 테이블(`TbCharacter` 등)은
레거시라 건드리지 않는다.

> **`UserStats`의 경계:** 필드 이름 `gameMode`→`queueId` **리네임까지가 이 spec**이다. 같은 테이블의
> 구조적 모순(`userId @unique`라 큐별 전적을 못 가짐)과 레이팅 알고리즘은 **D의 범위**다(§11).
> 리네임은 기계적이라 먼저 해도 안전하고, D가 그 위에서 스키마를 고친다.

## 4. 데이터 모델 — Luban 단일 진실원본

Excel(`infrastructure/table/Datas/`) → Luban → **세 갈래 출력:**

| 소비자 | 형식 | 비고 |
|---|---|---|
| 클라 | `.cs` + `.bytes` (group `c`) | 표시 정보(이름·아이콘·설명) 포함 |
| 게임 서버 | `.cs` + `.bytes` (group `s`) | |
| **매치메이킹 서버** | **JSON** | Luban JSON 출력. 기존 XML 로더를 대체 |

게임을 하나 추가하면 **Excel 한 행**으로 클라 목록·서버 정원·매칭 규칙이 동시에 갱신된다.

### `TbGameMode`

| 컬럼 | 뜻 |
|---|---|
| `id` | 정수 기본키 |
| `code` | `Dodgeball` 등 식별용 문자열 |
| `name` / `description` | 표시용 (클라 전용 group `c`) |
| `minPlayers` / `maxPlayers` | **정원. 게임이 소유한다** |

정원을 큐가 아니라 게임이 갖는 이유는 §6에 있다 — 풀 방식은 *게임을 가정한 뒤* 인원을 세므로
1v1 게임과 8인 게임이 한 큐에 섞여도 문제가 없다.

### `TbMap`

| 컬럼 | 뜻 |
|---|---|
| `id` | 정수 기본키 |
| `gameModeId` | **어느 게임의 맵인가** (종속) |
| `code` | 식별용 문자열 |
| `name` | 표시용 (클라 전용 group `c`) |
| `scenePath` | 게임 서버가 로드할 씬. 현재 `LOPRunner.MapId` 하드코딩을 대체 |

> 요소 타입 이름이 `Map`이 아니라 **`GameMap`인 이유(명명 결정)**: Luban이 생성하는 TypeScript는
> 모든 테이블에서 `getDataMap(): Map<number, T>`를 쓴다. 빈 클래스 이름을 `Map`으로 두면 이 제네릭이
> TypeScript 내장 `Map<K,V>` 대신 우리 빈 클래스로 해석되어 **`schema.ts` 전체가 컴파일되지 않는다.**
> 테이블명 `TbMap`, stem `tbmap`, 필드명은 `full_name`과 컬럼명에서 나오므로 영향이 없다 — 흔들리는
> 것은 `__tables__.xlsx`의 `value_type` 한 칸뿐이다. 이 결정을 모르고 슬라이스 2에서 "일관성을 위해"
> `value_type`을 `Map`으로 되돌리면 TypeScript 타깃이 원인 불명으로 컴파일이 깨진다.

### `TbQueue`

| id | code | 실력 폭(시작) | 폭 최대 | 초당 확장 | 랭크 표시 | 게임 선택 | 맵 선택 | 허용 게임 | 최대 대기 |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Casual | ±500 | ±2000 | +50 | X | 플레이어 | 플레이어 | 1,2,3,4,5 | 30초 |
| 2 | Ranked | ±100 | ±400 | +10 | O | 서버 | 서버 | 1,2,3,4,5 | 60초 |

**핵심 — 정책이 코드가 아니라 데이터다.** "랭킹전에서도 게임을 고르게 하고 싶다"는 두 칸을
`서버`→`플레이어`로 바꾸면 끝이고, "주말 닷지볼 전용전"은 행 추가다. 코드 변경 0줄.

**모든 큐가 실력 매칭을 한다.** 친선전이 "실력 무시"가 아니라 **폭이 넓고 유저에게 안 보이는**
것이다(LoL 숨은 노멀 MMR, 오버워치 빠른대전과 동일). `랭크 표시`가 X면 매칭에는 쓰되
화면에 티어·점수를 노출하지 않는다.

**폭이 시간에 따라 넓어진다.** 유저가 적을 때 처음부터 넓히면 매칭 품질이 나쁘고, 안 넓히면
영영 안 잡힌다. 표준(Open Match·FlexMatch)대로 **대기 시간에 비례해 완화**한다.

**허용 게임 목록이 "랜덤"의 범위를 통제한다.** 랜덤은 *"세상의 모든 게임 중 아무거나"* 가 아니라
*"이 큐가 허락한 것 중 아무거나"* 다. 8인 파티 게임을 기대하고 랜덤을 눌렀는데 1v1이 걸리는
상황이 문제가 되면, 그 큐의 목록에서 빼거나 큐를 나눈다 — 둘 다 데이터 편집이다.

## 5. 티켓 — 선택은 "후보 목록"이다

```
MatchmakingTicket {
    userIds:     [민수]        // 파티 대비. 지금은 항상 1명
    queueId:     1
    gameModeIds: [1]           // 지정 / 빈 목록 = 제한 없음(랜덤)
    mapIds:      [3]           // 빈 목록 = 제한 없음
    rating:      1050
    createdAt
}
```

**빈 목록 = 제한 없음.** 이 한 가지 표현으로 지금까지 나온 경우가 전부 통일된다:

| 상황 | `gameModeIds` |
|---|---|
| 친선전, 닷지볼 지정 | `[1]` |
| 친선전, 랜덤 | `[]` |
| 친선전, 여러 개 체크 (미래) | `[1, 2]` |
| **랭크전 (못 고름)** | `[]` — 큐 정책이 강제 |

**"서버가 뽑는다"가 별도 개념이 아니라 그냥 "후보가 비어 있다"** 가 된다. 그래서 랭크전을 위한
분기 코드가 없다 — 친선전과 **같은 코드**가 돈다.

**맵은 게임에 종속이므로**, 게임이 안 정해진 티켓의 `mapIds`는 비어 있어야 한다.
클라 UI에서도 게임을 랜덤으로 고르면 맵 선택을 **숨긴다**(비활성보다 숨김 — 고를 수 없는 이유가
자명하므로).

## 6. 매칭 — 풀 + 주기 평가 (표준 배치)

### 6-1. 역할 분리

현재 `Updater`(1초 tick + 등록/해제되는 `Updatable` 집합)는 **백엔드에 게임 루프를 흉내 낸 것**이라
§1-2의 3번 문제를 낳는다. [Open Match](https://openmatch.dev/site/docs/guides/matchmaker/director/)의
역할 분리로 전환한다.

| 역할 | 하는 일 | LOP 배치 |
|---|---|---|
| **Frontend** | 티켓 생성/취소/조회 (HTTP) | 기존 API 서버 |
| **Director** | **주기적으로 매칭을 돌리는 주체.** 결과 매치를 룸 서버에 할당 | **별도 프로세스 신설** |
| **MatchFunction** | 풀을 보고 매치 제안 생성 | Director가 호출 |
| **Evaluator** | 겹치는 제안 중 선택 | Director가 호출 |

**Director를 API 서버에서 떼어내는 것이 핵심이다:**

- API 서버는 **몇 대로든 늘릴 수 있다** (티켓 CRUD만 하므로)
- Director는 **한 대만** 돈다 → 매칭이 두 번 돌아 같은 사람을 두 매치에 넣는 사고가 원천 차단
  (k8s replica 1. 후일 다중화가 필요하면 리더 선출)
- 재시작해도 **풀(티켓)만 보면 되니** 잃을 상태가 없다
- `Updater` / `Updatable` / `WaitingRoomUpdater`는 삭제

> **Open Match 제품 자체는 도입하지 않는다.** k8s 네이티브 + gRPC + 자체 데이터 모델을 강제하는데
> 현 규모엔 무겁다. **어휘와 역할 분리만 따르면** 후일 실제 도입 시 구조가 이미 맞아 이전이 쉽다.

### 6-2. 알고리즘

```
매 틱(기본 1초), 각 큐마다:

  [MatchFunction] 제안 생성
      각 게임 g ∈ 큐.허용게임:
          eligible = 티켓 중
                     (g를 허용: gameModeIds 가 비었거나 g 포함)
                   ∧ (레이팅 구간이 서로 겹침)
              // 티켓의 레이팅 구간 = [rating − 폭, rating + 폭]
              // 폭 = min(큐.시작폭 + 큐.초당확장 × 대기초, 큐.폭최대)
              // 한 제안에 담기는 티켓들은 구간이 서로 모두 겹쳐야 한다
          if |eligible 인원 합| ≥ g.minPlayers:
              g.maxPlayers 까지 담아 제안 생성

  [Evaluator] 겹치는 제안 해소
      인원 큰 제안 우선 → 동률이면 가장 오래 기다린 티켓이 낀 쪽

  선택된 제안마다:
      Match 생성 (게임 확정 → 그 게임의 맵 중에서 맵 확정)
      룸 생성 → 유저 위치 갱신 → 티켓 삭제
```

**게임을 먼저 가정하고 인원을 세기 때문에**, 게임마다 정원이 크게 달라도(1v1 / 4인 / 8인)
같은 코드로 처리된다. 이것이 점진적 채우기(방을 먼저 만드는 방식)로는 불가능한 지점이다 —
방을 만드는 순간 정원을 알아야 하는데 그때는 게임이 안 정해져 있다.

**최대 대기 시간을 넘긴 티켓**은 최소 인원만 채워지면 출발시킨다(큐의 `최대 대기` 컬럼).

### 6-3. 무엇이 해결되는가

| §1-2의 문제 | 어떻게 사라지나 |
|---|---|
| 1. 큐·게임을 안 보고 방 선택 | 티켓을 게임별로 모으는 것이 알고리즘 자체 |
| 2. 먼저 온 사람이 조건 결정 | 방이 없음. 매 틱 전체를 보고 결정 |
| 3. 재시작 시 방 멈춤 / 다중 인스턴스 불가 | 무상태. 풀만 훑음 + Director 단일화 |
| 4. `maxWaitingTime` 하드코딩 | `TbQueue` 컬럼으로 |
| 5. 죽은 `WaitingRoomStatus` | 엔티티째 삭제 |
| 6. N+1 조회 | 큐 단위 일괄 조회 |
| 7. 파티 불가 | 티켓이 인원을 들고, 전체를 보고 묶으므로 자연 해결 |

## 7. `WaitingRoom` 폐기 — 누락 방지 체크리스트

`WaitingRoom`은 **Mirror 와이어를 타지 않는다**(전부 HTTP WebAPI). 따라서 **proto 재생성·MessageId
이동 위험이 없다** — 이 프로젝트에서 가장 위험한 축이 해당 없음.

아래는 착수 시점(2026-07-27) 기준 전수 조사 결과다. 슬라이스 도중 중단되더라도 이 목록으로
잔재를 추적한다.

### MatchmakingServer (19) — 소유자, 대부분 파일 통째 삭제

```
prisma/schema.prisma                        model WaitingRoom + enum WaitingRoomStatus 삭제
src/controllers/waitingRoom.controller.ts   삭제
src/daos/waitingRoom.dao.mongoose.ts        삭제
src/daos/waitingRoom.dao.postgres.ts        삭제
src/daos/waitingRoom.dao.redis.ts           삭제
src/dtos/waitingRoom.dto.ts                 삭제
src/factories/waitingRoom.factory.ts        삭제
src/interfaces/waitingRoom.interface.ts     삭제
src/mappers/controllers/waitingRoom.mapper.ts  삭제
src/mappers/entities/waitingRoom.mapper.ts     삭제
src/models/waitingRoom.model.ts             삭제
src/repositories/waitingRoom.repository.ts  삭제
src/routes/waitingRoom.route.ts             삭제
src/services/waitingRoom.service.ts         삭제 (로직은 Director/MatchFunction으로)
src/updater/waitingRoomUpdater.ts           삭제
src/updater/updater.ts                      삭제 (Director가 대체)
src/interfaces/responseCode.interface.ts    WaitingRoom 관련 코드 정리
src/interfaces/user-location.interface.ts   Location enum 값
src/main.ts                                 라우트 등록 제거
src/services/matchmaking.service.ts         호출부 재작성
```

### LobbyServer (8)

```
prisma/schema.prisma                        enum Location: WaitingRoom → Matchmaking (마이그레이션)
src/interfaces/waitingRoom.interface.ts     삭제
src/services/waitingRoom.service.ts         삭제
src/interfaces/responseCode.interface.ts
src/interfaces/user-location.interface.ts
src/mappers/entities/user.location.mapper.ts
src/services/httpServices/matchmakingServer.service.ts
src/services/user-location.service.ts
```

### Client (11)

```
Assets/Scripts/Domain/Location.cs                                   enum 값 WaitingRoom → Matchmaking
Assets/Scripts/Domain/WaitingRoomLocationDetail.cs                  → MatchmakingLocationDetail
                                                                    (waitingRoomId 제거, matchmakingTicketId만 남음)
Assets/Scripts/Lobby/LobbyLifetimeScope.cs                          등록
Assets/Scripts/Matchmaking/MatchStateMachine/MatchEvent.cs
Assets/Scripts/Matchmaking/MatchStateMachine/States/InWaitingRoom.cs  → Matchmaking.cs
Assets/Scripts/Matchmaking/MatchStateMachine/States/CancelMatchmaking.cs
Assets/Scripts/Matchmaking/MatchStateMachine/States/CheckMatch.cs
Assets/Scripts/Matchmaking/MatchStateMachine/States/RequestMatchmaking.cs
Assets/Scripts/UI/Matchmaking/MatchmakingViewModel.cs               하드코딩 제거
Assets/Scripts/WebAPI/Dto/Response/GetUserLocationResponse.Deserialize.cs
Assets/Scripts/WebAPI/ResponseCode.cs
```

### RoomServer (2) / 게임 Server (1) / Shared (0)

```
RoomServer/src/interfaces/responseCode.interface.ts
RoomServer/src/interfaces/user-location.interface.ts
Server/Assets/Scripts/WebAPI/ResponseCode.cs
Shared                                                              해당 없음 ✅
```

> **부수 발견:** `ResponseCode`가 5개 저장소에 **손으로 복제**돼 있다. 이번 범위는 아니지만
> 한 곳이 바뀌면 나머지를 손으로 맞춰야 하는 구조라 별도 정리 대상이다.

## 8. 슬라이스 분해

각 슬라이스는 독립적으로 컴파일·동작 확인이 되어야 한다.

| | 슬라이스 | 내용 | 검증 |
|---|---|---|---|
| **1** | **Luban 테이블 신설** | `TbGameMode`/`TbMap`/`TbQueue` Excel 저작 + gen + 매칭서버 JSON 로더(XML 대체). **런타임 동작 변경 0** | 클·서 컴파일, 매칭서버가 JSON으로 같은 값 로드 |
| **2** | **필드 어휘 리네임 + `Match` 라운드화** | `matchType`→`queueId`, `subGameId`→`gameModeId`, `SubGameData`→`TbGameMode`, `UserStats.gameMode`→`queueId`. **`Match`의 게임·맵을 `rounds[]`(원소 1개)로 이전**하고 게임 서버는 `rounds[0]`을 읽음. 백엔드 → 클라 순. **`WaitingRoom` 관련 이름은 건드리지 않음**(4·5에서) | 매칭 기존과 동일 동작, 게임이 정상 진입 |
| **3** | **티켓 모델 확장** | `creator`→`userIds[]`, `gameModeIds[]`/`mapIds[]` 후보 목록. 아직 매칭 로직은 기존 | 티켓 생성·조회 |
| **4** | **Director 전환 (백엔드)** | `Updater`/`WaitingRoom` 삭제, Director+MatchFunction+Evaluator 신설, 별도 배포. §7의 **MatchmakingServer·LobbyServer·RoomServer** 항목 소진 | 1인·다인 매칭, 재시작 중 매칭, 랜덤/지정 혼합 |
| **5** | **클라 잔재 정리** | §7의 **Client·게임 Server** 항목 소진 — `Location`/FSM 상태 개명, `MatchmakingLocationDetail`, `MatchmakingViewModel` 하드코딩 제거 | 매칭 전 구간 |

슬라이스 1·2는 **동작을 바꾸지 않는 준비 작업**이라 안전하고, 4가 실제 전환이다.

### 슬라이스 2 확정 사항 (착수 시점 2026-07-30에 코드를 열어 보고 정한 것)

**타입은 전면 정수 id로 간다.** 이름만 바꾸는 게 아니라 `matchType`(enum)·`subGameId`(string)·
`mapId`(string)가 각각 `queueId`/`gameModeId`/`mapId`(전부 `int`, 슬라이스 1의 Luban 테이블 기본키)가
된다. `enum GameMode { Normal, Ranked }`는 다섯 곳(백엔드 3앱 `enums.ts`, 클라 `Enums.cs`, 게임서버)에서
**삭제**되고 Casual=1 / Ranked=2는 `TbQueue` 행이 된다. 슬라이스 1이 남긴 임시 헬퍼
`findGameModeByCode`도 함께 사라진다(그 코드의 주석이 예고한 대로).

**`MatchRound`는 별도 테이블이되 surrogate `id`를 둔다.** 복합 PK(`@@id([matchId, index])`)가
자연스러워 보이지만, 기존 제네릭 DAO가 `T extends { id: any }` + `where: { id }`를 요구해서 들어가지
않는다. 그래서 `id String @id @default(uuid())` + `@@unique([matchId, index])`로 두며, 이는 스키마의
다른 모든 모델과도 일관된다.

**라운드 I/O는 `MatchRepository`(애그리게잇 루트)가 감춘다.** `save(match)`가 match와 rounds를 함께
쓰고 `findById`가 rounds를 채워 돌려준다. 호출부(`MatchService`·room-server)는 라운드가 별도 테이블인
것을 몰라도 되고, 제네릭 DAO·매퍼 구조와 쓰이지 않는 mongoose/redis DAO 변형도 그대로다.

**매치 행과 그 라운드는 한 트랜잭션으로 쓴다** — *라운드 없는 매치는 유효한 매치가 아니기* 때문이다.
(착수 시점엔 "현 코드에 트랜잭션 보장이 없으니 같은 수준을 유지한다"고 적었으나, Task 2 리뷰에서
`라운드 전부 삭제 → 새로 삽입` 사이가 끊기면 매치가 **라운드 0개로 영구 저장**된다는 구체적 유실
경로가 드러나 뒤집었다. 애그리게잇 하나를 한 DB 안에서 묶는 것뿐이라 값이 싸다.)
**여전히 미루는 것은 그 바깥의 넓은 경로다** — 매치 생성 → 룸 생성 → 유저 위치 갱신 → 티켓 삭제는
다른 서비스로 나가는 HTTP 호출을 포함해 DB 트랜잭션으로 묶을 수 없다. 슬라이스 4의 Director 소관.

**마이그레이션은 데이터를 보존하지 않는다.** 로컬 개발 DB뿐이고 실유저가 없다. 부작용으로 DB가
비면 에디터 픽스처의 게스트 uuid(`ConfigureRoomComponent.playerList`)가 무효가 된다 — 알려진 반복
함정이라 재생성이 필요하다.

**게임 서버에서 실제 동작이 하나 바뀐다.** `LOPRunner.MapId` 하드코딩이 `rounds[0].mapId` →
`TbMap.scene_path`로 대체된다. (`ConfigureRoomComponent`의 프로덕션 경로는 `GetMatch`의 반환값을
쓰지 않지만 `RoomDataStore`가 `GetMatchResponse`를 구독해 `match`를 채우므로 문제가 없다 — 07-30
E2E에서 `playerList` 기반 인증이 동작한 것이 그 증거다.) 다만 **맵 로딩이 이제 매치 조회에 의존하므로**
매치가 없을 때의 실패가 "맵이 안 뜬다"로 나타난다 — 그 경우 룸을 `Error`로 보고하고 죽는 경로가
필요하다.

**죽은 코드 `MatchSetting`/`NotifyStartServer`는 지운다.** 삭제되는 `GameMode` enum을 참조하는데
`WebAPI.NotifyStartServer`를 부르는 곳이 0곳이고 룸 서버에 대응 핸들러도 없다. 부르는 데가 없는
코드를 기계적으로 리네임하는 것보다 지우는 쪽이 정직하다.

**클라 `UserDataStore`의 `normalUserStats`/`rankedUserStats`는 `Dictionary<int, UserStats>`(queueId
키)가 된다.** 두 프로퍼티를 바깥에서 읽는 코드가 0곳이라 안전하고, "큐가 enum이 아니라 데이터"라는
이번 변경의 요지와 맞는다. 반면 `MatchmakingViewModel`의 하드코딩은 **값만 정수로** 바꾸고 제거는
슬라이스 5에 남긴다(§8의 슬라이스 경계).

**큐 목록을 데이터로 순회하는 것은 이번 슬라이스가 아니다.** 클라 `CheckUserComponent`(전적 2건 조회)와
로비 서버 `createUser`(전적 2행 시딩)는 큐를 리터럴 1·2로 둔다. 클라는 `LoadMasterDataComponent`가
`CheckUserComponent`보다 **뒤에** 실행돼 그 시점에 `TbQueue`가 없고, 로비 서버는 아직 마스터데이터를
아예 싣지 않는다. 둘 다 큐를 실제로 화면에 그리는 E(로비 선택 UI)에서 자연스럽게 해소된다 — 리네임
슬라이스에서 진입 순서와 서버 부팅 경로까지 건드리지 않는다.

**작업 순서**: DB 스키마 → 매칭서버 → 로비/룸서버 → 게임서버 → 클라. 클라를 마지막에 두는 이유는
Unity 에디터가 main 체크아웃에 묶여 있어 **워크트리의 클라 코드는 머지 전 컴파일 검증이 안 되기**
때문이다 — 진짜 게이트는 머지 후 에디터 리프레시다.

## 9. 검증

### 자동 테스트

| 대상 | 위치 | 케이스 |
|---|---|---|
| MatchFunction | MatchmakingServer | 랜덤만 N명 / 지정만 N명 / 혼합 / 인원 부족 / 정원 다른 게임 혼재(1v1 vs 8인) |
| Evaluator | MatchmakingServer | 겹치는 제안 해소, 대기 오래된 티켓 우선 |
| 레이팅 폭 확장 | MatchmakingServer | 시간 경과에 따른 폭 증가, 최대 폭 상한 |
| 마스터데이터 무결성 | MasterData EditMode | `TbQueue.허용게임`의 id가 `TbGameMode`에 존재 / `TbMap.gameModeId` 유효 / 큐 정책 값 유효 |

`TbQueue`/`TbGameMode`/`TbMap`을 **`LOPMasterData.TableFiles`에 등록**해야 한다
(`masterdata-new-table-checklist` — 누락 시 `KeyNotFoundException`. 기존 `TableFileManifestTests`가 잡음).

### 수동 검증

- 랜덤 2~3명만 있을 때 매칭되고 **게임·맵이 실제로 무작위로** 결정되는가
- 랜덤 유저와 지정 유저가 **같은 매치에 묶이는가**
- 지정이 서로 다른 두 유저가 **묶이지 않는가**
- 매칭 중 서버 재시작 → 티켓이 살아 있고 매칭이 계속되는가 (현 구조에서는 멈춤)
- 랭크전에서 클라가 게임을 지정해 보내도 **무시되는가**

## 10. 산업 표준 매핑

| LOP | 대응 | 근거 |
|---|---|---|
| `GameMode` | Riot `gameMode`(CLASSIC/ARAM), 언리얼 `AGameMode`, Mediatonic 사내 어휘 | 규칙 정의자. 언리얼 `AGameMode`↔`EndMatch`는 Slice D에서 이미 채택한 매핑과 동일 축 |
| `Queue` | Riot `queueId`/`queues.json`, Halo·Destiny 플레이리스트 | 매칭 풀 + 정책 선언 |
| `Map` | Riot `mapId` | |
| `Round` | Fall Guys의 Round, 철인 3종의 leg | 목록으로 짓고 원소 1개로 시작 |
| `MatchmakingTicket` | Open Match `Ticket`, GameLift FlexMatch matchmaking ticket | **이미 정합** |
| Director / MatchFunction / Evaluator | Open Match 동명 컴포넌트 | 역할 분리 그대로 |
| 후보 목록 매칭 | Open Match `Pool` 필터, Halo 플레이리스트 다중선택 | |
| 대기 시간에 따른 폭 확장 | Open Match·FlexMatch의 relaxation | |
| 숨은 실력값 + 넓은 폭(친선) | LoL 노멀 MMR, 오버워치 빠른대전 | |

**참고:** [Open Match Director](https://openmatch.dev/site/docs/guides/matchmaker/director/) ·
[Open Match Backfill](https://open-match.dev/site/docs/guides/backfill/) ·
[Riot Developer Docs](https://developer.riotgames.com/docs/lol) ·
[Wikipedia — Minigame](https://en.wikipedia.org/wiki/Minigame) ·
[Wikipedia — Fall Guys](https://en.wikipedia.org/wiki/Fall_Guys)

## 11. 범위 밖 (별도 spec)

### C. 매치 결과

지금 **승패 판정도, 결과 컬럼도, 결과 보고 경로도 전혀 없다.** 매치 종료는 5분 타이머
(`LOPRunner.cs:94`)고 `EndMatch()`는 상태만 바꾼다. `Match` 테이블에는 `playerList`만 있고
승자·점수·종료시각이 없으며, 게임 서버 WebAPI에 결과 보고 엔드포인트가 없다.

### D. 레이팅

`UserStats`에 `eloRating`·`mmr`·`tier`가 있으나 **GET 라우트만 있고 갱신 경로가 없어 죽어 있다.**
또한 `userId @unique`라 **유저당 행이 하나**인데 `gameMode` 컬럼이 있어 큐별 전적을 동시에 가질 수
없다(클라는 둘 다 요청 중 — 구조적 모순). Elo는 1:1 전용이라 다인 FFA에는 맞지 않는다
(TrueSkill·Glicko-2·OpenSkill 계열이 표준). 미니게임별 전적 축도 없다.

**이 spec은 D의 "큐 쪽 절반"만 정한다** — 큐마다 실력 폭이 다르고, 랭크 표시 여부가 다르며,
큐별로 실력값을 따로 두는 구조(LoL 방식)를 전제한다. 알고리즘·스키마는 D에서 확정한다.

### E. 로비 선택 UI

큐·게임·맵을 고르는 화면과 로비 다듬기. §4의 `TbQueue` 정책(`게임 선택`/`맵 선택`)을 읽어
UI를 구성하므로 이 spec이 선행돼야 한다.

## 12. Open Decisions

- [ ] **Director 틱 주기** — 기본 1초 유지 vs 부하·대기시간 보고 조정. 슬라이스 4에서 측정 후 결정
- [ ] **Evaluator 선택 규칙** — 초기안은 "인원 큰 제안 우선 → 오래 기다린 티켓 우선". 게임 편중이
      관측되면 가중치 도입 검토
- [ ] **`ResponseCode` 5중 복제 정리** — 이번 범위 밖. 별도 슬라이스
- [ ] **매치 종료 시 유저 위치 백엔드 정리** — 기존 파킹 항목(`flow-slice-d-match-result`)이
      이 트랙과 같은 영역을 건드림. 슬라이스 4~5 시점에 함께 볼지 판단
- [x] ~~**`Match`의 라운드 표현**~~ — **목록으로 확정**(§2). DB 스키마라 나중 변경 비용이 크고,
      확장이 추측이 아니라 계획된 것이라 지금 열어둔다
