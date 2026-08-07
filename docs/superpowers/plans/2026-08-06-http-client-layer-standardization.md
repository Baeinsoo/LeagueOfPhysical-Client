# HTTP 클라이언트 계층 표준화 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** GameFramework의 HTTP 계층을 .NET `HttpClient` + `DelegatingHandler` 구조로 재정리해, 연결 실패와 HTTP 오류를 타입으로 구분하고 핸들러가 요청 전후에 `await`·재전송할 수 있게 만든다.

**Architecture:** 요청을 만드는 것과 보내는 것을 분리한다. `HttpClient`가 핸들러 체인을 타고 `UnityWebRequestHandler`(체인의 끝)까지 내려가 실제 전송한다. 체인은 제네릭이 아니라 문자열 본문을 든 `HttpResponseMessage`만 다루고, `T`로의 역직렬화와 상태코드 기반 예외는 그 위 타입드 계층이 맡는다. 체인의 끝을 가짜로 갈아끼우면 나머지 전부가 Unity 없이 EditMode에서 검증된다.

**Tech Stack:** Unity 6000.3.16f1, C#, UniTask 2.5.4, Newtonsoft.Json, UnityWebRequest, NUnit(Unity Test Framework 1.4.6)

**Spec:** `docs/superpowers/specs/2026-08-06-http-client-layer-standardization-design.md`

## Global Constraints

- 대상 리포 3개: `/Users/insoobae/workspace/LOP/GameFramework`, `.../LeagueOfPhysical-Client`, `.../LeagueOfPhysical-Server`. 브랜치명은 셋 다 **`feature/http-client-standardization`**
- **클라·서버는 워크트리를 쓰지 않는다** — `manifest.json`이 GameFramework를 상대경로(`file:../../GameFramework`)로 참조해 워크트리 위치에서는 해석되지 않는다. 본체 체크아웃에서 브랜치를 전환해 작업한다
- 새 네임스페이스는 **`GameFramework.Http`**, 위치는 `GameFramework/Runtime/Scripts/Http/`
- 전송 타입 이름은 **`HttpRequestMessage` / `HttpResponseMessage`** — 짧은 `HttpRequest`/`HttpResponse`는 **금지**다. `LOP.HttpResponse`가 클·서 양쪽에서 모든 응답 DTO의 베이스 클래스라 충돌한다
- 상태코드 기반 예외는 **핸들러 체인이 아니라 그 위 타입드 계층에서만** 던진다. 체인은 4xx·5xx여도 `HttpResponseMessage`를 그대로 반환한다 (슬라이스 1의 401 재시도 핸들러가 401을 볼 수 있어야 한다)
- 전송 자체가 실패했을 때만 `HttpRequestException.StatusCode == null`. HTTP 오류는 항상 값이 들어 있다
- 비동기 타입은 **`UniTask`** (`System.Threading.Tasks.Task` 아님). `CancellationToken`을 `SendAsync`까지 관통시킨다
- 옛 계층(`Runtime/Scripts/WebRequest/`) 삭제는 **Task 8에서만** 한다. Task 1~7 동안 두 계층이 공존해야 어느 리포도 깨지지 않는다
- **공존 기간의 함정 — `HttpMethod` 가 두 곳에 있다.** Task 1~7 동안 `GameFramework.HttpMethod`(옛, `IWebRequestParam.cs` 안)와 `GameFramework.Http.HttpMethod`(새)가 동시에 존재한다. **한 파일에서 `using GameFramework;` 와 `using GameFramework.Http;` 를 둘 다 쓰면서 `HttpMethod` 를 쓰면 모호성 컴파일 에러**가 난다. 새 `WebAPI.cs` 는 `using GameFramework;` 를 넣지 않는다(필요 없다). Task 8 이후에는 새 것 하나만 남아 문제가 사라진다
- `SetForm` / `IMultipartFormSection` 지원은 새 계층에 **만들지 않는다** (사용처 0)
- 모든 신규 테스트는 **일부러 되돌려 실제로 실패하는지 확인**한다
- Unity 에디터가 해당 프로젝트를 열고 있으면 배치모드 실행이 잠금으로 실패한다. 실행 전 닫혀 있어야 한다

### 테스트 실행 명령 (검증된 형식)

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -runTests -batchmode \
  -projectPath /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client \
  -testPlatform EditMode -testFilter "GameFramework.Tests.Http" \
  -testResults /tmp/http-results.xml -logFile /tmp/http-tests.log
```

결과 확인:

```bash
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' /tmp/http-results.xml | head -1
```

### 컴파일 관문 (필수, 테스트와 별개)

**GameFramework EditMode 테스트는 별도 어셈블리라 `Assembly-CSharp`이 깨져도 통과한다.** 지난 슬라이스에서 이걸 몰라 "테스트 통과"가 클라 컴파일을 보증하지 못했다. 클라·서버를 건드리는 태스크는 반드시 아래를 별도로 통과해야 한다.

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -quit -batchmode -projectPath <프로젝트경로> -logFile /tmp/compile.log
grep -c "error CS" /tmp/compile.log   # 0 이어야 한다
```

---

## 파일 구조

### GameFramework — 새로 만드는 것

| 파일 | 책임 | 태스크 |
|---|---|---|
| `Runtime/Scripts/Http/HttpMethod.cs` | GET/POST/PUT/DELETE enum (옛 `IWebRequestParam.cs`에서 이동) | 1 |
| `Runtime/Scripts/Http/HttpJson.cs` | Newtonsoft 직렬화 설정 (옛 `WebRequestJson` 이름 변경) | 1 |
| `Runtime/Scripts/Http/HttpRequestMessage.cs` | 요청 데이터 + 정적 팩토리 | 1 |
| `Runtime/Scripts/Http/HttpResponseMessage.cs` | 응답 데이터 + `EnsureSuccessStatusCode()` | 1 |
| `Runtime/Scripts/Http/HttpRequestException.cs` | `long? StatusCode`, `string ResponseBody` | 1 |
| `Runtime/Scripts/Http/HttpMessageHandler.cs` | 핸들러 추상 | 2 |
| `Runtime/Scripts/Http/DelegatingHandler.cs` | 다음 핸들러를 감싸는 베이스 | 2 |
| `Runtime/Scripts/Http/HttpClient.cs` | 체인 진입점 + 타임아웃 | 2 |
| `Runtime/Scripts/Http/HttpClientJsonExtensions.cs` | 타입드 `SendAsync<T>` | 3 |
| `Runtime/Scripts/Http/BearerTokenHandler.cs` | 토큰이 있으면 Authorization 부착 | 4 |
| `Runtime/Scripts/Http/UnityWebRequestHandler.cs` | 체인의 끝 — 실제 전송 | 5 |
| `Tests/Runtime/Http/FakeHttpMessageHandler.cs` | 테스트용 가짜 전송 | 2 |
| `Tests/Runtime/Http/*Tests.cs` | EditMode 테스트 | 1~4 |

### 클라이언트 — 바꾸는 것

| 파일 | 변경 | 태스크 |
|---|---|---|
| `Assets/Scripts/WebAPI/WebAPI.cs` | 전면 교체 — `UniTask<T>` 반환, 클라이언트 2개 보유, 죽은 메서드 4개 삭제 | 6 |
| `Assets/Scripts/WebAPI/LOPWebRequestInterceptor.cs` | **삭제** | 6 |
| `Assets/Scripts/RootLifetimeScope.cs` | 토큰 공급자 주입 대상을 `WebAPI`로 | 6 |
| `Assets/Scripts/Auth/AuthenticationService.cs` | awaiter 흡수 코드 제거, `HttpRequestException.StatusCode`로 분기 | 6 |
| 호출부 9파일 (아래 목록) | `.response` 한 겹 제거 | 6 |

### 서버 — 바꾸는 것

| 파일 | 변경 | 태스크 |
|---|---|---|
| `Assets/Scripts/WebAPI/WebAPI.cs` | 전면 교체 — 죽은 메서드 1개 삭제 | 7 |
| `Assets/Scripts/WebAPI/LOPWebRequestInterceptor.cs` | **삭제** | 7 |
| `Assets/Scripts/Room/LOPRoom.cs` | 호출 5곳 (fire-and-forget 2곳은 `.Forget()`) | 7 |
| `Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs` | 호출 3곳 | 7 |

---

## Task 0: 브랜치 준비 (컨트롤러가 직접 수행)

- [ ] **Step 1: 3개 리포를 최신으로 당기고 브랜치를 만든다**

```bash
cd /Users/insoobae/workspace/LOP/GameFramework && git checkout main && git pull --ff-only && git checkout -b feature/http-client-standardization
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server && git checkout main && git pull --ff-only && git checkout -b feature/http-client-standardization
# 클라는 이미 feature/http-client-standardization 에 스펙이 커밋돼 있다 — 그대로 사용
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client && git branch --show-current
```

- [ ] **Step 2: Unity 에디터가 두 프로젝트를 열고 있지 않은지 확인**

배치모드 실행이 프로젝트 잠금으로 실패하므로, 두 Unity 에디터 창을 모두 닫는다.

---

## Task 1: Http 기본 타입 — 요청·응답·예외

**Files:**
- Create: `GameFramework/Runtime/Scripts/Http/HttpMethod.cs`
- Create: `GameFramework/Runtime/Scripts/Http/HttpJson.cs`
- Create: `GameFramework/Runtime/Scripts/Http/HttpRequestMessage.cs`
- Create: `GameFramework/Runtime/Scripts/Http/HttpResponseMessage.cs`
- Create: `GameFramework/Runtime/Scripts/Http/HttpRequestException.cs`
- Modify: `GameFramework/Runtime/baegames.GameFramework.Runtime.asmdef`
- Modify: `GameFramework/Tests/Runtime/baegames.GameFramework.Runtime.Tests.asmdef`
- Test: `GameFramework/Tests/Runtime/Http/HttpMessageTypesTests.cs`

**Interfaces:**
- Produces: `GameFramework.Http.HttpMethod` (enum GET/POST/PUT/DELETE), `HttpJson.SerializeObject(object) → string`, `HttpJson.DeserializeObject<T>(string) → T`, `HttpRequestMessage(HttpMethod, string uri, string content = null, string contentType = null)` + `Method`/`Uri`/`Headers`(Dictionary<string,string>)/`Content`/`ContentType` + 정적 팩토리 `Get(uri)`/`Post(uri, object body = null)`/`Put(uri, object body = null)`/`Delete(uri)`, `HttpResponseMessage(long statusCode, string body, IReadOnlyDictionary<string,string> headers = null)` + `StatusCode`/`Body`/`Headers`/`IsSuccessStatusCode`/`EnsureSuccessStatusCode()`, `HttpRequestException(string message)` (StatusCode = null) 와 `HttpRequestException(string message, long statusCode, string responseBody)` + `StatusCode`(long?)/`ResponseBody`

> **왜 asmdef 변경이 Task 1에 있나:** UniTask 참조가 없으면 Task 2부터 컴파일이 안 된다. 한 줄짜리 준비 작업이라 첫 태스크에 접어 넣는다.

- [ ] **Step 1: asmdef 두 개에 UniTask 참조 추가**

`GameFramework/Runtime/baegames.GameFramework.Runtime.asmdef` 의 `references` 배열을 이렇게 만든다:

```json
    "references": [
        "VContainer",
        "baegames.GameFramework.World",
        "UniTask"
    ],
```

`GameFramework/Tests/Runtime/baegames.GameFramework.Runtime.Tests.asmdef` 의 `references` 배열:

```json
    "references": [
        "baegames.GameFramework.Runtime",
        "baegames.GameFramework.World",
        "VContainer",
        "UniTask",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
```

- [ ] **Step 2: 실패하는 테스트를 쓴다**

`GameFramework/Tests/Runtime/Http/HttpMessageTypesTests.cs`:

```csharp
using GameFramework.Http;
using NUnit.Framework;

namespace GameFramework.Tests.Http
{
    public class HttpMessageTypesTests
    {
        [Test]
        public void Put_본문이_있으면_JSON으로_직렬화하고_컨텐트타입을_붙인다()
        {
            var request = HttpRequestMessage.Put("http://example.com/lobby", new { userId = "abc" });

            Assert.That(request.Method, Is.EqualTo(HttpMethod.PUT));
            Assert.That(request.Uri, Is.EqualTo("http://example.com/lobby"));
            Assert.That(request.Content, Does.Contain("abc"));
            Assert.That(request.ContentType, Is.EqualTo("application/json"));
        }

        [Test]
        public void Get_은_본문이_없다()
        {
            var request = HttpRequestMessage.Get("http://example.com/user/1");

            Assert.That(request.Method, Is.EqualTo(HttpMethod.GET));
            Assert.That(request.Content, Is.Null);
            Assert.That(request.Headers, Is.Empty);
        }

        [Test]
        [TestCase(200, true)]
        [TestCase(299, true)]
        [TestCase(300, false)]
        [TestCase(401, false)]
        [TestCase(500, false)]
        public void IsSuccessStatusCode_는_2xx만_참이다(long statusCode, bool expected)
        {
            var response = new HttpResponseMessage(statusCode, string.Empty);

            Assert.That(response.IsSuccessStatusCode, Is.EqualTo(expected));
        }

        [Test]
        public void EnsureSuccessStatusCode_는_2xx가_아니면_상태코드와_본문을_담아_던진다()
        {
            var response = new HttpResponseMessage(401, "{\"message\":\"denied\"}");

            var exception = Assert.Throws<HttpRequestException>(() => response.EnsureSuccessStatusCode());

            Assert.That(exception.StatusCode, Is.EqualTo(401));
            Assert.That(exception.ResponseBody, Is.EqualTo("{\"message\":\"denied\"}"));
        }

        [Test]
        public void 전송_실패용_생성자는_상태코드가_null이다()
        {
            //  이 null이 "서버에 닿지도 못했다"의 유일한 신호다 — 401(서버가 거부)과 반드시 구분돼야
            //  한다. 예전에 이 둘을 뭉개서 오프라인 플레이어의 계정을 지우는 사고가 났다.
            var exception = new HttpRequestException("연결 실패");

            Assert.That(exception.StatusCode, Is.Null);
        }
    }
}
```

- [ ] **Step 3: 테스트를 돌려 실패를 확인한다**

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -runTests -batchmode -projectPath /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client \
  -testPlatform EditMode -testFilter "GameFramework.Tests.Http" \
  -testResults /tmp/http-results.xml -logFile /tmp/http-tests.log
```

기대: 컴파일 에러(`HttpRequestMessage` 없음 등)로 테스트가 아예 안 돈다.

- [ ] **Step 4: `HttpMethod.cs` 를 만든다**

```csharp
namespace GameFramework.Http
{
    public enum HttpMethod
    {
        GET = 0,
        POST = 1,
        PUT = 2,
        DELETE = 3,
    }
}
```

- [ ] **Step 5: `HttpJson.cs` 를 만든다**

옛 `WebRequestJson`과 설정이 같아야 한다 — 직렬화 결과가 달라지면 서버가 못 읽는다.

```csharp
using Newtonsoft.Json;

namespace GameFramework.Http
{
    public static class HttpJson
    {
        private static readonly JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
        };

        public static string SerializeObject(object value)
        {
            return JsonConvert.SerializeObject(value, Formatting.Indented, jsonSerializerSettings);
        }

        public static T DeserializeObject<T>(string value)
        {
            return JsonConvert.DeserializeObject<T>(value, jsonSerializerSettings);
        }
    }
}
```

- [ ] **Step 6: `HttpRequestMessage.cs` 를 만든다**

```csharp
using System.Collections.Generic;

namespace GameFramework.Http
{
    /// <summary>한 번의 HTTP 요청을 담는 데이터. 핸들러가 헤더를 덧붙일 수 있고,
    /// 같은 인스턴스를 다시 보내도 된다(재시도 핸들러가 그렇게 쓴다).</summary>
    public class HttpRequestMessage
    {
        public HttpMethod Method { get; }
        public string Uri { get; }
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();
        public string Content { get; }
        public string ContentType { get; }

        public HttpRequestMessage(HttpMethod method, string uri, string content = null, string contentType = null)
        {
            Method = method;
            Uri = uri;
            Content = content;
            ContentType = contentType;
        }

        public static HttpRequestMessage Get(string uri) => new HttpRequestMessage(HttpMethod.GET, uri);

        public static HttpRequestMessage Delete(string uri) => new HttpRequestMessage(HttpMethod.DELETE, uri);

        public static HttpRequestMessage Post(string uri, object body = null) => Json(HttpMethod.POST, uri, body);

        public static HttpRequestMessage Put(string uri, object body = null) => Json(HttpMethod.PUT, uri, body);

        private static HttpRequestMessage Json(HttpMethod method, string uri, object body)
        {
            return body == null
                ? new HttpRequestMessage(method, uri)
                : new HttpRequestMessage(method, uri, HttpJson.SerializeObject(body), "application/json");
        }
    }
}
```

- [ ] **Step 7: `HttpResponseMessage.cs` 를 만든다**

```csharp
using System.Collections.Generic;

namespace GameFramework.Http
{
    /// <summary>서버가 실제로 답한 내용. 4xx·5xx도 정상적인 "응답"이라 여기까지 온다 —
    /// 예외로 바꿀지는 호출하는 쪽이 EnsureSuccessStatusCode로 정한다.</summary>
    public class HttpResponseMessage
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyHeaders = new Dictionary<string, string>();

        public long StatusCode { get; }
        public string Body { get; }
        public IReadOnlyDictionary<string, string> Headers { get; }

        public HttpResponseMessage(long statusCode, string body, IReadOnlyDictionary<string, string> headers = null)
        {
            StatusCode = statusCode;
            Body = body;
            Headers = headers ?? EmptyHeaders;
        }

        public bool IsSuccessStatusCode => StatusCode >= 200 && StatusCode <= 299;

        public HttpResponseMessage EnsureSuccessStatusCode()
        {
            if (IsSuccessStatusCode == false)
            {
                throw new HttpRequestException($"HTTP {StatusCode} 응답을 받았습니다.", StatusCode, Body);
            }

            return this;
        }
    }
}
```

- [ ] **Step 8: `HttpRequestException.cs` 를 만든다**

```csharp
using System;

namespace GameFramework.Http
{
    public class HttpRequestException : Exception
    {
        /// <summary>서버가 답한 HTTP 상태. <c>null</c>이면 서버에 닿지도 못한 것(연결 실패·타임아웃).
        /// "서버가 거부했다(401)"와 "물어보지 못했다"를 반드시 구분해야 하므로 nullable이다.</summary>
        public long? StatusCode { get; }

        public string ResponseBody { get; }

        public HttpRequestException(string message) : base(message) { }

        public HttpRequestException(string message, long statusCode, string responseBody) : base(message)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }
}
```

- [ ] **Step 9: 테스트를 돌려 통과를 확인한다**

Step 3과 같은 명령. 기대: `total="9" passed="9" failed="0"` (TestCase 5건이 각각 세어진다).

- [ ] **Step 10: 되돌려 실제로 실패하는지 확인한다**

`HttpResponseMessage.IsSuccessStatusCode`를 `StatusCode >= 200`로 바꿔 테스트를 돌린다 → `IsSuccessStatusCode_는_2xx만_참이다(300)`·`(401)`·`(500)`이 실패해야 한다. 확인 후 되돌린다.

- [ ] **Step 11: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/GameFramework
git add Runtime/Scripts/Http Runtime/baegames.GameFramework.Runtime.asmdef \
        Tests/Runtime/Http Tests/Runtime/baegames.GameFramework.Runtime.Tests.asmdef
git commit -m "feat(http): 요청·응답·예외 기본 타입

연결 실패(StatusCode null)와 HTTP 오류(값 있음)를 타입으로 가른다.
이름은 .NET 정식 명칭을 따른다 — LOP.HttpResponse가 모든 응답 DTO의
베이스라 짧은 이름은 충돌한다."
```

> `.meta` 파일은 Unity가 생성한다. 배치모드 실행 후 새로 생긴 `.meta`를 함께 커밋할 것.

---

## Task 2: 핸들러 체인과 HttpClient

**Files:**
- Create: `GameFramework/Runtime/Scripts/Http/HttpMessageHandler.cs`
- Create: `GameFramework/Runtime/Scripts/Http/DelegatingHandler.cs`
- Create: `GameFramework/Runtime/Scripts/Http/HttpClient.cs`
- Create: `GameFramework/Tests/Runtime/Http/FakeHttpMessageHandler.cs`
- Test: `GameFramework/Tests/Runtime/Http/HttpClientTests.cs`

**Interfaces:**
- Consumes: `HttpRequestMessage`, `HttpResponseMessage` (Task 1)
- Produces: `abstract class HttpMessageHandler` with `abstract UniTask<HttpResponseMessage> SendAsync(HttpRequestMessage, CancellationToken)`; `class DelegatingHandler : HttpMessageHandler` with `protected HttpMessageHandler InnerHandler` and ctor `(HttpMessageHandler innerHandler)`, `SendAsync`가 기본으로 InnerHandler에 위임; `class HttpClient` with ctor `(HttpMessageHandler handler)`, `TimeSpan Timeout { get; set; }` (기본 30초), `static readonly TimeSpan InfiniteTimeout`, `UniTask<HttpResponseMessage> SendAsync(HttpRequestMessage, CancellationToken = default)`; 테스트용 `FakeHttpMessageHandler`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`GameFramework/Tests/Runtime/Http/HttpClientTests.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework.Http;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GameFramework.Tests.Http
{
    public class HttpClientTests
    {
        //  바깥 핸들러가 안쪽을 감싸는지 순서를 기록으로 확인한다.
        private class RecordingHandler : DelegatingHandler
        {
            private readonly List<string> log;
            private readonly string name;

            public RecordingHandler(HttpMessageHandler inner, List<string> log, string name) : base(inner)
            {
                this.log = log;
                this.name = name;
            }

            public override async UniTask<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                log.Add($"{name}:before");
                HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
                log.Add($"{name}:after");
                return response;
            }
        }

        //  슬라이스 1의 401 재시도가 성립하려면 핸들러가 응답을 보고 다시 보낼 수 있어야 한다.
        private class RetryOnceHandler : DelegatingHandler
        {
            public RetryOnceHandler(HttpMessageHandler inner) : base(inner) { }

            public override async UniTask<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

                if (response.StatusCode == 401)
                {
                    response = await base.SendAsync(request, cancellationToken);
                }

                return response;
            }
        }

        [UnityTest]
        public IEnumerator 핸들러는_바깥부터_들어가_안쪽부터_나온다() => UniTask.ToCoroutine(async () =>
        {
            var log = new List<string>();
            var fake = FakeHttpMessageHandler.Returning(200, "{}");
            var client = new HttpClient(new RecordingHandler(new RecordingHandler(fake, log, "inner"), log, "outer"));

            await client.SendAsync(HttpRequestMessage.Get("http://example.com"));

            Assert.That(log, Is.EqualTo(new[] { "outer:before", "inner:before", "inner:after", "outer:after" }));
        });

        [UnityTest]
        public IEnumerator 핸들러는_응답을_보고_다시_보낼_수_있다() => UniTask.ToCoroutine(async () =>
        {
            int calls = 0;
            var fake = new FakeHttpMessageHandler((_, __) =>
            {
                calls++;
                return UniTask.FromResult(new HttpResponseMessage(calls == 1 ? 401 : 200, "{}"));
            });
            var client = new HttpClient(new RetryOnceHandler(fake));

            HttpResponseMessage response = await client.SendAsync(HttpRequestMessage.Get("http://example.com"));

            Assert.That(calls, Is.EqualTo(2));
            Assert.That(response.StatusCode, Is.EqualTo(200));
        });

        [UnityTest]
        public IEnumerator 이미_취소된_토큰이면_전송하지_않는다() => UniTask.ToCoroutine(async () =>
        {
            var fake = FakeHttpMessageHandler.Returning(200, "{}");
            var client = new HttpClient(fake);
            var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            try
            {
                await client.SendAsync(HttpRequestMessage.Get("http://example.com"), cancelled.Token);
                Assert.Fail("OperationCanceledException이 나와야 한다.");
            }
            catch (OperationCanceledException)
            {
            }

            Assert.That(fake.Requests, Is.Empty);
        });

        [UnityTest]
        public IEnumerator 타임아웃이_지나면_취소된다() => UniTask.ToCoroutine(async () =>
        {
            //  핸들러가 스스로는 끝나지 않고, 넘겨받은 토큰이 취소될 때만 끝난다. 그 토큰은
            //  HttpClient가 타임아웃과 묶어서 만든 것이라, 취소가 오면 곧 타임아웃이 동작한 것이다.
            //  (핸들러가 토큰을 무시하면 아무도 못 끊는다 — .NET도 같은 계약이다.)
            var fake = new FakeHttpMessageHandler((_, cancellationToken) => UniTask.Never<HttpResponseMessage>(cancellationToken));
            var client = new HttpClient(fake) { Timeout = TimeSpan.FromMilliseconds(50) };

            try
            {
                await client.SendAsync(HttpRequestMessage.Get("http://example.com"));
                Assert.Fail("OperationCanceledException이 나와야 한다.");
            }
            catch (OperationCanceledException)
            {
            }
        });
    }
}
```

> **타임아웃 테스트가 EditMode에서 불안정하면:** `FakeHttpMessageHandler`가 받은 토큰(`LastCancellationToken`)이 호출자 토큰과 **다른 인스턴스**이고 `CanBeCanceled == true`임을 확인하는 형태로 바꾼다(= `HttpClient`가 linked CTS를 만들었다는 계약을 검증). 바꿨다면 그 사실과 이유를 보고에 남긴다.

- [ ] **Step 2: `FakeHttpMessageHandler` 를 만든다**

`GameFramework/Tests/Runtime/Http/FakeHttpMessageHandler.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework.Http;

namespace GameFramework.Tests.Http
{
    /// <summary>체인의 끝을 대신하는 가짜 전송. 네트워크 없이 응답을 정해줄 수 있다.</summary>
    public sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, UniTask<HttpResponseMessage>> onSend;

        public List<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();
        public CancellationToken LastCancellationToken { get; private set; }

        //  토큰까지 받는 이유: 타임아웃 검증이 "HttpClient가 넘겨준 토큰이 실제로 취소되는가"라서,
        //  가짜 핸들러도 그 토큰을 존중해야 의미가 있다.
        public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, UniTask<HttpResponseMessage>> onSend)
        {
            this.onSend = onSend;
        }

        public static FakeHttpMessageHandler Returning(long statusCode, string body)
        {
            return new FakeHttpMessageHandler((_, __) => UniTask.FromResult(new HttpResponseMessage(statusCode, body)));
        }

        public static FakeHttpMessageHandler Throwing(Exception exception)
        {
            return new FakeHttpMessageHandler((_, __) => UniTask.FromException<HttpResponseMessage>(exception));
        }

        //  여기서 취소를 검사하지 않는다 — 검사하면 "HttpClient가 취소를 막았다"와 "가짜가 막았다"를
        //  구분할 수 없어져, 취소 테스트가 HttpClient를 실제로 검증하지 못한다.
        public override UniTask<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastCancellationToken = cancellationToken;
            Requests.Add(request);

            return onSend(request, cancellationToken);
        }
    }
}
```

- [ ] **Step 3: 테스트를 돌려 실패를 확인한다**

Task 1 Step 3의 명령. 기대: `HttpMessageHandler`/`DelegatingHandler`/`HttpClient` 부재로 컴파일 실패.

- [ ] **Step 4: `HttpMessageHandler.cs` 를 만든다**

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameFramework.Http
{
    public abstract class HttpMessageHandler
    {
        public abstract UniTask<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken);
    }
}
```

- [ ] **Step 5: `DelegatingHandler.cs` 를 만든다**

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameFramework.Http
{
    /// <summary>다음 핸들러를 감싸는 베이스. 상속해서 SendAsync를 재정의하고, 안쪽으로
    /// 넘길 때 base.SendAsync를 부른다.</summary>
    public class DelegatingHandler : HttpMessageHandler
    {
        protected HttpMessageHandler InnerHandler { get; }

        public DelegatingHandler(HttpMessageHandler innerHandler)
        {
            InnerHandler = innerHandler ?? throw new ArgumentNullException(nameof(innerHandler));
        }

        public override UniTask<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return InnerHandler.SendAsync(request, cancellationToken);
        }
    }
}
```

- [ ] **Step 6: `HttpClient.cs` 를 만든다**

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameFramework.Http
{
    /// <summary>핸들러 체인의 진입점. 4xx·5xx여도 예외를 던지지 않고 응답을 그대로 돌려준다 —
    /// 상태코드를 보고 재시도할지 정하는 것은 체인 안의 핸들러 몫이고, 예외로 바꾸는 것은
    /// 그 위 타입드 계층(SendAsync&lt;T&gt;) 몫이다.</summary>
    public class HttpClient
    {
        public static readonly TimeSpan InfiniteTimeout = System.Threading.Timeout.InfiniteTimeSpan;

        private readonly HttpMessageHandler handler;

        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        public HttpClient(HttpMessageHandler handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public async UniTask<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            //  호출자 토큰과 타임아웃을 하나로 묶어 넘긴다 — 둘 중 어느 쪽이 먼저 와도 끊긴다.
            using (var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                if (Timeout != InfiniteTimeout)
                {
                    timeoutSource.CancelAfter(Timeout);
                }

                return await handler.SendAsync(request, timeoutSource.Token);
            }
        }
    }
}
```

- [ ] **Step 7: 테스트를 돌려 통과를 확인한다**

기대: Task 1의 9건 + 이번 4건 = 13건 통과.

- [ ] **Step 8: 되돌려 실제로 실패하는지 확인한다 (2건)**

**(a)** `HttpClient.SendAsync` 첫 줄 `cancellationToken.ThrowIfCancellationRequested();` 를 지우고 돌린다 → `이미_취소된_토큰이면_전송하지_않는다` 가 **즉시 실패**해야 한다(가짜 핸들러가 호출되어 `Requests` 가 비어 있지 않다). 확인 후 되돌린다.

**(b)** `timeoutSource.CancelAfter(Timeout);` 줄을 지우고 돌린다 → `타임아웃이_지나면_취소된다` 가 실패해야 한다. 이 건은 **테스트 프레임워크의 타임아웃으로 실패**하므로 즉시 끝나지 않고 수 분이 걸린다. 확인 후 되돌린다.

- [ ] **Step 9: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/GameFramework
git add Runtime/Scripts/Http Tests/Runtime/Http
git commit -m "feat(http): 핸들러 체인 + HttpClient

체인은 4xx·5xx여도 응답을 그대로 돌려준다 — 핸들러가 401을 보고
재시도할 수 있어야 하기 때문. 예외는 그 위 계층에서 던진다."
```

---

## Task 3: 타입드 전송 — 역직렬화와 예외

**Files:**
- Create: `GameFramework/Runtime/Scripts/Http/HttpClientJsonExtensions.cs`
- Test: `GameFramework/Tests/Runtime/Http/HttpClientJsonExtensionsTests.cs`

**Interfaces:**
- Consumes: `HttpClient`, `HttpResponseMessage`, `HttpRequestException`, `HttpJson`, `FakeHttpMessageHandler`
- Produces: `HttpClientJsonExtensions.SendAsync<T>(this HttpClient, HttpRequestMessage, CancellationToken = default) → UniTask<T>` 와 커스텀 역직렬화 오버로드 `SendAsync<T>(this HttpClient, HttpRequestMessage, Func<string, T> deserialize, CancellationToken = default) → UniTask<T>`

> 커스텀 오버로드가 필요한 이유: 클라의 `GetUserLocationResponse.Deserialize(string)`가 `JObject`로 `locationDetail`을 손수 파싱한다. 기본 역직렬화로는 대체할 수 없다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`GameFramework/Tests/Runtime/Http/HttpClientJsonExtensionsTests.cs`:

```csharp
using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using GameFramework.Http;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GameFramework.Tests.Http
{
    public class HttpClientJsonExtensionsTests
    {
        private class Payload
        {
            public int code;
            public string name;
        }

        [UnityTest]
        public IEnumerator 성공이면_본문을_역직렬화해_돌려준다() => UniTask.ToCoroutine(async () =>
        {
            var client = new HttpClient(FakeHttpMessageHandler.Returning(200, "{\"code\":0,\"name\":\"kim\"}"));

            Payload payload = await client.SendAsync<Payload>(HttpRequestMessage.Get("http://example.com"));

            Assert.That(payload.code, Is.EqualTo(0));
            Assert.That(payload.name, Is.EqualTo("kim"));
        });

        [UnityTest]
        public IEnumerator 커스텀_역직렬화를_주면_그것을_쓴다() => UniTask.ToCoroutine(async () =>
        {
            var client = new HttpClient(FakeHttpMessageHandler.Returning(200, "무엇이든"));

            Payload payload = await client.SendAsync(
                HttpRequestMessage.Get("http://example.com"),
                _ => new Payload { code = 42, name = "custom" });

            Assert.That(payload.code, Is.EqualTo(42));
            Assert.That(payload.name, Is.EqualTo("custom"));
        });

        [UnityTest]
        public IEnumerator _401이면_상태코드를_담은_예외를_던진다() => UniTask.ToCoroutine(async () =>
        {
            var client = new HttpClient(FakeHttpMessageHandler.Returning(401, "{\"message\":\"denied\"}"));

            try
            {
                await client.SendAsync<Payload>(HttpRequestMessage.Get("http://example.com"));
                Assert.Fail("HttpRequestException이 나와야 한다.");
            }
            catch (HttpRequestException exception)
            {
                Assert.That(exception.StatusCode, Is.EqualTo(401));
                Assert.That(exception.ResponseBody, Is.EqualTo("{\"message\":\"denied\"}"));
            }
        });

        [UnityTest]
        public IEnumerator _500도_같은_모양으로_던진다() => UniTask.ToCoroutine(async () =>
        {
            var client = new HttpClient(FakeHttpMessageHandler.Returning(500, "boom"));

            try
            {
                await client.SendAsync<Payload>(HttpRequestMessage.Get("http://example.com"));
                Assert.Fail("HttpRequestException이 나와야 한다.");
            }
            catch (HttpRequestException exception)
            {
                Assert.That(exception.StatusCode, Is.EqualTo(500));
            }
        });

        [UnityTest]
        public IEnumerator 전송이_실패하면_상태코드가_null이다() => UniTask.ToCoroutine(async () =>
        {
            //  이 구분이 이 계층을 새로 짓는 이유다. 예전엔 오프라인과 401이 똑같이 보여서,
            //  오프라인으로 게임을 켠 플레이어의 계정을 "서버가 거부했다"로 오판해 지웠다.
            var client = new HttpClient(FakeHttpMessageHandler.Throwing(new HttpRequestException("연결 실패")));

            try
            {
                await client.SendAsync<Payload>(HttpRequestMessage.Get("http://example.com"));
                Assert.Fail("HttpRequestException이 나와야 한다.");
            }
            catch (HttpRequestException exception)
            {
                Assert.That(exception.StatusCode, Is.Null);
            }
        });
    }
}
```

- [ ] **Step 2: 테스트를 돌려 실패를 확인한다**

기대: `SendAsync<T>` 확장 부재로 컴파일 실패.

- [ ] **Step 3: `HttpClientJsonExtensions.cs` 를 만든다**

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameFramework.Http
{
    /// <summary>응답을 T로 바꿔 주는 계층. 여기서만 상태코드를 예외로 바꾼다 —
    /// 핸들러 체인은 4xx·5xx도 응답으로 넘겨야 재시도 판단이 가능하다.</summary>
    public static class HttpClientJsonExtensions
    {
        public static UniTask<T> SendAsync<T>(this HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            return client.SendAsync(request, HttpJson.DeserializeObject<T>, cancellationToken);
        }

        public static async UniTask<T> SendAsync<T>(this HttpClient client, HttpRequestMessage request, Func<string, T> deserialize, CancellationToken cancellationToken = default)
        {
            HttpResponseMessage response = await client.SendAsync(request, cancellationToken);

            response.EnsureSuccessStatusCode();

            return deserialize(response.Body);
        }
    }
}
```

- [ ] **Step 4: 테스트를 돌려 통과를 확인한다**

기대: 누적 18건 통과.

- [ ] **Step 5: 되돌려 실제로 실패하는지 확인한다**

`response.EnsureSuccessStatusCode();` 줄을 지우고 돌린다 → `_401이면...`·`_500도...` 두 건이 실패해야 한다. 확인 후 되돌린다.

- [ ] **Step 6: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/GameFramework
git add Runtime/Scripts/Http Tests/Runtime/Http
git commit -m "feat(http): 타입드 SendAsync<T> — 역직렬화 + 상태코드 예외

커스텀 역직렬화 오버로드도 함께. 클라 GetUserLocationResponse가
JObject로 locationDetail을 손수 파싱해 기본 경로로는 대체 불가."
```

---

## Task 4: BearerTokenHandler

**Files:**
- Create: `GameFramework/Runtime/Scripts/Http/BearerTokenHandler.cs`
- Test: `GameFramework/Tests/Runtime/Http/BearerTokenHandlerTests.cs`

**Interfaces:**
- Consumes: `DelegatingHandler`, `HttpClient`, `FakeHttpMessageHandler`
- Produces: `class BearerTokenHandler : DelegatingHandler` with ctor `(HttpMessageHandler innerHandler, Func<string> accessTokenProvider)`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`GameFramework/Tests/Runtime/Http/BearerTokenHandlerTests.cs`:

```csharp
using System.Collections;
using Cysharp.Threading.Tasks;
using GameFramework.Http;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GameFramework.Tests.Http
{
    public class BearerTokenHandlerTests
    {
        [UnityTest]
        public IEnumerator 토큰이_있으면_Authorization을_붙인다() => UniTask.ToCoroutine(async () =>
        {
            var fake = FakeHttpMessageHandler.Returning(200, "{}");
            var client = new HttpClient(new BearerTokenHandler(fake, () => "abc.def.ghi"));

            await client.SendAsync(HttpRequestMessage.Get("http://example.com"));

            Assert.That(fake.Requests[0].Headers["Authorization"], Is.EqualTo("Bearer abc.def.ghi"));
        });

        [UnityTest]
        public IEnumerator 토큰이_없으면_아무것도_붙이지_않는다() => UniTask.ToCoroutine(async () =>
        {
            //  로그인 전에는 토큰이 없다 — 빈 Bearer를 보내면 서버가 잘못된 토큰으로 읽는다.
            var fake = FakeHttpMessageHandler.Returning(200, "{}");
            var client = new HttpClient(new BearerTokenHandler(fake, () => null));

            await client.SendAsync(HttpRequestMessage.Get("http://example.com"));

            Assert.That(fake.Requests[0].Headers.ContainsKey("Authorization"), Is.False);
        });
    }
}
```

- [ ] **Step 2: 테스트를 돌려 실패를 확인한다**

- [ ] **Step 3: `BearerTokenHandler.cs` 를 만든다**

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameFramework.Http
{
    /// <summary>공급자가 토큰을 주면 Authorization 헤더를 붙인다. 공급자를 매번 호출하는 이유는
    /// 토큰이 갱신으로 바뀌기 때문 — 만들 때의 값을 스냅샷으로 들고 있으면 안 된다.</summary>
    public class BearerTokenHandler : DelegatingHandler
    {
        private readonly Func<string> accessTokenProvider;

        public BearerTokenHandler(HttpMessageHandler innerHandler, Func<string> accessTokenProvider) : base(innerHandler)
        {
            this.accessTokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));
        }

        public override UniTask<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string accessToken = accessTokenProvider.Invoke();

            if (string.IsNullOrEmpty(accessToken) == false)
            {
                request.Headers["Authorization"] = $"Bearer {accessToken}";
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
```

- [ ] **Step 4: 테스트를 돌려 통과를 확인한다** — 누적 20건

- [ ] **Step 5: 되돌려 실제로 실패하는지 확인한다**

`string.IsNullOrEmpty(accessToken) == false` 조건을 지워 항상 붙이게 하고 돌린다 → `토큰이_없으면...`이 실패해야 한다. 확인 후 되돌린다.

- [ ] **Step 6: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/GameFramework
git add Runtime/Scripts/Http Tests/Runtime/Http
git commit -m "feat(http): BearerTokenHandler

Authorization: Bearer 부착은 RFC 6750이 정한 표준 동작이고 LOP 타입을
하나도 모른다(생성자 인자가 Func<string> 하나) — 앱 비종속 인프라라
GameFramework에 둔다. 모든 HTTP 클라이언트 라이브러리가 기본 제공하는
것이기도 하다."
```

---

## Task 5: UnityWebRequestHandler — 실제 전송

**Files:**
- Create: `GameFramework/Runtime/Scripts/Http/UnityWebRequestHandler.cs`

**Interfaces:**
- Consumes: `HttpMessageHandler`, `HttpRequestMessage`, `HttpResponseMessage`, `HttpRequestException`, `HttpExtensions.SetRequestHeader(this UnityWebRequest, Dictionary<string,string>)`
- Produces: `class UnityWebRequestHandler : HttpMessageHandler` — 기본 생성자

> **단위 테스트를 만들지 않는다.** 실제 소켓을 쓰는 얇은 어댑터라 EditMode에서 의미 있게 격리할 수 없다. .NET에서도 실제 전송 핸들러는 단위 테스트 대상이 아니다. 검증은 Task 6·7의 수동 시나리오가 맡는다.

- [ ] **Step 1: `UnityWebRequestHandler.cs` 를 만든다**

```csharp
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace GameFramework.Http
{
    /// <summary>체인의 끝. 실제로 네트워크에 나가는 유일한 곳이다.</summary>
    public class UnityWebRequestHandler : HttpMessageHandler
    {
        public override async UniTask<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using (UnityWebRequest unityWebRequest = Create(request))
            {
                try
                {
                    await unityWebRequest.SendWebRequest().WithCancellation(cancellationToken);
                }
                catch (UnityWebRequestException)
                {
                    //  UniTask는 4xx·5xx에도 예외를 던진다. 우리 계약은 "상태코드는 응답으로
                    //  돌려준다"이므로(핸들러가 401을 보고 재시도할 수 있어야 한다) 여기서 삼키고
                    //  아래에서 result로 다시 판정한다. 취소는 OperationCanceledException이라
                    //  이 catch에 안 걸리고 그대로 올라간다.
                }

                if (unityWebRequest.result == UnityWebRequest.Result.ConnectionError ||
                    unityWebRequest.result == UnityWebRequest.Result.DataProcessingError)
                {
                    //  서버에 닿지 못했거나 응답을 읽지 못했다 — 상태코드가 없다는 것이 그 신호다.
                    throw new HttpRequestException(
                        $"요청 전송에 실패했습니다. uri: {request.Uri}, error: {unityWebRequest.error}");
                }

                return new HttpResponseMessage(
                    unityWebRequest.responseCode,
                    unityWebRequest.downloadHandler?.text ?? string.Empty,
                    unityWebRequest.GetResponseHeaders());
            }
        }

        private static UnityWebRequest Create(HttpRequestMessage request)
        {
            var unityWebRequest = new UnityWebRequest(request.Uri, request.Method.ToString());
            unityWebRequest.downloadHandler = new DownloadHandlerBuffer();

            if (string.IsNullOrEmpty(request.Content) == false)
            {
                unityWebRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.Content));
                unityWebRequest.SetRequestHeader("Content-Type", request.ContentType);
            }

            //  GameFramework.HttpExtensions 의 확장 — 바깥 네임스페이스라 using 없이 잡힌다.
            unityWebRequest.SetRequestHeader(request.Headers);

            return unityWebRequest;
        }
    }
}
```

- [ ] **Step 2: 컴파일을 확인한다**

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -quit -batchmode -projectPath /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client -logFile /tmp/compile.log
grep -c "error CS" /tmp/compile.log
```

기대: `0`. 기존 20건 테스트도 여전히 통과해야 한다.

- [ ] **Step 3: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/GameFramework
git add Runtime/Scripts/Http
git commit -m "feat(http): UnityWebRequestHandler — 체인의 끝

UniTask가 4xx·5xx에 던지는 예외는 여기서 삼키고 result로 다시 판정한다.
상태코드는 예외가 아니라 응답으로 올라가야 한다."
```

---

## Task 6: 클라이언트 이전

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/WebAPI/WebAPI.cs` (전면 교체)
- Delete: `LeagueOfPhysical-Client/Assets/Scripts/WebAPI/LOPWebRequestInterceptor.cs` (+ `.meta`)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/RootLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Auth/AuthenticationService.cs`
- Modify: 호출부 9파일 (Step 4 목록)

**Interfaces:**
- Consumes: `GameFramework.Http.HttpClient`, `BearerTokenHandler`, `UnityWebRequestHandler`, `HttpRequestMessage`, `HttpClientJsonExtensions.SendAsync<T>`, `HttpRequestException`
- Produces: `WebAPI.SetAccessTokenProvider(Func<string>)`; 10개 메서드가 전부 `UniTask<TResponse>` 를 반환

- [ ] **Step 1: `WebAPI.cs` 를 전면 교체한다**

> **호출부가 없는 메서드 4개(`LeaveLobby`·`GetUserByUsername`·`CreateUser`·`GetRoom`)는 옮기지 않고 삭제한다** — 사용자 결정. 파일을 통째로 다시 쓰는 지금이 지울 적기다. `UserDataStore`가 `CreateUserResponse`를 구독하는 코드는 그대로 두어도 컴파일에 문제 없다(발행자만 사라진다). 아래 코드가 최종 형태이며, 여기 없는 메서드는 만들지 않는다.

```csharp
using Cysharp.Threading.Tasks;
using GameFramework.Http;
using MessagePipe;
using System;
using System.Threading;

namespace LOP
{
    public class WebAPI
    {
        //  static이라 DI가 안 된다 — RootLifetimeScope가 기동 시 공급자를 꽂아 준다.
        private static Func<string> accessTokenProvider;

        //  인증을 붙이는 클라이언트와 절대 안 붙이는 클라이언트를 따로 둔다. 어느 쪽을 쓸지는
        //  호출부가 스스로 고른다 — URL 문자열로 판단하면 경로 접두사가 바뀔 때 조용히 깨진다
        //  (실제로 /lobby 접두사 때문에 죽은 검사가 된 적이 있다).
        private static readonly HttpClient authorized =
            new HttpClient(new BearerTokenHandler(new UnityWebRequestHandler(), () => accessTokenProvider?.Invoke()));

        private static readonly HttpClient anonymous = new HttpClient(new UnityWebRequestHandler());

        public static void SetAccessTokenProvider(Func<string> provider)
        {
            accessTokenProvider = provider;
        }

        //  응답을 역직렬화한 뒤 전역 발행까지 한다 — UserDataStore/RoomDataStore가 이걸 구독해
        //  자기 상태를 채운다. 이 발행이 끊기면 유저 데이터가 아예 안 들어온다.
        private static async UniTask<T> SendAsync<T>(HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            T response = await client.SendAsync<T>(request, cancellationToken);
            GlobalMessagePipe.GetPublisher<T>().Publish(response);
            return response;
        }

        private static async UniTask<T> SendAsync<T>(HttpClient client, HttpRequestMessage request, Func<string, T> deserialize, CancellationToken cancellationToken)
        {
            T response = await client.SendAsync(request, deserialize, cancellationToken);
            GlobalMessagePipe.GetPublisher<T>().Publish(response);
            return response;
        }

        #region Auth
        //  이 두 호출은 반드시 anonymous를 쓴다 — 로그인/가입 자체에 Bearer를 실으면, 갱신이 밀린
        //  상태에서 만료 임박/구 토큰이 얹혀 나가 서버가 401을 줄 수 있다. 그 401을
        //  AuthenticationService가 "자격증명이 거부됐다"로 오판하면 멀쩡한 계정을 지우고 새로
        //  가입해버린다(계정 유실).
        public static UniTask<AnonymousSignInResponse> SignInAnonymous(CancellationToken cancellationToken = default)
            => SendAsync<AnonymousSignInResponse>(anonymous,
                HttpRequestMessage.Post($"{EnvironmentSettings.active.lobbyBaseURL}/auth/anonymous"), cancellationToken);

        public static UniTask<LoginResponse> Login(LoginRequest request, CancellationToken cancellationToken = default)
            => SendAsync<LoginResponse>(anonymous,
                HttpRequestMessage.Post($"{EnvironmentSettings.active.lobbyBaseURL}/auth/login", request), cancellationToken);
        #endregion

        #region Lobby
        public static UniTask<JoinLobbyResponse> JoinLobby(string userId, CancellationToken cancellationToken = default)
            => SendAsync<JoinLobbyResponse>(authorized,
                HttpRequestMessage.Put($"{EnvironmentSettings.active.lobbyBaseURL}/lobby/join/{userId}"), cancellationToken);

        #endregion

        #region MatchmakingTicket
        public static UniTask<MatchmakingResponse> RequestMatchmaking(MatchmakingRequest request, CancellationToken cancellationToken = default)
            => SendAsync<MatchmakingResponse>(authorized,
                HttpRequestMessage.Post($"{EnvironmentSettings.active.matchmakingBaseURL}/matchmaking", request), cancellationToken);

        public static UniTask<CancelMatchmakingResponse> CancelMatchmaking(string ticketId, CancellationToken cancellationToken = default)
            => SendAsync<CancelMatchmakingResponse>(authorized,
                HttpRequestMessage.Delete($"{EnvironmentSettings.active.matchmakingBaseURL}/matchmaking/{ticketId}"), cancellationToken);

        public static UniTask<GetMatchResponse> GetMatch(string matchId, CancellationToken cancellationToken = default)
            => SendAsync<GetMatchResponse>(authorized,
                HttpRequestMessage.Get($"{EnvironmentSettings.active.matchmakingBaseURL}/match/{matchId}"), cancellationToken);
        #endregion

        #region User
        public static UniTask<GetUserResponse> GetUser(string userId, CancellationToken cancellationToken = default)
            => SendAsync<GetUserResponse>(authorized,
                HttpRequestMessage.Get($"{EnvironmentSettings.active.lobbyBaseURL}/user/{userId}"), cancellationToken);



        public static UniTask<GetUserLocationResponse> GetUserLocation(string userId, CancellationToken cancellationToken = default)
            => SendAsync(authorized,
                HttpRequestMessage.Get($"{EnvironmentSettings.active.lobbyBaseURL}/user/{userId}/location/"),
                GetUserLocationResponse.Deserialize, cancellationToken);

        public static UniTask<GetUserStatsResponse> GetUserStats(string userId, int queueId, CancellationToken cancellationToken = default)
            => SendAsync<GetUserStatsResponse>(authorized,
                HttpRequestMessage.Get($"{EnvironmentSettings.active.lobbyBaseURL}/user/{userId}/stats?queueId={queueId}"), cancellationToken);
        #endregion

        #region Room

        public static UniTask<RoomJoinableResponse> CheckRoomJoinable(string roomId, CancellationToken cancellationToken = default)
            => SendAsync<RoomJoinableResponse>(authorized,
                HttpRequestMessage.Get($"{EnvironmentSettings.active.roomBaseURL}/room/{roomId}/joinable"), cancellationToken);
        #endregion
    }
}
```

- [ ] **Step 2: 인터셉터를 삭제하고 `RootLifetimeScope` 배선을 바꾼다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git rm Assets/Scripts/WebAPI/LOPWebRequestInterceptor.cs Assets/Scripts/WebAPI/LOPWebRequestInterceptor.cs.meta
```

`Assets/Scripts/RootLifetimeScope.cs` 에서 이 줄을

```csharp
                LOPWebRequestInterceptor.SetAccessTokenProvider(() => authenticationService.AccessToken);
```

이렇게 바꾼다.

```csharp
                WebAPI.SetAccessTokenProvider(() => authenticationService.AccessToken);
```

같은 블록의 주석도 함께 고친다 — `정적/비-DI 코드(웹 인터셉터)가` → `정적/비-DI 코드(WebAPI)가`, `모든 REST 요청이 현재 세션 토큰을 싣도록 인터셉터에 공급자를 꽂는다.` → `모든 REST 요청이 현재 세션 토큰을 싣도록 WebAPI에 공급자를 꽂는다.`

- [ ] **Step 3: `AuthenticationService` 의 awaiter 흡수 코드를 걷어낸다**

`TryLoginAsync` 를 통째로 이렇게 바꾼다. `HttpStatusUnauthorized` 상수는 그대로 둔다.

```csharp
        private async UniTask<AuthSession> TryLoginAsync(AuthCredential credential)
        {
            try
            {
                LoginResponse response = await WebAPI.Login(new LoginRequest
                {
                    provider = credential.Provider,
                    providerUserId = credential.ProviderUserId,
                    secret = credential.Secret,
                });

                return new AuthSession(
                    response.userId,
                    AccessTokenInfo.FromExpiresIn(response.accessToken, response.expiresIn, DateTimeOffset.UtcNow));
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusUnauthorized)
            {
                //  401만 "이 자격증명은 더 이상 못 쓴다"는 확답이다 — 호출자가 거부로 취급해
                //  계정을 새로 만들 수 있도록 null을 돌려준다.
                return null;
            }
        }
```

`RegisterAnonymousAsync` 는 이렇게 바꾼다.

```csharp
        private async UniTask<AuthSession> RegisterAnonymousAsync()
        {
            AnonymousSignInResponse response = await WebAPI.SignInAnonymous();

            credentialStore.Save(new AuthCredential
            {
                Provider = response.credential.provider,
                ProviderUserId = response.credential.providerUserId,
                Secret = response.credential.secret,
            });

            return new AuthSession(
                response.userId,
                AccessTokenInfo.FromExpiresIn(response.accessToken, response.expiresIn, DateTimeOffset.UtcNow));
        }
```

파일 맨 위 `using` 에 `using GameFramework.Http;` 를 추가한다.

> **없어지는 것:** 어느 `GetAwaiter`가 바인딩되는지 몰라 예외를 흡수하던 `try/catch (GameFramework.WebRequestException)` 두 곳과 그 장문의 주석. **401 외의 실패(오프라인·타임아웃·5xx)는 이제 `HttpRequestException`이 그대로 올라가** 호출자에게 "지금은 확인 못 함"을 전한다 — 예전에 직접 `throw new Exception(...)` 하던 것과 같은 결과이며, 계정을 지우지 않는다는 성질이 유지된다.

- [ ] **Step 4: 호출부 9파일에서 `.response` 한 겹을 걷어낸다**

| 파일 | 부르는 것 |
|---|---|
| `Assets/Scripts/Entrance/EntranceComponent/JoinLobbyComponent.cs` | `JoinLobby` |
| `Assets/Scripts/Entrance/EntranceComponent/LoadUserComponent.cs` | `GetUser`, `GetUserLocation`, `GetUserStats` ×2 |
| `Assets/Scripts/Matchmaking/MatchStateMachine/States/RequestMatchmaking.cs` | `RequestMatchmaking` |
| `Assets/Scripts/Matchmaking/MatchStateMachine/States/CancelMatchmaking.cs` | `CancelMatchmaking` |
| `Assets/Scripts/Matchmaking/MatchStateMachine/States/CheckMatch.cs` | `GetUserLocation` |
| `Assets/Scripts/Matchmaking/MatchStateMachine/States/InMatchmaking.cs` | `GetUserLocation` |
| `Assets/Scripts/Room/LOPRoom.cs` | `GetMatch` |
| `Assets/Scripts/Room/RoomConnector.cs` | `CheckRoomJoinable` |
| `Assets/Scripts/Auth/AuthenticationService.cs` | Step 3에서 처리됨 |

변환 규칙은 하나다 — **`x.response.Y` → `x.Y`**. 예:

```csharp
// 전
var joinLobby = await WebAPI.JoinLobby(userDataStore.user.id);
if (joinLobby.response.code != ResponseCode.SUCCESS)

// 후
var joinLobby = await WebAPI.JoinLobby(userDataStore.user.id);
if (joinLobby.code != ResponseCode.SUCCESS)
```

`catch (WebRequestException e)` 로 잡던 곳은 `catch (GameFramework.Http.HttpRequestException e)` 로 바꾸거나, 파일에 `using GameFramework.Http;` 를 추가하고 `catch (HttpRequestException e)` 로 쓴다.

찾는 방법:

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
grep -rn "\.response\b" Assets/Scripts | grep -v "WebAPI/WebAPI.cs"
grep -rn "WebRequestException" Assets/Scripts
```

- [ ] **Step 5: 컴파일 에러 0을 확인한다**

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -quit -batchmode -projectPath /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client -logFile /tmp/client-compile.log
grep -c "error CS" /tmp/client-compile.log
grep "error CS" /tmp/client-compile.log | head -20
```

기대: `0`.

- [ ] **Step 6: 잔여 참조가 없는지 확인한다**

```bash
grep -rn "LOPWebRequestInterceptor\|WebRequestBuilder\|WebRequest<" Assets/Scripts
```

기대: 출력 없음.

- [ ] **Step 7: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add -A Assets/Scripts
git commit -m "refactor(webapi): 클라를 HttpClient 계층으로 이전

WebAPI가 요청 객체가 아니라 DTO를 바로 돌려준다. 인증을 붙이는
클라이언트와 안 붙이는 클라이언트를 따로 둔다.

AuthenticationService에서 awaiter 흡수 코드가 사라졌다 — 어느
GetAwaiter가 잡히느냐에 따라 401 처리가 뒤집히던 문제가 구조적으로
불가능해졌다."
```

- [ ] **Step 8: 사용자에게 수동 검증을 요청한다** (컨트롤러가 수행)

에디터를 열고 아래를 확인해 달라고 요청한다.

| # | 시나리오 | 기대 |
|---|---|---|
| 1 | 진입 → 로그인 → 유저 로드 → 로비 입장 | 평소대로 로비까지 들어간다 |
| 2 | 매칭 요청 → 매치 성사 → 방 입장 | 게임까지 들어간다 |
| 3 | **백엔드를 끈 채로 클라 재기동** | 로그인 실패 로그만 뜨고 **계정이 지워지지 않는다**. 백엔드를 켜고 다시 켜면 같은 계정으로 들어간다 |

3번이 이번 정리의 핵심 회귀 테스트다. 계정이 새로 생기면 실패다.

---

## Task 7: 서버 이전

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/WebAPI/WebAPI.cs` (전면 교체)
- Delete: `LeagueOfPhysical-Server/Assets/Scripts/WebAPI/LOPWebRequestInterceptor.cs` (+ `.meta`)
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Room/LOPRoom.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`

**Interfaces:**
- Consumes: `GameFramework.Http.HttpClient`, `UnityWebRequestHandler`, `HttpRequestMessage`, `HttpClientJsonExtensions.SendAsync<T>`
- Produces: 서버 `WebAPI` 4개 메서드가 `UniTask<TResponse>` 반환

> 서버는 토큰을 붙이지 않으므로 클라이언트가 하나뿐이고 `BearerTokenHandler`가 없다.

- [ ] **Step 1: `WebAPI.cs` 를 전면 교체한다**

> **호출부가 없는 `NotifyStopServer`는 옮기지 않고 삭제한다** — 사용자 결정. 아래 코드가 최종 형태이며, 여기 없는 메서드는 만들지 않는다.

```csharp
using Cysharp.Threading.Tasks;
using GameFramework.Http;
using MessagePipe;
using System.Threading;

namespace LOP
{
    public class WebAPI
    {
        private static readonly HttpClient httpClient = new HttpClient(new UnityWebRequestHandler());

        //  응답을 역직렬화한 뒤 전역 발행까지 한다 — 데이터 스토어가 이걸 구독해 상태를 채운다.
        private static async UniTask<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            T response = await httpClient.SendAsync<T>(request, cancellationToken);
            GlobalMessagePipe.GetPublisher<T>().Publish(response);
            return response;
        }

        #region Room
        public static UniTask<HttpResponse> Heartbeat(string roomId, CancellationToken cancellationToken = default)
            => SendAsync<HttpResponse>(
                HttpRequestMessage.Put($"{EnvironmentSettings.active.roomBaseURL}/room/heartbeat/{roomId}"), cancellationToken);


        public static UniTask<UpdateRoomStatusResponse> UpdateRoomStatus(UpdateRoomStatusRequest request, CancellationToken cancellationToken = default)
            => SendAsync<UpdateRoomStatusResponse>(
                HttpRequestMessage.Put($"{EnvironmentSettings.active.roomBaseURL}/room/status", request), cancellationToken);

        public static UniTask<GetRoomResponse> GetRoom(string roomId, CancellationToken cancellationToken = default)
            => SendAsync<GetRoomResponse>(
                HttpRequestMessage.Get($"{EnvironmentSettings.active.roomBaseURL}/room/{roomId}"), cancellationToken);
        #endregion

        #region Match
        public static UniTask<GetMatchResponse> GetMatch(string matchId, CancellationToken cancellationToken = default)
            => SendAsync<GetMatchResponse>(
                HttpRequestMessage.Get($"{EnvironmentSettings.active.matchmakingBaseURL}/match/{matchId}"), cancellationToken);
        #endregion
    }
}
```

> `HttpResponse` 는 여기서 `LOP.HttpResponse`(응답 DTO 베이스)를 가리킨다. `GameFramework.Http` 에는 그 이름이 없으므로(`HttpResponseMessage`) 모호하지 않다.

- [ ] **Step 2: 인터셉터를 삭제한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git rm Assets/Scripts/WebAPI/LOPWebRequestInterceptor.cs Assets/Scripts/WebAPI/LOPWebRequestInterceptor.cs.meta
```

- [ ] **Step 3: `LOPRoom.cs` 의 호출 5곳을 고친다**

`await` 하는 3곳(약 45행·71행·127행)은 그대로 `await` 를 유지한다 — 반환 타입만 바뀌고 결과를 안 쓰므로 코드 변경이 없다.

**`await` 없이 부르는 2곳은 반드시 `.Forget()` 을 붙인다.** 지금은 생성자가 전송하니 동작하지만, 새 구조에서는 **아무 에러 없이 그냥 안 나간다.**

약 141행 (`SendHeartbeat`):

```csharp
        private void SendHeartbeat()
        {
            if (!EnvironmentSettings.active.Standalone)
            {
                //  결과를 기다리지 않는다 — 하트비트는 실패해도 다음 주기가 이어서 보낸다.
                WebAPI.Heartbeat(roomDataStore.room.id).Forget();
            }
        }
```

약 160행 (`OnGameStateChanged` 의 `RunnerState.GameOver`):

```csharp
                    if (!EnvironmentSettings.active.Standalone)
                    {
                        //  이벤트 핸들러라 await 할 수 없다 — 보내기만 하고 넘어간다.
                        WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
                        {
                            roomId = roomDataStore.room.id,
                            status = RoomStatus.Closed,
                        }).Forget();
                    }
```

파일 맨 위 `using` 에 `using Cysharp.Threading.Tasks;` 가 없으면 추가한다.

- [ ] **Step 4: `ConfigureRoomComponent.cs` 의 호출 3곳을 고친다**

약 68~69행:

```csharp
// 전
var getRoom = await WebAPI.GetRoom(roomId);
var getMatch = await WebAPI.GetMatch(getRoom.response.room.matchId);

// 후
var getRoom = await WebAPI.GetRoom(roomId);
var getMatch = await WebAPI.GetMatch(getRoom.room.matchId);
```

이후 `getRoom.response.` / `getMatch.response.` 로 접근하는 곳이 더 있으면 모두 `.response` 를 제거한다. 82행의 `UpdateRoomStatus` 는 결과를 안 쓰므로 변경 없다.

- [ ] **Step 5: 컴파일 에러 0을 확인한다**

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -quit -batchmode -projectPath /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server -logFile /tmp/server-compile.log
grep -c "error CS" /tmp/server-compile.log
grep "error CS" /tmp/server-compile.log | head -20
```

기대: `0`.

- [ ] **Step 6: 잔여 참조가 없는지 확인한다**

```bash
grep -rn "LOPWebRequestInterceptor\|WebRequestBuilder\|WebRequest<\|\.response\b" Assets/Scripts
```

기대: 출력 없음.

- [ ] **Step 7: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git add -A Assets/Scripts
git commit -m "refactor(webapi): 서버를 HttpClient 계층으로 이전

await 없이 부르던 하트비트·상태갱신 2곳에 .Forget()을 붙였다 —
전송이 생성자에서 SendAsync로 옮겨져, 안 붙이면 조용히 안 나간다."
```

- [ ] **Step 8: 사용자에게 수동 검증을 요청한다** (컨트롤러가 수행)

| # | 시나리오 | 기대 |
|---|---|---|
| 1 | 매칭 → 방 생성 → 게임 진입 | 게임서버가 뜨고 클라가 붙는다 |
| 2 | 게임 중 room-server 로그 | 하트비트가 주기적으로 들어온다 (`.Forget()` 확인) |
| 3 | 매치 종료 | 방 상태가 `Closed` 로 갱신된다 |

---

## Task 8: 옛 계층 삭제

**Files:**
- Delete: `GameFramework/Runtime/Scripts/WebRequest/` 아래 9파일 (+ `.meta`)
- Modify: `GameFramework/Runtime/Scripts/WebRequest/HttpExtensions.cs` → `Runtime/Scripts/Http/` 로 이동하고 `GetAwaiter` 확장 2개 삭제

**Interfaces:**
- Consumes: 없음 (삭제 전용)
- Produces: `GameFramework.HttpExtensions` 가 `SetRequestHeader`/`ToQueryString`/`AppendQueryString` 세 개만 남는다

> **이 태스크는 Task 6·7이 둘 다 머지 가능한 상태가 된 뒤에만 한다.** 먼저 하면 양쪽 프로젝트가 동시에 깨진다.

- [ ] **Step 1: 옛 파일들을 지운다**

```bash
cd /Users/insoobae/workspace/LOP/GameFramework/Runtime/Scripts/WebRequest
git rm IWebRequest.cs IWebRequest.cs.meta \
       IWebRequestInterceptor.cs IWebRequestInterceptor.cs.meta \
       IWebRequestParam.cs IWebRequestParam.cs.meta \
       UnityWebRequestAwaiter.cs UnityWebRequestAwaiter.cs.meta \
       WebRequest.cs WebRequest.cs.meta \
       WebRequestAwaiter.cs WebRequestAwaiter.cs.meta \
       WebRequestBuilder.cs WebRequestBuilder.cs.meta \
       WebRequestException.cs WebRequestException.cs.meta \
       WebRequestJson.cs WebRequestJson.cs.meta \
       WebRequestParam.cs WebRequestParam.cs.meta
```

- [ ] **Step 2: `HttpExtensions.cs` 를 옮기고 `GetAwaiter` 를 걷어낸다**

```bash
cd /Users/insoobae/workspace/LOP/GameFramework
git mv Runtime/Scripts/WebRequest/HttpExtensions.cs Runtime/Scripts/Http/HttpExtensions.cs
git mv Runtime/Scripts/WebRequest/HttpExtensions.cs.meta Runtime/Scripts/Http/HttpExtensions.cs.meta
```

파일 끝의 확장 메서드 두 개를 삭제한다.

```csharp
        public static UnityWebRequestAwaiter GetAwaiter<T>(this UnityWebRequestAsyncOperation asyncOperation)
        {
            return new UnityWebRequestAwaiter(asyncOperation);
        }

        public static WebRequestAwaiter<T> GetAwaiter<T>(this WebRequest<T> webRequest)
        {
            return new WebRequestAwaiter<T>(webRequest);
        }
```

> **이 두 줄이 landmine의 정체였다.** 파일에 `using GameFramework;` 가 있느냐 없느냐로 어느 awaiter가 잡히는지 갈렸고, 그에 따라 요청 실패가 예외가 되기도 하고 안 되기도 했다. 지우면 UniTask 것 하나만 남는다.

빈 `Runtime/Scripts/WebRequest/` 폴더와 그 `.meta` 도 지운다.

- [ ] **Step 3: GameFramework 테스트가 여전히 통과하는지 확인한다**

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -runTests -batchmode -projectPath /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client \
  -testPlatform EditMode -testResults /tmp/all-results.xml -logFile /tmp/all-tests.log
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' /tmp/all-results.xml | head -1
```

기대: 기존 125건 + 신규 20건 = **145건 통과, 실패 0**.

- [ ] **Step 4: 양쪽 프로젝트 컴파일 에러 0을 확인한다**

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -quit -batchmode -projectPath /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client -logFile /tmp/c.log
grep -c "error CS" /tmp/c.log
"$UNITY" -quit -batchmode -projectPath /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server -logFile /tmp/s.log
grep -c "error CS" /tmp/s.log
```

기대: 둘 다 `0`.

- [ ] **Step 5: 잔여 참조 0을 확인한다**

```bash
cd /Users/insoobae/workspace/LOP
grep -rn "WebRequestBuilder\|WebRequestException\|WebRequestJson\|IWebRequestInterceptor\|WebRequestAwaiter\|WebRequest<" \
  GameFramework/Runtime GameFramework/Tests \
  LeagueOfPhysical-Client/Assets/Scripts LeagueOfPhysical-Server/Assets/Scripts
```

기대: 출력 없음.

- [ ] **Step 6: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/GameFramework
git add -A Runtime/Scripts
git commit -m "refactor(http): 옛 WebRequest 계층 삭제

GetAwaiter 확장 2개가 사라져 awaiter가 UniTask 것 하나로 확정됐다 —
using 한 줄로 요청 실패 처리가 뒤집히던 문제가 구조적으로 불가능해졌다."
```

---

## 완료 기준

- [ ] GameFramework EditMode 145건 통과 (기존 125 + 신규 20), 실패 0
- [ ] 신규 테스트 4건(Task 1·2·3·4 각각의 되돌리기 단계)이 되돌렸을 때 실제로 실패함을 확인
- [ ] 클라·서버 컴파일 에러 각 0 (배치모드 직접 확인)
- [ ] 클라 수동 검증 3건 통과 — 특히 **백엔드 끈 채 재기동 시 계정 보존**
- [ ] 서버 수동 검증 3건 통과
- [ ] `grep`으로 옛 타입 잔여 참조 0
- [ ] 병합 순서: **GameFramework → 클라 → 서버** (`--no-ff`)
