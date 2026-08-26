using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
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

        //  마우스는 손가락이 하나뿐이다. 터치 id와 겹치지 않는 값을 준다.
        private const int MouseTouchId = -1;

        private PanchigiContactCollector collector;

        private void Awake()
        {
            BoardLayerMask = LayerMask.GetMask("Default");
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }
            HideAllAimLines();
        }

        private void OnDisable()
        {
            //  조준 중에 꺼지면 조준선이 화면에 남고, 다음 켜질 때 절반쯤 조준된 상태로
            //  시작한다 — 꺼질 때 확실히 리셋한다.
            collector?.Clear();
            HideAllAimLines();
        }

        private void Update()
        {
            var config = masterData.Tables.TbPanchigiConfig.GetOrDefault(1);
            if (config == null)
            {
                return;
            }

            if (IsMyTurn() == false)
            {
                //  조준하는 중에 차례가 넘어갔다 — 모은 것도 조준선도 남으면 안 된다.
                if (collector != null && (collector.Pressed.Count > 0 || collector.Contacts.Count > 0))
                {
                    collector.Clear();
                    HideAllAimLines();
                }
                return;
            }

            collector ??= new PanchigiContactCollector(config.ContactMax);

            if (Touchscreen.current != null)
            {
                PollTouches(config);
            }
            else if (Mouse.current != null)
            {
                PollMouse(config);
            }

            DrawAimLines();

            //  손가락이 전부 떨어졌다 = 한 번의 치기가 끝났다.
            if (collector.IsComplete)
            {
                SendStrike();
                collector.Clear();
                HideAllAimLines();
            }
        }

        private bool IsMyTurn()
            => stateStore.Phase.CurrentValue == AimingPhase
            && stateStore.CurrentEntityId.CurrentValue == playerContext.entityId;

        private void PollTouches(LOP.MasterData.PanchigiConfig config)
        {
            foreach (TouchControl touch in Touchscreen.current.touches)
            {
                int touchId = touch.touchId.ReadValue();
                Vector2 screen = touch.position.ReadValue();

                if (touch.press.wasPressedThisFrame)
                {
                    //  판을 못 맞힌 손가락은 접수하지 않고 자리도 먹지 않는다.
                    if (TryBoardPoint(screen, out Vector3 begin))
                    {
                        collector.Begin(touchId, begin, Time.time);
                    }
                    continue;
                }

                //  뗀 그 프레임엔 isPressed가 아직 true일 수 있다 — release를 먼저 본다.
                //  순서를 바꾸면 누르고 뗀 게 같은 프레임인 탭이 씹힌다.
                if (touch.press.wasReleasedThisFrame)
                {
                    EndTouch(touchId, screen, config);
                }
                else if (touch.press.isPressed)
                {
                    if (TryBoardPoint(screen, out Vector3 moved))
                    {
                        collector.Update(touchId, moved);
                    }
                }
            }
        }

        private void PollMouse(LOP.MasterData.PanchigiConfig config)
        {
            Vector2 screen = Mouse.current.position.ReadValue();

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (TryBoardPoint(screen, out Vector3 begin))
                {
                    collector.Begin(MouseTouchId, begin, Time.time);
                }
                return;
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                EndTouch(MouseTouchId, screen, config);
            }
            else if (Mouse.current.leftButton.isPressed)
            {
                if (TryBoardPoint(screen, out Vector3 moved))
                {
                    collector.Update(MouseTouchId, moved);
                }
            }
        }

        //  뗀 자리가 판 밖이면 마지막으로 판 위에 있던 자리를 쓴다 — 손가락이 판을 벗어나며
        //  떨어졌다고 그 손가락의 힘이 사라지면 안 된다.
        private void EndTouch(int touchId, Vector2 screen, LOP.MasterData.PanchigiConfig config)
        {
            Vector3 endPoint;
            if (TryBoardPoint(screen, out Vector3 hit))
            {
                endPoint = hit;
            }
            else if (TryGetPressedCurrent(touchId, out Vector3 last))
            {
                endPoint = last;
            }
            else
            {
                return;   // 추적 중이 아닌 손가락이다
            }

            collector.End(touchId, endPoint, Time.time, config.HoldTimeMax, config.StrikePowerMax);
        }

        private bool TryGetPressedCurrent(int touchId, out Vector3 point)
        {
            foreach (PanchigiContactCollector.Aim aim in collector.Pressed)
            {
                if (aim.TouchId == touchId)
                {
                    point = aim.Current;
                    return true;
                }
            }
            point = default;
            return false;
        }

        private void SendStrike()
        {
            if (playerContext.session == null)
            {
                return;
            }

            var message = new PanchigiStrikeToS();
            foreach (PanchigiContactCollector.Contact contact in collector.Contacts)
            {
                message.Contacts.Add(new PanchigiStrikeContact
                {
                    StrikePoint = MapperConfig.mapper.Map<ProtoVector3>(contact.StrikePoint),
                    DragDelta = MapperConfig.mapper.Map<ProtoVector3>(contact.DragDelta),
                    HoldTime = contact.HoldTime,
                });
            }

            playerContext.session.Send(message);
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

        //  Task 6에서 손가락마다 그리도록 바뀐다. 지금은 첫 손가락만 그린다.
        private void DrawAimLines()
        {
            if (aimLine == null)
            {
                return;
            }
            if (collector.Pressed.Count == 0)
            {
                aimLine.enabled = false;
                return;
            }
            PanchigiContactCollector.Aim first = collector.Pressed[0];
            //  두 점은 판 윗면 바로 위에 찍힌다 — 그대로 그리면 깊이 테스트에 절반이 잘려 나간다.
            //  띄우는 건 그림뿐이고, 서버로 보내는 점은 건드리지 않는다.
            var lift = new Vector3(0f, 0.01f, 0f);
            aimLine.enabled = true;
            aimLine.positionCount = 2;
            aimLine.SetPosition(0, first.Start + lift);
            aimLine.SetPosition(1, first.Current + lift);
        }

        private void HideAllAimLines()
        {
            if (aimLine != null)
            {
                aimLine.enabled = false;
            }
        }
    }
}
