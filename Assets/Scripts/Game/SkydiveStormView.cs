using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 태풍의 눈 몸개그 장애물(시제품) 그림.
    /// <list type="bullet">
    /// <item>장애물 넷을 원시 도형으로 짓고, 움직이는 것(먹구름·갈매기)은 프레임마다 소수 틱으로 세운다 — 판정과 같은 식에 같은 틱.</item>
    /// <item>몸에 닿으면 몸 위에 글("뽀잉!")을 띄우고, 기절이면 굴러 돌며 별, 갈매기면 머리에 한 마리, 상승 기류면 빙글빙글 돈다.</item>
    /// <item>큰 지형물: 원판·풍차·조리개는 시뮬이 잔해 판정(통나무 뗏목·뽑힌 거목·판자 더미)으로 갈아 끼우고, 여기서 그 상자 안에 그림을 넣는다. 문은 콜라이더 윗면을 짚어 장식만 입힌다(그림 ⊆ 판정).</item>
    /// <item>벽을 따라 잔해(판자·잎·짚)가 빙빙 돈다.</item>
    /// </list>
    /// 몸 연출은 보간기·자세 기울기 뒤에 와야 지워지지 않는다 — <see cref="SkydiveStormFxDriver"/>(실행 순서 3060)가 부른다.
    /// </summary>
    public class SkydiveStormView : IStartable, ILateTickable, IDisposable
    {
        private readonly GameFramework.Runner.IRunner runner;
        private readonly StormHazardField field;
        private readonly ObstacleField obstacles;
        private readonly DoorField doors;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ActorRegistry actorRegistry;
        private readonly SkydiveConfig config;

        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private GameObject root;
        private Material cloudWhite, cloudShadow, stormDark, stormBolt, gullWhite, gullGrey, beak, updraftMat, starMat;
        private readonly List<Transform> stormClouds = new List<Transform>();
        private readonly List<GameObject> stormBolts = new List<GameObject>();
        private readonly List<(Transform body, Transform wingL, Transform wingR)[]> gulls = new List<(Transform, Transform, Transform)[]>();
        private readonly List<Transform> cushions = new List<Transform>();
        private readonly float[] cushionSquash = new float[32];
        private readonly List<(Transform ring, Transform[] motes)> updrafts = new List<(Transform, Transform[])>();
        private readonly List<(Transform t, float r, float a, float y, float w, float spin)> swirl = new List<(Transform, float, float, float, float, float)>();

        private class Body
        {
            public long ShownHitTick = -1;
            public Vector3 TumbleAxis = Vector3.right;
            public float TumbleStart;
            public GameObject Stars;
            public GameObject Gull;
            public float LiftYaw;
        }
        private readonly Dictionary<string, Body> bodies = new Dictionary<string, Body>();

        private struct Pop
        {
            public CardRushWorldLabel Label;
            public string Text;
            public Color Color;
            public Vector3 At;
            public float Born;
            public float Size;
        }
        private readonly List<Pop> pops = new List<Pop>();
        private readonly HashSet<int> dressed = new HashSet<int>();
        private SkydiveStormFxDriver driver;
        private double renderTick;

        public SkydiveStormView(GameFramework.Runner.IRunner runner, StormHazardField field, ObstacleField obstacles, DoorField doors,
                                GameFramework.World.EntityRegistry entityRegistry, ActorRegistry actorRegistry, SkydiveConfig config)
        {
            this.runner = runner;
            this.field = field;
            this.obstacles = obstacles;
            this.doors = doors;
            this.entityRegistry = entityRegistry;
            this.actorRegistry = actorRegistry;
            this.config = config;
        }

        public void Start()
        {
            root = new GameObject("SkydiveStorm");
            owned.Add(root);
            driver = root.AddComponent<SkydiveStormFxDriver>();
            driver.View = this;

            cloudWhite = Keep(CardRushDraw.Opaque("Storm_CloudWhite", new Color(0.97f, 0.98f, 1f)));
            cloudShadow = Keep(CardRushDraw.Opaque("Storm_CloudShade", new Color(0.82f, 0.86f, 0.95f)));
            stormDark = Keep(CardRushDraw.Opaque("Storm_Dark", new Color(0.24f, 0.26f, 0.33f)));
            stormBolt = Keep(CardRushDraw.Opaque("Storm_Bolt", new Color(0.65f, 0.9f, 1f)));
            gullWhite = Keep(CardRushDraw.Opaque("Storm_Gull", new Color(0.98f, 0.98f, 0.96f)));
            gullGrey = Keep(CardRushDraw.Opaque("Storm_GullWing", new Color(0.62f, 0.66f, 0.72f)));
            beak = Keep(CardRushDraw.Opaque("Storm_Beak", new Color(1f, 0.7f, 0.15f)));
            updraftMat = Keep(CardRushDraw.Transparent("Storm_Updraft", new Color(0.75f, 0.95f, 1f, 0.22f)));
            starMat = Keep(CardRushDraw.Opaque("Storm_Star", new Color(1f, 0.88f, 0.3f)));
            if (stormBolt.HasProperty("_RimStrength")) stormBolt.SetFloat("_RimStrength", 1f);

            BuildCushions();
            BuildClouds();
            BuildGulls();
            BuildUpdrafts();
            BuildSwirl();
        }

        private T Keep<T>(T o) where T : UnityEngine.Object { owned.Add(o); return o; }

        // ---------------- 짓기 ----------------

        private void BuildCushions()
        {
            for (int i = 0; i < field.Cushions.Count; i++)
            {
                CloudCushion c = field.Cushions[i];
                var node = CardRushDraw.Node("Cushion" + i, root.transform);
                node.transform.position = c.Top;
                //  그림은 판정(윗면 원판) 아래·안쪽에만 — 덩이 윗면이 판정 높이(0)를 넘지 않고 가로로 반지름을 넘지 않는다.
                var rng = new System.Random(i * 31 + 7);
                float mainHalfHeight = c.Radius * 0.95f * 0.7f;
                Puff(node.transform, Vector3.down * mainHalfHeight, c.Radius * 0.95f, 0.7f, cloudWhite);
                for (int k = 0; k < 7; k++)
                {
                    float a = (float)(k * Math.PI * 2 / 7 + rng.NextDouble() * 0.4);
                    float s = c.Radius * (0.3f + (float)rng.NextDouble() * 0.1f);
                    float r = c.Radius - s - 0.05f;
                    Puff(node.transform, new Vector3(Mathf.Cos(a) * r, -s * 0.75f - 0.05f, Mathf.Sin(a) * r), s, 0.75f, k % 3 == 0 ? cloudShadow : cloudWhite);
                }
                cushions.Add(node.transform);
            }
        }

        private static GameObject Puff(Transform parent, Vector3 at, float radius, float flatten, Material m)
        {
            var go = CardRushDraw.Primitive(PrimitiveType.Sphere, "Puff", parent, m);
            go.transform.localPosition = at;
            go.transform.localScale = new Vector3(radius * 2f, radius * 2f * flatten, radius * 2f);
            return go;
        }

        private void BuildClouds()
        {
            for (int i = 0; i < field.Clouds.Count; i++)
            {
                StormCloud c = field.Clouds[i];
                var node = CardRushDraw.Node("StormCloud" + i, root.transform);
                var rng = new System.Random(i * 17 + 3);
                //  판정 공(반지름 R) 안에 덩이를 모은다 — 덩이 끝이 R을 넘지 않게.
                Puff(node.transform, Vector3.zero, c.Radius * 0.8f, 0.85f, stormDark);
                for (int k = 0; k < 5; k++)
                {
                    float a = (float)(k * Math.PI * 2 / 5 + rng.NextDouble());
                    float s = c.Radius * 0.45f;
                    float r = c.Radius - s - 0.05f;
                    Puff(node.transform, new Vector3(Mathf.Cos(a) * r, (float)(rng.NextDouble() - 0.5) * s, Mathf.Sin(a) * r), s, 0.9f, stormDark);
                }
                //  안에서 번쩍이는 번개 줄기
                var bolt = CardRushDraw.Box("Bolt", node.transform, Vector3.zero, new Vector3(0.25f, c.Radius * 1.5f, 0.25f), stormBolt);
                bolt.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
                bolt.SetActive(false);
                stormBolts.Add(bolt);
                stormClouds.Add(node.transform);
            }
        }

        private void BuildGulls()
        {
            for (int f = 0; f < field.Flocks.Count; f++)
            {
                GullFlock flock = field.Flocks[f];
                var birds = new (Transform, Transform, Transform)[flock.Count];
                for (int b = 0; b < flock.Count; b++)
                {
                    birds[b] = MakeGull(root.transform, 1f);   // 날개 끝 1.8m = 판정 반지름
                }
                gulls.Add(birds);
            }
        }

        private (Transform body, Transform wingL, Transform wingR) MakeGull(Transform parent, float scale)
        {
            var node = CardRushDraw.Node("Gull", parent);
            node.transform.localScale = Vector3.one * scale;
            var body = CardRushDraw.Primitive(PrimitiveType.Sphere, "Body", node.transform, gullWhite);
            body.transform.localScale = new Vector3(0.7f, 0.6f, 1.6f);
            var head = CardRushDraw.Primitive(PrimitiveType.Sphere, "Head", node.transform, gullWhite);
            head.transform.localPosition = new Vector3(0f, 0.25f, 0.75f);
            head.transform.localScale = Vector3.one * 0.5f;
            CardRushDraw.Box("Beak", node.transform, new Vector3(0f, 0.2f, 1.1f), new Vector3(0.12f, 0.1f, 0.35f), beak);
            CardRushDraw.Box("EyeL", node.transform, new Vector3(-0.2f, 0.35f, 0.9f), Vector3.one * 0.08f, stormDark);
            CardRushDraw.Box("EyeR", node.transform, new Vector3(0.2f, 0.35f, 0.9f), Vector3.one * 0.08f, stormDark);
            var wl = CardRushDraw.Node("WingL", node.transform);
            CardRushDraw.Box("W", wl.transform, new Vector3(-0.9f, 0f, 0f), new Vector3(1.8f, 0.06f, 0.6f), gullGrey);
            var wr = CardRushDraw.Node("WingR", node.transform);
            CardRushDraw.Box("W", wr.transform, new Vector3(0.9f, 0f, 0f), new Vector3(1.8f, 0.06f, 0.6f), gullGrey);
            return (node.transform, wl.transform, wr.transform);
        }

        private void BuildUpdrafts()
        {
            for (int i = 0; i < field.Updrafts.Count; i++)
            {
                Updraft u = field.Updrafts[i];
                var node = CardRushDraw.Node("Updraft" + i, root.transform);
                node.transform.position = u.Base;
                //  반투명 기둥 — 판정 기둥과 같은 반지름·높이.
                var column = CardRushDraw.Primitive(PrimitiveType.Cylinder, "Column", node.transform, updraftMat);
                column.transform.localPosition = Vector3.up * (u.Height * 0.5f);
                column.transform.localScale = new Vector3(u.Radius * 2f, u.Height * 0.5f, u.Radius * 2f);
                var ring = CardRushDraw.Node("Spin", node.transform);
                var motes = new Transform[36];
                for (int k = 0; k < motes.Length; k++)
                {
                    var m = CardRushDraw.Box("Mote", ring.transform, Vector3.zero, new Vector3(0.12f, 1.2f, 0.12f), cloudWhite);
                    motes[k] = m.transform;
                }
                updrafts.Add((ring.transform, motes));
            }
        }

        private void BuildSwirl()
        {
            var wood = Keep(CardRushDraw.Opaque("Storm_DebrisWood", new Color(0.55f, 0.36f, 0.2f)));
            var leaf = Keep(CardRushDraw.Opaque("Storm_DebrisLeaf", new Color(0.42f, 0.7f, 0.32f)));
            var straw = Keep(CardRushDraw.Opaque("Storm_DebrisStraw", new Color(0.85f, 0.72f, 0.38f)));
            var rng = new System.Random(77);
            for (int i = 0; i < 220; i++)
            {
                int k = i % 3;
                Vector3 size = k == 0 ? new Vector3(1.8f, 0.15f, 0.45f) : k == 1 ? new Vector3(0.7f, 0.05f, 0.5f) : new Vector3(2.4f, 0.12f, 0.2f);
                var go = CardRushDraw.Box("Debris", root.transform, Vector3.zero, size, k == 0 ? wood : k == 1 ? leaf : straw);
                swirl.Add((go.transform, 50f + (float)rng.NextDouble() * 8f, (float)(rng.NextDouble() * Math.PI * 2), (float)rng.NextDouble() * 160f - 80f,
                           0.25f + (float)rng.NextDouble() * 0.35f, (float)rng.NextDouble() * 6f));
            }
        }

        // ---------------- 매 프레임 ----------------

        public void LateTick()
        {
            if (root == null || runner?.tickUpdater == null)
            {
                return;
            }
            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return;
            }
            renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;
            float t = Time.time;

            for (int i = 0; i < stormClouds.Count; i++)
            {
                stormClouds[i].position = field.Clouds[i].PositionAt(renderTick);
                stormClouds[i].rotation = Quaternion.Euler(0f, t * 20f + i * 40f, 0f);
                stormBolts[i].SetActive(Mathf.Repeat(t * 0.7f + i * 0.37f, 1f) < 0.08f);
            }

            for (int f = 0; f < gulls.Count; f++)
            {
                GullFlock flock = field.Flocks[f];
                Quaternion face = Quaternion.LookRotation(flock.Heading, Vector3.up);
                var birds = gulls[f];
                for (int b = 0; b < birds.Length; b++)
                {
                    Vector3 p = flock.BirdAt(b, renderTick);
                    bool inside = new Vector2(p.x, p.z).magnitude <= StormHazardSystem.WallHide;
                    birds[b].body.gameObject.SetActive(inside);
                    if (inside == false) continue;
                    birds[b].body.SetPositionAndRotation(p, face);
                    float flap = Mathf.Sin(t * 12f + b) * 35f;
                    birds[b].wingL.localRotation = Quaternion.Euler(0f, 0f, flap);
                    birds[b].wingR.localRotation = Quaternion.Euler(0f, 0f, -flap);
                }
            }

            for (int i = 0; i < updrafts.Count; i++)
            {
                Updraft u = field.Updrafts[i];
                var (ring, motes) = updrafts[i];
                for (int k = 0; k < motes.Length; k++)
                {
                    float h = Mathf.Repeat(t * 9f + k * u.Height / motes.Length, u.Height);
                    float a = t * 3f + k * 2.4f + h * 0.15f;
                    float r = u.Radius * (0.35f + 0.55f * ((k * 0.37f) % 1f));
                    motes[k].localPosition = new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r);
                    motes[k].localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 20f);
                }
            }

            for (int i = 0; i < cushions.Count; i++)
            {
                cushionSquash[i] = Mathf.Max(0f, cushionSquash[i] - Time.deltaTime * 3f);
                float s = 1f + Mathf.Sin(cushionSquash[i] * 14f) * cushionSquash[i] * 0.35f;
                cushions[i].localScale = new Vector3(1f / s, s, 1f / s);
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                float cy = cam.transform.position.y;
                for (int i = 0; i < swirl.Count; i++)
                {
                    var d = swirl[i];
                    float a = d.a + t * d.w * 0.4f;
                    float y = cy + Mathf.Repeat(d.y - t * 3f + 80f, 160f) - 80f;
                    d.t.position = new Vector3(Mathf.Cos(a) * d.r, y, Mathf.Sin(a) * d.r);
                    d.t.rotation = Quaternion.Euler(t * 40f * d.spin, t * 30f * d.spin, t * 25f);
                }
            }

            //  문 장식은 태풍의 눈에서만(다른 스카이다이브 맵은 그대로).
            if (field.IsEmpty == false) DressObstacles();
            DetectHits(t);
        }

        //  몸마다 "방금 무엇에 닿았나"를 읽어 글을 띄운다. 되감기로 같은 틱을 다시 돌아도 한 번만.
        private readonly List<string> gone = new List<string>();

        private void DetectHits(float now)
        {
            //  사라진 몸의 별·머리 위 갈매기를 치운다 — 안 치우면 마지막 자리에 떠 있다.
            gone.Clear();
            foreach (var id in bodies.Keys)
            {
                if (entityRegistry.Get(id) == null) gone.Add(id);
            }
            foreach (var id in gone)
            {
                var b = bodies[id];
                if (b.Stars != null) UnityEngine.Object.Destroy(b.Stars);
                if (b.Gull != null) UnityEngine.Object.Destroy(b.Gull);
                bodies.Remove(id);
            }

            foreach (var entity in entityRegistry.All)
            {
                var storm = entity.Get<StormState>();
                if (storm == null)
                {
                    continue;
                }
                if (bodies.TryGetValue(entity.Id, out var body) == false)
                {
                    body = new Body { ShownHitTick = storm.LastHitTick };
                    bodies[entity.Id] = body;
                }
                //  되감기로 맞은 틱이 앞당겨져도 다시 띄운다(같은 틱만 건너뛴다).
                if (storm.LastHitTick == body.ShownHitTick || storm.LastHitTick < 0)
                {
                    continue;
                }
                body.ShownHitTick = storm.LastHitTick;
                Vector3 at = GameFramework.World.EntityMotionExtensions.GetPosition(entity) + Vector3.up * 2.4f;
                switch (storm.LastHitKind)
                {
                    case StormHazardKind.CloudCushion:
                        AddPop("뽀잉!", at, new Color(1f, 0.55f, 0.85f), 1.3f);
                        SquashNearestCushion(at);
                        break;
                    case StormHazardKind.StormCloud:
                        AddPop("지지직!", at, new Color(0.55f, 0.9f, 1f), 1.4f);
                        body.TumbleAxis = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0.2f, UnityEngine.Random.Range(-1f, 1f)).normalized;
                        body.TumbleStart = now;
                        break;
                    case StormHazardKind.GullFlock:
                        AddPop("끼룩! 푸드덕!", at, Color.white, 1.3f);
                        body.TumbleAxis = Vector3.forward;
                        body.TumbleStart = now;
                        break;
                    case StormHazardKind.Updraft:
                        AddPop("둥실~ 빙글빙글", at, new Color(0.7f, 0.95f, 1f), 1.2f);
                        break;
                }
            }
        }

        private void SquashNearestCushion(Vector3 at)
        {
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < cushions.Count; i++)
            {
                float d = (cushions[i].position - at).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best >= 0 && best < cushionSquash.Length) cushionSquash[best] = 1f;
        }

        private void AddPop(string text, Vector3 at, Color color, float size)
        {
            if (pops.Count >= 10)
            {
                RemovePop(0);
            }
            var label = CardRushWorldLabel.Create("StormPop", root.transform);
            if (label == null)
            {
                return;
            }
            pops.Add(new Pop { Label = label, Text = text, Color = color, At = at, Born = Time.time, Size = size });
        }

        private void RemovePop(int index)
        {
            pops[index].Label?.Destroy();
            pops.RemoveAt(index);
        }

        /// <summary>보간기·자세 기울기가 몸을 다 세운 뒤(3060) — 굴러 돌기·떠오르며 돌기·별·머리 위 갈매기·글.</summary>
        public void ApplyBodyFx()
        {
            float now = Time.time;
            long tick = (long)Math.Floor(renderTick);
            foreach (var entity in entityRegistry.All)
            {
                var storm = entity.Get<StormState>();
                if (storm == null || actorRegistry.TryGet(entity.Id, out var actor) == false || actor == null || actor.visualGameObject == null)
                {
                    continue;
                }
                if (bodies.TryGetValue(entity.Id, out var body) == false)
                {
                    continue;
                }
                Transform visual = actor.visualGameObject.transform;
                Vector3 head = visual.position + Vector3.up * (config.BodyHeight + 0.35f);

                bool stunned = tick < storm.StunEndTick;
                bool lifting = tick < storm.LiftEndTick;
                if (stunned)
                {
                    float angle = (now - body.TumbleStart) * 600f;
                    visual.rotation = Quaternion.AngleAxis(angle, body.TumbleAxis) * visual.rotation;
                }
                else if (lifting)
                {
                    body.LiftYaw += Time.deltaTime * 900f;
                    visual.rotation = Quaternion.Euler(0f, body.LiftYaw, 0f) * visual.rotation;
                }

                bool showStars = stunned && storm.LastHitKind == StormHazardKind.StormCloud;
                if (showStars && body.Stars == null)
                {
                    body.Stars = MakeStars();
                }
                if (body.Stars != null)
                {
                    body.Stars.SetActive(showStars);
                    if (showStars)
                    {
                        body.Stars.transform.SetPositionAndRotation(head + Vector3.up * 1.1f, Quaternion.Euler(0f, now * 300f, 0f));   //  이름표 위 — 낙하 카메라 거리에서도 보이게
                    }
                }

                bool showGull = tick < storm.GullOnHeadEndTick;
                if (showGull && body.Gull == null)
                {
                    body.Gull = MakeGull(root.transform, 0.45f).body.gameObject;
                }
                if (body.Gull != null)
                {
                    body.Gull.SetActive(showGull);
                    if (showGull)
                    {
                        float hop = Mathf.Abs(Mathf.Sin(now * 9f)) * 0.12f;
                        body.Gull.transform.SetPositionAndRotation(head + Vector3.up * (0.1f + hop),
                            Quaternion.Euler(0f, visual.eulerAngles.y + Mathf.Sin(now * 2f) * 50f, 0f));
                    }
                }
            }

            Camera cam = Camera.main;
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                Pop p = pops[i];
                float age = now - p.Born;
                if (age > 1.2f)
                {
                    RemovePop(i);
                    continue;
                }
                float grow = age < 0.12f ? 0.6f + age / 0.12f * 0.6f : 1.2f - Mathf.Min(0.2f, (age - 0.12f) * 2f);
                float alpha = age < 0.7f ? 1f : 1f - (age - 0.7f) / 0.5f;
                p.Label.Show(p.Text, p.At + Vector3.up * age * 2f, p.Color, alpha, p.Size * grow, cam);
            }
        }

        private GameObject MakeStars()
        {
            var node = CardRushDraw.Node("StunStars", root.transform);
            for (int k = 0; k < 3; k++)
            {
                float a = k * Mathf.PI * 2f / 3f;
                var s = CardRushDraw.Box("Star", node.transform, new Vector3(Mathf.Cos(a) * 1.3f, 0f, Mathf.Sin(a) * 1.3f), Vector3.one * 0.6f, starMat);
                s.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
            }
            return node;
        }

        // ---------------- 큰 지형물 옷: 태풍에 휩쓸린 잔해 ----------------

        private enum DebrisLook { Planks, Raft, Thatch, Sail, Tree, Tiles, Door }

        private static DebrisLook LookFor(string name, int index)
        {
            if (name.Contains("1440")) return DebrisLook.Planks;    // 부서진 나무 다리
            if (name.Contains("760")) return DebrisLook.Raft;       // 통나무 뗏목
            if (name.Contains("740")) return DebrisLook.Thatch;     // 초가지붕
            if (name.Contains("1020")) return DebrisLook.Sail;      // 풍차 날개
            if (name.Contains("330")) return DebrisLook.Tree;       // 뽑힌 나무
            if (name.StartsWith("Iris")) return DebrisLook.Tiles;   // 날아온 기와지붕 조각
            if (name.StartsWith("Door")) return DebrisLook.Door;    // 창고 문짝
            return (DebrisLook)(index % 7);
        }

        //  문 둘레의 고정 판(문짝이 아니라 맵 바탕) — 이름으로 찾는다.
        private static readonly string[] SlabNames = { "DoorSlab_Ring", "DoorSlab_N", "DoorSlab_S", "DoorSlab_E", "DoorSlab_W" };
        private int slabsFound;

        private void DressObstacles()
        {
            if (slabsFound < SlabNames.Length)
            {
                slabsFound = 0;
                foreach (var n in SlabNames)
                {
                    var go = GameObject.Find(n);
                    if (go != null) { Dress(go, 6, DebrisLook.Door); slabsFound++; }
                }
            }
            int index = 0;
            if (obstacles != null)
            {
                foreach (var o in obstacles.All)
                {
                    //  큰 장애물은 시뮬이 첫 틱에 잔해 판정으로 갈아 끼운다(StormDebrisRebuilder) — 그 뒤에 그 조각들에 그림을 입힌다.
                    if (o is Component c && c != null && c.GetComponent<StormDebrisBuilt>() != null) DressDebris(c.gameObject);
                    index++;
                }
            }
            if (doors != null)
            {
                foreach (var d in doors.All)
                {
                    if (d != null) Dress(d.gameObject, index);
                    index++;
                }
            }
        }

        private void Dress(GameObject target, int index, DebrisLook? forced = null)
        {
            if (target == null || dressed.Add(target.GetInstanceID()) == false)
            {
                return;
            }
            DebrisLook look = forced ?? LookFor(target.name, index);
            var (baseColor, trimColor) = ColorsOf(look);
            var baseMat = Keep(CardRushDraw.Opaque("Debris_" + look, baseColor));
            var trimMat = Keep(CardRushDraw.Opaque("DebrisTrim_" + look, trimColor));
            var leafMat = Keep(CardRushDraw.Opaque("DebrisLeaf", new Color(0.36f, 0.66f, 0.3f)));

            foreach (var r in target.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.GetComponentInParent<SkydiveStormDecor>() != null) continue;
                var mats = r.sharedMaterials;
                for (int k = 0; k < mats.Length; k++) mats[k] = baseMat;
                r.sharedMaterials = mats;
            }
            foreach (var col in target.GetComponentsInChildren<Collider>(true))
            {
                DecorateCollider(col, look, trimMat, leafMat, index, target.transform.position);
            }
        }

        private static (Color, Color) ColorsOf(DebrisLook look)
        {
            switch (look)
            {
                case DebrisLook.Planks: return (new Color(0.66f, 0.46f, 0.27f), new Color(0.38f, 0.24f, 0.13f));
                case DebrisLook.Raft: return (new Color(0.5f, 0.33f, 0.18f), new Color(0.62f, 0.43f, 0.24f));
                case DebrisLook.Thatch: return (new Color(0.86f, 0.71f, 0.36f), new Color(0.66f, 0.51f, 0.22f));
                case DebrisLook.Sail: return (new Color(0.95f, 0.91f, 0.8f), new Color(0.46f, 0.3f, 0.17f));
                case DebrisLook.Tree: return (new Color(0.33f, 0.6f, 0.27f), new Color(0.43f, 0.29f, 0.16f));
                case DebrisLook.Tiles: return (new Color(0.62f, 0.27f, 0.2f), new Color(0.42f, 0.17f, 0.13f));
                default: return (new Color(0.58f, 0.4f, 0.24f), new Color(0.3f, 0.3f, 0.33f));
            }
        }

        //  콜라이더 윗면을 격자로 짚어(위에서 쏜 레이) 그 안쪽에만 장식을 놓는다 — 장식이 판정 밖으로 안 나간다.
        //  장식은 콜라이더의 자식이라 판이 돌고 날개가 움직여도 같이 간다.
        private void DecorateCollider(Collider col, DebrisLook look, Material trim, Material leaf, int seed, Vector3 hub)
        {
            Bounds b = col.bounds;
            if (b.size.x < 1f || b.size.z < 1f)
            {
                return;
            }
            const float step = 1.2f;
            int nx = Mathf.Min(220, Mathf.CeilToInt(b.size.x / step));
            int nz = Mathf.Min(220, Mathf.CeilToInt(b.size.z / step));
            var hits = new bool[nx, nz];
            var tops = new float[nx, nz];
            for (int ix = 0; ix < nx; ix++)
            {
                for (int iz = 0; iz < nz; iz++)
                {
                    Vector3 from = new Vector3(b.min.x + (ix + 0.5f) * step, b.max.y + 1f, b.min.z + (iz + 0.5f) * step);
                    if (col.Raycast(new Ray(from, Vector3.down), out RaycastHit hit, b.size.y + 2f) && hit.normal.y > 0.7f)
                    {
                        hits[ix, iz] = true;
                        tops[ix, iz] = hit.point.y;
                    }
                }
            }

            var decor = new GameObject("StormDecor");
            decor.AddComponent<SkydiveStormDecor>();
            decor.transform.SetParent(col.transform, false);
            Transform parent = decor.transform;
            var rng = new System.Random(seed * 101 + col.GetInstanceID());

            Vector3 W(int ix, int iz, float lift) => new Vector3(b.min.x + (ix + 0.5f) * step, tops[ix, iz] + lift, b.min.z + (iz + 0.5f) * step);

            void Strip(Vector3 a, Vector3 c, float width, float height, Material m)
            {
                Vector3 mid = (a + c) * 0.5f;
                Vector3 d = c - a;
                if (d.magnitude < 0.3f) return;
                var go = CardRushDraw.Primitive(PrimitiveType.Cube, "Strip", parent, m);
                go.transform.SetPositionAndRotation(mid, Quaternion.LookRotation(new Vector3(d.x, 0f, d.z), Vector3.up));
                go.transform.localScale = Divide(new Vector3(width, height, d.magnitude), parent.lossyScale);
            }

            void Runs(bool alongX, int every, float width, float height, Material m, float lift)
            {
                int outer = alongX ? nz : nx;
                int inner = alongX ? nx : nz;
                for (int o = 0; o < outer; o += every)
                {
                    int start = -1;
                    for (int i = 0; i <= inner; i++)
                    {
                        bool on = i < inner && (alongX ? hits[i, o] : hits[o, i]);
                        if (on && start < 0) start = i;
                        if ((on == false) && start >= 0)
                        {
                            int end = i - 1;
                            Vector3 a = alongX ? W(start, o, lift) : W(o, start, lift);
                            Vector3 c = alongX ? W(end, o, lift) : W(o, end, lift);
                            Strip(a, c, width, height, m);
                            start = -1;
                        }
                    }
                }
            }

            //  축(가운데)에서 가장 먼 칸까지 한 줄 — 날개·가지가 뻗은 방향을 따른다. 곁가지는 줄기에서 옆 칸으로.
            void Spine(float width, float height, int twigs)
            {
                int fx = -1, fz = -1;
                float far = -1f;
                for (int ix = 0; ix < nx; ix++)
                    for (int iz = 0; iz < nz; iz++)
                    {
                        if (hits[ix, iz] == false) continue;
                        Vector3 p = W(ix, iz, 0f);
                        float d = new Vector2(p.x - hub.x, p.z - hub.z).sqrMagnitude;
                        if (d > far) { far = d; fx = ix; fz = iz; }
                    }
                if (fx < 0) return;
                Vector3 tip = W(fx, fz, height * 0.5f);
                Vector3 axisRoot = new Vector3(hub.x, tip.y, hub.z);
                Vector3 axis = tip - axisRoot;
                Vector3 start = axisRoot + axis.normalized * Mathf.Min(axis.magnitude * 0.15f, 4f);
                Strip(start, tip, width, height, trim);
                for (int k = 0; k < twigs; k++)
                {
                    float t = 0.25f + 0.65f * k / Mathf.Max(1, twigs - 1);
                    Vector3 at = Vector3.Lerp(start, tip, t);
                    Vector3 side = Vector3.Cross(axis, Vector3.up).normalized * ((k % 2 == 0 ? 1f : -1f) * (2f + (float)rng.NextDouble() * 3f));
                    Vector3 end = at + side + axis.normalized * 2.5f;
                    int ex = Mathf.Clamp(Mathf.FloorToInt((end.x - b.min.x) / step), 0, nx - 1);
                    int ez = Mathf.Clamp(Mathf.FloorToInt((end.z - b.min.z) / step), 0, nz - 1);
                    if (hits[ex, ez]) Strip(at, new Vector3(end.x, at.y, end.z), width * 0.45f, height * 0.6f, trim);
                }
            }

            switch (look)
            {
                case DebrisLook.Planks:
                case DebrisLook.Door:
                    Runs(true, 2, 0.15f, 0.12f, trim, 0.04f);       // 판자 이음
                    Runs(false, 9, 0.5f, 0.18f, trim, 0.06f);       // 가로 덧댄 각목
                    break;
                case DebrisLook.Raft:
                    Runs(true, 1, 1.0f, 0.5f, trim, 0.05f);         // 통나무
                    Runs(false, 12, 0.3f, 0.6f, Keep(CardRushDraw.Opaque("Rope", new Color(0.85f, 0.75f, 0.5f))), 0.1f);   // 새끼줄 묶음
                    break;
                case DebrisLook.Thatch:
                    Runs(true, 2, 0.9f, 0.3f, trim, 0.1f);           // 짚단 줄
                    break;
                case DebrisLook.Sail:
                    Runs(true, 4, 0.35f, 0.25f, trim, 0.08f);        // 나무 격자
                    Runs(false, 4, 0.35f, 0.25f, trim, 0.08f);
                    Spine(1.2f, 0.5f, 0);                             // 가운데 큰 살
                    break;
                case DebrisLook.Tiles:
                    Runs(true, 2, 0.5f, 0.2f, trim, 0.05f);          // 기와 줄
                    break;
                case DebrisLook.Tree:
                    Spine(1.6f, 0.9f, 7);                             // 굵은 줄기 + 곁가지
                    break;
            }

            //  가장자리에 삐죽한 부서진 조각·잎 — 이웃 칸이 비어 있는 칸만(가장자리), 안쪽으로 살짝.
            for (int ix = 1; ix < nx - 1; ix++)
            {
                for (int iz = 1; iz < nz - 1; iz++)
                {
                    if (hits[ix, iz] == false) continue;
                    bool edge = !hits[ix - 1, iz] || !hits[ix + 1, iz] || !hits[ix, iz - 1] || !hits[ix, iz + 1];
                    if (look == DebrisLook.Tree)
                    {
                        if (rng.NextDouble() < 0.06)
                        {
                            var go = CardRushDraw.Primitive(PrimitiveType.Sphere, "Leaf", parent, leaf);
                            go.transform.position = W(ix, iz, 0.25f);
                            float s = 1.2f + (float)rng.NextDouble() * 1.2f;
                            go.transform.localScale = Divide(new Vector3(s, 0.6f, s), parent.lossyScale);
                        }
                        continue;
                    }
                    if (edge && rng.NextDouble() < 0.18)
                    {
                        var go = CardRushDraw.Primitive(PrimitiveType.Cube, "Splinter", parent, trim);
                        go.transform.SetPositionAndRotation(W(ix, iz, 0.25f), Quaternion.Euler((float)rng.NextDouble() * 40f - 20f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 40f - 20f));
                        go.transform.localScale = Divide(new Vector3(0.25f, 0.5f, 1.1f), parent.lossyScale);
                    }
                }
            }
        }

        // ---------------- 잔해 판정 조각 그림: 통나무·거목·판자 ----------------

        private Material[] bark, plankMats, leafMats;
        private Material cutWood, rope, earth, rootMat;

        private void EnsureDebrisMaterials()
        {
            if (bark != null) return;
            bark = new[]
            {
                Keep(CardRushDraw.Opaque("Debris_Bark0", new Color(0.42f, 0.28f, 0.16f))),
                Keep(CardRushDraw.Opaque("Debris_Bark1", new Color(0.50f, 0.34f, 0.20f))),
                Keep(CardRushDraw.Opaque("Debris_Bark2", new Color(0.36f, 0.25f, 0.17f))),
            };
            plankMats = new[]
            {
                Keep(CardRushDraw.Opaque("Debris_Plank0", new Color(0.66f, 0.48f, 0.30f))),
                Keep(CardRushDraw.Opaque("Debris_Plank1", new Color(0.56f, 0.40f, 0.25f))),
                Keep(CardRushDraw.Opaque("Debris_Tile", new Color(0.62f, 0.27f, 0.20f))),
                Keep(CardRushDraw.Opaque("Debris_Painted", new Color(0.36f, 0.52f, 0.62f))),
                Keep(CardRushDraw.Opaque("Debris_Thatch", new Color(0.82f, 0.68f, 0.36f))),
            };
            leafMats = new[]
            {
                Keep(CardRushDraw.Opaque("Debris_Leaf0", new Color(0.33f, 0.58f, 0.27f))),
                Keep(CardRushDraw.Opaque("Debris_Leaf1", new Color(0.42f, 0.68f, 0.30f))),
                Keep(CardRushDraw.Opaque("Debris_Leaf2", new Color(0.26f, 0.48f, 0.25f))),
            };
            cutWood = Keep(CardRushDraw.Opaque("Debris_Cut", new Color(0.86f, 0.70f, 0.47f)));
            rope = Keep(CardRushDraw.Opaque("Debris_Rope", new Color(0.86f, 0.76f, 0.50f)));
            earth = Keep(CardRushDraw.Opaque("Debris_Earth", new Color(0.38f, 0.27f, 0.19f)));
            rootMat = Keep(CardRushDraw.Opaque("Debris_Root", new Color(0.30f, 0.21f, 0.14f)));
        }

        //  그림은 판정 상자 안에만 — 통나무 원기둥 지름은 상자 두께·폭 이하, 잎 덩이도 상자 안.
        private void DressDebris(GameObject obstacle)
        {
            if (dressed.Add(obstacle.GetInstanceID()) == false) return;
            EnsureDebrisMaterials();
            var tags = obstacle.GetComponentsInChildren<StormDebrisTag>(true);
            for (int i = 0; i < tags.Length; i++)
            {
                var tag = tags[i];
                var box = tag.GetComponent<BoxCollider>();
                if (box == null) continue;
                Vector3 size = box.size;
                var rng = new System.Random(i * 7919 + obstacle.name.Length * 31);
                Transform t = tag.transform;
                switch (tag.Kind)
                {
                    case StormDebrisKind.Log:
                        Log(t, size, Mathf.Min(size.y, size.z) * 0.96f, bark[rng.Next(bark.Length)], true, rng);
                        break;
                    case StormDebrisKind.Trunk:
                        Log(t, size, Mathf.Min(size.y, size.z) * 0.96f, bark[2], false, rng);
                        break;
                    case StormDebrisKind.Branch:
                        Log(t, size, Mathf.Min(size.y, size.z) * 0.85f, bark[0], false, rng);
                        break;
                    case StormDebrisKind.Crown:
                        for (int k = 0; k < 7; k++)
                        {
                            float sx = size.x * (0.35f + (float)rng.NextDouble() * 0.15f);
                            float sz = size.z * (0.3f + (float)rng.NextDouble() * 0.15f);
                            float sy = size.y * (0.7f + (float)rng.NextDouble() * 0.28f);
                            var at = new Vector3(((float)rng.NextDouble() - 0.5f) * (size.x - sx), ((float)rng.NextDouble() - 0.5f) * (size.y - sy), ((float)rng.NextDouble() - 0.5f) * (size.z - sz));
                            Shape(t, PrimitiveType.Sphere, at, Quaternion.identity, new Vector3(sx, sy, sz), leafMats[rng.Next(leafMats.Length)]);
                        }
                        break;
                    case StormDebrisKind.RootBall:
                        Shape(t, PrimitiveType.Sphere, Vector3.zero, Quaternion.identity, new Vector3(size.x * 0.95f, size.y * 0.9f, size.z * 0.95f), earth);
                        for (int k = 0; k < 9; k++)
                        {
                            float a = k * 40f + (float)rng.NextDouble() * 20f;
                            Quaternion q = Quaternion.Euler(0f, -a, (float)rng.NextDouble() * 30f - 15f);
                            Vector3 d = q * Vector3.right;
                            float len = size.x * 0.42f;
                            Shape(t, PrimitiveType.Cylinder, d * (len * 0.5f), q * Quaternion.Euler(0f, 0f, 90f), new Vector3(0.5f, len * 0.5f, 0.5f), rootMat);
                        }
                        break;
                    case StormDebrisKind.Plank:
                        //  대부분 나무색, 가끔 기와·칠한 판자·짚(날아온 집 조각)
                        int pick = rng.Next(10);
                        Material m = plankMats[pick < 4 ? 0 : pick < 8 ? 1 : 2 + rng.Next(3)];
                        Shape(t, PrimitiveType.Cube, Vector3.zero, Quaternion.identity, size * 0.97f, m);
                        for (int k = -1; k <= 1; k += 2)
                        {
                            Shape(t, PrimitiveType.Cube, new Vector3(0f, size.y * 0.47f, k * size.z * 0.25f), Quaternion.identity,
                                  new Vector3(size.x * 0.95f, 0.04f, 0.12f), bark[0]);
                        }
                        break;
                }
            }
        }

        //  상자 길이 방향(로컬 X)으로 누운 통나무. 끝은 잘린 나무색, 뗏목 통나무엔 새끼줄 묶음.
        private void Log(Transform parent, Vector3 size, float diameter, Material skin, bool lashed, System.Random rng)
        {
            Quaternion lie = Quaternion.Euler(0f, 0f, 90f);
            float len = size.x * 0.98f;
            Shape(parent, PrimitiveType.Cylinder, Vector3.zero, lie, new Vector3(diameter, len * 0.5f, diameter), skin);
            for (int e = -1; e <= 1; e += 2)
            {
                Shape(parent, PrimitiveType.Cylinder, new Vector3(e * (len * 0.5f - 0.03f), 0f, 0f), lie, new Vector3(diameter * 0.86f, 0.04f, diameter * 0.86f), cutWood);
            }
            if (lashed)
            {
                for (float x = -len * 0.5f + 4f + (float)rng.NextDouble() * 3f; x < len * 0.5f - 2f; x += 9f)
                {
                    Shape(parent, PrimitiveType.Cylinder, new Vector3(x, 0f, 0f), lie, new Vector3(diameter * 1.02f, 0.25f, diameter * 1.02f), rope);
                }
            }
        }

        private static void Shape(Transform parent, PrimitiveType type, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material m)
        {
            var go = CardRushDraw.Primitive(type, type.ToString(), parent, m);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;
        }

        private static Vector3 Divide(Vector3 a, Vector3 b) =>
            new Vector3(a.x / Mathf.Max(1e-4f, b.x), a.y / Mathf.Max(1e-4f, b.y), a.z / Mathf.Max(1e-4f, b.z));

        public void Dispose()
        {
            foreach (var p in pops) p.Label?.Destroy();
            pops.Clear();
            foreach (var o in owned)
            {
                if (o != null) UnityEngine.Object.Destroy(o);
            }
            owned.Clear();
        }
    }

    /// <summary>잔해 옷 장식의 표식 — 다시 입힐 때 장식 렌더러는 건너뛴다.</summary>
    public class SkydiveStormDecor : MonoBehaviour { }

    /// <summary>몸 연출을 보간기(0)·자세 기울기(3050) 뒤에 부른다 — LateTick은 그보다 먼저 돌아 지워진다.</summary>
    [DefaultExecutionOrder(3060)]
    public class SkydiveStormFxDriver : MonoBehaviour
    {
        public SkydiveStormView View { get; set; }

        private void LateUpdate()
        {
            View?.ApplyBodyFx();
        }
    }
}
