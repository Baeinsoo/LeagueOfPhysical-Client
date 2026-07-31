# 매치메이킹 슬라이스 4a — 매칭 알고리즘 순수 함수 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 풀 기반 매칭의 판단 로직을 **순수 함수 다섯 개**로 신설하고 테스트로 못박는다 — 다음 슬라이스(4b)의 Director가 이 함수들을 호출해 대기방 방식을 대체한다.

**Architecture:** 전부 `apps/matchmaking-server/src/director/` 안의 순수 함수다. DB도, 시계도, 난수원도 직접 만지지 않고 전부 인자로 받는다(`now`, `random`). 그래서 테스트가 픽스처만으로 돌고, 4b는 이 함수들을 조립하기만 하면 된다. **배선이 없으므로 동작 변화가 0이다** — 기존 대기방 경로가 그대로 돈다.

**Tech Stack:** TypeScript, jest(ts-jest), pnpm + turbo 모노레포.

## Global Constraints

- **설계 원천:** `docs/superpowers/specs/2026-07-27-matchmaking-standardization-design.md` — §6(매칭), §9(검증), 그리고 §8의 "슬라이스 4 분할", "§6-2 정정", "맵 결정" 절.
- **저장소는 `Baeinsoo/lop-backend` 하나, 앱은 `apps/matchmaking-server` 하나.** 다른 앱·다른 저장소·마스터데이터(Excel)·k8s 매니페스트는 **한 줄도 건드리지 않는다.**
- **배선하지 않는다.** 기존 `waitingRoom.service.ts` / `matchmaking.service.ts` / `Updater`는 그대로 둔다. 이 슬라이스가 만든 함수를 부르는 곳은 테스트뿐이다.
- **요구 인원 공식(정정된 §6-2):** `필요인원(대기초) = max(최소, ceil(정원 − (정원−최소) × 대기초 / 최대대기))`
- **레이팅 폭 공식:** `폭 = min(시작폭 + 초당확장 × 대기초, 폭최대)`, 구간은 `[rating − 폭, rating + 폭]`
- **"서로 겹친다"의 정의:** 묶인 티켓들의 구간이 **모두** 겹친다 = `max(하한) ≤ min(상한)`
- **대기 시간의 기준은 "가장 오래 기다린 티켓"이다** (FlexMatch처럼 새 티켓이 붙을 때 리셋하지 않는다 — 한 사람의 대기에 상한을 보장하기 위해).
- **Evaluator 선택 규칙:** 인원 큰 제안 우선 → 오래 기다린 티켓이 낀 쪽 → 그래도 동률이면 **무작위**.
- **import 별칭은 이미 있는 `@src/...`만 쓴다.** `@director/...` 같은 새 별칭을 만들지 않는다 — 별칭은 `tsconfig.json`과 `jest.config.js` 두 곳에 손으로 복제돼 있어서, 한쪽만 고치면 조용히 깨진다.
- **브랜치:** `feature/matchmaking-slice4a-algorithm`. main 직접 커밋 금지.
- **커밋 메시지는 한국어**로 쓰고 아래 trailer를 붙인다:
  `Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>`
- **주석 컨벤션:** 한국어, 비자명한 *의도(왜)* 만 짧게. 코드로 자명한 것은 주석 없이 둔다. **아직 없는 기능을 주석에 쓰지 않는다.**

---

## File Structure

전부 신설이다. 기존 파일은 하나도 수정하지 않는다.

| 파일 | 책임 |
|---|---|
| `src/director/types.ts` | 이 폴더가 주고받는 타입 — 좁은 입력 타입과 제안(`Proposal`) |
| `src/director/ratingRange.ts` | 티켓 하나의 레이팅 허용 구간 (시간에 따라 넓어짐) |
| `src/director/requiredPlayerCount.ts` | 지금 몇 명이면 출발해도 되는가 (시간에 따라 줄어듦) |
| `src/director/matchFunction.ts` | 게임마다 후보를 모아 제안을 만든다 |
| `src/director/evaluator.ts` | 겹치는 제안 중 무엇을 채택할지 고른다 |
| `src/director/selectMap.ts` | 확정된 게임에서 맵 하나를 고른다 |
| `src/director/__tests__/*.test.ts` | 각 함수의 테스트 |

### 왜 마스터데이터 클래스를 직접 받지 않는가

Luban이 만든 `Queue`/`GameMode`/`GameMap` 클래스는 생성자가 snake_case JSON을 요구해서 테스트에서 만들기 번거롭다. 그래서 이 폴더는 **필요한 필드만 선언한 좁은 타입**을 받는다. TypeScript는 구조적 타이핑이라 **4b는 Luban 객체를 그대로 넘기면 되고**(필드 이름·타입이 이미 일치한다) 어댑터가 필요 없다. 테스트는 평범한 객체 리터럴을 쓴다.

---

## Task 1: 타입 + 시간에 따라 변하는 값 두 개

**Files:**
- Create: `lop-backend/apps/matchmaking-server/src/director/types.ts`
- Create: `lop-backend/apps/matchmaking-server/src/director/ratingRange.ts`
- Create: `lop-backend/apps/matchmaking-server/src/director/requiredPlayerCount.ts`
- Create: `lop-backend/apps/matchmaking-server/src/director/__tests__/ratingRange.test.ts`
- Create: `lop-backend/apps/matchmaking-server/src/director/__tests__/requiredPlayerCount.test.ts`

**Interfaces:**
- Consumes: 기존 도메인 타입 `MatchmakingTicket`(`@interfaces/matchmakingTicket.interface`) — `{ id: string; userIds: string[]; queueId: number; gameModeIds: number[]; mapIds: number[]; rating: number; createdAt: Date }`
- Produces:
  - `interface QueuePolicy { ratingRangeStart: number; ratingRangeMax: number; ratingRelaxPerSec: number; maxWaitSeconds: number }`
  - `interface GameModeCapacity { id: number; minPlayers: number; maxPlayers: number }`
  - `interface MapOption { id: number; gameModeId: number }`
  - `interface RatingRange { lower: number; upper: number }`
  - `interface Proposal { gameModeId: number; ticketIds: string[]; playerCount: number; oldestCreatedAt: Date }`
  - `waitedSeconds(createdAt: Date, now: Date): number`
  - `computeRatingRange(ticket: MatchmakingTicket, queue: QueuePolicy, now: Date): RatingRange`
  - `requiredPlayerCount(gameMode: GameModeCapacity, queue: QueuePolicy, waitedSec: number): number`

- [ ] **Step 1: 브랜치 생성**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git checkout main
git pull --ff-only origin main
git checkout -b feature/matchmaking-slice4a-algorithm
```

- [ ] **Step 2: 실패하는 테스트 작성 — 레이팅 구간**

`src/director/__tests__/ratingRange.test.ts` 생성:

```typescript
import { computeRatingRange, waitedSeconds } from '@src/director/ratingRange';
import { QueuePolicy } from '@src/director/types';
import { MatchmakingTicket } from '@interfaces/matchmakingTicket.interface';

const casual: QueuePolicy = {
    ratingRangeStart: 500,
    ratingRangeMax: 2000,
    ratingRelaxPerSec: 50,
    maxWaitSeconds: 10,
};

function ticketAt(createdAt: string, rating = 1000): MatchmakingTicket {
    return {
        id: 't',
        userIds: ['u'],
        queueId: 1,
        gameModeIds: [1],
        mapIds: [1],
        rating: rating,
        createdAt: new Date(createdAt),
    };
}

describe('waitedSeconds', () => {
    it('경과 초를 준다', () => {
        expect(waitedSeconds(new Date('2026-07-31T00:00:00Z'), new Date('2026-07-31T00:00:07Z'))).toBe(7);
    });

    it('시계가 거꾸로 가도 음수를 주지 않는다', () => {
        expect(waitedSeconds(new Date('2026-07-31T00:00:10Z'), new Date('2026-07-31T00:00:00Z'))).toBe(0);
    });
});

describe('computeRatingRange', () => {
    const now = new Date('2026-07-31T00:00:00Z');

    it('막 만든 티켓은 시작 폭만 갖는다', () => {
        const range = computeRatingRange(ticketAt('2026-07-31T00:00:00Z'), casual, now);

        expect(range).toEqual({ lower: 500, upper: 1500 });
    });

    it('기다린 만큼 폭이 넓어진다', () => {
        // 4초 대기 → 500 + 50*4 = 700
        const range = computeRatingRange(ticketAt('2026-07-30T23:59:56Z'), casual, now);

        expect(range).toEqual({ lower: 300, upper: 1700 });
    });

    it('아무리 기다려도 폭 최대를 넘지 않는다', () => {
        // 1시간 대기 → 500 + 50*3600 이지만 상한 2000
        const range = computeRatingRange(ticketAt('2026-07-30T23:00:00Z'), casual, now);

        expect(range).toEqual({ lower: -1000, upper: 3000 });
    });

    it('레이팅이 다르면 구간도 따라 움직인다', () => {
        const range = computeRatingRange(ticketAt('2026-07-31T00:00:00Z', 1800), casual, now);

        expect(range).toEqual({ lower: 1300, upper: 2300 });
    });
});
```

- [ ] **Step 3: 테스트가 실패하는지 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
pnpm test -- ratingRange
```
기대: FAIL — `Cannot find module '@src/director/ratingRange'`

- [ ] **Step 4: 타입 파일 작성**

`src/director/types.ts` 생성:

```typescript
/**
 * 이 폴더는 Luban이 만든 마스터데이터 클래스를 직접 받지 않고 필요한 필드만 선언한다.
 * 구조적 타이핑 덕에 호출부는 Luban 객체를 그대로 넘길 수 있고, 테스트는 평범한 객체를 쓴다.
 */
export interface QueuePolicy {
    ratingRangeStart: number;
    ratingRangeMax: number;
    ratingRelaxPerSec: number;
    maxWaitSeconds: number;
}

export interface GameModeCapacity {
    id: number;
    minPlayers: number;
    maxPlayers: number;
}

export interface MapOption {
    id: number;
    gameModeId: number;
}

export interface RatingRange {
    lower: number;
    upper: number;
}

/** 이 게임으로 이 티켓들을 묶어 한 판을 만들자는 제안. */
export interface Proposal {
    gameModeId: number;
    ticketIds: string[];
    playerCount: number;
    oldestCreatedAt: Date;
}
```

- [ ] **Step 5: 레이팅 구간 구현**

`src/director/ratingRange.ts` 생성:

```typescript
import { MatchmakingTicket } from '@interfaces/matchmakingTicket.interface';
import { QueuePolicy, RatingRange } from '@src/director/types';

/** 시계가 어긋나 음수가 나오면 0으로 본다 — 기다린 적 없는 것과 같다. */
export function waitedSeconds(createdAt: Date, now: Date): number {
    return Math.max(0, (now.getTime() - createdAt.getTime()) / 1000);
}

/** 오래 기다릴수록 받아들이는 실력 폭이 넓어진다. */
export function computeRatingRange(ticket: MatchmakingTicket, queue: QueuePolicy, now: Date): RatingRange {
    const waited = waitedSeconds(ticket.createdAt, now);
    const width = Math.min(queue.ratingRangeStart + queue.ratingRelaxPerSec * waited, queue.ratingRangeMax);

    return {
        lower: ticket.rating - width,
        upper: ticket.rating + width,
    };
}
```

- [ ] **Step 6: 테스트 통과 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
pnpm test -- ratingRange
```
기대: PASS (6 tests)

- [ ] **Step 7: 실패하는 테스트 작성 — 요구 인원**

`src/director/__tests__/requiredPlayerCount.test.ts` 생성:

```typescript
import { requiredPlayerCount } from '@src/director/requiredPlayerCount';
import { GameModeCapacity, QueuePolicy } from '@src/director/types';

const queue: QueuePolicy = {
    ratingRangeStart: 500,
    ratingRangeMax: 2000,
    ratingRelaxPerSec: 50,
    maxWaitSeconds: 30,
};

const eightPlayer: GameModeCapacity = { id: 1, minPlayers: 2, maxPlayers: 8 };

describe('requiredPlayerCount', () => {
    it('기다리지 않았으면 정원을 다 채워야 한다', () => {
        expect(requiredPlayerCount(eightPlayer, queue, 0)).toBe(8);
    });

    it('기다릴수록 요구 인원이 줄어든다', () => {
        expect(requiredPlayerCount(eightPlayer, queue, 10)).toBe(6);
        expect(requiredPlayerCount(eightPlayer, queue, 20)).toBe(4);
    });

    it('최대 대기에 이르면 최소 인원이 된다', () => {
        expect(requiredPlayerCount(eightPlayer, queue, 30)).toBe(2);
    });

    it('최대 대기를 넘겨도 최소 인원 밑으로는 안 내려간다', () => {
        expect(requiredPlayerCount(eightPlayer, queue, 300)).toBe(2);
    });

    it('중간값은 올림한다 — 요구를 덜 낮추는 쪽이 안전하다', () => {
        // 8 - 6*(5/30) = 7.0
        expect(requiredPlayerCount(eightPlayer, queue, 5)).toBe(7);
        // 8 - 6*(7/30) = 6.6 → 7
        expect(requiredPlayerCount(eightPlayer, queue, 7)).toBe(7);
    });

    it('정원과 최소가 같은 게임은 항상 그 수다', () => {
        const duel: GameModeCapacity = { id: 2, minPlayers: 2, maxPlayers: 2 };

        expect(requiredPlayerCount(duel, queue, 0)).toBe(2);
        expect(requiredPlayerCount(duel, queue, 30)).toBe(2);
    });

    it('최대 대기가 0이면 곧바로 최소 인원이다', () => {
        const instant: QueuePolicy = { ...queue, maxWaitSeconds: 0 };

        expect(requiredPlayerCount(eightPlayer, instant, 0)).toBe(2);
    });
});
```

- [ ] **Step 8: 테스트가 실패하는지 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
pnpm test -- requiredPlayerCount
```
기대: FAIL — `Cannot find module '@src/director/requiredPlayerCount'`

- [ ] **Step 9: 요구 인원 구현**

`src/director/requiredPlayerCount.ts` 생성:

```typescript
import { GameModeCapacity, QueuePolicy } from '@src/director/types';

/**
 * 처음엔 정원을 다 채우길 바라다가, 기다린 만큼 요구를 낮춰 최대 대기에 이르면 최소 인원으로 출발한다.
 * 계단이 아니라 직선인 이유는 지금 있는 두 값(정원, 최대 대기)만으로 같은 효과가 나기 때문이다.
 */
export function requiredPlayerCount(gameMode: GameModeCapacity, queue: QueuePolicy, waitedSec: number): number {
    if (queue.maxWaitSeconds <= 0) {
        return gameMode.minPlayers;
    }

    const span = gameMode.maxPlayers - gameMode.minPlayers;
    const progress = Math.min(1, Math.max(0, waitedSec / queue.maxWaitSeconds));

    return Math.max(gameMode.minPlayers, Math.ceil(gameMode.maxPlayers - span * progress));
}
```

- [ ] **Step 10: 테스트 통과 확인 + 전체 스위트**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
pnpm test -- requiredPlayerCount
pnpm test
```
기대: 첫 명령 PASS (7 tests). 두 번째는 5 suites / 29 tests PASS (기존 16 + 신규 13).

- [ ] **Step 11: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git add apps/matchmaking-server/src/director
git commit -m "$(cat <<'EOF'
feat(director): 시간에 따라 완화되는 두 값 — 레이팅 폭, 요구 인원

기다릴수록 받아들이는 실력 폭은 넓어지고 요구 인원은 줄어든다.
폭 완화는 원래 spec에 있었고, 인원 완화는 이번에 추가한 것이다 —
그 전 규칙은 계단이 하나뿐이라 7명이 모여도 최대 대기를 꽉 기다렸다.

마스터데이터 클래스를 직접 받지 않고 필요한 필드만 선언한 타입을 받는다.
구조적 타이핑이라 호출부는 Luban 객체를 그대로 넘길 수 있고 테스트는
평범한 객체를 쓴다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: 제안 만들기 (MatchFunction)

**Files:**
- Create: `lop-backend/apps/matchmaking-server/src/director/matchFunction.ts`
- Create: `lop-backend/apps/matchmaking-server/src/director/__tests__/matchFunction.test.ts`

**Interfaces:**
- Consumes: Task 1의 `QueuePolicy`, `GameModeCapacity`, `Proposal`, `computeRatingRange`, `waitedSeconds`, `requiredPlayerCount`.
- Produces: `proposeMatches(queue: QueuePolicy, gameModes: GameModeCapacity[], tickets: MatchmakingTicket[], now: Date): Proposal[]`
  — 게임마다 최대 한 개의 제안을 만들고, 만들지 못한 게임은 결과에 넣지 않는다.

**규칙 (이 태스크가 구현하는 것):**
1. 게임 하나를 가정하고, 그 게임을 **허용하는** 티켓만 본다 — `gameModeIds`가 비었거나(제한 없음) 그 게임 id를 포함.
2. 오래 기다린 티켓부터 훑으며 묶는다. 티켓을 넣었을 때 **묶음 전체의 레이팅 구간이 여전히 겹치면**(`max(하한) ≤ min(상한)`) 넣고, 아니면 건너뛴다.
3. 정원을 넘기게 되는 티켓도 건너뛴다(파티가 2명인데 자리가 1개 남은 경우). 뒤에 더 작은 티켓이 있으면 그건 들어갈 수 있다.
4. 묶음의 인원이 `requiredPlayerCount(게임, 큐, 가장 오래 기다린 티켓의 대기초)` 이상이면 제안을 만든다.

- [ ] **Step 1: 실패하는 테스트 작성**

`src/director/__tests__/matchFunction.test.ts` 생성:

```typescript
import { proposeMatches } from '@src/director/matchFunction';
import { GameModeCapacity, QueuePolicy } from '@src/director/types';
import { MatchmakingTicket } from '@interfaces/matchmakingTicket.interface';

const NOW = new Date('2026-07-31T00:01:00Z');

/** 폭이 넓어 레이팅이 매칭을 방해하지 않는 큐 — 인원 규칙에만 집중하려고. */
const wideQueue: QueuePolicy = {
    ratingRangeStart: 10000,
    ratingRangeMax: 10000,
    ratingRelaxPerSec: 0,
    maxWaitSeconds: 30,
};

const flapWang: GameModeCapacity = { id: 1, minPlayers: 2, maxPlayers: 8 };
const dodgeball: GameModeCapacity = { id: 2, minPlayers: 2, maxPlayers: 8 };
const duel: GameModeCapacity = { id: 3, minPlayers: 2, maxPlayers: 2 };

/** waitedSec 초 전에 만들어진 티켓. */
function ticket(id: string, waitedSec: number, gameModeIds: number[], rating = 1000, userIds = [id]): MatchmakingTicket {
    return {
        id: id,
        userIds: userIds,
        queueId: 1,
        gameModeIds: gameModeIds,
        mapIds: [],
        rating: rating,
        createdAt: new Date(NOW.getTime() - waitedSec * 1000),
    };
}

describe('proposeMatches — 후보 목록 해석', () => {
    it('빈 후보 목록은 모든 허용 게임에 낀다', () => {
        const tickets = [ticket('a', 30, []), ticket('b', 30, [])];

        const proposals = proposeMatches(wideQueue, [flapWang, dodgeball], tickets, NOW);

        expect(proposals.map(p => p.gameModeId).sort()).toEqual([1, 2]);
        expect(proposals[0].ticketIds.sort()).toEqual(['a', 'b']);
    });

    it('지정한 게임에만 낀다', () => {
        const tickets = [ticket('a', 30, [1]), ticket('b', 30, [1])];

        const proposals = proposeMatches(wideQueue, [flapWang, dodgeball], tickets, NOW);

        expect(proposals).toHaveLength(1);
        expect(proposals[0].gameModeId).toBe(1);
    });

    it('랜덤 유저와 지정 유저가 같은 제안에 묶인다', () => {
        const tickets = [ticket('random', 30, []), ticket('picked', 30, [2])];

        const proposals = proposeMatches(wideQueue, [flapWang, dodgeball], tickets, NOW);

        const dodgeballProposal = proposals.find(p => p.gameModeId === 2);
        expect(dodgeballProposal?.ticketIds.sort()).toEqual(['picked', 'random']);
    });

    it('서로 다른 게임만 지정한 둘은 묶이지 않는다', () => {
        const tickets = [ticket('a', 30, [1]), ticket('b', 30, [2])];

        const proposals = proposeMatches(wideQueue, [flapWang, dodgeball], tickets, NOW);

        expect(proposals).toHaveLength(0);
    });
});

describe('proposeMatches — 인원', () => {
    it('인원이 모자라면 제안이 없다', () => {
        const tickets = [ticket('a', 30, [1])];

        expect(proposeMatches(wideQueue, [flapWang], tickets, NOW)).toHaveLength(0);
    });

    it('정원이 차면 기다리지 않고 제안한다', () => {
        const tickets = Array.from({ length: 8 }, (_, i) => ticket(`t${i}`, 0, [1]));

        const proposals = proposeMatches(wideQueue, [flapWang], tickets, NOW);

        expect(proposals).toHaveLength(1);
        expect(proposals[0].playerCount).toBe(8);
    });

    it('정원에 못 미쳐도 충분히 기다렸으면 제안한다', () => {
        // 20초 대기 → 요구 4명
        const tickets = Array.from({ length: 4 }, (_, i) => ticket(`t${i}`, 20, [1]));

        expect(proposeMatches(wideQueue, [flapWang], tickets, NOW)).toHaveLength(1);
    });

    it('아직 덜 기다렸으면 인원이 있어도 제안하지 않는다', () => {
        // 5초 대기 → 요구 7명인데 4명뿐
        const tickets = Array.from({ length: 4 }, (_, i) => ticket(`t${i}`, 5, [1]));

        expect(proposeMatches(wideQueue, [flapWang], tickets, NOW)).toHaveLength(0);
    });

    it('정원을 넘겨 담지 않는다', () => {
        const tickets = Array.from({ length: 12 }, (_, i) => ticket(`t${i}`, 30, [1]));

        const proposals = proposeMatches(wideQueue, [flapWang], tickets, NOW);

        expect(proposals[0].playerCount).toBe(8);
        expect(proposals[0].ticketIds).toHaveLength(8);
    });

    it('정원이 다른 게임이 섞여 있어도 각자 자기 정원으로 판단한다', () => {
        // 2명, 30초 대기 — 1v1(정원 2)은 성립하고 8인 게임도 최소 2명이라 성립
        const tickets = [ticket('a', 30, []), ticket('b', 30, [])];

        const proposals = proposeMatches(wideQueue, [duel, flapWang], tickets, NOW);

        expect(proposals.map(p => p.gameModeId).sort()).toEqual([1, 3]);
        expect(proposals.find(p => p.gameModeId === 3)?.playerCount).toBe(2);
    });

    it('1v1 게임은 세 번째 티켓을 담지 않는다', () => {
        const tickets = [ticket('a', 30, [3]), ticket('b', 30, [3]), ticket('c', 30, [3])];

        const proposals = proposeMatches(wideQueue, [duel], tickets, NOW);

        expect(proposals[0].ticketIds).toEqual(['a', 'b']);
    });

    it('파티는 인원 수만큼 센다', () => {
        const tickets = [ticket('party', 30, [1], 1000, ['u1', 'u2'])];

        // 2명짜리 파티 하나로 최소 인원 충족
        const proposals = proposeMatches(wideQueue, [flapWang], tickets, NOW);

        expect(proposals[0].playerCount).toBe(2);
    });

    it('자리가 모자라는 파티는 건너뛰고 뒤의 작은 티켓을 담는다', () => {
        const tickets = [
            ticket('a', 30, [3]),
            ticket('bigParty', 29, [3], 1000, ['u1', 'u2']),
            ticket('solo', 28, [3]),
        ];

        // 정원 2 — a 다음에 2인 파티는 못 들어가고 solo가 들어간다
        const proposals = proposeMatches(wideQueue, [duel], tickets, NOW);

        expect(proposals[0].ticketIds).toEqual(['a', 'solo']);
    });
});

describe('proposeMatches — 레이팅', () => {
    const tightQueue: QueuePolicy = {
        ratingRangeStart: 100,
        ratingRangeMax: 100,
        ratingRelaxPerSec: 0,
        maxWaitSeconds: 30,
    };

    it('구간이 겹치지 않는 티켓은 묶이지 않는다', () => {
        const tickets = [ticket('low', 30, [1], 1000), ticket('high', 30, [1], 5000)];

        expect(proposeMatches(tightQueue, [flapWang], tickets, NOW)).toHaveLength(0);
    });

    it('묶음 전체가 겹쳐야 한다 — 둘씩만 겹치는 셋은 다 담기지 않는다', () => {
        // 1000, 1150, 1300 — 폭 100이라 이웃끼리만 겹친다
        const tickets = [
            ticket('a', 30, [1], 1000),
            ticket('b', 30, [1], 1150),
            ticket('c', 30, [1], 1300),
        ];

        const proposals = proposeMatches(tightQueue, [flapWang], tickets, NOW);

        expect(proposals[0].ticketIds).toEqual(['a', 'b']);
    });

    it('오래 기다린 티켓의 넓어진 폭이 매칭을 성사시킨다', () => {
        const relaxing: QueuePolicy = {
            ratingRangeStart: 100,
            ratingRangeMax: 2000,
            ratingRelaxPerSec: 100,
            maxWaitSeconds: 30,
        };
        // 30초 기다린 1000점은 폭 2000까지 열려 3000점과 겹친다
        const tickets = [ticket('waited', 30, [1], 1000), ticket('fresh', 30, [1], 3000)];

        expect(proposeMatches(relaxing, [flapWang], tickets, NOW)).toHaveLength(1);
    });
});

describe('proposeMatches — 제안의 내용', () => {
    it('가장 오래 기다린 티켓의 시각을 담는다', () => {
        const tickets = [ticket('young', 30, [1]), ticket('old', 45, [1])];

        const proposals = proposeMatches(wideQueue, [flapWang], tickets, NOW);

        expect(proposals[0].oldestCreatedAt).toEqual(new Date(NOW.getTime() - 45 * 1000));
    });

    it('오래 기다린 티켓부터 담는다', () => {
        const tickets = [ticket('young', 10, [3]), ticket('old', 45, [3]), ticket('middle', 30, [3])];

        const proposals = proposeMatches(wideQueue, [duel], tickets, NOW);

        expect(proposals[0].ticketIds).toEqual(['old', 'middle']);
    });
});
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
pnpm test -- matchFunction
```
기대: FAIL — `Cannot find module '@src/director/matchFunction'`

- [ ] **Step 3: 구현**

`src/director/matchFunction.ts` 생성:

```typescript
import { MatchmakingTicket } from '@interfaces/matchmakingTicket.interface';
import { computeRatingRange, waitedSeconds } from '@src/director/ratingRange';
import { requiredPlayerCount } from '@src/director/requiredPlayerCount';
import { GameModeCapacity, Proposal, QueuePolicy } from '@src/director/types';

function allows(ticket: MatchmakingTicket, gameModeId: number): boolean {
    return ticket.gameModeIds.length === 0 || ticket.gameModeIds.includes(gameModeId);
}

/**
 * 게임 하나를 가정하고 티켓을 묶어 본다.
 * 오래 기다린 사람부터 담고, 담았을 때 묶음 전체의 실력 구간이 여전히 겹치는 티켓만 받는다.
 */
function buildGroup(queue: QueuePolicy, gameMode: GameModeCapacity, candidates: MatchmakingTicket[], now: Date) {
    const picked: MatchmakingTicket[] = [];
    let playerCount = 0;
    let lower = -Infinity;
    let upper = Infinity;

    for (const candidate of candidates) {
        if (playerCount + candidate.userIds.length > gameMode.maxPlayers) {
            continue;
        }

        const range = computeRatingRange(candidate, queue, now);
        const nextLower = Math.max(lower, range.lower);
        const nextUpper = Math.min(upper, range.upper);
        if (nextLower > nextUpper) {
            continue;
        }

        picked.push(candidate);
        playerCount += candidate.userIds.length;
        lower = nextLower;
        upper = nextUpper;

        if (playerCount === gameMode.maxPlayers) {
            break;
        }
    }

    return { picked, playerCount };
}

/** 큐가 허락한 게임마다 한 개씩, 지금 성립하는 제안을 만든다. */
export function proposeMatches(
    queue: QueuePolicy,
    gameModes: GameModeCapacity[],
    tickets: MatchmakingTicket[],
    now: Date,
): Proposal[] {
    const byOldest = [...tickets].sort((x, y) => x.createdAt.getTime() - y.createdAt.getTime());
    const proposals: Proposal[] = [];

    for (const gameMode of gameModes) {
        const candidates = byOldest.filter(ticket => allows(ticket, gameMode.id));
        const { picked, playerCount } = buildGroup(queue, gameMode, candidates, now);
        if (picked.length === 0) {
            continue;
        }

        const oldestCreatedAt = picked[0].createdAt;
        const required = requiredPlayerCount(gameMode, queue, waitedSeconds(oldestCreatedAt, now));
        if (playerCount < required) {
            continue;
        }

        proposals.push({
            gameModeId: gameMode.id,
            ticketIds: picked.map(ticket => ticket.id),
            playerCount: playerCount,
            oldestCreatedAt: oldestCreatedAt,
        });
    }

    return proposals;
}
```

- [ ] **Step 4: 테스트 통과 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
pnpm test -- matchFunction
```
기대: PASS (18 tests)

- [ ] **Step 5: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git add apps/matchmaking-server/src/director
git commit -m "$(cat <<'EOF'
feat(director): 게임마다 후보를 모아 제안을 만든다

후보가 빈 티켓("랜덤")에 별도 코드가 없다 — 모든 허용 게임의 제안에
그냥 끼고, 어느 게임이 될지는 제안을 고르는 쪽이 정한다.

묶음 전체의 실력 구간이 겹쳐야 한다는 조건은 max(하한) <= min(상한)
한 줄이다. 1차원 구간은 둘씩 겹친다고 셋이 겹치지는 않으므로,
이웃끼리만 겹치는 셋은 둘까지만 담긴다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: 제안 고르기(Evaluator) + 맵 고르기

**Files:**
- Create: `lop-backend/apps/matchmaking-server/src/director/evaluator.ts`
- Create: `lop-backend/apps/matchmaking-server/src/director/selectMap.ts`
- Create: `lop-backend/apps/matchmaking-server/src/director/__tests__/evaluator.test.ts`
- Create: `lop-backend/apps/matchmaking-server/src/director/__tests__/selectMap.test.ts`

**Interfaces:**
- Consumes: Task 1의 `Proposal`, `MapOption`. Task 2의 `proposeMatches` 출력.
- Produces:
  - `evaluateProposals(proposals: Proposal[], random: () => number): Proposal[]`
  - `selectMap(gameModeId: number, tickets: MatchmakingTicket[], maps: MapOption[], random: () => number): number`

**규칙:**
- Evaluator는 **인원 큰 순 → 오래 기다린 티켓이 낀 쪽 → 무작위** 로 정렬한 뒤, 앞에서부터 훑으며 **이미 채택한 제안과 티켓이 겹치지 않는 것만** 채택한다.
- 무작위는 정렬 *전에* 제안마다 키를 하나씩 뽑아 쓴다. 비교 중에 난수를 부르면 정렬이 불안정해진다.
- `selectMap`은 티켓들의 `mapIds` **교집합**(그 게임의 맵으로 한정)을 쓰고, 비어 있으면 그 게임의 전체 맵에서 고른다. 빈 `mapIds`는 "아무거나"라는 뜻이므로 교집합 계산에서 제외한다.

- [ ] **Step 1: 실패하는 테스트 작성 — Evaluator**

`src/director/__tests__/evaluator.test.ts` 생성:

```typescript
import { evaluateProposals } from '@src/director/evaluator';
import { Proposal } from '@src/director/types';

const T0 = new Date('2026-07-31T00:00:00Z');

function proposal(gameModeId: number, ticketIds: string[], playerCount: number, oldestCreatedAt = T0): Proposal {
    return { gameModeId, ticketIds, playerCount, oldestCreatedAt };
}

/** 테스트에서 무작위를 없애고 싶을 때. */
const noRandom = () => 0;

describe('evaluateProposals', () => {
    it('겹치지 않는 제안은 모두 채택한다', () => {
        const proposals = [proposal(1, ['a', 'b'], 2), proposal(2, ['c', 'd'], 2)];

        expect(evaluateProposals(proposals, noRandom)).toHaveLength(2);
    });

    it('티켓이 겹치면 하나만 채택한다', () => {
        const proposals = [proposal(1, ['a', 'b'], 2), proposal(2, ['b', 'c'], 2)];

        const selected = evaluateProposals(proposals, noRandom);

        expect(selected).toHaveLength(1);
    });

    it('인원이 큰 제안을 먼저 채택한다', () => {
        const small = proposal(1, ['a', 'b'], 2);
        const big = proposal(2, ['a', 'b', 'c', 'd'], 4);

        const selected = evaluateProposals([small, big], noRandom);

        expect(selected).toEqual([big]);
    });

    it('인원이 같으면 오래 기다린 티켓이 낀 쪽을 채택한다', () => {
        const younger = proposal(1, ['a', 'b'], 2, new Date('2026-07-31T00:00:10Z'));
        const older = proposal(2, ['a', 'b'], 2, new Date('2026-07-31T00:00:00Z'));

        const selected = evaluateProposals([younger, older], noRandom);

        expect(selected).toEqual([older]);
    });

    it('완전히 동률이면 무작위가 가른다', () => {
        const first = proposal(1, ['a', 'b'], 2);
        const second = proposal(2, ['a', 'b'], 2);

        // 첫 제안에 큰 키, 둘째에 작은 키를 주면 둘째가 이긴다
        const keys = [0.9, 0.1];
        let call = 0;
        const selected = evaluateProposals([first, second], () => keys[call++]);

        expect(selected).toEqual([second]);
    });

    it('원본 배열을 건드리지 않는다', () => {
        const proposals = [proposal(1, ['a', 'b'], 2), proposal(2, ['c', 'd'], 4)];
        const before = [...proposals];

        evaluateProposals(proposals, noRandom);

        expect(proposals).toEqual(before);
    });

    it('제안이 없으면 빈 결과다', () => {
        expect(evaluateProposals([], noRandom)).toEqual([]);
    });
});
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
pnpm test -- evaluator
```
기대: FAIL — `Cannot find module '@src/director/evaluator'`

- [ ] **Step 3: Evaluator 구현**

`src/director/evaluator.ts` 생성:

```typescript
import { Proposal } from '@src/director/types';

/**
 * 같은 티켓을 노리는 제안 중 무엇을 살릴지 고른다.
 *
 * 무작위 키를 정렬 전에 미리 뽑는 이유: 비교 도중에 난수를 부르면 같은 두 제안의 우열이
 * 부를 때마다 달라져 정렬 결과를 신뢰할 수 없다.
 */
export function evaluateProposals(proposals: Proposal[], random: () => number): Proposal[] {
    const ranked = proposals.map(proposal => ({ proposal, tieBreaker: random() }));

    ranked.sort((x, y) => {
        if (x.proposal.playerCount !== y.proposal.playerCount) {
            return y.proposal.playerCount - x.proposal.playerCount;
        }

        const ageDiff = x.proposal.oldestCreatedAt.getTime() - y.proposal.oldestCreatedAt.getTime();
        if (ageDiff !== 0) {
            return ageDiff;
        }

        return x.tieBreaker - y.tieBreaker;
    });

    const taken = new Set<string>();
    const selected: Proposal[] = [];

    for (const { proposal } of ranked) {
        if (proposal.ticketIds.some(id => taken.has(id))) {
            continue;
        }

        proposal.ticketIds.forEach(id => taken.add(id));
        selected.push(proposal);
    }

    return selected;
}
```

- [ ] **Step 4: 테스트 통과 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
pnpm test -- evaluator
```
기대: PASS (7 tests)

- [ ] **Step 5: 실패하는 테스트 작성 — 맵 고르기**

`src/director/__tests__/selectMap.test.ts` 생성:

```typescript
import { selectMap } from '@src/director/selectMap';
import { MapOption } from '@src/director/types';
import { MatchmakingTicket } from '@interfaces/matchmakingTicket.interface';

const maps: MapOption[] = [
    { id: 1, gameModeId: 1 },
    { id: 2, gameModeId: 1 },
    { id: 3, gameModeId: 1 },
    { id: 9, gameModeId: 2 },
];

function ticket(id: string, mapIds: number[]): MatchmakingTicket {
    return {
        id: id,
        userIds: [id],
        queueId: 1,
        gameModeIds: [1],
        mapIds: mapIds,
        rating: 1000,
        createdAt: new Date('2026-07-31T00:00:00Z'),
    };
}

/** 항상 첫 후보를 고르게 한다. */
const pickFirst = () => 0;

describe('selectMap', () => {
    it('모두가 같은 맵을 원하면 그 맵이다', () => {
        const chosen = selectMap(1, [ticket('a', [2]), ticket('b', [2])], maps, pickFirst);

        expect(chosen).toBe(2);
    });

    it('겹치는 맵이 하나면 그 맵이다', () => {
        const chosen = selectMap(1, [ticket('a', [1, 2]), ticket('b', [2, 3])], maps, pickFirst);

        expect(chosen).toBe(2);
    });

    it('맵을 안 고른 티켓은 교집합에서 빼고 본다', () => {
        const chosen = selectMap(1, [ticket('a', [3]), ticket('picky', [])], maps, pickFirst);

        expect(chosen).toBe(3);
    });

    it('아무도 안 골랐으면 그 게임의 맵 중에서 고른다', () => {
        const chosen = selectMap(1, [ticket('a', []), ticket('b', [])], maps, pickFirst);

        expect([1, 2, 3]).toContain(chosen);
    });

    it('원하는 맵이 서로 어긋나면 그 게임의 맵 중에서 고른다', () => {
        const chosen = selectMap(1, [ticket('a', [1]), ticket('b', [3])], maps, pickFirst);

        expect([1, 2, 3]).toContain(chosen);
    });

    it('다른 게임의 맵은 후보가 되지 않는다', () => {
        const chosen = selectMap(1, [ticket('a', [9])], maps, pickFirst);

        expect([1, 2, 3]).toContain(chosen);
    });

    it('무작위가 후보 중에서 고른다', () => {
        // 후보 [1,2,3] 중 0.7 → floor(0.7*3) = 2번째
        const chosen = selectMap(1, [ticket('a', [])], maps, () => 0.7);

        expect(chosen).toBe(3);
    });

    it('그 게임에 맵이 하나도 없으면 에러다', () => {
        expect(() => selectMap(7, [ticket('a', [])], maps, pickFirst)).toThrow('No map for gameModeId: 7');
    });
});
```

- [ ] **Step 6: 테스트가 실패하는지 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
pnpm test -- selectMap
```
기대: FAIL — `Cannot find module '@src/director/selectMap'`

- [ ] **Step 7: 맵 고르기 구현**

`src/director/selectMap.ts` 생성:

```typescript
import { MatchmakingTicket } from '@interfaces/matchmakingTicket.interface';
import { MapOption } from '@src/director/types';

/**
 * 확정된 게임에서 맵 하나를 고른다.
 * 맵을 고르지 않은 티켓은 "아무거나"라는 뜻이라 교집합 계산에서 뺀다.
 * 모두의 뜻을 만족하는 맵이 없으면 그 게임의 맵 중에서 고른다 — 아무도 못 가는 것보다 낫다.
 */
export function selectMap(
    gameModeId: number,
    tickets: MatchmakingTicket[],
    maps: MapOption[],
    random: () => number,
): number {
    const gameMapIds = maps.filter(map => map.gameModeId === gameModeId).map(map => map.id);
    if (gameMapIds.length === 0) {
        throw new Error(`No map for gameModeId: ${gameModeId}`);
    }

    const wanted = tickets
        .map(ticket => ticket.mapIds.filter(id => gameMapIds.includes(id)))
        .filter(ids => ids.length > 0);

    const agreed = wanted.reduce<number[]>(
        (common, ids) => common.filter(id => ids.includes(id)),
        gameMapIds,
    );

    const candidates = agreed.length > 0 ? agreed : gameMapIds;

    return candidates[Math.floor(random() * candidates.length)];
}
```

- [ ] **Step 8: 테스트 통과 확인 + 전체 스위트**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
pnpm test -- selectMap
pnpm test
```
기대: 첫 명령 PASS (8 tests). 두 번째는 8 suites / 62 tests PASS (기존 16 + 4a 신규 46).

- [ ] **Step 9: 컴파일 확인 — 배선하지 않았음을 증명**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
npx tsc --noEmit
cd /c/Users/re5na/workspace/LOP/lop-backend
pnpm build
grep -rn "@src/director/" apps/matchmaking-server/src --include=*.ts | grep -v "^apps/matchmaking-server/src/director/"
```
기대: `tsc` 에러 0, 4개 패키지 빌드 성공, **grep 결과 없음**.

> 두 번째 `grep`의 제외 패턴을 `^`로 **경로 앞에 고정**한 이유: 그냥 `src/director/`로 거르면 *import 문자열 안*의 `@src/director/...` 때문에 바깥 파일의 import 줄까지 함께 걸러져, 진짜 배선이 생겨도 결과가 비어 보인다. 앞에 고정하면 `src/director/` **폴더 안의 파일**만 제외된다.

- [ ] **Step 10: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git add apps/matchmaking-server/src/director
git commit -m "$(cat <<'EOF'
feat(director): 제안 고르기와 맵 고르기

겹치는 제안은 인원 큰 순 -> 오래 기다린 쪽 -> 무작위로 가른다.
무작위 키를 정렬 전에 미리 뽑는 이유는, 비교 도중에 난수를 부르면
같은 두 제안의 우열이 부를 때마다 달라져 정렬을 믿을 수 없어서다.

맵은 티켓들이 원한 맵의 교집합에서 고르고, 교집합이 비면 그 게임의
맵 중에서 고른다 - 뜻이 어긋났다고 아무도 못 가는 것보다 낫다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: 머지

**Files:**
- Modify: `docs/ROADMAP.md` (클라 저장소 워크트리)

**Interfaces:**
- Consumes: Task 1~3.
- Produces: 없음(마감).

> **배포하지 않는다.** 이 슬라이스는 부르는 곳이 없는 코드만 추가했으므로 배포해도 동작이 같다. 배포는 4b에서 Director가 실제로 돌 때 한 번에 한다.

- [ ] **Step 1: 머지 + 푸시**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git checkout main
git merge --no-ff feature/matchmaking-slice4a-algorithm -m "Merge feature/matchmaking-slice4a-algorithm: 매칭 알고리즘 순수 함수"
git push origin main
git rev-parse --short HEAD
```

- [ ] **Step 2: ROADMAP 갱신**

클라 저장소 워크트리(`C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/docs+matchmaking-standardization`)에서 `docs/ROADMAP.md`의 매치메이킹 트랙 절에 있는 `▶ **다음 = 슬라이스 4**...` 줄을 아래로 교체한다:

```markdown
- ✅ **슬라이스 4a — 매칭 알고리즘 순수 함수 (백엔드 전용, 배포 없음)** — 풀 기반 매칭의 판단 로직을
  `apps/matchmaking-server/src/director/`의 순수 함수 다섯 개로 신설했다: 레이팅 폭 확장, **요구 인원
  선형 감소**, 제안 생성, 제안 선택, 맵 선택. 전부 `now`/`random`을 인자로 받아 DB도 시계도 없이 테스트된다.
  **부르는 곳이 없어 동작 변화 0**이고, 그래서 배포도 하지 않았다 — 4b가 Director를 세울 때 함께 나간다.
  spec §8의 "§6-2 정정"대로 요구 인원이 정원에서 최소로 줄어든다(옛 규칙은 7명이 모여도 최대 대기를
  꽉 기다렸다). plan `2026-07-31-matchmaking-slice4a-algorithm`.
- ▶ **다음 = 슬라이스 4b**(Director 프로세스 + 요청 경로 전환 + `WaitingRoom` 731줄 폐기 + 배포).

**슬라이스 4b가 할 일 (4a가 남긴 것):**

| | 항목 |
|---|---|
| 🔴 | Director를 **같은 이미지의 두 번째 진입점**(`dist/director.js`)으로 만들고 k8s Deployment **replica 1**로 띄우기 |
| 🔴 | 요청 경로에서 대기방 제거 — `requestMatchmaking`은 티켓만 만들고 끝 |
| 🔴 | `WaitingRoom` 17파일 731줄 + `Updater`/`Updatable` 삭제, 로비의 자가치유 경로를 티켓 기준으로 |
| 🟠 | `TbQueue`의 Casual `max_wait_seconds` 30 → **10**. 지금 코드는 `5`가 하드코딩돼 있어 엑셀을 바꿔도 효과가 없다 — 값이 실제로 쓰이는 4b에서 함께 (인프라 + MasterData 클·서 3개 저장소) |
| 🟠 | 매치 생성 경로의 원자성 — 룸 생성·유저 위치·티켓 삭제는 HTTP를 건너 DB 트랜잭션으로 못 묶는다 |
| 🟡 | 제출된 `queueId`가 실존하는지 검증 (슬라이스 2에서 미룬 것) |
| 🟡 | 규모가 커지면 Director를 **별도 앱·별도 이미지**로 (Open Match가 그렇게 배포한다). 동기는 Director를 늘리는 게 아니라 매칭 로직만 고쳤을 때 API 서버까지 재시작되는 것을 피하고 장애를 격리하기 위해서다 |
```

- [ ] **Step 3: 커밋 + 머지 + 푸시**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/docs+matchmaking-standardization"
git add docs/ROADMAP.md
git commit -m "$(cat <<'EOF'
docs(roadmap): 슬라이스 4a 완료 기록 + 4b가 할 일

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git merge --no-ff worktree-docs+matchmaking-standardization -m "Merge docs+matchmaking-standardization: 슬라이스 4a 완료 기록"
git push origin main
```

---

## 이 슬라이스에서 하지 않는 것 (경계)

| 안 하는 것 | 어디서 |
|---|---|
| **배선** — 이 함수들을 부르는 것 | 4b |
| Director 프로세스, k8s Deployment, 배포 | 4b |
| `WaitingRoom`/`Updater` 삭제, 요청 경로 전환 | 4b |
| `TbQueue`의 Casual 최대 대기 30 → 10 (마스터데이터 3개 저장소) | 4b |
| 매치 생성 경로의 원자성, `queueId` 검증 | 4b |
| 클라·게임 서버 코드 | — (이 트랙에서 건드릴 일 없음) |
| `Location.WaitingRoom` → `Matchmaking` 개명 | 슬라이스 5 |
| Director 틱 주기 튜닝, 게임 편중 가중치 | 관측 후 별도 |
