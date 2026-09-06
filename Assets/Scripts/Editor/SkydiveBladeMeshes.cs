using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 도는 날개 메시를 만든다. 선풍기 날개처럼 <b>길이를 따라 휘고 두께가 얇아지는</b> 형태다 —
    /// 실험의 요점이 "곡면에 닿았을 때 밀리는가"라 평평한 상자면 물어볼 것이 없다.
    ///
    /// <para><b>왜 한 장씩 따로인가</b>: 겹침을 밀어내는 계산(<c>Physics.ComputePenetration</c>)은
    /// 볼록한 형상만 답한다. 날개 세 장을 한 덩어리로 만들면 오목해지므로, 한 장씩 따로 두고
    /// 각자 볼록 콜라이더를 붙인다. 실제로 만들 때도 그렇게 쪼갠다.</para>
    ///
    /// <para>⚠️ <b>실험용(스파이크)</b> — 결과에 따라 통째로 버릴 수 있다.</para>
    /// </summary>
    public static class SkydiveBladeMeshes
    {
        private const int LengthSteps = 12;   // 길이 방향 마디
        private const int RingPoints = 8;     // 단면 둘레 점

        private const float HubRadius = 0.5f;
        private const float TipRadius = 3.0f;

        //  뒤로 젖혀지는 양(m). 이게 0이면 곧은 판이라 실험의 의미가 없다.
        private const float SweepBack = 0.9f;

        private const float ChordAtHub = 0.85f;
        private const float ChordAtTip = 0.45f;
        private const float ThickAtHub = 0.24f;
        private const float ThickAtTip = 0.09f;

        /// <summary>
        /// +X 방향으로 뻗는 날개 한 장. 회전축은 원점의 Y축이고, 휘는 방향은 +Z(도는 반대쪽)다.
        /// </summary>
        public static Mesh CreateBlade()
        {
            var vertices = new Vector3[LengthSteps * RingPoints + 2];
            int v = 0;

            for (int i = 0; i < LengthSteps; i++)
            {
                float t = (float)i / (LengthSteps - 1);
                float radius = Mathf.Lerp(HubRadius, TipRadius, t);

                //  뿌리 쪽은 거의 곧고 끝으로 갈수록 크게 젖혀진다 — 실제 날개의 모양이다.
                float back = SweepBack * t * t;

                float halfChord = Mathf.Lerp(ChordAtHub, ChordAtTip, t) * 0.5f;
                float halfThick = Mathf.Lerp(ThickAtHub, ThickAtTip, t) * 0.5f;

                //  단면을 조금씩 비튼다(피치). 곡면이 한 방향으로만 굽지 않게 해서
                //  "어느 면에 닿았나"가 실제로 갈리게 만든다.
                float pitch = Mathf.Lerp(0f, 35f, t) * Mathf.Deg2Rad;
                float cos = Mathf.Cos(pitch), sin = Mathf.Sin(pitch);

                for (int j = 0; j < RingPoints; j++)
                {
                    float a = j * Mathf.PI * 2f / RingPoints;
                    float z = Mathf.Cos(a) * halfChord;
                    float y = Mathf.Sin(a) * halfThick;
                    vertices[v++] = new Vector3(radius, y * cos - z * sin, back + z * cos + y * sin);
                }
            }

            int hubCap = v;
            vertices[v++] = new Vector3(HubRadius, 0f, 0f);
            int tipCap = v;
            vertices[v++] = new Vector3(TipRadius, 0f, SweepBack);

            var triangles = new System.Collections.Generic.List<int>();
            for (int i = 0; i < LengthSteps - 1; i++)
            {
                for (int j = 0; j < RingPoints; j++)
                {
                    int a = i * RingPoints + j;
                    int b = i * RingPoints + (j + 1) % RingPoints;
                    int c = (i + 1) * RingPoints + j;
                    int d = (i + 1) * RingPoints + (j + 1) % RingPoints;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }
            for (int j = 0; j < RingPoints; j++)
            {
                int a = j;
                int b = (j + 1) % RingPoints;
                triangles.Add(hubCap); triangles.Add(b); triangles.Add(a);

                int c = (LengthSteps - 1) * RingPoints + j;
                int d = (LengthSteps - 1) * RingPoints + (j + 1) % RingPoints;
                triangles.Add(tipCap); triangles.Add(c); triangles.Add(d);
            }

            var mesh = new Mesh { name = "SkydiveFanBlade" };
            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
