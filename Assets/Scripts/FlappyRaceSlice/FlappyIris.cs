using UnityEngine;

/// <summary>
/// 동적 장애물 — 위/아래 파이프가 중심 기준 대칭으로 벌어졌다 좁혀지는 조리개. 틈 크기가 sin 파형으로 개폐.
/// 두 파이프의 mouth(입구)를 ±gap/2 로 옮긴다. 지오메트리는 FlappyCourseGenerator가 만들어 topPipe/bottomPipe에 연결.
/// 통과보장: 최소 구경(baseGap − amp)을 통과 가능 크기 이상으로 유지 → 치명적으로 닫히지 않음(공정).
/// </summary>
public class FlappyIris : MonoBehaviour
{
    public Transform topPipe;
    public Transform bottomPipe;
    public float baseGap = 5f;
    public float amp = 2.2f;
    public float speed = 1.4f;
    public float phase = 0f;

    void Update()
    {
        float gap = baseGap + amp * Mathf.Sin(Time.time * speed + phase);
        float half = gap * 0.5f;
        if (topPipe != null)
        {
            var t = topPipe.localPosition; t.y = half; topPipe.localPosition = t;
        }
        if (bottomPipe != null)
        {
            var b = bottomPipe.localPosition; b.y = -half; bottomPipe.localPosition = b;
        }
    }
}
