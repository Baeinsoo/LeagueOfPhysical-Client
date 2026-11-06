using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace LOP
{
    /// <summary>카드 러시 그림이 함께 쓰는 원시 도형·재질 도우미. 콜라이더는 늘 지운다(그림이 PhysX에 끼면 안 된다).</summary>
    public static class CardRushDraw
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material, HideFlags hideFlags = HideFlags.None)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.hideFlags = hideFlags;
            CardRushGagFx.DestroyObject(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        public static GameObject Box(string name, Transform parent, Vector3 at, Vector3 size, Material material, HideFlags hideFlags = HideFlags.None)
        {
            var go = Primitive(PrimitiveType.Cube, name, parent, material, hideFlags);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            return go;
        }

        public static GameObject Node(string name, Transform parent, HideFlags hideFlags = HideFlags.None)
        {
            var go = new GameObject(name) { hideFlags = hideFlags };
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>툰 불투명 — 장애물·결승 문처럼 바닥에 선 것.</summary>
        public static Material Opaque(string name, Color color)
        {
            var shader = Shader.Find("LOP/Toon");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            SetColor(m, color);
            if (m.HasProperty("_RimStrength")) m.SetFloat("_RimStrength", 0f);
            return m;
        }

        /// <summary>반투명 무광 — 빨간 바닥·먼지·방패처럼 비치는 것. 알파는 <see cref="Tint"/>로 몸마다 준다.</summary>
        public static Material Transparent(string name, Color color)
        {
            var urp = Shader.Find("Universal Render Pipeline/Unlit");
            Material m;
            if (urp == null)
            {
                m = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
            }
            else
            {
                m = new Material(urp) { hideFlags = HideFlags.DontSave };
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            m.name = name;
            SetColor(m, color);
            return m;
        }

        public static void SetColor(Material m, Color color)
        {
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, color);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, color);
        }

        /// <summary>재질은 공유한 채 이 렌더러만 색·알파를 바꾼다.</summary>
        public static void Tint(Renderer renderer, MaterialPropertyBlock block, Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }

        public static void SetActive(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }
    }

    /// <summary>장애물·구역·트랙이 공유하는 재질 — 색마다 하나. 효과 하나에 한 색 계열(나무는 갈색, 바닥은 빨강, 먼지는 흰색).</summary>
    public sealed class CardRushStageMaterials : IDisposable
    {
        public static readonly Color Wood = new Color(0.54f, 0.35f, 0.20f);
        public static readonly Color WoodDark = new Color(0.42f, 0.26f, 0.14f);
        public static readonly Color WoodLight = new Color(0.72f, 0.54f, 0.35f);
        public static readonly Color Barrel = new Color(0.61f, 0.29f, 0.17f);
        public static readonly Color Iron = new Color(0.30f, 0.30f, 0.32f);
        public static readonly Color Chicken = new Color(0.96f, 0.94f, 0.90f);
        public static readonly Color Comb = new Color(0.86f, 0.18f, 0.16f);
        public static readonly Color Beak = new Color(0.98f, 0.62f, 0.15f);
        public static readonly Color Zone = new Color(0.90f, 0.28f, 0.30f);
        public static readonly Color ZoneEdge = new Color(0.75f, 0.15f, 0.17f);
        public static readonly Color Rock = new Color(0.42f, 0.39f, 0.37f);
        public static readonly Color Shadow = new Color(0.05f, 0.03f, 0.03f);
        public static readonly Color Dust = Color.white;
        public static readonly Color Line = Color.white;
        public static readonly Color Banner = new Color(0.95f, 0.89f, 0.70f);
        public static readonly Color Skin = new Color(0.94f, 0.79f, 0.66f);
        public static readonly Color Hair = new Color(0.17f, 0.13f, 0.11f);
        public static readonly Color PupilAngry = new Color(0.75f, 0.15f, 0.17f);

        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

        public Material WoodMat { get; }
        public Material WoodDarkMat { get; }
        public Material WoodLightMat { get; }
        public Material BarrelMat { get; }
        public Material IronMat { get; }
        public Material ChickenMat { get; }
        public Material CombMat { get; }
        public Material BeakMat { get; }
        public Material RockMat { get; }
        public Material BannerMat { get; }
        public Material SkinMat { get; }
        public Material HairMat { get; }
        public Material EyeMat { get; }
        public Material PupilMat { get; }
        public Material PupilAngryMat { get; }
        /// <summary>반투명 — 알파는 렌더러마다 <see cref="CardRushDraw.Tint"/>로.</summary>
        public Material Clear { get; }
        public Material Checker { get; }

        public CardRushStageMaterials()
        {
            WoodMat = Own(CardRushDraw.Opaque("CardRush_Wood", Wood));
            WoodDarkMat = Own(CardRushDraw.Opaque("CardRush_WoodDark", WoodDark));
            WoodLightMat = Own(CardRushDraw.Opaque("CardRush_WoodLight", WoodLight));
            BarrelMat = Own(CardRushDraw.Opaque("CardRush_Barrel", Barrel));
            IronMat = Own(CardRushDraw.Opaque("CardRush_Iron", Iron));
            ChickenMat = Own(CardRushDraw.Opaque("CardRush_Chicken", Chicken));
            CombMat = Own(CardRushDraw.Opaque("CardRush_Comb", Comb));
            BeakMat = Own(CardRushDraw.Opaque("CardRush_Beak", Beak));
            RockMat = Own(CardRushDraw.Opaque("CardRush_Rock", Rock));
            BannerMat = Own(CardRushDraw.Opaque("CardRush_Banner", Banner));
            SkinMat = Own(CardRushDraw.Opaque("CardRush_Skin", Skin));
            HairMat = Own(CardRushDraw.Opaque("CardRush_Hair", Hair));
            EyeMat = Own(CardRushDraw.Opaque("CardRush_Eye", Color.white));
            PupilMat = Own(CardRushDraw.Opaque("CardRush_Pupil", Hair));
            PupilAngryMat = Own(CardRushDraw.Opaque("CardRush_PupilAngry", PupilAngry));
            Clear = Own(CardRushDraw.Transparent("CardRush_Clear", Color.white));
            Checker = Own(MakeChecker());
        }

        private T Own<T>(T o) where T : UnityEngine.Object
        {
            owned.Add(o);
            return o;
        }

        //  결승 띠의 체크 무늬 — 2줄 × 42칸 점 텍스처를 띠 윗면 하나에 편다(칸마다 상자를 만들지 않는다).
        private Material MakeChecker()
        {
            const int columns = 42;
            var tex = new Texture2D(columns, 2, TextureFormat.RGBA32, false)
            {
                name = "CardRush_Checker", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave,
            };
            for (int x = 0; x < columns; x++)
                for (int y = 0; y < 2; y++)
                    tex.SetPixel(x, y, ((x + y) & 1) == 0 ? new Color(0.13f, 0.13f, 0.13f) : Color.white);
            tex.Apply();
            owned.Add(tex);
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");   //  엔진 리소스라 폰 빌드에도 있다(Unlit/Texture는 안 담긴다)
            var m = new Material(shader) { name = "CardRush_Checker", hideFlags = HideFlags.DontSave };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            CardRushDraw.SetColor(m, Color.white);
            return m;
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

    /// <summary>
    /// 월드 위에 뜨는 글 하나(남은 초·말풍선·결승 현수막) — 이름표(<see cref="KnockbackDamageLabel"/>)와 같은 월드 공간 UI 패널 방식.
    /// 카메라가 없으면(에디터 미리보기·시험) 만들지 않는다.
    /// </summary>
    public sealed class CardRushWorldLabel
    {
        private const string PanelSettingsResource = "UI/WorldSpaceNameplatePanelSettings";
        private const string UxmlResource = "UI/CardRushWorldLabel";
        private const float ScreenSizeFactor = 0.07f;

        private static PanelSettings panelSettings;
        private static VisualTreeAsset uxml;

        private readonly GameObject go;
        private readonly Label label;
        private string shown;

        private CardRushWorldLabel(GameObject go, Label label)
        {
            this.go = go;
            this.label = label;
        }

        public static CardRushWorldLabel Create(string name, Transform parent)
        {
            if (UnityEngine.Application.isPlaying == false)
            {
                return null;
            }
            if (panelSettings == null)
            {
                panelSettings = Resources.Load<PanelSettings>(PanelSettingsResource);
                uxml = Resources.Load<VisualTreeAsset>(UxmlResource);
            }
            if (panelSettings == null || uxml == null)
            {
                return null;   //  리소스가 없으면 글만 못 띄운다 — 게임은 계속된다
            }
            //  패널 값을 정한 뒤 OnEnable이 패널을 만들도록 꺼 둔 채 구성하고 켠다.
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = uxml;
            document.worldSpaceSize = new Vector2(240f, 80f);
            go.SetActive(true);
            var label = document.rootVisualElement.Q<Label>("label-text");
            var made = new CardRushWorldLabel(go, label);
            made.Hide();
            return made;
        }

        //  GameObject를 껐다 켜면 UIDocument가 트리를 새로 지어 잡아 둔 Label이 끊긴다 — 켠 채 글만 숨긴다.
        private void SetShown(bool on)
        {
            var display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (label.style.display != display) label.style.display = display;
        }

        /// <summary>카메라를 보는 글. <paramref name="size"/> 1 = 기본 크기(화면에서 일정).</summary>
        public void Show(string text, Vector3 at, Color color, float alpha, float size, Camera cam)
        {
            if (go == null || label == null || cam == null)
            {
                return;
            }
            SetShown(true);
            if (text != shown)
            {
                shown = text;
                label.text = text;
            }
            color.a = Mathf.Clamp01(alpha);
            label.style.color = color;
            go.transform.position = at;
            go.transform.rotation = cam.transform.rotation;
            go.transform.localScale = Vector3.one * (Vector3.Distance(at, cam.transform.position) * ScreenSizeFactor * size);
        }

        /// <summary>바닥에 고정된 글(결승 현수막) — 화면 크기가 아니라 월드 크기로, 정해진 방향을 본다.</summary>
        public void Pin(string text, Vector3 at, Quaternion facing, float worldScale, Color color)
        {
            if (go == null || label == null)
            {
                return;
            }
            SetShown(true);
            shown = text;
            label.text = text;
            label.style.color = color;
            go.transform.SetPositionAndRotation(at, facing);
            go.transform.localScale = Vector3.one * worldScale;
        }

        public void Hide()
        {
            if (go != null && label != null) SetShown(false);
        }

        public void Destroy() => CardRushGagFx.DestroyObject(go);
    }

    /// <summary>
    /// 한 레인의 장애물·구역 그림(spec §11.2). (상태, 그림 틱)에서 <see cref="CardRushHazardLayout"/>이 낸 모습을 자리·자세·투명도로만 옮긴다.
    /// 오브젝트는 몸 앞 몇 개만 만들고, 지나가거나 치운 것은 지운다. 실행 중 뷰와 에디터 미리보기가 같이 쓴다.
    /// </summary>
    public sealed class CardRushLaneHazards
    {
        /// <summary>몸 앞 이만큼(m)까지만 장애물을 만든다 — 그 너머는 카메라에 안 잡힌다.</summary>
        public const float AheadMeters = 40f;
        /// <summary>몸보다 이만큼(m) 뒤로 간 장애물·바닥은 숨긴다(1차 "지나온 문이 화면 아래를 덮음"의 교훈).</summary>
        public const float BehindMeters = 4f;
        /// <summary>바리케이드 폭 = 레인 폭의 80%.</summary>
        public const float BarricadeWidthOfLane = 0.8f;
        /// <summary>빨간 바닥 투명도 — 켜질 때 0.18 → 터질 때 0.43(시안).</summary>
        public const float ZoneAlphaStart = 0.18f;
        public const float ZoneAlphaEnd = 0.43f;
        /// <summary>테두리가 흰색과 깜빡이기 시작하는 경고 몫(마지막 30%)과 깜빡 주기(틱).</summary>
        public const float ZoneBlinkFrom = 0.7f;
        public const int ZoneBlinkTicks = 4;
        public const int ShardCount = 6;
        public const float RockBehindMeters = 1f;
        /// <summary>구역 남은 초는 레인 오른쪽 가장자리, 몸 앞 1 m(바닥 안으로 자름) — 바닥 가운데·앞 끝은 그 안에 선 내 몸·머리 위 카드가 가렸다(리뷰 I2).</summary>
        public const float ZoneLabelAheadMm = 1000f;
        public static float ZoneLabelRight(float laneSpacing) => laneSpacing * 0.5f;
        public static float ZoneLabelAlongMm(int fromMm, int toMm, float bodyMm)
            => float.IsInfinity(bodyMm) ? (fromMm + toMm) * 0.5f : Mathf.Clamp(bodyMm + ZoneLabelAheadMm, fromMm, toMm);

        private readonly Transform root;
        private readonly Vector3 origin;
        private readonly Vector3 forward;
        private readonly Vector3 right;
        private readonly Quaternion facing;
        private readonly float laneSpacing;
        private readonly CardRushStageMaterials materials;
        private readonly HideFlags hideFlags;
        private readonly bool labels;
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private readonly Dictionary<int, ObstacleGo> obstacles = new Dictionary<int, ObstacleGo>();
        private readonly Dictionary<int, ZoneGo> zones = new Dictionary<int, ZoneGo>();
        private readonly List<int> drop = new List<int>();
        private readonly HashSet<int> keep = new HashSet<int>();

        public GameObject Root => root.gameObject;
        /// <summary>이 레인을 깐 출발점. 몸의 출발점이 바뀌면(규칙이 늦게 옴) 뷰가 새로 깐다.</summary>
        public Vector3 Origin => origin;

        public CardRushLaneHazards(Transform parent, string name, Vector3 laneOrigin, Vector3 laneForward, float laneSpacing,
                                   CardRushStageMaterials materials, HideFlags hideFlags = HideFlags.None, bool labels = true)
        {
            origin = laneOrigin;
            forward = CardRushLanes.Flat(laneForward);
            right = Vector3.Cross(Vector3.up, forward);
            facing = Quaternion.LookRotation(forward, Vector3.up);
            this.laneSpacing = laneSpacing;
            this.materials = materials;
            this.hideFlags = hideFlags;
            this.labels = labels;
            root = CardRushDraw.Node(name, parent, hideFlags).transform;
        }

        /// <summary>레인 축 mm → 월드 자리(바닥).</summary>
        public Vector3 At(float alongMm) => origin + forward * (alongMm / 1000f);

        /// <summary>몸 앞 장애물과 지금·바로 앞 구역을 그림 틱 모습으로 놓는다.</summary>
        public void Apply(in CardRushRunnerState s, double d, float bodyMm, CardRushTrack track, in CardRushTuning tuning, Camera cam)
        {
            keep.Clear();
            int first = Math.Max(0, s.ObstacleIndex - 1);
            for (int i = first; i < track.Obstacles.Count; i++)
            {
                if (track.Obstacles[i].AtMm > bodyMm + AheadMeters * 1000f) break;
                var look = CardRushHazardLayout.ObstacleLook(s, i, d, bodyMm, track, tuning);
                if (look.Phase == CardRushObstaclePhase.Gone || look.AlongMm < bodyMm - BehindMeters * 1000f) continue;
                keep.Add(i);
                ShowObstacle(i, track.Obstacles[i], look, cam);
            }
            DropObstaclesExcept(keep);

            keep.Clear();
            for (int i = Math.Max(0, s.ZoneIndex - 1); i <= s.ZoneIndex && i < track.Zones.Count; i++)
            {
                var look = CardRushHazardLayout.ZoneLook(s, i, d, track);
                if (look.Phase != CardRushZonePhase.Warning && look.Phase != CardRushZonePhase.Impact) continue;
                if (look.ToMm < bodyMm - BehindMeters * 1000f) continue;
                keep.Add(i);
                ShowZone(i, look, (long)Math.Floor(d), cam, bodyMm);
            }
            DropZonesExcept(keep);
        }

        /// <summary>장애물 하나를 그 모습으로 놓는다(미리보기도 부른다).</summary>
        public void ShowObstacle(int index, in CardRushObstacle obstacle, in CardRushObstacleLook look, Camera cam)
        {
            if (obstacles.TryGetValue(index, out var o) == false)
            {
                o = new ObstacleGo(this, index, obstacle);
                obstacles.Add(index, o);
            }
            o.Place(look, cam);
        }

        /// <summary>
        /// 구역 하나를 놓는다. 몸이 이미 빠져나간 바닥(가운데가 몸보다 <see cref="RockBehindMeters"/> 뒤)엔 바위를 안 그린다 —
        /// 카메라 바로 앞에 떨어져 화면 위를 덮었다(실제 판 스크린샷). 먼지는 그대로.
        /// </summary>
        public void ShowZone(int index, in CardRushZoneLook look, long tick, Camera cam, float bodyMm = float.NegativeInfinity)
        {
            if (zones.TryGetValue(index, out var z) == false)
            {
                z = new ZoneGo(this, index, look.FromMm, look.ToMm);
                zones.Add(index, z);
            }
            bool behind = (look.FromMm + look.ToMm) * 0.5f < bodyMm - RockBehindMeters * 1000f;
            z.Place(look, tick, cam, behind == false, bodyMm);
        }

        private void DropObstaclesExcept(HashSet<int> alive)
        {
            drop.Clear();
            foreach (var pair in obstacles) if (alive.Contains(pair.Key) == false) drop.Add(pair.Key);
            foreach (var i in drop)
            {
                obstacles[i].Destroy();
                obstacles.Remove(i);
            }
        }

        private void DropZonesExcept(HashSet<int> alive)
        {
            drop.Clear();
            foreach (var pair in zones) if (alive.Contains(pair.Key) == false) drop.Add(pair.Key);
            foreach (var i in drop)
            {
                zones[i].Destroy();
                zones.Remove(i);
            }
        }

        /// <summary>지금 만든 장애물·구역 수 — 시험·진단용.</summary>
        public int ObstacleCount => obstacles.Count;
        public int ZoneCount => zones.Count;
        public Transform ObstacleRoot(int index) => obstacles.TryGetValue(index, out var o) ? o.Root : null;
        public Transform ZoneRoot(int index) => zones.TryGetValue(index, out var z) ? z.Root : null;

        public void Destroy()
        {
            foreach (var o in obstacles.Values) o.Destroy();
            foreach (var z in zones.Values) z.Destroy();
            obstacles.Clear();
            zones.Clear();
            CardRushGagFx.DestroyObject(root != null ? root.gameObject : null);
        }

        // ── 장애물 하나 ─────────────────────────────────────

        private sealed class ObstacleGo
        {
            private readonly CardRushLaneHazards lane;
            private readonly CardRushObstacleKind kind;
            private readonly int variant;
            private readonly int atMm;          //  코스에 놓인 자리 — 굴러온 거리(통·바퀴 돌기)를 잰다
            private readonly Transform body;        //  서 있을 때의 모습(올라가고 굴러가는 몸)
            private readonly Transform spin;        //  굴러갈 때 도는 부분(통 = 몸 전체, 수레 = 바퀴)
            private readonly Transform[] wheels;
            private readonly Transform[] shards = new Transform[ShardCount];
            private readonly CardRushWorldLabel label;

            public Transform Root { get; }

            public ObstacleGo(CardRushLaneHazards lane, int index, in CardRushObstacle obstacle)
            {
                this.lane = lane;
                kind = obstacle.Kind;
                variant = obstacle.Variant;
                atMm = obstacle.AtMm;
                var m = lane.materials;
                Root = CardRushDraw.Node((kind == CardRushObstacleKind.Barricade ? "Barricade " : "Roller ") + index, lane.root, lane.hideFlags).transform;
                Root.rotation = lane.facing;
                body = CardRushDraw.Node("Body", Root, lane.hideFlags).transform;
                spin = body;
                wheels = Array.Empty<Transform>();
                Material shardMat;
                if (kind == CardRushObstacleKind.Barricade)
                {
                    BuildBarricade(variant, lane.laneSpacing * BarricadeWidthOfLane);
                    shardMat = m.WoodMat;
                }
                else
                {
                    switch (variant)
                    {
                        case 0: spin = BuildBarrel(); shardMat = m.BarrelMat; break;
                        case 1: BuildChicken(); shardMat = m.ChickenMat; break;
                        default: wheels = BuildCart(); shardMat = m.WoodMat; break;
                    }
                }
                for (int i = 0; i < ShardCount; i++)
                {
                    shards[i] = CardRushDraw.Box("Shard", Root, Vector3.zero, new Vector3(0.38f, 0.12f, 0.08f), shardMat, lane.hideFlags).transform;
                    shards[i].gameObject.SetActive(false);
                }
                if (lane.labels) label = CardRushWorldLabel.Create("Seconds", Root);
            }

            private void BuildBarricade(int v, float width)
            {
                var m = lane.materials;
                var f = lane.hideFlags;
                float half = width * 0.5f;
                switch (v)
                {
                    case 0:   //  가로 판자
                        CardRushDraw.Box("PostL", body, new Vector3(-half + 0.08f, 0.5f, 0.06f), new Vector3(0.14f, 1.0f, 0.14f), m.WoodLightMat, f);
                        CardRushDraw.Box("PostR", body, new Vector3(half - 0.08f, 0.5f, 0.06f), new Vector3(0.14f, 1.0f, 0.14f), m.WoodLightMat, f);
                        CardRushDraw.Box("PlankLow", body, new Vector3(0f, 0.35f, 0f), new Vector3(width, 0.24f, 0.08f), m.WoodMat, f);
                        CardRushDraw.Box("PlankHigh", body, new Vector3(0f, 0.75f, 0f), new Vector3(width, 0.24f, 0.08f), m.WoodMat, f);
                        CardRushDraw.Box("Stripe", body, new Vector3(0f, 0.55f, -0.01f), new Vector3(width, 0.06f, 0.08f), m.WoodDarkMat, f);
                        break;
                    case 1:   //  X 판자
                    {
                        CardRushDraw.Box("PostL", body, new Vector3(-half + 0.08f, 0.5f, 0.06f), new Vector3(0.14f, 1.0f, 0.14f), m.WoodLightMat, f);
                        CardRushDraw.Box("PostR", body, new Vector3(half - 0.08f, 0.5f, 0.06f), new Vector3(0.14f, 1.0f, 0.14f), m.WoodLightMat, f);
                        float len = Mathf.Sqrt(width * width + 0.8f * 0.8f);
                        float deg = Mathf.Atan2(0.8f, width) * Mathf.Rad2Deg;
                        var a = CardRushDraw.Box("PlankA", body, new Vector3(0f, 0.55f, 0f), new Vector3(len, 0.22f, 0.08f), m.WoodMat, f);
                        a.transform.localRotation = Quaternion.Euler(0f, 0f, deg);
                        var b = CardRushDraw.Box("PlankB", body, new Vector3(0f, 0.55f, -0.02f), new Vector3(len, 0.22f, 0.08f), m.WoodDarkMat, f);
                        b.transform.localRotation = Quaternion.Euler(0f, 0f, -deg);
                        CardRushDraw.Box("Top", body, new Vector3(0f, 0.95f, 0f), new Vector3(width, 0.12f, 0.1f), m.WoodMat, f);
                        break;
                    }
                    default:  //  통나무 더미
                        Log("LogA", new Vector3(0f, 0.17f, -0.17f), width);
                        Log("LogB", new Vector3(0f, 0.17f, 0.17f), width);
                        Log("LogC", new Vector3(0f, 0.47f, 0f), width * 0.92f);
                        Log("LogD", new Vector3(0f, 0.75f, -0.05f), width * 0.7f);
                        break;
                }
            }

            private void Log(string name, Vector3 at, float length)
            {
                var log = CardRushDraw.Primitive(PrimitiveType.Cylinder, name, body, lane.materials.WoodMat, lane.hideFlags);
                log.transform.localPosition = at;
                log.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                log.transform.localScale = new Vector3(0.34f, length * 0.5f, 0.34f);
                var cap = CardRushDraw.Primitive(PrimitiveType.Cylinder, "Ring", log.transform, lane.materials.WoodLightMat, lane.hideFlags);
                cap.transform.localScale = new Vector3(0.9f, 1.005f, 0.9f);
            }

            //  통: 서 있을 땐 세워 두고, 굴러올 땐 옆으로 눕혀 진행 축으로 돈다. 돌리는 축 노드를 돌려준다.
            private Transform BuildBarrel()
            {
                var m = lane.materials;
                var pivot = CardRushDraw.Node("Pivot", body, lane.hideFlags).transform;
                var drum = CardRushDraw.Primitive(PrimitiveType.Cylinder, "Drum", pivot, m.BarrelMat, lane.hideFlags);
                drum.transform.localScale = new Vector3(0.8f, 0.5f, 0.8f);
                for (int i = -1; i <= 1; i += 2)
                {
                    var band = CardRushDraw.Primitive(PrimitiveType.Cylinder, "Band", pivot, m.IronMat, lane.hideFlags);
                    band.transform.localPosition = new Vector3(0f, 0.3f * i, 0f);
                    band.transform.localScale = new Vector3(0.83f, 0.04f, 0.83f);
                }
                return pivot;
            }

            private void BuildChicken()
            {
                var m = lane.materials;
                var f = lane.hideFlags;
                var torso = CardRushDraw.Primitive(PrimitiveType.Sphere, "Torso", body, m.ChickenMat, f);
                torso.transform.localPosition = new Vector3(0f, 0.5f, 0.05f);
                torso.transform.localScale = new Vector3(0.6f, 0.55f, 0.7f);
                var head = CardRushDraw.Primitive(PrimitiveType.Sphere, "Head", body, m.ChickenMat, f);
                head.transform.localPosition = new Vector3(0f, 0.92f, -0.22f);
                head.transform.localScale = Vector3.one * 0.34f;
                CardRushDraw.Box("Comb", body, new Vector3(0f, 1.1f, -0.22f), new Vector3(0.06f, 0.14f, 0.2f), m.CombMat, f);
                CardRushDraw.Box("Beak", body, new Vector3(0f, 0.9f, -0.42f), new Vector3(0.1f, 0.08f, 0.14f), m.BeakMat, f);
                CardRushDraw.Box("LegL", body, new Vector3(-0.12f, 0.13f, 0.05f), new Vector3(0.05f, 0.26f, 0.05f), m.BeakMat, f);
                CardRushDraw.Box("LegR", body, new Vector3(0.12f, 0.13f, 0.05f), new Vector3(0.05f, 0.26f, 0.05f), m.BeakMat, f);
                CardRushDraw.Box("WingL", body, new Vector3(-0.32f, 0.55f, 0.08f), new Vector3(0.06f, 0.3f, 0.4f), m.ChickenMat, f);
                CardRushDraw.Box("WingR", body, new Vector3(0.32f, 0.55f, 0.08f), new Vector3(0.06f, 0.3f, 0.4f), m.ChickenMat, f);
            }

            private Transform[] BuildCart()
            {
                var m = lane.materials;
                var f = lane.hideFlags;
                CardRushDraw.Box("Bed", body, new Vector3(0f, 0.6f, 0f), new Vector3(1.3f, 0.45f, 0.9f), m.WoodMat, f);
                CardRushDraw.Box("Rim", body, new Vector3(0f, 0.86f, 0f), new Vector3(1.36f, 0.08f, 0.96f), m.WoodDarkMat, f);
                CardRushDraw.Box("Load", body, new Vector3(0f, 0.95f, 0.05f), new Vector3(1.0f, 0.25f, 0.6f), m.BarrelMat, f);
                CardRushDraw.Box("Handle", body, new Vector3(0f, 0.75f, 0.75f), new Vector3(0.9f, 0.06f, 0.6f), m.WoodLightMat, f);
                var list = new Transform[2];
                for (int i = 0; i < 2; i++)
                {
                    var axle = CardRushDraw.Node("Wheel", body, f).transform;
                    axle.localPosition = new Vector3(i == 0 ? -0.72f : 0.72f, 0.32f, 0f);
                    var wheel = CardRushDraw.Primitive(PrimitiveType.Cylinder, "Disc", axle, m.WoodDarkMat, f);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    wheel.transform.localScale = new Vector3(0.64f, 0.05f, 0.64f);
                    CardRushDraw.Box("Spoke", axle, Vector3.zero, new Vector3(0.12f, 0.58f, 0.08f), m.WoodLightMat, f);
                    list[i] = axle;
                }
                return list;
            }

            public void Place(in CardRushObstacleLook look, Camera cam)
            {
                Root.position = lane.At(look.AlongMm);
                bool broken = look.Phase == CardRushObstaclePhase.Smashed;
                CardRushDraw.SetActive(body.gameObject, broken == false);
                for (int i = 0; i < ShardCount; i++) CardRushDraw.SetActive(shards[i].gameObject, broken);

                body.localPosition = Vector3.zero;
                body.localRotation = Quaternion.identity;
                body.localScale = Vector3.one;
                if (spin != body) spin.localRotation = Quaternion.identity;
                if (spin != body) spin.localPosition = new Vector3(0f, 0.5f, 0f);

                switch (look.Phase)
                {
                    case CardRushObstaclePhase.Standing:
                        break;
                    case CardRushObstaclePhase.Lifting:
                        body.localPosition = Vector3.up * (CardRushHazardLayout.BarricadeLiftHeight * look.T01);
                        break;
                    case CardRushObstaclePhase.Lifted:
                    {
                        //  끝까지 올라간 채 더 오르며 납작해져 사라진다.
                        body.localPosition = Vector3.up * (CardRushHazardLayout.BarricadeLiftHeight + 1.2f * look.T01);
                        body.localScale = new Vector3(1f, Mathf.Max(0.02f, 1f - look.T01), 1f);
                        break;
                    }
                    case CardRushObstaclePhase.Rolling:
                        Roll(look);
                        break;
                    case CardRushObstaclePhase.Smashed:
                        Shatter(look.T01);
                        break;
                    case CardRushObstaclePhase.Blocked:
                    {
                        //  방패에 튕겨 옆으로 굴러 나간다(몸 쪽으로 안 온다).
                        Roll(look);
                        float u = look.T01;
                        body.localPosition += new Vector3(1.6f * u, 0.5f * Mathf.Sin(u * Mathf.PI), 0.6f * u);
                        body.localRotation = Quaternion.Euler(0f, 0f, -200f * u) * body.localRotation;
                        body.localScale = Vector3.one * Mathf.Clamp01((1f - u) / 0.4f);
                        break;
                    }
                    case CardRushObstaclePhase.Tumbled:
                    {
                        //  몸을 넘어뜨리고 몸 뒤로 튕겨 굴러 나간다.
                        Roll(look);
                        float u = look.T01;
                        body.localPosition += new Vector3(0.5f * u, 0.9f * Mathf.Sin(u * Mathf.PI), -2.4f * u);
                        body.localRotation = Quaternion.Euler(-300f * u, 0f, 0f) * body.localRotation;
                        body.localScale = Vector3.one * Mathf.Clamp01((1f - u) / 0.4f);
                        break;
                    }
                }

                if (label != null)
                {
                    if (look.Phase == CardRushObstaclePhase.Lifting && cam != null)
                    {
                        //  바리케이드 오른쪽 위 — 가운데 위는 내 머리 위 카드가 가렸다(실제 판 스크린샷).
                        var at = Root.position + lane.right * (lane.laneSpacing * BarricadeWidthOfLane * 0.5f) + Vector3.up * (1.2f + body.localPosition.y);
                        label.Show(look.SecondsLeft.ToString("0.0"), at, Color.white, 1f, 0.8f, cam);
                    }
                    else
                    {
                        label.Hide();
                    }
                }
            }

            //  굴러온 거리만큼 — 통은 눕혀 진행 축으로 돌고, 수레는 바퀴가 돌고, 닭은 퍼덕이며 뛴다.
            private void Roll(in CardRushObstacleLook look)
            {
                if (kind != CardRushObstacleKind.Roller) return;
                float rolled = Mathf.Max(0f, (atMm - look.AlongMm) / 1000f);
                switch (variant)
                {
                    case 0:
                        spin.localPosition = new Vector3(0f, 0.4f, 0f);
                        //  몸 쪽(-z)으로 굴러온다 — 옆으로 눕힌 통이 오른쪽 축으로 뒤로 돈다.
                        spin.localRotation = Quaternion.AngleAxis(-rolled / 0.4f * Mathf.Rad2Deg, Vector3.right) * Quaternion.Euler(0f, 0f, 90f);
                        break;
                    case 1:
                        body.localPosition = Vector3.up * (0.25f * Mathf.Abs(Mathf.Sin(rolled * 3.2f)));
                        body.localRotation = Quaternion.Euler(10f * Mathf.Sin(rolled * 6.4f), 0f, 0f);
                        break;
                    default:
                        foreach (var w in wheels) w.localRotation = Quaternion.AngleAxis(-rolled / 0.32f * Mathf.Rad2Deg, Vector3.right);
                        break;
                }
            }

            private void Shatter(float t)
            {
                //  조각마다 정해진 방향(씨앗 없는 고정 표) — 늘 같은 모양으로 앞·위로 흩어진다.
                for (int i = 0; i < ShardCount; i++)
                {
                    float side = (i - (ShardCount - 1) * 0.5f) / (ShardCount * 0.5f);
                    var start = new Vector3(side * 0.9f, 0.3f + 0.15f * (i % 3), 0f);
                    var v = new Vector3(side * 2.2f, 3.2f + 0.8f * ((i * 7) % 3), 2.4f + 0.6f * (i % 2));
                    var p = start + v * (t * 0.35f) + Vector3.down * (4.9f * (t * 0.35f) * (t * 0.35f));
                    shards[i].localPosition = p;
                    shards[i].localRotation = Quaternion.Euler(400f * t * (i % 2 == 0 ? 1f : -1f), 60f * i, 250f * t);
                    shards[i].localScale = new Vector3(0.38f, 0.12f, 0.08f) * Mathf.Clamp01((1f - t) / 0.5f);
                }
            }

            public void Destroy()
            {
                label?.Destroy();
                CardRushGagFx.DestroyObject(Root != null ? Root.gameObject : null);
            }
        }

        // ── 구역 하나 ───────────────────────────────────────

        private sealed class ZoneGo
        {
            private const float DashMeters = 0.3f;
            private const float DashGap = 0.2f;

            private readonly CardRushLaneHazards lane;
            private readonly Renderer floor;
            private readonly List<Renderer> dashes = new List<Renderer>();
            private readonly Transform rock;
            private readonly Renderer shadow;
            private readonly Renderer dust;
            private readonly CardRushWorldLabel label;
            private readonly float length;

            public Transform Root { get; }

            public ZoneGo(CardRushLaneHazards lane, int index, int fromMm, int toMm)
            {
                this.lane = lane;
                var m = lane.materials;
                var f = lane.hideFlags;
                length = (toMm - fromMm) / 1000f;
                float width = lane.laneSpacing * 0.92f;
                Root = CardRushDraw.Node("Zone " + index, lane.root, f).transform;
                Root.SetPositionAndRotation(lane.At((fromMm + toMm) * 0.5f), lane.facing);

                //  바닥보다 1 cm 띄운다 — 맵 바닥과 같은 평면이면 깜빡인다.
                floor = CardRushDraw.Box("Floor", Root, new Vector3(0f, 0.012f, 0f), new Vector3(width, 0.004f, length), m.Clear, f).GetComponent<Renderer>();
                float halfW = width * 0.5f, halfL = length * 0.5f;
                for (float z = -halfL; z + DashMeters <= halfL + 1e-3f; z += DashMeters + DashGap)
                {
                    dashes.Add(Dash(new Vector3(-halfW, 0.02f, z + DashMeters * 0.5f), new Vector3(0.07f, 0.006f, DashMeters)));
                    dashes.Add(Dash(new Vector3(halfW, 0.02f, z + DashMeters * 0.5f), new Vector3(0.07f, 0.006f, DashMeters)));
                }
                for (float x = -halfW; x + DashMeters <= halfW + 1e-3f; x += DashMeters + DashGap)
                {
                    dashes.Add(Dash(new Vector3(x + DashMeters * 0.5f, 0.02f, -halfL), new Vector3(DashMeters, 0.006f, 0.07f)));
                    dashes.Add(Dash(new Vector3(x + DashMeters * 0.5f, 0.02f, halfL), new Vector3(DashMeters, 0.006f, 0.07f)));
                }

                shadow = CardRushDraw.Primitive(PrimitiveType.Cylinder, "Shadow", Root, m.Clear, f).GetComponent<Renderer>();
                shadow.transform.localPosition = new Vector3(0f, 0.022f, 0f);
                rock = CardRushDraw.Primitive(PrimitiveType.Sphere, "Rock", Root, m.RockMat, f).transform;
                rock.localScale = new Vector3(1.15f, 0.9f, 1.0f);
                rock.localRotation = Quaternion.Euler(10f, 35f, 5f);
                dust = CardRushDraw.Primitive(PrimitiveType.Cylinder, "Dust", Root, m.Clear, f).GetComponent<Renderer>();
                dust.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                if (lane.labels) label = CardRushWorldLabel.Create("ZoneSeconds", Root);
            }

            private Renderer Dash(Vector3 at, Vector3 size)
                => CardRushDraw.Box("Dash", Root, at, size, lane.materials.Clear, lane.hideFlags).GetComponent<Renderer>();

            public void Place(in CardRushZoneLook look, long tick, Camera cam, bool rockShown, float bodyMm)
            {
                var block = lane.block;
                bool warning = look.Phase == CardRushZonePhase.Warning;
                float floorAlpha = warning
                    ? Mathf.Lerp(ZoneAlphaStart, ZoneAlphaEnd, look.Warn01)
                    : ZoneAlphaEnd * Mathf.Clamp01(1f - look.T01 * 2f);
                CardRushDraw.Tint(floor, block, CardRushStageMaterials.Zone, floorAlpha);

                //  점선 테두리 — 마지막 30%엔 흰색과 깜빡인다(한 색 계열 + 흰색, 화면 번쩍임은 없다).
                bool white = warning && look.Warn01 >= ZoneBlinkFrom && (tick / ZoneBlinkTicks) % 2 == 0;
                var edge = white ? Color.white : CardRushStageMaterials.ZoneEdge;
                float edgeAlpha = warning ? 0.9f : 0.9f * Mathf.Clamp01(1f - look.T01 * 2f);
                foreach (var d in dashes) CardRushDraw.Tint(d, block, edge, edgeAlpha);

                bool falling = look.RockHeight >= 0f && rockShown;
                CardRushDraw.SetActive(rock.gameObject, falling);
                CardRushDraw.SetActive(shadow.gameObject, falling && warning);
                if (falling)
                {
                    rock.localPosition = new Vector3(0f, 0.45f + look.RockHeight, 0f);
                    float near = 1f - look.RockHeight / CardRushHazardLayout.RockDropHeight;
                    float r = Mathf.Lerp(0.4f, 1.3f, near);
                    shadow.transform.localScale = new Vector3(r, 0.002f, r);
                    CardRushDraw.Tint(shadow, block, CardRushStageMaterials.Shadow, 0.15f + 0.3f * near);
                }

                bool dusty = look.Phase == CardRushZonePhase.Impact;
                CardRushDraw.SetActive(dust.gameObject, dusty);
                if (dusty)
                {
                    //  흰 먼지 원 — 몸을 덮지 않게 옅게(효과 절제).
                    float r = Mathf.Lerp(1.0f, 3.4f, Mathf.Sqrt(look.T01));
                    dust.transform.localScale = new Vector3(r, 0.002f, r);
                    CardRushDraw.Tint(dust, block, CardRushStageMaterials.Dust, 0.35f * (1f - look.T01));
                }

                if (label != null)
                {
                    var at = Root.position + lane.right * ZoneLabelRight(lane.laneSpacing)
                           + lane.forward * (ZoneLabelAlongMm(look.FromMm, look.ToMm, bodyMm) - (look.FromMm + look.ToMm) * 0.5f) / 1000f
                           + Vector3.up * 0.35f;
                    if (warning && cam != null) label.Show(look.SecondsLeft.ToString("0.0"), at, Color.white, 1f, 0.9f, cam);
                    else label.Hide();
                }
            }

            public void Destroy()
            {
                label?.Destroy();
                CardRushGagFx.DestroyObject(Root != null ? Root.gameObject : null);
            }
        }
    }

    /// <summary>
    /// 몸이 있는 레인마다 장애물·구역을 그림 틱 모습으로 그린다(spec §11.2). 상태는 읽기만 한다.
    /// 그림 틱은 몸개그와 같은 <see cref="CardRushRunnerFxView.DrawnTick"/> — 내 몸은 예측 − 1틱, 남은 보간 재생 시각.
    /// </summary>
    /// <remarks>틱·보간 뒤(IPostLateTickable)에서 칠한다 — 몸이 이번 프레임 자리에 선 뒤라 굴러오는 것·바닥이 몸과 어긋나지 않는다.</remarks>
    public class CardRushHazardView : IStartable, IPostLateTickable, IDisposable
    {
        private readonly CardRushWorld world;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ActorRegistry actors;
        private readonly EntityRenderClock renderClock;
        private readonly IPlayerContext playerContext;
        private readonly CameraController cameraController;

        private readonly Dictionary<string, CardRushLaneHazards> lanes = new Dictionary<string, CardRushLaneHazards>();
        private readonly HashSet<string> seen = new HashSet<string>();
        private readonly List<string> gone = new List<string>();
        private CardRushStageMaterials materials;
        private GameObject root;
        private CardRushTrack builtFor;

        public CardRushHazardView(CardRushWorld world, GameFramework.World.EntityRegistry entityRegistry, ActorRegistry actors,
                                  EntityRenderClock renderClock, IPlayerContext playerContext, CameraController cameraController)
        {
            this.world = world;
            this.entityRegistry = entityRegistry;
            this.actors = actors;
            this.renderClock = renderClock;
            this.playerContext = playerContext;
            this.cameraController = cameraController;
        }

        /// <summary>지금 그리는 레인 수(몸 수). 시험·진단용.</summary>
        public int LaneCount => lanes.Count;

        public void Start()
        {
            materials = new CardRushStageMaterials();
            root = new GameObject("CardRushHazards");
        }

        public void PostLateTick()
        {
            var track = world.Track;
            var rules = world.Rules;
            if (track == null || rules == null || rules.HasRules == false || root == null)
            {
                return;
            }
            //  클라 씨앗은 늦게 와서 출발 전에 코스가 한 번 바뀔 수 있다 — 바뀐 줄로 다시 깐다.
            if (ReferenceEquals(track, builtFor) == false)
            {
                ClearLanes();
                builtFor = track;
            }

            var cam = cameraController != null && cameraController.MainCamera != null ? cameraController.MainCamera : Camera.main;
            string me = playerContext?.entityId;
            var tuning = world.Tuning;
            seen.Clear();
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
                {
                    continue;
                }
                var runner = entity.Get<CardRushRunner>();
                if (runner == null)
                {
                    continue;
                }
                seen.Add(entity.Id);
                //  규칙보다 먼저 생긴 몸은 규칙이 오면 출발점이 바뀐다 — 옛 출발점에 깐 장애물·구역을 걷고 다시 깐다(최종 리뷰 m3).
                if (lanes.TryGetValue(entity.Id, out var lane) && lane.Origin != runner.LaneOrigin)
                {
                    lane.Destroy();
                    lanes.Remove(entity.Id);
                    lane = null;
                }
                if (lane == null)
                {
                    lane = new CardRushLaneHazards(root.transform, "Lane " + entity.Id, runner.LaneOrigin, rules.Forward, rules.LaneSpacing, materials);
                    lanes.Add(entity.Id, lane);
                }
                double d = CardRushRunnerFxView.DrawnTick(renderClock, entity.Id, me);
                lane.Apply(runner.State, d, BodyMm(entity.Id, runner, rules.Forward), track, tuning, cam);
            }

            gone.Clear();
            foreach (var id in lanes.Keys)
            {
                if (seen.Contains(id) == false) gone.Add(id);
            }
            foreach (var id in gone)
            {
                lanes[id].Destroy();
                lanes.Remove(id);
            }
        }

        //  몸이 그려진 자리(레인 축 mm) — 그림이 없으면 상태 값.
        private float BodyMm(string id, CardRushRunner runner, Vector3 forward)
        {
            var visual = actors?.Get(id)?.visualGameObject;
            if (visual == null)
            {
                return runner.State.PosMm;
            }
            return Vector3.Dot(visual.transform.position - runner.LaneOrigin, CardRushLanes.Flat(forward)) * 1000f;
        }

        private void ClearLanes()
        {
            foreach (var lane in lanes.Values) lane.Destroy();
            lanes.Clear();
        }

        public void Dispose()
        {
            ClearLanes();
            CardRushGagFx.DestroyObject(root);
            root = null;
            materials?.Dispose();
            materials = null;
        }
    }
}
