# 매치 결과 기록 + 레이팅 — 고리 닫기

한 판이 끝나면 **누가 몇 등을 했는지 남기고, 그 결과로 실력 점수를 갱신하고, 다음 매칭이 그 점수로
사람을 붙인다.** 지금 이 고리에서 마지막 한 칸(결과 → 점수)이 비어 있어 **모든 유저가 영원히 1000점**이다.

같이 하는 일: 이 도메인의 **용어·구조를 업계 표준으로 재정비**한다(백엔드 + Unity 클라·서버 + 와이어).

---

## 1. 배경 — 읽는 쪽은 다 깔려 있고 쓰는 쪽만 비어 있다

| | 상태 |
|---|---|
| `UserStats` 표 (`eloRating`/`mmr`/`tier`/승·패·무, 유저×큐) | ✅ 있음. 가입 시 캐주얼·랭크 두 행 생성 |
| 매칭이 레이팅을 읽는다 | ✅ `matchmaking.service`가 lobby-server에 물어 `eloRating`을 `targetRating`으로 티켓 발급 → 디렉터가 범위를 넓혀가며 붙임 |
| `Match` / `MatchRound` 표 | ✅ 매치 성사 시 기록 |
| **결과 기록** | ❌ 없음. 게임서버는 끝날 때 `Room.status = Closed`만 올린다 |
| **레이팅 갱신** | ❌ **없음.** `eloRating`은 1000에서 안 움직이고 `wins/losses/draws`는 0 고정 |

즉 실력 기반 매칭의 배선은 이미 완성돼 있는데 **입력이 상수**라 사실상 무작위로 붙는다.
이 문서는 그 한 칸을 채운다.

---

## 2. 목표 / 비목표

### 목표
1. 끝난 매치의 **참가자별 등수**를 서버 권위로 기록한다.
2. 그 등수로 **실력 점수(MMR)를 갱신**하고, 다음 매칭이 그 값을 읽는다.
3. 유저가 **결과 화면에서 등수·점수 변화**를, **프로필에서 누적 전적**을 본다.
4. 이 도메인의 **용어·구조를 표준으로 재정비**한다(1:1 대전 유산·층 섞임 제거).

### 비목표 (이번에 하지 않는다)

| 안 하는 것 | 왜 |
|---|---|
| 티어·승급 연출·시즌 | 표시 설계·구간·승강급 규칙이 통째로 따라붙는다. 점수가 먼저 움직여야 의미가 생긴다 |
| 리더보드 | 조회 부하·랭킹 갱신 주기가 별개 문제 |
| 이탈·AFK 패널티 | 지금 이탈 관측 수단이 없다 |
| 팀전 | 게임 모드가 전부 FFA다. 엔진은 팀을 지원하지만 스키마에 팀 축을 지금 넣지 않는다 |
| 여러 라운드 결과 합산 | `MatchRound`는 자리만 유지. 지금 매치당 라운드 1개 |
| 결과 없는 매치를 쓸어담는 정리기 | 유저 위치에서 이미 내린 판단과 같다 — 읽을 때 고치면 충분하고 지금 아무도 안 아프다 |
| **진짜 등수 산출 로직** | 게임 모드 축 B2/D의 몫(다른 머신). 우리는 **포트와 호출 시점**만 정의하고 FlapWang은 무작위로 배선을 실증한다 |

---

## 3. 왜 이 배치인가 — 산업 표준

두 레퍼런스가 같은 그림을 가리킨다.

- **Open Match**(Google, 매치메이킹 사실상 표준) — 티켓·매치 제안·게임서버 할당까지가 범위이고,
  **끝난 경기의 결과는 명시적으로 범위 밖**이다. 즉 *매치메이킹 서비스는 결과 흐름에 끼지 않는다.*
  Open Match가 만드는 "Match"는 Redis에 잠깐 있다 사라지는 **제안**이고 저장하지도 않는다.
- **PlayFab**(Microsoft) — 권위 있는 게임서버가 **플레이어 데이터·통계 서비스에 직접**
  (`Server/UpdatePlayerStatistics`, 타이틀 시크릿 키) 보고한다. 멀티플레이어 세션/할당 서비스를
  경유하지 않는다. 문서가 못 박은 원칙: **통계는 반드시 서버 권위 경로로만 갱신**(클라 금지).

우리 대입:

```
게임서버(권위) ──내부 키──> lobby-server (= 플레이어 데이터·통계 서비스)
                                 ├ 경기 결과 확정 (Match + MatchParticipant)
                                 └ @lop/rating 으로 MMR 갱신 (UserRating)

matchmaking-server : 티켓·매치 성사·할당까지만            ← 결과 흐름에서 빠짐
room-server        : 방·파드 수명만                        ← 결과 흐름에서 빠짐
```

**`Match` 표의 주인은 lobby-server로 옮긴다.** 표준에서 매치메이킹의 "Match"는 저장되지 않는 제안이고,
우리 Postgres의 `Match` 행은 이미 *저장된 경기 레코드* 자리에 있다. 그 레코드에 결말을 적는 게 맞다
(Riot의 match API도 한 `matchId` 아래 참가자별 `placement`를 다는 같은 모양). 매치메이킹은 생성을
요청할 뿐 이후를 소유하지 않는다.

> **이사 비용이 싼 이유**: DB가 한 덩어리(`packages/database` 공용 Postgres)라 "주인을 옮긴다"가
> 데이터 이사가 아니라 **DAO·서비스 파일이 어느 앱에 사는지**의 문제다.

### 레이팅 엔진 — OpenSkill (Weng‑Lin)

| | 다인전 | 채택 | 우리가 쓸 수 있나 |
|---|---|---|---|
| Elo | ✗ 쌍으로 쪼개야 함 | 가장 흔함(단순해서) | 가능 |
| Glicko‑2 | ✗ 쌍으로 쪼개야 함 | 체스 사이트, CS:GO 등 | 가능 |
| TrueSkill | ✅ 네이티브 (다인전 정석) | Xbox Live 전체 | ⚠️ **MS 특허 + 비상업 라이선스** |
| **OpenSkill (Weng‑Lin)** | ✅ 네이티브 | TrueSkill의 오픈 대체재 | ✅ **MIT** (npm `openskill` v5.0.1) |

**채택 = OpenSkill.** 이유 셋:
1. **다인전이 1급 시민.** "1등, 2등, … 8등"을 그대로 입력받는다. Elo·Glicko는 8명이면 28쌍으로
   쪼개 억지로 합쳐야 한다. 우리 게임(2~8명 FFA, 후일 팀전 가능성)과 정확히 맞는다.
2. **실력을 `평균(μ) + 불확실성(σ)` 두 값으로 본다.** "대충 1000인데 3판밖에 안 해서 확신이 없다"를
   표현할 수 있어 신규 유저가 몇 판 만에 제자리를 찾는다. Elo는 K값 수동 튜닝으로 흉내 낼 뿐이다.
3. **특허 걸림돌 없고 살아 있다.** MIT, 최근 갱신 2026‑07. TrueSkill보다 빠르고 예측 정확도는 대등.

---

## 4. 도메인 모델과 어휘

### 세 층을 분리한다 (지금은 한 줄에 섞여 있다)

| 층 | 뜻 | 누가 쓰나 |
|---|---|---|
| **실력 추정치** `mu`, `sigma` | OpenSkill이 들고 도는 진짜 상태 | 레이팅 엔진만 |
| **MMR** (정수 하나) | μ·σ에서 뽑은 **보수적 점수**. 매칭이 범위를 넓혀가며 비교하는 값 | 매치메이킹 디렉터 |
| **표시값** (티어·랭크) | 유저에게 보이는 것 | **이번 범위 밖 → `tier` 컬럼 제거** |

이 분리 덕에 **디렉터 코드는 한 줄도 안 바뀐다** — 지금도 정수 하나를 읽고 있고 출처만 달라진다.

### 표 변경

| 지금 | 바꾼 뒤 | 왜 |
|---|---|---|
| `Match { id, queueId, targetRating, createdAt, playerList[] }` | `Match { id, queueId, targetMmr, state, createdAt, startedAt, endedAt }` | 문자열 배열엔 참가자별 결과를 못 붙인다. 판의 생애(시작·종료 시각)도 지금 없다 |
| (없음) | **`MatchParticipant { id, matchId, userId, placement?, mmrBefore?, mmrAfter?, muBefore?, muAfter?, sigmaBefore?, sigmaAfter? }`** + `@@unique([matchId, userId])` | 표준 모양. "이 판에서 몇 등, 점수가 얼마나 움직였나"가 여기 남고 결과 화면·전적이 이걸 읽는다 |
| `UserStats { queueId, eloRating, mmr, tier, gamesPlayed, wins, losses, draws }` | **`UserRating { queueId, mu, sigma, mmr, gamesPlayed, firstPlaces, placementSum }`** | ① `eloRating`·`mmr` 중복 해소 ② **FFA에 승/무/패는 1:1 유산** — 2~8명 등수 게임엔 "1등 횟수"와 "등수 합"(→평균 등수)이 맞다 ③ `tier`는 아무도 안 쓴다 |

`MatchRound { matchId, index, gameModeId, mapId }` — **무변경**(자리만 유지).

> ⚠️ **`playerList`는 지우지 않고 파생값으로 남긴다.** 이 필드는 장식이 아니라 세 곳이 읽는 계약이다:
> ① 게임서버 `LOPNetworkAuthenticator` — **방 접속 인증**(명단에 없으면 거절, cutover 1c),
> ② `canReadMatch` — 매치 조회는 참가자만, ③ 게임서버 `GameRuleSystem` — 플레이어 스폰 루프.
> 그래서 **DB의 진실원본은 `MatchParticipant`로 옮기되, 응답 DTO의 `playerList`는 참가자에서 뽑아
> 그대로 내려준다.** Unity 양쪽(`Match`/`MatchDto`)과 정책 코드는 **무변경**이고, 슬라이스 A의
> "동작 무변화"가 성립한다.

**이름 규칙**: 기존 스키마가 `UserProfile`·`UserLocation`·`UserCharacter`로 `User*`를 쓰므로 짝을 맞춰
`UserRating`으로 간다. 업계에선 `PlayerRating`이 더 흔하지만 **한 코드베이스 안의 어휘 일관이 우선**이다.

### `Match.state`

```
Created ──(게임서버가 방을 열고 시작)──> InProgress ──(결과 보고 확정)──> Finished
```

`state`는 장식이 아니라 **중복 보고를 막는 자물쇠**다(§6).

### Unity·와이어 어휘

| 지금 | 바꾼 뒤 |
|---|---|
| (없음) | 게임서버 `MatchOutcome { [{ userId, placement }] }` — 게임이 산출한 등수 |
| `MatchEndedToC` (빈 메시지) | `MatchEndedToC { repeated MatchParticipantResult { userId, placement, mmrBefore, mmrAfter } }` |
| 클라 `MatchResult { matchId }` | `MatchResult { matchId, myPlacement, myMmrDelta, participants[] }` |

메시지 **이름은 유지**한다(`MatchEndedToC`는 사건 이름이고 사건은 그대로다). 필드만 는다.

---

## 5. 결과가 흐르는 경로

### 지금 종료 시퀀스의 제약 (이미 이유가 붙어 있다)

`LOPRoom.CloseRoomAsync`의 주석이 못 박은 것:
- 방 `Closed`가 **먼저** 저장돼야 로비로 돌아간 클라가 방금 끝난 방으로 다시 끌려가지 않는다.
- 그런데 **`Closed`가 저장되는 순간 그 파드는 룸서버 정리 대상**이 되고 정리는 2초마다 돈다.

⇒ **결과 보고는 방을 닫기 *전에* 끝나야 한다.** 닫은 뒤면 파드가 그 사이 지워져 결과가 영영 안 나간다.
클라에 점수 변화를 실어 보내려면 보고의 **응답**이 필요하므로 순서상으로도 앞이 맞다.

```
GameOver
  │
  ├─ ① 게임서버: IMatchOutcomeResolver.Resolve() → MatchOutcome        ← 게임별. FlapWang은 무작위
  │
  ├─ ② POST /internal/match/{matchId}/result  ──────────> lobby-server
  │      { participants: [{ userId, placement }] }            ├ 결과 확정 (CAS)
  │   <──{ participants: [{ userId, placement,                ├ @lop/rating 으로 새 MMR 계산
  │           mmrBefore, mmrAfter }] }                        └ UserRating 갱신   (한 트랜잭션)
  │
  ├─ ③ 방 Closed 저장            (기존 코드)
  ├─ ④ 클라에 MatchEndedToC      (기존 위치, 페이로드만 채워짐)
  └─ ⑤ 배수 → 파드 자가 종료      (기존 코드)
```

### 신뢰 경계

- ②는 `/internal` 라우트 + **내부 키**(cutover 2b에서 만든 구조 그대로). PlayFab의 "타이틀 시크릿
  키를 든 서버만 통계 갱신"과 같은 자리다. **클라는 이 경로를 부를 수 없다.**
- **참가자 행은 매치 생성 시 미리 만든다**(`placement = null`). 결과 보고는 *비어 있는 칸을 채우는*
  일이지 명단을 만드는 일이 아니다. 명단에 없는 `userId`가 오면 **거절**한다. 명단 일부가 빠져도 거절.
- ⇒ **도중에 나간 사람도 등수를 받는다.** 게임서버는 전체 명단을 알고 있으므로 이탈자에게도
  `placement`를 매긴다(탈락 순서 등 — 게임별 resolver의 판단). "이탈 패널티"는 안 만들지만
  "이탈자는 결과에서 빠진다"도 아니다. 빠지면 위 규칙에 걸려 보고 전체가 거절된다.

---

## 6. 중복 보고 — 이 설계의 핵심 위험

재시도·중복 전송으로 같은 결과가 두 번 오면 점수가 두 번 움직인다.
**조회해서 "이미 있나?" 확인하는 방식은 원리적으로 못 막는다** — 두 요청이 둘 다 정직하게 조회하고
둘 다 "없음"을 받을 수 있다(대기표 유일성에서 이미 겪은 그것). 그래서 같은 해법을 쓴다:
**규칙을 DB가 강제하게 한다.**

1. `Match.state`를 **조건부 갱신**한다 — `UPDATE ... SET state='Finished' WHERE id=? AND state<>'Finished'`.
2. 그 갱신이 **1행을 바꾼 트랜잭션 안에서만** 참가자 결과 기록 + `UserRating` 갱신을 한다.
3. 두 번째 보고는 0행을 바꾼다 → **계산을 건너뛰고 이미 저장된 결과를 그대로 응답**한다.
   재시도한 게임서버는 같은 답을 받아 정상 진행한다(**멱등**).
4. `MatchParticipant`의 `@@unique([matchId, userId])`가 이중 안전장치.

### 실패했을 때

②가 실패해도 **③④⑤는 강행한다** — 지금 코드의 철학("클라를 끝난 방에 가둬 두는 쪽이 더 나쁘다")을
그대로 따른다. 짧은 재시도(2~3회, 방 닫기 타임아웃 안쪽) 후 포기하고 그 판은 **점수 무변화**로 남는다.
결과 없는 매치를 나중에 쓸어담는 액티브 정리기는 짓지 않는다(§2 비목표).

### 두 가지 예외 규칙

- **참가자 2명 미만이면 점수를 갱신하지 않는다** — 비교 대상이 없으면 실력 추정이 무의미하다.
  기록(`placement`)은 남긴다.
- **캐주얼 큐도 점수를 갱신한다.** `Queue.has_visible_rank`는 *보여주느냐*의 플래그일 뿐이고,
  숨은 MMR을 굴려야 캐주얼 매칭 품질이 생긴다(업계 표준). 큐별로 `UserRating` 행이 나뉘어 안 섞인다.

---

## 7. 레이팅 엔진 — `@lop/rating`

`packages/rating`. `server-core`와 같은 층. **DB도 HTTP도 모르는 순수 함수 묶음**이라 테스트가 싸고,
엔진을 바꿔도 이 파일들만 갈린다.

```ts
initialRating(): Rating                                  // 신규 유저 (openskill 기본 μ=25, σ=25/3)
rateMatch(entries: { rating: Rating; placement: number }[]): Rating[]
toMmr(rating: Rating): number                            // 매칭이 읽는 정수 한 개
```

- `rateMatch`는 각 참가자를 **1인 팀**으로 넘겨 FFA를 표현한다. 동점(같은 `placement`)은 엔진이
  그대로 무승부로 처리한다.
- `toMmr`은 **보수적 추정** `μ − 3σ`를 쓴다 — "아직 잘 모르는 유저"를 낮게 잡아 고수 자리에 잘못
  넣지 않게 하는 표준 처리. 여기에 스케일을 걸어 **신규 유저가 정확히 1000**이 되게 맞춘다:

  ```
  mmr = round( SCALE × (μ − 3σ) ) + BASE      // BASE = 1000, SCALE = 40 (시작값)
  ```

  신규는 `μ − 3σ = 25 − 25 = 0` → **1000**. 그래서 큐 테이블의 기존 상수(캐주얼 범위 500 / 랭크 100,
  상한 2000 / 400)가 지금 의미 그대로 살아 있고 **디렉터는 무변경**이다.
  `SCALE`·`BASE`는 **한 곳에만** 두고 테스트로 못 박는다(openskill의 `ordinal`이 스케일 인자를
  받으므로 그걸 쓰든 우리가 감싸든 결과는 같아야 한다).

---

## 8. 게임서버 — 등수는 게임이 정한다

등수 산출은 게임마다 다르므로 포트 하나로 가른다:

```
IMatchOutcomeResolver.Resolve() → MatchOutcome
   ├ FlapWangOutcomeResolver     ← 무작위 (배선 실증용)
   └ FlappyRaceOutcomeResolver   ← 진짜 등수. 게임 모드 축 B2/D가 채운다
```

게임 모드 축 B1이 이미 **게임별 스코프**를 갈라 놨으므로 그 자리에 등록한다. 우리가 정의하는 건
**인터페이스와 호출 시점**이고 Flappy Race 구현은 그쪽 트랙이 끼운다 — **두 머신이 안 부딪히는 경계**다.

이름 근거: 언리얼 `AGameMode::DetermineMatchWinner`가 "승패 판정은 게임 모드 소유"라 둔 자리와 같은
개념이라 `Resolver`로 맞춘다.

---

## 9. 클라 — 이미 빈 자리가 둘 있다

| 화면 | 지금 | 채우는 것 |
|---|---|---|
| `MatchResultView` | 플레이스홀더(확인 버튼만) | **내 등수 + 점수 변화(+15) + 참가자 등수표** |
| 프로필 셸 | 제목만 있는 껍데기 | **판수 · 1등 횟수 · 평균 등수 · 현재 MMR** |

- 결과 화면 데이터는 이미 있는 `MatchResultDataStore`를 타고 온다(와이어 → 스토어 → VM → View).
- 프로필은 lobby-server 조회 한 번(`GET /user/{id}/rating?queueId=`, 본인만 —
  cutover 2b의 `requireSelfOrService` 그대로).
- MVVM‑C 규칙대로 View는 VM의 R3만 구독하는 **얇은 바인더**로 둔다(`VisualElement` 상속 아님).

---

## 10. 슬라이스

| | 무엇 | 어디 | 끝났다는 기준 |
|---|---|---|---|
| **A** | 스키마·어휘 재정비 | 백엔드 (+마이그레이션) | **지금과 똑같이 동작한다.** 매칭이 `UserRating.mmr`(=1000)을 읽고 붙는다 |
| **B** | `@lop/rating` 패키지 | 백엔드 (독립) | 단위 테스트 green. 아직 아무도 안 부른다 |
| **C** | 결과 보고 + 확정 + 점수 갱신 | 게임서버 + lobby-server | 한 판 끝내면 **DB의 `mmr`이 실제로 움직인다**. 화면 변화는 없음 |
| **D** | 클라 표시 | 와이어 + 클라 | 결과 화면에 등수·변화가 뜨고 프로필에 전적이 뜬다 |

순서: **A → (B는 언제든 병렬) → C → D**

**A가 가장 중요하다.** 표 이름과 컬럼만 바뀌고 값은 그대로라 **회귀가 없다는 걸 확실히 검증할 수 있는
유일한 지점**이다. C부터는 새 코드가 섞여 원인을 가리기 어려워진다(게임 모드 축 슬라이스 A와 같은 이유).

---

## 11. 테스트

| 대상 | 방법 |
|---|---|
| 레이팅 수식 | `@lop/rating` 단위 — 1등은 오르고 꼴등은 내린다 / 판수가 늘면 σ가 준다 / **신규 = 정확히 1000**(스케일 앵커) / 참가자 1명은 무변화 / 동점 처리 |
| **중복 보고** | lobby-server 통합(기존 testcontainers 하네스) — 같은 결과 두 번 → **점수가 한 번만 움직인다**, 두 번째 응답이 첫 번째와 같다 |
| 명단 위조 | 매치에 없는 `userId`를 섞으면 거절 / 명단 일부 누락도 거절 |
| 매칭 회귀 (슬라이스 A) | 기존 매칭 통합 테스트 green — 티켓 발급이 `UserRating.mmr`을 읽어도 동작 동일 |
| 게임서버 | EditMode — outcome resolver / **보고가 실패해도 방 닫기·클라 통보가 진행된다** |
| 끝‑끝 | 로컬 k8s + 에디터 2대로 한 판 — 결과 화면에 등수·변화가 뜨고, DB `mmr`이 움직이고, **다음 매칭이 그 값을 읽는다** |

**이미 겪은 함정 셋(반드시 지킬 것):**
1. 백엔드는 `pnpm build`를 **테스트보다 먼저** 돌린다 — 타입만 깨져도 테스트는 통과한다.
2. 공유 타입(`@lop/database`·`server-core`)을 건드리면 **전체 테스트**를 돌린다.
3. **마이그레이션이 있으므로 배포는 `app=all`** — 일부만 올리면 에러 없이 기능만 죽는다.

---

## 12. 산업 표준 매핑

| 우리 것 | 대응 |
|---|---|
| 게임서버 → lobby-server 직접 보고 | PlayFab `Server/UpdatePlayerStatistics` (타이틀 시크릿 키, 서버 권위 전용) |
| 매치메이킹이 결과 흐름에서 빠짐 | Open Match — 결과는 명시적으로 범위 밖 |
| `Match` + `MatchParticipant` (참가자별 `placement`) | Riot match API, TFT `placement` (FFA 등수) |
| `mu`/`sigma` ↔ `mmr` ↔ 표시값 3층 분리 | 업계 통례 — 숨은 MMR / 보이는 티어·LP |
| `μ − 3σ` 보수적 추정 | TrueSkill·OpenSkill 표준 `ordinal` |
| 조건부 갱신(CAS)으로 결과 1회 확정 | 멱등 명령 처리 표준 |

**참고**: [OpenSkill (npm)](https://www.npmjs.com/package/openskill) ·
[openskill.js](https://github.com/philihp/openskill.js/) ·
[OpenSkill 논문 (arXiv 2401.05451)](https://arxiv.org/pdf/2401.05451) ·
[Open Match — Matchmaker 가이드](https://open-match.dev/site/docs/guides/matchmaker/) ·
[PlayFab UpdatePlayerStatistics (Server)](https://learn.microsoft.com/en-us/rest/api/playfab/server/player-data-management/update-player-statistics?view=playfab-rest)

---

## 13. Open Decisions

- [ ] **`SCALE = 40`의 적정값** — 실제 판이 쌓여 점수 분포가 보일 때 조정. 한 상수라 바꾸기 쉽지만
      **이미 저장된 `mmr`을 재계산해야** 하므로(μ·σ는 남아 있어 가능) 재계산 스크립트가 필요해진다.
- [ ] **`Match.state`와 `Room.status`의 관계** — 둘 다 "판이 끝났나"를 안다. 지금은 각자 자기 축
      (경기 / 파드)이라 중복이 아니지만, 한쪽이 다른 쪽에서 파생 가능한지는 C 구현에서 확인.
- [ ] **`MatchRound`에 결과를 붙일지** — 여러 라운드가 실제로 생길 때. 지금은 매치 단위 보고 1회.
- [ ] **동점 정책** — 엔진은 동점을 처리하지만, 게임이 동점을 낼 수 있는지는 게임별 판단(FlapWang
      무작위 구현은 동점을 만들지 않는다).
