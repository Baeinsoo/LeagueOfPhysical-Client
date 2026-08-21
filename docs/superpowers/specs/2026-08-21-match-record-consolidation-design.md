# 매치 기록 통합 + 전적 목록 설계

한 판의 결과를 **자기완결적인 기록 한 행**으로 저장하고, 그 위에 LoL 전적 보기식 목록 화면을 올린다.

---

## 1. 왜 지금

전적 화면을 만들려고 데이터를 찾아보니, **"매치 결과"라고 부를 표가 없었다.** 판 하나가 세 표에
흩어져 있다:

| 표 | 몇 행 | 담긴 것 |
|---|---|---|
| `Match` | 판당 1 | 끝난 시각, 큐, 상태 |
| `MatchRound` | 라운드당 1 | 게임 모드, 맵 |
| `MatchParticipant` | **사람당 1** | 등수, 점수 변화 |

그래서 전적 20판을 읽으려면 **5쿼리**가 든다:

```
① 내가 낀 매치 id      MatchParticipant WHERE userId = 나
② 그 매치들 (최신 20)   Match WHERE id IN (...) ORDER BY endedAt DESC
③ 게임 모드            MatchRound WHERE matchId IN (...)
④ 그 판들의 전체 참가자  MatchParticipant WHERE matchId IN (...)   ← 상대도 보여줘야 하니 다시
⑤ 이름                 User WHERE id IN (...)
```

①과 ④가 같은 표를 목적만 달리해 두 번 읽는다. 그리고 조회한 걸 코드에서 다시 조립해야 한다.

**이 표들은 전적을 보여주려고 만든 게 아니다.** 매치를 *진행하기 위해* 만들었다 — `Match.state`는
확정 자물쇠고 `MatchParticipant`는 게임서버가 남의 id를 못 끼워 넣게 막는 명단이다. 전적 화면은
그 부산물을 읽는 첫 소비자이고, **읽기용으로 설계된 적이 없다.**

### 더 중요한 것 — "그때 이름"이 없다

이름을 조회 시점에 `User.username`에서 끌어오면, 누가 개명하는 순간 **과거 전적이 소급해서 바뀐다.**
3년 전 판에 지금 닉네임이 붙는다. 전적의 존재 이유가 "그때 무슨 일이 있었나"인데 그게 깨진다.
그리고 **이건 나중에 못 고친다** — 안 박아둔 과거의 이름은 복원할 방법이 없다.

---

## 2. 결정 — 매치 한 행이 자기완결적인 기록이다

`Match` 한 행에 판의 전부를 담는다. `MatchRound`·`MatchParticipant` 두 표는 없앤다.

```prisma
model Match {
  id         String     @id @unique
  queueId    Int
  targetMmr  Int        @default(1000)
  state      MatchState @default(Created)
  createdAt  DateTime   @default(now())
  startedAt  DateTime?
  endedAt    DateTime?

  //  명단. 매치 생성 때 확정되고 이후 불변 — 결과 보고의 위조 방지 기준이다.
  playerList String[]

  //  [{ index, gameModeId, mapId }]. 지금은 원소 1개뿐이지만 여러 게임을 연속으로
  //  하는 형태를 위해 목록으로 둔다(기존 MatchRound와 같은 이유).
  rounds     Json

  //  확정될 때 정확히 한 번 쓴다. 그전에는 null.
  //  [{ userId, displayName, placement, mmrBefore, mmrAfter, muBefore, muAfter, sigmaBefore, sigmaAfter }]
  result     Json?
}
```

전적 조회가 **1쿼리**가 된다:

```sql
SELECT * FROM "Match"
WHERE "playerList" @> ARRAY[$me] AND state = 'Finished'
ORDER BY "endedAt" DESC LIMIT 20;
```

`playerList`에 **GIN 인덱스**를 건다(배열 포함 검색용).

---

## 3. 산업 표준 매핑

**Riot Match-V5**가 LoL 전적의 정본이고, 한 매치 = 자기완결적인 문서 하나다:

- `info`: `queueId`, `gameMode`, `gameCreation`, `gameEndTimestamp`, `gameDuration`
- `info.participants[]`: 참가자별 성적 **+ `riotIdGameName` / `riotIdTagline` / `summonerName`**

**핵심은 마지막이다 — Riot은 플레이어 이름을 매치 기록 안에 박는다.** 계정 표에서 조회하지 않는다.
Riot ID 전환 때도 매치 기록의 이름 필드를 유지했다.

| Riot | 이 설계 |
|---|---|
| 매치 문서 하나 | `Match` 한 행 |
| `info.queueId` / `gameMode` | `queueId` / `rounds[].gameModeId` |
| `info.gameEndTimestamp` | `endedAt` |
| `info.participants[]` | `result[]` |
| `participants[].riotIdGameName` | `result[].displayName` ← **이번에 새로 생기는 것** |

> 일반 원칙: **가변 상태는 정규화하고, 불변 기록은 비정규화한다.** 확정된 매치는 다시 바뀌지 않으므로
> 한 행에 담아도 어긋날 곳이 없고, 오히려 "그 시점의 사실"을 통째로 보존한다.

---

## 4. 왜 나뉘어 있었나 — 그 이유가 사라졌다

슬라이스 A 스펙의 근거는 이랬다:

> `Match { ..., playerList[] }` → `MatchParticipant` 신설. **"문자열 배열엔 참가자별 결과를 못 붙인다."**

맞는 말이었다. 명단이 *문자열 배열*이면 "몇 등, 점수 얼마 변동"을 붙일 자리가 없다.
**결과를 객체 목록(`result`)으로 담으면 그 제약이 사라진다.** 명단(`playerList`)은 문자열 배열로
남되, 결과는 별도 필드에 참가자별로 들어간다. 번복이 아니라 전제가 바뀐 것이다.

---

## 5. 명단 게이트는 그대로 유지된다 (보안)

슬라이스 A가 세운 성질을 깨면 안 된다:

> 참가자 행은 매치 생성 시 미리 깔린다 → 결과 보고가 *명단을 만드는 게 아니라 빈 칸을 채우는 일*이
> 되어, 게임서버가 남의 userId를 끼워 넣을 수 없다.

새 구조에서도 동일하다:

- `playerList`는 **매치 생성 시**(matchmaking-server, 티켓 선점과 같은 트랜잭션) 확정된다
- 결과 보고가 오면 **보고된 userId 집합을 `playerList`와 대조**한다. 어긋나면 거절(`RosterMismatch`)
- 대조는 지금과 같이 **정렬 후 바이트 정확 비교**. `playerList` 자체는 생성 이후 절대 갱신하지 않는다

즉 "명단을 먼저 박고 나중에 채운다"는 성질은 표가 아니라 **쓰기 시점**이 만든다. 표를 합쳐도 유지된다.

---

## 6. 잃는 것 (정직하게)

| 잃는 것 | 영향 | 대응 |
|---|---|---|
| `@@unique([matchId, userId])` | DB가 막아 주던 참가자 중복이 코드 책임이 된다 | `playerList`는 티켓 선점 결과로 한 번만 쓰이고 이후 불변 — 중복은 매칭 버그이지 입력 오류가 아니다. 생성 시 중복 검사 1줄 추가 |
| `Json` 필드의 타입 검사 | Prisma가 `Json`으로 다뤄 DB·ORM이 모양을 검증하지 않는다 | TS 타입 + DAO 경계에서 검증. 쓰는 곳이 **확정 트랜잭션 한 곳**뿐이라 표면이 좁다 |
| SQL 집계 | "평균 등수" 같은 걸 SQL로 뽑으려면 jsonb를 풀어야 한다 | 해당 없음 — 판수·1등·등수합은 이미 `UserRating` 카운터로 유지한다 |
| FK/`onDelete` | 원래도 없었다(로드맵의 고아 행 부채) | 표가 합쳐지면서 **부채 자체가 소멸**한다 |

---

## 7. 응답 계약은 바뀌지 않는다

도메인 모델과 DTO는 이미 우리가 원하는 모양이다:

```ts
Match { id, queueId, targetMmr, playerList: string[], rounds: MatchRoundDto[] }
```

지금은 리포지토리가 세 표에서 읽어 **이 모양으로 도로 조립**하고 있다(`findById`가 3쿼리).
표를 합치면 그 조립이 사라질 뿐, **`GetMatchResponseDto`는 한 글자도 안 바뀐다.**

→ **게임서버·클라는 변경 없음.** 방 접속 인증(`LOPNetworkAuthenticator`), 스폰 루프
(`GameRuleSystem`), 매치 조회 인가(`canReadMatch`)가 전부 그대로 동작한다.

---

## 8. 전적 조회 API

```
GET /user/{userId}/matches?limit=20      (lobby-server, 본인만 — 레이팅 라우트와 같은 인가)
```

```jsonc
{
  "code": 200,
  "matches": [{
    "matchId": "...", "queueId": 1, "endedAt": "2026-08-21T03:15:52.572Z",
    "rounds": [{ "index": 0, "gameModeId": 1, "mapId": 1 }],
    "participants": [
      { "userId": "...", "displayName": "Guest-d686ffee", "placement": 1, "mmrBefore": 875, "mmrAfter": 1033 }
    ]
  }]
}
```

- `mu`/`sigma`는 **내보내지 않는다** — 레이팅 엔진 내부값이라는 3층 분리를 유지한다
- `limit`은 상한 50으로 clamp. 페이징·무한스크롤은 범위 밖(목록이 길어지면 그때)
- 게임 모드 **이름**은 클라가 마스터데이터(`TbGameMode.Name`)로 해석한다 — 서버가 id만 준다

---

## 9. 클라 — 프로필 안에 이어서

- 위: 큐별 요약(이미 있음) / 아래: `ScrollView`에 판 카드
- 카드 하나: **게임 모드 이름 · 날짜 · 내 등수와 점수 증감(강조) · 참가자 등수 목록**
- 본인은 "나", 남은 `displayName`을 짧게(`Guest-d686ffee` → `Guest-d686ff`)
- `ProfileViewModel`이 레이팅과 전적을 함께 받아온다(이미 열 때 재조회하는 구조라 자리가 있다)

---

## 10. 슬라이스

| | 무엇 | 어디 | 끝났다는 기준 |
|---|---|---|---|
| **1** | 스키마 통합 + 쓰기 경로 이전 | 백엔드 (+마이그레이션) | **지금과 똑같이 동작한다.** 매칭·방 접속·결과 확정·멱등성 전부 그대로 |
| **2** | 전적 조회 라우트 | lobby-server | 1쿼리로 최근 20판을 내려준다 |
| **3** | 클라 전적 목록 | 클라 | 프로필에 판 카드가 최신순으로 뜬다 |

**슬라이스 1이 가장 위험하다.** 확정 트랜잭션(멱등 CAS + 명단 대조)을 다시 쓰기 때문이다.
슬라이스 C 수준의 검증을 다시 밟는다 — 아래 테스트 참조.

---

## 11. 테스트

| 대상 | 방법 |
|---|---|
| **멱등 확정** | 통합(testcontainers) — 같은 결과 두 번 → 점수가 한 번만 움직이고, 두 번째 응답이 첫 번째와 같다 |
| **명단 위조** | 매치에 없는 userId를 섞으면 거절 / 일부 누락도 거절 / `playerList`가 갱신되지 않음 |
| **응답 무변화** | 기존 매칭 통합 테스트 green — `GetMatchResponseDto`가 이전과 같은 모양·값 |
| **전적 조회** | 통합 — 최신순 정렬, limit clamp, 남의 전적 조회는 403, 참가자 전원이 담김 |
| **그때 이름** | 확정 후 `User.username`을 바꿔도 **전적의 displayName은 안 바뀐다** |
| 끝‑끝 | 로컬 k8s + 에디터 2대 — 한 판 하고 프로필에서 그 판이 보인다 |

**이미 겪은 함정(반복 금지):**
1. 백엔드는 `pnpm build`를 테스트보다 **먼저** 돌린다 — 타입만 깨져도 테스트는 통과한다
2. **삭제·개명은 역방향으로 검증한다** — "없앤 계약을 아직 부르는 곳이 있나"를 use-side 레포 전부에서
3. 게임서버 이미지는 `kubectl get pods`에 안 보인다 — `kubectl exec deploy/room-server -- printenv GAME_SERVER_IMAGE`

---

## 12. Open Decisions

- [ ] **`result`를 jsonb로 둘지, 참가자별 컬럼 배열로 풀지** — jsonb로 시작한다. 모양이 굳으면
      전용 컬럼으로 승격 검토(지금은 필드가 더 늘 여지가 있다)
- [ ] **닉네임 설정 기능** — 범위 밖. 생기면 `displayName`이 자동으로 좋아진다(화면 무변경)
- [ ] **페이징** — 목록이 20판을 넘겨 의미가 생길 때
