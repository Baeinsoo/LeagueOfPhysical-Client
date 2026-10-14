using System.Threading;
using Cysharp.Threading.Tasks;

namespace LOP
{
    /// <summary>방 접속과 출전 연출을 함께 시작해 둘 다 끝나야 들어간다. 실패하거나 던지면 연출을 되돌린다.</summary>
    public static class RunOutEntry
    {
        public static async UniTask<bool> EnterAsync(UniTask<bool> connect, IRunOutDirector director, CancellationToken ct)
        {
            var play = director.PlayAsync(ct);
            bool ok;
            try
            {
                ok = await connect;
                await play;
            }
            catch
            {
                director.Reset();
                throw;
            }
            if (!ok) director.Reset();
            return ok;
        }
    }
}
