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
        [Range(0f, 1f)] public float hazeCaveAlpha = 0.55f;
        //  먼 안개 막(z 12) 진하기 — 먼 비계·실루엣은 안개를 두 겹 쓴다(10-08). 같은 까닭으로 어두운 굴 안개는
        //  0.4 한 겹 더로는 22%밖에 안 어두워졌고, 밝은 바깥 안개는 0.65면 협곡이 다 지워졌다 — 구역마다 따로 둔다.
        [Range(0f, 1f)] public float farHazeOutsideAlpha = 0.15f;
        [Range(0f, 1f)] public float farHazeCaveAlpha = 0.65f;

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
        public float frameSpacing = 13f;         // 배경 갱목 틀(BgFrame) 간격(m)
        [Range(0f, 1f)] public float ladderChance = 0.35f;  // 틀마다 사다리가 걸릴 비율
        public float lanternSpacing = 16f;        // 랜턴 간격(m)
        public int cartCount = 2;                 // 보기 구간 안 배경 광차 수
        [Range(0.3f, 1f)] public float farScale = 0.75f;    // 먼 비계·광차 축척(z 18에서 멀리 보이게)
        [Range(0.3f, 1f)] public float midScale = 0.8f;     // 가운데 층(틀·발판·사다리) 축척

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }
}
