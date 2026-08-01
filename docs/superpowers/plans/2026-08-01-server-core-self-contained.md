# `@lop/server-core` 자기완결화 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 공용 패키지가 **누가 언제 import 하든 알아서 서게** 만든다. dotenv를 앱 진입점으로 되돌리고(업계 표준), 패키지가 자기 파일과 기본값을 스스로 갖게 해서 `jest.setup.js` 임시 봉합을 걷어낸다.

**Architecture:** 배럴을 `exports` 맵으로 쪼개는 정석은 `moduleResolution: node10` 때문에 지금 불가능하다(별도 Open Decision). 대신 **부수효과를 없애는 대신 자기완결로** 만든다 — 실제로 아팠던 것은 "부수효과가 있다"가 아니라 "부수효과가 자기 발로 못 선다"였다.

**Tech Stack:** TypeScript 5.7 / pnpm workspace + turbo / Express / Node CJS

## Global Constraints

- **동작 변화 0이 목표다.** 값을 바꾸거나 검증을 추가하지 마라. 기본값은 *env가 없을 때만* 적용되는 안전장치이지 설정 변경이 아니다.
- **`C:/Users/re5na/workspace/LOP/lop-backend` 밖의 파일을 절대 건드리지 마라.**
- 주석은 **한국어**로, *왜*만.
- 새 npm 의존성 추가 금지(이동·제거는 이번 작업의 일부).
- 검증: `pnpm build`(5개) + `pnpm --filter matchmaking-server test`(154) + `pnpm --filter room-server test`(11).
- 작업 브랜치: `feature/server-core-self-contained`. **main에 직접 커밋 금지.**

---

## Task 1: dotenv를 앱 진입점으로 되돌린다

**Files:**
- Modify: `packages/server-core/src/config/index.ts` (dotenv 호출 제거)
- Modify: `packages/server-core/package.json` (`dotenv` 의존 제거)
- Modify: `apps/{lobby,matchmaking,room}-server/src/config/index.ts` (dotenv 호출 복귀, 부수효과 import 제거)
- Modify: `apps/{lobby,matchmaking,room}-server/src/main.ts`, `apps/matchmaking-server/src/director.ts` (설정을 가장 먼저 import)

**Interfaces:**
- Consumes: (없음)
- Produces: 패키지 config는 `process.env`를 **읽기만** 한다. env 로딩은 앱 진입점 책임.

> **왜:** 라이브러리가 dotenv를 부르는 것은 안티패턴이다 — 그걸 쓰는 모든 앱이 `.env` 방식에 묶이고
> 관심사가 섞인다. 표준은 "앱 진입점에서 한 번, 라이브러리는 이미 로드됐다고 가정".
> 우리는 그 반대로 만들었고, 그 대가로 앱 설정이 `import '@lop/server-core';`라는 부수효과 import에
> 의존하게 됐다. 배럴이 무거워지자 그 한 줄이 express·winston·redis를 전부 끌고 온다.

- [ ] **Step 1: 패키지 config에서 dotenv를 뺀다**

`packages/server-core/src/config/index.ts`에서 `import { config } from 'dotenv';`와 `config({ path: ... })` 호출을 삭제하고, 파일 맨 위에 주석을 남긴다:

```typescript
//  .env 로딩은 앱 진입점 책임이다(라이브러리는 env가 이미 로드됐다고 가정한다).
//  각 앱의 config가 dotenv를 부르고, main.ts/director.ts가 그 config를 가장 먼저 import 한다.
```

나머지 `export const ...` 줄은 **그대로 둔다.**

`packages/server-core/package.json`의 `dependencies`에서 `dotenv`를 제거한다(세 앱은 이미 갖고 있다).

- [ ] **Step 2: 각 앱 config가 dotenv를 부르게 한다**

`apps/{lobby,matchmaking,room}-server/src/config/index.ts` 각각에서
`import '@lop/server-core';` 부수효과 import를 **삭제**하고, 대신 맨 위에 dotenv 호출을 넣는다
(이 프로젝트 이전에 이 파일들이 갖고 있던 바로 그 두 줄이다):

```typescript
import { config } from 'dotenv';
config({ path: `.env.${process.env.NODE_ENV || 'development'}.${process.env.SPECIFIC_ENV || 'local'}` });
```

이웃 서비스 주소를 export 하는 줄들은 그대로 둔다.

- [ ] **Step 3: 진입점이 설정을 가장 먼저 import 하게 한다**

`apps/{lobby,matchmaking,room}-server/src/main.ts`와 `apps/matchmaking-server/src/director.ts`의
**맨 첫 import**로 아래를 넣는다(기존 `import 'reflect-metadata';`보다 앞):

```typescript
//  가장 먼저 .env를 읽는다 — 아래 import들이 로드되는 순간 process.env를 읽으므로 순서가 강제된다.
import '@config';
```

> import는 선언 순서대로 평가되므로 이 한 줄이 순서를 보장한다. 이 줄이 없으면 뒤따르는
> `@lop/server-core` import가 아직 비어 있는 `process.env`를 읽는다.

- [ ] **Step 4: 빌드·테스트**

Run: `pnpm install && pnpm build && pnpm --filter matchmaking-server test && pnpm --filter room-server test`
Expected: 빌드 5/5, 154 + 11 통과

> 이 시점엔 `jest.setup.js`가 아직 남아 있어 테스트가 통과한다. 그 제거는 Task 2다.

- [ ] **Step 5: 로딩 순서를 실증한다**

빌드 산출물로 확인한다(정적 독해로 끝내지 마라 — 이건 런타임에만 드러나는 종류다):

```bash
cd apps/lobby-server && node -e "require('./dist/config'); console.log({ MM: process.env.MATCH_MAKING_SERVER_PORT, LOG: process.env.LOG_DIR });"
```
Expected: `.env.development.local`의 실제 값이 찍힌다(undefined면 실패다).

**음성 대조군**도 확인하라 — `dist/main.js`에서 첫 줄의 config require를 지우면 값이 비는지.
(확인 후 반드시 원상복구하고 `git status`가 깨끗한지 보라.)

- [ ] **Step 6: 커밋**

```bash
git add -A packages apps pnpm-lock.yaml
git commit -m "refactor(server-core): dotenv를 앱 진입점으로 되돌린다 (라이브러리는 env를 로드하지 않는다)"
```

---

## Task 2: 패키지를 자기완결로 만들고 임시 봉합을 걷어낸다

**Files:**
- Move: `apps/lobby-server/lua/` → `packages/server-core/lua/` (나머지 두 앱 사본은 삭제)
- Modify: `packages/server-core/src/loaders/redis.loader.ts` (lua 경로 + REDIS 기본값)
- Modify: `packages/server-core/src/utils/logger.ts` (`mkdirSync` 재귀 + `LOG_DIR` 기본값)
- Delete: `apps/{matchmaking,room}-server/jest.setup.js`
- Modify: `apps/{matchmaking,room}-server/jest.config.js` (`setupFiles` 참조 제거)

**Interfaces:**
- Consumes: Task 1
- Produces: `@lop/server-core`를 import 하는 데 어떤 env·CWD 전제도 필요 없다.

- [ ] **Step 1: lua 파일이 정말 3앱 동일한지 확인한다**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
for f in save count findAll deleteAll; do
  L=$(git hash-object apps/lobby-server/lua/redis/$f.lua)
  M=$(git hash-object apps/matchmaking-server/lua/redis/$f.lua)
  R=$(git hash-object apps/room-server/lua/redis/$f.lua)
  [ "$L" = "$M" ] && [ "$M" = "$R" ] || echo "DIFFERS: $f"
done
```
Expected: 출력 없음. **`DIFFERS`가 나오면 멈추고 보고하라.**

- [ ] **Step 2: lua를 패키지로 옮긴다**

```bash
git mv apps/lobby-server/lua packages/server-core/lua
git rm -r apps/matchmaking-server/lua apps/room-server/lua
```

- [ ] **Step 3: `redis.loader.ts`가 자기 파일을 읽게 한다**

lua를 읽는 4곳의 경로를 CWD 상대에서 **모듈 기준**으로 바꾼다:

```typescript
import { join } from 'path';

//  CWD 기준으로 읽으면 "이 패키지를 쓰는 쪽의 작업 디렉터리에 lua가 있어야 한다"는 숨은 계약이 생긴다.
//  모듈 위치 기준으로 바꿔 패키지가 자기 파일을 스스로 들고 있게 한다.
//  src/loaders에서도 dist/loaders에서도 두 단계 위가 패키지 루트라 같은 경로가 성립한다.
const LUA_DIR = join(__dirname, '..', '..', 'lua', 'redis');
```

그리고 각 `readFileSync('./lua/redis/X.lua')`를 `readFileSync(join(LUA_DIR, 'X.lua'))`로 바꾼다.

- [ ] **Step 4: `createClient`가 env 없이도 터지지 않게 한다**

`redis.loader.ts`의 URL 구성에서 `REDIS_HOST`/`REDIS_PORT`가 비었을 때 `localhost`/`6379`로 떨어지게 한다.

```typescript
//  모듈 로드만으로 터지지 않게 하는 안전장치다 — 실제 값은 env에서 온다.
//  주소가 틀리면 조용히 넘어가지 않고 connect() 시점에 크게 실패한다.
```

> **설정을 바꾸는 게 아니다.** env가 있으면 언제나 env가 이긴다.

- [ ] **Step 5: `logger.ts`를 자기완결로**

`packages/server-core/src/utils/logger.ts`에서:
- `LOG_DIR`이 비면 `'logs'`로 떨어지게 한다(사용처에서 기본값을 준다 — config는 env의 거울로 유지)
- `mkdirSync(logDir)` → `mkdirSync(logDir, { recursive: true })`

기존의 경로 관련 주석은 갱신하되 **`process.cwd()` 기준은 유지**한다(그건 별도로 결정된 사항이다).

- [ ] **Step 6: 임시 봉합을 걷어낸다**

```bash
git rm apps/matchmaking-server/jest.setup.js apps/room-server/jest.setup.js
```

두 앱의 `jest.config.js`에서 `setupFiles`(또는 `setupFilesAfterEnv`) 항목 중 그 파일을 가리키는 줄을 제거한다.
그 배열이 비면 항목 자체를 지운다.

- [ ] **Step 7: 검증 — 봉합 없이 통과하는지**

Run: `pnpm build && pnpm --filter matchmaking-server test && pnpm --filter room-server test`
Expected: 빌드 5/5, **154 + 11 통과**

**여기서 실패하면 그것이 이 태스크의 핵심 정보다** — 무엇이 아직 자기완결이 아닌지 알려 준다.
env 기본값을 더 넣어 억지로 통과시키지 말고, **무엇이 왜 실패했는지 보고하라.**

- [ ] **Step 8: 패키지가 정말 자립하는지 확인한다**

패키지 디렉터리를 CWD로 두고 배럴을 로드해 본다(예전엔 lua ENOENT로 죽던 자리다):

```bash
cd packages/server-core && node -e "require('./dist/index.js'); console.log('barrel loaded standalone');"
```
Expected: `barrel loaded standalone`

- [ ] **Step 9: 커밋**

```bash
git add -A packages apps
git commit -m "refactor(server-core): 패키지가 자기 파일·기본값을 갖게 하고 jest 임시 봉합 제거"
```

---

## 검증·배포 (사람이 수행 — 서브에이전트 아님)

- **로컬 `docker build` 3종** — lua 위치가 바뀌었으므로 이미지에 실제로 실리는지 확인해야 한다
  (`pnpm deploy --prod`가 패키지 디렉터리를 통째로 복사하는지가 관건)
- 머지 후 `backend-deploy`를 **`app: all`** 로 실행
- 배포 후 네 파드 기동 + 에러 로그 0 + 2클라 E2E
  - **동작 변화 0이 목표다** — 뭔가 달라 보이면 그게 결함이다
  - 특히 **Redis를 쓰는 경로**(룸 조회·티켓 캐시)가 정상인지: lua 스크립트 로딩이 바뀌었다
