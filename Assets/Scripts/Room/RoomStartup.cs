using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace LOP
{
    /// <summary>경기 씬 시작·정리에서 쓰는 규칙. 다시 띄울 때는 옛 씬의 정리와 새 씬의 시작이 겹친다.</summary>
    public static class RoomStartup
    {
        public const double ConnectTimeoutSeconds = 10;
        public const double GameInfoTimeoutSeconds = 10;

        /// <summary>done이 참이 될 때까지 기다린다. 시간을 넘기면 TimeoutException — 로딩판에서 영원히 멈추지 않게.</summary>
        public static async UniTask WaitOrThrowAsync(Func<bool> done, double seconds, string what, CancellationToken ct)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (done() == false)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                {
                    throw new TimeoutException($"{what} 대기 {seconds}초 초과");
                }
                await UniTask.Yield(ct);
            }
        }

        /// <summary>옛 씬이 늦게 정리할 때, 다음 씬이 방 정보를 새로 채웠으면 건드리지 않는다.</summary>
        public static bool ShouldClearRoomStore(Room ownedAtStart, Room current)
        {
            return current != null && ReferenceEquals(ownedAtStart, current);
        }
    }
}
