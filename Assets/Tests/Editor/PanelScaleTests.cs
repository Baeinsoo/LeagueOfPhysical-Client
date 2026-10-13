using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.Tests
{
    /// <summary>화면 UI 패널이 비율에 상관없이 같은 배치를 쓰도록 맞춰져 있는지.</summary>
    public class PanelScaleTests
    {
        [Test]
        public void 화면_패널은_기준보다_작아지지_않게_늘어난다()
        {
            //  가로 맞춤이면 20:9 폰에서 높이가 864로 줄어 로비 버튼이 안 들어간다(10-13).
            //  Expand면 넓은 폰은 가로만, 태블릿은 세로만 늘어 배치를 비율마다 따로 두지 않아도 된다.
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/UIPanelSettings.asset");
            Assert.AreEqual(PanelScaleMode.ScaleWithScreenSize, panel.scaleMode);
            Assert.AreEqual(PanelScreenMatchMode.Expand, panel.screenMatchMode);
            Assert.AreEqual(new Vector2Int(1920, 1080), panel.referenceResolution);
        }
    }
}
