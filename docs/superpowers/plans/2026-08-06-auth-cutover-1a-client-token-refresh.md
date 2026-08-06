# 인증 cutover 1a — 클라 토큰 갱신 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 액세스 토큰이 만료되기 전에 스스로 갱신되게 만든다 — 그래야 1b(서버가 토큰 검사)를 켜도 1시간 넘는 세션이 안 깨진다.

**Architecture:** `BearerTokenHandler`가 보내기 전에 토큰을 받아오고(만료 임박이면 그 안에서 갱신), 401이면 강제 갱신 후 딱 한 번 다시 보낸다. 토큰을 주는 쪽은 `IAccessTokenProvider` 포트 뒤에 있고, 클라의 `AuthenticationService`가 그걸 구현한다. 동시에 들어온 갱신 요청은 `SingleFlight`가 한 번으로 접는다.

**Tech Stack:** C# / Unity 6 / UniTask 2.5.4 / Unity Test Framework 1.4.6 (NUnit) / VContainer

**스펙:** `docs/superpowers/specs/2026-08-06-auth-cutover-1a-client-token-refresh-design.md`

## Global Constraints

- **저장소 2개**: `GameFramework`(`/Users/insoobae/workspace/LOP/GameFramework`)와 클라(`/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client`). 양쪽 다 **본 체크아웃의 `feature/auth-cutover-1a-token-refresh` 브랜치**에 이미 있다.
- **워크트리를 만들지 말 것.** 클라 `Packages/manifest.json`이 `file:../../GameFramework` 상대 경로라 워크트리에서는 패키지 참조가 깨지고, Unity가 여는 것도 본 체크아웃뿐이다(`Library/`가 거기만 있다).
- **`git add`는 경로를 명시한다. `-A`나 `.` 금지.** 클라 본 체크아웃에 다른 작업의 미추적 파일이 많다(`Assets/Scripts/FlappyRaceSlice/`, `FlappyCalibration/`, `docs/.../2026-07-18-flappy-sim-judge-redesign.md` 등). 지난 슬라이스에서 실제로 한 건이 섞여 들어가 되돌린 적이 있다. `Assets/Art`는 서브모듈 포인터가 이미 수정 상태이니 **건드리지 않는다**.
- **`.meta`는 Unity가 만든 것을 함께 커밋한다.** 직접 만들거나 수정하지 않는다. 새 폴더에도 `.meta`가 생긴다.
- **UnityMCP는 매 호출에 `unity_instance`를 명시한다.** `mcpforunity://instances`에서 `name`이 `LeagueOfPhysical-Client`인 인스턴스의 전체 `id`(`Name@hash`)를 읽어 쓴다. **서버 인스턴스를 건드리지 않는다.**
- **테스트는 클라 Unity 프로젝트에서 EditMode로 돌린다.** GameFramework는 패키지라 단독으로 컴파일되지 않는다 — 그래서 **클라가 컴파일되지 않으면 GameFramework 테스트도 못 돌린다.** 태스크 경계가 그 제약에 맞춰져 있으니 순서를 지킬 것.
- **기준선**: EditMode 전체 412건 통과(GameFramework 어셈블리 146건). 이 계획으로 **9건 추가** 예정.
- **주석은 "왜"만, 쉬운 말로.** 코드로 자명한 것은 쓰지 않는다.
- 커밋 메시지 끝에 `Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>`.

---

## 파일 구조

| 저장소 | 파일 | 책임 |
|---|---|---|
| GF | `Runtime/Scripts/Threading/SingleFlight.cs` | 같은 작업의 동시 호출을 실행 1번으로 접는다 |
| GF | `Runtime/Scripts/Http/IAccessTokenProvider.cs` | 요청에 실을 토큰을 주는 포트(필요하면 갱신까지) |
| GF | `Runtime/Scripts/Http/BearerTokenHandler.cs` | 헤더 부착 + 401 재시도 1회 |
| GF | `Tests/Runtime/Threading/SingleFlightTests.cs` | 접기·캐시 안 함·실패 전파 |
| GF | `Tests/Runtime/Http/FakeAccessTokenProvider.cs` | 토큰을 정해주는 가짜 공급자 |
| GF | `Tests/Runtime/Http/BearerTokenHandlerTests.cs` | 재시도 경로 |
| 클라 | `Assets/Scripts/Auth/AuthenticationService.cs` | 포트 구현 — 갱신 = 저장된 자격증명으로 재로그인 |
| 클라 | `Assets/Scripts/Auth/DeferredAccessTokenProvider.cs` | 정적 `HttpClient`와 DI 배선의 시차를 흡수 |
| 클라 | `Assets/Scripts/WebAPI/WebAPI.cs` | 공급자 타입 교체 |
| 클라 | `Assets/Scripts/RootLifetimeScope.cs` | 실제 서비스를 꽂는다 |

**태스크 순서의 이유**: Task 1·2는 **추가만** 해서 컴파일이 계속 살아 있다. Task 3만 `BearerTokenHandler`의 생성자를 바꾸는데(=클라가 깨진다), 배선 교체를 **같은 태스크 안에서** 해 컴파일을 복구한다.

---

## Task 1: `SingleFlight<T>`

**Files:**
- Create: `GameFramework/Runtime/Scripts/Threading/SingleFlight.cs`
- Test: `GameFramework/Tests/Runtime/Threading/SingleFlightTests.cs`

**Interfaces:**
- Consumes: (없음)
- Produces: `GameFramework.Threading.SingleFlight<T>` — `UniTask<T> RunAsync(Func<UniTask<T>> operation)`

**배경:** 로비 진입처럼 요청 여러 개가 동시에 나가는 순간, 만료가 임박했으면 셋 다 각자 재로그인한다. 그중 하나가 401을 맞으면 `AuthenticationService`가 자격증명을 지우고 새 계정을 만든다. 이걸 막는 부품이다.

- [ ] **Step 1: 실패하는 테스트 작성**

`GameFramework/Tests/Runtime/Threading/SingleFlightTests.cs`:

```csharp
using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using GameFramework.Threading;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GameFramework.Tests.Threading
{
    public class SingleFlightTests
    {
        [UnityTest]
        public IEnumerator 동시에_들어온_호출은_한_번만_실행된다() => UniTask.ToCoroutine(async () =>
        {
            var flight = new SingleFlight<int>();
            var gate = new UniTaskCompletionSource<int>();
            int executions = 0;

            UniTask<int> Operation()
            {
                executions++;
                return gate.Task;
            }

            UniTask<int> first = flight.RunAsync(Operation);
            UniTask<int> second = flight.RunAsync(Operation);
            UniTask<int> third = flight.RunAsync(Operation);

            gate.TrySetResult(42);
            int[] results = await UniTask.WhenAll(first, second, third);

            Assert.That(executions, Is.EqualTo(1));
            Assert.That(results, Is.EqualTo(new[] { 42, 42, 42 }));
        });

        [UnityTest]
        public IEnumerator 끝난_뒤에_부르면_다시_실행된다() => UniTask.ToCoroutine(async () =>
        {
            //  결과를 캐시해 버리면 토큰이 만료돼도 영영 갱신되지 않는다.
            var flight = new SingleFlight<int>();
            int executions = 0;

            UniTask<int> Operation()
            {
                executions++;
                return UniTask.FromResult(executions);
            }

            await flight.RunAsync(Operation);
            await flight.RunAsync(Operation);

            Assert.That(executions, Is.EqualTo(2));
        });

        [UnityTest]
        public IEnumerator 실패는_모든_대기자에게_전달된다() => UniTask.ToCoroutine(async () =>
        {
            var flight = new SingleFlight<int>();
            var gate = new UniTaskCompletionSource<int>();

            UniTask<int> first = flight.RunAsync(() => gate.Task);
            UniTask<int> second = flight.RunAsync(() => gate.Task);

            gate.TrySetException(new InvalidOperationException("갱신 실패"));

            Assert.That(await CatchAsync(first), Is.InstanceOf<InvalidOperationException>());
            Assert.That(await CatchAsync(second), Is.InstanceOf<InvalidOperationException>());
        });

        [UnityTest]
        public IEnumerator 실패한_뒤에_부르면_다시_시도한다() => UniTask.ToCoroutine(async () =>
        {
            //  실패를 캐시하면 네트워크가 한 번 끊긴 뒤로 영영 갱신을 시도하지 않게 된다.
            var flight = new SingleFlight<int>();
            int executions = 0;

            UniTask<int> Failing()
            {
                executions++;
                return UniTask.FromException<int>(new InvalidOperationException("갱신 실패"));
            }

            await CatchAsync(flight.RunAsync(Failing));
            await CatchAsync(flight.RunAsync(Failing));

            Assert.That(executions, Is.EqualTo(2));
        });

        //  NUnit의 Throws 제약은 UniTask를 다루지 못해서 직접 잡는다.
        private static async UniTask<Exception> CatchAsync(UniTask<int> task)
        {
            try
            {
                await task;
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Unity 에디터가 켜져 있어야 한다(없으면 사람에게 요청). `mcpforunity://instances`에서 `LeagueOfPhysical-Client`의 전체 id를 읽고, `run_tests`를 EditMode로 실행(`unity_instance` 명시).

Expected: **컴파일 실패** — `SingleFlight` 타입 없음(`CS0246`). 컴파일 에러는 `read_console`로 확인한다.

- [ ] **Step 3: 구현**

`GameFramework/Runtime/Scripts/Threading/SingleFlight.cs`:

```csharp
using System;
using Cysharp.Threading.Tasks;

namespace GameFramework.Threading
{
    /// <summary>같은 작업이 이미 돌고 있으면 새로 시작하지 않고 그 결과를 함께 기다린다.
    /// 동시에 들어온 호출 N개를 실제 실행 1번으로 접는다.</summary>
    /// <remarks>Unity 메인 스레드 전용이라 락이 없다.</remarks>
    public class SingleFlight<T>
    {
        private bool inFlight;
        private UniTask<T> pending;

        public UniTask<T> RunAsync(Func<UniTask<T>> operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (inFlight)
            {
                return pending;
            }

            inFlight = true;

            //  Preserve가 없으면 두 번째 대기자가 터진다 — UniTask는 기본적으로 한 번만 await할 수 있다.
            pending = RunAndReleaseAsync(operation).Preserve();
            return pending;
        }

        private async UniTask<T> RunAndReleaseAsync(Func<UniTask<T>> operation)
        {
            try
            {
                return await operation.Invoke();
            }
            finally
            {
                //  성공이든 실패든 자리를 비운다. 결과를 캐시하지 않으므로 다음 호출은 새로 실행된다.
                inFlight = false;
            }
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

`run_tests` EditMode 재실행. Expected: **416 통과 / 0 실패** (412 + 4).

- [ ] **Step 5: 커밋** (GameFramework 저장소)

```bash
cd /Users/insoobae/workspace/LOP/GameFramework
git add Runtime/Scripts/Threading Tests/Runtime/Threading
git status --short   # .meta가 함께 잡혔는지 눈으로 확인
git commit -m "feat(threading): SingleFlight — 같은 작업의 동시 호출을 한 번으로 접는다

동시에 들어온 토큰 갱신 요청이 각자 재로그인하면, 그중 하나가 401을 맞았을 때
자격증명을 지우고 새 계정을 만드는 사고로 이어진다. 결과는 캐시하지 않는다 —
캐시하면 한 번 실패한 뒤 영영 갱신을 시도하지 않게 된다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: 포트 신설 + `AuthenticationService` 구현

**Files:**
- Create: `GameFramework/Runtime/Scripts/Http/IAccessTokenProvider.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Auth/AuthenticationService.cs`

**Interfaces:**
- Consumes: `SingleFlight<T>` (Task 1)
- Produces: `GameFramework.Http.IAccessTokenProvider` — `UniTask<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken cancellationToken)`. `AuthenticationService`가 이걸 구현한다.

**이 태스크는 추가만 한다 — 기존 호출부를 하나도 안 건드리므로 컴파일이 계속 살아 있다.**

- [ ] **Step 1: 포트 작성**

`GameFramework/Runtime/Scripts/Http/IAccessTokenProvider.cs`:

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameFramework.Http
{
    /// <summary>요청에 실을 토큰을 준다. 필요하면 갱신까지 하고 준다 — 부르는 쪽은 갱신을 모른다.</summary>
    public interface IAccessTokenProvider
    {
        /// <param name="forceRefresh">만료가 남았어도 새로 받아온다. 401을 맞은 뒤에 쓴다.</param>
        /// <returns>실을 토큰. 로그인 상태가 아니면 null. 갱신에 실패하면 지금 가진 토큰을 그대로 준다
        /// — 갱신 실패는 "이 토큰이 죽었다"는 뜻이 아니라 서버에 못 물어봤다는 뜻이다.</returns>
        UniTask<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken cancellationToken);
    }
}
```

- [ ] **Step 2: `AuthenticationService`가 포트를 구현하도록 수정**

`Assets/Scripts/Auth/AuthenticationService.cs`:

using 추가 (`System.Threading`, `GameFramework.Threading`). `GameFramework.Http`는 이미 있다.

클래스 선언을 바꾼다:

```csharp
    public class AuthenticationService : IAccessTokenProvider
```

필드 추가 (`credentialStore` 옆):

```csharp
        private readonly SingleFlight<string> refreshFlight = new SingleFlight<string>();
```

**`RefreshIfNeededAsync` 메서드 전체를 아래 두 메서드로 교체한다.** (`RefreshIfNeededAsync`는 호출자가 0곳이라 삭제해도 안전하다.)

```csharp
        /// <summary>요청에 실을 토큰을 준다. 만료가 임박했거나 강제 갱신이면 저장된 자격증명으로 다시
        /// 로그인해 갈아끼운다.</summary>
        public async UniTask<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
        {
            if (IsSignedIn == false)
            {
                return null;
            }

            if (forceRefresh == false &&
                Current.Token.NeedsRefresh(DateTimeOffset.UtcNow, AccessTokenInfo.DefaultRefreshMargin) == false)
            {
                return AccessToken;
            }

            //  동시에 들어온 요청들이 각자 로그인하지 않도록 한 번으로 접는다.
            //  cancellationToken을 갱신에 넘기지 않는 이유: 갱신은 여러 요청이 함께 기다리는 작업이라,
            //  한 요청이 취소됐다고 죽이면 나머지가 말려든다. 시간 상한은 로그인 호출의 HTTP 타임아웃이 준다.
            return await refreshFlight.RunAsync(RefreshAsync);
        }

        private async UniTask<string> RefreshAsync()
        {
            AuthCredential stored = credentialStore.Load();
            if (stored == null)
            {
                return AccessToken;
            }

            try
            {
                AuthSession refreshed = await TryLoginAsync(stored);
                if (refreshed != null)
                {
                    Current = refreshed;
                }
            }
            catch (Exception exception)
            {
                //  갱신 실패는 자격증명이 죽었다는 뜻이 아니다(오프라인일 수도 있다). 계정을 건드리지 않고
                //  지금 토큰을 그대로 돌려준다 — 부르는 쪽은 토큰이 안 바뀐 것을 보고 재시도를 접는다.
                Debug.LogWarning($"[Auth] 토큰 갱신에 실패했습니다(다음 기회에 재시도): {exception.Message}");
            }

            return AccessToken;
        }
```

> `TryLoginAsync`가 `null`을 돌려주는 경우(=서버가 401로 자격증명을 거부)에도 `Current`를 그대로 둔다. 여기서 계정을 지우지 않는 것은 기존 동작 그대로다 — 지우는 판단은 `SignInAsync` 경로만 한다.

- [ ] **Step 3: 컴파일 확인**

`refresh_unity` 후 `read_console`(`unity_instance` 명시)로 `error CS`가 0인지 확인. 이 태스크는 추가만 했으므로 **깨끗해야 한다.**

- [ ] **Step 4: 커밋** (두 저장소 각각)

```bash
cd /Users/insoobae/workspace/LOP/GameFramework
git add Runtime/Scripts/Http/IAccessTokenProvider.cs Runtime/Scripts/Http/IAccessTokenProvider.cs.meta
git commit -m "feat(http): IAccessTokenProvider 포트 — 토큰을 주는 쪽을 뒤로 뺀다

갱신은 네트워크 왕복이라 기다릴 수 있어야 한다. 쓰는 쪽(BearerTokenHandler)이
필요한 모양을 정의하므로 포트를 Http에 둔다 — Http가 Auth를 몰라도 된다.
이름은 Kiota/ASP.NET Core WASM의 동명 인터페이스, forceRefresh는 MSAL에서 왔다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Auth/AuthenticationService.cs
git commit -m "feat(auth): AuthenticationService가 IAccessTokenProvider를 구현한다

갱신 판단(만료 5분 전)과 재로그인을 한 진입점으로 모으고 SingleFlight로 감쌌다.
호출자가 0곳이던 RefreshIfNeededAsync는 GetAccessTokenAsync(false, ct)로 흡수돼 삭제.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: 핸들러 전환 + 배선

**Files:**
- Modify: `GameFramework/Runtime/Scripts/Http/BearerTokenHandler.cs`
- Create: `GameFramework/Tests/Runtime/Http/FakeAccessTokenProvider.cs`
- Modify: `GameFramework/Tests/Runtime/Http/BearerTokenHandlerTests.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Auth/DeferredAccessTokenProvider.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/WebAPI/WebAPI.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/RootLifetimeScope.cs`

**Interfaces:**
- Consumes: `IAccessTokenProvider`(Task 2), `AuthenticationService`(Task 2)
- Produces: `BearerTokenHandler(HttpMessageHandler innerHandler, IAccessTokenProvider accessTokenProvider)`, `WebAPI.SetAccessTokenProvider(IAccessTokenProvider provider)`

> **이 태스크는 중간에 컴파일이 깨진다.** 생성자 시그니처가 바뀌는 순간 `WebAPI`가 안 맞는다. 배선까지 **한 태스크 안에서** 끝내 복구한다. 그래서 테스트 실행은 Step 6에서 한 번만 한다.

- [ ] **Step 1: 가짜 공급자 작성**

`GameFramework/Tests/Runtime/Http/FakeAccessTokenProvider.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework.Http;

namespace GameFramework.Tests.Http
{
    /// <summary>토큰을 정해줄 수 있는 가짜 공급자. forceRefresh 값을 호출 순서대로 기록한다.</summary>
    public sealed class FakeAccessTokenProvider : IAccessTokenProvider
    {
        private readonly Func<bool, string> resolve;

        public List<bool> Calls { get; } = new List<bool>();

        public FakeAccessTokenProvider(Func<bool, string> resolve)
        {
            this.resolve = resolve;
        }

        public static FakeAccessTokenProvider Returning(string accessToken)
        {
            return new FakeAccessTokenProvider(_ => accessToken);
        }

        public UniTask<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
        {
            Calls.Add(forceRefresh);
            return UniTask.FromResult(resolve(forceRefresh));
        }
    }
}
```

- [ ] **Step 2: 실패하는 테스트 작성**

`BearerTokenHandlerTests.cs`. 기존 두 테스트의 `() => "abc.def.ghi"` / `() => null`을 `FakeAccessTokenProvider.Returning("abc.def.ghi")` / `FakeAccessTokenProvider.Returning(null)`로 바꾸고, 파일 상단 using에 `System.Collections.Generic`을 추가한 뒤 아래 5개를 클래스에 더한다.

```csharp
        [UnityTest]
        public IEnumerator 성공하면_갱신을_부르지_않는다() => UniTask.ToCoroutine(async () =>
        {
            var fake = FakeHttpMessageHandler.Returning(200, "{}");
            var provider = FakeAccessTokenProvider.Returning("old");
            var client = new HttpClient(new BearerTokenHandler(fake, provider));

            await client.SendAsync(HttpRequestMessage.Get("http://example.com"));

            Assert.That(provider.Calls, Is.EqualTo(new[] { false }));
            Assert.That(fake.Requests.Count, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator _401이면_갱신해서_한_번_다시_보낸다() => UniTask.ToCoroutine(async () =>
        {
            var sent = new List<string>();

            //  보낼 때의 헤더를 그 자리에서 남긴다 — 재전송은 같은 요청 객체를 다시 쓰므로
            //  나중에 Requests에서 읽으면 최종값 하나만 보인다.
            var fake = new FakeHttpMessageHandler((request, _) =>
            {
                sent.Add(request.Headers.TryGetValue("Authorization", out string value) ? value : null);
                return UniTask.FromResult(new HttpResponseMessage(sent.Count == 1 ? 401 : 200, "{}"));
            });

            var provider = new FakeAccessTokenProvider(forceRefresh => forceRefresh ? "new" : "old");
            var client = new HttpClient(new BearerTokenHandler(fake, provider));

            HttpResponseMessage response = await client.SendAsync(HttpRequestMessage.Get("http://example.com"));

            Assert.That(sent, Is.EqualTo(new[] { "Bearer old", "Bearer new" }));
            Assert.That(provider.Calls, Is.EqualTo(new[] { false, true }));
            Assert.That(response.StatusCode, Is.EqualTo(200));
        });

        [UnityTest]
        public IEnumerator 갱신해도_토큰이_그대로면_다시_보내지_않는다() => UniTask.ToCoroutine(async () =>
        {
            //  갱신이 실패하면 공급자는 지금 가진 토큰을 그대로 준다. 방금 거부당한 토큰을 다시
            //  보내봐야 결과가 같으므로 헛수고를 하지 않는다.
            var fake = FakeHttpMessageHandler.Returning(401, "{}");
            var provider = FakeAccessTokenProvider.Returning("same");
            var client = new HttpClient(new BearerTokenHandler(fake, provider));

            HttpResponseMessage response = await client.SendAsync(HttpRequestMessage.Get("http://example.com"));

            Assert.That(fake.Requests.Count, Is.EqualTo(1));
            Assert.That(response.StatusCode, Is.EqualTo(401));
        });

        [UnityTest]
        public IEnumerator 로그인_상태가_아니면_다시_보내지_않는다() => UniTask.ToCoroutine(async () =>
        {
            var fake = FakeHttpMessageHandler.Returning(401, "{}");
            var provider = FakeAccessTokenProvider.Returning(null);
            var client = new HttpClient(new BearerTokenHandler(fake, provider));

            HttpResponseMessage response = await client.SendAsync(HttpRequestMessage.Get("http://example.com"));

            Assert.That(fake.Requests.Count, Is.EqualTo(1));
            Assert.That(response.StatusCode, Is.EqualTo(401));
        });

        [UnityTest]
        public IEnumerator 다시_보낸_것도_401이면_그대로_돌려준다() => UniTask.ToCoroutine(async () =>
        {
            //  재전송은 루프가 아니라 한 번뿐이다 — 여기서 안 멈추면 401이 무한히 반복된다.
            var fake = FakeHttpMessageHandler.Returning(401, "{}");
            var provider = new FakeAccessTokenProvider(forceRefresh => forceRefresh ? "new" : "old");
            var client = new HttpClient(new BearerTokenHandler(fake, provider));

            HttpResponseMessage response = await client.SendAsync(HttpRequestMessage.Get("http://example.com"));

            Assert.That(fake.Requests.Count, Is.EqualTo(2));
            Assert.That(provider.Calls, Is.EqualTo(new[] { false, true }));
            Assert.That(response.StatusCode, Is.EqualTo(401));
        });
```

> 스펙 §8의 "보내기 전 forceRefresh=false로 부른다"는 첫 테스트의 `provider.Calls` 단언에 포함된다. 테스트 메서드는 5개, 확인하는 행동은 6가지다.

- [ ] **Step 3: 핸들러 구현**

`GameFramework/Runtime/Scripts/Http/BearerTokenHandler.cs` **전체 교체**:

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameFramework.Http
{
    /// <summary>요청에 Authorization 헤더를 붙인다. 401이면 토큰을 갱신해 딱 한 번 다시 보낸다.</summary>
    public class BearerTokenHandler : DelegatingHandler
    {
        private const long HttpStatusUnauthorized = 401;

        private readonly IAccessTokenProvider accessTokenProvider;

        public BearerTokenHandler(HttpMessageHandler innerHandler, IAccessTokenProvider accessTokenProvider) : base(innerHandler)
        {
            this.accessTokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));
        }

        public override async UniTask<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            //  매번 물어보는 이유: 만료가 임박했으면 공급자가 이 안에서 갱신해 새 토큰을 준다.
            string accessToken = await accessTokenProvider.GetAccessTokenAsync(false, cancellationToken);
            SetAuthorization(request, accessToken);

            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode != HttpStatusUnauthorized)
            {
                return response;
            }

            string refreshed = await accessTokenProvider.GetAccessTokenAsync(true, cancellationToken);

            //  토큰이 그대로면 갱신이 실패한 것이다. 방금 거부당한 토큰을 다시 보내봐야 결과가 같으므로
            //  헛수고 대신 원래 401을 돌려준다.
            if (string.IsNullOrEmpty(refreshed) || refreshed == accessToken)
            {
                return response;
            }

            SetAuthorization(request, refreshed);

            //  재전송은 여기 한 번뿐이다 — 이 응답이 또 401이어도 그대로 반환되므로 루프가 될 수 없다.
            return await base.SendAsync(request, cancellationToken);
        }

        private static void SetAuthorization(HttpRequestMessage request, string accessToken)
        {
            //  토큰이 없으면 헤더를 붙이지 않는다 — 빈 Bearer를 보내면 서버가 잘못된 토큰으로 읽는다.
            if (string.IsNullOrEmpty(accessToken))
            {
                return;
            }

            request.Headers["Authorization"] = $"Bearer {accessToken}";
        }
    }
}
```

- [ ] **Step 4: 클라 어댑터 작성**

`Assets/Scripts/Auth/DeferredAccessTokenProvider.cs`:

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework.Http;

namespace LOP
{
    /// <summary>부를 때마다 현재 공급자를 찾아 넘긴다. WebAPI의 HttpClient는 정적이라 DI 배선보다
    /// 먼저 만들어질 수 있어서, 생성 시점의 공급자를 붙들면 영영 null이 된다.
    /// (Lazy와 다르다 — 한 번 계산하고 캐시하는 게 아니라 매번 다시 읽는다.)</summary>
    public class DeferredAccessTokenProvider : IAccessTokenProvider
    {
        private readonly Func<IAccessTokenProvider> resolve;

        public DeferredAccessTokenProvider(Func<IAccessTokenProvider> resolve)
        {
            this.resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
        }

        public UniTask<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
        {
            IAccessTokenProvider provider = resolve.Invoke();

            //  배선 전이면 토큰이 없다 — 헤더를 안 붙이는 것이 기존 동작이다.
            return provider == null
                ? UniTask.FromResult<string>(null)
                : provider.GetAccessTokenAsync(forceRefresh, cancellationToken);
        }
    }
}
```

- [ ] **Step 5: 배선 교체**

`Assets/Scripts/WebAPI/WebAPI.cs` — 필드와 세터를 바꾼다. **`anonymous` 클라이언트와 그 위 주석은 손대지 않는다.**

```csharp
        //  static이라 DI가 안 된다 — RootLifetimeScope가 기동 시 공급자를 꽂아 준다.
        private static IAccessTokenProvider accessTokenProvider;

        private static readonly HttpClient authorized =
            new HttpClient(new BearerTokenHandler(new UnityWebRequestHandler(),
                new DeferredAccessTokenProvider(() => accessTokenProvider)));

        private static readonly HttpClient anonymous = new HttpClient(new UnityWebRequestHandler());

        public static void SetAccessTokenProvider(IAccessTokenProvider provider)
        {
            accessTokenProvider = provider;
        }
```

`Assets/Scripts/RootLifetimeScope.cs` — `RegisterBuildCallback` 안의 두 줄을 한 줄로 바꾼다.

```csharp
                //  모든 REST 요청이 현재 세션 토큰을 싣도록 WebAPI에 공급자를 꽂는다.
                WebAPI.SetAccessTokenProvider(container.Resolve<AuthenticationService>());
```

- [ ] **Step 6: 테스트 통과 확인**

`refresh_unity` → `read_console`로 `error CS` 0 확인 → `run_tests` EditMode.

Expected: **421 통과 / 0 실패** (416 + 5).

- [ ] **Step 7: 커밋** (두 저장소 각각)

```bash
cd /Users/insoobae/workspace/LOP/GameFramework
git add Runtime/Scripts/Http/BearerTokenHandler.cs Tests/Runtime/Http
git commit -m "feat(http): BearerTokenHandler에 미리 갱신 + 401 재시도 1회

보내기 전 공급자에게 토큰을 받아오고(만료 임박이면 거기서 갱신됨), 401이면 강제
갱신 후 한 번만 다시 보낸다. 갱신해도 토큰이 그대로면 재전송하지 않는다 —
거부당한 토큰을 다시 보내는 헛수고를 막는다(OkHttp의 무한 루프 처방과 같은 취지).
재전송이 루프가 아니라 직선 한 번이라 무한 반복이 구조적으로 불가능하다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"

cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Auth/DeferredAccessTokenProvider.cs Assets/Scripts/Auth/DeferredAccessTokenProvider.cs.meta \
        Assets/Scripts/WebAPI/WebAPI.cs Assets/Scripts/RootLifetimeScope.cs
git commit -m "feat(webapi): 토큰 공급자를 IAccessTokenProvider로 교체

Func<string>은 기다릴 수 없어 갱신을 넣을 자리가 없었다. 정적 HttpClient가 DI
배선보다 먼저 만들어질 수 있어서 DeferredAccessTokenProvider가 그 시차를 흡수한다
(기존 람다가 하던 역할과 같다).

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## 수동 검증 (사람이 한다)

구현이 끝나면 사람에게 아래를 요청한다. 백엔드(`lop-backend`)와 로컬 k8s가 떠 있어야 한다.

- [ ] **1. 회귀 없음** — 로그인 → 로비 진입 → 매칭 요청 → 매칭 취소가 이전과 동일하게 동작한다.
- [ ] **2. 미리 갱신 + 합치기** — `AccessTokenInfo.DefaultRefreshMargin`을 일시적으로 `TimeSpan.FromMinutes(59)`로 바꾼다(토큰 수명이 1시간이라 모든 요청이 갱신을 유발하게 된다). 로비에 진입해 콘솔에서 `/auth/login` 호출이 **그 한 순간에 1번만** 나가는지 확인. 합치기가 없으면 동시 요청 수만큼 찍힌다. **확인 후 값을 5분으로 되돌린다.**

**이 조각에서 확인할 수 없는 것:** 401 재시도 경로. 서버가 아직 토큰을 검사하지 않아 401이 나오지 않는다. 단위 테스트로 덮었고, 실제 경로 확인은 **1b 배포 시 필수 항목**이다.

---

## 완료 조건

- [ ] EditMode 421 통과 / 0 실패
- [ ] 클라 컴파일 `error CS` 0
- [ ] 수동 검증 2건 통과
- [ ] 양쪽 저장소 커밋이 `feature/auth-cutover-1a-token-refresh`에 있고, 다른 작업의 미추적 파일이 섞이지 않았다 (`git show --stat`으로 확인)
