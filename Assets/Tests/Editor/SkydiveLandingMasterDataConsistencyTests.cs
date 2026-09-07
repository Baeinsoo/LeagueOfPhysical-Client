using System.IO;
using Luban;
using NUnit.Framework;

/// <summary>
/// 점프 착지가 죽지 않아야 한다는 것은 <c>SkydiveLandingTests.점프_착지는_절대_안_죽는다</c>가 코드
/// 상수(JumpPower/Lethal)로 지키고 있지만, 그 상수들은 <c>TbSkydiveConfig</c>를 베끼기만 했을 뿐
/// 아무도 실제 표와 맞는지 확인하지 않는다. 그래서 마스터데이터에서 <c>jump_power</c>를 올리거나
/// <c>landing_lethal_speed</c>를 내려도 저 유닛 테스트는 계속 초록이고, 실제 게임에서는 점프
/// 착지가 죽는 사고가 난다.
///
/// <c>LOPMasterData.LoadAsync()</c>는 <c>UnityWebRequest</c>를 써서 EditMode에서 블로킹 대기가
/// 안전하지 않으므로, 패키지가 배포하는 <c>.bytes</c>를 직접 읽는다 —
/// <see cref="SkydiveWindLagConsistencyTests"/>가 이미 쓰는 방식과 같다.
/// </summary>
public class SkydiveLandingMasterDataConsistencyTests
{
    [Test]
    public void 점프_착지_속도가_실제_문턱보다_낮다()
    {
        string path = Path.GetFullPath(
            "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/tbskydiveconfig.bytes");
        Assert.IsTrue(File.Exists(path), "tbskydiveconfig.bytes를 찾지 못했다: " + path);

        var table = new LOP.MasterData.TbSkydiveConfig(new ByteBuf(File.ReadAllBytes(path)));
        var row = table.GetOrDefault(1);
        Assert.IsNotNull(row, "TbSkydiveConfig id=1 행이 없다");

        Assert.Less(row.JumpPower, row.LandingLethalSpeed,
            "TbSkydiveConfig의 jump_power가 landing_lethal_speed 이상이다 — " +
            "점프해서 착지하는 것만으로 죽는 게임이 된다");
    }
}
