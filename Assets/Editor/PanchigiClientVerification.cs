using System.Text;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 클라 접촉점 수집기를 표로 찍는다. 상한·순서 규칙은 눈으로 틀린 걸 알기 어려워 직접 대조한다.
    /// unity CLI의 menu 명령으로 헤드리스 재실행이 된다.
    /// </summary>
    public static class PanchigiClientVerification
    {
        [MenuItem("LOP/판치기 클라 검증")]
        public static void Run()
        {
            var sb = new StringBuilder();
            Collector(sb);
            Debug.Log(sb.ToString());
        }

        private static void CheckInt(StringBuilder sb, string label, int actual, int expected)
        {
            sb.AppendLine($"  {label}: 실제={actual} 기대={expected} → {(actual == expected ? "OK" : "FAIL")}");
        }

        private static void CheckBool(StringBuilder sb, string label, bool actual, bool expected)
        {
            sb.AppendLine($"  {label}: 실제={actual} 기대={expected} → {(actual == expected ? "OK" : "FAIL")}");
        }

        private static void CheckFloat(StringBuilder sb, string label, float actual, float expected, float eps = 1e-4f)
        {
            sb.AppendLine($"  {label}: 실제={actual:F4} 기대={expected:F4} → {(Mathf.Abs(actual - expected) <= eps ? "OK" : "FAIL")}");
        }

        private static void Collector(StringBuilder sb)
        {
            sb.AppendLine("[접촉점 수집기] PanchigiContactCollector");

            const float HoldMax = 1f;
            const float PowerMax = 3f;

            //  상한까지 접수하고, 넘긴 손가락은 거절한다
            var c = new PanchigiContactCollector(4);
            CheckBool(sb, "1번째 손가락 접수", c.Begin(1, Vector3.zero, 0f), true);
            CheckBool(sb, "2번째 접수", c.Begin(2, Vector3.zero, 0f), true);
            CheckBool(sb, "3번째 접수", c.Begin(3, Vector3.zero, 0f), true);
            CheckBool(sb, "4번째 접수", c.Begin(4, Vector3.zero, 0f), true);
            CheckBool(sb, "5번째는 거절(상한 4)", c.Begin(5, Vector3.zero, 0f), false);
            CheckInt(sb, "눌린 손가락 수", c.Pressed.Count, 4);

            //  떼도 자리는 나지 않는다 — 접촉점이 보관되기 때문
            c.End(1, Vector3.zero, 0.5f, HoldMax, PowerMax);
            CheckInt(sb, "하나 떼면 눌린 수 3", c.Pressed.Count, 3);
            CheckInt(sb, "확정된 접촉점 1", c.Contacts.Count, 1);
            CheckBool(sb, "뗐어도 5번째는 여전히 거절", c.Begin(5, Vector3.zero, 0.5f), false);

            //  전부 떨어져야 완성이다
            CheckBool(sb, "아직 눌린 손가락 있음 → 미완성", c.IsComplete, false);
            c.End(2, Vector3.zero, 0.5f, HoldMax, PowerMax);
            c.End(3, Vector3.zero, 0.5f, HoldMax, PowerMax);
            CheckBool(sb, "1개 남으면 아직 미완성", c.IsComplete, false);
            c.End(4, Vector3.zero, 0.5f, HoldMax, PowerMax);
            CheckBool(sb, "전부 떨어지면 완성", c.IsComplete, true);
            CheckInt(sb, "모인 접촉점 4개", c.Contacts.Count, 4);

            //  Clear 뒤에는 다시 상한만큼 받는다
            c.Clear();
            CheckBool(sb, "Clear 뒤 미완성", c.IsComplete, false);
            CheckInt(sb, "Clear 뒤 접촉점 0", c.Contacts.Count, 0);
            CheckBool(sb, "Clear 뒤 다시 접수됨", c.Begin(9, Vector3.zero, 1f), true);

            //  누른 시간과 세기가 상한에서 잘린다
            var d = new PanchigiContactCollector(4);
            d.Begin(1, Vector3.zero, 0f);
            d.End(1, new Vector3(99f, 0f, 0f), 10f, HoldMax, PowerMax);
            CheckFloat(sb, "누른 시간이 상한으로 잘림", d.Contacts[0].HoldTime, HoldMax);
            CheckFloat(sb, "세기가 상한으로 잘림", d.Contacts[0].DragDelta.magnitude, PowerMax);

            //  드래그는 판 평면 위 변위다 — 높이는 빠진다
            var e = new PanchigiContactCollector(4);
            e.Begin(1, new Vector3(0f, 0f, 0f), 0f);
            e.End(1, new Vector3(1f, 5f, 0f), 0.2f, HoldMax, PowerMax);
            CheckFloat(sb, "드래그 y는 0", e.Contacts[0].DragDelta.y, 0f);
            CheckFloat(sb, "드래그 크기는 수평 성분만", e.Contacts[0].DragDelta.magnitude, 1f);

            //  같은 손가락을 두 번 접수하지 않는다
            var f = new PanchigiContactCollector(4);
            f.Begin(7, Vector3.zero, 0f);
            CheckBool(sb, "같은 touchId 재접수 거절", f.Begin(7, Vector3.zero, 0f), false);

            //  추적 중이 아닌 손가락의 Update/End는 아무 일도 하지 않는다
            var g = new PanchigiContactCollector(4);
            g.Update(42, Vector3.one);
            g.End(42, Vector3.one, 1f, HoldMax, PowerMax);
            CheckInt(sb, "모르는 손가락은 무시 — 접촉점 0", g.Contacts.Count, 0);
            CheckInt(sb, "모르는 손가락은 무시 — 눌린 수 0", g.Pressed.Count, 0);
        }
    }
}
