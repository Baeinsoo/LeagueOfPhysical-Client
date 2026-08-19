# 슬라이스 C — 결과 보고 + 멱등 확정 + 레이팅 갱신 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 한 판이 끝나면 게임서버가 등수를 보고하고, 백엔드가 그것을 **정확히 한 번** 확정하며 실력 점수(`mmr`)를 실제로 움직인다.

**Architecture:** 게임서버(권위)가 방을 닫기 **전에** lobby-server의 내부 라우트로 등수를 보고한다. lobby-server는 `Match.state` 조건부 갱신(CAS) → 참가자 명단 검증 → `@lop/rating` 계산 → 참가자 결과·`UserRating` 기록을 **한 DB 트랜잭션**으로 처리한다. 두 번째 보고는 CAS가 0행을 바꾸므로 계산을 건너뛰고 저장된 결과를 그대로 돌려준다(멱등).

**Tech Stack:** pnpm + turbo 모노레포, TypeScript, Prisma 6 + PostgreSQL, jest + testcontainers, `@lop/rating`(OpenSkill), Unity(C#) 게임서버

**Spec:** `docs/superpowers/specs/2026-08-17-match-result-rating-design.md`

## Global Constraints

- **결과 확정은 정확히 한 번.** 같은 결과가 두 번 와도 점수는 한 번만 움직이고, 두 번째 응답은 첫 번째와 같아야 한다.
- **보고는 방을 닫기 *전에*.** `Room.status = Closed`가 저장되는 순간 그 파드는 룸서버 정리 대상이 되고 정리는 2초마다 돈다 — 닫은 뒤에 보고하면 파드가 사라져 결과가 영영 안 나간다.
- **보고가 실패해도 방 닫기·클라 통보·배수·종료는 강행한다.** 기존 코드의 철학("클라를 끝난 방에 가둬 두는 쪽이 더 나쁘다")을 그대로 따른다. 그 판은 점수 무변화로 남는다.
- **참가자 2명 미만이면 점수를 갱신하지 않는다.** 기록(`placement`)은 남긴다. (`@lop/rating`이 이미 이 규칙을 갖고 있다.)
- **캐주얼 큐도 점수를 갱신한다.** `has_visible_rank`는 *보여주느냐*의 플래그일 뿐이다.
- **명단은 만들어지지 않고 채워진다.** 보고에 명단에 없는 `userId`가 오거나 명단 일부가 빠지면 **거절**한다.
- **`/internal` + 내부 키.** 클라는 이 경로를 부를 수 없다.
- **빌드 검증은 캐시를 무력화해서** — `pnpm exec turbo run build --force`, 출력의 `Cached: 0 cached`를 확인하고 보고에 붙인다. (이 브랜치 계열에서 캐시 히트가 거짓 "통과"를 만든 전례가 있다.)
- **테스트보다 빌드를 먼저 돌린다.** 타입만 깨져도 테스트는 통과한다.
- **푸시는 `CLAUDE.md`의 "푸시 규약"대로** — 원격 main 리베이스 → `--ff-only` → `--no-ff` 머지. force push 금지.

---

## 스펙과 달라지는 것 (결정과 근거)

**① `Match` 접근을 lobby-server에도 둔다.** 스펙 §3은 "`Match` 표의 주인은 lobby-server로 옮긴다"고
했지만 슬라이스 A가 그 이사를 하지 않아 지금 `Match`/`MatchParticipant` 접근 코드는 **matchmaking-server에만**
있다. 통째로 옮기는 것은 **위험하다** — 디렉터의 "티켓 선점 CAS + 매치 생성 + 라운드"가 한 트랜잭션인데,
그게 HTTP 너머로 가면 원자성이 깨진다(취소한 유저가 매치에 실려가는 걸 막는 바로 그 장치다).

→ **생성은 matchmaking, 확정은 lobby.** lobby에 결과 확정용 DAO를 새로 둔다. DB가 한 덩어리라
"주인"은 코드 배치의 문제이고, 이 배치라야 **확정 세 가지(`Match.state`·참가자·`UserRating`)가 한
트랜잭션에 들어간다.** 결과를 matchmaking이 받으면 `UserRating`이 lobby 소유라 마지막 하나가 HTTP
너머로 나가 부분 실패가 생긴다.

**② 등수 산출을 `IGameRuleSystem`에 얹는다.** 스펙 §8은 별도 `IMatchOutcomeResolver`를 제안했지만, 그
스펙을 쓴 뒤 게임 모드 축 B1이 **`IGameRuleSystem`**(`"게임별 서버 룰 — 누구를 어디에 스폰하고,
**무엇으로 점수를 매기고**, 언제 끝내는지. 언리얼의 GameMode에 해당한다"`)을 들여왔다. 스펙이 `Resolver`
이름의 근거로 든 언리얼 `AGameMode::DetermineMatchWinner`는 **GameMode의 메서드**이지 별도 인터페이스가
아니다. 같은 스코프에 게임별 seam을 두 개 두지 않는다.

**③ `Match.state`는 `InProgress`를 거치지 않는다.** 아무도 그 값을 쓰지 않으므로 CAS 조건은
`state != 'Finished'`로 충분하다. 게임 시작 보고를 새로 만들지 않는다(YAGNI).

---

## File Structure

### `lop-backend/packages/rating` — 이미 있음 (슬라이스 B)
소비만 한다. `initialRating()`, `rateMatch(entries)`, `toMmr(rating)`, `MMR_SCALE`, `MMR_BASE`.

### `lop-backend/apps/lobby-server` (T1·T2·T3)
| 파일 | 책임 |
|---|---|
| `package.json` | `@lop/rating` 의존 추가 |
| `Dockerfile` | `packages/rating` 복사 + 빌드 |
| `src/factories/user-rating.factory.ts` | 기본값을 엔진에서 받아오게(앵커 중복 해소) |
| `src/daos/match-result.dao.postgres.ts` | 확정 트랜잭션 하나 — CAS + 참가자 + UserRating |
| `src/interfaces/match-result.interface.ts` | 도메인 타입 |
| `src/services/match-result.service.ts` | 명단 검증 + 엔진 호출 + DAO 위임 |
| `src/dtos/match-result.dto.ts` | 요청·응답 계약 |
| `src/controllers/match-result.controller.ts` | HTTP 어댑터 |
| `src/routes/internal.route.ts` | 라우트 등록 |
| `test/integration/db.ts` | `match`/`matchRound` 정리 추가 |
| `test/integration/matchResult.integration.test.ts` | 멱등·검증·점수 이동 |

### `lop-backend/apps/matchmaking-server` (T0)
| 파일 | 책임 |
|---|---|
| `src/daos/match.dao.postgres.ts` | `upsert`의 `update`에서 생애 컬럼 제외 |

### `LeagueOfPhysical-Server` (Unity, T4)
| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Game/IGameRuleSystem.cs` | `ResolveOutcome()` 추가 |
| `Assets/Scripts/Game/FlapWangRuleSystem.cs` | 무작위 등수(배선 실증용) |
| `Assets/Scripts/Game/FlappyRaceRuleSystem.cs` | 같은 자리 채움(진짜 등수는 B2/D) |
| `Assets/Scripts/Domain/MatchOutcome.cs` | 등수 결과 타입 |
| `Assets/Scripts/WebAPI/Dto/Request/ReportMatchResultRequest.cs` | 요청 DTO |
| `Assets/Scripts/WebAPI/Dto/Response/ReportMatchResultResponse.cs` | 응답 DTO |
| `Assets/Scripts/WebAPI/WebAPI.cs` | 보고 호출 |
| `Assets/Scripts/Room/LOPRoom.cs` | 종료 순서에 보고 삽입 |
| `Assets/Scripts/Room/IRoomDataStore.cs` + 구현체 | 등수를 러너→룸으로 넘기는 자리 |
| `Assets/Scripts/Game/LOPRunner.cs` | `EndMatch()`에서 등수 산출 |

---

## Task 0: `upsert`가 생애 컬럼을 덮지 않게 한다

**Files:**
- Modify: `lop-backend/apps/matchmaking-server/src/daos/match.dao.postgres.ts`
- Test: `lop-backend/apps/matchmaking-server/test/integration/matchParticipant.integration.test.ts`

**Interfaces:**
- Consumes: (없음)
- Produces: 같은 매치를 다시 저장해도 `state`/`startedAt`/`endedAt`이 보존된다 — T2의 CAS 자물쇠가 성립하는 전제

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`matchParticipant.integration.test.ts` 맨 아래에 describe를 하나 더한다. **그 파일의 기존 헬퍼를
그대로 쓴다**(컨트롤러가 확인한 실제 시그니처): `매치(id)` — 인자는 **id 하나뿐**이고 명단은 안 받는다,
`라운드(matchId)`, `resetTables`, `rawPrisma`, 그리고 `matchDao`는 `beforeEach`에서 만들어진다.

```ts
describe('매치를 다시 저장해도 판의 생애는 보존된다', () => {
    let dao: MatchDaoPostgres;

    beforeEach(async () => {
        await resetTables();
        dao = new MatchDaoPostgres();
    });

    it('state가 Created로 되돌아가지 않는다', async () => {
        await dao.saveWithRounds(매치('M1'), 라운드('M1'), ['U1', 'U2'], [], []);

        //  결과 확정이 지나간 상태를 흉내낸다.
        await rawPrisma.match.update({
            where: { id: 'M1' },
            data: { state: 'Finished', endedAt: new Date() },
        });

        //  같은 매치가 다시 저장돼도 확정 사실이 지워지면 안 된다 —
        //  그 컬럼이 결과를 한 번만 확정하게 하는 자물쇠다.
        await dao.saveWithRounds(매치('M1'), 라운드('M1'), ['U1', 'U2'], [], []);

        const 매치행 = await rawPrisma.match.findUniqueOrThrow({ where: { id: 'M1' } });
        expect(매치행.state).toBe('Finished');
        expect(매치행.endedAt).not.toBeNull();
    });
});
```

> ⚠️ 그 파일의 `매치(id)` 헬퍼는 `state: 'Created' as const`를 박아 넣는다. 즉 **두 번째 저장이
> 넘기는 엔티티에도 `state: 'Created'`가 들어 있고**, 그래서 이 테스트가 실제로 문다.

- [ ] **Step 2: 실패를 확인한다**

```bash
cd lop-backend
pnpm --filter matchmaking-server test:integration -- matchParticipant
```
Expected: FAIL — `state`가 `'Created'`로 되돌아간다.

- [ ] **Step 3: `update`에서 생애 컬럼을 뺀다**

`src/daos/match.dao.postgres.ts`의 `tx.match.upsert` 부분:

```ts
                //  update에 match를 통째로 넣으면 state/startedAt/endedAt까지 덮어써서, 결과가
                //  확정된 매치를 다시 저장할 때 확정 사실이 지워진다. 그 컬럼들은 판의 생애를
                //  기록하는 자리이고 여기(매치 성사)는 그 생애의 시작점일 뿐이라, 다시 저장할 때
                //  건드리지 않는다. 생성 시에는 스키마 기본값(Created)이 그대로 들어간다.
                const { state, startedAt, endedAt, ...updatable } = match;

                const savedMatch = await tx.match.upsert({
                    where: { id: match.id },
                    update: updatable,
                    create: match,
                });
```

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```bash
pnpm exec turbo run build --force
pnpm --filter matchmaking-server test:integration -- matchParticipant
```
Expected: 빌드 `Cached: 0 cached` 후 성공, 통합 테스트 전부 green.

- [ ] **Step 5: 커밋**

```bash
git add apps/matchmaking-server
git commit -m "fix(match): 매치 재저장이 판의 생애를 덮지 않게 한다

state/startedAt/endedAt은 결과를 한 번만 확정하게 하는 자물쇠다. upsert의
update가 그걸 덮으면 확정된 매치가 다시 Created로 돌아간다."
```

---

## Task 1: `@lop/rating`을 lobby-server에 배선한다 (앵커 중복 해소)

**Files:**
- Modify: `lop-backend/apps/lobby-server/package.json`
- Modify: `lop-backend/apps/lobby-server/Dockerfile`
- Modify: `lop-backend/apps/lobby-server/src/factories/user-rating.factory.ts`
- Test: `lop-backend/apps/lobby-server/src/factories/__tests__/user-rating.factory.test.ts` (신규)

**Interfaces:**
- Consumes: `@lop/rating`의 `initialRating(): { mu, sigma }`, `toMmr(rating): number`
- Produces: lobby-server가 `@lop/rating`을 의존한다(T2가 `rateMatch`를 쓴다). `UserRatingFactory.create()`의 기본값이 엔진에서 나온다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`src/factories/__tests__/user-rating.factory.test.ts`:

```ts
import { initialRating, toMmr } from '@lop/rating';
import { UserRatingFactory } from '@factories/user-rating.factory';

describe('UserRatingFactory', () => {
    it('신규 유저의 기본값은 레이팅 엔진에서 온다', () => {
        //  이 값이 세 곳(엔진·팩토리·Prisma 기본값)에 복제돼 있었다. 팩토리를 엔진에
        //  묶어 두 곳으로 줄인다 — 남은 하나(Prisma 기본값)는 SQL이라 코드를 못 부른다.
        const 신규 = UserRatingFactory.create();
        const 엔진 = initialRating();

        expect(신규.mu).toBe(엔진.mu);
        expect(신규.sigma).toBe(엔진.sigma);
        expect(신규.mmr).toBe(toMmr(엔진));
    });

    it('Prisma 스키마의 기본값과도 일치한다', () => {
        //  스키마는 SQL이라 엔진을 못 부른다. 어긋나면 "가입 경로로 만든 유저"와
        //  "DB 기본값으로 생긴 유저"의 시작점이 달라지므로 여기서 잡는다.
        const 신규 = UserRatingFactory.create();

        expect(신규.mu).toBe(25);
        expect(신규.sigma).toBe(8.333333333333334);
        expect(신규.mmr).toBe(1000);
    });

    it('넘긴 값은 기본값을 덮는다', () => {
        expect(UserRatingFactory.create({ queueId: 2, gamesPlayed: 7 })).toMatchObject({
            queueId: 2,
            gamesPlayed: 7,
        });
    });
});
```

- [ ] **Step 2: 실패를 확인한다**

```bash
cd lop-backend
pnpm --filter lobby-server test -- user-rating.factory
```
Expected: FAIL — `Cannot find module '@lop/rating'`

- [ ] **Step 3: 의존을 더하고 팩토리를 엔진에 묶는다**

`apps/lobby-server/package.json`의 `dependencies`에 한 줄:
```json
        "@lop/rating": "workspace:*",
```

`src/factories/user-rating.factory.ts` — 하드코딩된 세 값을 엔진에서 받는다:
```ts
import { initialRating, toMmr } from '@lop/rating';
import { UserRating } from '@interfaces/user-rating.interface';

//  기본값을 여기 적지 않는다 — mu/sigma와 거기서 나오는 mmr은 한 몸이라, 따로 적으면
//  엔진의 눈금을 바꿀 때 신규 유저만 조용히 어긋난다.
export class UserRatingFactory {
    public static create(properties?: Partial<UserRating>): UserRating {
        return { ...UserRatingFactory.createDefault(), ...properties };
    }

    private static createDefault(): UserRating {
        const rating = initialRating();

        return {
            id: '',
            userId: '',
            queueId: 1,
            mu: rating.mu,
            sigma: rating.sigma,
            mmr: toMmr(rating),
            gamesPlayed: 0,
            firstPlaces: 0,
            placementSum: 0,
        };
    }
}
```

- [ ] **Step 4: 도커파일에 패키지를 더한다**

`apps/lobby-server/Dockerfile` — **`COPY packages/server-core` 아래**와 **`pnpm --filter lobby-server run build` 위**:

```dockerfile
COPY packages/rating ./packages/rating
```
```dockerfile
RUN pnpm --filter @lop/rating run build
```

> 도커파일은 패키지를 선택적으로 복사한다. 빠뜨리면 **로컬 빌드는 워크스페이스 hoisting 때문에
> 통과하고 도커의 격리 설치에서만 깨진다** — 그래서 아래 Step 6에서 실제로 도커 빌드를 돌린다.

- [ ] **Step 5: 설치·빌드·테스트**

```bash
pnpm install
pnpm exec turbo run build --force
pnpm --filter lobby-server test
```
Expected: `Cached: 0 cached`로 빌드 성공, 팩토리 테스트 3건 green, 기존 lobby 단위 테스트도 green.

- [ ] **Step 6: 도커 빌드로 실증한다**

```bash
docker build -f apps/lobby-server/Dockerfile -t lobby-server:slice-c-check .
```
Expected: 성공. **실패하면 그게 이 Step의 요점이다** — 도커파일 두 줄이 빠졌거나 순서가 틀린 것이다.
성공 후 이미지는 지워도 된다(`docker rmi lobby-server:slice-c-check`).

- [ ] **Step 7: 커밋**

```bash
git add apps/lobby-server pnpm-lock.yaml
git commit -m "feat(lobby): 신규 유저 기본값을 레이팅 엔진에서 받는다

mu/sigma와 mmr은 한 몸이라 따로 적으면 눈금을 바꿀 때 어긋난다. 도커파일이
패키지를 선택적으로 복사하므로 packages/rating 복사·빌드도 함께 더한다."
```

---

## Task 2: 결과 확정 트랜잭션 (CAS + 참가자 + 레이팅)

**Files:**
- Create: `lop-backend/apps/lobby-server/src/interfaces/match-result.interface.ts`
- Create: `lop-backend/apps/lobby-server/src/daos/match-result.dao.postgres.ts`
- Create: `lop-backend/apps/lobby-server/src/services/match-result.service.ts`
- Modify: `lop-backend/apps/lobby-server/test/integration/db.ts`
- Test: `lop-backend/apps/lobby-server/test/integration/matchResult.integration.test.ts`

**Interfaces:**
- Consumes: T1의 `@lop/rating` 의존
- Produces:
  - `type ReportedPlacement = { userId: string; placement: number }`
  - `type ConfirmedParticipant = { userId: string; placement: number; mmrBefore: number; mmrAfter: number }`
  - `MatchResultService.confirm(matchId: string, placements: ReportedPlacement[]): Promise<{ code: number; participants?: ConfirmedParticipant[] }>`
  - 코드: 성공 `ResponseCode.SUCCESS`, 매치 없음 `ResponseCode.MATCH_NOT_EXIST`, 명단 불일치 `ResponseCode.INVALID_MATCH_RESULT`(신규, 20001)

- [ ] **Step 1: 실패하는 통합 테스트를 쓴다**

먼저 `test/integration/db.ts`의 `resetTables`에 두 줄을 더한다(테스트가 매치를 만든다):
```ts
    await rawPrisma.matchRound.deleteMany({});
    await rawPrisma.match.deleteMany({});
```
`matchParticipant.deleteMany` **뒤**에 둔다(FK는 없지만 읽는 사람에게 순서가 자연스럽다).

`test/integration/matchResult.integration.test.ts`:

```ts
import { rawPrisma, resetTables, connectRedis, disconnectAll } from './db';
import MatchResultService from '@services/match-result.service';
import { ResponseCode } from '@lop/server-core';
import { initialRating, toMmr } from '@lop/rating';

const service = new MatchResultService();

async function 매치를_만든다(matchId: string, userIds: string[], queueId = 1): Promise<void> {
    await rawPrisma.user.createMany({
        data: userIds.map(id => ({ id, username: `u-${id}` })),
    });
    await rawPrisma.userRating.createMany({
        data: userIds.map(id => {
            const r = initialRating();
            return { userId: id, queueId, mu: r.mu, sigma: r.sigma, mmr: toMmr(r) };
        }),
    });
    await rawPrisma.match.create({
        data: { id: matchId, queueId, targetMmr: 1000, state: 'Created' },
    });
    await rawPrisma.matchParticipant.createMany({
        data: userIds.map(id => ({ matchId, userId: id })),
    });
}

describe('결과 확정', () => {
    beforeAll(connectRedis);
    beforeEach(resetTables);
    afterAll(disconnectAll);

    it('등수를 채우고 점수를 움직인다', async () => {
        await 매치를_만든다('M1', ['U1', 'U2']);

        const 응답 = await service.confirm('M1', [
            { userId: 'U1', placement: 1 },
            { userId: 'U2', placement: 2 },
        ]);

        expect(응답.code).toBe(ResponseCode.SUCCESS);

        const 참가자 = await rawPrisma.matchParticipant.findMany({
            where: { matchId: 'M1' }, orderBy: { userId: 'asc' },
        });
        expect(참가자.map(p => p.placement)).toEqual([1, 2]);

        const 레이팅 = await rawPrisma.userRating.findMany({ orderBy: { userId: 'asc' } });
        expect(레이팅[0].mmr).toBeGreaterThan(1000);   //  1등
        expect(레이팅[1].mmr).toBeLessThan(1000);      //  꼴등
        expect(레이팅[0].gamesPlayed).toBe(1);
        expect(레이팅[0].firstPlaces).toBe(1);
        expect(레이팅[1].firstPlaces).toBe(0);
        expect(레이팅[0].placementSum).toBe(1);
        expect(레이팅[1].placementSum).toBe(2);
    });

    it('매치를 Finished로 확정하고 끝난 시각을 남긴다', async () => {
        await 매치를_만든다('M1', ['U1', 'U2']);

        await service.confirm('M1', [
            { userId: 'U1', placement: 1 },
            { userId: 'U2', placement: 2 },
        ]);

        const 매치 = await rawPrisma.match.findUniqueOrThrow({ where: { id: 'M1' } });
        expect(매치.state).toBe('Finished');
        expect(매치.endedAt).not.toBeNull();
    });

    it('같은 결과가 두 번 와도 점수는 한 번만 움직인다', async () => {
        await 매치를_만든다('M1', ['U1', 'U2']);
        const 등수 = [
            { userId: 'U1', placement: 1 },
            { userId: 'U2', placement: 2 },
        ];

        const 첫번째 = await service.confirm('M1', 등수);
        const 두번째 = await service.confirm('M1', 등수);

        expect(두번째.code).toBe(ResponseCode.SUCCESS);
        //  두 번째는 계산을 건너뛰고 저장된 결과를 그대로 돌려준다.
        expect(두번째.participants).toEqual(첫번째.participants);

        const 레이팅 = await rawPrisma.userRating.findMany({ orderBy: { userId: 'asc' } });
        expect(레이팅[0].gamesPlayed).toBe(1);
        expect(레이팅[0].mmr).toBe(첫번째.participants![0].mmrAfter);
    });

    it('명단에 없는 userId가 섞이면 거절하고 아무것도 바꾸지 않는다', async () => {
        await 매치를_만든다('M1', ['U1', 'U2']);

        const 응답 = await service.confirm('M1', [
            { userId: 'U1', placement: 1 },
            { userId: '침입자', placement: 2 },
        ]);

        expect(응답.code).toBe(ResponseCode.INVALID_MATCH_RESULT);

        const 매치 = await rawPrisma.match.findUniqueOrThrow({ where: { id: 'M1' } });
        expect(매치.state).toBe('Created');   //  CAS가 롤백돼야 한다
        const 레이팅 = await rawPrisma.userRating.findMany();
        expect(레이팅.every(r => r.mmr === 1000)).toBe(true);
    });

    it('명단 일부가 빠져도 거절한다', async () => {
        await 매치를_만든다('M1', ['U1', 'U2']);

        const 응답 = await service.confirm('M1', [{ userId: 'U1', placement: 1 }]);

        expect(응답.code).toBe(ResponseCode.INVALID_MATCH_RESULT);
    });

    it('없는 매치는 MATCH_NOT_EXIST', async () => {
        const 응답 = await service.confirm('없음', [{ userId: 'U1', placement: 1 }]);

        expect(응답.code).toBe(ResponseCode.MATCH_NOT_EXIST);
    });

    it('참가자가 1명이면 기록만 남기고 점수는 안 움직인다', async () => {
        await 매치를_만든다('M1', ['U1']);

        const 응답 = await service.confirm('M1', [{ userId: 'U1', placement: 1 }]);

        expect(응답.code).toBe(ResponseCode.SUCCESS);
        const 참가자 = await rawPrisma.matchParticipant.findMany({ where: { matchId: 'M1' } });
        expect(참가자[0].placement).toBe(1);
        const 레이팅 = await rawPrisma.userRating.findMany();
        expect(레이팅[0].mmr).toBe(1000);
        expect(레이팅[0].gamesPlayed).toBe(1);   //  판수는 센다
    });
});
```

- [ ] **Step 2: 실패를 확인한다**

```bash
cd lop-backend
pnpm --filter lobby-server test:integration -- matchResult
```
Expected: FAIL — `Cannot find module '@services/match-result.service'`

- [ ] **Step 3: 도메인 타입을 만든다**

`src/interfaces/match-result.interface.ts`:
```ts
/** 게임서버가 보고한 한 사람의 등수. 1이 1등이고 같은 값이면 동점이다. */
export type ReportedPlacement = {
    userId: string;
    placement: number;
};

/** 확정된 한 사람의 결과. 클라의 결과 화면이 이걸 그대로 보여준다. */
export type ConfirmedParticipant = {
    userId: string;
    placement: number;
    mmrBefore: number;
    mmrAfter: number;
};
```

- [ ] **Step 4: 확정 트랜잭션을 만든다**

`src/daos/match-result.dao.postgres.ts`:

```ts
import { Prisma } from '@lop/database';
import { prismaClient } from '@lop/server-core/postgres';
import { initialRating, rateMatch, toMmr } from '@lop/rating';
import { ConfirmedParticipant, ReportedPlacement } from '@interfaces/match-result.interface';

export type ConfirmOutcome =
    | { kind: 'confirmed'; participants: ConfirmedParticipant[] }
    | { kind: 'alreadyConfirmed'; participants: ConfirmedParticipant[] }
    | { kind: 'matchNotFound' }
    | { kind: 'rosterMismatch' };

/** 명단 불일치로 트랜잭션을 통째로 되돌리기 위한 신호. 밖으로 새지 않는다. */
class RosterMismatch extends Error {}

export class MatchResultDaoPostgres {

    /**
     * 결과를 정확히 한 번 확정한다.
     *
     * 순서가 중요하다:
     * 1. `state != Finished`인 매치만 Finished로 **조건부 갱신**(CAS). 0행이면 이미 확정된 것이므로
     *    계산을 건너뛰고 저장된 결과를 돌려준다 — 재시도한 게임서버가 같은 답을 받는다.
     * 2. 명단 검증은 CAS **뒤**에 한다. 여기서 던지면 트랜잭션이 통째로 롤백돼 CAS도 없던 일이 된다.
     *    (먼저 검증하면 두 요청이 동시에 통과해 둘 다 계산할 수 있다.)
     * 3. 점수 계산·기록은 같은 트랜잭션 안에서 끝낸다.
     */
    public async confirm(matchId: string, placements: ReportedPlacement[]): Promise<ConfirmOutcome> {
        try {
            return await prismaClient.$transaction(async (tx: Prisma.TransactionClient) => {
                const match = await tx.match.findUnique({ where: { id: matchId } });
                if (!match) {
                    return { kind: 'matchNotFound' } as const;
                }

                const claimed = await tx.match.updateMany({
                    where: { id: matchId, state: { not: 'Finished' } },
                    data: { state: 'Finished', endedAt: new Date() },
                });

                if (claimed.count === 0) {
                    return {
                        kind: 'alreadyConfirmed',
                        participants: await this.readConfirmed(tx, matchId),
                    } as const;
                }

                const roster = await tx.matchParticipant.findMany({
                    where: { matchId },
                    orderBy: { userId: 'asc' },
                });

                const 보고된 = [...placements].map(p => p.userId).sort();
                const 명단 = roster.map(p => p.userId).sort();
                if (보고된.length !== 명단.length || 보고된.some((id, i) => id !== 명단[i])) {
                    throw new RosterMismatch();
                }

                const before = new Map<string, { mu: number; sigma: number; mmr: number }>();
                for (const userId of 명단) {
                    const row = await tx.userRating.findFirst({
                        where: { userId, queueId: match.queueId },
                    });
                    const seed = initialRating();
                    before.set(userId, row
                        ? { mu: row.mu, sigma: row.sigma, mmr: row.mmr }
                        : { mu: seed.mu, sigma: seed.sigma, mmr: toMmr(seed) });
                }

                const 순서 = [...placements].sort((a, b) => a.userId.localeCompare(b.userId));
                const after = rateMatch(순서.map(p => ({
                    rating: { mu: before.get(p.userId)!.mu, sigma: before.get(p.userId)!.sigma },
                    placement: p.placement,
                })));

                const confirmed: ConfirmedParticipant[] = [];

                for (let i = 0; i < 순서.length; i += 1) {
                    const { userId, placement } = 순서[i];
                    const prev = before.get(userId)!;
                    const next = after[i];
                    const mmrAfter = toMmr(next);

                    await tx.matchParticipant.update({
                        where: { matchId_userId: { matchId, userId } },
                        data: {
                            placement,
                            mmrBefore: prev.mmr,
                            mmrAfter,
                            muBefore: prev.mu,
                            muAfter: next.mu,
                            sigmaBefore: prev.sigma,
                            sigmaAfter: next.sigma,
                        },
                    });

                    await tx.userRating.updateMany({
                        where: { userId, queueId: match.queueId },
                        data: {
                            mu: next.mu,
                            sigma: next.sigma,
                            mmr: mmrAfter,
                            gamesPlayed: { increment: 1 },
                            firstPlaces: { increment: placement === 1 ? 1 : 0 },
                            placementSum: { increment: placement },
                        },
                    });

                    confirmed.push({ userId, placement, mmrBefore: prev.mmr, mmrAfter });
                }

                return { kind: 'confirmed', participants: confirmed } as const;
            });
        } catch (error) {
            if (error instanceof RosterMismatch) {
                return { kind: 'rosterMismatch' };
            }
            return Promise.reject(error);
        }
    }

    /** 이미 확정된 매치의 저장된 결과. 재시도한 보고에 같은 답을 주기 위한 것이다. */
    private async readConfirmed(tx: Prisma.TransactionClient, matchId: string): Promise<ConfirmedParticipant[]> {
        const rows = await tx.matchParticipant.findMany({
            where: { matchId },
            orderBy: { userId: 'asc' },
        });

        return rows.map(row => ({
            userId: row.userId,
            placement: row.placement ?? 0,
            mmrBefore: row.mmrBefore ?? 0,
            mmrAfter: row.mmrAfter ?? 0,
        }));
    }
}
```

> **`rateMatch`가 2명 미만이면 아무것도 안 바꾼다** — 그래서 1인 매치에서도 위 루프가 그대로 돌고
> `mmr`만 그대로 남는다. 판수·등수합은 센다(스펙 §6: "기록은 남긴다").

- [ ] **Step 5: 서비스를 만든다**

`src/services/match-result.service.ts`:
```ts
import { ResponseCode } from '@lop/server-core';
import { MatchResultDaoPostgres } from '@daos/match-result.dao.postgres';
import { ConfirmedParticipant, ReportedPlacement } from '@interfaces/match-result.interface';

class MatchResultService {

    private matchResultDao = new MatchResultDaoPostgres();

    public async confirm(
        matchId: string,
        placements: ReportedPlacement[],
    ): Promise<{ code: number; participants?: ConfirmedParticipant[] }> {
        try {
            const outcome = await this.matchResultDao.confirm(matchId, placements);

            switch (outcome.kind) {
                case 'confirmed':
                case 'alreadyConfirmed':
                    return { code: ResponseCode.SUCCESS, participants: outcome.participants };
                case 'matchNotFound':
                    return { code: ResponseCode.MATCH_NOT_EXIST };
                case 'rosterMismatch':
                    return { code: ResponseCode.INVALID_MATCH_RESULT };
            }
        } catch (error) {
            return Promise.reject(error);
        }
    }
}

export default MatchResultService;
```

> **`ResponseCode` 확인 결과(컨트롤러가 미리 봄):** `MATCH_NOT_EXIST = 20000`은 **있고**,
> `INVALID_PARAMETER`는 **없다**. `packages/server-core/src/interfaces/responseCode.interface.ts`의
> Match 블록에 새 상수를 더한다 — **기존 값은 절대 건드리지 않는다**:
> ```ts
>     public static readonly INVALID_MATCH_RESULT = 20001;
> ```
> Unity 클라의 `ResponseCode.cs`에는 **더하지 않는다** — 이 라우트를 부르는 건 게임서버뿐이고
> 게임서버 레포에는 `ResponseCode.cs`가 아예 없다(확인함). 클라가 이 코드를 볼 일은 슬라이스 D에서
> 결과 화면이 생길 때이고, 그때 필요하면 같은 숫자로 더한다.

- [ ] **Step 6: 테스트가 통과하는지 확인한다**

```bash
pnpm exec turbo run build --force
pnpm --filter lobby-server test:integration -- matchResult
```
Expected: 빌드 `Cached: 0 cached`, 통합 테스트 7건 green.

- [ ] **Step 7: 커밋**

```bash
git add apps/lobby-server
git commit -m "feat(lobby): 매치 결과를 정확히 한 번 확정한다

조건부 갱신(CAS)으로 자물쇠를 잡고, 명단 검증은 그 뒤에 둬서 실패하면 트랜잭션이
통째로 롤백되게 한다. 두 번째 보고는 계산을 건너뛰고 저장된 결과를 돌려준다."
```

---

## Task 3: 내부 라우트 (HTTP 계약)

**Files:**
- Create: `lop-backend/apps/lobby-server/src/dtos/match-result.dto.ts`
- Create: `lop-backend/apps/lobby-server/src/controllers/match-result.controller.ts`
- Modify: `lop-backend/apps/lobby-server/src/routes/internal.route.ts`
- Test: `lop-backend/apps/lobby-server/test/integration/matchResultRoute.integration.test.ts`

**Interfaces:**
- Consumes: T2의 `MatchResultService.confirm(matchId, placements)`
- Produces: `POST /internal/match/:matchId/result`
  - 요청 `{ participants: [{ userId, placement }] }`
  - 응답 `{ code, participants?: [{ userId, placement, mmrBefore, mmrAfter }] }`
  - 내부 키 없으면 401

- [ ] **Step 1: 실패하는 통합 테스트를 쓴다**

`test/integration/matchResultRoute.integration.test.ts`:

```ts
import request from 'supertest';
import { App } from '@lop/server-core/express';
import InternalRoute from '@routes/internal.route';
import { rawPrisma, resetTables, connectRedis, disconnectAll } from './db';
import { initialRating, toMmr } from '@lop/rating';

const app = new App([new InternalRoute()]).getServer();
const KEY = 'test-internal-key';

async function 매치를_만든다(matchId: string, userIds: string[]): Promise<void> {
    await rawPrisma.user.createMany({ data: userIds.map(id => ({ id, username: `u-${id}` })) });
    await rawPrisma.userRating.createMany({
        data: userIds.map(id => {
            const r = initialRating();
            return { userId: id, queueId: 1, mu: r.mu, sigma: r.sigma, mmr: toMmr(r) };
        }),
    });
    await rawPrisma.match.create({ data: { id: matchId, queueId: 1, targetMmr: 1000, state: 'Created' } });
    await rawPrisma.matchParticipant.createMany({ data: userIds.map(id => ({ matchId, userId: id })) });
}

describe('POST /internal/match/:matchId/result', () => {
    beforeAll(connectRedis);
    beforeEach(async () => {
        process.env.INTERNAL_API_KEY = KEY;
        await resetTables();
    });
    afterAll(disconnectAll);

    it('내부 키가 없으면 401', async () => {
        const response = await request(app)
            .post('/internal/match/M1/result')
            .send({ participants: [{ userId: 'U1', placement: 1 }] });

        expect(response.status).toBe(401);
    });

    it('키가 있으면 확정하고 참가자별 점수 변화를 돌려준다', async () => {
        await 매치를_만든다('M1', ['U1', 'U2']);

        const response = await request(app)
            .post('/internal/match/M1/result')
            .set('X-Internal-Api-Key', KEY)
            .send({ participants: [{ userId: 'U1', placement: 1 }, { userId: 'U2', placement: 2 }] });

        expect(response.status).toBe(200);
        expect(response.body.participants).toHaveLength(2);

        const 일등 = response.body.participants.find((p: any) => p.userId === 'U1');
        expect(일등.mmrAfter).toBeGreaterThan(일등.mmrBefore);
    });

    it('participants가 없으면 400', async () => {
        const response = await request(app)
            .post('/internal/match/M1/result')
            .set('X-Internal-Api-Key', KEY)
            .send({});

        expect(response.status).toBe(400);
    });
});
```

- [ ] **Step 2: 실패를 확인한다**

```bash
pnpm --filter lobby-server test:integration -- matchResultRoute
```
Expected: FAIL — 404(라우트 없음).

- [ ] **Step 3: DTO를 만든다**

`src/dtos/match-result.dto.ts`:
```ts
import { IsArray, IsInt, IsNotEmpty, IsString, Min, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { ResponseBase } from '@lop/server-core';

export class ReportedPlacementDto {
    @IsString()
    @IsNotEmpty()
    public userId: string;

    @IsInt()
    @Min(1)
    public placement: number;
}

export class ReportMatchResultDto {
    @IsArray()
    @ValidateNested({ each: true })
    @Type(() => ReportedPlacementDto)
    public participants: ReportedPlacementDto[];
}

export class ConfirmedParticipantDto {
    public userId: string;
    public placement: number;
    public mmrBefore: number;
    public mmrAfter: number;
}

export class ReportMatchResultResponseDto implements ResponseBase {
    public code: number;
    public participants?: ConfirmedParticipantDto[];
}
```

- [ ] **Step 4: 컨트롤러를 만든다**

`src/controllers/match-result.controller.ts` — 같은 폴더의 기존 컨트롤러(`user-rating.controller.ts`)의
상태코드·에러 처리 관례를 그대로 따른다:
```ts
import { NextFunction, Request, Response } from 'express';
import MatchResultService from '@services/match-result.service';
import { ReportMatchResultDto } from '@dtos/match-result.dto';

class MatchResultController {
    private matchResultService = new MatchResultService();

    public reportMatchResult = async (req: Request, res: Response, next: NextFunction) => {
        try {
            const matchId = req.params.matchId;
            const dto = req.body as ReportMatchResultDto;

            const response = await this.matchResultService.confirm(matchId, dto.participants);

            res.status(200).json(response);
        } catch (error) {
            next(error);
        }
    };
}

export default MatchResultController;
```

- [ ] **Step 5: 라우트를 등록한다**

`src/routes/internal.route.ts` — `internalApiKeyMiddleware`를 거는 `this.router.use(...)` **아래**에
(그래야 키 검사가 걸린다), 기존 라우트들과 나란히:
```ts
        this.router.post(
            `${this.path}/match/:matchId/result`,
            validationMiddleware(ReportMatchResultDto, 'body'),
            this.matchResultController.reportMatchResult,
        );
```
클래스 필드에 `public matchResultController = new MatchResultController();`, 상단에 두 import를 더한다.

- [ ] **Step 6: 테스트가 통과하는지 확인한다**

```bash
pnpm exec turbo run build --force
pnpm --filter lobby-server test:integration
```
Expected: 빌드 `Cached: 0 cached`, lobby 통합 테스트 전부 green(신규 3건 포함).

- [ ] **Step 7: 커밋**

```bash
git add apps/lobby-server
git commit -m "feat(lobby): 결과 보고 내부 라우트

게임서버만 부른다. 내부 키가 없으면 401 — 클라는 이 경로에 닿을 수 없다."
```

---

## Task 4: 게임서버 — 등수를 산출하고 보고한다

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Domain/MatchOutcome.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scripts/WebAPI/Dto/Request/ReportMatchResultRequest.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scripts/WebAPI/Dto/Response/ReportMatchResultResponse.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/IGameRuleSystem.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/FlapWangRuleSystem.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyRaceRuleSystem.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/WebAPI/WebAPI.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Room/LOPRoom.cs`

**Interfaces:**
- Consumes: T3의 `POST {lobbyBaseURL}/internal/match/{matchId}/result`
- Produces: `IGameRuleSystem.ResolveOutcome(): MatchOutcome`

> ⚠️ **커밋 오염 금지.** 이 레포에는 커밋하지 않는 로컬 픽스처가 있다
> (`DefaultVolumeProfile.asset`, `ConfigureRoomComponent.cs`, `FlapWangRuleSystem.cs`의 스폰 개수).
> **`git add -A` 금지** — 바꾼 파일만 경로로 지정하고, 커밋 전 `git status --short`로 확인한다.
> `FlapWangRuleSystem.cs`는 픽스처와 우리 변경이 **같은 파일**이므로, 커밋에 우리 메서드만 들어가고
> 스폰 개수 변경은 안 들어가는지 `git diff --cached`로 눈으로 확인할 것.
>
> ⚠️ **새 `.cs`를 만들면 `.meta`도 함께 커밋한다.** Unity 에디터가 만들어 준 것만 커밋하고 직접 쓰지 않는다.
> 에디터가 안 떠 있으면 `.meta`가 안 생기므로, 그 경우 **에디터를 한 번 띄워 생성시킨 뒤** 커밋한다.

- [ ] **Step 1: 결과 타입을 만든다**

`Assets/Scripts/Domain/MatchOutcome.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace LOP
{
    /// <summary>한 판이 끝났을 때 게임이 내놓는 등수. 1이 1등이고 같은 값이면 동점이다.</summary>
    public class MatchOutcome
    {
        public List<MatchPlacement> placements = new List<MatchPlacement>();
    }

    [Serializable]
    public class MatchPlacement
    {
        public string userId;
        public int placement;
    }
}
```

- [ ] **Step 2: 게임별 룰에 등수 산출을 얹는다**

`Assets/Scripts/Game/IGameRuleSystem.cs` — 인터페이스에 한 줄:
```csharp
        /// <summary>이 판의 등수. 무엇으로 순위를 매길지는 게임마다 다르다.</summary>
        MatchOutcome ResolveOutcome();
```

`Assets/Scripts/Game/FlapWangRuleSystem.cs` — 클래스 맨 아래에 더한다:
```csharp
        //  FlapWang은 넷코드 검증용이라 순위 개념이 없다. 결과 보고 배선이 실제로 도는지
        //  확인하려고 무작위로 섞는다 — 진짜 등수는 Flappy Race가 낸다.
        public MatchOutcome ResolveOutcome()
        {
            var userIds = roomDataStore.match.playerList.ToList();

            for (int i = userIds.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (userIds[i], userIds[j]) = (userIds[j], userIds[i]);
            }

            var outcome = new MatchOutcome();
            for (int i = 0; i < userIds.Count; i++)
            {
                outcome.placements.Add(new MatchPlacement { userId = userIds[i], placement = i + 1 });
            }

            return outcome;
        }
```
파일 상단에 `using System.Linq;`가 없으면 더한다.

`Assets/Scripts/Game/FlappyRaceRuleSystem.cs` — 같은 자리에 같은 내용을 넣되 주석만 바꾼다:
```csharp
        //  진짜 등수(결승선 통과 순서)는 게임플레이가 붙는 슬라이스에서 채운다. 그때까지는
        //  보고 경로가 끊기지 않도록 무작위로 둔다.
```
본문은 위 FlapWang과 동일하다. 그 파일은 **생성자 주입**이고 필드 이름이 `roomDataStore`이며
`roomDataStore.match.playerList`는 `string[]`이다(확인함) — `using System.Linq;`가 필요하다.

- [ ] **Step 3: 요청·응답 DTO를 만든다**

`Assets/Scripts/WebAPI/Dto/Request/ReportMatchResultRequest.cs`:
```csharp
using System;

namespace LOP
{
    [Serializable]
    public class ReportMatchResultRequest
    {
        public MatchPlacement[] participants;
    }
}
```

`Assets/Scripts/WebAPI/Dto/Response/ReportMatchResultResponse.cs`:
```csharp
using System;
using GameFramework.Http;

namespace LOP
{
    [Serializable]
    public class ReportMatchResultResponse : HttpResponse
    {
        public ConfirmedParticipantDto[] participants;
    }

    [Serializable]
    public class ConfirmedParticipantDto
    {
        public string userId;
        public int placement;
        public int mmrBefore;
        public int mmrAfter;
    }
}
```
> `HttpResponse`의 실제 네임스페이스·경로는 같은 폴더의 기존 응답 DTO(`GetMatchResponse.cs` 등)를
> 열어 그대로 따른다.

- [ ] **Step 4: 보고 호출을 더한다**

`Assets/Scripts/WebAPI/WebAPI.cs`의 `#region Match` 안:
```csharp
        //  결과는 lobby가 받는다 — 레이팅과 유저 데이터의 주인이고, 확정 세 가지(매치 상태·참가자·
        //  점수)가 거기서 한 트랜잭션에 들어간다.
        public static UniTask<ReportMatchResultResponse> ReportMatchResult(string matchId, ReportMatchResultRequest request, CancellationToken cancellationToken = default)
            => SendAsync<ReportMatchResultResponse>(
                HttpRequestMessage.Post($"{EnvironmentSettings.active.lobbyBaseURL}/internal/match/{matchId}/result", request), cancellationToken);
```

- [ ] **Step 5: 종료 순서에 보고를 끼운다**

`Assets/Scripts/Room/LOPRoom.cs`의 `CloseRoomAsync` — **하트비트 중단 뒤, `UpdateRoomStatus(Closed)` 앞**에:

```csharp
            //  방을 닫기 전에 보고한다. Closed가 저장되는 순간 이 파드는 룸서버 정리 대상이 되고
            //  정리는 2초마다 도니, 닫은 뒤에 보고하면 파드가 사라져 결과가 영영 안 나간다.
            //  실패해도 아래 닫기·통보·배수·종료는 그대로 간다 — 클라를 끝난 방에 가둬 두는 쪽이
            //  더 나쁘다. 그 판은 점수 무변화로 남는다.
            if (!EnvironmentSettings.active.Standalone)
            {
                try
                {
                    //  등수는 러너가 EndMatch() 시점에 이미 채워 뒀다 — 아래 "배선" 참고.
                    var outcome = roomDataStore.outcome;
                    ...   //  전체 코드는 아래 "배선" 블록에 있다
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to report match result. Continuing to close the room. Error: {e.Message}");
                }
            }
```

클래스 상단 상수에 한 줄:
```csharp
        private const double REPORT_TIMEOUT_SECONDS = 1.5;
```

**⚠️ `LOPRoom`은 `IGameRuleSystem`을 주입받을 수 없다 — 배선은 아래대로 한다(컨트롤러가 미리 확인함).**

룰 시스템은 **게임 스코프**(`FlapWangLifetimeScope` 등)에 등록돼 있고 `LOPRoom`은 **룸 스코프**(부모)다.
부모는 자식 스코프의 등록을 못 본다. 그리고 `LOPRoom.runner`의 타입은 `IRunner`(GameFramework의
앱 비종속 인터페이스)라 거기에 LOP 도메인 메서드를 얹을 수도 없다.

**대신 `LOPRunner`가 등수를 만들어 `roomDataStore`에 남기고, `LOPRoom`이 그걸 읽어 보고한다.**
`LOPRunner`는 게임 스코프(자식)라 부모의 `IRoomDataStore`를 주입받을 수 있고, **이미 받고 있다**(확인함).

`Assets/Scripts/Room/IRoomDataStore.cs` — 한 줄 더한다:
```csharp
        /// <summary>이번 판의 등수. 매치가 끝나는 순간 러너가 채우고, 방이 닫히기 전에 보고에 실린다.</summary>
        MatchOutcome outcome { get; set; }
```
구현체(`RoomDataStore`)에도 같은 프로퍼티를 더한다 — 기존 `room`/`match` 프로퍼티와 같은 모양으로.

`Assets/Scripts/Game/LOPRunner.cs`의 `EndMatch()`:
```csharp
        /// <summary>매치 종료 진입점. 종료 판정은 서버 권위이고, 클라는 통보를 받아 같은 이름의 메서드로 들어온다.</summary>
        public void EndMatch()
        {
            //  등수는 지금 뽑는다 — 게임이 아직 살아 있을 때만 알 수 있는 값이라(엔티티·점수),
            //  방이 닫히는 시점에는 이미 늦다. 보고는 LOPRoom이 방을 닫기 전에 한다.
            roomDataStore.outcome = gameRuleSystem.ResolveOutcome();

            gameState = RunnerState.GameOver;
        }
```

그리고 위 Step 5의 보고 블록에서 `gameRuleSystem.ResolveOutcome()` 대신 `roomDataStore.outcome`을 쓴다:
```csharp
                    var outcome = roomDataStore.outcome;
                    if (outcome == null)
                    {
                        //  러너를 거치지 않고 방이 닫히는 경로(초기화 실패 등)가 있다. 보고할 등수가
                        //  없으면 조용히 건너뛴다 — 없는 결과를 지어내지 않는다.
                        Debug.LogWarning("No match outcome to report. Skipping.");
                    }
                    else
                    {
                        using var reportCts = new CancellationTokenSource(TimeSpan.FromSeconds(REPORT_TIMEOUT_SECONDS));

                        await WebAPI.ReportMatchResult(
                            roomDataStore.match.id,
                            new ReportMatchResultRequest { participants = outcome.placements.ToArray() },
                            reportCts.Token);
                    }
```

`IRoomDataStore.cs`·구현체도 커밋 목록에 넣는다(Step 7).

- [ ] **Step 6: 컴파일을 확인한다**

Unity 에디터(서버 프로젝트)에서 컴파일 에러 0을 확인한다. UnityMCP를 쓸 경우 `unity_instance`를
서버 인스턴스로 명시한다. 에디터에 붙을 수 없으면 그 사실을 보고하고 T5의 실플레이가 받는다.

- [ ] **Step 7: 커밋**

```bash
git status --short          # 로컬 픽스처가 스테이지에 없는지 확인
git add Assets/Scripts/Domain/MatchOutcome.cs Assets/Scripts/Domain/MatchOutcome.cs.meta \
        Assets/Scripts/WebAPI/Dto/Request/ReportMatchResultRequest.cs Assets/Scripts/WebAPI/Dto/Request/ReportMatchResultRequest.cs.meta \
        Assets/Scripts/WebAPI/Dto/Response/ReportMatchResultResponse.cs Assets/Scripts/WebAPI/Dto/Response/ReportMatchResultResponse.cs.meta \
        Assets/Scripts/Game/IGameRuleSystem.cs Assets/Scripts/Game/FlapWangRuleSystem.cs \n        Assets/Scripts/Room/IRoomDataStore.cs Assets/Scripts/Room/RoomDataStore.cs Assets/Scripts/Game/LOPRunner.cs \
        Assets/Scripts/Game/FlappyRaceRuleSystem.cs Assets/Scripts/WebAPI/WebAPI.cs \
        Assets/Scripts/Room/LOPRoom.cs
git diff --cached            # FlapWangRuleSystem에 스폰 개수 픽스처가 섞이지 않았는지 눈으로 확인
git commit -m "feat(match): 방을 닫기 전에 등수를 보고한다

Closed가 저장되면 파드가 정리 대상이 되므로 보고는 그 앞이어야 한다. 실패해도
닫기·통보·종료는 강행한다 — 클라를 끝난 방에 가둬 두는 쪽이 더 나쁘다."
```

---

## Task 5: 끝-끝 검증

**Files:** (코드 변경 없음 — 검증만)

- [ ] **Step 1: 배포한다**

```bash
cd lop-backend
gh workflow run backend-deploy.yml --ref <브랜치 또는 main> -f app=all -f environment=local
```
마이그레이션은 없지만 `app=all`이 안전하다. 롤아웃 완료까지 기다린 뒤 시작한다:
```bash
kubectl get deploy -n default -o custom-columns='NAME:.metadata.name,IMAGE:.spec.template.spec.containers[0].image'
```

- [ ] **Step 2: 게임서버 이미지를 새로 굽는다**

**이번엔 게임서버 재빌드가 필요하다** — 슬라이스 A와 달리 Unity 서버 코드가 실제로 동작한다
(등수 산출 + 보고 호출). 배포된 이미지가 옛것이면 **결과가 영영 안 온다.**
게임서버 CI로 이미지를 굽고 `infrastructure`의 `GAME_SERVER_IMAGE`가 새 태그를 가리키는지 확인한다.

- [ ] **Step 3: 실제로 한 판 돌린다**

클라 인스턴스 2개(`local-k8s`) → 로그인 → 매칭 → 게임 진입 → 5분 대기(또는 강제 종료).

- [ ] **Step 4: DB에서 확정을 확인한다**

```sql
SELECT id, state, "endedAt" FROM "Match" ORDER BY "createdAt" DESC LIMIT 1;
SELECT "userId", placement, "mmrBefore", "mmrAfter" FROM "MatchParticipant"
  WHERE "matchId" = '<위 id>' ORDER BY placement;
SELECT "userId", mmr, "gamesPlayed", "firstPlaces", "placementSum" FROM "UserRating" WHERE "queueId" = 1;
```
Expected: `state = 'Finished'`, `endedAt` 있음, 참가자마다 `placement`·`mmrBefore`·`mmrAfter`가 **채워져 있고**,
`UserRating.mmr`이 **1000에서 움직였다**. 1등은 오르고 꼴등은 내렸다.

- [ ] **Step 5: 다음 매칭이 그 값을 읽는지 확인한다**

같은 유저로 다시 매칭을 걸고 티켓의 rating을 본다:
```sql
SELECT "userId", rating FROM "MatchmakingTicket" ORDER BY "createdAt" DESC LIMIT 2;
```
Expected: 1000이 아니라 **방금 갱신된 mmr**. 이게 고리가 닫혔다는 최종 증거다.

- [ ] **Step 6: 멱등을 실물로 확인한다**

방금 끝난 매치에 같은 결과를 한 번 더 보고한다:
```bash
curl -s -X POST "http://localhost/lobby/internal/match/<matchId>/result" \
  -H "X-Internal-Api-Key: <키>" -H 'Content-Type: application/json' \
  -d '{"participants":[{"userId":"<u1>","placement":1},{"userId":"<u2>","placement":2}]}'
```
Expected: 200 + 첫 번째와 **같은** `participants`. 그리고 `UserRating.gamesPlayed`가 **안 늘었다.**

---

## 이번에 하지 않는 것 (경계)

- **`MatchEndedToC`에 결과를 싣는 것** — 지금은 빈 메시지 그대로 둔다. 와이어(proto) 변경은
  LOP-Shared를 건드리고, 그걸 받아 그리는 화면이 없으면 값이 없다. **슬라이스 D**에서 화면과 함께 한다.
  이번 슬라이스의 결과는 **DB에만** 남는다(그리고 다음 매칭이 그걸 읽는다).
- **`Match.state`의 `InProgress`** — 아무도 안 쓴다(위 "스펙과 달라지는 것 ③").
- **Prisma 기본값의 앵커 중복** — SQL이라 코드를 못 부른다. T1이 팩토리를 엔진에 묶어 두 곳으로 줄이고,
  스키마 기본값과 어긋나지 않는지는 T1의 테스트가 지킨다.

---

## 이 슬라이스가 끝나면

- 한 판이 끝나면 등수가 남고 `mmr`이 실제로 움직인다. **다음 매칭이 그 값으로 사람을 붙인다.**
- 남은 것 = **슬라이스 D**(결과 화면에 등수·점수 변화, 프로필에 누적 전적).
