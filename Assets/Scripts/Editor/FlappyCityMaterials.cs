using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 무너지는 도시의 재질 다섯. <b>없을 때만 만든다</b> — 한 번 만든 뒤 인스펙터에서 손으로
    /// 고친 값이 다시 구울 때마다 날아가면 아트를 만질 수가 없다.
    ///
    /// <para><b>게임 평면은 어두워지지 않는다.</b> 구간이 진행돼도 알베도 휘도를 0.60 아래로
    /// 내리지 않는다 — 제일 어려운 구간에서 관문이 제일 안 보이면 난이도를 아트로 몰래 올린
    /// 셈이다. 그을음은 <i>색조</i>로만 표현하고, 잔불 발광이 테두리를 오히려 밝게 만든다.</para>
    ///
    /// <para>추격자 벽 재질은 여기 없다 — 벽은 런타임 생성물이라 에셋을 물릴 길이 없어
    /// <see cref="LOP.FlappyChaserView"/>가 코드로 만든다.</para>
    /// </summary>
    public static class FlappyCityMaterials
    {
        private const string Folder = "Assets/Art/Environment/FlappyRace";

        public static Material Of(FlappyRace.CourseSection section)
        {
            switch (section)
            {
                case FlappyRace.CourseSection.Exposed: return Load("CityExposed");
                case FlappyRace.CourseSection.Charred: return Load("CityCharred");
                default: return Load("CityIntact");
            }
        }

        public static Material Midground => Load("Midground");
        public static Material Skyline => Load("Skyline");

        [MenuItem("LOP/Debug/Flappy 도시 재질 만들기")]
        public static void EnsureAll()
        {
            //  게임 평면 셋 — 휘도 0.77 / 0.68 / 0.61. <b>셋 다 따뜻하다</b>: 안개가 배경을
            //  안개색(차가운 청회)으로 씻기므로, 근경이 중성 회백이면 배경과 같은 값이 되어
            //  파이프가 묻힌다(실제로 그랬다 — 안개를 켜자마자 드러났다).
            //  "근경 따뜻 / 원경 차갑게"는 Skydive 대기 슬라이스가 먼저 박은 규칙이다.
            Ensure("CityIntact", new Color(0.84f, 0.76f, 0.62f), smoothness: 0.10f, emission: Color.black);
            Ensure("CityExposed", new Color(0.84f, 0.66f, 0.44f), smoothness: 0.14f, emission: Color.black);
            Ensure("CityCharred", new Color(0.84f, 0.56f, 0.42f), smoothness: 0.18f,
                   emission: new Color(0.55f, 0.18f, 0.05f));

            //  중간층·배경 — 여기만 어두워진다. 안개가 거리로 더 씻긴다.
            Ensure("Midground", new Color(0.33f, 0.34f, 0.38f), smoothness: 0f, emission: Color.black);
            Ensure("Skyline", new Color(0.28f, 0.34f, 0.46f), smoothness: 0f, emission: Color.black);

            AssetDatabase.SaveAssets();
            Debug.Log("[도시 재질] 없던 것만 만들었다 — 이미 있던 것은 손대지 않았다.");
        }

        private static Material Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{Folder}/{name}.mat");
        }

        private static void Ensure(string name, Color baseColor, float smoothness, Color emission)
        {
            if (Load(name) != null)
            {
                return;
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[도시 재질] URP Lit 셰이더를 못 찾았다.");
                return;
            }
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            if (emission != Color.black)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", emission);
            }
            AssetDatabase.CreateAsset(material, $"{Folder}/{name}.mat");
        }
    }
}
