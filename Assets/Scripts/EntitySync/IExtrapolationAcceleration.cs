using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 외삽(마지막 스냅 이후를 이어 그리는 것)에 더할 가속도(중력 등)를 게임 스코프가 공급한다.
    /// <see cref="EntityBinder"/>는 여러 게임이 공유하므로 값의 정체(FlappyConfig.Gravity 등)를 몰라야
    /// 하고, 이 인터페이스만 알면 되게 한다.
    /// </summary>
    public interface IExtrapolationAcceleration
    {
        Vector3 Acceleration { get; }
    }
}
