# `@lop/server-core` 서브패스 `exports` 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `@lop/server-core`의 루트 진입점에서 외부 자원을 잡는 것을 전부 서브패스로 밀어, 루트를 import 해도 아무 자원도 생기지 않게 한다.

**Architecture:** 패키지에 진입점 파일 5개(`src/entries/*.ts`)와 `exports` 맵을 먼저 추가한다(이 시점엔 루트 배럴이 그대로라 아무것도 안 깨진다). 그다음 앱 3개의 import 30곳을 서브패스로 옮기고, **마지막에** 루트 배럴에서 무거운 재수출을 걷어낸다. 되돌리기 쉬운 순서다.

**Tech Stack:** pnpm workspace, turbo, TypeScript 5.7 (`module`/`moduleResolution`: `node16`), jest 30 + ts-jest, Node CJS

**작업 저장소:** `C:\Users\re5na\workspace\LOP\lop-backend` (설계 문서는 클라 repo에 있지만 **코드 변경은 전부 lop-backend**)

**설계 문서:** `docs/superpowers/specs/2026-08-01-server-core-subpath-exports-design.md` (클라 repo)

## Global Constraints

- **`exports` 맵의 모든 항목은 자기 `types`를 가져야 한다.** `exports`가 존재하면 최상위 `types` 필드는 그 경로에 적용되지 않는다(dotenv@10이 이것으로 깨졌다).
- **조건 객체 안에서 `types`는 맨 앞.** Node는 키 순서대로 매칭하며 앞 항목이 우선한다.
- **최상위 `main`/`types`는 지운다 아니라 유지한다** — `exports`를 무시하는 낡은 도구용 폴백(TS 문서 권고).
- **산출물은 CJS 유지.** 어느 `package.json`에도 `"type": "module"`을 넣지 않는다.
- **서브패스 이름은 `logger` / `postgres` / `redis` / `mongoose` / `express` 다섯 개 고정.** 새로 만들지 않는다.
- **로더 심볼 이름을 바꾸지 않는다** — `mongooseLoader`/`postgresLoader`/`redisLoader` 네임스페이스 이름 그대로 유지(`export * as`). 호출부는 경로만 바뀐다.
- **한글 주석.** 새로 다는 주석은 "왜"만 짧게, 일상어로.
- 커밋 메시지는 한국어. 각 태스크 끝에 커밋.

## File Structure

**새로 만드는 것** (`packages/server-core/src/entries/`):

| 파일 | 책임 |
|---|---|
| `logger.ts` | winston 로거 재수출 |
| `postgres.ts` | postgres 연결 설정 + 로더 + `prismaClient` |
| `redis.ts` | redis 연결 설정 + 로더 + `redisClient` + `RedisCache` + `DaoRedisBase` |
| `mongoose.ts` | mongodb 연결 설정 + 로더 |
| `express.ts` | `App` + 라우트/컨트롤러/미들웨어 |

**고치는 것:**

| 파일 | 무엇 |
|---|---|
| `packages/server-core/package.json` | `exports` 맵 추가 |
| `packages/server-core/src/index.ts` | (Task 5) 무거운 재수출 제거 |
| `apps/matchmaking-server/jest.config.js` | 서브패스 매퍼 추가 |
| `apps/room-server/jest.config.js` | 서브패스 매퍼 추가 |
| 앱 소스 30개 | import 경로 분리 |

---

### Task 1: 서브패스 진입점 + `exports` 맵 + jest 매퍼

루트 배럴은 **건드리지 않는다.** 이 태스크가 끝나도 기존 import 82곳이 전부 그대로 동작해야 한다.

**Files:**
- Create: `packages/server-core/src/entries/logger.ts`
- Create: `packages/server-core/src/entries/postgres.ts`
- Create: `packages/server-core/src/entries/redis.ts`
- Create: `packages/server-core/src/entries/mongoose.ts`
- Create: `packages/server-core/src/entries/express.ts`
- Modify: `packages/server-core/package.json`
- Modify: `apps/matchmaking-server/jest.config.js`
- Modify: `apps/room-server/jest.config.js`

**Interfaces:**
- Produces: 서브패스 5개. 각각이 내보내는 심볼 이름은 **현재 루트 배럴이 내보내는 이름과 동일하다** — `logger`, `stream` / `postgresConnection`, `postgresLoader`, `prismaClient` / `redisConnection`, `redisLoader`, `redisClient`, `RedisCache`, `DaoRedisBase` / `mongodbConnection`, `mongooseLoader` / `App`, `IndexRoute`, `IndexController`, `errorMiddleware`, `validationMiddleware`

- [ ] **Step 1: 진입점 5개 작성**

`packages/server-core/src/entries/logger.ts`:
```typescript
export { logger, stream } from '../utils/logger';
```

`packages/server-core/src/entries/postgres.ts`:
```typescript
export * from '../databases/postgres';
export * as postgresLoader from '../loaders/postgres.loader';
export { prismaClient } from '../loaders/postgres.loader';
```

`packages/server-core/src/entries/redis.ts`:
```typescript
export * from '../caches';
export { RedisCache } from '../caches/redis.cache';
export { DaoRedisBase } from '../daos/dao.redis.base';
export * as redisLoader from '../loaders/redis.loader';
export { redisClient } from '../loaders/redis.loader';
```

`packages/server-core/src/entries/mongoose.ts`:
```typescript
export * from '../databases/mongodb';
export * as mongooseLoader from '../loaders/mongoose.loader';
```

`packages/server-core/src/entries/express.ts`:
```typescript
export { default as App } from '../app';
export { default as IndexController } from '../controllers/index.controller';
export { default as errorMiddleware } from '../middlewares/error.middleware';
export { default as validationMiddleware } from '../middlewares/validation.middleware';
export { default as IndexRoute } from '../routes/index.route';
```

- [ ] **Step 2: `exports` 맵 추가**

`packages/server-core/package.json`에서 `"types": "./dist/index.d.ts",` 바로 다음 줄에 아래를 삽입한다. **`main`/`types`는 지우지 않는다.**

> ⚠️ **주석은 `exports` 밖에 둔다.** Node는 `exports` 안의 키가 하나라도 `.`로 시작하면 **전부** 그래야 한다고 검증한다 — `"//"` 같은 주석 키를 넣으면 `ERR_INVALID_PACKAGE_CONFIG`로 패키지 자체가 로드 불가가 된다(`turbo.json`에서 쓰는 `"//"` 관습이 여기선 안 통한다). 그래서 최상위 형제 키 `"//exports"`로 뺀다.

```json
    "//exports": "항목마다 types를 갖고 맨 앞에 둔다 — exports가 있으면 최상위 types 필드가 이 경로엔 적용되지 않고, 조건은 적힌 순서대로 매칭된다.",
    "exports": {
        ".": { "types": "./dist/index.d.ts", "default": "./dist/index.js" },
        "./logger": { "types": "./dist/entries/logger.d.ts", "default": "./dist/entries/logger.js" },
        "./postgres": { "types": "./dist/entries/postgres.d.ts", "default": "./dist/entries/postgres.js" },
        "./redis": { "types": "./dist/entries/redis.d.ts", "default": "./dist/entries/redis.js" },
        "./mongoose": { "types": "./dist/entries/mongoose.d.ts", "default": "./dist/entries/mongoose.js" },
        "./express": { "types": "./dist/entries/express.d.ts", "default": "./dist/entries/express.js" },
        "./package.json": "./package.json"
    },
```

- [ ] **Step 2b: `typesVersions`도 함께 추가 (ts-jest용)**

**`exports`만으로는 부족하다 — ts-jest는 `exports` 맵을 아예 읽지 않는다.** 실측으로 확인했다:
`exports["."].types`를 없는 파일로 바꿔도 기존 테스트가 그대로 통과한다(= 최상위 `types` 필드로 폴백,
즉 node10 방식 해석). 루트는 최상위 `types` 덕에 우연히 살아 있지만 **서브패스는 node10 대응물이 없어**
`TS2307: Cannot find module '@lop/server-core/logger'`가 난다.

`typesVersions`가 바로 그 node10 해석용 서브패스 타입 매핑 기구다. `exports` 바로 아래에 넣는다:

```json
    "//typesVersions": "ts-jest는 exports 맵을 읽지 않고 node10 방식으로 해석한다 — 서브패스 타입을 여기로도 알려 준다. node16 tsc는 exports가 우선이라 이 항목을 보지 않는다.",
    "typesVersions": {
        "*": {
            "logger": ["dist/entries/logger.d.ts"],
            "postgres": ["dist/entries/postgres.d.ts"],
            "redis": ["dist/entries/redis.d.ts"],
            "mongoose": ["dist/entries/mongoose.d.ts"],
            "express": ["dist/entries/express.d.ts"]
        }
    },
```

> ⛔ **`isolatedModules: true`를 쓰지 말 것.** ts-jest가 `TS151002` 경고로 그것을 권하고, 실제로 그걸
> 켜면 이 에러가 사라진다. 하지만 그건 고치는 게 아니라 **타입 검사를 통째로 끄는 것**이다 — 명백한
> 타입 오류(`const x: number = "문자열"`)가 그대로 통과함을 실측했다. 게다가 앱 tsconfig가
> `src/**/__tests__/**`를 exclude하므로, 그러면 **테스트 파일은 어디서도 타입 검사를 못 받는다.**

- [ ] **Step 3: 빌드해서 진입점이 나오는지 확인**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm exec turbo run build --filter=@lop/server-core --force`
Expected: 성공. 이어서 `ls packages/server-core/dist/entries/` 가 `.js`와 `.d.ts` 각 5개를 보여야 한다.

- [ ] **Step 4: 서브패스가 실제로 해석되는지 확인 (node 런타임)**

`apps/matchmaking-server/`에서:

Run: `node -e "const r=require('@lop/server-core/redis'); const l=require('@lop/server-core/logger'); console.log(typeof r.redisClient, typeof r.DaoRedisBase, typeof l.logger.info)"`
Expected: `object function function`

Run: `node -e "require('@lop/server-core/dist/index.js')"`
Expected: **실패** — `ERR_PACKAGE_PATH_NOT_EXPORTED`. deep import가 막혔다는 증거다.

- [ ] **Step 5: jest 매퍼 추가**

`apps/matchmaking-server/jest.config.js`와 `apps/room-server/jest.config.js` 양쪽에서, 기존 줄
```js
        '^@lop/server-core$': '<rootDir>/../../packages/server-core/src',
```
을 아래 두 줄로 교체한다:
```js
        //  jest는 package.json의 exports 맵을 보지 않는다. 서브패스를 여기 적지 않으면 그것만 node 해석을
        //  타고 dist로 새어, 한 테스트 안에 src와 dist 두 벌이 뜬다.
        '^@lop/server-core$': '<rootDir>/../../packages/server-core/src',
        '^@lop/server-core/(.*)$': '<rootDir>/../../packages/server-core/src/entries/$1',
```

- [ ] **Step 6: 기존 테스트가 전부 그대로 통과하는지 확인**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm exec turbo run test`
Expected: matchmaking 19 suites / 154 tests, room 1 suite / 11 tests 모두 PASS. **실패 0.**

- [ ] **Step 7: 매퍼가 src를 태우는지 확인 (dist가 아니라)**

`apps/matchmaking-server/`에 임시 파일 `src/__tests__/subpath.probe.test.ts`를 만든다:
```typescript
import { logger } from '@lop/server-core/logger';

//  매퍼가 dist가 아니라 src를 태우는지 확인한다 — 두 벌이 뜨면 여기서 잡힌다.
it('서브패스가 패키지 소스로 해석된다', () => {
    const resolved = require.resolve('@lop/server-core/logger');
    expect(resolved.replace(/\\/g, '/')).toContain('/packages/server-core/src/entries/');
    expect(resolved.replace(/\\/g, '/')).not.toContain('/dist/');
    expect(typeof logger.info).toBe('function');
});
```

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server && npx jest src/__tests__/subpath.probe.test.ts`
Expected: PASS

그다음 이 파일을 **삭제한다**(일회성 확인용):
Run: `rm apps/matchmaking-server/src/__tests__/subpath.probe.test.ts`

- [ ] **Step 8: 타입체크 전체**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm exec turbo run build --force`
Expected: 5개 프로젝트 전부 성공

- [ ] **Step 9: 커밋**

```bash
git add packages/server-core/src/entries packages/server-core/package.json apps/matchmaking-server/jest.config.js apps/room-server/jest.config.js
git commit -m "feat(server-core): 서브패스 진입점 5개 + exports 맵 추가

루트 배럴은 그대로 두어 기존 import 82곳이 전부 동작한다.
jest는 exports를 안 보므로 moduleNameMapper에 서브패스를 함께 넣었다."
```

---

### Task 2: lobby-server 13개 파일 마이그레이션

**Files:**
- Modify: 아래 13개

**Interfaces:**
- Consumes: Task 1이 만든 서브패스 5개

- [ ] **Step 1: import 교체 (아래 13개, 각각 "기존" 줄을 "변경" 줄들로)**

```
apps/lobby-server/src/daos/user-location.dao.postgres.ts
  기존: import { DaoPostgresBase, prismaClient } from '@lop/server-core';
  변경: import { DaoPostgresBase } from '@lop/server-core';
        import { prismaClient } from '@lop/server-core/postgres';

apps/lobby-server/src/daos/user-profile.dao.postgres.ts
  기존: import { DaoPostgresBase, prismaClient } from '@lop/server-core';
  변경: import { DaoPostgresBase } from '@lop/server-core';
        import { prismaClient } from '@lop/server-core/postgres';

apps/lobby-server/src/daos/user-stats.dao.postgres.ts
  기존: import { DaoPostgresBase, prismaClient } from '@lop/server-core';
  변경: import { DaoPostgresBase } from '@lop/server-core';
        import { prismaClient } from '@lop/server-core/postgres';

apps/lobby-server/src/daos/user.dao.postgres.ts
  기존: import { DaoPostgresBase, prismaClient } from '@lop/server-core';
  변경: import { DaoPostgresBase } from '@lop/server-core';
        import { prismaClient } from '@lop/server-core/postgres';

apps/lobby-server/src/daos/user-location.dao.redis.ts
  기존: import { DaoRedisBase } from '@lop/server-core';
  변경: import { DaoRedisBase } from '@lop/server-core/redis';

apps/lobby-server/src/daos/user-profile.dao.redis.ts
  기존: import { DaoRedisBase } from '@lop/server-core';
  변경: import { DaoRedisBase } from '@lop/server-core/redis';

apps/lobby-server/src/daos/user-stats.dao.redis.ts
  기존: import { DaoRedisBase } from '@lop/server-core';
  변경: import { DaoRedisBase } from '@lop/server-core/redis';

apps/lobby-server/src/daos/user.dao.redis.ts
  기존: import { DaoRedisBase } from '@lop/server-core';
  변경: import { DaoRedisBase } from '@lop/server-core/redis';

apps/lobby-server/src/loaders/index.ts
  기존: import { mongooseLoader, postgresLoader, redisLoader, logger } from '@lop/server-core';
  변경: import { logger } from '@lop/server-core/logger';
        import { mongooseLoader } from '@lop/server-core/mongoose';
        import { postgresLoader } from '@lop/server-core/postgres';
        import { redisLoader } from '@lop/server-core/redis';

apps/lobby-server/src/main.ts
  기존: import { App, IndexRoute, validateEnv, logger } from '@lop/server-core';
  변경: import { validateEnv } from '@lop/server-core';
        import { App, IndexRoute } from '@lop/server-core/express';
        import { logger } from '@lop/server-core/logger';

apps/lobby-server/src/routes/user-location.route.ts
  기존: import { Routes, validationMiddleware } from '@lop/server-core';
  변경: import { Routes } from '@lop/server-core';
        import { validationMiddleware } from '@lop/server-core/express';

apps/lobby-server/src/routes/user-profile.route.ts
  기존: import { Routes, validationMiddleware } from '@lop/server-core';
  변경: import { Routes } from '@lop/server-core';
        import { validationMiddleware } from '@lop/server-core/express';

apps/lobby-server/src/routes/user.route.ts
  기존: import { Routes, validationMiddleware } from '@lop/server-core';
  변경: import { Routes } from '@lop/server-core';
        import { validationMiddleware } from '@lop/server-core/express';
```

> `import` 줄의 위치는 원래 줄 자리를 지킨다. 다른 줄은 건드리지 않는다.

- [ ] **Step 2: 남은 게 없는지 확인**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && grep -rn "prismaClient\|DaoRedisBase\|mongooseLoader\|postgresLoader\|redisLoader\|validationMiddleware\|App,\|IndexRoute\|logger" apps/lobby-server/src --include=*.ts | grep "from '@lop/server-core'"`
Expected: 출력 없음

- [ ] **Step 3: 빌드**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm exec turbo run build --filter=lobby-server --force`
Expected: 성공

- [ ] **Step 4: 커밋**

```bash
git add apps/lobby-server
git commit -m "refactor(lobby): server-core import를 서브패스로 분리"
```

---

### Task 3: matchmaking-server 9개 파일 + jest.mock 2곳

**Files:**
- Modify: 아래 9개 + 테스트 2개

- [ ] **Step 1: import 교체 (9개)**

```
apps/matchmaking-server/src/daos/match.dao.postgres.ts
  기존: import { DaoPostgresBase, prismaClient } from '@lop/server-core';
  변경: import { DaoPostgresBase } from '@lop/server-core';
        import { prismaClient } from '@lop/server-core/postgres';

apps/matchmaking-server/src/daos/matchmakingTicket.dao.postgres.ts
  기존: import { DaoPostgresBase, prismaClient } from '@lop/server-core';
  변경: import { DaoPostgresBase } from '@lop/server-core';
        import { prismaClient } from '@lop/server-core/postgres';

apps/matchmaking-server/src/daos/matchRound.dao.postgres.ts
  기존: import { DaoPostgresBase, prismaClient } from '@lop/server-core';
  변경: import { DaoPostgresBase } from '@lop/server-core';
        import { prismaClient } from '@lop/server-core/postgres';

apps/matchmaking-server/src/daos/matchmakingTicket.dao.redis.ts
  기존: import { DaoRedisBase } from '@lop/server-core';
  변경: import { DaoRedisBase } from '@lop/server-core/redis';

apps/matchmaking-server/src/director.ts
  기존: import { validateEnv, logger } from '@lop/server-core';
  변경: import { validateEnv } from '@lop/server-core';
        import { logger } from '@lop/server-core/logger';

apps/matchmaking-server/src/loaders/index.ts
  기존: import { mongooseLoader, postgresLoader, redisLoader, logger } from '@lop/server-core';
  변경: import { logger } from '@lop/server-core/logger';
        import { mongooseLoader } from '@lop/server-core/mongoose';
        import { postgresLoader } from '@lop/server-core/postgres';
        import { redisLoader } from '@lop/server-core/redis';

apps/matchmaking-server/src/main.ts
  기존: import { App, IndexRoute, validateEnv, logger } from '@lop/server-core';
  변경: import { validateEnv } from '@lop/server-core';
        import { App, IndexRoute } from '@lop/server-core/express';
        import { logger } from '@lop/server-core/logger';

apps/matchmaking-server/src/routes/matchmaking.route.ts
  기존: import { Routes, validationMiddleware } from '@lop/server-core';
  변경: import { Routes } from '@lop/server-core';
        import { validationMiddleware } from '@lop/server-core/express';

apps/matchmaking-server/src/routes/matchmakingTicket.route.ts
  기존: import { Routes, validationMiddleware } from '@lop/server-core';
  변경: import { Routes } from '@lop/server-core';
        import { validationMiddleware } from '@lop/server-core/express';
```

- [ ] **Step 2: `jest.mock` 대상 옮기기**

`prismaClient`가 `/postgres`로 갔으므로 그것을 스텁하는 mock도 따라간다. **`requireActual` 스프레드는 없앤다** — 예전엔 배럴 하나를 통째로 모킹해서 나머지 실제 export를 살려야 했지만, 이제 모킹 대상이 `/postgres` 하나뿐이고 이 파일들이 거기서 쓰는 건 `prismaClient`뿐이다. 스프레드를 빼면 테스트가 진짜 `PrismaClient` 인스턴스를 만들지 않는다.

`apps/matchmaking-server/src/daos/__tests__/match.dao.postgres.test.ts` — 아래 블록을

```typescript
//  prismaClient이 @loaders/postgres.loader에서 @lop/server-core로 옮겨졌다(Task 4) — DaoPostgresBase 등
//  이 파일이 쓰는 나머지 실제 export는 requireActual로 살려두고 prismaClient만 모킹으로 덮어쓴다.
jest.mock('@lop/server-core', () => ({
    ...jest.requireActual('@lop/server-core'),
    prismaClient: {
        match: { upsert },
        matchRound: { deleteMany, createMany },
        matchmakingTicket: { updateMany, findMany },
        $transaction,
    },
}));
```

이렇게 바꾼다:

```typescript
//  prismaClient는 @lop/server-core/postgres에 있다. 이 서브패스에서 쓰는 게 그것뿐이라 통째로 대체한다
//  — 실제 PrismaClient 인스턴스가 만들어지지 않는다.
jest.mock('@lop/server-core/postgres', () => ({
    prismaClient: {
        match: { upsert },
        matchRound: { deleteMany, createMany },
        matchmakingTicket: { updateMany, findMany },
        $transaction,
    },
}));
```

`apps/matchmaking-server/src/daos/__tests__/matchmakingTicket.dao.postgres.test.ts` — 아래 블록을

```typescript
//  prismaClient이 @loaders/postgres.loader에서 @lop/server-core로 옮겨졌다(Task 4) — DaoPostgresBase 등
//  이 파일이 쓰는 나머지 실제 export는 requireActual로 살려두고 prismaClient만 모킹으로 덮어쓴다.
jest.mock('@lop/server-core', () => ({
    ...jest.requireActual('@lop/server-core'),
    prismaClient: {
        matchmakingTicket: { deleteMany, findMany },
    },
}));
```

이렇게 바꾼다:

```typescript
//  prismaClient는 @lop/server-core/postgres에 있다. 이 서브패스에서 쓰는 게 그것뿐이라 통째로 대체한다
//  — 실제 PrismaClient 인스턴스가 만들어지지 않는다.
jest.mock('@lop/server-core/postgres', () => ({
    prismaClient: {
        matchmakingTicket: { deleteMany, findMany },
    },
}));
```

> 만약 테스트가 `@lop/server-core/postgres`의 다른 export가 없다고 실패하면, 그때만
> `...jest.requireActual('@lop/server-core/postgres'),`를 첫 줄에 되살린다.

- [ ] **Step 3: 남은 게 없는지 확인**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && grep -rn "prismaClient\|DaoRedisBase\|mongooseLoader\|postgresLoader\|redisLoader\|validationMiddleware\|IndexRoute\|logger" apps/matchmaking-server/src --include=*.ts | grep "server-core'"`
Expected: 출력 없음 (`server-core/postgres'` 등 서브패스는 걸리지 않는다)

- [ ] **Step 4: 빌드 + 테스트**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm exec turbo run build --filter=matchmaking-server --force && cd apps/matchmaking-server && npx jest`
Expected: 빌드 성공, **19 suites / 154 tests PASS**

- [ ] **Step 5: 커밋**

```bash
git add apps/matchmaking-server
git commit -m "refactor(matchmaking): server-core import를 서브패스로 분리

prismaClient가 /postgres로 갔으므로 jest.mock 대상도 함께 옮겼다."
```

---

### Task 4: room-server 8개 파일

**Files:**
- Modify: 아래 8개

- [ ] **Step 1: import 교체 (8개)**

```
apps/room-server/src/daos/room.dao.postgres.ts
  기존: import { DaoPostgresBase, prismaClient } from '@lop/server-core';
  변경: import { DaoPostgresBase } from '@lop/server-core';
        import { prismaClient } from '@lop/server-core/postgres';

apps/room-server/src/daos/room.dao.redis.ts
  기존: import { DaoRedisBase, redisClient } from '@lop/server-core';
  변경: import { DaoRedisBase, redisClient } from '@lop/server-core/redis';

apps/room-server/src/loaders/index.ts
  기존: import { mongooseLoader, postgresLoader, redisLoader, logger } from '@lop/server-core';
  변경: import { logger } from '@lop/server-core/logger';
        import { mongooseLoader } from '@lop/server-core/mongoose';
        import { postgresLoader } from '@lop/server-core/postgres';
        import { redisLoader } from '@lop/server-core/redis';

apps/room-server/src/main.ts
  기존: import { App, IndexRoute, validateEnv, logger } from '@lop/server-core';
  변경: import { validateEnv } from '@lop/server-core';
        import { App, IndexRoute } from '@lop/server-core/express';
        import { logger } from '@lop/server-core/logger';

apps/room-server/src/routes/room.route.ts
  기존: import { Routes, validationMiddleware } from '@lop/server-core';
  변경: import { Routes } from '@lop/server-core';
        import { validationMiddleware } from '@lop/server-core/express';

apps/room-server/src/schedulers/index.ts
  기존: import { logger } from '@lop/server-core';
  변경: import { logger } from '@lop/server-core/logger';

apps/room-server/src/schedulers/room.scheduler.ts
  기존: import { logger } from '@lop/server-core';
  변경: import { logger } from '@lop/server-core/logger';

apps/room-server/src/utils/k8sUtils.ts
  기존: import { logger } from '@lop/server-core';
  변경: import { logger } from '@lop/server-core/logger';
```

- [ ] **Step 2: 남은 게 없는지 확인**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && grep -rn "prismaClient\|DaoRedisBase\|redisClient\|mongooseLoader\|postgresLoader\|redisLoader\|validationMiddleware\|IndexRoute\|logger" apps/room-server/src --include=*.ts | grep "server-core'"`
Expected: 출력 없음

- [ ] **Step 3: 빌드 + 테스트**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm exec turbo run build --filter=room-server --force && cd apps/room-server && npx jest`
Expected: 빌드 성공, **1 suite / 11 tests PASS**

- [ ] **Step 4: 커밋**

```bash
git add apps/room-server
git commit -m "refactor(room): server-core import를 서브패스로 분리"
```

---

### Task 5: 루트 배럴에서 무거운 재수출 제거 (keystone)

여기서 실제 효과가 난다. 앞의 세 태스크가 끝나 **루트를 통해 무거운 심볼을 가져가는 앱 파일이 0개**여야 한다.

**Files:**
- Modify: `packages/server-core/src/index.ts`

- [ ] **Step 1: 시작 전 비용 측정 (기준선)**

`apps/matchmaking-server/`에서:

Run: `node -e "const t=Date.now();require('@lop/server-core');console.log((Date.now()-t)+'ms '+Object.keys(require.cache).length+' modules')"`
Expected: 대략 `1600ms 1500 modules` 근처. **이 숫자를 적어 둔다.**

- [ ] **Step 2: 루트 배럴 교체**

`packages/server-core/src/index.ts` 전체를 아래로 바꾼다:

```typescript
//  루트는 "순수 계약"만 내보낸다 — 여기 있는 것은 import 해도 외부 자원(DB 클라이언트, redis 연결,
//  로그 파일, express 앱)을 하나도 만들지 않는다. 자원을 잡는 것은 서브패스로 나가 있다:
//  @lop/server-core/{logger,postgres,redis,mongoose,express}
export * from './config';

export * from './exceptions/HttpException';

export * from './daos/dao.interface';
export * from './daos/dao.mongoose.base';
export * from './daos/dao.postgres.base';

export * from './interfaces/responseBase.interface';
export * from './interfaces/routes.interface';
export * from './interfaces/user-location.interface';

export * from './mappers/domain.entity.mapper';

export * from './repositories/repository.interface';
export * from './repositories/crudRepository.interface';
export * from './repositories/cacheCrudRepository';

export * from './utils/redis-json.utils';
export { default as validateEnv } from './utils/validateEnv';
```

- [ ] **Step 3: 빌드 — 여기서 깨지면 놓친 소비처가 있다는 뜻**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm exec turbo run build --force`
Expected: 5개 프로젝트 전부 성공. 실패하면 그 파일이 아직 루트에서 무거운 심볼을 가져가는 것이므로 서브패스로 고친다.

- [ ] **Step 4: 효과 측정**

`apps/matchmaking-server/`에서:

Run: `node -e "const t=Date.now();require('@lop/server-core');console.log((Date.now()-t)+'ms '+Object.keys(require.cache).length+' modules')"`
Expected: **50ms 미만 / 20 모듈 미만.** Step 1의 숫자와 함께 커밋 메시지에 적는다.

- [ ] **Step 5: 자원이 안 생기는지 직접 확인**

`apps/matchmaking-server/`에서:

Run: `node -e "const before=Object.keys(require.cache).length;require('@lop/server-core');const has=n=>Object.keys(require.cache).some(k=>k.includes('node_modules'+require('path').sep+n));console.log('redis:',has('redis'),'mongoose:',has('mongoose'),'express:',has('express'),'winston:',has('winston'))"`
Expected: `redis: false mongoose: false express: false winston: false`

- [ ] **Step 6: 전체 테스트**

Run: `cd /c/Users/re5na/workspace/LOP/lop-backend && pnpm exec turbo run test`
Expected: 154 + 11 전부 PASS

- [ ] **Step 7: 커밋**

```bash
git add packages/server-core/src/index.ts
git commit -m "refactor(server-core): 루트 배럴을 순수 계약만 남기고 비움

루트 import 비용: <Step1 숫자> -> <Step4 숫자>.
자원을 잡는 것은 전부 서브패스(logger/postgres/redis/mongoose/express)로 나갔다."
```

---

## 실행 후 (컨트롤러가 직접)

태스크 5개가 끝나면 아래를 순서대로 확인한다. 이것들은 서브에이전트에게 맡기지 않는다.

1. **로컬 `docker build` 3종** — 로컬 `pnpm build`는 워크스페이스 hoisting이 문제를 가린다(전례 있음).
2. **CI 워크플로 점검** — `.github/workflows/backend-deploy.yml`이 `turbo run build --filter`를 쓰는지(이미 그렇다) 재확인.
3. **배포** — `app: all`, 마이그레이션 없음.
4. **E2E** — 4파드 기동 + 에러 0 + 2클라 매칭→입장.
5. **ROADMAP 갱신 + 커밋**, 워크트리 머지.
