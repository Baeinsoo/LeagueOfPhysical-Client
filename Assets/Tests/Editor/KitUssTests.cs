using System.IO;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>키트 USS가 유니티 기본 테마 규칙에 지지 않는지.</summary>
    public class KitUssTests
    {
        [Test]
        public void 눌림_버튼은_호버에도_바탕이_비치지_않는다()
        {
            //  기본 테마의 .unity-button:hover:enabled(클래스 + 가상 클래스 둘)가 .kit-press:hover를 이겨
            //  스티커 뒤로 버튼 바탕 직사각형이 비쳤다(10-12 에디터 확인). 같은 특이도 이상으로 덮는다.
            var uss = File.ReadAllText("Assets/UI/Kit/Kit.uss");
            StringAssert.Contains(".kit-press:hover:enabled", uss);
            StringAssert.Contains(".kit-press:active:enabled", uss);
        }
    }
}
