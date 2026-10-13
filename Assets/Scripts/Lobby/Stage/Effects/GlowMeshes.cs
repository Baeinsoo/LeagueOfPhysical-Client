using UnityEngine;

namespace LOP
{
    /// <summary>무대 연출용 코드 메시 — 서치라이트 원뿔(옆면만), 가랜드 삼각 깃발(앞뒤 두 장).</summary>
    public static class GlowMeshes
    {
        /// <summary>바닥(y=0) 반지름 bottomRadius에서 위(y=height) 반지름 topRadius로 넓어지는 옆면. uv.y는 아래 0 → 위 1.</summary>
        public static Mesh Cone(float bottomRadius, float topRadius, float height, int segments)
        {
            var v = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[v.Length];
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v[i * 2] = dir * bottomRadius;
                v[i * 2 + 1] = dir * topRadius + Vector3.up * height;
                uv[i * 2] = new Vector2(i / (float)segments, 0f);
                uv[i * 2 + 1] = new Vector2(i / (float)segments, 1f);
            }
            var t = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int b = i * 2;
                t[i * 6] = b; t[i * 6 + 1] = b + 1; t[i * 6 + 2] = b + 2;
                t[i * 6 + 3] = b + 2; t[i * 6 + 4] = b + 1; t[i * 6 + 5] = b + 3;
            }
            var mesh = new Mesh { name = "GlowCone", vertices = v, uv = uv, triangles = t };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>윗변이 x=±width/2, 꼭짓점이 아래(y=−height)인 삼각형. 툰 셰이더가 뒷면을 버려서 뒤집은 한 장을 더 둔다(꼭짓점·법선 따로).</summary>
        public static Mesh Triangle(float width, float height)
        {
            //  앞뒤 면이 꼭짓점을 같이 쓰면 법선이 서로 지워져 검게 나온다 — 면마다 꼭짓점을 따로 둔다.
            var a = new Vector3(-width / 2f, 0f, 0f);
            var b = new Vector3(width / 2f, 0f, 0f);
            var c = new Vector3(0f, -height, 0f);
            var v = new[] { a, b, c, a, b, c };
            var n = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.forward, Vector3.forward, Vector3.forward };
            var mesh = new Mesh { name = "BuntingFlag", vertices = v, normals = n, triangles = new[] { 0, 1, 2, 3, 5, 4 } };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
