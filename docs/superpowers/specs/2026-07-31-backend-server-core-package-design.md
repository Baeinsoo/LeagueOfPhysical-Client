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
