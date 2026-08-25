using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 판치기 조준·타격(클라). 판 위 한 점을 누르고, 끌고, 뗀다 — 끈 방향이 수평 힘이고
    /// 누른 시간이 수직 힘이다. 예측은 하지 않는다(판치기는 서버가 굴린 물리를 보기만 한다).
    /// 조준선은 서버 왕복 없이 로컬로만 그린다.
    /// </summary>
    public class PanchigiStrikeInput : MonoBehaviour
    {
        [SerializeField] private Camera aimCamera;
        [SerializeField] private LineRenderer aimLine;

        [Inject] private IPlayerContext playerContext;
        [Inject] private LOP.MasterData.LOPMasterData masterData;
        [Inject] private PanchigiStateStore stateStore;

        private const int AimingPhase = 1;

        //  판만 맞힌다 — 동전을 눌러도 "판의 그 자리를 쳤다"로 읽어야 조작이 자연스럽다.
        //  static 필드 초기화자에서 LayerMask.GetMask를 부르면 Unity가 예외를 던진다
        //  (MonoBehaviour 생성자/필드 초기화자에서 금지) — Awake에서 인스턴스 필드로 채운다.
        private int BoardLayerMask;

        private bool aiming;
        private float pressTime;
        private Vector3 pressPoint;
        private Vector3 currentPoint;

        private void Awake()
        {
            BoardLayerMask = LayerMask.GetMask("Default");
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }
            SetAimLineVisible(false);
        }

        private void OnDisable()
        {
            //  조준 중에 꺼지면 조준선이 화면에 남고, 다음 켜질 때 절반쯤 조준된 상태로
            //  시작한다 — 꺼질 때 확실히 리셋한다.
            aiming = false;
            SetAimLineVisible(false);
        }

        private void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                return;   // 마우스도 터치도 없는 환경
            }

            if (aiming
                && (stateStore.Phase.CurrentValue != AimingPhase
                    || stateStore.CurrentEntityId.CurrentValue != playerContext.entityId))
            {
                //  조준하는 중에 차례가 넘어갔다 — 조준선이 화면에 남으면 안 된다.
                aiming = false;
                SetAimLineVisible(false);
            }

            if (pointer.press.wasPressedThisFrame)
            {
                BeginAim(pointer.position.ReadValue());
            }

            //  release를 held보다 먼저 본다 — 뗀 그 프레임엔 isPressed가 아직 true일 수 있어서,
            //  순서를 바꾸면 release가 held 분기에 먹혀 버린다(누르고 뗀 게 같은 프레임이면 탭이 씹힘).
            if (aiming && pointer.press.wasReleasedThisFrame)
            {
                EndAim(pointer.position.ReadValue());
            }
            else if (aiming && pointer.press.isPressed)
            {
                UpdateAim(pointer.position.ReadValue());
            }
        }

        private void BeginAim(Vector2 screenPosition)
        {
            //  내 차례가 아니면 조준을 시작하지 않는다 — 조준선이 안 뜨는 것이 곧 안내다.
            if (stateStore.Phase.CurrentValue != AimingPhase
                || stateStore.CurrentEntityId.CurrentValue != playerContext.entityId)
            {
                return;
            }

            if (TryBoardPoint(screenPosition, out Vector3 point) == false)
            {
                return;   // 판 밖을 눌렀다 — 조준을 시작하지 않는다
            }

            aiming = true;
            pressTime = Time.time;
            pressPoint = point;
            currentPoint = point;
            SetAimLineVisible(true);
            DrawAimLine();
        }

        private void UpdateAim(Vector2 screenPosition)
        {
            if (TryBoardPoint(screenPosition, out Vector3 point))
            {
                currentPoint = point;
            }
            DrawAimLine();
        }

        private void EndAim(Vector2 screenPosition)
        {
            aiming = false;
            SetAimLineVisible(false);

            if (TryBoardPoint(screenPosition, out Vector3 point))
            {
                currentPoint = point;
            }

            var config = masterData.Tables.TbPanchigiConfig.GetOrDefault(1);
            if (config == null || playerContext.session == null)
            {
                return;
            }

            //  누른 시간에 상한이 없으면 오래 누를수록 힘이 무한히 커진다(원본의 문제).
            float holdTime = Mathf.Min(Time.time - pressTime, config.HoldTimeMax);

            Vector3 drag = currentPoint - pressPoint;
            drag.y = 0f;
            //  세기 상한도 여기서 자른다 — 서버는 넘으면 클램프가 아니라 거절한다.
            drag = Vector3.ClampMagnitude(drag, config.StrikePowerMax);

            playerContext.session.Send(new PanchigiStrikeToS
            {
                StrikePoint = MapperConfig.mapper.Map<ProtoVector3>(currentPoint),
                DragDelta = MapperConfig.mapper.Map<ProtoVector3>(drag),
                HoldTime = holdTime,
            });
        }

        private bool TryBoardPoint(Vector2 screenPosition, out Vector3 point)
        {
            Ray ray = aimCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f, BoardLayerMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }
            point = default;
            return false;
        }

        private void DrawAimLine()
        {
            if (aimLine == null)
            {
                return;
            }
            //  두 점은 판 윗면 바로 위에 찍힌다 — 그대로 그리면 깊이 테스트에 절반이 잘려 나간다.
            //  띄우는 건 그림뿐이고, 서버로 보내는 점은 건드리지 않는다.
            var lift = new Vector3(0f, 0.01f, 0f);
            aimLine.positionCount = 2;
            aimLine.SetPosition(0, pressPoint + lift);
            aimLine.SetPosition(1, currentPoint + lift);
        }

        private void SetAimLineVisible(bool visible)
        {
            if (aimLine != null)
            {
                aimLine.enabled = visible;
            }
        }
    }
}
