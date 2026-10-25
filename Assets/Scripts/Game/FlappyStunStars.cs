using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 기절 동안 머리 위에 별 셋이 돈다(spec §3, 시제품 <c>drawBird</c>의 별). 내 새는 예측 기절, 남의 새는 스냅의 기절 끝 틱
    /// (<see cref="IFlappyBirdLooks"/>가 읽는다). 무적 깜빡임은 지금처럼 <see cref="StunAppearance"/>가 그대로 한다.
    /// <para>별 묶음을 새 그림의 <b>자식</b>으로 단다 — 이 진입점(LateTick)은 보간기 LateUpdate보다 먼저 돌아, 월드 좌표만 찍으면
    /// 그 프레임에 새가 옮긴 만큼 뒤처진다(<see cref="FlappyMyBirdMarker"/>와 같은 이유). 묶음엔 <see cref="StunAppearanceIgnore"/>를 단다.</para>
    /// <para>수치는 시제품(30 px/m)을 m로: 몸 중심 위 1.25R, 가로 1.3R·세로 0.35R 타원, 초당 5 rad, 별끼리 2.1 rad, 별 반지름 4 px → 지름 0.27 m.</para>
    /// </summary>
    public class FlappyStunStars : ILateTickable, IDisposable
    {
        internal const int StarCount = 3;
        internal const float SpinSpeed = 5f;
        internal const float PhaseStep = 2.1f;
        internal const float OrbitX = 1.3f;
        internal const float OrbitY = 0.35f;
        internal const float Lift = 1.25f;
        internal const float StarSize = 0.27f;
        //  몸보다 카메라 쪽 — 날개에 안 가린다.
        private const float FrontZ = -0.15f;

        private static readonly Color StarColor = new Color32(0xff, 0xe0, 0x8a, 0xff);

        private class Halo
        {
            public GameObject Root;
            public SpriteRenderer[] Stars;
        }

        private readonly IFlappyBirdLooks looks;
        private readonly FlappyConfig config;
        private readonly Dictionary<string, Halo> halos = new Dictionary<string, Halo>();
        private readonly List<FlappyBirdLook> buffer = new List<FlappyBirdLook>();
        private readonly HashSet<string> seen = new HashSet<string>();
        private readonly List<string> gone = new List<string>();
        private Sprite star;
        private float time;

        internal bool Shows(string id) => halos.TryGetValue(id, out var h) && h.Root != null && h.Root.activeSelf;
        internal UnityEngine.Transform StarOf(string id, int index) => halos[id].Stars[index].transform;

        public FlappyStunStars(IFlappyBirdLooks looks, FlappyConfig config)
        {
            this.looks = looks;
            this.config = config;
        }

        public void LateTick()
        {
            LateTick(Time.deltaTime);
        }

        internal void LateTick(float deltaTime)
        {
            time += deltaTime;
            looks.Fill(buffer);
            seen.Clear();
            foreach (var bird in buffer)
            {
                seen.Add(bird.Id);
                halos.TryGetValue(bird.Id, out var halo);
                if (bird.Stunned == false)
                {
                    if (halo != null && halo.Root != null)
                    {
                        halo.Root.SetActive(false);
                    }
                    continue;
                }
                //  그림이 바뀌면(모델 교체) 옛 그림과 함께 묶음도 사라졌다 — 다시 만든다.
                if (halo == null || halo.Root == null)
                {
                    halo = MakeHalo();
                    halos[bird.Id] = halo;
                }
                if (halo.Root.transform.parent != bird.Visual)
                {
                    halo.Root.transform.SetParent(bird.Visual, false);
                }
                halo.Root.SetActive(true);
                Place(halo, bird.Visual);
            }

            gone.Clear();
            foreach (var id in halos.Keys)
            {
                if (seen.Contains(id) == false)
                {
                    gone.Add(id);
                }
            }
            foreach (var id in gone)
            {
                DestroyObject(halos[id].Root);
                halos.Remove(id);
            }
        }

        //  월드 기준으로 찍는다(그림이 돌아도 별은 화면을 본다). 찍은 뒤 보간기가 그림을 옮기면 자식이라 같이 옮겨진다.
        private void Place(Halo halo, UnityEngine.Transform visual)
        {
            float r = config.BodyRadius;
            Vector3 center = visual.position + Vector3.up * r;
            halo.Root.transform.SetPositionAndRotation(center, Quaternion.identity);
            Vector3 parentScale = visual.lossyScale;
            for (int k = 0; k < StarCount; k++)
            {
                float a = time * SpinSpeed + k * PhaseStep;
                var t = halo.Stars[k].transform;
                t.position = center + new Vector3(Mathf.Cos(a) * r * OrbitX, r * Lift + Mathf.Sin(a) * r * OrbitY, FrontZ);
                t.localScale = new Vector3(StarSize / Mathf.Max(1e-4f, parentScale.x), StarSize / Mathf.Max(1e-4f, parentScale.y), 1f);
            }
        }

        private Halo MakeHalo()
        {
            star = star != null ? star : MakeStar();
            var root = new GameObject("FlappyStunStars");
            //  그림의 자식이라 StunAppearance가 렌더러를 모을 때 걸린다 — 머티리얼 주인이 아니니 빼 달라는 표시.
            root.AddComponent<StunAppearanceIgnore>();
            var halo = new Halo { Root = root, Stars = new SpriteRenderer[StarCount] };
            for (int k = 0; k < StarCount; k++)
            {
                var go = new GameObject("Star" + k);
                go.transform.SetParent(root.transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = star;
                r.color = StarColor;
                r.sortingOrder = 5;
                halo.Stars[k] = r;
            }
            return halo;
        }

        //  다섯 꼭지 별(바깥 1, 안쪽 0.45 — 시제품 star()). 지름 1 m 스프라이트, 크기는 localScale로.
        private static Sprite MakeStar()
        {
            const int size = 64;
            var poly = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI / 5f + Mathf.PI / 2f;   //  첫 꼭지가 위
                float q = i % 2 == 1 ? 0.45f : 1f;
                poly[i] = new Vector2(Mathf.Cos(a) * q, Mathf.Sin(a) * q);
            }
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Inside(poly, p) ? 255 : 0));
                }
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FlappyStunStar" };
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        //  점이 다각형 안인가(가로 반직선이 변을 몇 번 건너나).
        private static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                bool crosses = (poly[i].y > p.y) != (poly[j].y > p.y)
                    && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x;
                if (crosses)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        public void Dispose()
        {
            foreach (var halo in halos.Values)
            {
                DestroyObject(halo.Root);
            }
            halos.Clear();
            if (star != null)
            {
                var tex = star.texture;
                DestroyObject(star);
                DestroyObject(tex);
                star = null;
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
