using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 드럼통 그림. 눕힌 원기둥(길이 1.1 m)에 파랑 띠 두 줄을 두른 산업용 드럼 모양이다.
    /// 위치는 보간기가 월드에 써 준 값을 읽어 따라가고(드럼통은 예측하지 않는다),
    /// 축은 굴러가는 방향에 수직으로 맞추며, 간 거리만큼 굴린다.
    /// </summary>
    [DefaultExecutionOrder(200)]   // 보간기(LateUpdate)가 월드를 갱신한 뒤에 읽는다
    public class KnockbackBarrelView : MonoBehaviour
    {
        public const float Length = 1.1f;
        private const float DefaultRadius = 0.55f;
        private static readonly Color BodyRed = new Color(0.6f, 0.08f, 0.08f);
        private static readonly Color BandBlue = new Color(0.1f, 0.25f, 0.7f);

        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private IObjectResolver resolver;

        private string entityId;
        private float radius = DefaultRadius;
        private Transform body;
        private Material redMaterial;
        private Material blueMaterial;
        private Vector3 axis = Vector3.right;
        private float roll;

        public void SetEntityId(string entityId)
        {
            this.entityId = entityId;
        }

        private void Start()
        {
            // 반지름은 맵 설정에서 온다. 설정이 없는 씬이면 기본값으로 그린다.
            if (resolver != null && resolver.TryResolve<KnockbackConfig>(out var config) && config.Barrels.Count > 0)
            {
                radius = config.Barrels.Radius;
            }

            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            redMaterial = new Material(unlit) { color = BodyRed };
            blueMaterial = new Material(unlit) { color = BandBlue };

            body = new GameObject("BarrelBody").transform;
            body.SetParent(transform, false);
            float d = radius * 2f;
            // 기본 원기둥은 높이 2, 지름 1이라 y 스케일은 길이의 절반.
            CreateCylinder("Drum", body, redMaterial, new Vector3(d, Length * 0.5f, d), 0f);
            CreateCylinder("BandA", body, blueMaterial, new Vector3(d * 1.03f, 0.07f, d * 1.03f), Length * 0.3f);
            CreateCylinder("BandB", body, blueMaterial, new Vector3(d * 1.03f, 0.07f, d * 1.03f), -Length * 0.3f);
            body.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            var entity = entityId != null ? entityRegistry.Get(entityId) : null;
            if (entity == null || body == null)
            {
                return;
            }
            Vector3 position = GameFramework.World.EntityMotionExtensions.GetPosition(entity);
            Vector3 velocity = GameFramework.World.EntityMotionExtensions.GetVelocity(entity);

            axis = KnockbackBarrelPose.Axis(velocity, axis);
            roll += KnockbackBarrelPose.RollDegrees(velocity, radius, Time.deltaTime);

            body.gameObject.SetActive(true);
            body.SetPositionAndRotation(position + Vector3.up * radius, KnockbackBarrelPose.Rotation(axis, roll));
        }

        private void OnDestroy()
        {
            if (redMaterial != null) Destroy(redMaterial);
            if (blueMaterial != null) Destroy(blueMaterial);
        }

        private static void CreateCylinder(string name, Transform parent, Material material, Vector3 scale, float offsetAlongAxis)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, offsetAlongAxis, 0f);
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }
}
