using System;
using LOP.MapTools;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 광산 코스의 움직이는 장애물(승강기·진자 광차·낙석)과 무너진 갱도 받침목을 굽는다 — 값은 전부 <see cref="MineCourse"/>에서.
    /// 회색 상자 = 판정 콜라이더(Default 층, 게임 평면). 마커가 움직이는 자식을 갖고, 게임에서는 필드가 매 틱 자세를 대입한다.
    /// 구운 자세는 쉬는 자리(승강기 가운데 · 진자 수직 · 낙석 숨음)다.
    /// </summary>
    internal static class FlappyMineMoverBake
    {
        private const string UndoName = "Build mine course";

        internal readonly struct Counts
        {
            public readonly int Lifts, Carts, Rockfalls, Props;
            public Counts(int lifts, int carts, int rockfalls, int props) { Lifts = lifts; Carts = carts; Rockfalls = rockfalls; Props = props; }
        }

        internal static Counts Build(Transform root, MineCourse course, Material skin, Func<float, float> floorAt, Func<float, float> ceilingAt)
        {
            //  승강기 파이프 이름은 LiftPipe… — PipeLow_/PipeHigh_로 부르면 옷 입히기(FlappyMineDressing)가 고정 관문 옷을 입히고 렌더러를 끈다.
            foreach (MineLift l in course.Lifts)
            {
                MineGate g = l.Gate;
                Transform marker = NewRoot(root, $"Lift_{g.X:F1}", new Vector3(g.X, 0f, 0f));
                Transform cage = NewChild(marker, "Cage", Vector3.zero);
                var lift = marker.gameObject.AddComponent<LOP.FlappyLift>();
                lift.Amplitude = l.Amplitude; lift.Period = l.Period; lift.Phase = l.Phase; lift.Cage = cage;

                //  케이지가 진폭만큼 오르내려도 파이프 끝이 통로에 드러나지 않게 진폭만큼 더 묻는다.
                //  묻는 거리는 MineDressingLayout.GatePipeBury 하나만 본다(10-14 리뷰 T6) — 옷(LiftCageLocal)도 같은 상수를 쓰므로
                //  굽기(판정 콜라이더)와 그림이 한쪽만 바뀌어 벌어질 일이 없다.
                float half = g.Width * 0.5f;
                float low = Mathf.Min(floorAt(g.X - half), floorAt(g.X), floorAt(g.X + half)) - MineDressingLayout.GatePipeBury - l.Amplitude;
                float high = Mathf.Max(ceilingAt(g.X - half), ceilingAt(g.X), ceilingAt(g.X + half)) + MineDressingLayout.GatePipeBury + l.Amplitude;
                float a = g.GapCenter - g.Gap * 0.5f, b = g.GapCenter + g.Gap * 0.5f;
                FlappyClassicCourseBuilder.Pipe(cage, $"LiftPipeLow_{g.X:F1}", g.X, low, a, skin, g.Width);
                FlappyClassicCourseBuilder.Pipe(cage, $"LiftPipeHigh_{g.X:F1}", g.X, b, high, skin, g.Width);
            }

            //  광차 하나 = 진자 하나: 축이 마커 자리, 몸은 광차 상자뿐(줄은 그림만 — 판정 없음).
            //  광차 상자는 줄 끝의 `Upright` 자식 아래에 두고 FlappyPendulum.Upright에 잇는다 — 필드가 진자 각도만큼
            //  거꾸로 돌려 주므로 진자가 흔들려도 광차는 수평이다.
            int perStation = 0;
            float lastPivot = float.NaN;
            foreach (MinePendulum p in course.Pendulums)
            {
                perStation = Mathf.Approximately(p.PivotX, lastPivot) ? perStation + 1 : 0;
                lastPivot = p.PivotX;
                Transform marker = NewRoot(root, $"Cart_{p.PivotX:F1}_{perStation}", new Vector3(p.PivotX, p.PivotY, 0f));
                var pendulum = marker.gameObject.AddComponent<LOP.FlappyPendulum>();
                pendulum.Amplitude = p.Amplitude; pendulum.Period = p.Period; pendulum.Phase = p.Phase;
                Transform upright = NewChild(marker, "Upright", new Vector3(0f, -p.Length, 0f));
                pendulum.Upright = upright;
                float cy = p.PivotY - p.Length;
                FlappyClassicCourseBuilder.Pipe(upright, "CartBox", p.PivotX, cy - p.CartHeight * 0.5f, cy + p.CartHeight * 0.5f, skin, p.CartWidth);
            }

            foreach (MineRockfall r in course.Rockfalls)
            {
                Transform marker = NewRoot(root, $"Rockfall_{r.X:F1}", new Vector3(r.X, r.Ceiling, 0f));
                var rockfall = marker.gameObject.AddComponent<LOP.FlappyRockfall>();
                rockfall.Period = r.Period; rockfall.Speed = r.Speed; rockfall.Drop = r.Drop; rockfall.Size = r.Size; rockfall.Phase = r.Phase;
                //  쉬는 자리 = 천장 속에 숨은 자리. 필드가 첫 틱을 대입하기 전에도 통로를 막지 않는다.
                Transform rock = NewChild(marker, "Rock", new Vector3(0f, rockfall.HiddenRise, 0f));
                rockfall.Rock = rock;
                float cy = r.Ceiling + rockfall.HiddenRise;
                FlappyClassicCourseBuilder.Pipe(rock, "RockBox", r.X, cy - r.Size * 0.5f, cy + r.Size * 0.5f, skin, r.Size);
            }

            foreach (MineRect p in course.Props)
            {
                FlappyClassicCourseBuilder.Prism(root, $"CollapsedProp_{p.X0:F1}",
                    FlappyClassicCourseBuilder.BoxPolygon(new Box2(p.X0, p.Y0, p.X1, p.Y1)), skin);
            }
            return new Counts(course.Lifts.Count, course.Pendulums.Count, course.Rockfalls.Count, course.Props.Count);
        }

        //  마커 루트는 움직이지 않거나(승강기·낙석) 돌기만 한다(진자). 정적 배칭에 넣지 않는다 — 넣으면 자식이 안 움직인다.
        private static Transform NewRoot(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = position;
            go.layer = LayerMask.NameToLayer("Default");
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            return go.transform;
        }

        //  움직이는 자식(케이지·광차 받침·바위) — 필드가 이 Transform에 자세를 대입한다.
        private static Transform NewChild(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = localPosition;
            go.layer = LayerMask.NameToLayer("Default");
            return go.transform;
        }
    }
}
