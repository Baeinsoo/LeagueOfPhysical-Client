using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FlappyRace 코스 조립기 — 인스펙터 구간 리스트를 읽어 파이프/동적 장애물을 절차 생성한다.
/// ContextMenu의 Generate/Clear로 반복 튜닝. 일회성 execute_code 생성을 대체.
/// 고도(elevation): 코스 전체에 걸쳐 통로 밴드가 완만히 오르내리고 지형 바닥이 따라옴(맵 느낌). 카메라는 세로도 추적.
/// 통과보장: 연속 틈 중심(=고도+국소변동)의 이동량을 제한 → 타이밍만 맞으면 항상 통과 가능.
/// </summary>
public class FlappyCourseGenerator : MonoBehaviour
{
    public enum SectionType { WideGap, TightGap, MultiSlot, Dynamic }
    public enum DynamicKind { MovingPipe, Donut, Iris }

    [System.Serializable]
    public class Section
    {
        public SectionType type = SectionType.WideGap;
        public int count = 3;
        public DynamicKind dynamicKind = DynamicKind.MovingPipe; // type==Dynamic일 때만 의미
    }

    [Header("배치")]
    public Transform courseRoot;       // 비우면 이 오브젝트 자신을 루트로
    public float startX = 6f;
    public float wallSpacing = 14f;
    public int seed = 12345;

    [Header("고도 (맵처럼 오르내림)")]
    public bool enableElevation = true;
    public float elevationAmp = 7f;      // 고도 진폭(유닛)
    public float elevationWavelength = 120f; // 오르내림 한 주기의 X 길이(유닛) — 클수록 완만
    public bool sharpElevation = false;  // true=삼각파(뾰족한 V·W 급격), false=사인(완만한 언덕)
    public float localGapHalf = 4f;      // 베이스라인 주변 국소 틈 변동 반경
    public float pipeExtent = 26f;       // 파이프가 틈 중심에서 위/아래로 뻗는 길이(화면 밖까지)
    public float groundDrop = 9f;        // 베이스라인 아래 지형 바닥까지 거리 (최저 틈 바로 아래)
    public float ceilRise = 9f;          // 베이스라인 위 천장까지 거리 (위로 못 넘어감)

    [Header("틈 크기")]
    public float wideGapSize = 7.5f;   // 넓은 틈 (4인 몸싸움용 여유)
    public float tightGapSize = 3.8f;  // 좁은 단일-틈 (솔로 도전)
    public int multiSlotCount = 3;     // 슬롯(구멍) 수
    public float slotGapSize = 4.0f;   // 슬롯 하나 높이
    public float slotSpan = 12f;       // 슬롯들이 퍼지는 세로 폭(국소)

    [Header("통과보장 (플레이어 값과 일치시킬 것)")]
    public float flapImpulse = 23f;
    public float forwardSpeed = 10.5f;
    public float reachFactor = 0.3f;   // 국소 변동 걸음 크기(작을수록 완만)

    [Header("파이프 비주얼")]
    public Material pipeMaterial;      // Pipe.mat 할당
    public float pipeWidth = 1.6f;     // X 두께
    public float pipeDepth = 2f;       // Z 두께
    public float lipExtra = 0.4f;      // 립이 몸통보다 넓은 정도
    public float lipHeight = 0.5f;

    [Header("동적 장애물")]
    public float movingPipeGap = 8f;
    public float movingPipeAmp = 2.0f;
    public float movingPipeSpeed = 1.2f;
    public float donutRadius = 3.4f;   // 스택 링 하나의 반지름
    public float donutRotSpeed = 25f;
    public int donutGapCount = 3;      // 링 둘레에 균등 배치되는 빈틈 개수
    public int donutStackCount = 2;    // 세로로 쌓는 링 개수 (위/아래 2개 — 중간은 통로)
    public float donutStackOffset = 6f;   // 맨위~맨아래 링 중심의 절반 폭
    public float irisBaseGap = 7f;
    public float irisAmp = 3.0f;
    public float irisSpeed = 1.0f;

    [Header("코스 시퀀스")]
    public List<Section> sections = new List<Section>()
    {
        new Section { type = SectionType.WideGap,  count = 2 },   // 쉬운 인트로
        new Section { type = SectionType.TightGap, count = 3 },   // 솔로 도전
        new Section { type = SectionType.MultiSlot, count = 2 },
        new Section { type = SectionType.Dynamic,  count = 2, dynamicKind = DynamicKind.MovingPipe },
        new Section { type = SectionType.TightGap, count = 3 },
        new Section { type = SectionType.Dynamic,  count = 2, dynamicKind = DynamicKind.Donut },
        new Section { type = SectionType.WideGap,  count = 2 },   // 멀티용 숨돌리기
        new Section { type = SectionType.TightGap, count = 2 },
        new Section { type = SectionType.Dynamic,  count = 2, dynamicKind = DynamicKind.Iris },
        new Section { type = SectionType.WideGap,  count = 2 },   // 결승
    };

    /// <summary>연속 틈이 세로로 움직일 수 있는 최대치 = (플랩힘/2)×(간격/전진속도)×마진.</summary>
    public float MaxGapDelta => (flapImpulse * 0.5f) * (wallSpacing / forwardSpeed) * reachFactor;

    Transform Root => courseRoot != null ? courseRoot : transform;

    [ContextMenu("Generate Course")]
    public void Generate()
    {
        Clear();
        Random.InitState(seed);
        var root = Root;

        // 스폰~시작 구간에도 지형/천장을 깔아 새가 허공으로 떨어지거나 위로 새지 않게 (베이스라인 0)
        if (enableElevation)
            for (int lead = 1; lead <= 2; lead++) { BuildGround(root, startX - lead * wallSpacing, 0f); BuildCeiling(root, startX - lead * wallSpacing, 0f); }

        // 플레이어 바닥/천장이 지형 고도를 따라가도록 파라미터 동기화(단일 진실원본=생성기)
        var player = FindObjectOfType<FlappyPlayer>();
        if (player != null)
        {
            player.elevationFloor = enableElevation;
            player.elevAmp = elevationAmp;
            player.elevWavelength = elevationWavelength;
            player.elevStartX = startX;
            player.floorDrop = groundDrop;
            player.ceilRise = ceilRise;
            player.elevSharp = sharpElevation;
        }

        float x = startX;
        float localCenter = 0f;
        float slotOffset = 0f;

        foreach (var s in sections)
        {
            for (int c = 0; c < Mathf.Max(0, s.count); c++)
            {
                float baseY = enableElevation
                    ? FlappyElevation.Value(x, elevationAmp, startX, elevationWavelength, sharpElevation)
                    : 0f;
                localCenter = Mathf.Clamp(localCenter + RandStep(), -localGapHalf, localGapHalf);

                switch (s.type)
                {
                    case SectionType.WideGap:
                        BuildGapWall(root, x, baseY, localCenter, wideGapSize, "WideGap");
                        break;
                    case SectionType.TightGap:
                        BuildGapWall(root, x, baseY, localCenter, tightGapSize, "TightGap");
                        break;
                    case SectionType.MultiSlot:
                        slotOffset = Mathf.Clamp(slotOffset + RandStep(), -2f, 2f);
                        BuildMultiSlotWall(root, x, baseY, slotOffset);
                        break;
                    case SectionType.Dynamic:
                        BuildDynamic(root, x, baseY, localCenter, s.dynamicKind);
                        break;
                }

                if (enableElevation) { BuildGround(root, x, baseY); BuildCeiling(root, x, baseY); }

                x += wallSpacing;
            }
        }
    }

    [ContextMenu("Clear Course")]
    public void Clear()
    {
        var root = Root;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var c = root.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }
    }

    float RandStep() => Random.Range(-MaxGapDelta, MaxGapDelta);

    // --- 정적 구간 (컨테이너를 고도 baseY에 놓고, 내부는 국소 기준으로 생성) ---

    void BuildGapWall(Transform parent, float x, float baseY, float localCenter, float gapSize, string name)
    {
        var wall = NewChild(parent, name, new Vector3(x, baseY, 0f));
        float topMouth = localCenter + gapSize * 0.5f;
        float bottomMouth = localCenter - gapSize * 0.5f;
        BuildPipe(wall, "Top", new Vector3(0f, topMouth, 0f), pipeExtent - topMouth, true);
        BuildPipe(wall, "Bottom", new Vector3(0f, bottomMouth, 0f), bottomMouth + pipeExtent, false);
    }

    void BuildMultiSlotWall(Transform parent, float x, float baseY, float offset)
    {
        var wall = NewChild(parent, "MultiSlot", new Vector3(x, baseY, 0f));
        float half = slotSpan * 0.5f;
        float lowerEdge = -pipeExtent;
        for (int i = 0; i < multiSlotCount; i++)
        {
            float center = -half + (i + 0.5f) * (slotSpan / multiSlotCount) + offset;
            BuildSegment(wall, lowerEdge, center - slotGapSize * 0.5f);
            lowerEdge = center + slotGapSize * 0.5f;
        }
        BuildSegment(wall, lowerEdge, pipeExtent);
    }

    // 세로 [fromY, toY] 구간(국소)을 메우는 solid 세그먼트 + 양 끝(슬롯 대면) 립.
    void BuildSegment(Transform wall, float fromY, float toY)
    {
        if (toY - fromY <= 0.05f) return;
        float center = (fromY + toY) * 0.5f;
        float len = toY - fromY;
        MakeBox(wall, "Seg", new Vector3(0f, center, 0f), new Vector3(pipeWidth, len, pipeDepth));
        MakeBox(wall, "LipTop", new Vector3(0f, toY, 0f), LipSize());
        MakeBox(wall, "LipBottom", new Vector3(0f, fromY, 0f), LipSize());
    }

    // 지형 바닥 — 베이스라인 아래로 groundDrop 만큼, 벽 간격만큼 넓게(연속 바닥). 충돌 시 유령.
    void BuildGround(Transform parent, float x, float baseY)
    {
        var g = NewChild(parent, "Ground", new Vector3(x, baseY - groundDrop - 3f, 0f));
        MakeBox(g, "GroundBox", Vector3.zero, new Vector3(wallSpacing + 0.5f, 6f, pipeDepth + 2f), false);
    }

    // 시각 천장 — 베이스라인 위 ceilRise. 바닥과 대칭(시각 전용, 실제 막음은 플레이어 CeilAt 클램프).
    void BuildCeiling(Transform parent, float x, float baseY)
    {
        var g = NewChild(parent, "Ceiling", new Vector3(x, baseY + ceilRise + 3f, 0f));
        MakeBox(g, "CeilBox", Vector3.zero, new Vector3(wallSpacing + 0.5f, 6f, pipeDepth + 2f), false);
    }

    // --- 동적 구간 ---

    void BuildDynamic(Transform parent, float x, float baseY, float localCenter, DynamicKind kind)
    {
        switch (kind)
        {
            case DynamicKind.MovingPipe: BuildMovingPipe(parent, x, baseY + localCenter); break;
            case DynamicKind.Donut: BuildDonut(parent, x, baseY); break;
            case DynamicKind.Iris: BuildIris(parent, x, baseY + localCenter); break;
        }
    }

    void BuildMovingPipe(Transform parent, float x, float centerY)
    {
        var go = NewChild(parent, "MovingPipe", new Vector3(x, centerY, 0f));
        float half = movingPipeGap * 0.5f;
        BuildPipe(go, "Top", new Vector3(0f, half, 0f), pipeExtent, true);
        BuildPipe(go, "Bottom", new Vector3(0f, -half, 0f), pipeExtent, false);
        var mp = go.gameObject.AddComponent<FlappyMovingPipe>();
        mp.amplitude = movingPipeAmp;
        mp.speed = movingPipeSpeed;
    }

    void BuildIris(Transform parent, float x, float centerY)
    {
        var go = NewChild(parent, "Iris", new Vector3(x, centerY, 0f));
        var top = BuildPipe(go, "Top", new Vector3(0f, irisBaseGap * 0.5f, 0f), pipeExtent, true);
        var bottom = BuildPipe(go, "Bottom", new Vector3(0f, -irisBaseGap * 0.5f, 0f), pipeExtent, false);
        var iris = go.gameObject.AddComponent<FlappyIris>();
        iris.topPipe = top;
        iris.bottomPipe = bottom;
        iris.baseGap = irisBaseGap;
        iris.amp = irisAmp;
        iris.speed = irisSpeed;
    }

    void BuildDonut(Transform parent, float x, float centerY)
    {
        var root = NewChild(parent, "Donut", new Vector3(x, centerY, 0f));
        int stacks = Mathf.Max(1, donutStackCount);
        for (int s = 0; s < stacks; s++)
        {
            float oy = stacks == 1 ? 0f : Mathf.Lerp(-donutStackOffset, donutStackOffset, (float)s / (stacks - 1));
            BuildDonutRing(root, oy);
        }
    }

    void BuildDonutRing(Transform root, float offsetY)
    {
        var go = NewChild(root, "Ring", new Vector3(0f, offsetY, 0f));
        const int segs = 18;
        const int gapWidthSegs = 2;
        float bandThickness = 1.2f;
        float segLen = (2f * Mathf.PI * donutRadius / segs) * 1.2f;

        int gaps = Mathf.Max(1, donutGapCount);
        var skip = new HashSet<int>();
        for (int g = 0; g < gaps; g++)
        {
            int start = Mathf.RoundToInt((float)g / gaps * segs);
            for (int w = 0; w < gapWidthSegs; w++) skip.Add((start + w) % segs);
        }

        for (int i = 0; i < segs; i++)
        {
            if (skip.Contains(i)) continue;
            float ang = (360f / segs) * i;
            float rad = ang * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * donutRadius;
            var seg = MakeBox(go, "Seg", pos, new Vector3(bandThickness, segLen, pipeDepth));
            seg.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
        }
        go.gameObject.AddComponent<FlappyRotatingDonut>().rotSpeed = donutRotSpeed;
    }

    // --- 프리미티브 헬퍼 ---

    Transform BuildPipe(Transform parent, string name, Vector3 mouthLocalPos, float length, bool up)
    {
        var pipe = NewChild(parent, name, mouthLocalPos);
        float dir = up ? 1f : -1f;
        float len = Mathf.Max(0.1f, length);
        MakeBox(pipe, "Body", new Vector3(0f, dir * len * 0.5f, 0f), new Vector3(pipeWidth, len, pipeDepth));
        MakeBox(pipe, "Lip", Vector3.zero, LipSize());
        return pipe;
    }

    Vector3 LipSize() => new Vector3(pipeWidth + lipExtra, lipHeight, pipeDepth + lipExtra);

    Transform NewChild(Transform parent, string name, Vector3 localPos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        return t;
    }

    GameObject MakeBox(Transform parent, string name, Vector3 localPos, Vector3 size, bool solid = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        if (pipeMaterial != null) go.GetComponent<MeshRenderer>().sharedMaterial = pipeMaterial;
        var col = go.GetComponent<BoxCollider>();
        if (solid) { col.isTrigger = true; go.AddComponent<FlappyObstacle>(); } // 충돌=유령
        else col.enabled = false;                                              // 지형=시각 전용(바닥은 플레이어 클램프)
        return go;
    }
}
