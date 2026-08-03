# 익명 로그인 + 세션 토큰 설계

기기 ID를 그대로 계정으로 쓰던 구조를 걷어내고, **익명(anonymous) 계정 + 서버 발급 세션 토큰**으로
바꾼다. 구글/애플 로그인은 *자리만* 잡아두고 검증 구현은 다음 슬라이스로 미룬다.

---

## 1. 배경 — 지금 무엇이 문제인가

로그인이라 부를 만한 것이 사실상 없다. 흐름을 따라가면 이렇다.

```
LoginComponent → LoginService.TryAutoLogin() → GuestLogin.Login()  ← 서버 왕복 0, 무조건 성공
                                                    ↓
                                              LoginResult.id       ← 아무도 안 씀 (버려짐)

CheckUserComponent → GET /user/username/{DeviceIdentifier.Current} ← 이게 진짜 "계정 식별"
                   → 없으면 POST /user 로 생성
```

| 문제 | 내용 |
|---|---|
| 자격 증명이 없음 | 기기 ID가 곧 계정. 그 값을 아는 누구나 그 사람이 될 수 있다 |
| 로그인이 유저 식별과 끊겨 있음 | `LoginResult.id`는 버려지고, 실제 식별은 `User` 생성자가 만든 username |
| REST 무인증 | `LOPWebRequestInterceptor.OnBeforeRequest`가 빈 함수 — 토큰도 헤더도 없다 |
| 룸 인증이 자기신고 | 클라가 보낸 userId가 매치 참가자 목록에 있으면 통과. 토큰은 `"token"` 리터럴 |
| 계정 영속성 | 기기 ID에 묶여 있어 기기를 바꾸면 계정이 따라오지 않는다 |

`#if UNITY_EDITOR`는 로그인 경로에 딱 한 군데(구글 버튼 숨김)뿐이다. **에디터 전용 치트가 아니라
빌드에서도 같은 구조**다.

## 2. 목표 / 비목표

**목표**
- 서버가 로그인을 실제로 검증한다 (기기 ID 자기신고 제거)
- 계정 영속성의 표준 경로를 연다 — 익명 계정 + 나중 연동(link)
- AOS/iOS 로그인이 들어올 자리를 표준 모양으로 미리 잡는다
- 한 PC에서 여러 계정으로 테스트할 수 있다 (지금 `DeviceIdentifier`가 하던 것을 승계)

**비목표 (이번 범위 밖)**
- 구글/애플 토큰의 **실제 검증** — verifier 인터페이스와 stub만 둔다
- 계정 **연동(link)** 플로우 — 데이터 모델만 그것을 받을 수 있게 만든다
- 리프레시 토큰, 세션 단위 무효화, 계정 밴/정지
- `characterId` 선택 (지금처럼 0 고정)

## 3. 채택 방향과 그 근거

**lobby-server에 인증을 얹는다.** 외부 인증 서비스(Firebase/UGS/PlayFab)를 쓰지 않는 이유는 둘이다.
① 이번에 플랫폼 토큰 검증을 stub으로 두므로 외부 서비스의 최대 이점(검증 대행)이 지금은 발생하지 않는다.
② `User`/`UserStats`/매칭이 전부 자체 DB의 userId에 묶여 있어 매핑 레이어가 하나 더 생긴다.

> 재검토 시점: 실제 출시가 가까워 구글/애플 검증을 진짜로 붙여야 할 때. 아래 verifier 인터페이스
> 자리에 외부 서비스를 끼우는 것도 그때의 선택지로 남는다.

---

## 4. ① 데이터 모델 — 계정과 신원의 분리

"계정"과 "무엇으로 로그인했나"가 지금은 `User.username` 한 칸에 뭉쳐 있다. 이를 나눈다.

```
User (계정 — 게임 데이터가 붙는 주체)
  id, username, email?, createdAt, lastLoginAt
    │  1 : N
UserIdentity (신원 — 로그인 수단)
  id, userId, provider, providerUserId, secretHash?, createdAt
  @@unique([provider, providerUserId])
```

| provider | providerUserId | secretHash |
|---|---|---|
| `ANONYMOUS` | 서버가 만든 랜덤 ID | 발급한 secret의 bcrypt 해시 |
| `GOOGLE_PLAY_GAMES` | 구글이 준 playerId (나중) | 없음 — 플랫폼이 신원을 보증 |
| `GAME_CENTER` | 애플이 준 gamePlayerID (나중) | 없음 |

**기존 `User` 필드 변경**

| 필드 | 변경 | 이유 |
|---|---|---|
| `username` | 의미가 *표시용 이름*으로 바뀜. 익명 가입 시 서버가 `Guest-a3f9c1` 형태로 생성 | 신원이 `UserIdentity`로 빠짐 |
| `email` | `String @unique` → `String? @unique` | 익명 계정엔 이메일이 없는 게 정상 |
| `passwordHash` | **삭제** | 아무도 안 쓰는 유령 필드. 남기면 "쓰는 줄" 오해를 부른다 |

**연동(link)의 자리**: 나중에 구글 연동은 "이 `User`에 `UserIdentity` 한 줄 추가"가 전부다.
스키마를 지금 이 모양으로 잡는 실질적 이유가 이것이다.

**마이그레이션**: dev DB의 유저는 개발용 더미뿐이므로 백필하지 않는다. 파괴적 마이그레이션이다.

## 5. ② 인증 API와 세션 토큰

### 엔드포인트

```
POST /auth/anonymous          (자격증명 없이 — 앱 최초 실행)
  → 201 { userId, credential: { provider, providerUserId, secret }, accessToken, expiresIn }
      · User + UserIdentity(ANONYMOUS) 생성
      · secret은 이 응답에서 딱 한 번만 평문으로 나간다 (DB엔 bcrypt 해시만)

POST /auth/login              (저장된 자격증명 제출 — 이후 모든 실행)
  { provider, providerUserId, secret? }
  → 200 { userId, accessToken, expiresIn }
  → 401 검증 실패 / 없는 신원
  → 501 미구현 provider
```

`provider`가 무엇이든 `/auth/login` 하나를 탄다. 갈리는 것은 **verifier**뿐이다.

```
AuthProviderVerifier
  verify(credential) → providerUserId

  AnonymousVerifier          secretHash와 bcrypt 비교        ← 이번에 구현
  GooglePlayGamesVerifier    서버 auth code → OAuth 교환     ← stub (501)
  GameCenterVerifier         서명·salt·timestamp 공개키 검증  ← stub (501)
```

구현 stub이지 **인터페이스는 진짜**다. 나중에 구글 검증을 붙일 때 클라·라우트·토큰 어느 것도
건드리지 않고 verifier 파일 하나만 채우면 된다.

### 세션 토큰

JWT(HS256), 클레임은 `sub = userId` + `exp`. **수명 1시간, 리프레시 토큰 없음.**

리프레시 토큰을 두지 않는 이유: 클라가 이미 **만료 없는 자격증명**(익명 secret)을 들고 있어서
그것이 리프레시 토큰의 역할을 그대로 한다. 하나 더 얹으면 같은 일을 하는 긴 수명 비밀이 클라에
둘 생긴다. 갱신은 **저장된 자격증명으로 `/auth/login`을 다시 치는 것**(silent re-auth)이다.

> 판단 기준: "원래 자격증명을 사용자 개입 없이 다시 쓸 수 있는가?" 사람 비밀번호나 1회용 OAuth
> 코드라면 못 쓰므로 리프레시 토큰이 필요하다. 우리 세 provider는 전부 다시 쓸 수 있다 — 익명은
> 저장된 secret, 구글/애플은 SDK에 다시 물어보면 사용자 개입 없이 새 토큰이 나온다.
>
> **파생 규칙: 플랫폼 토큰은 저장하지 않는다.** 짧은 수명 1회용이라 저장해도 못 쓴다. 재로그인
> 때마다 SDK에서 새로 받는다. 저장하는 것은 익명 secret뿐이다.
>
> 포기하는 것: 세션 단위 무효화("이 기기만 로그아웃"). 필요해지면 `User.tokenVersion` 한 칸으로
> "이전 토큰 전부 무효" 정도는 값싸게 얻을 수 있다.

### 코드 위치와 보호 범위

발급·검증·미들웨어를 **`packages/server-core`의 auth entry**에 둔다 (`@lop/server-core/auth`).
`express`/`postgres`/`redis`가 이미 그 모양이고, 인증 미들웨어는 `validation.middleware`·
`error.middleware`와 같은 성격이라 자리가 맞는다. `jsonwebtoken` 의존성도 server-core에 추가한다.

| 앱 | 역할 |
|---|---|
| lobby-server | 발급 + 검증 |
| matchmaking-server | 검증 — 남의 이름으로 매칭 신청 차단 |
| Unity 룸 서버 | 검증 (§7) |

미들웨어가 `Authorization: Bearer`를 검증해 `req.userId`를 채운다. 경로에 userId가 박힌 라우트
(`/user/:id/...`)는 **경로의 id와 토큰의 userId가 다르면 403**이다. URL 모양을 그대로 두고 소유권만
확인하므로 클라 변경이 최소다. `/auth/*` 두 개만 무인증이다.

## 6. ③ 클라이언트 구조

### 핵심 타입 (`Assets/Scripts/Auth/`)

```
AuthenticationService  (순수 C#, RootLifetimeScope 등록)
  UniTask<AuthSession> SignInAsync(AuthProvider)
  AuthSession Current { get; }        // userId, accessToken, expiresAt
  bool IsSignedIn { get; }
  void SignOut()

IAuthCredentialStore → PlayerPrefsAuthCredentialStore
  익명 자격증명 {providerUserId, secret} 저장/로드/삭제

enum AuthProvider { Anonymous, GooglePlayGames, GameCenter }
```

> **저장 위치의 한계를 명시해 둔다**: `PlayerPrefs`는 암호화되지 않는다(에디터는 레지스트리/plist,
> 기기에서는 앱 샌드박스 안 평문). 기기를 물리적으로 만질 수 있는 사람은 secret을 꺼낼 수 있고, 그것은
> 곧 그 익명 계정이다. 지금 단계에서 수용하는 이유는 ① 익명 계정에 지킬 가치가 쌓이기 전이고
> ② Unity Authentication도 세션 토큰을 `PlayerPrefs`에 캐시하는 등 이 수준이 모바일 게임의 통상
> 관행이기 때문이다. 계정에 결제·재화가 붙는 시점에는 플랫폼 보안 저장소(Keychain / Keystore)로
> 옮긴다 — `IAuthCredentialStore` 인터페이스를 두는 이유가 그 교체 지점을 미리 만들어 두는 것이다.

`SignInAsync(Anonymous)`는 저장된 자격증명이 있으면 `/auth/login`, 없으면 `/auth/anonymous`를 친다.

### 기존 코드 정리

| 대상 | 처리 |
|---|---|
| `LoginService`(MonoSingleton) + `GuestLogin`(MonoBehaviour) | `AuthenticationService` 하나로 통합. MonoBehaviour일 이유가 사라져 순수 C# + VContainer로 |
| `LoginType` | `AuthProvider`로. `Guest` → `Anonymous` (기기 ID를 안 쓰게 되어 실제로 익명 계정이 됨). **UI 문구는 "게스트로 시작" 유지** |
| `User` 생성자의 `username = DeviceIdentifier.Current` | 삭제. 계정 생성은 서버 몫 |
| `LoginResult` | 삭제. `AuthSession`이 대체하며 `userId`가 `UserDataStore`로 실제로 흘러간다 |

**로그인 팝업의 반환 타입**: `LoginView`는 `IResultView<AuthSession>`이 된다. 기존 다이얼로그 서비스
패턴대로 **결과를 확정하는 주체는 ViewModel**이다 — `LoginViewModel`이 사용자가 고른 provider로
`AuthenticationService.SignInAsync`를 호출하고, 성공한 `AuthSession`을 결과로 확정한다(확정 = 닫기 신호).

실패하면 **결과를 확정하지 않는다.** 모달은 열린 채 에러 문구를 띄우고 사용자가 다시 시도할 수 있게
한다. 로그인은 `AutoClose = false`인 필수 모달이라 "실패했는데 닫혀서 아무것도 못 하는" 상태가
생기면 안 된다.
| `DeviceIdentifier` | 신원으로는 안 쓰임. `-name` 인자를 읽는 부분만 프로필 이름 추출로 승계 (아래) |

### 토큰을 요청에 싣기

`LOPWebRequestInterceptor.OnBeforeRequest`가 `Authorization: Bearer`를 붙인다. 이 인터셉터는
static이라 DI가 안 되는데, `RootLifetimeScope`가 이미 `GlobalMessagePipe.SetProvider`로 같은 문제를
푸는 방식이 있으므로 그 자리에서 세션 참조를 넣는다.

### 갱신은 배경 타이머로

`OnBeforeRequest`는 동기 함수라 그 안에서 재로그인을 기다릴 수 없다. `AuthenticationService`가
**만료 5분 전에 스스로 재로그인**해 둔다. 요청 시점엔 항상 유효한 토큰이 있으므로 인터셉터는 값만 읽는다.

> **한계(의도적)**: 그럼에도 401이 나면(서버가 새 비밀키로 재시작 등) **그 요청 한 건은 실패**하고
> 클라는 재로그인만 해둔다. 다음 요청부터 정상이다. 요청 단위 자동 재시도를 넣지 않는 이유는
> `GameFramework.WebRequest<T>`가 **생성자에서 곧바로 요청을 발사**해서(`WebRequest.cs`) 만들어 둔
> 요청을 다시 쏠 수 없기 때문이다. 재시도하려면 호출부를 전부 감싸야 하는데, 사전 갱신이 있으면
> 실제로 밟히는 일이 드물어 값을 못 한다. 문제가 관측되면 그때 넣는다.

### 여러 계정 테스트 — 프로필

자격증명 저장 키에 프로필 이름을 섞는다. 프로필이 다르면 다른 익명 계정이 된다.

```
LOP.Auth.{profile}.Credential
```

프로필 이름은 **Multiplayer Play Mode가 인스턴스마다 넘기는 `-name` 인자**(Player1/Player2/…)를
그대로 쓴다. `DeviceIdentifier`가 이미 이 인자로 같은 문제(한 PC 두 클라가 같은 유저로 보이는 것)를
풀고 있었으므로, 그 메커니즘을 승계하는 것이지 새 관례를 만드는 것이 아니다. 새 인자
(`-authProfile` 등)를 도입하지 않는 이유도 같다 — MPPM이 자동으로 주는 값이 이미 있다.

`-name`이 없거나 `Player1`이면 프로필은 `default`다.

### Entrance 흐름 변화

| 단계 | 지금 | 바뀐 뒤 |
|---|---|---|
| ① Login | 자동로그인 실패 → 팝업 → 항상 성공(결과는 버려짐) | `SignInAsync`로 세션 확보. 저장된 자격증명이 없으면 팝업 → 익명 계정 생성 |
| ② CheckUser | 이름으로 조회 → 없으면 유저 생성 | **유저 생성 책임이 사라짐**. 프로필·위치·전적 로드만 → `LoadUserComponent`로 개명 |
| ③ JoinLobby / ④ LoadMasterData | 그대로 | 그대로 (요청에 토큰이 실릴 뿐) |

플랫폼 로그인 버튼은 화면에 두되 누르면 "준비 중" 안내다. 클라에 GPGS/Game Center 클래스를 미리
만들지 않는다 — SDK 없이 만든 껍데기는 실제로 붙일 때 어차피 다시 쓰게 된다. 자리를 잡아두는 것은
**서버의 verifier와 `AuthProvider` enum**이고, 그 둘이면 확장에 충분하다.

## 7. ④ 룸 접속 인증

```
[지금]  클라 → { userId: "내가 주장하는 값", token: "token", characterId: 0 }
        서버 → userId가 매치 참가자 목록에 있나?          ← 남의 userId를 알면 통과

[바뀐 뒤] 클라 → { accessToken, characterId }              ← userId를 보내지 않는다
        서버 → ① 토큰 서명·만료 검증
              ② 토큰에서 꺼낸 userId가 매치 참가자인가?
              ③ 통과 시 conn.authenticationData에 그 userId 기록
```

**클라가 자기 userId를 말하지 않는 것**이 핵심이다. 서버가 토큰에서 꺼내므로 위조할 대상이 없다.
이후 스폰 등에서 쓰는 `conn.authenticationData`의 userId도 서버가 확정한 값이 된다.

**JWT 검증 코드는 `GameFramework.Auth`에** — HS256 서명 검증(~50줄, 순수 C#). 앱 비종속이고
EditMode 테스트가 가능한 자리다. 넷코드 Phase 4에서 `InputTimingTracker`/`LeadController`를 같은
이유로 GameFramework에 둔 전례를 따른다.

**비밀키 전달**

| 실행 환경 | 방법 |
|---|---|
| k8s (dev) | `LOP_AUTH_JWT_SECRET` 환경변수 — 이미 `ROOM_ID`/`PORT`를 이 방식으로 받는다 |
| 로컬 에디터 | `EditorPrefs` + `LOP > Auth > Set JWT Secret` 메뉴 |
| 키가 없으면 | **인증 전부 거부 + 기동 시 에러 로그.** 기본값을 두지 않는다 |

에디터에서 `EditorPrefs`를 쓰는 이유는 키가 git에 들어가지 않게 하기 위함이다. dev 서버가 공인 IP에
떠 있어서, 키가 리포에 있으면 누구나 임의 유저로 룸에 들어올 수 있다. 대가는 개발자가 로컬 세팅을
한 번 해야 한다는 것이다.

## 8. ⑤ 에러 처리

| 상황 | 동작 |
|---|---|
| 저장된 자격증명이 서버에 없음 (**개발 중 DB 초기화**) | `/auth/login` 401 → 저장된 자격증명을 버리고 새 익명 계정 생성. 사용자는 새 계정으로 그냥 진입 |
| 서버 다운·네트워크 불가 | 진입 실패. 에러 메시지 + 재시도 |
| 토큰 만료 | 배경 갱신 (§6) |
| 프로필 전환 | `SignOut` → 다음 진입에서 그 프로필의 계정으로 |

첫 줄이 실무에서 가장 자주 밟힌다. dev DB를 밀 때마다 클라가 먹통이 되면 안 된다.

**함께 고치는 것**: `CheckUserComponent`가 예외를 `LogError`만 하고 삼키는 문제. 유저 로드에
실패했는데 로비까지 진행하면 그 뒤가 전부 이상하게 망가진다.

## 9. ⑥ 테스트

도구는 이미 정해진 것을 따른다 — **jest + ts-jest**. matchmaking-server의 구성을 lobby-server에
복제하고 path alias만 교체한다.

```
apps/lobby-server/
  jest.config.js              ← matchmaking 것 복제
  jest.integration.config.js  ← testMatch/globalSetup/maxWorkers:1/timeout 동일
  test/integration/
    globalSetup.ts   testcontainers postgres + prisma migrate deploy
    globalTeardown.ts
    db.ts            rawPrisma + resetTables
    auth.integration.test.ts
  package.json  "test": "jest", "test:integration": "jest -c jest.integration.config.js"
```

| 계층 | 도구 | 대상 |
|---|---|---|
| 단위 | jest | server-core auth — 토큰 발급·검증, 만료, 서명 변조, 다른 키, secret 해시 비교 |
| HTTP E2E | jest + supertest + testcontainers | 아래 시나리오 |
| Unity 단위 | EditMode | `GameFramework.Auth` JWT 검증(**서버가 발급한 실제 토큰을 fixture로** 넣어 클·서 상호운용 확인), credential store, 프로필 키 조합 |
| Unity 통합 | PlayMode | `AuthenticationService`가 로컬 lobby-server에 붙어 익명 생성 → 재로그인 → 갱신. 서버 기동이 필요해 로컬 전용 |
| 수동 | — | 룸 접속 인증, MPPM 2인스턴스가 서로 다른 계정으로 매칭 |

`globalSetup`이 **실제 마이그레이션으로 스키마를 만드는** 방식이라
`UNIQUE(provider, providerUserId)` 같은 제약이 진짜로 검증된다. 그것이 계정 중복 생성을 막는
실제 장치다. 로컬 k8s postgres를 직접 쓰지 않는 이유는 테스트가 개발 DB를 오염시키고 CI에서 못 돌기
때문이다.

### E2E 시나리오 (전부 실패하는 테스트부터)

```
익명 가입        POST /auth/anonymous → User·UserIdentity 각 1행, 토큰 유효
재로그인         받은 자격증명으로 /auth/login → 같은 userId
잘못된 secret    /auth/login → 401, 계정은 그대로
없는 신원        /auth/login → 401 (계정 생성 안 됨)
미구현 provider  GOOGLE_PLAY_GAMES로 로그인 → 501
무인증 요청      토큰 없이 GET /user/:id → 401
타인 자원        남의 userId 경로 접근 → 403
만료 토큰        exp가 지난 토큰 → 401
중복 방지        같은 자격증명으로 동시 로그인 2회 → 계정 1개
```

### CI

`backend-ci.yml`(push·PR에서 실행)에 lobby 통합 스텝을 추가한다.

```yaml
- name: 통합 테스트 (진짜 DB — lobby)
  run: pnpm --filter lobby-server run test:integration
```

## 10. 작업 순서

순서가 곧 안전장치다. 병렬 작업(다른 머신에서 백엔드 수정 중)과의 충돌도 이 순서로 최소화한다.

```
1) server-core auth entry + 토큰 유닛 테스트        신규 파일만 — 충돌 0
2) 클라 AuthenticationService + 저장 + 프로필        Unity 리포 — 백엔드와 무관
3) lobby-server 테스트 하니스                       신규 파일만 — 충돌 0
4) 스키마 + 마이그레이션 + /auth 라우트              ← 기존 파일 수정. 직전에 pull
5) 미들웨어 적용 (기존 API 보호)                     ← 이 시점부터 구 클라는 못 붙는다
6) 클라 Entrance 흐름 교체
7) 룸 인증 (클·서 동시)
```

- **5)를 뒤로 미룬 이유**: 미들웨어를 먼저 켜면 그 순간부터 클라 작업 내내 모든 API가 401이라
  아무것도 확인할 수 없다.
- **4)를 늦게 두는 이유**: Prisma 마이그레이션은 파일명 타임스탬프 순으로 적용돼, 양쪽이 같은 시기에
  `User`를 건드리면 순서가 꼬인다. 직전에 pull하면 그 창이 최소가 된다.

**수정하는 기존 파일** (충돌 감시 대상): `schema.prisma`, `server-core/package.json`,
`lobby-server/package.json`·`app.ts`, `backend-ci.yml`, 그리고 Unity 클라의 로그인·Entrance 경로.

## 11. 산업 표준 매핑

이 설계는 새 발명이 아니라 표준 조립이다. 명명과 구조를 어디에 맞췄는지 남긴다.

| 우리 것 | 대응하는 표준 |
|---|---|
| 익명 계정 → 나중 연동(link) | Firebase Auth, Unity Authentication, PlayFab, Nakama가 공통으로 쓰는 "anonymous first, link later" |
| `User` 1 : N `UserIdentity` | Firebase `providerData[]`, PlayFab linked identities, Nakama linked accounts, EOS product user ↔ external accounts |
| 기기 ID를 계정 식별자로 쓰지 않음 | PlayFab이 device-ID 로그인 대신 저장된 랜덤 ID를 권장하는 것과 같은 이유 (스푸핑 가능·플랫폼 정책으로 값이 불안정) |
| `AuthenticationService.SignInAsync` | Unity Authentication의 `AuthenticationService` / `SignInAnonymouslyAsync` |
| provider / `AuthProvider` | Firebase "sign-in provider", UGS "identity provider" |
| 재로그인 기반 갱신 (리프레시 토큰 없음) | PlayFab 세션 티켓, EOS Connect 토큰 만료 시 재로그인 |
| 프로필로 계정 분리 | Unity Authentication의 profile (같은 기기에서 여러 계정 테스트 용도) |
| Google Play Games v2 서버 auth code | 클라 `requestServerSideAccess` → 서버가 OAuth 교환 (stub 자리) |
| Game Center 서명 검증 | `fetchItems(forIdentityVerificationSignature:)` → 서버가 애플 공개키로 검증 (stub 자리) |

## 12. Open Decisions

- [ ] 플랫폼 verifier 실제 구현 시점 — 출시 준비 단계. 그때 외부 인증 서비스 채택을 재검토
- [ ] 계정 연동(link) 플로우 — 익명 계정에 구글/애플을 붙이는 UI와 충돌 규칙(이미 다른 계정에
      붙은 신원이면?)
- [ ] 세션 단위 무효화가 필요해지면 `User.tokenVersion`
- [ ] `characterId` 선택 — 캐릭터 선택 UI 슬라이스
- [ ] 토큰 수명 1시간이 적절한지 — 실사용 후 조정
