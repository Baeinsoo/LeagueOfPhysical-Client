using UnityEngine;

/// <summary>
/// 동적 장애물 — 위/아래 파이프쌍(틈 크기 고정)이 세로로 왕복.
/// 부모 오브젝트를 baseY 기준 sin 파형으로 움직인다. 파이프 지오메트리는 FlappyCourseGenerator가 자식으로 붙인다.
/// 통과보장: 틈 크기는 항상 통과 가능(고정), amplitude가 플레이 세로범위를 벗어나지 않게 튜닝.
/// </summary>
public class FlappyMovingPipe : MonoBehaviour
{
    public float amplitude = 3f;   // 왕복 진폭(유닛)
    public float speed = 1.2f;     // 각속도(rad/s)
    public float phase = 0f;       // 시작 위상 — 여러 개가 엇박으로 움직이게

    float baseY;

    void Start() => baseY = transform.position.y;

    void Update()
    {
        var p = transform.position;
        p.y = baseY + amplitude * Mathf.Sin(Time.time * speed + phase);
        transform.position = p;
    }
}
