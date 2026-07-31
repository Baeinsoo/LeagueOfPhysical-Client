# 매치메이킹 슬라이스 4b — Director 전환 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 매칭을 대기방(waiting room) 방식에서 풀 기반 Director 방식으로 실제 전환하고, `WaitingRoom` 관련 코드를 전 저장소에서 제거한다.

**Architecture:** 슬라이스 4a가 만든 순수 함수(`src/director/`)에 배선을 붙인다. 매칭 서버와 **같은 이미지의 두 번째 진입점** `dist/director.js`가 1초 주기로 티켓 풀을 훑어 매치를 확정한다(k8s replica 1, `strategy: Recreate`). 요청 경로(`requestMatchmaking`)는 티켓만 만들고 끝난다. 로비의 자가치유는 대기방이 아니라 티켓 존재만 본다.

**Tech Stack:** TypeScript 5.7 / Node 22 / Express / Prisma 6 / Redis / Jest 29 (ts-jest) / pnpm workspace (turbo) / kustomize + ArgoCD / Luban master data

## Global Constraints

- **백엔드 전용 슬라이스다.** Unity 클라이언트·게임 서버의 `Assets/` 코드는 **한 줄도 바꾸지 않는다** (마스터데이터 `.bytes` 재생성은 예외 — Task 8).
- **`Location.WaitingRoom` enum 이름은 그대로 둔다.** 개명은 슬라이스 5. 4b에서는 *뜻만* "매칭 풀에서 대기 중"으로 바뀐다.
- **새 npm 의존성을 추가하지 않는다.**
- 테스트 실행: `pnpm --filter matchmaking-server test` (repo 루트 `C:/Users/re5na/workspace/LOP/lop-backend`에서).
- 빌드 확인: `pnpm --filter matchmaking-server run build`, `pnpm --filter lobby-server run build`.
- 주석은 **한국어**로, *왜*만 쓴다. 코드로 자명한 것은 주석 없이 둔다.
- 작업 브랜치: `lop-backend`는 `feature/matchmaking-slice4b-director`, `infrastructure`는 `feature/matchmaking-slice4b-director`. **main에 직접 커밋 금지.**
- 티켓 1장의 인원은 `userIds.length`다 (현재 항상 1이지만 코드는 목록으로 다룬다).
- 매칭 알고리즘 순수 함수(`src/director/{ratingRange,requiredPlayerCount,matchFunction,evaluator,selectMap,types}.ts`)는 **이미 존재하며 수정 대상이 아니다.** 이 계획은 그것들을 *부른다*.

---

## File Structure

### `lop-backend/apps/matchmaking-server`

| 파일 | 책임 |
|---|---|
| `src/director/loop.ts` (신규) | 겹치지 않는 주기 루프 — 한 틱을 끝까지 기다린 뒤 다음을 예약 |
| `src/director/assignment.ts` (신규) | 제안 1개 → 매치 생성·티켓 소비·룸 생성·유저 위치 갱신 |
| `src/director/tick.ts` (신규) | 한 틱: 풀 읽기 → 큐별 제안·평가 → 확정. 결과 요약 반환 |
| `src/director.ts` (신규) | Director 진입점 — 로더·루프·시그널·로그 |
| `src/services/ticketRequestValidation.ts` (신규) | 티켓 요청이 애초에 매칭 가능한지 판정하는 순수 함수 |
| `src/daos/match.dao.postgres.ts` (수정) | 매치+라운드+티켓소비를 한 트랜잭션으로 |
| `src/repositories/match.repository.ts` (수정) | `saveConsumingTickets` |
| `src/repositories/matchmakingTicket.repository.ts` (수정) | 캐시만 비우는 `evictFromCacheAllById` |
| `src/services/{match,matchmakingTicket}.service.ts` (수정) | 위 두 가지의 서비스 통로 |
| `src/services/matchmaking.service.ts` (수정) | 대기방 제거 + 검증 |
| `src/interfaces/{responseCode,user-location}.interface.ts` (수정) | 코드 추가/삭제, `waitingRoomId` 제거 |
| `src/main.ts` (수정) | 대기방 라우트 제거 |
| `waitingRoom.*` 14파일 + `updater/` 2파일 (삭제) | — |

### 그 외

| 저장소 | 파일 |
|---|---|
| `lop-backend/packages/database` | `prisma/schema.prisma` + 마이그레이션 (WaitingRoom 제거) |
| `lop-backend/apps/lobby-server` | 자가치유·매퍼·인터페이스 수정, 대기방 2파일 삭제 |
| `lop-backend/apps/room-server` | `responseCode`·`user-location` 인터페이스 정리 |
| `infrastructure/table` | `Datas/#Queue.xlsx` (Casual 10초) + `gen.sh` 산출물 |
| `infrastructure/k8s` | `matchmaking-director-deployment.yaml` + kustomization |
| `LeagueOfPhysical-MasterData-{Client,Server}` | 재생성된 `tbqueue.bytes` |

---

## Task 1: Director 루프 — 겹치지 않는 주기 실행

**Files:**
- Create: `apps/matchmaking-server/src/director/loop.ts`
- Test: `apps/matchmaking-server/src/director/__tests__/loop.test.ts`

**Interfaces:**
- Consumes: (없음)
- Produces: `startDirectorLoop(tick: () => Promise<void>, intervalMs: number, onError: (error: unknown) => void): DirectorLoop`, `interface DirectorLoop { stop(): void }`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`apps/matchmaking-server/src/director/__tests__/loop.test.ts`:

```typescript
import { startDirectorLoop } from '@src/director/loop';

describe('startDirectorLoop', () => {
    beforeEach(() => {
        jest.useFakeTimers();
    });

    afterEach(() => {
        jest.useRealTimers();
    });

    it('앞 틱이 끝나기 전에는 다음 틱을 시작하지 않는다', async () => {
        let finishTick: () => void = () => undefined;
        const tick = jest.fn(() => new Promise<void>(resolve => { finishTick = resolve; }));

        const loop = startDirectorLoop(tick, 1000, () => undefined);
        await Promise.resolve();

        expect(tick).toHaveBeenCalledTimes(1);

        //  주기의 다섯 배가 지나도, 첫 틱이 안 끝났으면 두 번째는 없다.
        await jest.advanceTimersByTimeAsync(5000);
        expect(tick).toHaveBeenCalledTimes(1);

        finishTick();
        await jest.advanceTimersByTimeAsync(1000);
        expect(tick).toHaveBeenCalledTimes(2);

        loop.stop();
    });

    it('stop 이후에는 더 이상 틱이 돌지 않는다', async () => {
        const tick = jest.fn(async () => undefined);

        const loop = startDirectorLoop(tick, 1000, () => undefined);
        await jest.advanceTimersByTimeAsync(1000);
        expect(tick).toHaveBeenCalledTimes(2);

        loop.stop();
        await jest.advanceTimersByTimeAsync(10000);
        expect(tick).toHaveBeenCalledTimes(2);
    });

    it('틱이 던져도 루프는 계속 돌고 에러를 알린다', async () => {
        const tick = jest.fn(async () => { throw new Error('boom'); });
        const onError = jest.fn();

        const loop = startDirectorLoop(tick, 1000, onError);
        await jest.advanceTimersByTimeAsync(1000);

        expect(onError).toHaveBeenCalled();
        expect(tick).toHaveBeenCalledTimes(2);

        loop.stop();
    });
});
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

Run: `pnpm --filter matchmaking-server test -- loop.test.ts`
Expected: FAIL — `Cannot find module '@src/director/loop'`

- [ ] **Step 3: 최소 구현**

`apps/matchmaking-server/src/director/loop.ts`:

```typescript
export interface DirectorLoop {
    stop(): void;
}

/**
 * 주기적으로 tick을 돌리되, 한 틱이 끝난 뒤에 다음 틱을 예약한다.
 *
 * setInterval을 쓰면 한 틱이 주기보다 오래 걸릴 때 다음 틱이 겹쳐 시작하고,
 * 매칭이 겹쳐 돌면 같은 티켓이 두 매치에 들어간다 — 이 시스템에서 가장 나쁜 고장이다.
 *
 * 한 틱이 실패해도 루프는 멈추지 않는다. 다음 틱이 같은 풀을 다시 보므로 대개 저절로 회복된다.
 */
export function startDirectorLoop(
    tick: () => Promise<void>,
    intervalMs: number,
    onError: (error: unknown) => void,
): DirectorLoop {
    let stopped = false;
    let timer: NodeJS.Timeout | undefined;

    const run = async (): Promise<void> => {
        try {
            await tick();
        } catch (error) {
            onError(error);
        }

        if (!stopped) {
            timer = setTimeout(run, intervalMs);
        }
    };

    void run();

    return {
        stop(): void {
            stopped = true;
            if (timer !== undefined) {
                clearTimeout(timer);
                timer = undefined;
            }
        },
    };
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `pnpm --filter matchmaking-server test -- loop.test.ts`
Expected: PASS (3 tests)

- [ ] **Step 5: 커밋**

```bash
git add apps/matchmaking-server/src/director/loop.ts apps/matchmaking-server/src/director/__tests__/loop.test.ts
git commit -m "feat(director): 겹치지 않는 주기 루프"
```

---

## Task 2: 매치 확정 트랜잭션 — 티켓을 같은 트랜잭션에서 소비

**Files:**
- Modify: `apps/matchmaking-server/src/daos/match.dao.postgres.ts`
- Modify: `apps/matchmaking-server/src/repositories/match.repository.ts`
- Modify: `apps/matchmaking-server/src/repositories/matchmakingTicket.repository.ts`
- Modify: `apps/matchmaking-server/src/services/match.service.ts`
- Modify: `apps/matchmaking-server/src/services/matchmakingTicket.service.ts`

**Interfaces:**
- Consumes: 기존 `MatchDaoPostgres.saveWithRounds(match, rounds)`
- Produces:
  - `MatchDaoPostgres.saveWithRounds(match: MatchEntity, rounds: MatchRoundEntity[], consumedTicketIds?: string[]): Promise<MatchEntity>`
  - `MatchRepository.saveConsumingTickets(match: Match, consumedTicketIds: string[]): Promise<Match>`
  - `MatchmakingTicketRepository.evictFromCacheAllById(ids: Iterable<string>): Promise<void>`
  - `MatchService.createMatchConsumingTickets(createMatchDto: CreateMatchDto, consumedTicketIds: string[]): Promise<Match>`
  - `MatchmakingTicketService.evictFromCacheAllById(ids: Iterable<string>): Promise<void>`

> **왜 트랜잭션인가:** "매치가 생겼다"와 "그 사람들이 대기 풀에서 빠졌다"가 어긋나면 다음 틱이 같은 사람을 또 매칭한다. 반대로 티켓만 지워지고 매치가 없으면 그 사람은 아무 데도 못 간다. 둘은 같은 DB라 한 트랜잭션으로 묶을 수 있다.
>
> **왜 캐시를 따로 비우나:** 티켓 삭제가 트랜잭션 안에서 일어나면 `MatchmakingTicketRepository`를 거치지 않아 **Redis 캐시에 티켓이 남는다**(TTL 300초). 그러면 로비 자가치유가 "티켓이 아직 있네"라고 잘못 판단한다. 그래서 **트랜잭션 *전에* 캐시를 비운다** — 트랜잭션이 실패해도 캐시는 다음 조회 때 DB에서 다시 채워지므로 안전하다(기존 `save`가 쓰는 순서와 같다).

- [ ] **Step 1: DAO에 티켓 소비를 추가한다**

`apps/matchmaking-server/src/daos/match.dao.postgres.ts`의 `saveWithRounds`를 통째로 아래로 교체:

```typescript
    /**
     * 매치 행 저장 + 라운드 전체 교체를 한 트랜잭션으로 묶는다.
     * 라운드 없는 매치는 존재해선 안 되므로, 둘 중 하나만 반영되고 끝나면 안 된다.
     *
     * consumedTicketIds를 주면 그 티켓 삭제까지 같은 트랜잭션에 넣는다 — "매치가 생겼다"와
     * "그 사람들이 대기 풀에서 빠졌다"가 어긋나면 다음 매칭 틱이 같은 사람을 또 매칭한다.
     */
    public async saveWithRounds(match: MatchEntity, rounds: MatchRoundEntity[], consumedTicketIds: string[] = []): Promise<MatchEntity> {
        try {
            const saveMatch = this.model.upsert({
                where: { id: match.id },
                update: match,
                create: match,
            });
            const clearRounds = this.prismaClient.matchRound.deleteMany({ where: { matchId: match.id } });
            const insertRounds = this.prismaClient.matchRound.createMany({ data: rounds });

            if (consumedTicketIds.length === 0) {
                const [savedMatch] = await this.prismaClient.$transaction([saveMatch, clearRounds, insertRounds]);
                return savedMatch;
            }

            const consumeTickets = this.prismaClient.matchmakingTicket.deleteMany({
                where: { id: { in: consumedTicketIds } },
            });
            const [savedMatch] = await this.prismaClient.$transaction([saveMatch, clearRounds, insertRounds, consumeTickets]);
            return savedMatch;
        } catch (error) {
            return Promise.reject(error);
        }
    }
```

- [ ] **Step 2: Repository에 통로를 낸다**

`apps/matchmaking-server/src/repositories/match.repository.ts`의 `save`를 아래 두 메서드로 교체 (`findById`는 그대로):

```typescript
    public async save(match: Match): Promise<Match> {
        return this.saveConsumingTickets(match, []);
    }

    /**
     * 매치를 만들면서 그 매치를 이룬 티켓을 대기 풀에서 함께 제거한다 — 한 트랜잭션.
     * 매치만 생기고 티켓이 남으면 다음 매칭 틱이 같은 사람을 또 매칭한다.
     */
    public async saveConsumingTickets(match: Match, consumedTicketIds: string[]): Promise<Match> {
        try {
            const matchEntity = this.mapper.toEntity(match);
            const roundEntities = this.roundMapper.toEntities(match.id, match.rounds);

            const savedEntity = await this.matchDao.saveWithRounds(matchEntity, roundEntities, consumedTicketIds);

            return { ...this.mapper.toDomain(savedEntity), rounds: match.rounds };
        } catch (error) {
            return Promise.reject(error);
        }
    }
```

같은 파일 상단의 클래스 주석에서 "아직 트랜잭션 밖인 것은 …(룸 생성, 유저 위치 갱신, 티켓 삭제)" 문장을 아래로 갱신한다 (티켓 삭제는 이제 트랜잭션 안이다):

```
 * 매치를 이룬 티켓의 삭제도 같은 트랜잭션에 들어간다 — 매치만 생기고 티켓이 남으면
 * 다음 매칭 틱이 같은 사람을 또 매칭하기 때문이다. 여전히 트랜잭션 밖인 것은 룸 생성과
 * 유저 위치 갱신인데, 그건 다른 서비스로 나가는 HTTP라 DB 트랜잭션으로 묶을 수 없다.
```

- [ ] **Step 3: 티켓 저장소에 캐시 무효화를 추가한다**

`apps/matchmaking-server/src/repositories/matchmakingTicket.repository.ts`의 클래스 본문에 추가:

```typescript
    /**
     * 캐시에서만 지운다.
     * 티켓 삭제가 DB 트랜잭션 안에서 일어날 때는 이 저장소를 거치지 않아 캐시에 티켓이 남는다.
     * 트랜잭션 *전에* 불러 무효화한다 — 트랜잭션이 실패해도 캐시는 다음 조회 때 DB에서 다시 채워진다.
     */
    public async evictFromCacheAllById(ids: Iterable<string>): Promise<void> {
        try {
            await this.cacheDao.deleteAllById(ids);
        } catch (error) {
            return Promise.reject(error);
        }
    }
```

- [ ] **Step 4: 서비스에 통로를 낸다**

`apps/matchmaking-server/src/services/match.service.ts`의 `createMatch` 아래에 추가:

```typescript
    /** 매치를 만들면서 그 매치를 이룬 티켓을 대기 풀에서 함께 제거한다 (한 트랜잭션). */
    public async createMatchConsumingTickets(createMatchDto: CreateMatchDto, consumedTicketIds: string[]): Promise<Match> {
        try {
            return await this.matchRepository.saveConsumingTickets(MatchMapper.CreateMatchDto.toEntity(createMatchDto), consumedTicketIds);
        } catch (error) {
            return Promise.reject(error);
        }
    }
```

`apps/matchmaking-server/src/services/matchmakingTicket.service.ts`의 클래스 본문에 추가:

```typescript
    public async evictFromCacheAllById(ids: Iterable<string>): Promise<void> {
        try {
            return await this.matchmakingTicketRepository.evictFromCacheAllById(ids);
        } catch (error) {
            return Promise.reject(error);
        }
    }
```

- [ ] **Step 5: 빌드와 기존 테스트 확인**

Run: `pnpm --filter matchmaking-server run build && pnpm --filter matchmaking-server test`
Expected: 빌드 성공, 기존 테스트 전부 PASS (9 suites)

> DB를 실제로 때리는 자동 테스트는 이 저장소에 없다. 트랜잭션 동작은 배포 전 수동 검증(계획 끝 "배포와 검증")에서 확인한다.

- [ ] **Step 6: 커밋**

```bash
git add apps/matchmaking-server/src/daos/match.dao.postgres.ts apps/matchmaking-server/src/repositories apps/matchmaking-server/src/services/match.service.ts apps/matchmaking-server/src/services/matchmakingTicket.service.ts
git commit -m "feat(match): 매치 생성과 티켓 소비를 한 트랜잭션으로"
```

---

## Task 3: Assignment — 제안 하나를 실제 매치로 확정

**Files:**
- Create: `apps/matchmaking-server/src/director/assignment.ts`
- Test: `apps/matchmaking-server/src/director/__tests__/assignment.test.ts`

**Interfaces:**
- Consumes: Task 2의 `MatchService.createMatchConsumingTickets`, `MatchmakingTicketService.evictFromCacheAllById`. 4a의 `selectMap(gameModeId, tickets, maps, random)`, `Proposal`, `MapOption`.
- Produces:
  ```typescript
  export interface AssignmentDeps {
      evictTicketsFromCache(ids: string[]): Promise<void>;
      createMatch(dto: CreateMatchDto, consumedTicketIds: string[]): Promise<{ id: string }>;
      createRoom(dto: CreateRoomDto): Promise<CreateRoomResponseDto>;
      updateUserLocation(dto: UpdateUserLocationDto): Promise<unknown>;
  }
  export interface AssignmentResult { matchId: string; roomId: string; gameModeId: number; mapId: number; playerIds: string[] }
  export function assignProposal(proposal: Proposal, tickets: MatchmakingTicket[], maps: MapOption[], random: () => number, deps: AssignmentDeps): Promise<AssignmentResult>
  ```

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`apps/matchmaking-server/src/director/__tests__/assignment.test.ts`:

```typescript
import { assignProposal, AssignmentDeps } from '@src/director/assignment';
import { Proposal, MapOption } from '@src/director/types';
import { MatchmakingTicket } from '@interfaces/matchmakingTicket.interface';
import { ResponseCode } from '@interfaces/responseCode.interface';

const MAPS: MapOption[] = [
    { id: 1, gameModeId: 1 },
    { id: 2, gameModeId: 1 },
];

function ticket(id: string, userIds: string[], rating: number, mapIds: number[] = []): MatchmakingTicket {
    return {
        id,
        userIds,
        queueId: 1,
        gameModeIds: [1],
        mapIds,
        rating,
        createdAt: new Date('2026-07-31T00:00:00Z'),
    } as MatchmakingTicket;
}

function proposal(ticketIds: string[]): Proposal {
    return {
        gameModeId: 1,
        queueId: 1,
        ticketIds,
        playerCount: ticketIds.length,
        oldestCreatedAt: new Date('2026-07-31T00:00:00Z'),
        ratingLower: 0,
        ratingUpper: 2000,
    };
}

function deps(overrides: Partial<AssignmentDeps> = {}): AssignmentDeps & { calls: string[] } {
    const calls: string[] = [];
    return {
        calls,
        evictTicketsFromCache: jest.fn(async () => { calls.push('evict'); }),
        createMatch: jest.fn(async () => { calls.push('createMatch'); return { id: 'match-1' }; }),
        createRoom: jest.fn(async () => { calls.push('createRoom'); return { code: ResponseCode.SUCCESS, room: { id: 'room-1' } } as any; }),
        updateUserLocation: jest.fn(async () => { calls.push('updateUserLocation'); return undefined; }),
        ...overrides,
    } as AssignmentDeps & { calls: string[] };
}

describe('assignProposal', () => {
    it('캐시 무효화 → 매치 생성 → 룸 생성 → 위치 갱신 순서로 진행한다', async () => {
        const d = deps();
        const tickets = [ticket('t1', ['u1'], 1000), ticket('t2', ['u2'], 1000)];

        const result = await assignProposal(proposal(['t1', 't2']), tickets, MAPS, () => 0, d);

        expect(d.calls).toEqual(['evict', 'createMatch', 'createRoom', 'updateUserLocation']);
        expect(result.matchId).toBe('match-1');
        expect(result.roomId).toBe('room-1');
        expect(result.playerIds).toEqual(['u1', 'u2']);
    });

    it('제안에 없는 티켓은 매치에 넣지 않는다', async () => {
        const d = deps();
        const tickets = [ticket('t1', ['u1'], 1000), ticket('t2', ['u2'], 1000), ticket('t3', ['u3'], 1000)];

        await assignProposal(proposal(['t1', 't3']), tickets, MAPS, () => 0, d);

        expect(d.createMatch).toHaveBeenCalledWith(
            expect.objectContaining({ playerList: ['u1', 'u3'] }),
            ['t1', 't3'],
        );
    });

    it('매치의 targetRating은 묶인 티켓 레이팅의 평균이다', async () => {
        const d = deps();
        const tickets = [ticket('t1', ['u1'], 1000), ticket('t2', ['u2'], 1300)];

        await assignProposal(proposal(['t1', 't2']), tickets, MAPS, () => 0, d);

        expect(d.createMatch).toHaveBeenCalledWith(
            expect.objectContaining({ targetRating: 1150 }),
            expect.anything(),
        );
    });

    it('맵은 묶인 티켓들의 희망 교집합에서 고른다', async () => {
        const d = deps();
        const tickets = [ticket('t1', ['u1'], 1000, [2]), ticket('t2', ['u2'], 1000, [2])];

        await assignProposal(proposal(['t1', 't2']), tickets, MAPS, () => 0, d);

        expect(d.createMatch).toHaveBeenCalledWith(
            expect.objectContaining({ rounds: [{ index: 0, gameModeId: 1, mapId: 2 }] }),
            expect.anything(),
        );
    });

    it('룸 생성이 실패하면 던진다 — 티켓은 이미 소비된 뒤라 되돌리지 않는다', async () => {
        const d = deps({
            createRoom: jest.fn(async () => ({ code: ResponseCode.UNKNOWN_ERROR }) as any),
        });
        const tickets = [ticket('t1', ['u1'], 1000)];

        await expect(assignProposal(proposal(['t1']), tickets, MAPS, () => 0, d)).rejects.toThrow(/room/i);
        expect(d.updateUserLocation).not.toHaveBeenCalled();
    });
});
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

Run: `pnpm --filter matchmaking-server test -- assignment.test.ts`
Expected: FAIL — `Cannot find module '@src/director/assignment'`

- [ ] **Step 3: 구현**

`apps/matchmaking-server/src/director/assignment.ts`:

```typescript
import { MatchmakingTicket } from '@interfaces/matchmakingTicket.interface';
import { ResponseCode } from '@interfaces/responseCode.interface';
import { Location, GameRoomLocationDetail } from '@interfaces/user-location.interface';
import { CreateMatchDto } from '@dtos/match.dto';
import { CreateRoomDto, CreateRoomResponseDto } from '@dtos/room.dto';
import { UpdateUserLocationDto } from '@dtos/user-location.dto';
import { Proposal, MapOption } from '@src/director/types';
import { selectMap } from '@src/director/selectMap';

export interface AssignmentDeps {
    evictTicketsFromCache(ids: string[]): Promise<void>;
    createMatch(dto: CreateMatchDto, consumedTicketIds: string[]): Promise<{ id: string }>;
    createRoom(dto: CreateRoomDto): Promise<CreateRoomResponseDto>;
    updateUserLocation(dto: UpdateUserLocationDto): Promise<unknown>;
}

export interface AssignmentResult {
    matchId: string;
    roomId: string;
    gameModeId: number;
    mapId: number;
    playerIds: string[];
}

/**
 * 제안 하나를 실제 매치로 확정한다.
 *
 * 순서가 곧 안전장치다. "티켓이 풀에서 빠졌다"와 "매치가 있다"만 한 트랜잭션으로 묶고,
 * 그 뒤의 룸 생성·위치 갱신은 다른 서비스로 나가는 HTTP라 함께 묶을 수 없다.
 * 뒤가 실패하면 유저는 이 매치를 잃지만, 티켓이 없으므로 로비 자가치유가 로비로 되돌린다.
 * 절대 일어나면 안 되는 것은 한 명이 두 매치에 들어가는 것이고, 그것만 트랜잭션이 막는다.
 */
export async function assignProposal(
    proposal: Proposal,
    tickets: MatchmakingTicket[],
    maps: MapOption[],
    random: () => number,
    deps: AssignmentDeps,
): Promise<AssignmentResult> {
    const ticketIds = new Set(proposal.ticketIds);
    const picked = tickets.filter(ticket => ticketIds.has(ticket.id));
    if (picked.length === 0) {
        throw new Error(`No tickets for proposal. gameModeId: ${proposal.gameModeId}`);
    }

    const playerIds = picked.flatMap(ticket => ticket.userIds);
    const mapId = selectMap(proposal.gameModeId, picked, maps, random);
    const targetRating = Math.round(picked.reduce((sum, ticket) => sum + ticket.rating, 0) / picked.length);

    const createMatchDto: CreateMatchDto = {
        queueId: proposal.queueId,
        targetRating: targetRating,
        playerList: playerIds,
        rounds: [{ index: 0, gameModeId: proposal.gameModeId, mapId: mapId }],
    };

    //  트랜잭션 안의 삭제는 저장소를 거치지 않아 캐시를 갱신하지 못한다 — 먼저 비운다.
    await deps.evictTicketsFromCache(proposal.ticketIds);

    const match = await deps.createMatch(createMatchDto, proposal.ticketIds);

    const createRoomResponse = await deps.createRoom({ matchId: match.id });
    if (createRoomResponse.code !== ResponseCode.SUCCESS || createRoomResponse.room === undefined) {
        throw new Error(`Failed to create room. matchId: ${match.id}, code: ${createRoomResponse.code}`);
    }

    const roomId = createRoomResponse.room.id;

    await deps.updateUserLocation({
        userLocations: playerIds.map(userId => ({
            userId: userId,
            location: Location.GameRoom,
            locationDetail: new GameRoomLocationDetail(Location.GameRoom, roomId),
        })),
    });

    return {
        matchId: match.id,
        roomId: roomId,
        gameModeId: proposal.gameModeId,
        mapId: mapId,
        playerIds: playerIds,
    };
}
```

> `UpdateUserLocationDto`는 `userLocations` 필드를 가진 클래스다. 위처럼 객체 리터럴로 만들 때 타입이 안 맞으면 `const dto = new UpdateUserLocationDto(); dto.userLocations = [...]` 형태로 바꾼다 (기존 `waitingRoom.service.ts`가 그 방식을 쓴다).

- [ ] **Step 4: 테스트 통과 확인**

Run: `pnpm --filter matchmaking-server test -- assignment.test.ts`
Expected: PASS (5 tests)

- [ ] **Step 5: 커밋**

```bash
git add apps/matchmaking-server/src/director/assignment.ts apps/matchmaking-server/src/director/__tests__/assignment.test.ts
git commit -m "feat(director): 제안을 매치·룸·위치로 확정"
```

---

## Task 4: Director 틱 + 진입점

**Files:**
- Create: `apps/matchmaking-server/src/director/tick.ts`
- Create: `apps/matchmaking-server/src/director.ts`
- Test: `apps/matchmaking-server/src/director/__tests__/tick.test.ts`

**Interfaces:**
- Consumes: 4a의 `proposeMatches(queue, gameModes, tickets, now)`, `evaluateProposals(proposals, random)`, `waitedSeconds(createdAt, now)`. Task 3의 `assignProposal`. Task 1의 `startDirectorLoop`.
- Produces:
  ```typescript
  export interface DirectorTickDeps {
      getTables(): Tables;
      findAllTickets(): Promise<MatchmakingTicket[]>;
      assign(proposal: Proposal, tickets: MatchmakingTicket[], maps: MapOption[]): Promise<AssignmentResult>;
      random(): number;
  }
  export interface PoolSummary { queueId: number; ticketCount: number; playerCount: number; oldestWaitSeconds: number; stalledTicketIds: string[] }
  export interface DirectorTickResult { assignments: AssignmentResult[]; failures: string[]; pools: PoolSummary[] }
  export function runDirectorTick(now: Date, deps: DirectorTickDeps): Promise<DirectorTickResult>
  ```

> **관측을 여기서 로그로 찍지 않고 결과로 돌려주는 이유:** 얼마나 자주 무엇을 남길지는 *조립하는 쪽*(`director.ts`)의 정책이고, 틱은 테스트 가능해야 한다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`apps/matchmaking-server/src/director/__tests__/tick.test.ts`:

```typescript
import { runDirectorTick, DirectorTickDeps } from '@src/director/tick';
import { MatchmakingTicket } from '@interfaces/matchmakingTicket.interface';

const NOW = new Date('2026-07-31T00:10:00Z');

//  실제 Tables 대신 같은 모양의 최소 스텁 — 마스터데이터 파일 없이 틱 로직만 본다.
function tables(maxWaitSeconds = 30) {
    const queue = {
        id: 1,
        ratingRangeStart: 500,
        ratingRangeMax: 2000,
        ratingRelaxPerSec: 50,
        maxWaitSeconds: maxWaitSeconds,
        allowedGameModeIds: [1],
    };
    const gameMode = { id: 1, minPlayers: 2, maxPlayers: 2 };
    const map = { id: 1, gameModeId: 1 };

    return {
        TbQueue: { getDataList: () => [queue], get: (id: number) => (id === 1 ? queue : undefined) },
        TbGameMode: { getDataList: () => [gameMode], get: (id: number) => (id === 1 ? gameMode : undefined) },
        TbMap: { getDataList: () => [map], get: (id: number) => (id === 1 ? map : undefined) },
    } as any;
}

function ticket(id: string, userId: string, secondsAgo: number, queueId = 1): MatchmakingTicket {
    return {
        id,
        userIds: [userId],
        queueId,
        gameModeIds: [1],
        mapIds: [1],
        rating: 1000,
        createdAt: new Date(NOW.getTime() - secondsAgo * 1000),
    } as MatchmakingTicket;
}

function deps(tickets: MatchmakingTicket[], overrides: Partial<DirectorTickDeps> = {}): DirectorTickDeps {
    return {
        getTables: () => tables(),
        findAllTickets: async () => tickets,
        assign: jest.fn(async (proposal) => ({
            matchId: `match-${proposal.ticketIds.join('-')}`,
            roomId: 'room-1',
            gameModeId: proposal.gameModeId,
            mapId: 1,
            playerIds: [],
        })),
        random: () => 0,
        ...overrides,
    };
}

describe('runDirectorTick', () => {
    it('정원이 찬 티켓들을 매치로 확정한다', async () => {
        const result = await runDirectorTick(NOW, deps([ticket('t1', 'u1', 5), ticket('t2', 'u2', 5)]));

        expect(result.assignments).toHaveLength(1);
        expect(result.failures).toHaveLength(0);
    });

    it('인원이 모자라면 아무것도 확정하지 않는다', async () => {
        const result = await runDirectorTick(NOW, deps([ticket('t1', 'u1', 0)]));

        expect(result.assignments).toHaveLength(0);
    });

    it('확정이 실패해도 던지지 않고 실패로 기록한다', async () => {
        const result = await runDirectorTick(NOW, deps(
            [ticket('t1', 'u1', 5), ticket('t2', 'u2', 5)],
            { assign: jest.fn(async () => { throw new Error('room down'); }) },
        ));

        expect(result.assignments).toHaveLength(0);
        expect(result.failures).toHaveLength(1);
        expect(result.failures[0]).toContain('room down');
    });

    it('다른 큐의 티켓은 서로 섞이지 않는다', async () => {
        const assign = jest.fn(async (proposal: any) => ({
            matchId: 'm', roomId: 'r', gameModeId: proposal.gameModeId, mapId: 1, playerIds: [],
        }));
        //  큐 2는 마스터데이터에 없으므로 그 티켓은 어떤 제안에도 들어가면 안 된다.
        await runDirectorTick(NOW, deps([ticket('t1', 'u1', 5), ticket('t2', 'u2', 5, 2)], { assign }));

        expect(assign).not.toHaveBeenCalled();
    });

    it('풀 요약에 대기 인원과 최장 대기 시간을 담는다', async () => {
        const result = await runDirectorTick(NOW, deps([ticket('t1', 'u1', 40)]));

        expect(result.pools).toEqual([
            expect.objectContaining({ queueId: 1, ticketCount: 1, playerCount: 1, oldestWaitSeconds: 40 }),
        ]);
    });

    it('최대 대기의 두 배를 넘긴 티켓을 정체로 표시한다', async () => {
        const result = await runDirectorTick(NOW, deps([ticket('t1', 'u1', 61)]));

        expect(result.pools[0].stalledTicketIds).toEqual(['t1']);
    });
});
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

Run: `pnpm --filter matchmaking-server test -- tick.test.ts`
Expected: FAIL — `Cannot find module '@src/director/tick'`

- [ ] **Step 3: 틱 구현**

`apps/matchmaking-server/src/director/tick.ts`:

```typescript
import { MatchmakingTicket } from '@interfaces/matchmakingTicket.interface';
import { Tables } from '@src/masterdata/schema';
import { Proposal, MapOption } from '@src/director/types';
import { proposeMatches } from '@src/director/matchFunction';
import { evaluateProposals } from '@src/director/evaluator';
import { waitedSeconds } from '@src/director/ratingRange';
import { AssignmentResult } from '@src/director/assignment';

/** 대기 풀이 어떤 상태인지 — 매치가 안 생기는 것과 대기자가 없는 것을 구분하기 위한 관측값. */
export interface PoolSummary {
    queueId: number;
    ticketCount: number;
    playerCount: number;
    oldestWaitSeconds: number;
    stalledTicketIds: string[];
}

export interface DirectorTickResult {
    assignments: AssignmentResult[];
    failures: string[];
    pools: PoolSummary[];
}

export interface DirectorTickDeps {
    getTables(): Tables;
    findAllTickets(): Promise<MatchmakingTicket[]>;
    assign(proposal: Proposal, tickets: MatchmakingTicket[], maps: MapOption[]): Promise<AssignmentResult>;
    random(): number;
}

/** 최대 대기의 몇 배를 넘기면 "아무와도 못 맞는 티켓"으로 의심할지. */
const STALLED_WAIT_MULTIPLIER = 2;

/**
 * 한 틱: 대기 풀 전체를 읽어 큐마다 제안을 만들고, 겹치는 제안을 정리한 뒤 확정한다.
 * 한 제안의 확정이 실패해도 나머지 제안은 계속 확정한다 — 룸 서버 한 번의 실패로 매칭 전체가 멈추면 안 된다.
 */
export async function runDirectorTick(now: Date, deps: DirectorTickDeps): Promise<DirectorTickResult> {
    const tables = deps.getTables();
    const tickets = await deps.findAllTickets();
    const maps: MapOption[] = tables.TbMap.getDataList();

    const assignments: AssignmentResult[] = [];
    const failures: string[] = [];
    const pools: PoolSummary[] = [];

    for (const queue of tables.TbQueue.getDataList()) {
        const queueTickets = tickets.filter(ticket => ticket.queueId === queue.id);
        if (queueTickets.length === 0) {
            continue;
        }

        pools.push(summarizePool(queue.id, queue.maxWaitSeconds, queueTickets, now));

        const gameModes = queue.allowedGameModeIds
            .map(id => tables.TbGameMode.get(id))
            .filter((gameMode): gameMode is NonNullable<typeof gameMode> => gameMode !== undefined);

        const proposals = proposeMatches(queue, gameModes, queueTickets, now);

        for (const proposal of evaluateProposals(proposals, deps.random)) {
            try {
                assignments.push(await deps.assign(proposal, queueTickets, maps));
            } catch (error) {
                failures.push(`gameModeId: ${proposal.gameModeId}, tickets: ${proposal.ticketIds.join(',')}, error: ${error}`);
            }
        }
    }

    return { assignments: assignments, failures: failures, pools: pools };
}

function summarizePool(queueId: number, maxWaitSeconds: number, tickets: MatchmakingTicket[], now: Date): PoolSummary {
    const waits = tickets.map(ticket => waitedSeconds(ticket.createdAt, now));

    return {
        queueId: queueId,
        ticketCount: tickets.length,
        playerCount: tickets.reduce((sum, ticket) => sum + ticket.userIds.length, 0),
        oldestWaitSeconds: Math.max(...waits),
        stalledTicketIds: tickets
            .filter((_, index) => waits[index] > maxWaitSeconds * STALLED_WAIT_MULTIPLIER)
            .map(ticket => ticket.id),
    };
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `pnpm --filter matchmaking-server test -- tick.test.ts`
Expected: PASS (6 tests)

- [ ] **Step 5: 진입점을 만든다**

`apps/matchmaking-server/src/director.ts`:

```typescript
import 'reflect-metadata';
import validateEnv from '@utils/validateEnv';
import { logger } from '@utils/logger';
import loader from '@loaders/index';
import { getTables } from '@loaders/masterdata.loader';
import { startDirectorLoop } from '@src/director/loop';
import { runDirectorTick, DirectorTickResult } from '@src/director/tick';
import { assignProposal } from '@src/director/assignment';
import MatchService from '@services/match.service';
import MatchmakingTicketService from '@services/matchmakingTicket.service';
import RoomService from '@services/room.service';
import UserLocationService from '@services/user-location.service';

const TICK_INTERVAL_MS = 1000;
/** 풀 상태를 몇 틱마다 남길지 — 매 틱 남기면 로그가 대기 인원 수만큼 불어난다. */
const POOL_REPORT_EVERY_TICKS = 30;

(async () => {
    try {
        validateEnv();

        await loader();

        const matchService = new MatchService();
        const matchmakingTicketService = new MatchmakingTicketService();
        const roomService = new RoomService();
        const userLocationService = new UserLocationService();

        let tickCount = 0;

        const loop = startDirectorLoop(
            async () => {
                const result = await runDirectorTick(new Date(), {
                    getTables: getTables,
                    findAllTickets: () => matchmakingTicketService.findAllMatchmakingTickets(),
                    assign: (proposal, tickets, maps) => assignProposal(proposal, tickets, maps, Math.random, {
                        evictTicketsFromCache: ids => matchmakingTicketService.evictFromCacheAllById(ids),
                        createMatch: (dto, consumedTicketIds) => matchService.createMatchConsumingTickets(dto, consumedTicketIds),
                        createRoom: dto => roomService.createRoom(dto),
                        updateUserLocation: dto => userLocationService.updateUserLocation(dto),
                    }),
                    random: Math.random,
                });

                report(result, tickCount);
                tickCount += 1;
            },
            TICK_INTERVAL_MS,
            error => logger.error(`director tick failed. error: ${error}`),
        );

        logger.info(`✌️ Director started. interval: ${TICK_INTERVAL_MS}ms`);

        const shutdown = (signal: string) => {
            logger.info(`Director stopping. signal: ${signal}`);
            loop.stop();
        };
        process.on('SIGTERM', () => shutdown('SIGTERM'));
        process.on('SIGINT', () => shutdown('SIGINT'));
    } catch (error) {
        logger.error(`director main error. error: ${error}`);
    }
})();

function report(result: DirectorTickResult, tickCount: number): void {
    for (const assignment of result.assignments) {
        logger.info(`[director] match created. matchId: ${assignment.matchId}, roomId: ${assignment.roomId}, gameModeId: ${assignment.gameModeId}, mapId: ${assignment.mapId}, players: ${assignment.playerIds.length}`);
    }

    for (const failure of result.failures) {
        logger.error(`[director] failed to assign. ${failure}`);
    }

    if (tickCount % POOL_REPORT_EVERY_TICKS !== 0) {
        return;
    }

    for (const pool of result.pools) {
        logger.info(`[director] pool. queueId: ${pool.queueId}, tickets: ${pool.ticketCount}, players: ${pool.playerCount}, oldestWait: ${pool.oldestWaitSeconds}s`);

        if (pool.stalledTicketIds.length > 0) {
            logger.warn(`[director] tickets waiting too long — no proposal includes them. queueId: ${pool.queueId}, ticketIds: ${pool.stalledTicketIds.join(',')}`);
        }
    }
}
```

- [ ] **Step 6: 빌드 확인 — `dist/director.js`가 나오는지**

Run: `pnpm --filter matchmaking-server run build && ls apps/matchmaking-server/dist/director.js`
Expected: 빌드 성공 + 파일 존재

- [ ] **Step 7: 전체 테스트**

Run: `pnpm --filter matchmaking-server test`
Expected: 전부 PASS

- [ ] **Step 8: 커밋**

```bash
git add apps/matchmaking-server/src/director/tick.ts apps/matchmaking-server/src/director/__tests__/tick.test.ts apps/matchmaking-server/src/director.ts
git commit -m "feat(director): 틱 조립과 진입점"
```

---

## Task 5: 요청 경로 전환 — 대기방 제거 + 티켓 검증

**Files:**
- Create: `apps/matchmaking-server/src/services/ticketRequestValidation.ts`
- Test: `apps/matchmaking-server/src/services/__tests__/ticketRequestValidation.test.ts`
- Modify: `apps/matchmaking-server/src/interfaces/responseCode.interface.ts`
- Modify: `apps/matchmaking-server/src/interfaces/user-location.interface.ts`
- Modify: `apps/matchmaking-server/src/services/matchmaking.service.ts`

**Interfaces:**
- Consumes: `getTables()` from `@loaders/masterdata.loader`
- Produces: `validateTicketRequest(tables: Tables, queueId: number, gameModeIds: number[], mapIds: number[], playerCount: number): number` — `ResponseCode.SUCCESS` 또는 거절 코드

- [ ] **Step 1: 응답 코드를 추가한다**

`apps/matchmaking-server/src/interfaces/responseCode.interface.ts`의 MatchMaking 영역에 추가 (기존 줄은 그대로):

```typescript
    public static readonly INVALID_QUEUE = 10102;
    public static readonly INVALID_GAME_MODE = 10103;
    public static readonly INVALID_MAP = 10104;
    public static readonly PARTY_TOO_LARGE = 10105;
```

- [ ] **Step 2: 실패하는 테스트를 쓴다**

`apps/matchmaking-server/src/services/__tests__/ticketRequestValidation.test.ts`:

```typescript
import { validateTicketRequest } from '@services/ticketRequestValidation';
import { ResponseCode } from '@interfaces/responseCode.interface';

//  실제 Tables 대신 같은 모양의 최소 스텁 — 판정 규칙만 본다.
const TABLES = {
    TbQueue: { get: (id: number) => (id === 1 ? { id: 1, allowedGameModeIds: [1, 2] } : undefined) },
    TbGameMode: {
        get: (id: number) => {
            if (id === 1) return { id: 1, minPlayers: 2, maxPlayers: 8 };
            if (id === 2) return { id: 2, minPlayers: 2, maxPlayers: 2 };
            return undefined;
        },
    },
    TbMap: {
        get: (id: number) => {
            if (id === 10) return { id: 10, gameModeId: 1 };
            if (id === 20) return { id: 20, gameModeId: 2 };
            return undefined;
        },
    },
} as any;

describe('validateTicketRequest', () => {
    it('정상 요청은 통과한다', () => {
        expect(validateTicketRequest(TABLES, 1, [1], [10], 1)).toBe(ResponseCode.SUCCESS);
    });

    it('게임·맵을 안 고른 요청(랜덤)도 통과한다', () => {
        expect(validateTicketRequest(TABLES, 1, [], [], 1)).toBe(ResponseCode.SUCCESS);
    });

    it('없는 큐는 거절한다', () => {
        expect(validateTicketRequest(TABLES, 99, [1], [10], 1)).toBe(ResponseCode.INVALID_QUEUE);
    });

    it('큐가 허락하지 않는 게임은 거절한다', () => {
        expect(validateTicketRequest(TABLES, 1, [3], [], 1)).toBe(ResponseCode.INVALID_GAME_MODE);
    });

    it('그 게임의 맵이 아니면 거절한다', () => {
        expect(validateTicketRequest(TABLES, 1, [1], [20], 1)).toBe(ResponseCode.INVALID_MAP);
    });

    it('없는 맵은 거절한다', () => {
        expect(validateTicketRequest(TABLES, 1, [1], [99], 1)).toBe(ResponseCode.INVALID_MAP);
    });

    it('게임을 안 골랐는데 맵만 고른 요청은 거절한다', () => {
        expect(validateTicketRequest(TABLES, 1, [], [10], 1)).toBe(ResponseCode.INVALID_MAP);
    });

    it('어떤 후보 게임에도 안 들어가는 인원이면 거절한다', () => {
        //  게임 2는 정원 2명이고, 게임을 2로 지정했으므로 3명은 절대 못 들어간다.
        expect(validateTicketRequest(TABLES, 1, [2], [], 3)).toBe(ResponseCode.PARTY_TOO_LARGE);
    });

    it('후보 중 하나라도 담을 수 있으면 통과한다', () => {
        expect(validateTicketRequest(TABLES, 1, [1, 2], [], 3)).toBe(ResponseCode.SUCCESS);
    });

    it('게임을 안 골랐으면 큐의 허용 게임 전체로 인원을 따진다', () => {
        expect(validateTicketRequest(TABLES, 1, [], [], 8)).toBe(ResponseCode.SUCCESS);
        expect(validateTicketRequest(TABLES, 1, [], [], 9)).toBe(ResponseCode.PARTY_TOO_LARGE);
    });
});
```

- [ ] **Step 3: 테스트가 실패하는지 확인**

Run: `pnpm --filter matchmaking-server test -- ticketRequestValidation.test.ts`
Expected: FAIL — `Cannot find module '@services/ticketRequestValidation'`

- [ ] **Step 4: 구현**

`apps/matchmaking-server/src/services/ticketRequestValidation.ts`:

```typescript
import { Tables } from '@src/masterdata/schema';
import { ResponseCode } from '@interfaces/responseCode.interface';

/**
 * 티켓이 애초에 매칭될 수 있는 요청인지 본다.
 * 여기서 안 걸러내면 잘못된 요청도 티켓이 되어 **조용히 영원히 대기**한다 —
 * 유저는 취소할 때까지 아무 일도 안 일어나는 화면을 본다.
 *
 * 게임·맵 후보가 비어 있는 것은 "아무거나"라는 뜻이라 정상이다(랜덤 선택).
 */
export function validateTicketRequest(
    tables: Tables,
    queueId: number,
    gameModeIds: number[],
    mapIds: number[],
    playerCount: number,
): number {
    const queue = tables.TbQueue.get(queueId);
    if (queue === undefined) {
        return ResponseCode.INVALID_QUEUE;
    }

    for (const gameModeId of gameModeIds) {
        if (!queue.allowedGameModeIds.includes(gameModeId)) {
            return ResponseCode.INVALID_GAME_MODE;
        }
        if (tables.TbGameMode.get(gameModeId) === undefined) {
            return ResponseCode.INVALID_GAME_MODE;
        }
    }

    //  맵은 게임에 딸린 선택이라, 게임을 안 정했으면 맵도 정할 수 없다.
    if (mapIds.length > 0 && gameModeIds.length === 0) {
        return ResponseCode.INVALID_MAP;
    }

    for (const mapId of mapIds) {
        const map = tables.TbMap.get(mapId);
        if (map === undefined || !gameModeIds.includes(map.gameModeId)) {
            return ResponseCode.INVALID_MAP;
        }
    }

    //  후보 게임 중 하나라도 이 인원을 담을 수 있어야 한다. 하나도 없으면 영원히 못 맞는다.
    const candidateGameModeIds = gameModeIds.length > 0 ? gameModeIds : queue.allowedGameModeIds;
    const fits = candidateGameModeIds.some(gameModeId => {
        const gameMode = tables.TbGameMode.get(gameModeId);
        return gameMode !== undefined && playerCount <= gameMode.maxPlayers;
    });
    if (!fits) {
        return ResponseCode.PARTY_TOO_LARGE;
    }

    return ResponseCode.SUCCESS;
}
```

- [ ] **Step 5: 테스트 통과 확인**

Run: `pnpm --filter matchmaking-server test -- ticketRequestValidation.test.ts`
Expected: PASS (10 tests)

- [ ] **Step 6: `WaitingRoomLocationDetail`에서 `waitingRoomId`를 뺀다**

`apps/matchmaking-server/src/interfaces/user-location.interface.ts`의 `WaitingRoomLocationDetail`을 교체:

```typescript
/**
 * 매칭 풀에서 기다리는 중. (클래스·enum 이름은 슬라이스 5에서 Matchmaking으로 개명한다.)
 * 대기방이 없어졌으므로 이제 티켓 하나가 곧 대기 상태다.
 */
export class WaitingRoomLocationDetail extends LocationDetail {
    matchmakingTicketId: string;

    public constructor(location: Location, matchmakingTicketId: string) {
        super(location);

        this.matchmakingTicketId = matchmakingTicketId;
    }
}
```

- [ ] **Step 7: 요청 경로에서 대기방을 걷어낸다**

`apps/matchmaking-server/src/services/matchmaking.service.ts`:

1. `import WaitingRoomService from '@services/waitingRoom.service';` 줄 삭제, 필드 `private waitingRoomService ...` 삭제
2. import 추가:
```typescript
import { getTables } from '@loaders/masterdata.loader';
import { validateTicketRequest } from '@services/ticketRequestValidation';
```
3. `requestMatchmaking`에서 **`//  트랜잭션으로 묶어서 처리해야 할 것 같은데...` 주석 줄부터
   `if (waitingRoom) { ... } else { ... }` 블록 끝까지**(즉 `} catch (error) {` 직전까지)를 아래로 교체한다.
   `const targetRating = ...` 줄은 남기고 그 아래부터다:

```typescript
            const validationCode = validateTicketRequest(
                getTables(),
                requestMatchmakingDto.queueId,
                [requestMatchmakingDto.gameModeId],
                [requestMatchmakingDto.mapId],
                1,
            );
            if (validationCode !== ResponseCode.SUCCESS) {
                return {
                    code: validationCode,
                };
            }

            //  요청 하나에는 게임모드·맵이 각각 하나뿐이라, 후보 목록도 원소 하나짜리로 저장한다.
            const matchmakingTicket = await this.matchmakingTicketService.issueMatchmakingTicket(
                [requestMatchmakingDto.userId],
                requestMatchmakingDto.queueId,
                [requestMatchmakingDto.gameModeId],
                [requestMatchmakingDto.mapId],
                targetRating
            );

            //  티켓을 만드는 것으로 끝이다 — 매칭은 Director가 풀 전체를 보고 따로 돈다.
            const updateUserLocationDto: UpdateUserLocationDto = {
                userLocations: [{
                    userId: requestMatchmakingDto.userId,
                    location: Location.WaitingRoom,
                    locationDetail: new WaitingRoomLocationDetail(Location.WaitingRoom, matchmakingTicket.id),
                }]
            };

            await this.userLocationService.updateUserLocation(updateUserLocationDto);

            return {
                code: ResponseCode.SUCCESS,
                ticketId: matchmakingTicket.id
            };
```

4. `cancelMatchmaking`에서 `leaveWaitingRoom` 블록을 삭제한다. 즉 아래 부분을

```typescript
            const waitingRoomLocationDetail = userLocation.locationDetail as WaitingRoomLocationDetail;

            const result = await this.waitingRoomService.leaveWaitingRoom(waitingRoomLocationDetail.waitingRoomId, waitingRoomLocationDetail.matchmakingTicketId);
            if (result === false) {
                return {
                    code: ResponseCode.FAIL_TO_LEAVE_WAITING_ROOM
                };
            }
            const ticket = await this.matchmakingTicketService.deleteMatchmakingTicketById(ticketId);
```

아래로 바꾼다:

```typescript
            await this.matchmakingTicketService.deleteMatchmakingTicketById(ticketId);
```

5. 더 이상 쓰이지 않는 import(`WaitingRoomLocationDetail`은 여전히 씀)를 정리한다.

- [ ] **Step 8: 빌드 + 전체 테스트**

Run: `pnpm --filter matchmaking-server run build && pnpm --filter matchmaking-server test`
Expected: 빌드 성공(대기방 파일들은 아직 남아 있으므로 컴파일됨), 테스트 전부 PASS

- [ ] **Step 9: 커밋**

```bash
git add apps/matchmaking-server/src
git commit -m "feat(matchmaking): 요청은 티켓만 만든다 + 티켓 요청 검증"
```

---

## Task 6: `WaitingRoom` 삭제 (매칭 서버) + 마이그레이션

**Files:**
- Delete (14): `src/controllers/waitingRoom.controller.ts`, `src/daos/waitingRoom.dao.{mongoose,postgres,redis}.ts`, `src/dtos/waitingRoom.dto.ts`, `src/factories/waitingRoom.factory.ts`, `src/interfaces/waitingRoom.interface.ts`, `src/mappers/controllers/waitingRoom.mapper.ts`, `src/mappers/entities/waitingRoom.mapper.ts`, `src/models/waitingRoom.model.ts`, `src/repositories/waitingRoom.repository.ts`, `src/routes/waitingRoom.route.ts`, `src/services/waitingRoom.service.ts`, `src/updater/waitingRoomUpdater.ts`
- Delete (2): `src/updater/updater.ts`, `src/interfaces/updatable.interface.ts` (지우고 나면 `src/updater/` 폴더는 빈다)
- Modify: `src/main.ts`, `src/interfaces/responseCode.interface.ts`
- Modify: `packages/database/prisma/schema.prisma` + 새 마이그레이션

**Interfaces:**
- Consumes: Task 5가 마지막 호출자를 제거했다
- Produces: (없음 — 삭제만)

> `Updatable`을 참조하는 파일은 `updater.ts`와 `waitingRoomUpdater.ts` 둘뿐이며 둘 다 이 태스크에서 지운다 (착수 시점 확인 완료).

- [ ] **Step 1: 아무도 안 쓰는지 먼저 확인한다**

Run:
```bash
grep -rn "waitingRoom\|WaitingRoom" apps/matchmaking-server/src \
  | grep -v "^apps/matchmaking-server/src/controllers/waitingRoom" \
  | grep -v "^apps/matchmaking-server/src/daos/waitingRoom" \
  | grep -v "^apps/matchmaking-server/src/dtos/waitingRoom" \
  | grep -v "^apps/matchmaking-server/src/factories/waitingRoom" \
  | grep -v "^apps/matchmaking-server/src/interfaces/waitingRoom" \
  | grep -v "^apps/matchmaking-server/src/mappers" \
  | grep -v "^apps/matchmaking-server/src/models/waitingRoom" \
  | grep -v "^apps/matchmaking-server/src/repositories/waitingRoom" \
  | grep -v "^apps/matchmaking-server/src/routes/waitingRoom" \
  | grep -v "^apps/matchmaking-server/src/services/waitingRoom" \
  | grep -v "^apps/matchmaking-server/src/updater"
```
Expected: `main.ts`(라우트 등록), `responseCode.interface.ts`, `user-location.interface.ts`(클래스 이름 `WaitingRoomLocationDetail` — **유지**), `matchmaking.service.ts`(`WaitingRoomLocationDetail`·`Location.WaitingRoom` — **유지**)만 남는다. 그 밖에 실제 대기방 *기능*을 쓰는 곳이 나오면 멈추고 보고한다.

- [ ] **Step 2: 파일을 지운다**

```bash
git rm apps/matchmaking-server/src/controllers/waitingRoom.controller.ts \
       apps/matchmaking-server/src/daos/waitingRoom.dao.mongoose.ts \
       apps/matchmaking-server/src/daos/waitingRoom.dao.postgres.ts \
       apps/matchmaking-server/src/daos/waitingRoom.dao.redis.ts \
       apps/matchmaking-server/src/dtos/waitingRoom.dto.ts \
       apps/matchmaking-server/src/factories/waitingRoom.factory.ts \
       apps/matchmaking-server/src/interfaces/waitingRoom.interface.ts \
       apps/matchmaking-server/src/mappers/controllers/waitingRoom.mapper.ts \
       apps/matchmaking-server/src/mappers/entities/waitingRoom.mapper.ts \
       apps/matchmaking-server/src/models/waitingRoom.model.ts \
       apps/matchmaking-server/src/repositories/waitingRoom.repository.ts \
       apps/matchmaking-server/src/routes/waitingRoom.route.ts \
       apps/matchmaking-server/src/services/waitingRoom.service.ts \
       apps/matchmaking-server/src/updater/waitingRoomUpdater.ts \
       apps/matchmaking-server/src/updater/updater.ts
```

`Updatable` 인터페이스 파일도 다른 사용처가 없으면 함께 지운다 (Step 1의 grep으로 확인).

- [ ] **Step 3: `main.ts`에서 라우트를 뺀다**

`apps/matchmaking-server/src/main.ts`에서 `import WaitingRoomRoute ...` 줄을 지우고, `new App([...])` 인자에서 `new WaitingRoomRoute(), `를 뺀다.

- [ ] **Step 4: 응답 코드를 정리한다**

`apps/matchmaking-server/src/interfaces/responseCode.interface.ts`에서 아래 `//#region WaitingRoom` 블록 전체(주석 포함)를 삭제:

```typescript
    //#region WaitingRoom
    public static readonly WAITING_ROOM_NOT_EXIST = 40000;
    public static readonly FAIL_TO_LEAVE_WAITING_ROOM = 40001;
    //#endregion
```

- [ ] **Step 5: 스키마에서 `WaitingRoom`을 없앤다**

`packages/database/prisma/schema.prisma`에서 `model WaitingRoom { ... }`과 `enum WaitingRoomStatus { ... }` 블록을 삭제한다.

- [ ] **Step 6: 마이그레이션을 만든다**

Run (repo 루트에서):
```bash
pnpm --filter @lop/database exec prisma migrate dev --name drop_waiting_room --create-only
```
Expected: `packages/database/prisma/migrations/<timestamp>_drop_waiting_room/migration.sql` 생성. 내용이 `DROP TABLE "WaitingRoom"` + `DROP TYPE "WaitingRoomStatus"`인지 확인한다.

> `--create-only`로 만들고 **적용은 하지 않는다.** 실제 적용은 배포 시 ArgoCD PreSync Job이 한다.
> 이 마이그레이션은 되돌릴 수 없다(`DROP`에 down이 없다) — 앞으로만 롤한다.

- [ ] **Step 7: 빌드 + 테스트**

Run: `pnpm --filter @lop/database run generate && pnpm --filter matchmaking-server run build && pnpm --filter matchmaking-server test`
Expected: 전부 성공. 남은 대기방 참조가 있으면 여기서 컴파일 에러로 드러난다.

- [ ] **Step 8: 커밋**

```bash
git add -A apps/matchmaking-server packages/database
git commit -m "refactor(matchmaking): WaitingRoom과 Updater 삭제"
```

---

## Task 7: 로비·룸 서버 전환 — 자가치유를 티켓으로

**Files:**
- Modify: `apps/lobby-server/src/services/user-location.service.ts`
- Modify: `apps/lobby-server/src/mappers/entities/user.location.mapper.ts`
- Modify: `apps/lobby-server/src/interfaces/user-location.interface.ts`
- Modify: `apps/lobby-server/src/interfaces/responseCode.interface.ts`
- Modify: `apps/lobby-server/src/services/httpServices/matchmakingServer.service.ts`
- Delete: `apps/lobby-server/src/services/waitingRoom.service.ts`, `apps/lobby-server/src/interfaces/waitingRoom.interface.ts`
- Modify: `apps/room-server/src/interfaces/responseCode.interface.ts`, `apps/room-server/src/interfaces/user-location.interface.ts`

**Interfaces:**
- Consumes: 매칭 서버의 티켓 조회 HTTP (`MatchmakingTicketService.findMatchmakingTicketById` — 기존 그대로)
- Produces: (없음)

- [ ] **Step 1: 자가치유를 티켓 존재만 보게 바꾼다**

`apps/lobby-server/src/services/user-location.service.ts`의 `verifyUserLocation` 안 `case Location.WaitingRoom:` 블록 전체를 교체:

```typescript
                case Location.WaitingRoom:
                    //  대기방이 없어졌으므로 대기 상태의 근거는 티켓 하나뿐이다.
                    //  티켓이 사라졌다는 건 매칭됐거나(그럼 곧 GameRoom으로 갱신된다) 취소됐다는 뜻이라 로비로 돌린다.
                    const matchmakingLocationDetail = userLocation.locationDetail as WaitingRoomLocationDetail;
                    const matchmakingTicket = await this.matchmakingTicketService.findMatchmakingTicketById(matchmakingLocationDetail.matchmakingTicketId);
                    if (!matchmakingTicket) {
                        userLocation.location = Location.None;
                        userLocation.locationDetail = {
                            location: Location.None
                        }
                    }
                    break;
```

같은 파일에서 `import WaitingRoomService ...`와 `private waitingRoomService = new WaitingRoomService();`를 삭제한다.

- [ ] **Step 2: 판별자를 티켓 id로 바꾼다**

`apps/lobby-server/src/mappers/entities/user.location.mapper.ts`의 `toDomain`에서:

```typescript
        if (parsedDetail.waitingRoomId !== undefined) {
            locationDetail = new WaitingRoomLocationDetail(
                location,
                parsedDetail.waitingRoomId,
                parsedDetail.matchmakingTicketId
            );
        } else if (parsedDetail.gameRoomId !== undefined) {
```

를 아래로 교체:

```typescript
        if (parsedDetail.matchmakingTicketId !== undefined) {
            locationDetail = new WaitingRoomLocationDetail(
                location,
                parsedDetail.matchmakingTicketId
            );
        } else if (parsedDetail.gameRoomId !== undefined) {
```

- [ ] **Step 3: 로비의 `WaitingRoomLocationDetail`에서 `waitingRoomId`를 뺀다**

`apps/lobby-server/src/interfaces/user-location.interface.ts`의 `WaitingRoomLocationDetail`을 Task 5 Step 6과 **같은 모양**으로 바꾼다 (필드 `matchmakingTicketId`만, 생성자 인자 2개, 같은 주석).

- [ ] **Step 4: 대기방 코드를 지운다**

```bash
git rm apps/lobby-server/src/services/waitingRoom.service.ts apps/lobby-server/src/interfaces/waitingRoom.interface.ts
```

`apps/lobby-server/src/services/httpServices/matchmakingServer.service.ts`에서 `findWaitingRoomById` 메서드와 `import { WaitingRoom } ...` 줄을 삭제한다.

`apps/lobby-server/src/interfaces/responseCode.interface.ts`와 `apps/room-server/src/interfaces/responseCode.interface.ts`에서 `WAITING_ROOM_NOT_EXIST` / `FAIL_TO_LEAVE_WAITING_ROOM` 항목(과 그 `//#region WaitingRoom` 블록)을 삭제한다.

`apps/room-server/src/interfaces/user-location.interface.ts`의 `WaitingRoomLocationDetail`에도 같은 변경(필드 제거)을 적용한다. **단** 룸 서버가 그 필드를 읽는 곳이 있으면 멈추고 보고한다 (`grep -rn "waitingRoomId" apps/room-server/src`로 확인).

- [ ] **Step 5: 남은 참조 확인**

Run:
```bash
grep -rn "waitingRoomId" apps/ packages/ ; grep -rln "waitingRoom.service\|waitingRoom.interface" apps/
```
Expected: 출력 없음 (`Location.WaitingRoom`·`WaitingRoomLocationDetail`는 남아 있어야 정상 — 이름 개명은 슬라이스 5)

- [ ] **Step 6: 빌드**

Run: `pnpm --filter lobby-server run build && pnpm --filter room-server run build && pnpm --filter matchmaking-server run build`
Expected: 전부 성공

- [ ] **Step 7: 커밋**

```bash
git add -A apps/lobby-server apps/room-server
git commit -m "refactor(lobby): 자가치유를 티켓 존재로 + 대기방 코드 삭제"
```

---

## Task 8: 마스터데이터 — Casual 최대 대기 30초 → 10초

**Files:**
- Modify: `infrastructure/table/Datas/#Queue.xlsx` (실제 파일명은 `ls infrastructure/table/Datas`로 확인)
- Regenerate: `lop-backend/apps/matchmaking-server/master_data/tbqueue.json`, `LeagueOfPhysical-MasterData-Client/Runtime.Generated/StreamingAssets/MasterData/tbqueue.bytes`, `LeagueOfPhysical-MasterData-Server/…/tbqueue.bytes`

**Interfaces:**
- Consumes: Task 4의 Director가 `queue.maxWaitSeconds`를 읽는다
- Produces: 데이터 값 변경만

> **왜 10초인가:** 필요 인원이 최대 대기에 걸쳐 정원→최소로 선형 감소한다(spec §6-2 정정). 정원 8·최소 2·10초면 0초에 8명, 2.5초에 6명, 5초에 4명, 7.5초에 3명, 10초에 2명. **최소 10초를 기다린다는 뜻이 아니다** — 사람이 모이면 그 전에 출발한다. Ranked 60초는 그대로 둔다.

- [ ] **Step 1: 엑셀 값을 바꾼다**

`infrastructure/table/Datas/#Queue.xlsx`의 `Casual` 행 `max_wait_seconds`를 `30` → `10`으로 바꾼다.
파일이 바이너리라 손으로 열지 말고 아래 스크립트로 바꾼다 (`openpyxl` 3.1.5 설치돼 있음).
헤더 행은 `##var`(1행)이고 데이터는 5행부터다 — 위치를 하드코딩하지 말고 이름으로 찾는다.

Run (`infrastructure/table/Datas`에서):

```bash
python -c "
import openpyxl
path = '#Queue.xlsx'
wb = openpyxl.load_workbook(path)
ws = wb.worksheets[0]

header = [cell.value for cell in ws[1]]
col = header.index('max_wait_seconds') + 1
code_col = header.index('code') + 1

changed = False
for row in range(2, ws.max_row + 1):
    if ws.cell(row=row, column=code_col).value == 'Casual':
        cell = ws.cell(row=row, column=col)
        #  원래 값이 문자열로 들어 있으면 문자열로 되돌려 쓴다 (Luban이 둘 다 읽지만 diff를 최소로).
        cell.value = '10' if isinstance(cell.value, str) else 10
        changed = True

if not changed:
    raise SystemExit('Casual row not found')

wb.save(path)
print('ok')
"
```
Expected: `ok`

- [ ] **Step 2: 재생성**

Run (`infrastructure/table`에서):
```bash
./gen.sh
```
Expected: client / server / matchmaking 세 타깃 모두 `[gen]` 로그 후 `[done]`

- [ ] **Step 3: 값이 실제로 바뀌었는지 확인**

Run:
```bash
grep -n "max_wait_seconds" /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server/master_data/tbqueue.json
```
Expected: 첫 행(Casual)이 `10`, 두 번째(Ranked)가 `60`

- [ ] **Step 4: 커밋 — 4개 저장소**

`gen.sh`는 산출물 폴더를 `rm -rf` 후 다시 만든다. Unity 패키지 두 곳은 **Unity가 `.meta`를 다시 만들 때까지 기다린 뒤** 커밋한다 (에디터를 켜 두었다면 자동 재스캔, 아니면 다음 실행 시). `.meta`가 빠지면 에셋 참조가 깨진다.

```bash
# infrastructure
cd /c/Users/re5na/workspace/LOP/infrastructure && git add table/Datas && git commit -m "data(queue): Casual 최대 대기 30초 -> 10초"

# lop-backend (feature 브랜치 위에서)
cd /c/Users/re5na/workspace/LOP/lop-backend && git add apps/matchmaking-server/master_data apps/matchmaking-server/src/masterdata && git commit -m "data(queue): Casual 최대 대기 30초 -> 10초 (재생성)"

# MasterData-Client / MasterData-Server (각 저장소 main에서 — 데이터 재생성뿐이라 브랜치 불필요)
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client && git add -A Runtime.Generated && git commit -m "data(queue): Casual 최대 대기 30초 -> 10초 (재생성)"
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server && git add -A Runtime.Generated && git commit -m "data(queue): Casual 최대 대기 30초 -> 10초 (재생성)"
```

- [ ] **Step 5: 재생성이 코드까지 바꿨는지 확인**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && git diff HEAD~1 --stat -- apps/matchmaking-server/src/masterdata`
Expected: 변경 없음(스키마는 그대로여야 한다). 스키마가 바뀌었다면 엑셀에서 값 말고 다른 것을 건드린 것이므로 되돌린다.

---

## Task 9: k8s — Director Deployment

**Files:**
- Create: `infrastructure/k8s/apps/backend/matchmaking-server/matchmaking-director-deployment.yaml`
- Modify: `infrastructure/k8s/apps/backend/matchmaking-server/kustomization.yaml`

**Interfaces:**
- Consumes: Task 4가 만든 `dist/director.js`
- Produces: `matchmaking-director` Deployment

> **왜 같은 kustomization 안인가:** `images:` 변환기가 이미지 *이름*(`re5nardo/matchmaking-server`)으로 태그를 갈아 끼운다. Director가 같은 이미지 이름을 쓰므로 **CI의 태그 bump가 자동으로 함께 적용**되고, API 서버와 Director가 항상 같은 커밋으로 돈다.

- [ ] **Step 1: Deployment를 만든다**

`infrastructure/k8s/apps/backend/matchmaking-server/matchmaking-director-deployment.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: matchmaking-director
spec:
  replicas: 1
  # 롤링 업데이트는 새 파드를 띄운 뒤 옛 파드를 내린다 = 두 Director가 잠시 함께 돈다.
  # 매칭 루프가 겹쳐 돌면 같은 사람이 두 매치에 들어가므로, 먼저 내리고 띄운다.
  strategy:
    type: Recreate
  selector:
    matchLabels:
      app: matchmaking-director
  template:
    metadata:
      labels:
        app: matchmaking-director
    spec:
      containers:
      - name: matchmaking-director
        image: re5nardo/matchmaking-server:latest
        # 매칭 서버와 같은 이미지의 두 번째 진입점 — 스키마 불일치가 구조적으로 불가능하다.
        command: ["node", "dist/director.js"]
        envFrom:
        - configMapRef:
            name: matchmaking-server-config
        - secretRef:
            name: postgres-secret
```

> Service는 만들지 않는다 — Director는 아무도 호출하지 않는다.

- [ ] **Step 2: kustomization에 등록한다**

`infrastructure/k8s/apps/backend/matchmaking-server/kustomization.yaml`의 `resources:` 목록에 추가:

```yaml
- matchmaking-director-deployment.yaml
```

- [ ] **Step 3: 렌더 결과를 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/infrastructure && kubectl kustomize k8s/apps/backend/matchmaking-server | grep -A2 "name: matchmaking-director\|image:"
```
Expected: `matchmaking-director` Deployment가 나오고, 이미지 태그가 `matchmaking-server`와 **같은 태그**로 치환돼 있다

- [ ] **Step 4: 커밋**

```bash
git add k8s/apps/backend/matchmaking-server && git commit -m "feat(k8s): matchmaking-director Deployment (replica 1, Recreate)"
```

---

## 배포와 검증 (사람이 수행 — 서브에이전트 아님)

> 이 절은 태스크가 아니다. 모든 태스크와 최종 리뷰가 끝난 뒤 **컨트롤러가 직접** 수행한다.

### 배포 전 조건 확인

- [ ] `WaitingRoom` 테이블 0행 (매칭 중인 유저가 없어야 마이그레이션이 안전하다)
- [ ] Redis 티켓 키 0개 (`matchmakingTicket:*`) — 옛 모양 캐시가 남으면 새 코드가 오해한다
- [ ] 유저 위치가 `WaitingRoom`인 행 0개 — 있으면 `None`으로 정리

### 배포

1. `lop-backend` feature 브랜치를 main에 `--no-ff` 머지 후 push
2. GitHub Actions `backend-deploy`를 **`app: all`로 한 번** 실행 (matchmaking / lobby / room / db-migrate 모두 이번에 바뀜)
3. 태그 bump 커밋이 infrastructure main에 들어갔는지 확인
4. `infrastructure` feature 브랜치(k8s Director + 마스터데이터)를 main에 머지 후 push
5. kind 클러스터라면 새 이미지를 미리 로드 (`kind load docker-image ... --name lop`)
6. ArgoCD 동기 확인: `matchmaking-server`, `matchmaking-director` 두 Deployment가 Running

### E2E

- [ ] `kubectl logs deploy/matchmaking-director`에 `Director started`가 보인다
- [ ] 클라 1대로 매칭 요청 → 티켓만 생기고 대기 (`매칭 중` 화면 유지)
- [ ] 클라 2대 매칭 → 매치 생성 → 게임 입장 → 게임 진행
- [ ] Director 로그에 `match created` 한 줄, 30틱마다 `pool` 한 줄
- [ ] 매칭 취소가 정상 동작 (티켓 삭제 → 로비)
- [ ] DB에 `Match` 1행 + `MatchRound` 1행 + `MatchmakingTicket` 0행 (트랜잭션이 함께 반영됐다는 증거)
- [ ] Director 파드를 강제로 재시작해도 다음 매칭이 정상 (무상태 확인)
