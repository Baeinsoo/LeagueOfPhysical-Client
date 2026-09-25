# 백엔드 공용 패키지 `@lop/server-core` — 설계

백엔드 3개 앱(`lobby-server` / `matchmaking-server` / `room-server`)이 **복사해서 나눠 갖고 있는**
공통 코드를 워크스페이스 패키지 하나로 모은다.

## 1. 문제 — 무엇이 실제로 복제돼 있나

세 앱은 각각 독립된 Express 프로젝트이고 공유 모듈이 없다. 그래서 공통으로 필요한 것을 **복사**해 뒀다.
착수 시점(2026-07-31) 실측:

- **26개 파일이 세 앱에서 바이트 단위로 동일**하다 — 로거, 미들웨어, 예외, DAO/저장소 베이스,
  DB·Redis 로더, Express 앱 골격, 공통 인터페이스.
- 그 밖에 **14개는 세 앱에 다 있는데 내용이 갈렸다.** 대부분은 정상이다 — 룸 서버만
  `room.service.ts`가 342줄이고 나머지는 17줄짜리 HTTP 껍데기인 식으로, 앱마다 소유한 책임이 다르다.

### 갈라짐이 이미 시작됐다는 증거 둘

**① 글자 단위로 갈라진다.** `loaders/redis.loader.ts`는 세 앱 모두 67줄인데 내용이 다르다.
차이는 **세미콜론 하나**다. 아무 해도 없지만 누구도 의도하지 않은 차이가 이미 생겼다는 뜻이다.

**② 타입이 거짓말을 하고 있다.** `UserLocationResponseDto`에서
- **로비 서버**(이 응답을 *만드는* 쪽)에는 `timestamp` 필드가 **없고**
- **매칭·룸 서버**(이 응답을 *받는* 쪽)에는 **있다.**

로비의 매퍼는 `userId`/`location`/`locationDetail` 셋만 담으므로 그 필드는 **영영 오지 않는다.**
지금은 아무도 읽지 않아 잠복해 있지만, 누가 읽으면 컴파일은 통과하고 런타임에 `undefined`가 나온다.

### 비용이 실제로 청구된 사례

매치메이킹 슬라이스 5(개명)에서 `Location` enum 하나를 바꾸는 데 **손으로 세 번** 고쳐야 했고,
리뷰는 "정말 셋이 같은가"를 눈으로 볼 수 없어 **git blob 해시를 비교해 증명**했다.
한 곳만 빠뜨렸다면 그 앱만 옛 이름을 쓰다 런타임에 깨졌을 것이다.

## 2. 결정

### 패키지 하나 — `@lop/server-core`

`packages/server-core/`에 워크스페이스 패키지를 신설한다. 이미 `packages/database`(`@lop/database`)라는
선례가 있고 `pnpm-workspace.yaml`이 `packages/*`를 잡고 있어 구조는 그대로 따른다.

- **왜 하나인가**: 26개 파일이 전부 "Express 서비스 골격"이라는 한 가지 관심사다. 관심사별로
  `@lop/config`/`@lop/logger`/`@lop/persistence`로 쪼개는 안은 현 규모에 과하다.
- **왜 `shared`가 아닌가**: 이 생태계에서 "Shared"는 이미 Unity 공유 게임 코드
  (`LeagueOfPhysical-Shared`)를 뜻한다. 어휘 충돌을 피한다.
- **명명 근거(산업 표준)**: Turborepo·Nx의 *internal packages* 관용 — 앱이 워크스페이스 의존으로
  가져다 쓰는 비배포 패키지. 기존 `@lop/database`와 같은 "무엇인가" 명명 축을 유지한다.

### 경계 — "설정에 닿는가"가 슬라이스를 가른다

26개의 의존 그래프를 실제로 계산해 보면 딱 한 지점에서 갈린다: **`@config`에 닿는가.**

| | 개수 | 내용 |
|---|---|---|
| **설정에 안 닿음** | **16** | 서로만 참조하거나 외부 라이브러리만 쓴다 → 그대로 옮길 수 있다 |
| **설정에 닿음** | **10** | `app.ts`, 로거, DB/캐시 로더 등 → 설정을 먼저 정리해야 한다 |

### 설정 분리 — 인프라 설정 vs 이웃 주소

앱별 `config/index.ts`가 다른 이유는 **딱 두 줄**이다:

```
로비:   MATCH_MAKING_SERVER_*, ROOM_SERVER_*
매칭:   LOBBY_SERVER_*,        ROOM_SERVER_*
룸:     LOBBY_SERVER_*,        MATCH_MAKING_SERVER_*
```

나머지(`NODE_ENV`/`PORT`/`LOG_*`/`MONGODB_*`/`POSTGRES_*`/`REDIS_*`)와 dotenv 로딩은 **세 앱이 동일**하다.
따라서:

- **`@lop/server-core`** — dotenv 로딩 + **인프라 설정**(위 동일한 부분)
- **각 앱** — **이웃 서비스 주소**만 두 줄

각 앱은 "내가 누구를 부르는가"만 알고, "나는 어떤 인프라 위에 있는가"는 공용이 안다.

## 3. 범위

| | 슬라이스 | 내용 | 동작 변화 |
|---|---|---|---|
| **1** | 패키지 신설 + **설정에 안 닿는 16개** 이전 | 배선(패키지·tsconfig·jest·Dockerfile·lockfile)이 실제로 도는지 증명 | **0** |
| **2** | **설정 분리 + 나머지 10개** 이전 | "손으로 세 번 고치기"가 소멸 | **0** |

**슬라이스 3(갈라진 계약 정리)은 이번 범위가 아니다.** 응답 코드 3앱 통합과 위 `timestamp` 거짓말
해소는 *진짜 계약이 무엇인가*를 새로 정하는 일이라 성격이 다르다. 1·2를 끝내고 따로 판단한다.

## 4. 배선 — 리팩터보다 여기가 위험하다

코드 이동 자체는 기계적이다. 실제 위험은 **빌드 배선**이고, 네 곳을 함께 고쳐야 한다.

| | 무엇 | 안 하면 |
|---|---|---|
| 🔴 | **각 앱 Dockerfile에 `COPY packages/server-core`** | 이미지 빌드 실패 (지금은 `COPY packages/database`만 있다) |
| 🔴 | **각 앱 Dockerfile에 패키지 빌드 단계** | 앱 `tsc`가 패키지의 `.d.ts`를 못 찾아 실패 (`@lop/database`는 `generate`로 처리되고 있다) |
| 🔴 | **`pnpm-lock.yaml` 갱신 커밋** | Dockerfile이 `--frozen-lockfile`이라 설치 단계에서 실패 |
| 🟠 | 각 앱 `tsconfig.json` 경로 별칭 / `jest.config.js` moduleNameMapper | 컴파일·테스트 실패 (jest 설정은 matchmaking·room에만 있다) |

전부 **CI에서 큰 소리로 깨지는** 종류라 조용한 사고는 아니다. 다만 넷 중 하나만 빠져도 배포가 막힌다.

`turbo.json`은 이미 `build: dependsOn ["^build"]`라 워크스페이스 의존이 먼저 빌드된다 — 손댈 필요 없다.

## 5. 검증

- 세 앱 **빌드 통과**(`pnpm --filter <app> run build`)
- 기존 테스트 전부 통과(matchmaking 154 + room 11)
- **로컬 `docker build`로 이미지 3개 실증** — Dockerfile 변경은 로컬 `pnpm build`가 증거가 되지 못한다
  (워크스페이스 hoisting이 문제를 가린다 — 과거 `db-migrate`가 이 방식으로 3주간 잠복해 깨져 있었다)
- 배포 후 4개 앱 기동 + 2클라 E2E (동작 변화가 0임을 확인하는 것이 목적)

## 6. 이 슬라이스가 건드리지 않는 것

- Unity 클라이언트·게임 서버 (백엔드 전용)
- DB 스키마·마이그레이션 (없음)
- 앱별 라우트·컨트롤러·서비스·도메인 DTO
- 갈라진 14개 파일 중 **정당하게 다른 것들**(룸 서버의 `room.service.ts` 등)

## 7. Open Decisions

- [ ] **슬라이스 3 착수 여부** — 응답 코드 3앱 통합 + `UserLocationResponseDto`의 `timestamp` 해소.
      1·2 완료 후 판단.
- [ ] **`httpService.ts` 타임아웃 비대칭** — 매치메이킹 슬라이스 4b에서 HTTP 타임아웃 5초를 매칭 서버에만
      넣었다(당시 Director만 문제였다). 로비·룸에는 여전히 없다. 이 파일이 공용으로 올라오면 자연히
      통일되는데, **세 앱에 같은 타임아웃이 옳은지**는 별도 판단이 필요하다(슬라이스 2에서 결정).

---

## 8. 후속 정정 — 배럴 부수효과를 표준 형태로 해소 (2026-08-01)

슬라이스 1·2를 마친 뒤 "배럴에 부수효과가 생겼다"를 후속으로 남겼는데, **업계 표준을 조사한 결과
우리가 표준에서 벗어난 지점이 어디인지가 분명해졌다.** 네 가지 모두 이 프로젝트 고유 문제가 아니다.

### 조사 결과 — 전부 교과서에 있는 문제다

| 우리가 겪은 것 | 업계에서 부르는 이름 | 표준 해법 |
|---|---|---|
| 배럴에서 타입 하나만 가져와도 부수효과 실행 | *barrel file* 문제 | `exports` 맵으로 서브패스 제공, 배럴은 순수 re-export만. `"sideEffects": false`는 **번들러 힌트라 Node CJS 백엔드인 우리에겐 안 먹는다** |
| 같은 라이브러리가 두 물리 실체로 | React의 "두 사본이 보인다"와 같은 부류 | `peerDependencies`로 소비자가 제공. 단 **pnpm에선 이게 알려진 함정**이라 사본이 늘기도 한다 |
| 공용 패키지가 dotenv를 부른다 | **안티패턴** | dotenv는 **앱 진입점에서 한 번**. 라이브러리는 env가 이미 로드됐다고 가정하고 `process.env`만 읽는다 |
| `exports` 맵이 안 먹는다 | `moduleResolution: node10` 제약 | `node16`/`nodenext`/`bundler`로. **node10은 deprecated이고 TS 7.0에서 동작을 멈춘다** |

### 정정 — dotenv를 패키지에 넣은 것이 비표준이었다

슬라이스 2에서 "인프라 설정(dotenv 포함)은 공용, 이웃 주소는 앱별"로 나눴다. **dotenv를 공용에 넣은 부분이
표준과 어긋난다.** 그 선택의 대가가 지금 문제의 절반이다:

- 앱 설정이 `import '@lop/server-core';`라는 **부수효과 import**로 로딩 순서를 맞춰야 했고,
- 배럴이 무거워지자 그 한 줄이 express·winston·redis·lua 읽기를 전부 끌고 오게 됐다.

**표준 형태로 되돌린다:** 각 앱의 `config/index.ts`가 dotenv를 부르고(이 프로젝트 이전 모습),
**진입점(`main.ts`/`director.ts`)이 그 설정을 가장 먼저 import** 해 순서를 명시적으로 강제한다.
공용 패키지의 config는 `process.env`를 **읽기만** 한다.

### 부수효과를 없애는 대신 *자기완결*로 만든다

배럴을 쪼개는 정석은 `exports` 맵인데 `moduleResolution` 때문에 지금 불가능하다. 그런데 실제로 아픈 것은
"부수효과가 있다"가 아니라 **"부수효과가 자기 발로 서지 못한다"** 였다:

- `redis.loader`가 lua 4개를 **CWD 기준**으로 읽는데 패키지엔 `lua/`가 없다 → 이 패키지에 테스트를
  추가하는 순간 ENOENT. (부수 발견: **lua도 3앱에 복제**돼 있다 — 옮기면 그것도 한 벌이 된다.)
- `logger`가 `LOG_DIR` 없으면 터지고 `mkdirSync`가 비재귀다.

둘을 고치면 "누가 언제 import 하든 알아서 선다"가 되어 `jest.setup.js` 임시 봉합을 걷어낼 수 있다.

### 범위

| | 내용 | 표준 근거 |
|---|---|---|
| **1** | dotenv를 앱 진입점으로 되돌린다 (패키지 config는 읽기만) | 라이브러리는 env를 로드하지 않는다 |
| **2** | lua를 패키지 안으로(`__dirname` 기준) + `mkdirSync` 재귀 + `LOG_DIR` 기본값 | 패키지는 자기완결이어야 한다 |
| ~~3~~ | ~~`exports` 맵으로 배럴 분해~~ | **범위 밖** — `moduleResolution` 업그레이드가 선행돼야 한다 |

1·2 이후에도 "타입 하나 가져오면 Prisma/Redis **객체**가 만들어진다"는 순수성 문제는 남는다.
연결은 명시 호출이라 **동작상 해는 없고**, 해소는 `moduleResolution` 업그레이드와 묶어 판단한다.

### 새 Open Decision

- [ ] **`moduleResolution` node10 → node16/bundler 업그레이드** — `exports` 맵의 전제이고,
      node10은 **TS 7.0에서 제거**되므로 어차피 해야 한다. 세 앱의 import 의미론에 영향이 있어 별도 계획.

> 출처: [Barrel Exports considered harmful](https://blog.coderspirit.xyz/blog/2022/11/06/export-barrels-considered-harmful/) ·
> [Turborepo — Structuring a repository](https://turborepo.dev/docs/crafting-your-repository/structuring-a-repository) ·
> [pnpm — Monorepo peer dependency hell](https://github.com/orgs/pnpm/discussions/5431) ·
> [dotenv](https://github.com/motdotla/dotenv) ·
> [TypeScript — moduleResolution](https://www.typescriptlang.org/tsconfig/moduleResolution.html)
