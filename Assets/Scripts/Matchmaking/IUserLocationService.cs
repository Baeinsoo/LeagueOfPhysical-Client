using Cysharp.Threading.Tasks;
using R3;
using System.Threading;

namespace LOP
{
    /// <summary>
    /// 유저 위치를 서버에 물어보는 유일한 곳. 폴링 루프·재시도 정책·매칭 티켓 id를 소유하고,
    /// 위치는 R3로 노출한다. 소비자(FSM 상태·UI)는 여기만 보고 WebAPI를 직접 부르지 않는다.
    /// </summary>
    public interface IUserLocationService
    {
        /// <summary>현재 위치 + 변화 알림. 구독하면 현재 값부터 흘러온다.</summary>
        ReadOnlyReactiveProperty<UserLocation> UserLocation { get; }

        /// <summary>매칭 대기 중이면 그 티켓 id, 아니면 null.</summary>
        string TicketId { get; }

        /// <summary>조회를 연속 실패해 폴링을 포기했다. 위치를 더는 못 믿는다는 신호.</summary>
        Observable<Unit> Faulted { get; }

        /// <summary>지금 한 번 조회한다(실패 시 재시도). 성공하면 true.</summary>
        UniTask<bool> RefreshAsync(CancellationToken ct = default);

        /// <summary>매칭 요청이 성공했을 때 응답으로 받은 티켓 id를 넘긴다. 폴링도 함께 시작된다.</summary>
        void OnMatchmakingRequested(string ticketId);
    }
}
