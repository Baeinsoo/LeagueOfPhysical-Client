using System;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 카드 색·모양 재질(옛 <c>CardRushCoursePalette</c>를 옮김 — 3차엔 문·돌이 없어 머리 위 카드만 쓴다). 색마다 재질 하나를 공유한다.
    /// </summary>
    public sealed class CardRushCardPalette : IDisposable
    {
        public static readonly Color Red = Hex("#E5484D");
        public static readonly Color Yellow = Hex("#F5C542");
        public static readonly Color Blue = Hex("#3B82F6");

        private readonly Material[] colors = new Material[CardColors.Count];
        private readonly Mesh[] shapes = new Mesh[CardColors.Count];
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

        public Material Decal { get; }

        public CardRushCardPalette()
        {
            for (int i = 0; i < CardColors.Count; i++)
            {
                colors[i] = Make("CardRush_" + (CardColor)i, ColorOf((CardColor)i));
                shapes[i] = MakeShape((CardColor)i);
            }
            Decal = Make("CardRush_Decal", Color.white);
        }

        public static Color ColorOf(CardColor color) => color switch
        {
            CardColor.Red => Red,
            CardColor.Yellow => Yellow,
            _ => Blue,
        };

        public Material Of(CardColor color) => colors[(int)color];

        /// <summary>색마다 모양(빨강 ●, 노랑 ▲, 파랑 ■) — 색을 못 가리는 사람도 모양으로 읽는다.</summary>
        public Mesh ShapeOf(CardColor color) => shapes[(int)color];

        private Material Make(string name, Color color)
        {
            var shader = Shader.Find("LOP/Toon");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            var m = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            //  가장자리 빛은 캐릭터용 — 비스듬히 보이는 판이 회색으로 뜨지 않게 끈다(LobbyTunnel과 같은 이유).
            if (m.HasProperty("_RimStrength")) m.SetFloat("_RimStrength", 0f);
            owned.Add(m);
            return m;
        }

        //  -z를 보는 평면 모양(지름 1). 앞뒤 면을 다 넣어 어느 쪽에서 봐도 보인다.
        private Mesh MakeShape(CardColor color)
        {
            var verts = new List<Vector3>();
            switch (color)
            {
                case CardColor.Red:
                    for (int i = 0; i < 32; i++)
                    {
                        float a = i * Mathf.PI * 2f / 32f;
                        verts.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.5f);
                    }
                    break;
                case CardColor.Yellow:
                    for (int i = 0; i < 3; i++)
                    {
                        float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / 3f;
                        verts.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a) - 0.1f, 0f) * 0.55f);
                    }
                    break;
                default:
                    verts.Add(new Vector3(-0.4f, -0.4f, 0f));
                    verts.Add(new Vector3(0.4f, -0.4f, 0f));
                    verts.Add(new Vector3(0.4f, 0.4f, 0f));
                    verts.Add(new Vector3(-0.4f, 0.4f, 0f));
                    break;
            }

            //  가운데 점에서 부채꼴로 잇는다. 앞뒤 면은 점을 따로 둔다 — 점을 같이 쓰면 법선이 서로 지워져 검게 그려진다.
            int n = verts.Count;
            var all = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            for (int side = 0; side < 2; side++)
            {
                int start = all.Count;
                var normal = side == 0 ? Vector3.back : Vector3.forward;
                all.Add(Vector3.zero);
                normals.Add(normal);
                foreach (var v in verts)
                {
                    all.Add(v);
                    normals.Add(normal);
                }
                for (int i = 0; i < n; i++)
                {
                    int a = start + 1 + i;
                    int b = start + 1 + (i + 1) % n;
                    //  유니티 앞면 = 보는 쪽에서 시계 방향. -z 쪽 면은 각을 줄이는 순서, +z 쪽은 반대.
                    if (side == 0) { tris.Add(start); tris.Add(b); tris.Add(a); }
                    else { tris.Add(start); tris.Add(a); tris.Add(b); }
                }
            }
            var mesh = new Mesh { name = "CardRush_Shape_" + color, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(all);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            owned.Add(mesh);
            return mesh;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public void Dispose()
        {
            foreach (var o in owned)
            {
                CardRushGagFx.DestroyObject(o);
            }
            owned.Clear();
        }
    }
}
