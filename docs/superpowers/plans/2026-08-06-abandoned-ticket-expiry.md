# 버려진 매칭 대기표 만료 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 클라가 죽거나 너무 오래 기다린 매칭 대기표를 Director 틱이 스스로 정리해, 유령 플레이어가 낀 매치가 만들어지지 않게 한다.

**Architecture:** 대기표에 `lastHeartbeat` 컬럼을 두고, 로비가 1초마다 부르는 티켓 조회가 그 값을 갱신한다(신호를 새로 만들지 않는다). Director 틱이 대기 풀을 읽은 직후 순수 함수로 "신호 끊김 / 상한 초과"를 판정해 삭제하고, 살아남은 것만 매칭한다. 삭제 이후 유저를 로비로 되돌리는 일은 이미 있는 로비 자가치유가 처리하므로 **클라 변경은 없다**.

**Tech Stack:** TypeScript, Prisma(PostgreSQL), Express, Jest(유닛 + testcontainers 통합), Luban 마스터데이터(Excel → gen.sh)

**Spec:** `docs/superpowers/specs/2026-08-05-abandoned-ticket-expiry-design.md`

## Global Constraints

- 신호 끊김 임계값 = **60초**(`60_000` ms). 매칭서버 코드 상수. 방(Room)의 `HEARTBEAT_THRESHOLD`와 같은 값
- 절대 상한 = **`TbQueue.ticket_ttl_seconds`**, 시작값 **600**(10분), 큐 1·2 모두 600
- 마스터데이터 컬럼 그룹 = **`m`**(매칭서버 전용). 클라·서버 빌드에 실리면 안 된다
- 신호 갱신은 **컨트롤러가 부르는 전용 메서드에서만**. 기존 `findMatchmakingTicketById`는 건드리지 않는다 (내부 호출자가 있어 "바깥에서 온 신호"라는 구분이 무너진다)
- **`matchId`가 있는 티켓은 절대 삭제하지 않는다** (이미 매치가 가져간 것)
- 명명은 방(Room)의 어휘를 따른다 — `lastHeartbeat`, `heartbeat`. 같은 개념에 새 단어를 만들지 않는다
- 클라이언트(Unity) 코드는 **변경하지 않는다**
- 모든 신규 테스트는 **일부러 되돌려 실제로 실패하는지 확인**한다

---

## 파일 구조

| 파일 | 책임 | 태스크 |
|---|---|---|
| `infrastructure/table/Datas/#Queue.xlsx` | 큐별 상한 값의 진실원본 | 1 |
| `lop-backend/apps/matchmaking-server/master_data/tbqueue.json` | 생성물 — 매칭서버가 읽는 데이터 | 1 |
| `lop-backend/apps/matchmaking-server/src/masterdata/schema.ts` | 생성물 — 매칭서버가 읽는 스키마 | 1 |
| `lop-backend/packages/database/prisma/schema.prisma` | `lastHeartbeat` 컬럼 | 2 |
| `.../prisma/migrations/20260806000000_matchmaking_ticket_heartbeat/migration.sql` | 컬럼 추가 마이그레이션 | 2 |
| `.../src/interfaces/matchmakingTicket.interface.ts` | 도메인 타입 | 2 |
| `.../src/factories/matchmakingTicket.factory.ts` | 기본값 | 2 |
| `.../src/mappers/entities/matchmakingTicket.mapper.ts` | 엔티티↔도메인 | 2 |
| `.../src/daos/matchmakingTicket.dao.postgres.ts` | 갱신+조회 한 쿼리 | 3 |
| `.../src/repositories/matchmakingTicket.repository.ts` | 저장소 배선 | 3 |
| `.../src/services/matchmakingTicket.service.ts` | 신호 기록용 전용 메서드 | 3 |
| `.../src/controllers/matchmakingTicket.controller.ts` | 그 메서드로 교체 | 3 |
| **`.../src/director/abandonedTickets.ts`** | **판정 순수 함수 (신규)** | 4 |
| `.../src/director/types.ts` | `QueuePolicy.ticketTtlSeconds` | 4 |
| `.../src/director/tick.ts` | 풀 읽은 직후 판정·삭제 | 5 |
| `.../src/director.ts` | 삭제 의존성 주입 + 로깅 | 5 |

---

## Task 1: 마스터데이터에 `ticket_ttl_seconds` 추가

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/infrastructure/table/Datas/#Queue.xlsx`
- Generated (커밋 대상): `lop-backend/apps/matchmaking-server/master_data/tbqueue.json`, `lop-backend/apps/matchmaking-server/src/masterdata/schema.ts`
- Test: `lop-backend/apps/matchmaking-server/src/loaders/__tests__/masterdata.loader.test.ts` (기존 파일에 추가)

**Interfaces:**
- Consumes: 없음
- Produces: `getTables().TbQueue.getDataList()[i].ticketTtlSeconds: number` — Task 4·5가 읽는다

**배경:** `#Queue.xlsx`는 Luban의 "Excel 내장 스키마" 형식이다. 1행 `##var`(컬럼명), 2행 `##type`(타입), 3행 `##group`(그룹), 4행 `##`(설명), 5행부터 데이터. 그룹이 비면 모든 타깃에 나가고, `c`를 적으면 클라 타깃에만 나간다(`name` 컬럼이 그 예다 — 백엔드 JSON에 `name`이 없다).

- [ ] **Step 1: 현재 Excel 구조를 눈으로 확인**

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure/table"
python -c "
import openpyxl
wb = openpyxl.load_workbook('Datas/#Queue.xlsx')
ws = wb.active
for row in ws.iter_rows(values_only=True): print(row)
"
```

Expected: 6행이 나오고 마지막 컬럼(L열)이 `max_wait_seconds`, 데이터 2행(Casual 10 / Ranked 60).

- [ ] **Step 2: 컬럼 추가**

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure/table"
python - <<'PY'
import openpyxl
wb = openpyxl.load_workbook('Datas/#Queue.xlsx')
ws = wb.active
col = ws.max_column + 1
assert ws.cell(row=1, column=col).value is None, '빈 열이 아니다 — 구조를 다시 확인할 것'

ws.cell(row=1, column=col, value='ticket_ttl_seconds')   # ##var
ws.cell(row=2, column=col, value='int')                  # ##type
ws.cell(row=3, column=col, value='m')                    # ##group — 매칭서버 전용
ws.cell(row=4, column=col, value='ticket_ttl_seconds')   # ##  (설명 행: 다른 열과 같은 관례)
ws.cell(row=5, column=col, value=600)                    # Casual
ws.cell(row=6, column=col, value=600)                    # Ranked

wb.save('Datas/#Queue.xlsx')
print('컬럼 추가 완료')
PY
```

- [ ] **Step 3: 생성 실행**

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure/table"
bash gen.sh
```

Expected: `[gen] target=client ... [gen] target=server ... [gen] target=matchmaking ... [done]`

- [ ] **Step 4: 그룹이 실제로 걸러졌는지 실측 (이 태스크의 핵심 검증)**

```bash
cd "C:/Users/re5na/workspace/LOP"
echo "--- 매칭서버에는 있어야 한다 ---"
grep -c "ticket_ttl_seconds" lop-backend/apps/matchmaking-server/master_data/tbqueue.json
grep -c "ticketTtlSeconds" lop-backend/apps/matchmaking-server/src/masterdata/schema.ts
echo "--- 클라·서버에는 없어야 한다 ---"
grep -c "TicketTtlSeconds" LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData/Queue.cs || echo 0
grep -c "TicketTtlSeconds" LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/Queue.cs || echo 0
```

Expected: 앞의 둘은 **2 이상 / 1 이상**, 뒤의 둘은 **0**.

**뒤의 둘이 0이 아니면 멈추고 보고할 것.** `luban.conf`의 `m` 그룹이 `default: false`라 태그 없는 컬럼과 동작이 다를 수 있고, 그러면 클라 빌드에 매칭 전용 값이 실린다.

- [ ] **Step 5: Unity 패키지의 `.meta` 유실 확인 및 원복**

`gen.sh`는 출력 폴더를 `rm -rf` 하는데 Unity `.meta`는 Luban이 다시 만들어 주지 않는다. 그룹 `m`이라 클라·서버 생성물의 *내용*은 안 바뀌었어야 한다.

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client" && git status --porcelain | head -20
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server" && git status --porcelain | head -20
```

Expected: `.meta` 파일의 삭제(`D`)만 보이고, `.cs`/`.bytes` 내용 변경(`M`)은 **없어야 한다**.

내용 변경이 없다면 두 저장소를 통째로 원복한다(GUID churn을 커밋하지 않는다):

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client" && git checkout -- . && git status --porcelain
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server" && git checkout -- . && git status --porcelain
```

Expected: 두 명령 모두 출력 없음(깨끗함).

**`M`으로 표시된 `.cs`/`.bytes`가 있으면 멈추고 보고할 것** — 그룹 필터가 안 먹었다는 뜻이다.

- [ ] **Step 6: 값이 매칭서버까지 도달했는지 보는 테스트 작성**

`lop-backend/apps/matchmaking-server/src/loaders/__tests__/masterdata.loader.test.ts` 는 이미 있다(확인함). 그 파일 끝에 아래 `describe` 를 추가한다.

```typescript
import { getTables, load } from '@loaders/masterdata.loader';

describe('TbQueue.ticketTtlSeconds', () => {
    beforeAll(async () => { await load(); });

    it('모든 큐가 양수 상한을 갖는다 — 0이면 상한 만료가 통째로 꺼진다', () => {
        const queues = getTables().TbQueue.getDataList();

        expect(queues.length).toBeGreaterThan(0);
        for (const queue of queues) {
            expect(queue.ticketTtlSeconds).toBeGreaterThan(0);
        }
    });
});
```

- [ ] **Step 7: 테스트 실행**

```bash
cd "C:/Users/re5na/workspace/LOP/lop-backend"
pnpm --filter matchmaking-server run test -- masterdata.loader
```

Expected: PASS.

`load()`가 상대 경로 `master_data`를 읽으므로 jest의 작업 디렉터리가 `apps/matchmaking-server`여야 한다. 실패하면 에러 메시지에 찍힌 경로를 보고 보고할 것.

- [ ] **Step 8: 커밋 (저장소 2개)**

```bash
cd "C:/Users/re5na/workspace/LOP/infrastructure"
git add table/Datas/#Queue.xlsx
git commit -m "feat(masterdata): TbQueue에 ticket_ttl_seconds 추가 (그룹 m)

대기표 절대 상한. 매칭서버만 읽으므로 그룹 m으로 좁혀 클라 빌드에 안 싣는다.
시작값은 두 큐 모두 600초(10분) — Open Match assignedDeleteTimeout과 같은 눈금."

cd "C:/Users/re5na/workspace/LOP/lop-backend"
git add apps/matchmaking-server/master_data/tbqueue.json apps/matchmaking-server/src/masterdata/schema.ts apps/matchmaking-server/src/loaders/__tests__/masterdata.loader.test.ts
git commit -m "feat(masterdata): ticket_ttl_seconds 생성물 반영 + 도달 확인 테스트"
```

---

## Task 2: `lastHeartbeat` 컬럼과 도메인 배선

**Files:**
- Modify: `lop-backend/packages/database/prisma/schema.prisma` (model `MatchmakingTicket`)
- Create: `lop-backend/packages/database/prisma/migrations/20260806000000_matchmaking_ticket_heartbeat/migration.sql`
- Modify: `lop-backend/apps/matchmaking-server/src/interfaces/matchmakingTicket.interface.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/factories/matchmakingTicket.factory.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/mappers/entities/matchmakingTicket.mapper.ts`
- Test: `lop-backend/apps/matchmaking-server/src/mappers/entities/__tests__/matchmakingTicket.mapper.test.ts` (기존 파일)

**Interfaces:**
- Consumes: 없음
- Produces: `MatchmakingTicket.lastHeartbeat: Date` — Task 3·4가 읽고 쓴다

- [ ] **Step 1: 매퍼 왕복 테스트를 먼저 추가(실패해야 한다)**

`lop-backend/apps/matchmaking-server/src/mappers/entities/__tests__/matchmakingTicket.mapper.test.ts`의 `describe('MatchmakingTicketMapper', ...)` 안에 추가:

```typescript
    it('lastHeartbeat 을 양방향으로 옮긴다', () => {
        const mapper = new MatchmakingTicketMapper();
        const 시각 = new Date('2026-08-06T00:00:00.000Z');

        const entity = mapper.toEntity(MatchmakingTicketFactory.create({ id: 't1', userIds: ['u1'], lastHeartbeat: 시각 }));
        expect(entity.lastHeartbeat).toEqual(시각);

        expect(mapper.toDomain(entity).lastHeartbeat).toEqual(시각);
    });
```

`MatchmakingTicketFactory`가 이 파일에 이미 import 되어 있다(파일 하단 `describe('MatchmakingTicketFactory', ...)`가 쓴다). 없으면 추가:
`import { MatchmakingTicketFactory } from '@factories/matchmakingTicket.factory';`

- [ ] **Step 2: 실패 확인**

```bash
cd "C:/Users/re5na/workspace/LOP/lop-backend"
pnpm --filter matchmaking-server run test -- matchmakingTicket.mapper
```

Expected: FAIL — `lastHeartbeat` 가 타입에 없어 컴파일 에러(TS2353 또는 TS2339).

- [ ] **Step 3: Prisma 스키마에 컬럼 추가**

`packages/database/prisma/schema.prisma`의 `model MatchmakingTicket`에서 `createdAt` 줄 바로 아래에 추가:

```prisma
  createdAt   DateTime @default(now())
  //  마지막으로 이 티켓의 주인이 살아 있음을 확인한 시각. 로비의 티켓 조회가 갱신한다.
  //  이름은 Room.lastHeartbeat 와 같게 맞췄다 — 같은 개념에 다른 단어를 만들지 않는다.
  lastHeartbeat DateTime @default(now())
```

- [ ] **Step 4: 마이그레이션 작성**

`packages/database/prisma/migrations/20260806000000_matchmaking_ticket_heartbeat/migration.sql` 생성:

```sql
--  기존 행은 마이그레이션 시각으로 채워진다. 이게 중요하다 — NULL 이나 과거 시각으로 두면
--  배포 직후 첫 틱이 그때 대기 중이던 사람들을 전부 "신호 없음"으로 지운다.
ALTER TABLE "MatchmakingTicket" ADD COLUMN "lastHeartbeat" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP;
```

- [ ] **Step 5: Prisma 클라이언트 재생성**

```bash
cd "C:/Users/re5na/workspace/LOP/lop-backend"
pnpm --filter @lop/database run build
```

Expected: 성공. (`@lop/database`의 build 는 `prisma generate && tsc -p tsconfig.json` 이다 — 확인함.)

- [ ] **Step 6: 도메인 타입에 추가**

`apps/matchmaking-server/src/interfaces/matchmakingTicket.interface.ts`:

```typescript
export interface MatchmakingTicket {
    id: string;
    userIds: string[];
    queueId: number;
    gameModeIds: number[];
    mapIds: number[];
    rating: number;
    createdAt: Date;
    /** 마지막으로 주인이 살아 있음을 확인한 시각. 로비의 티켓 조회가 갱신한다. */
    lastHeartbeat: Date;
    /** 이 티켓을 가져간 매치. 값이 있으면 대기 풀에서 빠졌지만 아직 지워지지 않은 상태다. */
    matchId: string | null;
}
```

- [ ] **Step 7: 팩토리 기본값 추가**

`apps/matchmaking-server/src/factories/matchmakingTicket.factory.ts`의 `createDefault()`에서 `createdAt` 아래에 추가:

```typescript
            createdAt: new Date(),
            lastHeartbeat: new Date(),
            matchId: null,
```

- [ ] **Step 8: 매퍼 양방향 추가**

`apps/matchmaking-server/src/mappers/entities/matchmakingTicket.mapper.ts`:

`toDomain`의 `createdAt: entity.createdAt,` 아래에

```typescript
            lastHeartbeat: entity.lastHeartbeat,
```

`toEntity`의 `createdAt: new Date(domain.createdAt),` 아래에

```typescript
            lastHeartbeat: new Date(domain.lastHeartbeat),
```

- [ ] **Step 9: 테스트 통과 확인**

```bash
cd "C:/Users/re5na/workspace/LOP/lop-backend"
pnpm --filter matchmaking-server run build
pnpm --filter matchmaking-server run test
```

Expected: 빌드 성공, 전체 테스트 PASS(157건 + 방금 추가한 1건 = 158).

- [ ] **Step 10: 통합 테스트로 기본값 확인**

`apps/matchmaking-server/test/integration/ticketFreshness.integration.test.ts`의 `describe` 안에 추가:

```typescript
    it('새 티켓은 lastHeartbeat 이 채워진 채로 만들어진다', async () => {
        const 만들기전 = Date.now();
        await createTicket('T1', 'U1');

        const 티켓 = await repository.findById('T1');

        expect(티켓?.lastHeartbeat).toBeInstanceOf(Date);
        //  DB default 로 채워지므로 만든 시점 근처여야 한다(1분 이상 어긋나면 default 가 안 걸린 것).
        expect(Math.abs((티켓!.lastHeartbeat as Date).getTime() - 만들기전)).toBeLessThan(60_000);
    });
```

- [ ] **Step 11: 통합 테스트 실행**

```bash
cd "C:/Users/re5na/workspace/LOP/lop-backend"
pnpm --filter matchmaking-server run test:integration -- ticketFreshness
```

Expected: PASS (2건).

- [ ] **Step 12: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/lop-backend"
git add packages/database/prisma apps/matchmaking-server/src apps/matchmaking-server/test
git commit -m "feat(matchmaking): 대기표에 lastHeartbeat 컬럼 추가

주인이 살아 있음을 마지막으로 확인한 시각. 기존 행은 마이그레이션 시각으로
채워진다 — 과거 시각으로 두면 배포 직후 첫 틱이 대기 중이던 사람을 전부 지운다.
이름은 Room.lastHeartbeat 와 같게 맞췄다."
```

---

## Task 3: 티켓 조회가 하트비트를 찍는다

**Files:**
- Modify: `lop-backend/apps/matchmaking-server/src/daos/matchmakingTicket.dao.postgres.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/repositories/matchmakingTicket.repository.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/services/matchmakingTicket.service.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/controllers/matchmakingTicket.controller.ts`
- Test: `lop-backend/apps/matchmaking-server/test/integration/ticketHeartbeat.integration.test.ts` (신규)

**Interfaces:**
- Consumes: `MatchmakingTicket.lastHeartbeat` (Task 2)
- Produces:
  - DAO `heartbeatAndFindById(id: string): Promise<MatchmakingTicketEntity | null>`
  - Repository `heartbeatAndFindById(id: string): Promise<MatchmakingTicket | null>`
  - Service `heartbeatAndFindMatchmakingTicketById(id: string): Promise<MatchmakingTicket | undefined>`

- [ ] **Step 1: 통합 테스트를 먼저 작성(실패해야 한다)**

`apps/matchmaking-server/test/integration/ticketHeartbeat.integration.test.ts` 생성:

```typescript
import { rawPrisma, resetTables, createTicket } from './db';
import MatchmakingTicketService from '@services/matchmakingTicket.service';

const 잠깐 = (ms: number) => new Promise(resolve => setTimeout(resolve, ms));

/**
 * 로비는 1초마다 이 조회를 부른다. 그 조회가 "주인이 아직 살아 있다"는 기록을 남겨야
 * Director 가 죽은 대기표를 골라낼 수 있다.
 */
describe('티켓 조회가 하트비트를 찍는다', () => {
    let service: MatchmakingTicketService;

    beforeEach(async () => {
        await resetTables();
        service = new MatchmakingTicketService();
    });
    afterAll(async () => { await rawPrisma.$disconnect(); });

    it('조회하면 lastHeartbeat 이 올라간다', async () => {
        await createTicket('T1', 'U1');
        const 처음 = (await rawPrisma.matchmakingTicket.findUnique({ where: { id: 'T1' } }))!.lastHeartbeat;

        //  TIMESTAMP(3) 이라 밀리초 해상도다 — 확실히 달라지도록 잠깐 기다린다.
        await 잠깐(50);
        await service.heartbeatAndFindMatchmakingTicketById('T1');

        const 나중 = (await rawPrisma.matchmakingTicket.findUnique({ where: { id: 'T1' } }))!.lastHeartbeat;
        expect(나중.getTime()).toBeGreaterThan(처음.getTime());
    });

    it('찍으면서 티켓도 함께 돌려준다', async () => {
        await createTicket('T1', 'U1');

        const 티켓 = await service.heartbeatAndFindMatchmakingTicketById('T1');

        expect(티켓?.id).toBe('T1');
        expect(티켓?.userIds).toEqual(['U1']);
    });

    it('없는 티켓이면 undefined 를 주고 터지지 않는다 — 취소·매치 확정 뒤 정상 상황이다', async () => {
        expect(await service.heartbeatAndFindMatchmakingTicketById('없는id')).toBeUndefined();
    });

    //  Director 가 매칭하려고 읽는 것까지 신호로 치면 자기가 읽고 자기가 살아있다고 하는 꼴이 된다.
    it('일반 조회(findMatchmakingTicketById)는 lastHeartbeat 을 건드리지 않는다', async () => {
        await createTicket('T1', 'U1');
        const 처음 = (await rawPrisma.matchmakingTicket.findUnique({ where: { id: 'T1' } }))!.lastHeartbeat;

        await 잠깐(50);
        await service.findMatchmakingTicketById('T1');

        const 나중 = (await rawPrisma.matchmakingTicket.findUnique({ where: { id: 'T1' } }))!.lastHeartbeat;
        expect(나중.getTime()).toBe(처음.getTime());
    });
});
```

- [ ] **Step 2: 실패 확인**

```bash
cd "C:/Users/re5na/workspace/LOP/lop-backend"
pnpm --filter matchmaking-server run test:integration -- ticketHeartbeat
```

Expected: FAIL — `heartbeatAndFindMatchmakingTicketById` 가 없어 컴파일 에러(TS2339).

- [ ] **Step 3: DAO에 갱신+조회 한 쿼리 추가**

`apps/matchmaking-server/src/daos/matchmakingTicket.dao.postgres.ts`의 `findOpenByUserId` 아래에 추가:

```typescript
    /**
     * `lastHeartbeat`을 지금으로 올리면서 그 티켓을 돌려준다. 없으면 null.
     *
     * 갱신과 조회를 한 쿼리로 묶는다 — 로비가 대기자 한 명당 초당 한 번 부르는 경로라
     * 왕복을 둘로 늘릴 이유가 없다.
     */
    public async heartbeatAndFindById(id: string): Promise<MatchmakingTicketEntity | null> {
        try {
            return await this.model.update({
                where: { id: id },
                data: { lastHeartbeat: new Date() },
            });
        } catch (error) {
            //  P2025 = 갱신할 행이 없다. 티켓이 없는 것은 취소·매치 확정 뒤의 정상 상황이라 에러가 아니다.
            if (error instanceof Prisma.PrismaClientKnownRequestError && error.code === 'P2025') {
                return null;
            }
            return Promise.reject(error);
        }
    }
```

`Prisma`는 이 파일 첫 줄에서 이미 import 되어 있다.

- [ ] **Step 4: 저장소에 배선**

`apps/matchmaking-server/src/repositories/matchmakingTicket.repository.ts`의 `findOpenByUserId` 아래에 추가:

```typescript
    /** 조회하면서 "주인이 살아 있다"를 기록한다. 바깥(로비)에서 들어온 조회에만 쓴다. */
    public async heartbeatAndFindById(id: string): Promise<MatchmakingTicket | null> {
        try {
            const entity = await this.postgresDao.heartbeatAndFindById(id);
            return entity ? this.mapper.toDomain(entity) : null;
        } catch (error) {
            return Promise.reject(error);
        }
    }
```

- [ ] **Step 5: 서비스에 전용 메서드 추가**

`apps/matchmaking-server/src/services/matchmakingTicket.service.ts`의 `findMatchmakingTicketById` 아래에 추가:

```typescript
    /**
     * 로비가 1초마다 부르는 조회. **이 경로에서만** 하트비트를 찍는다.
     *
     * 기존 findMatchmakingTicketById 에 갱신을 넣지 않는 이유: 그 메서드는 취소 판정 등
     * 매칭서버 내부에서도 쓰인다. 내부 조회까지 신호로 치면 "바깥에서 들어온 신호"라는
     * 구분이 무너져 아무도 죽지 않는다.
     */
    public async heartbeatAndFindMatchmakingTicketById(id: string): Promise<MatchmakingTicket | undefined> {
        try {
            if (isEmpty(id)) {
                return undefined;
            }

            return (await this.matchmakingTicketRepository.heartbeatAndFindById(id)) ?? undefined;
        } catch (error) {
            return Promise.reject(error);
        }
    }
```

- [ ] **Step 6: 컨트롤러가 그 메서드를 쓰게 교체**

`apps/matchmaking-server/src/controllers/matchmakingTicket.controller.ts`에서 한 줄만 바꾼다:

```typescript
            const findOne = await this.matchmakingTicketService.heartbeatAndFindMatchmakingTicketById(matchmakingTicketId);
```

- [ ] **Step 7: 통과 확인**

```bash
cd "C:/Users/re5na/workspace/LOP/lop-backend"
pnpm --filter matchmaking-server run build
pnpm --filter matchmaking-server run test:integration -- ticketHeartbeat
```

Expected: 빌드 성공, PASS (4건).

- [ ] **Step 8: 되돌려서 실제로 잡는지 확인**

컨트롤러를 원래대로(`findMatchmakingTicketById`) 잠깐 되돌리고 통합 테스트를 돌린다.

Expected: **"조회하면 lastHeartbeat 이 올라간다"가 실패**해야 한다. 실패하지 않으면 테스트가 아무것도 지키지 않는 것이니 멈추고 보고할 것. 확인 후 되돌린 것을 복구한다.

- [ ] **Step 9: 전체 테스트**

```bash
cd "C:/Users/re5na/workspace/LOP/lop-backend"
pnpm test
pnpm --filter matchmaking-server run test:integration
```

Expected: 모두 PASS.

- [ ] **Step 10: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/lop-backend"
git add apps/matchmaking-server
git commit -m "feat(matchmaking): 로비의 티켓 조회가 하트비트를 찍는다

신호를 새로 만들지 않는다 — 로비가 대기자 한 명당 초당 한 번 부르는 조회가
이미 매칭서버에 꽂히고 있어 그 자리에서 lastHeartbeat 을 올린다.
갱신과 조회는 한 쿼리(update)로 묶고, 행이 없으면(P2025) 정상 상황으로 본다.

기존 findMatchmakingTicketById 는 그대로 둔다 — 내부 호출자가 있어 거기에
넣으면 '바깥에서 들어온 신호'라는 구분이 무너진다."
```

---

## Task 4: 버려진 티켓 판정 — 순수 함수

**Files:**
- Create: `lop-backend/apps/matchmaking-server/src/director/abandonedTickets.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/director/types.ts`
- Test: `lop-backend/apps/matchmaking-server/src/director/__tests__/abandonedTickets.test.ts` (신규)

**Interfaces:**
- Consumes: `MatchmakingTicket.lastHeartbeat`(Task 2), `TbQueue.ticketTtlSeconds`(Task 1)
- Produces:
  - `HEARTBEAT_THRESHOLD_MS: number` (= 60_000)
  - `classifyAbandonedTickets(tickets: MatchmakingTicket[], queues: QueuePolicy[], now: Date, heartbeatThresholdMs?: number): AbandonedTicketResult`
  - `interface AbandonedTicketResult { keep: MatchmakingTicket[]; abandonedIds: string[]; silenceSweepSkipped: boolean }`
  - Task 5의 `tick.ts`가 이 셋을 쓴다

- [ ] **Step 1: `QueuePolicy`에 필드 추가**

`apps/matchmaking-server/src/director/types.ts`:

```typescript
export interface QueuePolicy {
    id: number;
    ratingRangeStart: number;
    ratingRangeMax: number;
    ratingRelaxPerSec: number;
    maxWaitSeconds: number;
    /**
     * 살아 있어도 이만큼 지나면 대기표를 버린다(초).
     * maxWaitSeconds 와 헷갈리지 말 것 — 그건 "요구 인원을 최소치까지 낮추는 데 걸리는 시간"이고,
     * 이건 "그만 기다리게 하는 시간"이다.
     */
    ticketTtlSeconds: number;
}
```

- [ ] **Step 2: 유닛 테스트를 먼저 작성(실패해야 한다)**

`apps/matchmaking-server/src/director/__tests__/abandonedTickets.test.ts` 생성:

```typescript
import { classifyAbandonedTickets, HEARTBEAT_THRESHOLD_MS } from '@src/director/abandonedTickets';
import { MatchmakingTicket } from '@interfaces/matchmakingTicket.interface';
import { QueuePolicy } from '@src/director/types';

const 지금 = new Date('2026-08-06T12:00:00.000Z');
const 초전 = (sec: number) => new Date(지금.getTime() - sec * 1000);

const 큐: QueuePolicy = {
    id: 1,
    ratingRangeStart: 500,
    ratingRangeMax: 2000,
    ratingRelaxPerSec: 50,
    maxWaitSeconds: 10,
    ticketTtlSeconds: 600,
};

function 티켓(id: string, opts: { 만든지초?: number; 신호전초?: number; matchId?: string | null; queueId?: number } = {}): MatchmakingTicket {
    return {
        id: id,
        userIds: [`u-${id}`],
        queueId: opts.queueId ?? 1,
        gameModeIds: [1],
        mapIds: [1],
        rating: 1000,
        createdAt: 초전(opts.만든지초 ?? 5),
        lastHeartbeat: 초전(opts.신호전초 ?? 0),
        matchId: opts.matchId ?? null,
    };
}

describe('classifyAbandonedTickets', () => {
    it('방금 신호를 준 티켓은 남긴다', () => {
        const result = classifyAbandonedTickets([티켓('T1')], [큐], 지금);

        expect(result.abandonedIds).toEqual([]);
        expect(result.keep.map(t => t.id)).toEqual(['T1']);
    });

    it('신호가 임계값을 넘겨 끊긴 티켓은 버린다', () => {
        const 살아있음 = 티켓('T1', { 신호전초: 1 });
        const 조용함 = 티켓('T2', { 신호전초: 61 });

        const result = classifyAbandonedTickets([살아있음, 조용함], [큐], 지금);

        expect(result.abandonedIds).toEqual(['T2']);
        expect(result.keep.map(t => t.id)).toEqual(['T1']);
        expect(result.silenceSweepSkipped).toBe(false);
    });

    it('임계값 경계(정확히 60초)는 아직 살아 있는 것으로 본다', () => {
        const result = classifyAbandonedTickets([티켓('T1', { 신호전초: 60 })], [큐], 지금);

        expect(result.abandonedIds).toEqual([]);
    });

    it('신호는 멀쩡해도 상한을 넘긴 티켓은 버린다', () => {
        const 오래됨 = 티켓('T1', { 만든지초: 601, 신호전초: 0 });

        const result = classifyAbandonedTickets([오래됨], [큐], 지금);

        expect(result.abandonedIds).toEqual(['T1']);
    });

    //  이 가드가 없으면 로비가 죽었을 때 대기자 전원의 티켓이 날아간다.
    it('풀 전체가 조용하면 신호 기반 삭제를 건너뛴다 — 개인의 죽음이 아니라 신호 계통 장애다', () => {
        const 전부조용 = [티켓('T1', { 신호전초: 300 }), 티켓('T2', { 신호전초: 300 })];

        const result = classifyAbandonedTickets(전부조용, [큐], 지금);

        expect(result.abandonedIds).toEqual([]);
        expect(result.keep.map(t => t.id)).toEqual(['T1', 'T2']);
        expect(result.silenceSweepSkipped).toBe(true);
    });

    it('가드가 발동해도 상한 기반 삭제는 계속한다 — 상한은 신호와 무관하다', () => {
        const 조용하고오래됨 = 티켓('T1', { 만든지초: 601, 신호전초: 300 });
        const 조용하기만함 = 티켓('T2', { 만든지초: 5, 신호전초: 300 });

        const result = classifyAbandonedTickets([조용하고오래됨, 조용하기만함], [큐], 지금);

        expect(result.silenceSweepSkipped).toBe(true);
        expect(result.abandonedIds).toEqual(['T1']);
        expect(result.keep.map(t => t.id)).toEqual(['T2']);
    });

    it('빈 풀에서는 가드가 발동하지 않는다 — 치울 것도 장애도 없다', () => {
        const result = classifyAbandonedTickets([], [큐], 지금);

        expect(result.abandonedIds).toEqual([]);
        expect(result.silenceSweepSkipped).toBe(false);
    });

    //  이미 매치가 가져간 티켓은 그 사람이 게임 중이라는 뜻이고, 지우는 경로가 따로 있다.
    it('matchId 가 있는 티켓은 지우지도, 매칭 후보로 남기지도 않는다', () => {
        const 소비됨 = 티켓('T1', { 만든지초: 9999, 신호전초: 9999, matchId: 'M1' });

        const result = classifyAbandonedTickets([소비됨], [큐], 지금);

        expect(result.abandonedIds).toEqual([]);
        expect(result.keep).toEqual([]);
    });

    it('큐 설정을 못 찾으면 상한을 적용하지 않는다 — 설정 누락으로 유저를 쫓아내지 않는다', () => {
        const 낯선큐 = 티켓('T1', { 만든지초: 99999, queueId: 999 });

        const result = classifyAbandonedTickets([낯선큐], [큐], 지금);

        expect(result.abandonedIds).toEqual([]);
    });

    it('상한이 0 이하면 상한 만료를 끄는 뜻으로 본다', () => {
        const 상한없음: QueuePolicy = { ...큐, ticketTtlSeconds: 0 };

        const result = classifyAbandonedTickets([티켓('T1', { 만든지초: 99999 })], [상한없음], 지금);

        expect(result.abandonedIds).toEqual([]);
    });

    it('임계값은 Room 과 같은 60초다', () => {
        expect(HEARTBEAT_THRESHOLD_MS).toBe(60_000);
    });
});
```

- [ ] **Step 3: 실패 확인**

```bash
cd "C:/Users/re5na/workspace/LOP/lop-backend"
pnpm --filter matchmaking-server run test -- abandonedTickets
```

Expected: FAIL — `Cannot find module '@src/director/abandonedTickets'`.

- [ ] **Step 4: 순수 함수 구현**

`apps/matchmaking-server/src/director/abandonedTickets.ts` 생성:

```typescript
import { MatchmakingTicket } from '@interfaces/matchmakingTicket.interface';
import { QueuePolicy } from '@src/director/types';

/**
 * 신호가 이만큼 끊기면 그 클라는 없는 것으로 본다.
 * 로비가 1초마다 신호를 보내므로 60번 연속 실패해야 하는 값이고, 방(Room)의
 * HEARTBEAT_THRESHOLD 와 같다. 더 짧게 잡으면 백엔드 배포로 파드가 갈릴 때마다
 * 대기 중인 유저가 전부 튕겨 나간다.
 */
export const HEARTBEAT_THRESHOLD_MS = 60_000;

export interface AbandonedTicketResult {
    /** 이번 틱에서 매칭 후보로 삼을 티켓. */
    keep: MatchmakingTicket[];
    /** 지울 티켓 id. */
    abandonedIds: string[];
    /** 신호 계통 장애로 판단해 신호 기반 삭제를 건너뛰었나. */
    silenceSweepSkipped: boolean;
}

/**
 * 대기 풀을 "계속 볼 것"과 "버릴 것"으로 가른다.
 *
 * 버리는 이유는 둘이고 서로 독립이다:
 *   - **신호 끊김** — 주인의 클라가 사라졌다
 *   - **상한 초과** — 살아 있지만 큐가 정한 시간을 넘겼다
 *
 * 신호 기반 삭제에는 가드가 붙는다. 풀 안에 최근 신호를 준 티켓이 **하나도** 없으면
 * 그건 여러 명이 동시에 죽은 게 아니라 신호 계통(로비 또는 그 사이 네트워크)이 끊긴
 * 것으로 보고 건너뛴다. 그 판단을 못 하면 로비가 잠깐 죽었을 때 대기자 전원의 티켓이
 * 날아간다. 상한 기반 삭제는 신호와 무관하므로 가드와 상관없이 계속한다.
 */
export function classifyAbandonedTickets(
    tickets: MatchmakingTicket[],
    queues: QueuePolicy[],
    now: Date,
    heartbeatThresholdMs: number = HEARTBEAT_THRESHOLD_MS,
): AbandonedTicketResult {
    const ttlSecondsByQueueId = new Map(queues.map(queue => [queue.id, queue.ticketTtlSeconds]));

    //  이미 매치가 가져간 티켓은 이 판정의 대상이 아니다 — 지우는 경로가 따로 있고,
    //  그 주인은 게임 중이라 매칭 후보로도 삼으면 안 된다.
    const pool = tickets.filter(ticket => ticket.matchId === null);

    const heard = (ticket: MatchmakingTicket): boolean =>
        now.getTime() - ticket.lastHeartbeat.getTime() <= heartbeatThresholdMs;

    const signalAlive = pool.some(heard);
    const silenceSweepSkipped = pool.length > 0 && !signalAlive;

    const keep: MatchmakingTicket[] = [];
    const abandonedIds: string[] = [];

    for (const ticket of pool) {
        const ttlSeconds = ttlSecondsByQueueId.get(ticket.queueId);
        //  설정을 못 찾으면 상한을 적용하지 않는다 — 설정 누락으로 유저를 쫓아내지 않는다.
        const tooOld = ttlSeconds !== undefined && ttlSeconds > 0
            && now.getTime() - ticket.createdAt.getTime() > ttlSeconds * 1000;
        const silent = signalAlive && !heard(ticket);

        if (tooOld || silent) {
            abandonedIds.push(ticket.id);
        } else {
            keep.push(ticket);
        }
    }

    return { keep: keep, abandonedIds: abandonedIds, silenceSweepSkipped: silenceSweepSkipped };
}
```

- [ ] **Step 5: 통과 확인**

```bash
cd "C:/Users/re5na/workspace/LOP/lop-backend"
pnpm --filter matchmaking-server run test -- abandonedTickets
```

Expected: PASS (11건).

- [ ] **Step 6: 가드를 되돌려서 실제로 잡는지 확인**

`silenceSweepSkipped` 계산을 `const signalAlive = true;`로 잠깐 바꾸고 테스트를 돌린다.

Expected: **"풀 전체가 조용하면 신호 기반 삭제를 건너뛴다"가 실패**해야 한다. 확인 후 복구한다.

- [ ] **Step 7: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/lop-backend"
git add apps/matchmaking-server/src/director apps/matchmaking-server/src/director/__tests__
git commit -m "feat(matchmaking): 버려진 대기표 판정 순수 함수

버리는 이유 둘 — 신호 60초 끊김, 큐별 상한 초과 — 을 독립으로 판정한다.
신호 기반 삭제에는 가드를 뒀다: 풀에 최근 신호를 준 티켓이 하나도 없으면
여러 명이 동시에 죽은 게 아니라 신호 계통 장애로 보고 건너뛴다. 없으면
로비가 잠깐 죽었을 때 대기자 전원의 티켓이 날아간다.
상한 기반 삭제는 신호와 무관하므로 가드와 상관없이 계속한다."
```

---

## Task 5: Director 틱에 배선

**Files:**
- Modify: `lop-backend/apps/matchmaking-server/src/director/tick.ts`
- Modify: `lop-backend/apps/matchmaking-server/src/director.ts`
- Test: `lop-backend/apps/matchmaking-server/src/director/__tests__/tick.test.ts` (기존 파일)
- Test: `lop-backend/apps/matchmaking-server/test/integration/abandonedTicketSweep.integration.test.ts` (신규)

**Interfaces:**
- Consumes: `classifyAbandonedTickets`, `AbandonedTicketResult`(Task 4)
- Produces: `DirectorTickDeps.deleteAbandonedTickets(ids: string[]): Promise<void>`, `DirectorTickResult.abandonedTicketIds: string[]`, `DirectorTickResult.silenceSweepSkipped: boolean`

- [ ] **Step 1: 기존 테스트 헬퍼부터 고친다 (⚠️ 안 하면 런타임에 터진다)**

`apps/matchmaking-server/src/director/__tests__/tick.test.ts`에는 이미 헬퍼 셋이 있다 — `tables(maxWaitSeconds?)`, `ticket(id, userId, secondsAgo, queueId?)`, `deps(tickets, overrides?)`.

**⚠️ 이 파일의 `ticket()`은 `as MatchmakingTicket` 캐스트를 쓰고 `tables()`는 `as any`를 쓴다. 즉 필드를 빠뜨려도 컴파일러가 잡지 않고 런타임에 터진다** (`ticket.lastHeartbeat`가 `undefined`가 되어 `.getTime()`에서 TypeError). 먼저 세 헬퍼를 고친다.

`tables()`의 `queue` 객체에 한 줄 추가:

```typescript
        maxWaitSeconds: maxWaitSeconds,
        ticketTtlSeconds: 600,
        allowedGameModeIds: [1],
```

`ticket()`에 매개변수와 필드 추가 — 기본값은 `NOW`(살아 있음)로 두어 기존 테스트의 의도를 유지한다:

```typescript
function ticket(id: string, userId: string, secondsAgo: number, queueId = 1, heartbeatSecondsAgo = 0): MatchmakingTicket {
    return {
        id,
        userIds: [userId],
        queueId,
        gameModeIds: [1],
        mapIds: [1],
        rating: 1000,
        createdAt: new Date(NOW.getTime() - secondsAgo * 1000),
        lastHeartbeat: new Date(NOW.getTime() - heartbeatSecondsAgo * 1000),
        matchId: null,
    } as MatchmakingTicket;
}
```

`deps()`에 한 줄 추가 — **여기 한 곳만 고치면 기존 테스트 전부가 새 의존성을 얻는다**:

```typescript
        findAllTickets: async () => tickets,
        deleteAbandonedTickets: jest.fn(async () => {}),
```

- [ ] **Step 2: 틱 유닛 테스트 추가(실패해야 한다)**

같은 파일의 `describe('runDirectorTick', ...)` 안에 추가한다.

```typescript
    it('신호가 끊긴 티켓은 지우고 매칭 후보에서도 뺀다', async () => {
        const 지운것: string[] = [];
        //  다섯째 인자가 "신호를 준 지 몇 초 됐나"다. 120초 = 임계값(60초)을 넘겼다.
        const 조용함 = ticket('T-dead', 'u1', 5, 1, 120);
        const 살아있음 = ticket('T-live', 'u2', 5, 1, 0);

        const result = await runDirectorTick(NOW, deps([조용함, 살아있음], {
            deleteAbandonedTickets: async ids => { 지운것.push(...ids); },
        }));

        expect(지운것).toEqual(['T-dead']);
        expect(result.abandonedTicketIds).toEqual(['T-dead']);
        expect(result.silenceSweepSkipped).toBe(false);
    });

    it('버린 티켓은 매칭 후보에서도 빠진다 — 죽은 사람으로 판이 짜이면 안 된다', async () => {
        //  기본 게임모드는 최소 2명이다. 살아있는 한 명 + 죽은 한 명이면 매치가 생기면 안 된다.
        const result = await runDirectorTick(NOW, deps([
            ticket('T-dead', 'u1', 5, 1, 120),
            ticket('T-live', 'u2', 5, 1, 0),
        ]));

        expect(result.assignments).toEqual([]);
    });
```

- [ ] **Step 3: 실패 확인**

```bash
cd "C:/Users/re5na/workspace/LOP/lop-backend"
pnpm --filter matchmaking-server run test -- director/__tests__/tick
```

Expected: FAIL — `deleteAbandonedTickets`가 `DirectorTickDeps`에 없어 컴파일 에러(TS2353), 또는 `abandonedTicketIds`가 결과에 없음(TS2339).

- [ ] **Step 4: 틱 타입 확장**

`apps/matchmaking-server/src/director/tick.ts` 상단 import에 추가:

```typescript
import { classifyAbandonedTickets } from '@src/director/abandonedTickets';
```

`DirectorTickResult`에 두 필드 추가:

```typescript
export interface DirectorTickResult {
    assignments: AssignmentResult[];
    failures: string[];
    pools: PoolSummary[];
    purgedTicketIds: string[];
    /** 이번 틱에서 버린 대기표(신호 끊김 또는 상한 초과). */
    abandonedTicketIds: string[];
    /** 신호 계통 장애로 판단해 신호 기반 삭제를 건너뛰었나. */
    silenceSweepSkipped: boolean;
}
```

`DirectorTickDeps`에 의존성 추가(`findAllTickets` 아래):

```typescript
    /** 버려진 대기표를 지운다. */
    deleteAbandonedTickets(ids: string[]): Promise<void>;
```

- [ ] **Step 5: 틱 본문에 배선**

`runDirectorTick` 안에서 티켓을 읽는 줄을 교체한다.

바꾸기 전:

```typescript
    //  큐로 나누기 전에 거른다 — 같은 유저의 두 티켓이 서로 다른 큐에 있어도 두 매치에 들어가면 안 된다.
    const tickets = oneTicketPerUser(await deps.findAllTickets());
```

바꾼 뒤:

```typescript
    //  버려진 것부터 걷어낸다 — 죽은 대기표가 매치에 끼면 나머지 인원이 안 오는 사람을 기다린다.
    const swept = classifyAbandonedTickets(await deps.findAllTickets(), tables.TbQueue.getDataList(), now);
    if (swept.abandonedIds.length > 0) {
        await deps.deleteAbandonedTickets(swept.abandonedIds);
    }

    //  큐로 나누기 전에 거른다 — 같은 유저의 두 티켓이 서로 다른 큐에 있어도 두 매치에 들어가면 안 된다.
    const tickets = oneTicketPerUser(swept.keep);
```

그리고 반환문을 교체한다.

바꾸기 전:

```typescript
    return { assignments: assignments, failures: failures, pools: pools, purgedTicketIds: purgedTicketIds };
```

바꾼 뒤:

```typescript
    return {
        assignments: assignments,
        failures: failures,
        pools: pools,
        purgedTicketIds: purgedTicketIds,
        abandonedTicketIds: swept.abandonedIds,
        silenceSweepSkipped: swept.silenceSweepSkipped,
    };
```

- [ ] **Step 6: 통과 확인**

```bash
cd "C:/Users/re5na/workspace/LOP/lop-backend"
pnpm --filter matchmaking-server run test -- director/__tests__/tick
```

Expected: PASS.

- [ ] **Step 7: Director 배선**

`apps/matchmaking-server/src/director.ts`의 `runDirectorTick` 호출부에서 `findAllTickets` 아래에 추가:

```typescript
                    //  소비 티켓 정리와 같은 경로를 쓴다.
                    deleteAbandonedTickets: ids => matchmakingTicketService.deleteAllMatchmakingTicketsById(ids),
```

- [ ] **Step 8: 로깅 추가**

`director.ts`의 `report` 함수 안, 기존 로그들과 나란히 추가한다(assignment 루프 아래).

```typescript
    if (result.abandonedTicketIds.length > 0) {
        logger.info(`[director] abandoned tickets swept. count: ${result.abandonedTicketIds.length}, ticketIds: ${result.abandonedTicketIds.join(',')}`);
    }
    if (result.silenceSweepSkipped) {
        //  개인의 죽음이 아니라 신호 계통(로비 또는 그 사이 네트워크) 장애로 보고 건너뛴 것이다.
        logger.warn(`[director] heartbeat sweep skipped — no ticket in the pool was heard from recently`);
    }
```

- [ ] **Step 9: 통합 테스트 작성**

`apps/matchmaking-server/test/integration/abandonedTicketSweep.integration.test.ts` 생성:

```typescript
import { rawPrisma, resetTables, createTicket } from './db';
import { MatchmakingTicketRepository } from '@repositories/matchmakingTicket.repository';
import { classifyAbandonedTickets } from '@src/director/abandonedTickets';
import { QueuePolicy } from '@src/director/types';

const 큐: QueuePolicy = {
    id: 1, ratingRangeStart: 500, ratingRangeMax: 2000,
    ratingRelaxPerSec: 50, maxWaitSeconds: 10, ticketTtlSeconds: 600,
};

async function 신호시각을바꾼다(id: string, 초전: number): Promise<void> {
    await rawPrisma.matchmakingTicket.update({
        where: { id: id },
        data: { lastHeartbeat: new Date(Date.now() - 초전 * 1000) },
    });
}

/** 판정 함수와 실제 삭제 경로를 붙여, DB에서 정말로 사라지는지까지 본다. */
async function 쓸어담기(): Promise<string[]> {
    const repository = new MatchmakingTicketRepository();
    const pool = await repository.findAllUnconsumed();
    const swept = classifyAbandonedTickets(pool, [큐], new Date());
    if (swept.abandonedIds.length > 0) {
        await repository.deleteAllById(swept.abandonedIds);
    }
    return swept.abandonedIds;
}

describe('버려진 대기표 쓸어담기', () => {
    beforeEach(resetTables);
    afterAll(async () => { await rawPrisma.$disconnect(); });

    it('신호가 끊긴 티켓은 DB에서 사라진다', async () => {
        await createTicket('T-dead', 'U1');
        await createTicket('T-live', 'U2');
        await 신호시각을바꾼다('T-dead', 120);

        expect(await 쓸어담기()).toEqual(['T-dead']);

        expect(await rawPrisma.matchmakingTicket.findUnique({ where: { id: 'T-dead' } })).toBeNull();
        expect(await rawPrisma.matchmakingTicket.findUnique({ where: { id: 'T-live' } })).not.toBeNull();
    });

    //  반대 방향도 봐야 진짜 테스트다 — 무조건 지우는 코드도 위 테스트는 통과한다.
    it('방금 신호를 준 티켓은 그대로 있다', async () => {
        await createTicket('T1', 'U1');

        expect(await 쓸어담기()).toEqual([]);

        expect(await rawPrisma.matchmakingTicket.findUnique({ where: { id: 'T1' } })).not.toBeNull();
    });

    it('상한을 넘긴 티켓은 신호가 멀쩡해도 사라진다', async () => {
        await createTicket('T-old', 'U1');
        await createTicket('T-new', 'U2');
        await rawPrisma.matchmakingTicket.update({
            where: { id: 'T-old' },
            data: { createdAt: new Date(Date.now() - 601 * 1000) },
        });

        expect(await 쓸어담기()).toEqual(['T-old']);

        expect(await rawPrisma.matchmakingTicket.findUnique({ where: { id: 'T-old' } })).toBeNull();
    });

    it('매치가 가져간 티켓은 아무리 조용해도 남는다', async () => {
        await createTicket('T-consumed', 'U1');
        await createTicket('T-live', 'U2');
        await rawPrisma.matchmakingTicket.update({
            where: { id: 'T-consumed' },
            data: { matchId: 'M1', lastHeartbeat: new Date(Date.now() - 9999 * 1000) },
        });

        expect(await 쓸어담기()).toEqual([]);

        expect(await rawPrisma.matchmakingTicket.findUnique({ where: { id: 'T-consumed' } })).not.toBeNull();
    });

    it('풀 전체가 조용하면 아무것도 지우지 않는다 — 신호 계통 장애로 본다', async () => {
        await createTicket('T1', 'U1');
        await createTicket('T2', 'U2');
        await 신호시각을바꾼다('T1', 300);
        await 신호시각을바꾼다('T2', 300);

        expect(await 쓸어담기()).toEqual([]);

        expect(await rawPrisma.matchmakingTicket.count()).toBe(2);
    });
});
```

- [ ] **Step 10: 통합 테스트 실행**

```bash
cd "C:/Users/re5na/workspace/LOP/lop-backend"
pnpm --filter matchmaking-server run test:integration -- abandonedTicketSweep
```

Expected: PASS (5건).

- [ ] **Step 11: 되돌려서 실제로 잡는지 확인**

`abandonedTickets.ts`의 `matchId === null` 필터를 잠깐 지우고 통합 테스트를 돌린다.

Expected: **"매치가 가져간 티켓은 아무리 조용해도 남는다"가 실패**해야 한다. 확인 후 복구한다.

- [ ] **Step 12: 전체 검증**

```bash
cd "C:/Users/re5na/workspace/LOP/lop-backend"
pnpm build
pnpm test
pnpm --filter matchmaking-server run test:integration
pnpm --filter lobby-server run test:integration
```

Expected: 빌드 5/5, 모든 테스트 PASS.

- [ ] **Step 13: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/lop-backend"
git add apps/matchmaking-server
git commit -m "feat(matchmaking): Director 틱이 버려진 대기표를 쓸어담는다

풀을 읽은 직후, 매칭 후보를 고르기 전에 판정·삭제한다. 별도 스케줄러를 두지
않은 이유는 우리가 원하는 보장이 'Director가 죽은 티켓을 매칭하지 않는다'라서다
— 같은 틱 안에서 지워야 그 보장이 확실하고, 별도 스케줄러는 틱과 경합한다.

삭제 이후 유저를 로비로 되돌리는 일은 로비 자가치유가 이미 한다(클라 변경 0).
건너뛴 경우는 warn 으로 남긴다 — 조용히 넘어가면 신호 계통 장애가 안 보인다."
```

---

## 배포 후 확인

- [ ] **Step 1: 배포**

```bash
cd "C:/Users/re5na/workspace/LOP/lop-backend"
gh workflow run backend-deploy.yml -f app=all --ref main
```

**`app=all`이어야 한다** — 마이그레이션이 있으므로 `db-migrate`가 빠지면 PreSync가 아무것도 적용하지 않는다.

- [ ] **Step 2: 롤아웃과 로그 확인**

```bash
kubectl rollout status deploy/matchmaking-director --timeout=180s
kubectl logs deploy/matchmaking-director --tail=100 | grep -iE "abandoned|skipped|error"
```

Expected: 에러 없음. 대기자가 없으면 sweep 로그도 안 나오는 것이 정상이다.

- [ ] **Step 3: 손으로 재현**

1. 클라를 켜고 매칭을 건다
2. **클라를 강제 종료**한다(취소를 누르지 않는다)
3. 60~65초 뒤 로그에 `abandoned tickets swept` 가 뜨는지 본다
4. DB에서 그 티켓이 사라졌는지 확인한다

- [ ] **Step 4: 정상 대기가 안 끊기는지 확인**

클라를 켜 두고 매칭을 건 채 **2분 이상 방치**한다. 대기표가 그대로 있어야 한다(10분 상한 전까지).

---

## 후속 (이 계획 범위 밖)

- **"매칭 실패" 알림** — 지금은 아무 설명 없이 로비로 복귀한다. 유저 위치 재정비 트랙에서
- **매치 수락 팝업** — 유령 플레이어의 AAA 표준 방어선. 클라 UI + 해체·복귀 흐름이라 별도 트랙
- **`matchmakingTicket.service.ts`의 철 지난 주석** — "캐시(TTL 5분)에 티켓이 남아"라고 적혀 있는데
  그 캐시는 2026-08-05에 제거됐다. 이 파일을 손대는 태스크(Task 3)에서 함께 고칠 것
