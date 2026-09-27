using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 화살 모양을 코드로 만든다 — 육각 대, 촉, 깃 세 장. 에셋이 아니라서 전달 과정이 없고 숫자만 고치면 된다.
    /// 날아가는 쪽이 +Z이고, 기준점은 촉 끝에서 <see cref="TipEmbed"/>만큼 뒤다(꽂히면 촉이 묻힌다).
    /// 실물보다 굵다 — 45m 밖에서도 보여야 한다.
    /// </summary>
    public static class ArcheryArrowMesh
    {
        public const float Length = 0.85f;
        public const float ShaftRadius = 0.035f;
        public const float HeadLength = 0.16f;
        public const float HeadRadius = 0.07f;
        public const float FletchLength = 0.22f;
        public const float FletchHeight = 0.08f;
        public const float TipEmbed = 0.06f;

        //  부분 번호 — 렌더러의 재질 순서와 같다.
        public const int ShaftPart = 0;
        public const int HeadPart = 1;
        public const int FletchPart = 2;

        private const int Sides = 6;

        public static Mesh Build()
        {
            var vertices = new List<Vector3>();
            var parts = new[] { new List<int>(), new List<int>(), new List<int>() };

            float tipZ = TipEmbed;
            float headBaseZ = TipEmbed - HeadLength;
            float tailZ = TipEmbed - Length;

            //  대: 촉 밑동부터 꼬리까지 육각 기둥 + 꼬리 뚜껑.
            for (int i = 0; i < Sides; i++)
            {
                Vector3 a = Ring(i, ShaftRadius), b = Ring(i + 1, ShaftRadius);
                Quad(vertices, parts[ShaftPart],
                     a + Vector3.forward * tailZ, b + Vector3.forward * tailZ,
                     b + Vector3.forward * headBaseZ, a + Vector3.forward * headBaseZ);
                Tri(vertices, parts[ShaftPart],
                    new Vector3(0f, 0f, tailZ), b + Vector3.forward * tailZ, a + Vector3.forward * tailZ);
            }

            //  촉: 육각뿔 + 밑면.
            for (int i = 0; i < Sides; i++)
            {
                Vector3 a = Ring(i, HeadRadius) + Vector3.forward * headBaseZ;
                Vector3 b = Ring(i + 1, HeadRadius) + Vector3.forward * headBaseZ;
                Tri(vertices, parts[HeadPart], a, b, new Vector3(0f, 0f, tipZ));
                Tri(vertices, parts[HeadPart], new Vector3(0f, 0f, headBaseZ), b, a);
            }

            //  깃: 꼬리 쪽에 세 장, 뒤로 갈수록 높아지는 사다리꼴. 앞뒤 양면.
            float fletchBackZ = tailZ + 0.02f;
            float fletchFrontZ = fletchBackZ + FletchLength;
            for (int k = 0; k < 3; k++)
            {
                float angle = Mathf.PI * 0.5f + k * Mathf.PI * 2f / 3f;
                var dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                Vector3 innerBack = dir * ShaftRadius + Vector3.forward * fletchBackZ;
                Vector3 innerFront = dir * ShaftRadius + Vector3.forward * fletchFrontZ;
                Vector3 outerBack = dir * (ShaftRadius + FletchHeight) + Vector3.forward * fletchBackZ;
                Vector3 outerFront = dir * (ShaftRadius + FletchHeight) + Vector3.forward * (fletchBackZ + FletchLength * 0.45f);
                Quad(vertices, parts[FletchPart], innerBack, outerBack, outerFront, innerFront);
                Quad(vertices, parts[FletchPart], innerFront, outerFront, outerBack, innerBack);
            }

            var mesh = new Mesh { name = "ArcheryArrow" };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = parts.Length;
            for (int i = 0; i < parts.Length; i++)
            {
                mesh.SetTriangles(parts[i], i);
            }
            mesh.RecalculateNormals();   // 면마다 점을 따로 두어 각진 로우폴리 음영이 난다
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Ring(int i, float radius)
        {
            float angle = i * Mathf.PI * 2f / Sides;
            return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }

        private static void Tri(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        private static void Quad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Tri(vertices, triangles, a, b, c);
            Tri(vertices, triangles, a, c, d);
        }
    }
}
