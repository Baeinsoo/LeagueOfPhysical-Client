using UnityEngine;

/// <summary>측면 카메라가 플레이어의 X를 따라가게 한다(횡스크롤). followY면 높이도 부드럽게 추적(고도 맵용).</summary>
public class FlappyCameraFollow : MonoBehaviour
{
    public Transform target;
    public float offsetX = 8f;
    public float fixedY = 1f;
    public float fixedZ = -30f;
    public float lerp = 6f;

    [Header("선두 추종 — 추격자가 뒤를 자르므로 카메라는 선두를 잡아야 전원이 화면에 담긴다")]
    public bool followLeader = true;
    public float leaderOffsetX = -14f;   // 선두를 화면 오른쪽에 두려고 카메라를 그만큼 왼쪽으로 당김

    [Header("세로 추적 (고도 맵)")]
    public bool followY = true;
    public float yLerp = 2f;      // 느리게 = 플랩 흔들림 무시하고 지형 높이를 따라감

    float shakeAmp, shakeT, shakeDur;

    /// <summary>대시 등에서 호출 — dur초 동안 진폭 amp로 화면을 흔든다(시간이 갈수록 감쇠).</summary>
    public void Shake(float amp, float dur) { shakeAmp = amp; shakeDur = Mathf.Max(0.01f, dur); shakeT = shakeDur; }

    void LateUpdate()
    {
        if (target == null) return;
        var p = transform.position;
        float focusX = followLeader ? LeaderX() : target.position.x;
        float wantX = followLeader ? focusX + leaderOffsetX : focusX + offsetX;
        p.x = Mathf.Lerp(p.x, wantX, Time.deltaTime * lerp);
        p.y = followY ? Mathf.Lerp(p.y, target.position.y, Time.deltaTime * yLerp) : fixedY;
        p.z = fixedZ;

        // 흔들림은 팔로우로 정한 기준 위치에 오프셋만 얹는다(팔로우가 매 프레임 기준을 다시 잡으므로 누적 안 됨)
        if (shakeT > 0f)
        {
            shakeT -= Time.deltaTime;
            float k = Mathf.Clamp01(shakeT / shakeDur);   // 남은 비율 = 감쇠
            Vector2 o = Random.insideUnitCircle * (shakeAmp * k);
            p.x += o.x; p.y += o.y;
        }
        transform.position = p;
    }

    // 선두 = 활성 레이서 중 가장 앞선 X. 아무도 없으면 target으로 되돌아간다.
    float LeaderX()
    {
        float best = float.NegativeInfinity;
        foreach (var b in FlappyBird.All)
            if (b != null && b.transform.position.x > best) best = b.transform.position.x;
        return float.IsNegativeInfinity(best) ? target.position.x : best;
    }
}
