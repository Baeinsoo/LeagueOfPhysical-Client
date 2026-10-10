using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 광산 코스 입히기 조절판(spec §7) — <c>Assets/Art/Settings/FlappyMineLook.asset</c>.
    /// 굽기(<c>FlappyMineDressing</c>, 다음 태스크)가 자리·분량을 읽고, 재질 값(<see cref="FlappyMineMaterials"/>)은
    /// 굽지 않고도 인스펙터에서 값을 고치고 <c>LOP/Debug/Flappy 광산 재질 갱신</c>만 다시 돌리면 바로 바뀐다.
    ///
    /// <para><b>에디터 전용</b>이다 — 굽기만 이 값을 읽고 런타임 조립은 이 에셋을 모른다. 그래서 Editor 폴더에 둔다
    /// (SkydivePyramidDressing 등 기존 꾸밈 스크립트와 같은 자리).</para>
    /// </summary>
    public sealed class FlappyMineLook : ScriptableObject
    {
        [Header("색 — global-constraints.md §색")]
        public Color plankColor = Hex("#c88a52");              // 판자(관문 기둥)
        public Color ironBandColor = Hex("#d9c7a4");            // 쇠테(관문 끝)
        public Color ironStrapColor = Hex("#5b4a3e");           // 쇠띠(관문 중간)
        public Color trestleColor = Hex("#b97e48");             // 비계(바닥·배경 나무 구조물)
        public Color rockColor = Hex("#7a5646");                // 바위(천장·아치)
        public Color rockBandColor = Hex("#e2b47c");            // 바위 띠(천장 아랫면 밝은 띠)
        public Color caveSilhouette1Color = Hex("#2b1b14");     // 굴 실루엣 — 가까운 겹
        public Color caveSilhouette2Color = Hex("#3a2430");     // 굴 실루엣 — 먼 겹
        public Color skySunsetTopColor = Hex("#f6b27a");        // 하늘 노을 — 위
        public Color skySunsetBottomColor = Hex("#e08a62");     // 하늘 노을 — 아래
        public Color canyonSilhouette1Color = Hex("#c9785e");   // 협곡 실루엣 — 가까운 겹
        public Color canyonSilhouette2Color = Hex("#a85f4e");   // 협곡 실루엣 — 먼 겹
        public Color lanternColor = Hex("#ffcf75");              // 랜턴(쇠틀 유리·빛 원판)

        [Header("안개 — 구역별(§5). '굴 배경'은 굴 안개와 같은 값이라 따로 두지 않는다")]
        public Color hazeCaveColor = Hex("#1c120e");             // 안개(굴 안) = 굴 배경 = Sky_Cave
        public Color hazeOutsideColor = Hex("#f2a878");          // 안개(노을 바깥)
        [Range(0f, 1f)] public float hazeAlpha = 0.4f;           // 안개 진하기(비교판 "중간") — 노을 바깥
        //  굴 안 안개 진하기. 유니티는 선형 공간에서 섞어서, 어두운 굴 안개 0.4는 시안(sRGB에서 섞은 0.4)보다 옅게 먹는다 —
        //  배경 틀이 관문만큼 밝았다(10-08 캡처). 굴 안만 올린다.
        [Range(0f, 1f)] public float hazeCaveAlpha = 0.65f;   // 10-10 Task 7 Fix 1: 0.55 → 0.65(배경이 관문 집중을 흐렸다)
        //  먼 안개 막(z 12) 진하기 — 먼 비계·실루엣은 안개를 두 겹 쓴다(10-08). 같은 까닭으로 어두운 굴 안개는
        //  0.4 한 겹 더로는 22%밖에 안 어두워졌고, 밝은 바깥 안개는 0.65면 협곡이 다 지워졌다 — 구역마다 따로 둔다.
        [Range(0f, 1f)] public float farHazeOutsideAlpha = 0.15f;
        [Range(0f, 1f)] public float farHazeCaveAlpha = 0.88f;   // 10-10 Task 7 Fix 1: 0.65 → 0.88

        [Header("나무결·그림자")]
        //  나무결 세기 — 텍스처 대비 조절. LOPToon.shader는 _BaseMap × _BaseColor를 그대로 곱할 뿐
        //  대비(contrast) 입력이 없어서, 텍스처 자체의 명암 폭(wood_grain/rock, 0.72~1.0배)은 코드로 못 줄인다.
        //  대신 "약하게"를 색으로 흉내 낸다: _BaseColor를 흰색 쪽으로 당기면(1 → 저장색, 0 → 흰색) 무늬가 더
        //  옅고 밝게 보인다(상대 대비 자체는 줄지 않지만 채도가 빠지며 "흐려진" 느낌을 준다 — Task 3 보고 우려 2 참고).
        //  기본 1(원래 질감 그대로, 텍스처가 구운 대비를 전부 보여준다).
        [Range(0f, 1f)] public float grainStrength = 1f;

        //  ADR-0017(소프트 툰, 그림자 푸른 보라) — LOPToon.shader의 _ShadowColor 기본값과 같은 값을 그대로 옮겼다.
        public Color shadowColor = new Color(0.62f, 0.64f, 0.86f, 1f);

        [Header("배경 밀도·크기")]
        //  10-08 캡처: 배경이 관문과 다퉜다 — 틀을 성기게(9 → 13 m), 사다리를 덜(0.5 → 0.35), 둘 다 줄여(×0.8) 뒤로 물렸다.
        //  10-10 Task 7 Fix 1(사용자 "배경이 너무 눈에 들어온다"·"투머치"): 13 → 45 m, 사다리 0.35 → 0.1.
        //  10-10 Fix 1b(사용자 v2 "너무 휑하다"): 열린 통로 뒤를 조금 더 깊게 — 45 → 30 m, 사다리 0.1 → 0.3, 발판 광차, 먼 비계 빈 칸 7 → 5.
        public float frameSpacing = 30f;         // 배경 갱목 틀(BgFrame) 간격(m)
        [Range(0f, 1f)] public float ladderChance = 0.3f;  // 틀마다 사다리가 걸릴 비율
        [Range(0f, 1f)] public float midCartChance = 0.3f; // 가운데 층 발판마다 광차가 설 비율
        public float lanternSpacing = 16f;        // 랜턴 간격(m)
        public int cartCount = 2;                 // 평평한 앞부분 안 배경 광차 수
        [Range(0.3f, 1f)] public float farScale = 0.75f;    // 먼 비계·광차 축척(z 18에서 멀리 보이게)
        [Range(0.3f, 1f)] public float midScale = 0.8f;     // 가운데 층(틀·발판·사다리) 축척
        //  10-10 Task 7 Fix 1: 먼 비계가 이어진 X 가새 띠로 관문과 다퉜다("배경이 너무 눈에 들어와서 장애물 집중이 떨어진다") —
        //  run칸 놓고 gap칸 비워 무리로 끊는다(0이면 이어 깐다).
        [Min(0)] public int farTrestleRun = 2;   // 먼 비계 무리 하나의 칸 수
        [Min(0)] public int farTrestleGap = 5;   // 무리 사이 빈 칸 수

        [Header("바위 면 장식(10-10 Fix 1b) — 판정 고체 바위 면 위에만, 어둡고 납작하게. 칸 확률 합 ≤ 1")]
        //  사용자 10-10: v1 "투머치·요란하다" → v2 "차분한데 너무 휑하다". 그 사이 — 칸(decorCell m 격자)마다 하나까지.
        [Min(0.6f)] public float decorCell = 2.6f;             // 후보 격자 칸(m) — 작을수록 촘촘
        [Range(0f, 1f)] public float decorLensChance = 0.4f;  // 은은한 지층 렌즈
        [Range(0f, 1f)] public float decorSeamChance = 0.14f;  // 큰 금(이음매)
        [Range(0f, 1f)] public float decorPropChance = 0.07f;  // 박힌 갱목 받침(기둥 둘 + 상인방)
        [Range(0f, 1f)] public float decorCribChance = 0.04f;  // 박힌 갱목 쌓기
        [Range(0f, 1f)] public float decorOreChance = 0.05f;   // 가는 광맥 + 어두운 광석 조각
        [Range(0f, 1f)] public float decorRailChance = 0.025f; // 묻힌 레일 조각
        [Range(0f, 1f)] public float decorLanternChance = 0.5f; // 갱목 받침마다 작은 랜턴을 걸 비율
        [Min(0f)] public float decorLanternMinGap = 18f;       // 바위 면 랜턴 사이 최소 간격(m)
        [Min(0.2f)] public float decorMargin = 0.45f;          // 열린 통로·관문·칸막이에서 떨어질 거리(m)
        [Min(0f)] public float shaftDeepTimberOffset = 2.6f;   // 수직 갱 벽 깊은 갱목 두 줄의 판정선 바깥 거리(m, 0 = 없음)
        public bool shaftWallLadder = true;                   // 수직 갱 아래쪽 벽 사다리
        [Range(0f, 0.3f)] public float decorContrast = 0.09f;  // 지층 렌즈 밝기 차(바위 대비 ±)
        [Range(0.3f, 1f)] public float decorSeamShade = 0.72f; // 금·광맥 = 바위 × 이 값
        [Range(0.2f, 1f)] public float decorTimberShade = 0.5f; // 박힌 갱목·사다리 = 갱목 색 × 이 값
        [Range(0.2f, 1f)] public float decorOreShade = 0.55f;   // 광석 조각 = 광석 색 × 이 값

        [Header("펼치기 1차 — 새 부품(Task 3·4). 배치 값은 FlappyMineDressing(다음 태스크)이 읽는다")]
        //  지층 띠(Strata) 4색 — v(아래→위) 순서, mine_parts.py STRATA_COLORS와 같은 시안 값. 색은 이미
        //  strata.png에 구워 있어 재질 기본색은 흰색이다(FlappyMineMaterials 참고) — 여기서 바꿔도 네 띠를 따로
        //  바꾸지는 못하고(텍스처 재굽기가 필요) 전체를 한 틴트로만 반영한다.
        public Color[] strataColors =
        {
            Hex("#7a5646"), Hex("#6a4838"), Hex("#8a644e"), Hex("#5e4034"),
        };
        //  10-10 Task 7 Fix 1(사용자 "디자인이 시끄럽다 — 바위는 차분한 어두운 덩어리, 지층은 아주 은은하게"): 바위 덩어리(Strata 슬롯)는
        //  strata.png 대신 위 네 색을 평균 쪽으로 당긴(대비 strataContrast) 어두운(strataBrightness) 부드러운 띠 텍스처를 굽는다.
        [Range(0f, 1f)] public float strataContrast = 0.12f;
        [Range(0.3f, 1.2f)] public float strataBrightness = 0.8f;
        public float shaftTimberSpacing = 0.45f;   // 갱도 벽 갱목(ShaftTimber) 배치 간격(m)
        //  케이지(Cage)가 CageRail 위에서 쉬는 높이 비율(0 아래 끝 ~ 1 위 끝). 10-10 Task 7 Fix 1부터 케이지를 놓지 않아 지금은 안 쓴다.
        [Range(0f, 1f)] public float cagePositionRatio = 0.3f;
        public int lanternsPerString = 4;          // 랜턴 줄 하나에 매단 랜턴 수 — 10-10 Task 7 Fix 1부터 입구 랜턴 줄을 놓지 않아 지금은 안 쓴다
        //  ⚡ 판·발판 빛 세기 — 시안 emit은 각각 ×1.4·×1.6이었지만 과하게 빛난다는 지적(10-08류)이 있어 낮췄다.
        //  밑감 색에 곱하는 배수라 1 미만이면 블룸 문턱을 안 넘는다(발판은 일부러 더 은은하게).
        public float boltGlow = 1.2f;              // ⚡ 판(BoltGlow) 빛 세기
        public float padGlow = 0.8f;               // 발판(PadGlow) 빛 세기 — 더 은은하게
        public Color signColor = Hex("#7a4e30");   // 간판 판자 색(SignWood 슬롯 — 입구 간판·⚡ 판이 같이 쓴다)

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }
}
