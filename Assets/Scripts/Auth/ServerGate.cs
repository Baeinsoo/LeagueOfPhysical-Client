using Cysharp.Threading.Tasks;
using LOP.UI;

namespace LOP
{
    /// <summary>서버에 닿을 때까지 기다린다 — 닿으면 바로 지나가고, 아니면 "연결할 수 없습니다" 팝업을 띄워 [다시 시도]로 닿을 때까지 막는다.</summary>
    public class ServerGate
    {
        private readonly ServerReachability reachability;
        private readonly IWindowManager windowManager;

        public ServerGate(ServerReachability reachability, IWindowManager windowManager)
        {
            this.reachability = reachability;
            this.windowManager = windowManager;
        }

        public async UniTask EnsureReachableAsync()
        {
            if (await reachability.IsReachableAsync())
            {
                return;
            }
            await windowManager.OpenModalAsync<ServerUnreachableView, bool>();
        }
    }
}
