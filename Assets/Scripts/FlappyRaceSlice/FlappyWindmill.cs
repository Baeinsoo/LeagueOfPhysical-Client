using UnityEngine;

/// <summary>
/// 동적 장애물 — 십자(풍차) 날개가 화면축(Z) 기준으로 회전. 도넛 대체.
/// 날개 지오메트리(십자 막대)는 생성기가 자식으로 붙이며, 각 막대는 트리거 콜라이더+FlappyObstacle라
/// 플레이어/페이서의 밀어내기(관통 방지)와 0.5s 유령 정지가 그대로 적용된다.
/// 통과보장: 코리도 절반 대비 날개 길이를 짧게 두면(중앙만 점유) 항상 바깥 밴드로 우회 가능 → 공정.
/// </summary>
public class FlappyWindmill : MonoBehaviour
{
    public float rotSpeed = 55f;   // deg/s (음수 = 반대 방향)

    void Update() => transform.Rotate(0f, 0f, rotSpeed * Time.deltaTime, Space.Self);
}
