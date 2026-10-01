using System;
using Cysharp.Threading.Tasks;
using GameFramework.Http;

namespace LOP
{
    /// <summary>
    /// 로비 서버에 닿는가 — 로그인보다 먼저 묻는다(서버가 꺼졌는데 "로그인 실패"로 보이지 않게).
    /// 상태 확인 주소가 따로 없어 로비 주소에 짧게 묻고, 404라도 <b>답이 오면</b> 살아 있는 것으로 본다.
    /// </summary>
    public class ServerReachability
    {
        private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

        private readonly HttpClient client;
        private readonly Func<string> baseUrl;

        public ServerReachability() : this(new UnityWebRequestHandler(), () => EnvironmentSettings.active.lobbyBaseURL)
        {
        }

        public ServerReachability(HttpMessageHandler handler, Func<string> baseUrl)
        {
            client = new HttpClient(handler) { Timeout = ProbeTimeout };
            this.baseUrl = baseUrl;
        }

        public async UniTask<bool> IsReachableAsync()
        {
            try
            {
                await client.SendAsync(HttpRequestMessage.Get(baseUrl()));
                return true;
            }
            catch (Exception exception) when (ConnectionFailure.Is(exception))
            {
                UnityEngine.Debug.LogWarning($"[Server] 로비 서버에 닿지 못했다: {exception.Message}");
                return false;
            }
        }
    }
}
