using UnityEngine;

/// <summary>
/// 동적 장애물 — 둘레에 빈틈 하나가 있는 링(도넛)이 화면 축(Z) 기준으로 회전.
/// 링 세그먼트(장애물)는 FlappyCourseGenerator가 자식으로 붙인다.
/// 통과보장: 링 중앙 구멍은 항상 열려 있어(회전과 무관) 타이밍만 맞추면 스레딩 통과 가능 — 공정.
/// </summary>
public class FlappyRotatingDonut : MonoBehaviour
{
    public float rotSpeed = 40f;   // deg/s

    void Update() => transform.Rotate(0f, 0f, rotSpeed * Time.deltaTime, Space.Self);
}
