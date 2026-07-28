# 매치메이킹 슬라이스 1b — 매칭 서버를 모노레포로 이식

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 슬라이스 1의 매칭 서버 변경(Luban JSON 로더 + jest + XML 제거)을 **실제로 배포되는 저장소**인 `lop-backend` 모노레포에 적용한다.

**왜 필요한가:** 슬라이스 1의 Task 4·5는 `re5nardo/LeagueOfPhysical-MatchmakingServer`에 적용됐는데, 그 저장소는 **2025-08-31 아카이브**됐다. 배포 파이프라인은 `Baeinsoo/lop-backend`의 `apps/matchmaking-server`를 빌드한다(infrastructure README: *"백엔드 서버 코드는 이 레포가 아니라 lop-backend 모노레포에 있다"*). 배포된 이미지 태그 `re5nardo/matchmaking-server:e08245e`의 sha가 아카이브 저장소에 존재하지 않는 것으로 실증됨. 따라서 기존 작업물은 프로덕션에 도달할 수 없다.

**Architecture:** 이식 원본은 **이미 리뷰를 통과한 코드**다. 모노레포의 해당 소스가 아카이브 저장소의 시작점과 **줄바꿈 문자를 빼면 0줄 차이**임을 실측했으므로, 변경을 거의 그대로 옮긴다. 도구 체계만 npm→pnpm/turbo로 맞춘다.

**Tech Stack:** pnpm 10.11.0 (워크스페이스) · turbo 2.5 · TypeScript 5.7 · jest 29 + ts-jest · Luban 4.9 생성물

## Global Constraints

- **이식 원본(진실원본)**: 아카이브 저장소 `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MatchmakingServer`, 브랜치 `feature/matchmaking-slice1-luban-tables`, **커밋 `168fe04`**. 이 커밋의 파일 내용이 그대로 정답이다 — 새로 발명하지 말 것.
- **동작 무변화.** 게임 5종의 정원은 전부 `min 2 / max 8`. 매칭 결과가 달라지면 실패다.
- 생성 파일(`src/loaders/generated/`, `master_data/*.json`)은 **Luban 산출물** — 손으로 만들거나 고치지 않는다.
- `src/interfaces/enums.ts`의 `GameMode` enum(Normal/Ranked)과 생성 schema의 `GameMode` 클래스를 **한 파일에서 동시에 import하지 않는다.**
- 맵 테이블의 요소 타입은 `GameMap`(생성 TS의 `Map<K,V>` 충돌 회피). 접근자는 `Tables.TbMap`.
- **`main`에 직접 커밋 금지.** 각 저장소에서 피처 브랜치.
- 커밋 메시지 끝에 `Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>`.

**경로:**

| 별칭 | 경로 |
|---|---|
| `INFRA` | `C:/Users/re5na/workspace/LOP/infrastructure` |
| `OLD` | `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MatchmakingServer` (아카이브 — **이식 원본, 커밋 안 함**) |
| `MONO` | `C:/Users/re5na/workspace/LOP/lop-backend` (pnpm 루트) |
| `APP` | `MONO/apps/matchmaking-server` |

**기준선 (이식 전 실측):** `pnpm install --frozen-lockfile` OK · `pnpm build` 4/4 성공.

**모노레포가 아카이브 저장소와 다른 점:**

| | 아카이브 | 모노레포 |
|---|---|---|
| 패키지 매니저 | npm | **pnpm 워크스페이스** |
| 태스크 러너 | 없음 | **turbo** (`tasks`에 `build`만 존재) |
| tsconfig | 단독 | `../../tsconfig.base.json` 상속, `exclude: ["node_modules", "src/logs"]` |
| DB | 자체 prisma | `@lop/database` 워크스페이스 패키지 |
| `.dockerignore` | 앱별 | **모노레포 루트** (`**/node_modules`, `**/dist`, `**/generated`, `**/.turbo`, `**/logs`, `.git`) |

---

## Task 1b-1: gen 출력 경로를 모노레포로 교정 + 재생성

**Files:**
- Modify: `INFRA/table/gen.sh`, `INFRA/table/gen.bat`
- 생성물(커밋 대상): `APP/src/loaders/generated/schema.ts`, `APP/master_data/*.json`

**Interfaces:**
- Consumes: 슬라이스 1의 Luban 테이블(`TbGameMode`/`TbMap`/`TbQueue`, 그룹 `m`, 타깃 `matchmaking`) — 이미 `INFRA`에 커밋됨
- Produces: `APP/src/loaders/generated/schema.ts`(`Tables`, `JsonLoader`, `GameMode`, `GameMap`, `Queue`), `APP/master_data/{tbgamemode,tbmap,tbqueue}.json`

- [ ] **Step 1: 피처 브랜치 생성**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && git checkout -b feature/matchmaking-luban-masterdata
```

`INFRA`는 이미 `feature/matchmaking-slice1-luban-tables`에 있다 — 그대로 쓴다.

- [ ] **Step 2: `gen.sh`의 매칭 출력 경로 교정**

`INFRA/table/gen.sh`의 `MM_PKG` 정의를 아래로 바꾼다. **죽은 저장소를 가리키던 경로를 모노레포로 돌린다.**

```bash
MM_PKG="../../lop-backend/apps/matchmaking-server"
```

`echo "[gen] target=matchmaking -> MatchmakingServer"` 문구도 `-> lop-backend/apps/matchmaking-server`로 고쳐 어디로 나가는지가 로그에 드러나게 한다.

- [ ] **Step 3: `gen.bat`도 같이 교정**

```bat
set MM_PKG=..\..\lop-backend\apps\matchmaking-server
```

문구도 동일하게 수정한다. **`gen.sh`와 `gen.bat`은 항상 등가여야 한다.**

- [ ] **Step 4: 재생성**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table && ./gen.sh
```

Expected: `[gen] target=client` / `target=server` / `target=matchmaking -> lop-backend/...` / `[done]`, 에러 0.

> ⚠️ `gen.sh`는 출력 폴더를 `rm -rf` 한다. 이 때문에 두 MasterData 패키지의 **Unity 생성 `.meta`가 삭제 상태가 된다**(Luban은 `.meta`를 만들지 않음). 재생성 후 `git checkout -- .`로 되돌려라 — 내용은 동일하고 GUID가 보존된다. 이건 알려진 기존 결함이며 이 태스크의 범위가 아니다.

- [ ] **Step 5: 산출물 검증**

```bash
cd /c/Users/re5na/workspace/LOP
ls lop-backend/apps/matchmaking-server/master_data/
ls lop-backend/apps/matchmaking-server/src/loaders/generated/
cat lop-backend/apps/matchmaking-server/master_data/tbqueue.json
for r in LeagueOfPhysical-MasterData-Client LeagueOfPhysical-MasterData-Server; do (cd $r && git status --short | head -3); done
```

Expected:
- `master_data/`에 **정확히 3개** — `tbgamemode.json`, `tbmap.json`, `tbqueue.json` (옛 `sub_game_data/`는 아직 남아 있다 — 1b-3에서 지운다)
- `generated/schema.ts` 존재
- `tbqueue.json`에 `"allowed_game_mode_ids": [1,2,3,4,5]`, `name` 키 없음
- 두 MasterData 저장소는 **`.meta` 복구 후 깨끗**해야 한다(재생성 결과가 커밋본과 동일 = 파이프라인 결정론)

- [ ] **Step 6: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure && git add table/gen.sh table/gen.bat && git commit -m "$(cat <<'EOF'
fix(gen): matchmaking 출력을 lop-backend 모노레포로 돌림

기존 경로는 2025-08-31 아카이브된 LeagueOfPhysical-MatchmakingServer를
가리키고 있었다. 실제 배포 소스는 lop-backend/apps/matchmaking-server다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"

cd /c/Users/re5na/workspace/LOP/lop-backend && git add apps/matchmaking-server/src/loaders/generated apps/matchmaking-server/master_data && git commit -m "$(cat <<'EOF'
feat(masterdata): Luban 생성 schema.ts + 테이블 json 추가

Excel(infrastructure/table) 단일 진실원본에서 생성. 손으로 고치지 않는다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 1b-2: jest + Luban 로더 이식 (TDD)

**Files:**
- Create: `MONO/apps/matchmaking-server/jest.config.js`
- Create: `APP/src/loaders/__tests__/masterdata.loader.test.ts`
- Create: `APP/src/loaders/__tests__/fixtures/malformed-master-data/{tbgamemode,tbmap,tbqueue}.json`
- Modify: `APP/package.json` (jest devDeps + `test` 스크립트)
- Modify: `MONO/turbo.json` (`test` 태스크 추가)
- Modify: `APP/src/loaders/masterdata.loader.ts` (전면 재작성)

**Interfaces:**
- Consumes: 1b-1의 `generated/schema.ts`, `master_data/*.json`
- Produces — 1b-3이 쓰는 것:
  - `export function getTables(): Tables` — `load()` 전 호출 시 `'MasterData is not loaded. Call load() first.'`로 throw
  - `export async function load(folder?: string): Promise<void>` — 폴더 기본값은 실제 마스터데이터 경로, 테스트가 fixture 폴더 주입
  - `export function findGameModeByCode(code: string): GameMode | undefined`

- [ ] **Step 1: 이식 원본을 확인한다**

아래 네 파일이 **정답**이다. 읽고 그대로 옮긴다(경로만 `MONO` 기준으로):

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MatchmakingServer
git show 168fe04:MatchmakingServer/src/loaders/masterdata.loader.ts
git show 168fe04:MatchmakingServer/src/loaders/__tests__/masterdata.loader.test.ts
git show 168fe04:MatchmakingServer/jest.config.js
git show 168fe04:MatchmakingServer/package.json
```

fixture 3개도 같은 커밋의 `MatchmakingServer/src/loaders/__tests__/fixtures/malformed-master-data/`에 있다.

- [ ] **Step 2: jest 설치 (pnpm, 워크스페이스 필터)**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
pnpm --filter matchmaking-server add -D jest@^29 ts-jest@^29 @types/jest@^29
```

> npm이 아니라 **pnpm**이고, 루트가 아니라 **해당 앱에만** 넣는다(`--filter`). 루트에 넣으면 워크스페이스 규약이 깨진다.

- [ ] **Step 3: `jest.config.js` 생성**

원본과 동일하되, `moduleNameMapper`는 **모노레포 앱의 `tsconfig.json`에 있는 18개 별칭**과 1:1로 맞춘다(원본과 같은 목록임을 확인했다). `APP/jest.config.js`:

```javascript
/** @type {import('ts-jest').JestConfigWithTsJest} */
module.exports = {
    preset: 'ts-jest',
    testEnvironment: 'node',
    rootDir: '.',
    testMatch: ['<rootDir>/src/**/__tests__/**/*.test.ts'],
    moduleNameMapper: {
        '^@src/(.*)$': '<rootDir>/src/$1',
        '^@controllers/(.*)$': '<rootDir>/src/controllers/$1',
        '^@exceptions/(.*)$': '<rootDir>/src/exceptions/$1',
        '^@interfaces/(.*)$': '<rootDir>/src/interfaces/$1',
        '^@middlewares/(.*)$': '<rootDir>/src/middlewares/$1',
        '^@models/(.*)$': '<rootDir>/src/models/$1',
        '^@routes/(.*)$': '<rootDir>/src/routes/$1',
        '^@services/(.*)$': '<rootDir>/src/services/$1',
        '^@utils/(.*)$': '<rootDir>/src/utils/$1',
        '^@dtos/(.*)$': '<rootDir>/src/dtos/$1',
        '^@daos/(.*)$': '<rootDir>/src/daos/$1',
        '^@repositories/(.*)$': '<rootDir>/src/repositories/$1',
        '^@databases/(.*)$': '<rootDir>/src/databases/$1',
        '^@caches/(.*)$': '<rootDir>/src/caches/$1',
        '^@loaders/(.*)$': '<rootDir>/src/loaders/$1',
        '^@factories/(.*)$': '<rootDir>/src/factories/$1',
        '^@mappers/(.*)$': '<rootDir>/src/mappers/$1',
        '^@config$': '<rootDir>/src/config',
    },
};
```

- [ ] **Step 4: `test` 스크립트 + turbo 태스크**

`APP/package.json`의 `scripts`에 추가:

```json
        "test": "jest",
```

`MONO/turbo.json`의 `tasks`에 추가 — 지금은 `build`밖에 없어서 루트에서 `pnpm test`가 아무것도 안 한다:

```json
        "test": {
            "dependsOn": ["^build"]
        }
```

`MONO/package.json`의 `scripts`에 `"test": "turbo run test"`를 추가한다.

- [ ] **Step 5: 실패하는 테스트를 먼저 놓는다**

Step 1에서 확인한 테스트 파일과 fixture 3개를 그대로 배치한다. 그리고:

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server && pnpm test
```

Expected: **FAIL** — `getTables`/`findGameModeByCode`가 아직 없어 컴파일 단계에서 터진다.

- [ ] **Step 6: 로더를 원본대로 재작성**

`APP/src/loaders/masterdata.loader.ts`를 `168fe04` 버전으로 교체한다. 내용 요지(원본 확인 필수):
- 모듈 레벨 `tables` 싱글턴 + `getTables()` 가드
- `load(folder?)` — 기본 폴더는 `master_data`, `Tables` 생성자에 파일별 JSON 로더 주입
- `JSON.parse` 실패 시 **파일 경로를 메시지에 담고** 원본 오류를 `cause`로 보존
- `findGameModeByCode(code)` — `getDataList().find(...)`

- [ ] **Step 7: 테스트 통과 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server && pnpm test
```

Expected: **6 passed** (기본 4 + 가드 1 + 깨진 JSON 1).

> 테스트가 `master_data/`를 cwd 상대로 읽는다. 앱 디렉터리에서 실행해야 맞는다.

- [ ] **Step 8: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
git add apps/matchmaking-server/jest.config.js apps/matchmaking-server/package.json \
        apps/matchmaking-server/src/loaders/__tests__ apps/matchmaking-server/src/loaders/masterdata.loader.ts \
        turbo.json package.json pnpm-lock.yaml
git commit -m "$(cat <<'EOF'
feat(masterdata): 매칭 서버 로더를 Luban JSON으로 전환 + jest 도입

자체 XML 스캔 대신 Luban 생성 Tables를 구성한다. 타입도 생성물이라
손으로 쓴 인터페이스가 필요 없다. 이 저장소의 첫 테스트이며,
후속 슬라이스(MatchFunction/Evaluator)의 토대다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 1b-3: 소비처 전환 + XML 삭제 + 테스트를 빌드에서 제외

**Files:**
- Modify: `APP/src/services/waitingRoom.service.ts`
- Modify: `APP/package.json` (`fast-xml-parser` 제거)
- Modify: `APP/tsconfig.json` (`exclude`에 테스트 추가)
- Delete: `APP/src/interfaces/masterdata/subGameData.interface.ts`, `APP/src/utils/util.xml.ts`, `APP/master_data/sub_game_data/`

**Interfaces:**
- Consumes: 1b-2의 `findGameModeByCode`
- Produces: 없음 (슬라이스 종료)

- [ ] **Step 1: 소비처 전환**

원본 정답: `git show 168fe04:MatchmakingServer/src/services/waitingRoom.service.ts`. 바뀌는 부분은 import 한 줄과 조회 블록뿐이다.

import를 교체:

```typescript
import { findGameModeByCode } from '@loaders/masterdata.loader';
```

조회 블록을 교체 (**값은 동일**, `MinPlayerCount`→`minPlayers` 등 이름만):

```typescript
                const gameMode = findGameModeByCode(matchmakingTicket.subGameId);
                if (gameMode === undefined) {
                    throw new Error(`Unknown gameMode code: ${matchmakingTicket.subGameId}`);
                }
                waitingRoom = await this.createWaitingRoom(new CreateWaitingRoomDto(
                    matchmakingTicket.matchType,
                    matchmakingTicket.subGameId,
                    matchmakingTicket.mapId,
                    matchmakingTicket.rating,
                    5,  //  ?
                    gameMode.minPlayers,
                    gameMode.maxPlayers
                ));
```

> 옛 코드는 `undefined`인 결과에서 그대로 `.MinPlayerCount`를 읽어 애매한 `TypeError`로 죽었다. 원인을 짚는 에러로 바꾼다.
>
> `5,  //  ?`(최대 대기시간)는 **그대로 둔다.** 큐 데이터의 `max_wait_seconds`(30/60)로 바꾸는 건 동작 변경이라 후속 슬라이스 몫이다.

- [ ] **Step 2: 죽은 파일 삭제**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
git rm src/interfaces/masterdata/subGameData.interface.ts
git rm src/utils/util.xml.ts
git rm -r master_data/sub_game_data
```

- [ ] **Step 3: `fast-xml-parser` 제거**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
pnpm --filter matchmaking-server remove fast-xml-parser
```

- [ ] **Step 4: 테스트를 프로덕션 빌드에서 제외**

`APP/tsconfig.json`의 `exclude`에 테스트 글롭을 추가한다. 지금은 `["node_modules", "src/logs"]`라 테스트가 `dist/`로 나가고, 모노레포 루트 `.dockerignore`도 테스트를 막지 않아 **컨테이너 이미지에 테스트와 깨진 fixture가 실린다.** 또한 프로덕션 빌드가 `@types/jest`(devDependency)에 의존하게 된다.

```json
    "exclude": ["node_modules", "src/logs", "src/**/__tests__/**"]
```

- [ ] **Step 5: 잔재 검색 (0이어야 함)**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server
grep -rn "MasterDataType\|readXml\|fast-xml-parser\|subGameData\|SubGameData" --include="*.ts" --include="*.json" src package.json | grep -v node_modules | wc -l
```

Expected: `0`

- [ ] **Step 6: 빌드 + 테스트 + dist 확인**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend
rm -rf apps/matchmaking-server/dist
pnpm build 2>&1 | tail -5
cd apps/matchmaking-server && pnpm test 2>&1 | tail -8
ls dist/loaders/ | grep -c __tests__ || echo "dist에 테스트 없음 ✅"
```

Expected: 빌드 4/4 성공(기준선과 동일), 테스트 6 passed, `dist/`에 `__tests__` 없음.

- [ ] **Step 7: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/lop-backend && git add -A && git commit -m "$(cat <<'EOF'
refactor(masterdata): sub_game_data XML 잔재 제거 + 테스트를 빌드에서 제외

유일한 소비처(waitingRoom.service)를 Luban 테이블 조회로 전환하고,
XML 스키마 인터페이스·파서 유틸·데이터 파일과 fast-xml-parser 의존을 제거.
정원 값은 동일하며, 못 찾은 code는 원인을 짚는 에러로 바꿨다.
tsconfig exclude로 테스트가 dist/·컨테이너 이미지에 실리지 않게 한다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## 완료 기준

- [ ] `gen.sh`/`gen.bat`이 모노레포로 출력하고, 둘이 등가다
- [ ] `APP/master_data/`에 Luban json 3개만 있고 `sub_game_data/`는 없다
- [ ] 매칭 서버에 XML 관련 코드·의존이 남아 있지 않다
- [ ] `pnpm build` 4/4 (기준선 유지), `pnpm test` 6 passed
- [ ] `dist/`에 테스트 산출물이 없다
- [ ] 정원 값이 이전과 동일하다 (5종 min 2 / max 8)

## 이후 (이 계획 밖)

1. **머지** — INFRA + MasterData 2종 + 모노레포. 슬라이스 1의 아카이브 저장소 커밋은 버린다(아카이브라 push 불가).
2. **클러스터 재구축** — Docker Desktop 재시작으로 클러스터가 재생성돼 인그레스·DB·백엔드가 전부 소멸했다. `infrastructure/k8s/argocd/install/README.md`의 ArgoCD 부트스트랩 → `root-app.yaml` → platform(wave 0) → backend(wave 1) 순서로 복구.
3. **배포** — `lop-backend`에서 GitHub Actions `backend-deploy` 워크플로(대상 `matchmaking-server`) 실행 → 이미지 sha 태그가 `INFRA`의 kustomization에 자동 bump → ArgoCD sync.
4. **플레이 확인** — 클라 2개로 매칭. 클라는 `local-k8s` 환경이 기본이라 **k8s에 배포된 것**을 본다(로컬 `pnpm start`는 클라가 보지 않는다).
