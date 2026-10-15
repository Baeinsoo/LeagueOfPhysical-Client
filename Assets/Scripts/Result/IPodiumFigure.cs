using UnityEngine;

namespace LOP
{
    /// <summary>시상대에 서는 선수 하나. 결과 단계는 이 계약만 쓴다(시험은 가짜로 바꿔 끼운다).</summary>
    public interface IPodiumFigure
    {
        void Stand(Vector3 position, float yaw, float scale);
        void SetLook(PlayerLook look);
        void React(ResultReaction reaction);
        void SetAbsent(bool absent);
        bool HasBody { get; }
    }
}
