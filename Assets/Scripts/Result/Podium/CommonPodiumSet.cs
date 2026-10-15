using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 공용 "피지컬 리그 시상식" 세트 — 인디고 바닥·뒷벽, 크림 단 셋(1위 노랑), 기울인 현수막, 단 번호.
    /// 일반은 주황 현수막 + 색종이, 랭크는 빨강 현수막 + 서치라이트(5부 로비와 같은 짝). 모두 코드로 만든다.
    /// </summary>
    public sealed class CommonPodiumSet : IPodiumSet
    {
        //  단 무리가 화면 오른쪽 절반 가운데에 오는 고정 카메라(시작값 — 미리보기 그림으로 맞춘다).
        public static readonly StageCameraPose CameraPose = new StageCameraPose(new Vector3(0f, 2f, -10f), new Vector3(0f, 1f, 0f));

        public PodiumStage Build(Transform root, PodiumContext context)
        {
            var slots = PodiumLayout.P1(context.Placements, context.IsDraw);
            var go = new GameObject("CommonPodium");
            go.transform.SetParent(root, false);

            Box(go, "Floor", LobbyTunnel.MakeGlowMaterial("#262458"), new Vector3(0f, -0.05f, 2f), new Vector3(40f, 0.1f, 20f));
            Box(go, "BackWall", LobbyTunnel.MakeGlowMaterial(context.Ranked ? "#1C1B4A" : "#2C2A6B"), new Vector3(0f, 6f, 4f), new Vector3(40f, 12f, 0.2f));

            if (context.HasResult)
            {
                var step = LobbyTunnel.MakeGlowMaterial("#FFF8E8");
                var gold = LobbyTunnel.MakeGlowMaterial("#FFD23F");
                int n = context.Placements.Count;
                int steps = Mathf.Min(3, n);
                for (int i = 0; i < steps; i++)
                {
                    float h = PodiumLayout.StepHeight(i);
                    Box(go, $"Step_{i + 1}", i == 0 ? gold : step,
                        new Vector3(PodiumLayout.StepX(i, n), h / 2f, PodiumLayout.Center.z),
                        new Vector3(PodiumLayout.StepWidth - 0.06f, h, PodiumLayout.StepDepth));
                }
            }

            var decor = go.AddComponent<PodiumDecor>();
            decor.Build(context, slots);
            return new PodiumStage { Slots = slots, Camera = CameraPose, OnReveal = decor.Reveal };
        }

        private static void Box(GameObject root, string name, Material m, Vector3 pos, Vector3 size)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            Object.DestroyImmediate(b.GetComponent<Collider>());
            b.transform.SetParent(root.transform, false);
            b.transform.localPosition = pos;
            b.transform.localScale = size;
            var r = b.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
