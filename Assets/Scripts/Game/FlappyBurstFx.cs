using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 조각 터뜨리기(시제품 <c>burst</c>) — 충돌 파편과 결승 꽃가루가 같이 쓴다. 네모 조각이 사방으로 튀고 중력 12로 떨어지며
    /// 남은 수명만큼 흐려진다. 판정과 무관한 화면 연출.
    /// </summary>
    public class FlappyBurstFx : ITickable, IDisposable
    {
        internal const float Gravity = 12f;

        /// <summary>새(z 0)보다 카메라 쪽으로 — 몸에 가리지 않게.</summary>
        internal const float FrontZ = -0.2f;

        private class Bit
        {
            public SpriteRenderer Renderer;
            public Vector3 Velocity;
            public float Life;
            public float Max;
        }

        private readonly List<Bit> bits = new List<Bit>();
        private GameObject root;
        private Sprite square;

        //  시험 이음매 — 0~1.
        internal Func<float> random = () => UnityEngine.Random.value;
        internal int Count => bits.Count;
        internal Vector3 PositionOf(int i) => bits[i].Renderer.transform.position;
        internal float AlphaOf(int i) => bits[i].Renderer.color.a;
        internal float SizeOf(int i) => bits[i].Renderer.transform.localScale.x;

        /// <summary>center에서 count조각. 속력 speed × (0.4~1.2), 크기 size × (0.6~1.4) m, 수명 life초.</summary>
        public void Burst(Vector3 center, Color color, int count, float speed, float life, float size)
        {
            EnsureRoot();
            for (int i = 0; i < count; i++)
            {
                float angle = random() * Mathf.PI * 2f;
                float s = speed * (0.4f + random() * 0.8f);
                float sz = size * (0.6f + random() * 0.8f);
                var go = new GameObject("Bit");
                go.transform.SetParent(root.transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = square;
                r.color = color;
                r.sortingOrder = 4;
                go.transform.position = new Vector3(center.x, center.y, FrontZ);
                go.transform.localScale = new Vector3(sz, sz, 1f);
                bits.Add(new Bit { Renderer = r, Velocity = new Vector3(Mathf.Cos(angle) * s, Mathf.Sin(angle) * s, 0f), Life = life, Max = life });
            }
        }

        public void Tick()
        {
            Tick(Time.deltaTime);
        }

        internal void Tick(float deltaTime)
        {
            for (int i = bits.Count - 1; i >= 0; i--)
            {
                var b = bits[i];
                b.Life -= deltaTime;
                if (b.Life <= 0f || b.Renderer == null)
                {
                    if (b.Renderer != null)
                    {
                        DestroyObject(b.Renderer.gameObject);
                    }
                    bits.RemoveAt(i);
                    continue;
                }
                //  시제품 순서: 자리를 옮기고 그다음 속도를 꺾는다.
                b.Renderer.transform.position += b.Velocity * deltaTime;
                b.Velocity += Vector3.down * Gravity * deltaTime;
                var c = b.Renderer.color;
                c.a = Mathf.Clamp01(b.Life / b.Max);
                b.Renderer.color = c;
            }
        }

        private void EnsureRoot()
        {
            if (root != null)
            {
                return;
            }
            root = new GameObject("FlappyBurstFx");
            if (square == null)
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "FlappyBurstSquare", filterMode = FilterMode.Point };
                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++) { pixels[i] = new Color32(255, 255, 255, 255); }
                tex.SetPixels32(pixels);
                tex.Apply();
                square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);   // 1 m 네모 — 크기는 localScale로
            }
        }

        public void Dispose()
        {
            bits.Clear();
            DestroyObject(root);   //  조각은 root 자식이라 같이 지워진다
            root = null;
            if (square != null)
            {
                var tex = square.texture;
                DestroyObject(square);
                DestroyObject(tex);
                square = null;
            }
        }

        private static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return;
            }
            if (UnityEngine.Application.isPlaying) { UnityEngine.Object.Destroy(obj); }
            else { UnityEngine.Object.DestroyImmediate(obj); }
        }
    }
}
