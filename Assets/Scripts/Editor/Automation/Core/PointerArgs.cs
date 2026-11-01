using System.Globalization;
using UnityEngine;

namespace LOP.EditorTools.Automation
{
    /// <summary>좌표 단계 인자와 되돌리기 값의 글자 꼴(spec §8.4). 소수점은 문화권과 상관없이 점.</summary>
    public static class PointerArgs
    {
        public static bool TryParseDevice(string name, out PointerDevice device)
        {
            switch (name)
            {
                case null:
                case "mouse":
                    device = PointerDevice.Mouse;
                    return true;
                case "touch":
                    device = PointerDevice.Touch;
                    return true;
                default:
                    device = PointerDevice.Mouse;
                    return false;
            }
        }

        public static PointerDevice ParseDevice(string name) => TryParseDevice(name, out var device) ? device : PointerDevice.Mouse;

        public static string DeviceName(PointerDevice device) => device == PointerDevice.Touch ? "touch" : "mouse";

        public static string Format(Vector2 point) =>
            point.x.ToString("0.###", CultureInfo.InvariantCulture) + "," + point.y.ToString("0.###", CultureInfo.InvariantCulture);

        public static bool TryParsePoint(string text, out Vector2 point)
        {
            point = default;
            var parts = (text ?? string.Empty).Split(',');
            if (parts.Length != 2) return false;
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) return false;
            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) return false;
            point = new Vector2(x, y);
            return true;
        }
    }
}
