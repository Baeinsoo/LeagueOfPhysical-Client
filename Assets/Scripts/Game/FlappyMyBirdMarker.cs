using System;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 내 새 표시(spec §2): 몸 둘레 흰 테두리 + 머리 위 노란 아래 화살표(위아래로 2 px 흔들림). 여러 새 사이에서
    /// 내 새를 한눈에 찾게 한다(intent 성공 판정 2).
    /// <para>테두리는 외곽선 셰이더가 아니라 몸 <b>뒤</b>(+z, 카메라 반대쪽)에 한 겹 큰 흰 원판이다 — 불투명한 몸이 가운데를
    /// 가려 둘레만 보인다. 모바일에서 싸고, 새 머티리얼(주인은 StunAppearance)을 건드리지 않는다.</para>
    /// <para>표시를 내 새 그림의 <b>자식</b>으로 단다. 이 진입점(LateTick)은 보간기 LateUpdate보다 먼저 돌아서, 월드 좌표만
    /// 찍으면 그 프레임에 보간기가 새를 옮긴 만큼(11 m/s면 ~7 cm) 표시가 뒤처진다. 자식이면 같이 움직인다.</para>
    /// <para>수치는 시제품(30 px/m)의 픽셀 값을 m로 옮긴 것: 테두리 3 px → 0.15 m, 화살표 14×10 px → 0.47×0.33 m,
    /// 몸 위 8 px → 0.27 m, 흔들림 2 px → 0.067 m, 각속도 6.</para>
    /// </summary>
    public class FlappyMyBirdMarker : ILateTickable, IDisposable
    {
        internal const float RingWidth = 0.15f;
        internal const float BehindZ = 0.6f;
        internal const float ArrowGap = 0.27f;
        internal const float ArrowWidth = 0.47f;
        internal const float ArrowHeight = 0.33f;
        internal const float BobAmplitude = 0.067f;
        internal const float BobSpeed = 6f;
        //  화살표는 몸보다 카메라 쪽으로 살짝 — 날개 끝에 가려지지 않게.
        private const float ArrowFrontZ = -0.1f;

        private static readonly Color ArrowColor = new Color32(0xff, 0xd2, 0x57, 0xff);

        private readonly ActorRegistry actorRegistry;
        private readonly IGameDataStore gameDataStore;
        private readonly FlappyConfig config;

        private GameObject root;
        private SpriteRenderer silhouette;
        private SpriteRenderer arrow;
        private Sprite disk;
        private Sprite triangle;
        private float time;

        internal Transform Silhouette => silhouette != null ? silhouette.transform : null;
        internal Transform Arrow => arrow != null ? arrow.transform : null;
        internal bool Visible => root != null && root.activeSelf;

        public FlappyMyBirdMarker(ActorRegistry actorRegistry, IGameDataStore gameDataStore, FlappyConfig config)
        {
            this.actorRegistry = actorRegistry;
            this.gameDataStore = gameDataStore;
            this.config = config;
        }

        public void LateTick() => LateTick(Time.deltaTime);

        internal void LateTick(float deltaTime)
        {
            time += deltaTime;
            var visual = actorRegistry.Get(gameDataStore.userEntityId)?.visualGameObject;
            if (visual == null)
            {
                //  잡혔거나 아직 그림이 안 붙었다. 그림의 자식이었다면 그림과 함께 이미 사라졌다(root == null).
                if (root != null)
                {
                    root.SetActive(false);
                }
                return;
            }

            EnsureRoot();
            if (root.transform.parent != visual.transform)
            {
                root.transform.SetParent(visual.transform, false);
            }
            root.SetActive(true);

            //  월드 기준으로 찍는다(그림이 돌아도 원판은 카메라를 보고 화살표는 아래를 가리키게).
            //  찍은 뒤 보간기가 그림을 옮기면 자식이라 같이 옮겨진다.
            Vector3 center = visual.transform.position + Vector3.up * config.BodyRadius;
            root.transform.SetPositionAndRotation(center, Quaternion.identity);
            silhouette.transform.position = center + new Vector3(0f, 0f, BehindZ);
            float bob = Mathf.Sin(time * BobSpeed) * BobAmplitude;
            arrow.transform.position = center + new Vector3(0f, config.BodyRadius + ArrowGap + ArrowHeight * 0.5f + bob, ArrowFrontZ);
            //  그림 뿌리 크기가 1이 아니어도 월드 크기가 같게.
            Vector3 parentScale = visual.transform.lossyScale;
            float diameter = 2f * (config.BodyRadius + RingWidth);
            silhouette.transform.localScale = Divide(new Vector3(diameter, diameter, 1f), parentScale);
            arrow.transform.localScale = Divide(new Vector3(ArrowWidth, ArrowHeight, 1f), parentScale);
        }

        private static Vector3 Divide(Vector3 size, Vector3 scale)
            => new Vector3(size.x / Mathf.Max(1e-4f, scale.x), size.y / Mathf.Max(1e-4f, scale.y), size.z / Mathf.Max(1e-4f, scale.z));

        private void EnsureRoot()
        {
            if (root != null)
            {
                return;
            }
            disk = disk != null ? disk : MakeDisk();
            triangle = triangle != null ? triangle : MakeDownTriangle();
            root = new GameObject("FlappyMyBirdMarker");
            silhouette = MakeSprite("MyBirdSilhouette", disk, Color.white);
            arrow = MakeSprite("MyBirdArrow", triangle, ArrowColor);
        }

        private SpriteRenderer MakeSprite(string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            return r;
        }

        //  가장자리만 살짝 부드러운 흰 원판(지름 1 m 스프라이트 — 크기는 localScale로).
        private static Sprite MakeDisk()
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half));
                    float a = Mathf.Clamp01(half - d);   // 바깥 1 px만 흐린다
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * a));
                }
            }
            return SpriteFrom(pixels, size, size, "FlappyMyBirdDisk");
        }

        //  아래를 가리키는 세모(위 변이 넓다).
        private static Sprite MakeDownTriangle()
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float halfWidth = (y / (float)(size - 1)) * (size * 0.5f);   // 아래(y=0) 뾰족, 위 넓음
                for (int x = 0; x < size; x++)
                {
                    bool inside = Mathf.Abs(x - (size - 1) * 0.5f) <= halfWidth;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(inside ? 255 : 0));
                }
            }
            return SpriteFrom(pixels, size, size, "FlappyMyBirdArrow");
        }

        private static Sprite SpriteFrom(Color32[] pixels, int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = name };
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Mathf.Max(w, h));
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

        public void Dispose()
        {
            DestroyObject(root);
            root = null;
            foreach (var sprite in new[] { disk, triangle })
            {
                if (sprite != null)
                {
                    var tex = sprite.texture;
                    DestroyObject(sprite);
                    DestroyObject(tex);
                }
            }
            disk = triangle = null;
        }
    }
}
