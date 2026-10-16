using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

        [Test]
        public void 눌림_버튼은_꺼져도_바탕이_비치지_않는다()
        {
            //  기본 테마의 .unity-button:disabled가 .kit-press를 이겨 꺼진 [구매]·[전부 기본으로] 뒤로
            //  회색 직사각형이 비쳤다(10-16 사용자 Play 확인). 키트 토글도 고른 쪽 색을 지킨다.
            var uss = File.ReadAllText("Assets/UI/Kit/Kit.uss");
            StringAssert.Contains(".kit-press:disabled", uss);
            StringAssert.Contains(".kit-toggle__opt:disabled", uss);
            StringAssert.Contains(".kit-toggle__opt--on:disabled", uss);
        }

        //  키트 화면(루트에 .kit)의 버튼은 껍데기를 지워야 한다 — 안 지우면 기본 테마의 회색 직사각형이 호버·눌림·꺼짐 때 보인다.
        private static readonly string[] OwnShellClasses = { "kit-press", "kit-toggle__opt" };

        [Test]
        public void 키트_화면_UXML의_버튼은_모두_껍데기를_지운다()
        {
            var bad = Directory.GetFiles("Assets/UI", "*.uxml", SearchOption.AllDirectories)
                .Where(path => Regex.IsMatch(File.ReadAllText(path), "class=\"kit[ \"]"))
                .SelectMany(path => Regex.Matches(File.ReadAllText(path), "<ui:Button[^>]*>").Cast<System.Text.RegularExpressions.Match>()
                    .Where(m => !OwnShellClasses.Any(c => m.Value.Contains(c)))
                    .Select(m => $"{path}: {m.Value}"))
                .ToList();
            CollectionAssert.IsEmpty(bad);
        }

        [Test]
        public void 키트_화면_코드로_만든_버튼도_껍데기를_지운다()
        {
            //  new Button( 뒤 다섯 줄 안에 kit-press를 붙여야 한다. 키트 화면 코드만 본다(옛 화면은 제외).
            string[] kitViews =
            {
                "Assets/Scripts/UI/Shop/ShopView.cs", "Assets/Scripts/UI/Locker/LockerView.cs",
                "Assets/Scripts/UI/Shell/ProfileView.cs", "Assets/Scripts/UI/GameSelect/GameSelectView.cs",
            };
            var bad = kitViews.SelectMany(path =>
            {
                var lines = File.ReadAllLines(path);
                return Enumerable.Range(0, lines.Length)
                    .Where(i => lines[i].Contains("new Button("))
                    .Where(i => !lines.Skip(i).Take(6).Any(l => l.Contains("\"kit-press\"")))
                    .Select(i => $"{path}:{i + 1}");
            }).ToList();
            CollectionAssert.IsEmpty(bad);
        }
    }
}
