using System.Threading.Tasks;

namespace LOP
{
    /// <summary>진입 첫 단계 — 로그인보다 먼저 서버에 닿는지 본다. 서버가 꺼져 있으면 "로그인 실패"가 아니라 "연결할 수 없습니다"를 보여 준다.</summary>
    public class ServerCheckComponent : IEntranceComponent
    {
        private readonly ServerGate gate;

        public ServerCheckComponent(ServerGate gate)
        {
            this.gate = gate;
        }

        public async Task Execute() => await gate.EnsureReachableAsync();
    }
}
