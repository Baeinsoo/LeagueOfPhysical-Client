using System.IO;
using LOP.EditorTools;
using Luban;
using NUnit.Framework;

/// <summary>
/// <see cref="SkydiveCourseBuilder"/>는 <c>SpreadWindLag</c>·<c>DiveWindLag</c>·
/// <c>BodyRadiusForGateCheck</c>를 상수로 박아 둔다(이 파일의 다른 검사 기준값들과 같은 컨벤션 —
/// 기준이 데이터를 따라 조용히 움직이면 검사가 아니게 된다). 하지만 아무도 <c>TbSkydiveConfig</c>와
/// 같은 값인지 확인하지 않으면, 마스터데이터를 만졌을 때 이 상수들이 조용히 낡아 굽기 전 검사가
/// 더 이상 실제 코스를 재지 않게 된다.
///
/// <c>LOPMasterData.LoadAsync()</c>는 <c>UnityWebRequest</c>를 써서 EditMode에서 블로킹 대기가
/// 안전하지 않으므로, 패키지가 배포하는 <c>.bytes</c>를 직접 읽는다 — <c>FlappyMapTrapScanner</c>가
/// 이미 쓰는 방식과 같다.
/// </summary>
public class SkydiveWindLagConsistencyTests
{
    [Test]
    public void 코스_빌더의_바람_지연_상수가_마스터데이터와_같다()
    {
        string path = Path.GetFullPath(
            "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/tbskydiveconfig.bytes");
        Assert.IsTrue(File.Exists(path), "tbskydiveconfig.bytes를 찾지 못했다: " + path);

        var table = new LOP.MasterData.TbSkydiveConfig(new ByteBuf(File.ReadAllBytes(path)));
        var row = table.GetOrDefault(1);
        Assert.IsNotNull(row, "TbSkydiveConfig id=1 행이 없다");

        Assert.AreEqual(row.SpreadWindLag, SkydiveCourseBuilder.SpreadWindLag, 1e-4f,
            "SkydiveCourseBuilder.SpreadWindLag가 TbSkydiveConfig와 어긋났다 — 굽기 전 검사가 실제 코스를 재지 않는다");
        Assert.AreEqual(row.DiveWindLag, SkydiveCourseBuilder.DiveWindLag, 1e-4f,
            "SkydiveCourseBuilder.DiveWindLag가 TbSkydiveConfig와 어긋났다 — 굽기 전 검사가 실제 코스를 재지 않는다");
    }

    [Test]
    public void 코스_빌더의_몸_반지름_상수가_마스터데이터와_같다()
    {
        string path = Path.GetFullPath(
            "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/tbskydiveconfig.bytes");
        Assert.IsTrue(File.Exists(path), "tbskydiveconfig.bytes를 찾지 못했다: " + path);

        var table = new LOP.MasterData.TbSkydiveConfig(new ByteBuf(File.ReadAllBytes(path)));
        var row = table.GetOrDefault(1);
        Assert.IsNotNull(row, "TbSkydiveConfig id=1 행이 없다");

        // BodyRadiusForGateCheck는 "구멍이 언젠가 열리는가"를 재는 판정 기준이다 — 마스터데이터의
        // 실제 몸통 반지름과 어긋나면 굽기 전 검사가 실제보다 좁거나 넓은 몸으로 통과 여부를 잰다.
        Assert.AreEqual(row.BodyRadius, SkydiveCourseBuilder.BodyRadiusForGateCheck, 1e-4f,
            "SkydiveCourseBuilder.BodyRadiusForGateCheck가 TbSkydiveConfig와 어긋났다 — 구멍 열림 검사가 실제 몸 크기를 재지 않는다");
    }
}
