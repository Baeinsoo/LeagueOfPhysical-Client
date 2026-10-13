using System.Collections.Generic;
using System.Threading.Tasks;

namespace LOP
{
    /// <summary>부팅 단계를 차례로 돌며 진행을 알린다. 단계가 예외로 끝나면 Complete 없이 그대로 던진다 — 막대가 가득 차 성공처럼 보이지 않게.</summary>
    public static class EntranceRunner
    {
        public static async Task RunAsync(IReadOnlyList<IEntranceComponent> components, EntranceProgress progress)
        {
            for (int i = 0; i < components.Count; i++)
            {
                progress.Begin(i, components.Count, components[i].StepLabel);
                await components[i].Execute();
            }

            progress.Complete();
        }
    }
}
