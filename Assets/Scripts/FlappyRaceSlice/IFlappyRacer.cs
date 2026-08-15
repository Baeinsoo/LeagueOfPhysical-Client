using UnityEngine;

/// <summary>
/// 플레이어와 봇이 충돌 처리에서 동일하게 다뤄지기 위한 공통 창구.
/// 새끼리 충돌 코드가 어느 쪽이 대시 중인지, 얼마나 빨리 다가왔는지 알아야 규칙을 가를 수 있다.
/// </summary>
public interface IFlappyRacer
{
    bool IsDashing { get; }
    Vector2 Velocity { get; }
    void Dismount();
    void AddKnockback(Vector2 impulse);
}
