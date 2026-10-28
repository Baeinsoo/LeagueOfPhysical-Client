using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP
{
    /// <summary>
    /// 연속 날갯짓 연출 "집중형"을 화면 공간 판에 그린다(spec §7, 사용자 결정 10-26). 흐름은 <see cref="FlapComboFx"/>가 갖고, 여기는 그리기만 한다.
    /// <para>실행순서 3100 — 보간기와 <see cref="CameraController"/>(3000)가 그 장의 새·카메라를 옮긴 <b>뒤</b> 새 자리를 읽는다. 화면 공간 판은
    /// 요소 스타일을 그 장 그리기에서 반영하므로, 월드 공간 판(UIDocument가 transform을 −100에서 읽어 한 장 늦다)처럼 옛 자리가 그려지지 않는다.</para>
    /// <para>새 그림은 <b>읽기만</b> 한다 — 자식으로 붙거나 자리를 고치지 않는다(사용자 규칙 10-25). 카메라도 건드리지 않는다.
    /// 판은 앱 UI(정렬 0) 아래(−1)라 HUD를 가리지 않고, 아무것도 누르지 않는다.</para>
    /// </summary>
    [DefaultExecutionOrder(3100)]
    public class FlapComboView : MonoBehaviour
    {
        private VisualElement _root;
        private VisualElement _canvas;
        private FlapComboFx _fx;
        private Func<Transform> _myVisual;
        private Func<bool> _racing;
        private float _bodyRadius;
        private Camera _camera;

        private readonly List<Label> _numbers = new List<Label>();
        private readonly List<VisualElement> _feathers = new List<VisualElement>();
        private Label _word;

        internal Label Word => _word;
        internal IReadOnlyList<Label> NumberLabels => _numbers;

        //  그리기 콜백이 읽는 이번 장 값(패널 px).
        private readonly List<Vector2> _trailPx = new List<Vector2>();
        private readonly List<float> _trailAge = new List<float>();
        private float _unit = 1f;
        private float _rPx;

        public void Bind(VisualElement root, FlapComboFx fx, Func<Transform> myVisual, Func<bool> racing, float bodyRadius)
        {
            _root = root;
            _fx = fx;
            _myVisual = myVisual;
            _racing = racing;
            _bodyRadius = bodyRadius;
            if (_root == null)
            {
                return;
            }
            _root.pickingMode = PickingMode.Ignore;

            //  꼬리·고리는 선이라 그리기 콜백으로, 깃털·글자는 풀 요소로. 그리는 순서 = 꼬리·고리 → 깃털 → 숫자 → 한마디.
            _canvas = new VisualElement { pickingMode = PickingMode.Ignore };
            _canvas.AddToClassList("combo-layer");
            _canvas.generateVisualContent += Draw;
            _root.Add(_canvas);

            for (int i = 0; i < FlapComboFx.MaxFeathers; i++)
            {
                var feather = new VisualElement { pickingMode = PickingMode.Ignore };
                feather.AddToClassList("combo-feather");
                feather.style.display = DisplayStyle.None;
                _root.Add(feather);
                _feathers.Add(feather);
            }
            for (int i = 0; i < FlapComboFx.MaxNumbers; i++)
            {
                _numbers.Add(NewText());
            }
            _word = NewText();
        }

        private Label NewText()
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList("combo-text");
            label.style.display = DisplayStyle.None;
            _root.Add(label);
            return label;
        }

        //  시험 이음매 — 실제로는 Camera.main.
        internal Camera cameraOverride;

        private void LateUpdate() => Step(Time.deltaTime);

        internal void Step(float deltaTime)
        {
            if (_fx == null || _root == null || _root.panel == null)
            {
                return;
            }
            if (_camera == null)
            {
                _camera = cameraOverride != null ? cameraOverride : Camera.main;
            }
            float height = _root.panel.visualTree.layout.height;
            _unit = float.IsNaN(height) || height <= 0f ? 1f : FlapComboLook.Unit(height);
            _fx.Advance(deltaTime, _unit);

            Transform visual = _myVisual != null ? _myVisual() : null;
            bool placed = visual != null && _camera != null;
            Vector2 birdPx = default;
            if (placed)
            {
                //  그림 뿌리는 몸 바닥이다 — 가운데 = 뿌리 + R.
                Vector3 center = visual.position + Vector3.up * _bodyRadius;
                birdPx = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, center, _camera);
                Vector2 top = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, center + _camera.transform.up * _bodyRadius, _camera);
                _rPx = Vector2.Distance(birdPx, top);
                _fx.Resolve(birdPx, _rPx, _unit);
                _fx.RecordTrail(center, _racing != null && _racing());
                ProjectTrail();
            }
            else
            {
                _fx.DropPending();
                _trailPx.Clear();
                _trailAge.Clear();
            }

            RenderNumbers();
            RenderWord(placed, birdPx);
            RenderFeathers();
            _canvas.MarkDirtyRepaint();
        }

        private void ProjectTrail()
        {
            _trailPx.Clear();
            _trailAge.Clear();
            //  꼬리는 몸 뒤 끝에서 시작한다 — 화면 위 선이 새 그림을 덮지 않게.
            var back = new Vector2(_rPx, 0f);
            foreach (var point in _fx.Trail)
            {
                _trailPx.Add(RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, point.World, _camera) - back);
                _trailAge.Add(point.Age);
            }
        }

        private void RenderNumbers()
        {
            for (int i = 0; i < _numbers.Count; i++)
            {
                var label = _numbers[i];
                if (i >= _fx.Numbers.Count)
                {
                    label.style.display = DisplayStyle.None;
                    continue;
                }
                var p = _fx.Numbers[i];
                float k = p.Age / FlapComboLook.NumberLife;
                float font = FlapComboLook.NumberFont * _unit * FlapComboLook.NumberSize(p.Count);
                label.text = FlapComboLook.Text(p.Count);
                Place(label, p.Corner - new Vector2(0f, FlapComboLook.NumberRise * _unit * k), font,
                      FlapComboLook.NumberPop(k), FlapComboLook.NumberAlpha(k), FlapComboLook.TierColor(p.Count),
                      FlapComboLook.NumberOutlineRatio, shadow: true);
            }
        }

        private void RenderWord(bool placed, Vector2 birdPx)
        {
            //  한마디는 새를 따라간다(시제품 집중형) — 새가 안 보이는 장엔 숨긴다.
            if (_fx.WordVisible == false || placed == false)
            {
                _word.style.display = DisplayStyle.None;
                return;
            }
            float k = _fx.WordAge / FlapComboLook.WordLife;
            _word.text = FlapComboLook.WordWithCue(_fx.WordText, _fx.WordDashCue);
            Vector2 corner = FlapComboLook.WordCorner(birdPx, _rPx, _unit) - new Vector2(0f, FlapComboLook.WordRise * _unit * k);
            Place(_word, corner, FlapComboLook.WordFont * _unit, FlapComboLook.WordPop(k), FlapComboLook.WordAlpha(k), _fx.WordColor,
                  FlapComboLook.WordOutlineRatio, shadow: false);
        }

        //  시제품 테두리 lineWidth = 글자 × 0.2(바깥 반만 보임) — UI 테두리는 안팎으로 걸치니 그 반. 숫자는 10-27 피드백으로 더 두껍게,
        //  어두운 그림자도 깔아 더 묵직해 보이게 한다(한마디는 그대로 — 사용자 요청 범위 밖).
        private static void Place(Label label, Vector2 corner, float font, float scale, float alpha, Color color, float outlineRatio, bool shadow)
        {
            label.style.display = DisplayStyle.Flex;
            label.style.left = corner.x;
            label.style.top = corner.y;
            label.style.fontSize = font;
            label.style.unityTextOutlineWidth = font * outlineRatio;
            label.style.scale = new Scale(new Vector2(scale, scale));
            label.style.opacity = alpha;
            label.style.color = color;
            if (shadow)
            {
                label.style.textShadow = new TextShadow
                {
                    offset = new Vector2(0f, font * FlapComboLook.NumberShadowOffsetRatio),
                    blurRadius = font * FlapComboLook.NumberShadowBlurRatio,
                    color = FlapComboLook.NumberShadow,
                };
            }
            else
            {
                label.style.textShadow = StyleKeyword.Null;
            }
        }

        private void RenderFeathers()
        {
            for (int i = 0; i < _feathers.Count; i++)
            {
                var el = _feathers[i];
                if (i >= _fx.Feathers.Count)
                {
                    el.style.display = DisplayStyle.None;
                    continue;
                }
                var f = _fx.Feathers[i];
                //  시제품: 깃털은 타원 3.5×1.6 · 3.5×0.8(돌며), 반짝이는 2.5 반변 네모(더 빨리 돈다).
                float w = (f.Spark ? 5f : 11.2f) * _unit;
                float h = (f.Spark ? 5f : 5.6f) * _unit;
                el.style.display = DisplayStyle.Flex;
                el.EnableInClassList("combo-feather--spark", f.Spark);
                el.style.left = f.Position.x;
                el.style.top = f.Position.y;
                el.style.width = w;
                el.style.height = h;
                el.style.backgroundColor = f.Color;
                el.style.opacity = 1f - f.Age / f.Life;
                el.style.rotate = new Rotate(new Angle(f.Age * (f.Spark ? 8f : 5f), AngleUnit.Radian));
            }
        }

        private void Draw(MeshGenerationContext mgc)
        {
            if (_fx == null)
            {
                return;
            }
            var painter = mgc.painter2D;
            painter.lineCap = LineCap.Round;

            float life = FlapComboLook.TrailLife(_fx.Count);
            for (int i = 1; i < _trailPx.Count; i++)
            {
                float a = 1f - _trailAge[i] / life;
                painter.strokeColor = FlapComboLook.TrailColor(_fx.Count, a);
                painter.lineWidth = FlapComboLook.TrailWidth(_fx.Count, a, _unit);
                painter.BeginPath();
                painter.MoveTo(_trailPx[i - 1]);
                painter.LineTo(_trailPx[i]);
                painter.Stroke();
            }

            foreach (var ring in _fx.Rings)
            {
                float k = ring.Age / FlapComboLook.RingLife;
                Color c = ring.Color;
                c.a = 1f - k;
                painter.strokeColor = c;
                painter.lineWidth = FlapComboLook.RingWidth(k, _unit);
                painter.BeginPath();
                painter.Arc(ring.Center, FlapComboLook.RingRadius(k, _rPx, _unit), Angle.Degrees(0f), Angle.Degrees(360f));
                painter.Stroke();
            }
        }

        private void OnDestroy()
        {
            if (_canvas != null)
            {
                _canvas.generateVisualContent -= Draw;
            }
        }
    }
}
