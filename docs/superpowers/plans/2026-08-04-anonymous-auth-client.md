# 익명 로그인 클라이언트 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Unity 클라이언트가 서버의 익명 로그인(`/auth/anonymous`, `/auth/login`)으로 세션 토큰을 받아 보관·갱신하고, 그 토큰을 모든 REST 요청에 싣게 한다.

**Architecture:** 재사용 가능한 메커니즘(JWT 검증, 토큰 만료·갱신 계산, 자격증명 저장, 프로필 분리)은 **GameFramework**에 두고 거기서 EditMode 테스트로 덮는다. LOP 서버 계약에 묶인 부분(`/auth` 호출, DTO, Entrance 흐름, 로그인 팝업)만 클라이언트에 남는다.

**Tech Stack:** Unity, C#, VContainer, UniTask, MessagePipe, GameFramework(`Tests/Runtime` EditMode), NUnit

**설계 문서:** `docs/superpowers/specs/2026-08-04-anonymous-auth-session-design.md`
**선행 완료:** 백엔드 계획(`2026-08-04-anonymous-auth-backend.md`) — lop-backend `main`에 병합됨(`58d813e`)

## Global Constraints

- **두 저장소를 건드린다.** `/Users/insoobae/workspace/LOP/GameFramework`(Task 1~3)와 클라 워크트리 `/Users/insoobae/workspace/LOP/.worktrees/auth-anonymous-session`(Task 4~7). 각 태스크의 **Files** 절이 어느 리포인지 명시한다.
- **서버 계약은 이미 확정돼 있다. 바꾸지 않는다.**
  - `POST {lobbyBaseURL}/auth/anonymous` — 요청 바디 없음 → `201 { userId, credential: { provider, providerUserId, secret }, accessToken, expiresIn }`
  - `POST {lobbyBaseURL}/auth/login` — `{ provider, providerUserId, secret? }` → `200 { userId, accessToken, expiresIn }` / `401` / `501` / `400`
  - `provider` 문자열은 `"ANONYMOUS"` / `"GOOGLE_PLAY_GAMES"` / `"GAME_CENTER"` (서버 Prisma enum과 정확히 일치)
  - `expiresIn` 단위는 **초**, 현재 3600
- **명명 충돌 주의**: GameFramework에는 이미 `GameFramework.ISession`/`ISessionManager`가 있고 그건 **네트워크 접속 세션**이다. 인증 세션에 `Session` 단독 이름을 쓰지 않는다 — `AccessToken`, `AuthCredential`, `AuthProfile`처럼 `Auth`/`Token`을 붙여 구분한다.
- **이번 계획에서 기존 API 보호를 켜지 않는다.** 서버 미들웨어 부착과 룸 인증은 다음 계획(cutover)이다. 이 계획이 끝난 시점에도 게임은 지금처럼 동작해야 한다.
- 들여쓰기 4칸, C# 표준 컨벤션. 주석은 *왜*만, 한국어로, 비자명한 곳에만.
- **`.meta` 파일**: 새 스크립트/폴더를 만들면 Unity가 생성한 `.meta`를 반드시 함께 커밋한다. 직접 만들지 않는다.
- Unity 에디터를 열어야 `.meta`가 생긴다. 각 태스크의 커밋 단계에서 에디터를 한 번 띄워 컴파일과 `.meta` 생성을 확인한다.

---

### Task 0: 두 리포 브랜치 준비

**Files:** 없음 (git 작업)

- [ ] **Step 1: GameFramework 브랜치**

```bash
cd /Users/insoobae/workspace/LOP/GameFramework
git checkout main && git pull --ff-only
git checkout -b feature/auth-primitives
git status -sb
```

- [ ] **Step 2: 클라 워크트리 확인**

클라는 이미 `feature/auth-anonymous-session` 워크트리가 있다(문서 5커밋). 그대로 쓴다.

```bash
cd /Users/insoobae/workspace/LOP/.worktrees/auth-anonymous-session
git status -sb
git log --oneline -3
```

기대: 브랜치 `feature/auth-anonymous-session`, 트리 깨끗.

---

### Task 1: GameFramework — JWT HS256 검증

**Files (리포: GameFramework):**
- Create: `Runtime/Scripts/Auth/Jwt.cs`
- Create: `Tests/Runtime/Auth/JwtTests.cs`

**Interfaces:**
- Produces:
  - `static class Jwt`
  - `static bool TryVerifyHs256(string token, string secret, out string subject, DateTimeOffset now)` — 서명·만료·`sub` 검증. 실패 시 false, `subject`는 null

> 이 검증기는 **Unity 룸 서버가 다음 계획에서 쓴다.** 여기서 먼저 만드는 이유는 순수 로직이라 GF에서 테스트로 완전히 덮을 수 있고, 서버 발급 토큰과의 상호운용을 미리 확인해 두기 위해서다.

- [ ] **Step 1: 실패하는 테스트 작성**

`Tests/Runtime/Auth/JwtTests.cs`:

```csharp
using System;
using System.Security.Cryptography;
using System.Text;
using GameFramework.Auth;
using NUnit.Framework;

namespace GameFramework.Tests.Auth
{
    public class JwtTests
    {
        private const string Secret = "test-secret-0123456789";
        private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

        //  서버(jsonwebtoken)가 만드는 것과 같은 모양의 토큰을 테스트 안에서 직접 만든다.
        //  구현이 자기 자신이 만든 토큰만 통과시키는 상황을 피하려면 인코딩을 독립적으로 재현해야 한다.
        private static string MakeToken(string subject, long expUnixSeconds, string secret)
        {
            string header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
            string payload = Base64Url(Encoding.UTF8.GetBytes($"{{\"sub\":\"{subject}\",\"exp\":{expUnixSeconds}}}"));
            string signingInput = $"{header}.{payload}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            string signature = Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput)));

            return $"{signingInput}.{signature}";
        }

        private static string Base64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        [Test]
        public void 유효한_토큰이면_sub를_돌려준다()
        {
            string token = MakeToken("user-1", Now.ToUnixTimeSeconds() + 3600, Secret);

            Assert.IsTrue(Jwt.TryVerifyHs256(token, Secret, out string subject, Now));
            Assert.AreEqual("user-1", subject);
        }

        [Test]
        public void 다른_키로_검증하면_실패한다()
        {
            string token = MakeToken("user-1", Now.ToUnixTimeSeconds() + 3600, Secret);

            Assert.IsFalse(Jwt.TryVerifyHs256(token, "another-secret", out string subject, Now));
            Assert.IsNull(subject);
        }

        //  서명이 깨진 토큰은 "이상한 값"이 아니라 위조 시도다. 절대 통과하면 안 된다.
        [Test]
        public void 페이로드를_바꿔치기하면_실패한다()
        {
            string token = MakeToken("user-1", Now.ToUnixTimeSeconds() + 3600, Secret);
            string[] parts = token.Split('.');
            string forged = Base64Url(Encoding.UTF8.GetBytes("{\"sub\":\"user-2\",\"exp\":9999999999}"));

            Assert.IsFalse(Jwt.TryVerifyHs256($"{parts[0]}.{forged}.{parts[2]}", Secret, out _, Now));
        }

        [Test]
        public void 만료된_토큰은_실패한다()
        {
            string token = MakeToken("user-1", Now.ToUnixTimeSeconds() - 1, Secret);

            Assert.IsFalse(Jwt.TryVerifyHs256(token, Secret, out _, Now));
        }

        [Test]
        public void 만료_직전은_통과한다()
        {
            string token = MakeToken("user-1", Now.ToUnixTimeSeconds() + 1, Secret);

            Assert.IsTrue(Jwt.TryVerifyHs256(token, Secret, out _, Now));
        }

        //  alg를 none으로 바꾸고 서명을 비운 고전적 우회. 알고리즘을 고정하지 않으면 통과해 버린다.
        [Test]
        public void alg_none_토큰은_실패한다()
        {
            string header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"));
            string payload = Base64Url(Encoding.UTF8.GetBytes($"{{\"sub\":\"user-1\",\"exp\":{Now.ToUnixTimeSeconds() + 3600}}}"));

            Assert.IsFalse(Jwt.TryVerifyHs256($"{header}.{payload}.", Secret, out _, Now));
        }

        [TestCase("")]
        [TestCase("not-a-token")]
        [TestCase("a.b")]
        [TestCase("a.b.c.d")]
        public void 형식이_아니면_실패한다(string token)
        {
            Assert.IsFalse(Jwt.TryVerifyHs256(token, Secret, out _, Now));
        }
    }
}
```

- [ ] **Step 2: Unity에서 테스트가 실패하는지 확인**

Unity 에디터 `Window > General > Test Runner > EditMode`에서 `JwtTests` 실행.
기대: 컴파일 실패 (`GameFramework.Auth.Jwt` 없음).

- [ ] **Step 3: 구현**

`Runtime/Scripts/Auth/Jwt.cs`:

```csharp
using System;
using System.Security.Cryptography;
using System.Text;

namespace GameFramework.Auth
{
    /// <summary>서버가 발급한 HS256 JWT를 검증한다. 발급은 서버 몫이고 여기서는 검증만 한다.</summary>
    public static class Jwt
    {
        private const string ExpectedHeaderAlgorithm = "\"alg\":\"HS256\"";

        public static bool TryVerifyHs256(string token, string secret, out string subject, DateTimeOffset now)
        {
            subject = null;

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(secret))
            {
                return false;
            }

            string[] parts = token.Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            string header = DecodeToString(parts[0]);
            //  알고리즘을 고정하지 않으면 alg를 none이나 비대칭으로 바꾼 토큰이 통과한다.
            if (header == null || header.Replace(" ", string.Empty).Contains(ExpectedHeaderAlgorithm) == false)
            {
                return false;
            }

            if (ComputeSignature($"{parts[0]}.{parts[1]}", secret) != parts[2])
            {
                return false;
            }

            string payload = DecodeToString(parts[1]);
            if (payload == null)
            {
                return false;
            }

            if (TryReadNumber(payload, "exp", out long exp) == false || exp <= now.ToUnixTimeSeconds())
            {
                return false;
            }

            if (TryReadString(payload, "sub", out string sub) == false || string.IsNullOrEmpty(sub))
            {
                return false;
            }

            subject = sub;
            return true;
        }

        private static string ComputeSignature(string signingInput, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            return Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput)));
        }

        private static string Base64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string DecodeToString(string base64Url)
        {
            try
            {
                string padded = base64Url.Replace('-', '+').Replace('_', '/');
                padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
                return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            }
            catch
            {
                return null;
            }
        }

        //  JSON 파서를 끌어오지 않는 이유: 이 payload는 우리 서버가 만든 두 필드짜리 고정 형태다.
        private static bool TryReadString(string json, string key, out string value)
        {
            value = null;
            int start = json.IndexOf($"\"{key}\":\"", StringComparison.Ordinal);
            if (start < 0)
            {
                return false;
            }

            start += key.Length + 4;
            int end = json.IndexOf('"', start);
            if (end < 0)
            {
                return false;
            }

            value = json.Substring(start, end - start);
            return true;
        }

        private static bool TryReadNumber(string json, string key, out long value)
        {
            value = 0;
            int start = json.IndexOf($"\"{key}\":", StringComparison.Ordinal);
            if (start < 0)
            {
                return false;
            }

            start += key.Length + 3;
            int end = start;
            while (end < json.Length && char.IsDigit(json[end]))
            {
                end++;
            }

            return end > start && long.TryParse(json.Substring(start, end - start), out value);
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Test Runner에서 `JwtTests` 전체 통과(10 케이스 — `[TestCase]` 4개 포함). 기존 GameFramework 테스트도 모두 통과하는지 함께 확인.

- [ ] **Step 5: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/GameFramework
git add Runtime/Scripts/Auth Tests/Runtime/Auth
git status --short   # .meta 4개가 함께 잡히는지 확인
git commit -m "feat(auth): 서버 발급 HS256 JWT 검증기 추가

알고리즘을 HS256으로 고정한다 — 헤더의 alg를 그대로 믿으면 none으로 바꾼
토큰이 서명 없이 통과한다."
```

---

### Task 2: GameFramework — 액세스 토큰 만료·갱신 계산

**Files (리포: GameFramework):**
- Create: `Runtime/Scripts/Auth/AccessTokenInfo.cs`
- Create: `Tests/Runtime/Auth/AccessTokenInfoTests.cs`

**Interfaces:**
- Produces:
  - `readonly struct AccessTokenInfo` — `string Token`, `DateTimeOffset ExpiresAt`
  - `static AccessTokenInfo FromExpiresIn(string token, int expiresInSeconds, DateTimeOffset issuedAt)`
  - `bool IsExpired(DateTimeOffset now)`
  - `bool NeedsRefresh(DateTimeOffset now, TimeSpan margin)` — 만료 `margin` 전이면 true
  - `static readonly TimeSpan DefaultRefreshMargin` (= 5분)

> 시각을 인자로 받는 이유: `DateTimeOffset.UtcNow`를 내부에서 읽으면 만료 경계를 테스트할 수 없다.

- [ ] **Step 1: 실패하는 테스트 작성**

`Tests/Runtime/Auth/AccessTokenInfoTests.cs`:

```csharp
using System;
using GameFramework.Auth;
using NUnit.Framework;

namespace GameFramework.Tests.Auth
{
    public class AccessTokenInfoTests
    {
        private static readonly DateTimeOffset Issued = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

        [Test]
        public void expiresIn_초를_만료시각으로_바꾼다()
        {
            var info = AccessTokenInfo.FromExpiresIn("t", 3600, Issued);

            Assert.AreEqual(Issued.AddSeconds(3600), info.ExpiresAt);
            Assert.AreEqual("t", info.Token);
        }

        [Test]
        public void 만료_전에는_만료가_아니다()
        {
            var info = AccessTokenInfo.FromExpiresIn("t", 3600, Issued);

            Assert.IsFalse(info.IsExpired(Issued.AddSeconds(3599)));
        }

        [Test]
        public void 만료_시각_정각부터_만료다()
        {
            var info = AccessTokenInfo.FromExpiresIn("t", 3600, Issued);

            Assert.IsTrue(info.IsExpired(Issued.AddSeconds(3600)));
        }

        //  갱신은 만료보다 먼저 일어나야 한다 — 만료된 뒤에 갱신하면 그 사이 요청이 401을 맞는다.
        [Test]
        public void 만료_5분_전부터_갱신이_필요하다()
        {
            var info = AccessTokenInfo.FromExpiresIn("t", 3600, Issued);
            var margin = AccessTokenInfo.DefaultRefreshMargin;

            Assert.IsFalse(info.NeedsRefresh(Issued.AddSeconds(3600 - 301), margin));
            Assert.IsTrue(info.NeedsRefresh(Issued.AddSeconds(3600 - 300), margin));
        }

        [Test]
        public void 이미_만료됐으면_갱신도_필요하다()
        {
            var info = AccessTokenInfo.FromExpiresIn("t", 3600, Issued);

            Assert.IsTrue(info.NeedsRefresh(Issued.AddSeconds(7200), AccessTokenInfo.DefaultRefreshMargin));
        }

        [Test]
        public void 기본값은_비어있는_토큰이고_항상_만료다()
        {
            var info = default(AccessTokenInfo);

            Assert.IsTrue(info.IsExpired(Issued));
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Test Runner에서 컴파일 실패(`AccessTokenInfo` 없음)를 확인한다.

- [ ] **Step 3: 구현**

`Runtime/Scripts/Auth/AccessTokenInfo.cs`:

```csharp
using System;

namespace GameFramework.Auth
{
    /// <summary>서버가 발급한 액세스 토큰과 그 만료 시각. 갱신 시점 판단만 담당한다.</summary>
    public readonly struct AccessTokenInfo
    {
        /// <summary>만료 몇 분 전부터 갱신할지. 요청이 만료된 토큰을 만나 401이 나기 전에 미리 바꾼다.</summary>
        public static readonly TimeSpan DefaultRefreshMargin = TimeSpan.FromMinutes(5);

        public string Token { get; }
        public DateTimeOffset ExpiresAt { get; }

        private AccessTokenInfo(string token, DateTimeOffset expiresAt)
        {
            Token = token;
            ExpiresAt = expiresAt;
        }

        public static AccessTokenInfo FromExpiresIn(string token, int expiresInSeconds, DateTimeOffset issuedAt)
        {
            return new AccessTokenInfo(token, issuedAt.AddSeconds(expiresInSeconds));
        }

        public bool IsExpired(DateTimeOffset now)
        {
            return now >= ExpiresAt;
        }

        public bool NeedsRefresh(DateTimeOffset now, TimeSpan margin)
        {
            return now >= ExpiresAt - margin;
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인 후 커밋**

```bash
cd /Users/insoobae/workspace/LOP/GameFramework
git add Runtime/Scripts/Auth Tests/Runtime/Auth
git commit -m "feat(auth): 액세스 토큰 만료·갱신 시점 계산 추가

시각을 인자로 받는다 — UtcNow를 내부에서 읽으면 만료 경계를 테스트할 수 없다."
```

---

### Task 3: GameFramework — 자격증명 저장소와 프로필

**Files (리포: GameFramework):**
- Create: `Runtime/Scripts/Auth/AuthCredential.cs`
- Create: `Runtime/Scripts/Auth/IAuthCredentialStore.cs`
- Create: `Runtime/Scripts/Auth/PlayerPrefsAuthCredentialStore.cs`
- Create: `Runtime/Scripts/Auth/AuthProfile.cs`
- Create: `Tests/Runtime/Auth/AuthProfileTests.cs`
- Create: `Tests/Runtime/Auth/PlayerPrefsAuthCredentialStoreTests.cs`

**Interfaces:**
- Produces:
  - `sealed class AuthCredential { string Provider; string ProviderUserId; string Secret; }`
  - `interface IAuthCredentialStore { AuthCredential Load(); void Save(AuthCredential); void Clear(); }`
  - `sealed class PlayerPrefsAuthCredentialStore : IAuthCredentialStore` — ctor `(string keyPrefix, string profile)`
  - `static class AuthProfile` — `string Resolve(string[] commandLineArgs)`, `string Current`
  - `const string DefaultProfile = "default"`

> **프로필의 목적**: 한 PC에서 클라를 두 개 띄울 때 서로 다른 계정으로 붙게 한다. Unity Multiplayer
> Play Mode가 인스턴스마다 `-name Player1/Player2/…`를 넘기므로 그 값을 그대로 프로필 이름으로 쓴다.
> 클라 리포의 `DeviceIdentifier`가 이미 같은 인자로 같은 문제를 풀고 있었고, 그 메커니즘을 승계하는
> 것이지 새 관례를 만드는 것이 아니다.

- [ ] **Step 1: 실패하는 테스트 작성**

`Tests/Runtime/Auth/AuthProfileTests.cs`:

```csharp
using GameFramework.Auth;
using NUnit.Framework;

namespace GameFramework.Tests.Auth
{
    public class AuthProfileTests
    {
        [Test]
        public void 인자가_없으면_기본_프로필이다()
        {
            Assert.AreEqual(AuthProfile.DefaultProfile, AuthProfile.Resolve(new[] { "Unity.exe" }));
        }

        //  MPPM의 첫 인스턴스는 Player1이며 그것을 기본과 같은 계정으로 본다 —
        //  그렇지 않으면 평소 에디터 실행과 첫 인스턴스가 서로 다른 계정이 되어 혼란스럽다.
        [Test]
        public void Player1은_기본_프로필과_같다()
        {
            Assert.AreEqual(AuthProfile.DefaultProfile, AuthProfile.Resolve(new[] { "Unity.exe", "-name", "Player1" }));
        }

        [Test]
        public void 다른_인스턴스_이름은_그대로_프로필이_된다()
        {
            Assert.AreEqual("Player2", AuthProfile.Resolve(new[] { "Unity.exe", "-name", "Player2" }));
        }

        [Test]
        public void 값이_빠진_인자는_기본_프로필이다()
        {
            Assert.AreEqual(AuthProfile.DefaultProfile, AuthProfile.Resolve(new[] { "Unity.exe", "-name" }));
        }

        [Test]
        public void 인자가_null이어도_죽지_않는다()
        {
            Assert.AreEqual(AuthProfile.DefaultProfile, AuthProfile.Resolve(null));
        }
    }
}
```

`Tests/Runtime/Auth/PlayerPrefsAuthCredentialStoreTests.cs`:

```csharp
using GameFramework.Auth;
using NUnit.Framework;
using UnityEngine;

namespace GameFramework.Tests.Auth
{
    public class PlayerPrefsAuthCredentialStoreTests
    {
        private const string Prefix = "Test.Auth";

        [TearDown]
        public void TearDown()
        {
            //  PlayerPrefs는 에디터 전역에 남는다 — 테스트가 서로의 값을 보지 않도록 지운다.
            new PlayerPrefsAuthCredentialStore(Prefix, "default").Clear();
            new PlayerPrefsAuthCredentialStore(Prefix, "Player2").Clear();
            PlayerPrefs.Save();
        }

        [Test]
        public void 저장한_것을_그대로_돌려준다()
        {
            var store = new PlayerPrefsAuthCredentialStore(Prefix, "default");
            store.Save(new AuthCredential { Provider = "ANONYMOUS", ProviderUserId = "p-1", Secret = "s-1" });

            var loaded = store.Load();

            Assert.AreEqual("ANONYMOUS", loaded.Provider);
            Assert.AreEqual("p-1", loaded.ProviderUserId);
            Assert.AreEqual("s-1", loaded.Secret);
        }

        [Test]
        public void 저장한_적_없으면_null이다()
        {
            Assert.IsNull(new PlayerPrefsAuthCredentialStore(Prefix, "default").Load());
        }

        [Test]
        public void 지우면_null이_된다()
        {
            var store = new PlayerPrefsAuthCredentialStore(Prefix, "default");
            store.Save(new AuthCredential { Provider = "ANONYMOUS", ProviderUserId = "p-1", Secret = "s-1" });

            store.Clear();

            Assert.IsNull(store.Load());
        }

        //  프로필 분리가 실제로 되는지 — 이게 깨지면 한 PC의 두 인스턴스가 같은 계정으로 붙는다.
        [Test]
        public void 프로필이_다르면_서로_보이지_않는다()
        {
            var first = new PlayerPrefsAuthCredentialStore(Prefix, "default");
            var second = new PlayerPrefsAuthCredentialStore(Prefix, "Player2");
            first.Save(new AuthCredential { Provider = "ANONYMOUS", ProviderUserId = "p-1", Secret = "s-1" });

            Assert.IsNull(second.Load());
            Assert.AreEqual("p-1", first.Load().ProviderUserId);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

- [ ] **Step 3: 구현**

`Runtime/Scripts/Auth/AuthCredential.cs`:

```csharp
using System;

namespace GameFramework.Auth
{
    /// <summary>다음 실행에서 다시 로그인하기 위해 기기에 보관하는 자격증명.</summary>
    [Serializable]
    public class AuthCredential
    {
        public string Provider;
        public string ProviderUserId;
        public string Secret;
    }
}
```

`Runtime/Scripts/Auth/IAuthCredentialStore.cs`:

```csharp
namespace GameFramework.Auth
{
    /// <summary>자격증명 보관소. 저장 위치를 바꿀 수 있도록 인터페이스로 둔다
    /// (지금은 PlayerPrefs, 계정에 지킬 가치가 생기면 플랫폼 보안 저장소로 교체).</summary>
    public interface IAuthCredentialStore
    {
        AuthCredential Load();
        void Save(AuthCredential credential);
        void Clear();
    }
}
```

`Runtime/Scripts/Auth/PlayerPrefsAuthCredentialStore.cs`:

```csharp
using UnityEngine;

namespace GameFramework.Auth
{
    /// <summary>PlayerPrefs 기반 보관소.
    /// 주의: PlayerPrefs는 암호화되지 않는다. 기기를 만질 수 있는 사람은 값을 꺼낼 수 있다.</summary>
    public class PlayerPrefsAuthCredentialStore : IAuthCredentialStore
    {
        private readonly string key;

        public PlayerPrefsAuthCredentialStore(string keyPrefix, string profile)
        {
            //  프로필을 키에 섞어야 한 기기에서 인스턴스마다 다른 계정을 쓸 수 있다.
            key = $"{keyPrefix}.{profile}.Credential";
        }

        public AuthCredential Load()
        {
            string json = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            return JsonUtility.FromJson<AuthCredential>(json);
        }

        public void Save(AuthCredential credential)
        {
            PlayerPrefs.SetString(key, JsonUtility.ToJson(credential));
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
```

`Runtime/Scripts/Auth/AuthProfile.cs`:

```csharp
using System;

namespace GameFramework.Auth
{
    /// <summary>한 기기에서 여러 계정을 쓰기 위한 프로필. Multiplayer Play Mode가 인스턴스마다
    /// 넘겨주는 -name 인자를 그대로 쓴다(MPPM에는 "몇 번 인스턴스인가"를 주는 API가 없다).</summary>
    public static class AuthProfile
    {
        public const string DefaultProfile = "default";

        private const string InstanceNameArgument = "-name";
        private const string FirstInstanceName = "Player1";

        private static string cached;

        public static string Current => cached ??= Resolve(Environment.GetCommandLineArgs());

        public static string Resolve(string[] commandLineArgs)
        {
            if (commandLineArgs == null)
            {
                return DefaultProfile;
            }

            for (int i = 0; i < commandLineArgs.Length - 1; i++)
            {
                if (commandLineArgs[i] != InstanceNameArgument)
                {
                    continue;
                }

                string name = commandLineArgs[i + 1];
                //  첫 인스턴스를 기본과 같게 둬야 평소 에디터 실행과 계정이 갈리지 않는다.
                return string.IsNullOrEmpty(name) || name == FirstInstanceName ? DefaultProfile : name;
            }

            return DefaultProfile;
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인 후 커밋**

```bash
cd /Users/insoobae/workspace/LOP/GameFramework
git add Runtime/Scripts/Auth Tests/Runtime/Auth
git commit -m "feat(auth): 자격증명 보관소와 인스턴스 프로필 추가

프로필을 저장 키에 섞는다 — 한 PC에서 클라 두 개가 같은 계정으로 붙으면
매칭 테스트가 불가능하다. MPPM의 -name을 그대로 쓴다."
```

- [ ] **Step 5: GameFramework 푸시**

```bash
git push -u origin feature/auth-primitives
```

---

### Task 4: 클라 — `/auth` WebAPI와 DTO

**Files (리포: 클라 워크트리):**
- Create: `Assets/Scripts/WebAPI/Dto/AuthDto.cs`
- Modify: `Assets/Scripts/WebAPI/WebAPI.cs`

**Interfaces:**
- Produces:
  - `class CredentialDto { public string provider; public string providerUserId; public string secret; }`
  - `class AnonymousSignInResponse { public string userId; public CredentialDto credential; public string accessToken; public int expiresIn; }`
  - `class LoginRequest { public string provider; public string providerUserId; public string secret; }`
  - `class LoginResponse { public string userId; public string accessToken; public int expiresIn; }`
  - `WebAPI.SignInAnonymous()` → `WebRequest<AnonymousSignInResponse>`
  - `WebAPI.Login(LoginRequest)` → `WebRequest<LoginResponse>`

> 서버 응답은 `code` 필드가 없는 평범한 JSON이다(`/auth`는 기존 `ResponseBase` 규약을 따르지 않는다).
> 성공 여부는 HTTP 상태 코드로 판단한다.

- [ ] **Step 1: DTO 작성**

`Assets/Scripts/WebAPI/Dto/AuthDto.cs`:

```csharp
using System;

namespace LOP
{
    [Serializable]
    public class CredentialDto
    {
        public string provider;
        public string providerUserId;
        public string secret;
    }

    [Serializable]
    public class AnonymousSignInResponse
    {
        public string userId;
        public CredentialDto credential;
        public string accessToken;
        public int expiresIn;
    }

    [Serializable]
    public class LoginRequest
    {
        public string provider;
        public string providerUserId;
        public string secret;
    }

    [Serializable]
    public class LoginResponse
    {
        public string userId;
        public string accessToken;
        public int expiresIn;
    }
}
```

- [ ] **Step 2: WebAPI에 두 호출 추가**

`Assets/Scripts/WebAPI/WebAPI.cs`의 `#region Lobby` 바로 위에 새 region을 추가:

```csharp
        #region Auth
        public static WebRequest<AnonymousSignInResponse> SignInAnonymous()
        {
            return new WebRequestBuilder<AnonymousSignInResponse>()
                .SetUri($"{EnvironmentSettings.active.lobbyBaseURL}/auth/anonymous")
                .SetMethod(HttpMethod.POST)
                .SetWebRequestInterceptor(LOPWebRequestInterceptor.Default)
                .Build();
        }

        public static WebRequest<LoginResponse> Login(LoginRequest request)
        {
            return new WebRequestBuilder<LoginResponse>()
                .SetUri($"{EnvironmentSettings.active.lobbyBaseURL}/auth/login")
                .SetMethod(HttpMethod.POST)
                .SetRequestBody(request)
                .SetWebRequestInterceptor(LOPWebRequestInterceptor.Default)
                .Build();
        }
        #endregion
```

- [ ] **Step 3: 컴파일 확인 후 커밋**

Unity 에디터에서 컴파일 에러가 없는지 확인한다.

```bash
cd /Users/insoobae/workspace/LOP/.worktrees/auth-anonymous-session
git add Assets/Scripts/WebAPI
git status --short   # AuthDto.cs.meta 포함 확인
git commit -m "feat(auth): /auth 엔드포인트 호출과 DTO 추가"
```

---

### Task 5: 클라 — AuthenticationService

**Files (리포: 클라 워크트리):**
- Create: `Assets/Scripts/Auth/AuthProvider.cs`
- Create: `Assets/Scripts/Auth/AuthSession.cs`
- Create: `Assets/Scripts/Auth/AuthenticationService.cs`
- Modify: `Assets/Scripts/RootLifetimeScope.cs`

**Interfaces:**
- Consumes: `AccessTokenInfo`, `AuthCredential`, `IAuthCredentialStore`, `PlayerPrefsAuthCredentialStore`, `AuthProfile` (Task 1~3), `WebAPI.SignInAnonymous`/`Login` (Task 4)
- Produces:
  - `enum AuthProvider { Anonymous, GooglePlayGames, GameCenter }` + `ToWireString()`
  - `class AuthSession { string UserId; AccessTokenInfo Token; }`
  - `class AuthenticationService`
    - `UniTask<AuthSession> SignInAsync(AuthProvider provider)`
    - `AuthSession Current { get; }`
    - `bool IsSignedIn { get; }`
    - `string AccessToken { get; }` — 없으면 null
    - `void SignOut()`

- [ ] **Step 1: enum과 세션 타입 작성**

`Assets/Scripts/Auth/AuthProvider.cs`:

```csharp
using System;

namespace LOP
{
    public enum AuthProvider
    {
        Anonymous = 0,
        GooglePlayGames = 1,
        GameCenter = 2,
    }

    public static class AuthProviderExtensions
    {
        //  서버 Prisma enum 값과 정확히 일치해야 한다. 어긋나면 400/501로 떨어진다.
        public static string ToWireString(this AuthProvider provider)
        {
            switch (provider)
            {
                case AuthProvider.Anonymous: return "ANONYMOUS";
                case AuthProvider.GooglePlayGames: return "GOOGLE_PLAY_GAMES";
                case AuthProvider.GameCenter: return "GAME_CENTER";
                default: throw new ArgumentOutOfRangeException(nameof(provider), provider, null);
            }
        }
    }
}
```

`Assets/Scripts/Auth/AuthSession.cs`:

```csharp
using GameFramework.Auth;

namespace LOP
{
    /// <summary>로그인으로 확보한 세션. 네트워크 접속 세션(GameFramework.ISession)과 다른 개념이다.</summary>
    public class AuthSession
    {
        public string UserId { get; }
        public AccessTokenInfo Token { get; }

        public AuthSession(string userId, AccessTokenInfo token)
        {
            UserId = userId;
            Token = token;
        }
    }
}
```

- [ ] **Step 2: AuthenticationService 작성**

`Assets/Scripts/Auth/AuthenticationService.cs`:

```csharp
using System;
using Cysharp.Threading.Tasks;
using GameFramework.Auth;
using UnityEngine;

namespace LOP
{
    /// <summary>익명 계정 로그인과 세션 보관. 저장된 자격증명이 있으면 그것으로 로그인하고,
    /// 없으면 새 익명 계정을 만든다.</summary>
    public class AuthenticationService
    {
        private readonly IAuthCredentialStore credentialStore;

        public AuthSession Current { get; private set; }
        public bool IsSignedIn => Current != null;
        public string AccessToken => Current?.Token.Token;

        /// <summary>기기에 쓸 수 있는 자격증명이 남아 있는지. 있으면 팝업 없이 자동 로그인한다.</summary>
        public bool HasStoredCredential => credentialStore.Load() != null;

        //  생성자를 하나만 둔다 — VContainer가 여러 생성자 중 무엇을 쓸지 헷갈리지 않게.
        public AuthenticationService(IAuthCredentialStore credentialStore)
        {
            this.credentialStore = credentialStore;
        }

        public async UniTask<AuthSession> SignInAsync(AuthProvider provider)
        {
            if (provider != AuthProvider.Anonymous)
            {
                throw new NotSupportedException($"{provider} 로그인은 아직 준비 중입니다.");
            }

            AuthCredential stored = credentialStore.Load();

            if (stored != null)
            {
                AuthSession session = await TryLoginAsync(stored);
                if (session != null)
                {
                    Current = session;
                    return session;
                }

                //  서버가 자격증명을 거부했다(개발 중 DB 초기화 등). 들고 있어봐야 영영 못 쓰므로
                //  버리고 새 계정을 만든다 — 사용자는 새 계정으로 그냥 진입한다.
                Debug.LogWarning("[Auth] 저장된 자격증명이 거부되어 새 익명 계정을 만듭니다.");
                credentialStore.Clear();
            }

            Current = await RegisterAnonymousAsync();
            return Current;
        }

        public void SignOut()
        {
            credentialStore.Clear();
            Current = null;
        }

        /// <summary>만료가 임박했으면 저장된 자격증명으로 다시 로그인해 토큰을 갈아끼운다.</summary>
        public async UniTask RefreshIfNeededAsync()
        {
            if (IsSignedIn == false)
            {
                return;
            }

            if (Current.Token.NeedsRefresh(DateTimeOffset.UtcNow, AccessTokenInfo.DefaultRefreshMargin) == false)
            {
                return;
            }

            AuthCredential stored = credentialStore.Load();
            if (stored == null)
            {
                return;
            }

            AuthSession refreshed = await TryLoginAsync(stored);
            if (refreshed != null)
            {
                Current = refreshed;
            }
        }

        private async UniTask<AuthSession> TryLoginAsync(AuthCredential credential)
        {
            var request = WebAPI.Login(new LoginRequest
            {
                provider = credential.Provider,
                providerUserId = credential.ProviderUserId,
                secret = credential.Secret,
            });

            await request;

            if (request.isSuccess == false)
            {
                return null;
            }

            LoginResponse response = request.response;
            return new AuthSession(
                response.userId,
                AccessTokenInfo.FromExpiresIn(response.accessToken, response.expiresIn, DateTimeOffset.UtcNow));
        }

        private async UniTask<AuthSession> RegisterAnonymousAsync()
        {
            var request = WebAPI.SignInAnonymous();
            await request;

            if (request.isSuccess == false)
            {
                throw new Exception($"익명 계정 생성에 실패했습니다. error: {request.error}");
            }

            AnonymousSignInResponse response = request.response;

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
    }
}
```

- [ ] **Step 3: DI 등록**

`Assets/Scripts/RootLifetimeScope.cs`의 `builder.Register<LOP.MasterData.LOPMasterData>(Lifetime.Singleton);` 바로 아래에 추가:

```csharp
            //  자격증명 보관소는 프로필(인스턴스)마다 키가 달라야 해서 인스턴스로 등록한다.
            builder.RegisterInstance<GameFramework.Auth.IAuthCredentialStore>(
                new GameFramework.Auth.PlayerPrefsAuthCredentialStore("LOP.Auth", GameFramework.Auth.AuthProfile.Current));
            builder.Register<AuthenticationService>(Lifetime.Singleton);
```

- [ ] **Step 4: 컴파일 확인 후 커밋**

```bash
cd /Users/insoobae/workspace/LOP/.worktrees/auth-anonymous-session
git add Assets/Scripts/Auth Assets/Scripts/RootLifetimeScope.cs
git status --short   # Auth 폴더 + 3개 .cs.meta 포함 확인
git commit -m "feat(auth): AuthenticationService — 익명 로그인과 세션 보관

저장된 자격증명이 거부되면 버리고 새 계정을 만든다 — dev DB를 밀 때마다
클라가 먹통이 되면 안 된다."
```

---

### Task 6: 클라 — 요청에 Bearer 토큰 싣기

**Files (리포: 클라 워크트리):**
- Modify: `Assets/Scripts/WebAPI/LOPWebRequestInterceptor.cs`
- Modify: `Assets/Scripts/RootLifetimeScope.cs`

**Interfaces:**
- Consumes: `AuthenticationService.AccessToken` (Task 5)
- Produces: `LOPWebRequestInterceptor.SetAccessTokenProvider(Func<string>)`

> 인터셉터는 `static`이라 DI로 주입할 수 없다. `RootLifetimeScope`가 이미 `GlobalMessagePipe.SetProvider`로
> 같은 문제를 푸는 자리가 있으므로 거기서 함께 배선한다.

- [ ] **Step 1: 인터셉터에 토큰 주입**

`Assets/Scripts/WebAPI/LOPWebRequestInterceptor.cs`를 다음으로 교체:

```csharp
using GameFramework;
using MessagePipe;
using System;
using UnityEngine.Networking;

namespace LOP
{
    public class LOPWebRequestInterceptor : IWebRequestInterceptor
    {
        public static LOPWebRequestInterceptor Default { get; private set; } = new LOPWebRequestInterceptor();

        //  static이라 DI가 안 된다 — RootLifetimeScope가 기동 시 공급자를 꽂아 준다.
        private static Func<string> accessTokenProvider;

        public static void SetAccessTokenProvider(Func<string> provider)
        {
            accessTokenProvider = provider;
        }

        public void OnBeforeRequest(UnityWebRequest request)
        {
            string token = accessTokenProvider?.Invoke();
            if (string.IsNullOrEmpty(token))
            {
                return;
            }

            request.SetRequestHeader("Authorization", $"Bearer {token}");
        }

        public void OnSuccess<T>(UnityWebRequest request, T response)
        {
            //  정적 인터셉터라 DI 주입 불가 → GlobalMessagePipe로 타입별 발행(RootLifetimeScope가 SetProvider).
            GlobalMessagePipe.GetPublisher<T>().Publish(response);
        }

        public void OnError(UnityWebRequest request, string error) { }
    }
}
```

- [ ] **Step 2: RootLifetimeScope에서 배선**

`RootLifetimeScope.cs`의 `RegisterBuildCallback` 안, `GlobalMessagePipe.SetProvider` 호출 근처에 추가:

```csharp
                //  모든 REST 요청이 현재 세션 토큰을 싣도록 인터셉터에 공급자를 꽂는다.
                var authenticationService = container.Resolve<AuthenticationService>();
                LOPWebRequestInterceptor.SetAccessTokenProvider(() => authenticationService.AccessToken);
```

> `RegisterBuildCallback` 본문의 정확한 형태는 파일을 열어 확인하고 기존 스타일에 맞춘다. 이미
> `GlobalMessagePipe.SetProvider(container.AsServiceProvider())` 같은 호출이 있으므로 그 옆에 둔다.

- [ ] **Step 3: 컴파일 확인 후 커밋**

```bash
git add Assets/Scripts/WebAPI/LOPWebRequestInterceptor.cs Assets/Scripts/RootLifetimeScope.cs
git commit -m "feat(auth): 모든 REST 요청에 Bearer 토큰 첨부

토큰이 없으면 헤더를 붙이지 않는다 — /auth 자체는 무인증 엔드포인트다."
```

---

### Task 7: 클라 — Entrance 흐름 교체

**Files (리포: 클라 워크트리):**
- Modify: `Assets/Scripts/Entrance/EntranceComponent/LoginComponent.cs`
- Modify: `Assets/Scripts/Entrance/EntranceComponent/CheckUserComponent.cs` → 개명 `LoadUserComponent.cs`
- Modify: `Assets/Scripts/Entrance/EntranceLifetimeScope.cs`
- Modify: `Assets/Scripts/UI/Login/LoginViewModel.cs`
- Modify: `Assets/Scripts/UI/Login/LoginView.cs`
- Modify: `Assets/Scripts/Domain/User.cs`
- Delete: `Assets/Scripts/Login/` 폴더 전체 (`Login.cs`, `LoginResult.cs`, `LoginService.cs`, `GuestLogin.cs`, `LogoutResult.cs`)
- Delete: `Assets/Scripts/Domain/DeviceIdentifier.cs`

**Interfaces:**
- Consumes: `AuthenticationService.SignInAsync` (Task 5)
- Produces: `LoginView : UIPopup, IResultView<AuthSession>`

- [ ] **Step 1: LoginViewModel 교체**

결과 타입이 `LoginResult` → `AuthSession`으로 바뀌고, VM이 직접 서비스를 호출한다.
**실패 시 결과를 확정하지 않고 모달을 열어둔 채 에러를 노출한다** — 로그인은 `AutoClose = false`인
필수 모달이라 실패하고 닫히면 빠져나올 길이 없다.

```csharp
using System;
using Cysharp.Threading.Tasks;

namespace LOP.UI
{
    /// <summary>로그인 팝업 ViewModel. 고른 방식으로 AuthenticationService를 호출해 세션을 확보하고,
    /// 결과(AuthSession)를 1회성으로 확정한다. 결과 확정이 곧 모달 닫기 신호.</summary>
    public class LoginViewModel : IDisposable
    {
        private readonly AuthenticationService authenticationService;
        private readonly UniTaskCompletionSource<AuthSession> _result = new();

        public UniTask<AuthSession> ResultAsync => _result.Task;

        public bool ShowGuest { get; }
        public bool ShowGpgs { get; }
        public bool ShowGameCenter { get; }

        /// <summary>로그인 실패 문구. 비어 있으면 표시하지 않는다.</summary>
        public string ErrorMessage { get; private set; } = string.Empty;

        public event Action ErrorMessageChanged;

        public LoginViewModel(AuthenticationService authenticationService)
        {
            this.authenticationService = authenticationService;

            ShowGuest = true;
            ShowGameCenter = false;
#if !UNITY_EDITOR && UNITY_ANDROID
            ShowGpgs = true;
#else
            ShowGpgs = false;
#endif
        }

        public async void RequestLogin(AuthProvider provider)
        {
            SetError(string.Empty);

            try
            {
                AuthSession session = await authenticationService.SignInAsync(provider);
                _result.TrySetResult(session);
            }
            catch (NotSupportedException)
            {
                SetError("준비 중입니다.");
            }
            catch (Exception exception)
            {
                //  실패해도 결과를 확정하지 않는다 — 확정하면 모달이 닫히고 사용자가 다시 시도할 수 없다.
                SetError("로그인에 실패했습니다. 다시 시도해 주세요.");
                UnityEngine.Debug.LogError(exception);
            }
        }

        private void SetError(string message)
        {
            ErrorMessage = message;
            ErrorMessageChanged?.Invoke();
        }

        public void Dispose()
        {
            _result.TrySetCanceled();
        }
    }
}
```

- [ ] **Step 2: LoginView 교체**

`IResultView<LoginResult>` → `IResultView<AuthSession>`, 버튼이 `AuthProvider`를 넘기고,
에러 문구를 표시한다.

```csharp
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>로그인 팝업 View. 버튼 클릭을 ViewModel 커맨드로 전달하고, ViewModel이 만든 결과를 포워딩한다.</summary>
    public class LoginView : UIPopup, IResultView<AuthSession>
    {
        private readonly LoginViewModel _viewModel;

        private Button _guestButton;
        private Button _gpgsButton;
        private Button _gamecenterButton;
        private Label _errorLabel;

        public LoginView(LoginViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        /// <summary>로그인은 임의로 닫을 수 없는 필수 모달.</summary>
        public override bool AutoClose => false;

        public UniTask<AuthSession> ResultAsync => _viewModel.ResultAsync;

        public override void OnOpen()
        {
            base.OnOpen();

            _guestButton = Root.Q<Button>("guest-login");
            _gpgsButton = Root.Q<Button>("gpgs-login");
            _gamecenterButton = Root.Q<Button>("gamecenter-login");
            _errorLabel = Root.Q<Label>("login-error");

            SetVisible(_guestButton, _viewModel.ShowGuest);
            SetVisible(_gpgsButton, _viewModel.ShowGpgs);
            SetVisible(_gamecenterButton, _viewModel.ShowGameCenter);

            _guestButton.clicked += OnGuestClicked;
            _gpgsButton.clicked += OnGpgsClicked;
            _gamecenterButton.clicked += OnGameCenterClicked;
            _viewModel.ErrorMessageChanged += OnErrorMessageChanged;

            OnErrorMessageChanged();
        }

        public override void OnClose()
        {
            if (_guestButton != null) _guestButton.clicked -= OnGuestClicked;
            if (_gpgsButton != null) _gpgsButton.clicked -= OnGpgsClicked;
            if (_gamecenterButton != null) _gamecenterButton.clicked -= OnGameCenterClicked;
            _viewModel.ErrorMessageChanged -= OnErrorMessageChanged;

            base.OnClose();
        }

        private void OnGuestClicked() => _viewModel.RequestLogin(AuthProvider.Anonymous);
        private void OnGpgsClicked() => _viewModel.RequestLogin(AuthProvider.GooglePlayGames);
        private void OnGameCenterClicked() => _viewModel.RequestLogin(AuthProvider.GameCenter);

        private void OnErrorMessageChanged()
        {
            if (_errorLabel == null)
            {
                return;
            }

            _errorLabel.text = _viewModel.ErrorMessage;
            SetVisible(_errorLabel, string.IsNullOrEmpty(_viewModel.ErrorMessage) == false);
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            if (element == null)
            {
                return;
            }

            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public override void Dispose()
        {
            _viewModel.Dispose();
            base.Dispose();
        }
    }
}
```

> **UXML 확인 필요**: 로그인 팝업 UXML에 `login-error`라는 이름의 `Label`이 없으면 추가한다.
> 파일 위치는 `Root.Q<Button>("guest-login")`가 찾는 UXML을 역추적해 확인할 것. 없으면 위 코드의
> null 가드 덕에 죽지는 않지만 에러가 보이지 않으므로 반드시 추가한다.

- [ ] **Step 3: LoginComponent 교체**

```csharp
using Cysharp.Threading.Tasks;
using LOP.UI;
using System;
using System.Threading.Tasks;

namespace LOP
{
    public class LoginComponent : IEntranceComponent
    {
        private readonly IWindowManager windowManager;
        private readonly AuthenticationService authenticationService;
        private readonly IUserDataStore userDataStore;

        public LoginComponent(IWindowManager windowManager, AuthenticationService authenticationService, IUserDataStore userDataStore)
        {
            this.windowManager = windowManager;
            this.authenticationService = authenticationService;
            this.userDataStore = userDataStore;
        }

        public async Task Execute()
        {
            AuthSession session = await TrySilentSignIn();

            if (session == null)
            {
                //  저장된 자격증명이 없다 — 사용자가 로그인 방식을 고르게 한다.
                session = await windowManager.OpenModalAsync<LoginView, AuthSession>();
            }

            userDataStore.user.id = session.UserId;
        }

        private async UniTask<AuthSession> TrySilentSignIn()
        {
            if (authenticationService.HasStoredCredential == false)
            {
                return null;
            }

            try
            {
                return await authenticationService.SignInAsync(AuthProvider.Anonymous);
            }
            catch (Exception)
            {
                //  네트워크 실패 등 — 팝업으로 넘겨 사용자가 재시도할 수 있게 한다.
                return null;
            }
        }
    }
}
```

`HasStoredCredential`은 Task 5에서 이미 만들어 둔 것을 쓴다 — 여기서 새로 추가하지 않는다.

- [ ] **Step 4: CheckUserComponent → LoadUserComponent**

유저 생성 책임이 사라졌다. 파일명을 `LoadUserComponent.cs`로 바꾸고 클래스명도 맞춘 뒤, 본문을
다음으로 교체한다. **예외를 삼키지 않는다** — 유저 로드에 실패했는데 로비까지 진행하면 그 뒤가 전부
이상하게 망가진다.

```csharp
using System.Threading.Tasks;

namespace LOP
{
    /// <summary>로그인으로 확보한 userId로 유저 부속 데이터를 읽어 온다. 계정 생성은 /auth 몫이다.</summary>
    public class LoadUserComponent : IEntranceComponent
    {
        private readonly IUserDataStore userDataStore;

        public LoadUserComponent(IUserDataStore userDataStore)
        {
            this.userDataStore = userDataStore;
        }

        public async Task Execute()
        {
            string userId = userDataStore.user.id;

            var getUser = await WebAPI.GetUser(userId);
            if (getUser.response.code != ResponseCode.SUCCESS)
            {
                throw new System.Exception($"유저 정보를 가져오는데 실패했습니다. code: {getUser.response.code}");
            }

            await WebAPI.GetUserLocation(userId);

            //  큐 목록을 TbQueue에서 읽는 것은 로비 선택 UI 슬라이스 몫이다 —
            //  마스터데이터가 이 컴포넌트보다 뒤에 로드돼서 지금은 값을 안다고 칠 수 없다.
            await WebAPI.GetUserStats(userId, 1);   // TbQueue: Casual
            await WebAPI.GetUserStats(userId, 2);   // TbQueue: Ranked
        }
    }
}
```

`EntranceLifetimeScope.cs`의 등록도 바꾼다:

```csharp
            builder.Register<IEntranceComponent, LoadUserComponent>(Lifetime.Transient);
```

- [ ] **Step 5: User 생성자 정리와 옛 코드 삭제**

`Assets/Scripts/Domain/User.cs`에서 기기 ID 기반 초기화를 지운다:

```csharp
namespace LOP
{
    public class User
    {
        public string id;
        public string username;
        public string email;
    }
}
```

옛 로그인 코드와 기기 식별자를 지운다(`.meta`도 함께).

```bash
cd /Users/insoobae/workspace/LOP/.worktrees/auth-anonymous-session
git rm -r Assets/Scripts/Login
git rm Assets/Scripts/Domain/DeviceIdentifier.cs Assets/Scripts/Domain/DeviceIdentifier.cs.meta
```

> 지우기 전에 `grep -rn "LoginService\|GuestLogin\|LoginResult\|DeviceIdentifier" Assets/Scripts`로
> 남은 참조가 없는지 확인한다. 있으면 그 자리를 먼저 정리한다.

- [ ] **Step 6: 컴파일 + 실제 동작 확인**

로컬 백엔드(`local-k8s` 또는 `local`)를 띄운 상태에서 Unity 에디터로 실행한다.

1. `PlayerPrefs`를 비운 상태에서 시작 → 로그인 팝업 → "게스트로 시작" → 진입 성공
2. 에디터를 껐다 다시 실행 → **팝업 없이** 바로 진입 (저장된 자격증명으로 자동 로그인)
3. 백엔드 DB를 비운 뒤 다시 실행 → 경고 로그와 함께 새 계정으로 진입, 먹통 없음
4. MPPM으로 인스턴스 2개 실행 → 서버에서 **서로 다른 userId** 확인
5. **자격증명이 저장된 상태에서 서버를 끄고(또는 네트워크를 끊고) 실행** → 로그인 팝업이 뜨고,
   서버를 다시 켠 뒤 재시도하면 **원래 계정(같은 userId)으로** 들어가야 한다.
   `PlayerPrefs`의 자격증명이 지워지거나 새 userId가 발급되면 **실패**다.

각 시나리오의 결과를 보고에 적는다. 3번이 실무에서 가장 자주 밟히는 경로다.

5번은 클라에 테스트 어셈블리가 없어 자동으로 덮을 수 없는데, 실패 시 피해는 가장 크다 — Task 5
리뷰에서 "일시적 실패(오프라인·5xx)를 자격증명 거부로 오인해 계정을 영구히 날리는" 버그가 실제로
발견됐고, 그 수정이 지켜지는지 확인하는 유일한 관문이다. 건너뛰지 말 것.

- [ ] **Step 7: 커밋**

```bash
git add -A Assets/Scripts
git status --short
git commit -m "feat(auth): Entrance 흐름을 익명 로그인으로 교체

기기 ID를 계정 식별자로 쓰던 경로를 걷어낸다. 계정 생성은 /auth 몫이 되어
CheckUserComponent는 데이터 로드만 하는 LoadUserComponent가 된다.
로그인 실패 시 결과를 확정하지 않아 모달이 열린 채 재시도할 수 있다."
```

---

## 완료 조건

- 앱을 처음 켜면 로그인 팝업이 뜨고, 게스트로 시작하면 서버가 익명 계정을 만들어 준다
- 두 번째 실행부터는 팝업 없이 자동 로그인된다
- 서버 DB를 비워도 클라가 스스로 새 계정을 만들어 복구한다
- MPPM 인스턴스 2개가 서로 다른 계정으로 붙는다
- 모든 REST 요청에 `Authorization: Bearer` 헤더가 실린다
- GameFramework의 인증 기반(JWT 검증, 만료 계산, 자격증명 저장, 프로필)이 EditMode 테스트로 덮인다
- **기존 API는 여전히 무인증이라 게임은 지금처럼 동작한다**

## 다음 계획 (별도 문서) — cutover

이 계획이 끝나도 **서버는 아직 토큰을 요구하지 않는다.** 남은 것:

1. lobby/matchmaking 기존 라우트에 `authMiddleware` + `requireSelf` 부착
2. `/auth/*` 레이트리밋 (스펙 §12의 "공개 배포 전 필수" 항목)
3. 룸 접속 인증 — 클라가 `userId` 대신 토큰만 보내고, Unity 서버가 `GameFramework.Auth.Jwt`로 검증해 토큰에서 userId를 꺼낸다 (클·서 동시 변경)
4. Unity 서버에 `LOP_AUTH_JWT_SECRET` 배선 (k8s env / 로컬 EditorPrefs)

3번이 클·서가 동시에 바뀌는 유일한 지점이라 별도 계획으로 분리했다. 2번을 여기 묶는 이유는 미들웨어를
켜는 작업과 손이 같기 때문이다.
