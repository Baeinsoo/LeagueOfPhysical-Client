using System.Threading.Tasks;

namespace LOP
{
    public interface IEntranceComponent
    {
        /// <summary>타이틀 진행 막대 아래에 보이는 글자(예: "서버 확인 중").</summary>
        string StepLabel { get; }

        Task Execute();
    }
}
