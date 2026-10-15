using System.Threading;
using Cysharp.Threading.Tasks;

namespace LOP
{
    /// <summary>방 접속과 출전 연출을 함께 시작해 둘 다 끝나야 들어간다. 접속이 실패하면 연출을 기다리지 않고 바로, 던지면 그 자리에서 되돌린다.</summary>
    public static class RunOutEntry
    {
        public static async UniTask<bool> EnterAsync(UniTask<bool> connect, IRunOutDirector director, CancellationToken ct)
        {
            var play = director.PlayAsync(ct);
            try
            {
                if (!await connect)
                {
                    //  끝까지 달려 하얀 빛·로딩판까지 보여 준 뒤 되돌리면, 다시 시도할 때마다 깜박인다 — 바로 되돌린다.
                    //  달리던 루프는 되돌리면(재생 중 아님) 다음 프레임에 스스로 끝난다.
                    director.Reset();
                    return false;
                }
                await play;
                return true;
            }
            catch
            {
                director.Reset();
                throw;
            }
        }
    }
}
