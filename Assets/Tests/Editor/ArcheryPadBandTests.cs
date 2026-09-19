using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    //  띠의 높이가 코드(LowerBandFraction)와 USS 두 곳에 적혀 있다. 어긋나면 **보이는 띠와
    //  판정하는 띠가 달라져** "분명히 띠 밖에서 뗐는데 안 나간다"가 된다 — 화면상 아무 표시가
    //  없어 원인을 짚기 어려운 종류다. 그래서 둘을 대조한다.
    public class ArcheryPadBandTests
    {
        [Test]
        public void USS의_띠_높이가_코드의_값과_같다()
        {
            string uss = File.ReadAllText("Assets/UI/ArcheryPad/ArcheryPad.uss");
            int expected = Mathf.RoundToInt(LOP.UI.ArcheryPadViewModel.LowerBandFraction * 100f);
            StringAssert.Contains("height: " + expected + "%", uss,
                "USS의 .lower-band 높이가 LowerBandFraction과 다르다 — 보이는 띠와 판정이 어긋난다");
        }
    }
}
