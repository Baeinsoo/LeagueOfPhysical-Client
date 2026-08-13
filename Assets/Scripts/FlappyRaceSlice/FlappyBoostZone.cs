using UnityEngine;

/// <summary>
/// 속도 보상 존 — 통과하면 플레이어에게 일시 전진 부스트(지형 실력 보상 ①).
/// 장애물 아님(FlappyObstacle 없음). 트리거 콜라이더 필요.
/// </summary>
public class FlappyBoostZone : MonoBehaviour
{
    public float mult = 1.8f;
    public float duration = 1.6f;

    void OnTriggerEnter(Collider other)
    {
        var p = other.GetComponentInParent<FlappyPlayer>();
        if (p != null) p.AddBoost(mult, duration);
    }
}
