using UnityEngine;

/// <summary>
/// 실력기반 분기 지형 프로토타입 — 갈림길(위=어려움+부스트 / 아래=쉬움+통행료) → 병합 → 결승.
///  ① 속도 보상: 위 좁은 터널 출구의 부스트 존(통과 시 전진 배속) → 이후 앞서감.
///  ② 충돌 통행료: 아래 넓은 레인 중간의 좁은 관문(평범하면 유령정지 1회=시간 손실 확정).
/// 수평속도 고정이라 "거리 지름길"은 불가 → 보상=속도/충돌회피로 실력 차등. [ContextMenu]로 빌드.
/// </summary>
public class FlappyBranchDemo : MonoBehaviour
{
    public Material pipeMat;    // 장애물 재질(Pipe.mat)
    public Material boostMat;   // 부스트 존 재질(초록)
    public float boostMult = 1.8f;
    public float boostDur = 1.6f;

    const float Z = 2f;

    [ContextMenu("Build Branch Demo")]
    public void Build()
    {
        Clear();

        // 플레이어: 고도 끄고 바깥 경계 넓게, 스폰 중앙
        var player = FindObjectOfType<FlappyPlayer>();
        if (player != null)
        {
            player.elevationFloor = false;
            player.groundY = -13f; player.ceilingY = 10f;
            player.transform.position = new Vector3(-3f, 0f, 0f);
        }

        float fx = 14f, mx = 42f;   // 갈림 시작 / 병합

        // 중앙 분리대 (위/아래 레인 가름) — 이 위/아래 중 하나를 골라야 함
        Box("Divider", (fx + mx) / 2f, 0f, mx - fx, 1.8f, true);

        // ===== 위 레인(어려움): 대체로 열린 통로(편한 비행) + 스킬 게이트 1곳(mouth5.0) + 부스트 =====
        // 긴 좁은 터널(지속 정밀=피곤) 대신, 한 번의 정밀 통과가 스킬 체크. 나머진 넉넉.
        Box("UpCeil", (fx + mx) / 2f, 12.9f, mx - fx, 8f, true);        // 천장 바닥 y8.9 → 통로 mouth 8.0(열림)
        Box("GateBottom", 28f, 1.65f, 2f, 1.5f, true);                 // 스킬 게이트 아래(y0.9~2.4)
        Box("GateTop", 28f, 8.15f, 2f, 1.5f, true);                    // 스킬 게이트 위(y7.4~8.9) → 틈 y2.4~7.4 mouth5.0
        Boost("BoostZone", 39.5f, 4.9f, 2.5f, 8.0f);                   // 출구 부스트(위 실력 보상 ①)

        // ===== 아래 레인(쉬움): 넓은 통로 + 통행료 관문 =====
        Box("DownFloor", (fx + mx) / 2f, -11f, mx - fx, 4f, true);      // 바닥(y-13~-9), 통로 넓음
        // 통행료 관문(x27): 통로 중앙에 좁은 틈(약 4.0) — 평범하면 스침 ②
        Box("TollBottom", 27f, -7.975f, 2f, 2.05f, true);              // 아래 채움 (~-9..-6.95)
        Box("TollTop", 27f, -1.925f, 2f, 2.05f, true);                 // 위 채움 (~-2.95..-0.9)

        // ===== 결승 게이트(넓음, 표식) — 병합 후 부스트가 값하는 구간 =====
        Box("FinishTop", 57f, 11f, 1.5f, 6f, true);                    // y8~14
        Box("FinishBottom", 57f, -10f, 1.5f, 6f, true);                // y-13~-7 (틈 y-7~8 매우 넓음)
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }
    }

    void Box(string name, float cx, float cy, float w, float h, bool solid)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(cx, cy, 0f);
        go.transform.localScale = new Vector3(w, h, Z);
        if (pipeMat != null) go.GetComponent<MeshRenderer>().sharedMaterial = pipeMat;
        var col = go.GetComponent<BoxCollider>();
        if (solid) { col.isTrigger = true; go.AddComponent<FlappyObstacle>(); }
        else col.enabled = false;
    }

    void Boost(string name, float cx, float cy, float w, float h)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(cx, cy, 0f);
        go.transform.localScale = new Vector3(w, h, Z);
        if (boostMat != null) go.GetComponent<MeshRenderer>().sharedMaterial = boostMat;
        var col = go.GetComponent<BoxCollider>();
        col.isTrigger = true;
        var bz = go.AddComponent<FlappyBoostZone>();
        bz.mult = boostMult; bz.duration = boostDur;
    }
}
