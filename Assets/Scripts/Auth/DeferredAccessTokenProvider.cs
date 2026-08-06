using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework.Http;
using UnityEngine;

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

            if (provider == null)
            {
                //  배선 전이면 토큰이 없다 — 헤더를 안 붙이는 것이 기존 동작이다. 이 요청은 로그인 DI가
                //  아직 끝나기 전에 나간 것이라, 서버가 토큰을 강제하면 401로 되돌아온다(드물게 발생 예상).
                Debug.LogWarning("[Auth] 로그인 배선이 끝나기 전에 나간 요청이라 토큰을 붙이지 못했습니다.");
                return UniTask.FromResult<string>(null);
            }

            return provider.GetAccessTokenAsync(forceRefresh, cancellationToken);
        }
    }
}
