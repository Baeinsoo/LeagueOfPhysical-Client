using Cysharp.Threading.Tasks;
using GameFramework;
using R3;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LOP
{
    public class InMatchmaking : State<MatchEvent>
    {
        private readonly Func<CancelMatchmaking> cancelMatchmaking;
        private readonly Func<InGameRoom> inGameRoom;
        private readonly Func<Idle> idle;
        private readonly IUserLocationService userLocationService;

        public InMatchmaking(Func<CancelMatchmaking> cancelMatchmaking, Func<InGameRoom> inGameRoom, Func<Idle> idle, IUserLocationService userLocationService)
        {
            this.cancelMatchmaking = cancelMatchmaking;
            this.inGameRoom = inGameRoom;
            this.idle = idle;
            this.userLocationService = userLocationService;
        }

        public override IState<MatchEvent> GetNextState(MatchEvent ev)
        {
            return ev switch
            {
                MatchEvent.CancelClicked => cancelMatchmaking(),
                MatchEvent.LocationIsGameRoom => inGameRoom(),
                MatchEvent.LocationIsNone => idle(),
                _ => this,
            };
        }

        protected override async Task<MatchEvent?> OnExecuteAsync(CancellationToken ct)
        {
            //  폴링은 서비스가 돈다. 여기서는 위치가 매칭을 벗어나는 순간만 기다린다.
            //  구독하면 현재 값부터 흘러오므로, 진입 시점에 이미 벗어나 있으면 즉시 전이한다.
            var completion = new UniTaskCompletionSource<MatchEvent>();

            using var cancellation = ct.Register(() => completion.TrySetCanceled());

            using var locationSubscription = userLocationService.UserLocation.Subscribe(userLocation =>
            {
                switch (userLocation.location)
                {
                    case Location.GameRoom:
                        completion.TrySetResult(MatchEvent.LocationIsGameRoom);
                        break;

                    case Location.Matchmaking:
                        break;   //  아직 대기 중.

                    default:
                        completion.TrySetResult(MatchEvent.LocationIsNone);
                        break;
                }
            });

            //  서비스가 조회를 포기했으면 위치를 더는 못 믿으므로 초기 화면으로.
            using var faultedSubscription = userLocationService.Faulted.Subscribe(_ =>
            {
                completion.TrySetResult(MatchEvent.LocationIsNone);
            });

            return await completion.Task;
        }

        protected override MatchEvent? OnError(Exception e)
        {
            Debug.LogError($"Unexpected error while waiting. Error: {e.Message}");
            return MatchEvent.LocationIsNone;
        }
    }
}
