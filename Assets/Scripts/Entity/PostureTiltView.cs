using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 자세(대자/다이브/패러세일)를 몸의 기울기로 보여 준다. 속도만 바뀌면 화면에서는 아무 일도
    /// 일어나지 않는 것처럼 보이므로, 실루엣이 달라져야 무슨 자세인지 읽힌다.
    ///
    /// <b>왜 별도 컴포넌트인가:</b> 몸의 회전은 매 프레임 보간기(Predicted/Extrapolated/Snapshot
    /// EntityInterpolator)가 <c>LateUpdate</c>에서 월드 회전째로 덮어쓴다. 그래서 기울기를
    /// 공용 뷰의 <c>Update</c>에 두면 계산은 되는데 화면에는 절대 안 나온다. 실행 순서를 보간기
    /// 뒤로 밀어야 하는데, 공용 뷰의 순서를 바꾸면 다른 게임 모드의 애니메이션까지 같이 밀린다 —
    /// 그래서 순서가 필요한 이 연출만 떼어 냈다(스턴 연출 <see cref="StunAppearance"/>와 같은 자리).
    /// </summary>
    [DefaultExecutionOrder(3050)]   // 보간기(0) 뒤, 월드공간 UI(3100) 앞
    public class PostureTiltView : MonoBehaviour, ICleanup
    {
        // 젤다의 세 자세를 각도로 옮긴 것: 대자는 배를 살짝 아래로, 다이브는 머리부터 수직,
        // 패러세일은 매달린 것처럼 뒤로 눕는다. 셋이 서로 확실히 다른 각도여야 구분된다.
        private const float SpreadPitch = 25f;
        private const float DivePitch = 85f;
        private const float GlidePitch = -15f;

        // 기울기가 붙는 속도(초당 도). 자세는 즉시 바뀌어도 몸은 따라가는 데 시간이 걸리는 게
        // 자연스럽고, 입력이 튈 때 몸이 덜덜거리는 것도 막는다.
        private const float PitchDegreesPerSecond = 360f;

        [Inject] private GameFramework.World.EntityRegistry entityRegistry;

        private string entityId;
        private LOPEntityView view;

        // 지금 적용 중인 기울기. 트랜스폼에서 되읽지 않는 이유: localEulerAngles는 0~360으로
        // 정규화돼서 음수 각(GlidePitch=-15)이 345로 튀어나와 몸이 한 바퀴 돈다.
        private float currentPitch = SpreadPitch;

        public void SetEntityId(string entityId)
        {
            this.entityId = entityId;
        }

        public void Cleanup()
        {
            entityId = null;
        }

        private void LateUpdate()
        {
            if (entityId == null)
            {
                return;
            }

            if (view == null)
            {
                view = GetComponent<LOPEntityView>();
            }

            GameObject visual = view != null ? view.visualGameObject : null;
            if (visual == null)
            {
                return;
            }

            var worldEntity = entityRegistry.Get(entityId);
            var posture = worldEntity?.Get<Posture>();
            if (posture == null)
            {
                return;   // 자세 개념이 없는 게임 모드
            }

            // 자세 기울기는 *활공 중인* 몸의 연출이다. 걷거나(서 있음) 그냥 떨어지는 중이면
            // 똑바로 선다 — 접지만 보면, 뛰어오른 몸이나 선반에서 막 벗어난 몸이 대자(25°)로
            // 기운 채 그려진다. 젤다의 "선 채로 내려간다"가 이 자리다.
            bool posing = worldEntity.Get<MotionState>()?.Value == SkydiveMotionState.Skydiving;
            float targetPitch = posing
                ? (posture.Gliding ? GlidePitch : Mathf.Lerp(SpreadPitch, DivePitch, posture.Axis))
                : 0f;
            currentPitch = Mathf.MoveTowards(currentPitch, targetPitch, PitchDegreesPerSecond * Time.deltaTime);

            // 보간기가 이미 써 놓은 이 프레임의 방향(yaw)만 뽑아내고 그 위에 기울기를 얹는다.
            // yaw만 뽑으므로 이 줄이 여러 번 실행돼도 기울기가 누적되지 않는다.
            float yaw = visual.transform.eulerAngles.y;
            visual.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(currentPitch, 0f, 0f);
        }
    }
}
