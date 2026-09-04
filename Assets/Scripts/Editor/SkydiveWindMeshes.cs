using System.Collections.Generic;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 바람 시각물의 메시를 만든다 — 방향을 가리키는 화살표와, 범위를 감싸는 원기둥 면.
    ///
    /// <para>유니티 기본 도형에는 <b>원뿔이 없어서</b> 화살표 머리를 직접 만든다. 머리가 없으면
    /// 막대가 앞뒤 대칭이라 어느 쪽으로 부는지 알 수 없다.</para>
    ///
    /// <para>둘 다 <b>크기 1짜리 원본</b>을 만들고, 볼륨마다 스케일만 다르게 쓴다.</para>
    /// </summary>
    internal static class SkydiveWindMeshes
    {
        // 화살표 원본의 비율. 전체 길이 1을 기준으로 한 값이다.
        private const float HeadFraction = 0.38f;
        private const float ShaftRadius = 0.035f;
        private const float HeadRadius = 0.09f;
        private const int ArrowSides = 8;

        // 원기둥 옆면을 몇 조각으로 나눌지. 반지름 150짜리 볼륨에서도 각져 보이지 않을 만큼.
        private const int ShellSides = 48;

        /// <summary>
        /// +Z를 향한 화살표. 길이 1, 꼬리가 z=-0.5, 끝이 z=+0.5.
        /// 스케일을 바람 세기로 주면 길이가 곧 "1초에 밀리는 거리"가 된다.
        /// </summary>
        public static Mesh CreateArrow()
        {
            var v = new List<Vector3>();
            var t = new List<int>();

            const float tail = -0.5f;
            const float tip = 0.5f;
            float headBase = tip - HeadFraction;

            AddTube(v, t, ArrowSides, ShaftRadius, tail, headBase);
            AddDiscFacingBack(v, t, ArrowSides, ShaftRadius, tail);
            AddDiscFacingBack(v, t, ArrowSides, HeadRadius, headBase);
            AddCone(v, t, ArrowSides, HeadRadius, headBase, tip);

            return Build(v, t, "SkydiveWindArrow");
        }

        /// <summary>
        /// 범위를 감싸는 원기둥. 반지름 0.5, 높이 1, 원점 가운데.
        /// 위·아래 뚜껑까지 있어서 들어가고 나오는 면이 보인다.
        /// </summary>
        public static Mesh CreateShell()
        {
            var v = new List<Vector3>();
            var t = new List<int>();

            AddShellSide(v, t, ShellSides, 0.5f, -0.5f, 0.5f);
            AddShellCap(v, t, ShellSides, 0.5f, 0.5f, up: true);
            AddShellCap(v, t, ShellSides, 0.5f, -0.5f, up: false);

            return Build(v, t, "SkydiveWindShell");
        }

        private static Mesh Build(List<Vector3> verts, List<int> tris, string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // --- 화살표(축 = Z) ---

        // 옆면. 면마다 꼭짓점을 새로 만들어 모서리가 뭉개지지 않게 한다.
        private static void AddTube(List<Vector3> v, List<int> t, int sides, float radius, float z0, float z1)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = Angle(i, sides), a1 = Angle(i + 1, sides);
                int b = v.Count;
                v.Add(RingZ(a0, radius, z0));
                v.Add(RingZ(a1, radius, z0));
                v.Add(RingZ(a0, radius, z1));
                v.Add(RingZ(a1, radius, z1));
                t.Add(b); t.Add(b + 3); t.Add(b + 2);
                t.Add(b); t.Add(b + 1); t.Add(b + 3);
            }
        }

        private static void AddCone(List<Vector3> v, List<int> t, int sides, float radius, float zBase, float zTip)
        {
            for (int i = 0; i < sides; i++)
            {
                int b = v.Count;
                v.Add(RingZ(Angle(i, sides), radius, zBase));
                v.Add(RingZ(Angle(i + 1, sides), radius, zBase));
                v.Add(new Vector3(0f, 0f, zTip));
                t.Add(b); t.Add(b + 1); t.Add(b + 2);
            }
        }

        private static void AddDiscFacingBack(List<Vector3> v, List<int> t, int sides, float radius, float z)
        {
            for (int i = 0; i < sides; i++)
            {
                int b = v.Count;
                v.Add(new Vector3(0f, 0f, z));
                v.Add(RingZ(Angle(i + 1, sides), radius, z));
                v.Add(RingZ(Angle(i, sides), radius, z));
                t.Add(b); t.Add(b + 1); t.Add(b + 2);
            }
        }

        // --- 원기둥(축 = Y) ---

        private static void AddShellSide(List<Vector3> v, List<int> t, int sides, float radius, float y0, float y1)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = Angle(i, sides), a1 = Angle(i + 1, sides);
                int b = v.Count;
                v.Add(RingY(a0, radius, y0));
                v.Add(RingY(a1, radius, y0));
                v.Add(RingY(a0, radius, y1));
                v.Add(RingY(a1, radius, y1));
                t.Add(b); t.Add(b + 2); t.Add(b + 3);
                t.Add(b); t.Add(b + 3); t.Add(b + 1);
            }
        }

        private static void AddShellCap(List<Vector3> v, List<int> t, int sides, float radius, float y, bool up)
        {
            for (int i = 0; i < sides; i++)
            {
                int b = v.Count;
                v.Add(new Vector3(0f, y, 0f));
                v.Add(RingY(Angle(up ? i + 1 : i, sides), radius, y));
                v.Add(RingY(Angle(up ? i : i + 1, sides), radius, y));
                t.Add(b); t.Add(b + 1); t.Add(b + 2);
            }
        }

        private static float Angle(int i, int sides) => i * Mathf.PI * 2f / sides;

        private static Vector3 RingZ(float angle, float radius, float z)
            => new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z);

        private static Vector3 RingY(float angle, float radius, float y)
            => new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
    }
}
