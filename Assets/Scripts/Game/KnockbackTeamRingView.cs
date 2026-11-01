using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 팀전에서 몸마다 발밑에 팀 색 원판을 깐다. 우리 팀 파랑, 상대 빨강(관전 중에도 "우리 팀"은 내 팀).
    /// 그려진 몸이 움직인 뒤에 놓아야 떨리지 않아 LateUpdate 발판(3100)으로 돈다.
    /// </summary>
    public class KnockbackTeamRingView : IStartable, System.IDisposable
    {
        private const float Diameter = 1.4f;
        private const float Height = 0.02f;
        private const float FallingY = -0.05f;
        private static readonly Color Blue = new Color(0.25f, 0.55f, 1f);
        private static readonly Color Red = new Color(1f, 0.3f, 0.25f);

        private readonly ActorRegistry actors;
        private readonly KnockbackClientState state;
        private readonly IGameDataStore gameDataStore;

        private readonly Dictionary<string, GameObject> rings = new Dictionary<string, GameObject>();
        private readonly List<string> gone = new List<string>();
        private Material blueMaterial;
        private Material redMaterial;
        private GameObject root;
        private KnockbackLateUpdateDriver driver;

        public KnockbackTeamRingView(ActorRegistry actors, KnockbackClientState state, IGameDataStore gameDataStore)
        {
            this.actors = actors;
            this.state = state;
            this.gameDataStore = gameDataStore;
        }

        /// <summary>둘 중 하나라도 팀을 모르면(0) 고리 없음.</summary>
        public static Color? RingColor(int myTeam, int bodyTeam)
        {
            if (myTeam == 0 || bodyTeam == 0) return null;
            return myTeam == bodyTeam ? Blue : Red;
        }

        /// <summary>몸이 바닥 아래로 내려가기 시작하면(관전 후보에서 빠지는 높이와 같다) 고리를 숨긴다.</summary>
        public static bool ShowsRing(float bodyY) => bodyY >= FallingY;

        public void Start()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            blueMaterial = new Material(shader) { color = Blue };
            redMaterial = new Material(shader) { color = Red };
            root = new GameObject("KnockbackTeamRings");
            driver = KnockbackLateUpdateDriver.Create("KnockbackTeamRingDriver", Place);
        }

        private void Place()
        {
            int myTeam = state.TeamCount >= 2 ? state.MyTeamOf(gameDataStore?.userEntityId, gameDataStore?.formerEntityId) : 0;
            foreach (var actor in actors.All)
            {
                string id = actor.entityId;
                var model = actor.visualGameObject;
                var color = model == null || state.IsEliminated(id) || !ShowsRing(model.transform.position.y)
                    ? null : RingColor(myTeam, state.TeamOf(id));
                rings.TryGetValue(id, out var ring);
                if (color == null)
                {
                    if (ring != null && ring.activeSelf) ring.SetActive(false);
                    continue;
                }
                if (ring == null)
                {
                    ring = Create(id);
                    rings[id] = ring;
                }
                if (!ring.activeSelf) ring.SetActive(true);
                ring.GetComponent<MeshRenderer>().sharedMaterial = color.Value == Blue ? blueMaterial : redMaterial;
                var p = model.transform.position;
                ring.transform.position = new Vector3(p.x, Height, p.z);
            }

            // 몸이 없어진 고리는 치운다.
            gone.Clear();
            foreach (var pair in rings)
            {
                if (!actors.Contains(pair.Key)) gone.Add(pair.Key);
            }
            for (int i = 0; i < gone.Count; i++)
            {
                if (rings[gone[i]] != null) Object.Destroy(rings[gone[i]]);
                rings.Remove(gone[i]);
            }
        }

        private GameObject Create(string id)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = $"TeamRing_{id}";
            // 보이기만 한다 — 충돌이 있으면 몸 이동을 막는다.
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root.transform, false);
            // 원기둥 기본형은 지름 1, 높이 2 — 납작하게 눌러 원판으로 쓴다.
            go.transform.localScale = new Vector3(Diameter, 0.005f, Diameter);
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        public void Dispose()
        {
            if (driver != null) Object.Destroy(driver.gameObject);
            if (root != null) Object.Destroy(root);
            if (blueMaterial != null) Object.Destroy(blueMaterial);
            if (redMaterial != null) Object.Destroy(redMaterial);
            rings.Clear();
        }
    }
}
