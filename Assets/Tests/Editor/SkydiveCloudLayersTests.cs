using LOP;
using NUnit.Framework;

public class SkydiveCloudLayersTests
{
    [Test]
    public void 층은_코스_안에만_놓인다()
    {
        foreach (float y in SkydiveCloudLayers.Altitudes)
        {
            Assert.That(y, Is.GreaterThan(0f).And.LessThan(3000f));
        }
        Assert.That(SkydiveCloudLayers.Altitudes.Length, Is.GreaterThan(8));
    }

    [Test]
    public void 층_밖에서는_기준_밀도다()
    {
        // 층 사이의 한가운데. 간격(170)보다 반두께(40)가 작아 여기는 확실히 바깥이다.
        // "반두께의 몇 배"로 잡으면 오히려 다음 층 안으로 들어간다.
        float between = (SkydiveCloudLayers.Altitudes[0] + SkydiveCloudLayers.Altitudes[1]) * 0.5f;

        Assert.That(SkydiveCloudLayers.DensityAt(between),
                    Is.EqualTo(SkydiveCloudLayers.BaseFogDensity).Within(1e-6f));
    }

    [Test]
    public void 층_사이에는_틈이_있다()
    {
        // 반두께가 간격의 절반보다 크면 코스 전체가 구름 속이 되어 버린다.
        float gap = SkydiveCloudLayers.Altitudes[0] - SkydiveCloudLayers.Altitudes[1];

        Assert.That(SkydiveCloudLayers.HalfThickness * 2f, Is.LessThan(gap),
                    "층 두께가 간격보다 작아야 들어갔다 나오는 게 느껴진다");
    }

    [Test]
    public void 층_한가운데가_가장_짙다()
    {
        float mid = SkydiveCloudLayers.Altitudes[1];

        Assert.That(SkydiveCloudLayers.DensityAt(mid),
                    Is.GreaterThan(SkydiveCloudLayers.BaseFogDensity * 2f));
    }

    [Test]
    public void 층을_연달아_지나도_값이_누적되지_않는다()
    {
        // 같은 고도를 여러 번 물어도 답이 같아야 한다 — 자기가 쓴 값을 기억하면 안 된다.
        float mid = SkydiveCloudLayers.Altitudes[2];
        float first = SkydiveCloudLayers.DensityAt(mid);

        for (int i = 0; i < 50; i++)
        {
            SkydiveCloudLayers.DensityAt(mid);
        }

        Assert.That(SkydiveCloudLayers.DensityAt(mid), Is.EqualTo(first).Within(1e-6f));
    }

    [Test]
    public void 가장자리에서는_부드럽게_들어간다()
    {
        float mid = SkydiveCloudLayers.Altitudes[1];
        float edge = mid + SkydiveCloudLayers.HalfThickness * 0.98f;

        float atEdge = SkydiveCloudLayers.DensityAt(edge);
        Assert.That(atEdge, Is.GreaterThan(SkydiveCloudLayers.BaseFogDensity));
        Assert.That(atEdge, Is.LessThan(SkydiveCloudLayers.DensityAt(mid)));
    }
}
