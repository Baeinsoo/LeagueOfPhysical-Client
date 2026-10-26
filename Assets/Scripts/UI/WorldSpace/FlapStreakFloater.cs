using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP
{
    /// <summary>
    /// 내 새 머리 위 연속 날갯짓 "+N"(spec §7). 월드 공간 UI Toolkit(<see cref="DamageFloater"/>·<see cref="CharacterNameplate"/>와 같은 길).
    /// <para>실행순서 3100 — 보간기와 <see cref="CameraController"/>(3000)가 그 프레임에 새·카메라를 옮긴 <b>뒤</b> 자리를 잡는다. 그래서 새의 자식으로
    /// 붙이지 않아도 뒤처지지 않는다. 새 그림은 <b>읽기만</b> 한다 — 연출이 내 새 자리를 어긋나게 하면 안 된다(사용자 규칙 10-25).</para>
    /// </summary>
    [DefaultExecutionOrder(3100)]
    public class FlapStreakFloater : MonoBehaviour
    {
        /// <summary>몸 꼭대기 위 높이(m). 노란 화살표(몸 위 0.27 + 높이 0.33 + 흔들림 0.067)를 넘는다.</summary>
        internal const float Lift = 0.95f;

        /// <summary>거리 비례 크기 — 데미지 숫자(0.1)보다 작게. 사람 확인에서 고친다.</summary>
        internal const float ScreenSizeFactor = 0.06f;

        private Func<FlapStreakFrame> _frame;
        private Func<Transform> _myVisual;
        private float _bodyRadius;
        private VisualElement _root;
        private Label _label;
        private Camera _camera;

        public void Bind(VisualElement root, Label label, Func<FlapStreakFrame> frame, Func<Transform> myVisual, float bodyRadius)
        {
            _root = root;
            _label = label;
            _frame = frame;
            _myVisual = myVisual;
            _bodyRadius = bodyRadius;
            SetShown(false);
        }

        /// <summary>그림 뿌리(몸 바닥) → 글자 가운데.</summary>
        internal static Vector3 Anchor(Vector3 visualPosition, float bodyRadius)
            => visualPosition + Vector3.up * (2f * bodyRadius + Lift);

        internal static float ScaleFor(float distance) => distance * ScreenSizeFactor;

        private void LateUpdate()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }
            Follow(_myVisual != null ? _myVisual() : null, _camera, _frame != null ? _frame() : FlapStreakFrame.Hidden);
        }

        internal void Follow(Transform visual, Camera camera, FlapStreakFrame frame)
        {
            bool show = frame.Visible && visual != null && camera != null;
            SetShown(show);
            if (show == false)
            {
                return;
            }
            Vector3 at = Anchor(visual.position, _bodyRadius);
            //  카메라 회전을 그대로 — 늘 정면·수평으로 읽힌다.
            transform.SetPositionAndRotation(at, camera.transform.rotation);
            float size = ScaleFor(Vector3.Distance(at, camera.transform.position)) * frame.Scale;
            transform.localScale = new Vector3(size, size, size);
            if (_label != null)
            {
                _label.text = frame.Text;
                _label.style.opacity = frame.Alpha;
            }
        }

        private void SetShown(bool shown)
        {
            if (_root != null)
            {
                _root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
