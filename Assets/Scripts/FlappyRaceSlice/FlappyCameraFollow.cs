using UnityEngine;

/// <summary>측면 카메라가 플레이어의 X를 따라가게 한다(횡스크롤). followY면 높이도 부드럽게 추적(고도 맵용).</summary>
public class FlappyCameraFollow : MonoBehaviour
{
    public Transform target;
    public float offsetX = 8f;
    public float fixedY = 1f;
    public float fixedZ = -30f;
    public float lerp = 6f;

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
        p.x = Mathf.Lerp(p.x, target.position.x + offsetX, Time.deltaTime * lerp);
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
}
