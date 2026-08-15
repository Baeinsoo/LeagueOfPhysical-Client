using UnityEngine;

/// <summary>
/// 낙마 연출 — 라이더가 탈것에서 튕겨 나가 빙글빙글 날아갔다 돌아온다.
/// 탈것은 제자리에서 태연하게 떠 있는다. 이 대비가 비급 감성의 자리다.
/// 라이더 아트가 아직 없어 지금은 탈것 스프라이트를 축소 복제해 자리표시로 쓴다.
/// </summary>
public class FlappyDismountFx : MonoBehaviour
{
    public float flyDuration = 0.45f;     // 튕겨 나가는 시간
    public Vector2 flyVelocity = new Vector2(-3f, 9f);
    public float flyGravity = 22f;
    public float spinSpeed = 900f;
    public float riderScale = 0.55f;

    SpriteRenderer source;
    Transform rider;
    SpriteRenderer riderSr;
    float t = -1f;
    Vector2 vel;

    /// <summary>연출이 진행 중인지 — 테스트·검증에서 확인용.</summary>
    public bool Playing => t >= 0f;

    void Awake()
    {
        source = GetComponentInChildren<SpriteRenderer>();

        var p = GetComponent<FlappyPlayer>();
        if (p != null) p.Dismounted += Play;
        var b = GetComponent<FlappyPacer>();
        if (b != null) b.Dismounted += Play;
    }

    void Play()
    {
        if (source == null) return;
        EnsureRider();
        rider.position = transform.position;
        rider.localScale = Vector3.one * riderScale;
        rider.gameObject.SetActive(true);
        vel = flyVelocity;
        t = 0f;
    }

    void EnsureRider()
    {
        if (rider != null) return;
        var go = new GameObject("DismountRider");
        riderSr = go.AddComponent<SpriteRenderer>();
        riderSr.sprite = source.sprite;
        riderSr.color = new Color(1f, 0.9f, 0.45f, 1f);   // 자리표시 — Plan 2에서 라이더 아트로 교체
        riderSr.sortingOrder = source.sortingOrder + 1;
        rider = go.transform;
    }

    void Update()
    {
        if (t < 0f || rider == null) return;
        float dt = Time.deltaTime;
        t += dt;

        if (t < flyDuration)
        {
            vel.y -= flyGravity * dt;
            rider.position += (Vector3)(vel * dt);
            rider.Rotate(0f, 0f, spinSpeed * dt);
        }
        else
        {
            // 탈것이 라이더를 낚아채 복귀
            float k = Mathf.Clamp01((t - flyDuration) / 0.2f);
            rider.position = Vector3.Lerp(rider.position, transform.position, k);
            rider.rotation = Quaternion.Slerp(rider.rotation, Quaternion.identity, k);
            if (k >= 1f) { rider.gameObject.SetActive(false); t = -1f; }
        }
    }
}
