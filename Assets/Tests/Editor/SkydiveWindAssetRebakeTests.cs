using LOP.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 코스를 <b>두 번째로</b> 구울 때를 재현한다. 첫 굽기는 에셋을 새로 만드는 길이라 멀쩡하고,
/// 두 번째부터 "이미 있으니 내용만 덮는" 길로 갈라진다 — 잠복 버그가 여기서만 드러난다.
/// </summary>
public class SkydiveWindAssetRebakeTests
{
    private const string Folder = "Assets/__SkydiveWindAssetRebakeTests";
    private const string AssetPath = Folder + "/SkydiveWindArrow_Test.mat";
    private const string ExpectedName = "SkydiveWindArrow_Test";

    [SetUp]
    public void SetUp()
    {
        if (AssetDatabase.IsValidFolder(Folder) == false)
        {
            AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(Folder));
        }
    }

    [TearDown]
    public void TearDown()
    {
        AssetDatabase.DeleteAsset(Folder);
    }

    private static Material MakeMaterial(Color color)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", color);
        return material;
    }

    [Test]
    public void 두_번_구워도_에셋_이름이_유지된다()
    {
        SkydiveWindAssets.EnsureAsset(AssetPath, () => MakeMaterial(Color.red));
        Material second = SkydiveWindAssets.EnsureAsset(AssetPath, () => MakeMaterial(Color.blue));

        Assert.That(second.name, Is.EqualTo(ExpectedName));
    }

    [Test]
    public void 두_번_구워도_에셋_고유번호가_유지된다()
    {
        SkydiveWindAssets.EnsureAsset(AssetPath, () => MakeMaterial(Color.red));
        string firstGuid = AssetDatabase.AssetPathToGUID(AssetPath);

        SkydiveWindAssets.EnsureAsset(AssetPath, () => MakeMaterial(Color.blue));

        //  이 함수가 존재하는 이유 자체다 — 새로 만들면 고유번호가 바뀌어 씬 참조가 끊긴다.
        Assert.That(AssetDatabase.AssetPathToGUID(AssetPath), Is.EqualTo(firstGuid));
    }

    [Test]
    public void 두_번째_굽기가_내용을_갱신한다()
    {
        SkydiveWindAssets.EnsureAsset(AssetPath, () => MakeMaterial(Color.red));
        Material second = SkydiveWindAssets.EnsureAsset(AssetPath, () => MakeMaterial(Color.blue));

        //  이름을 지키느라 덮어쓰기 자체가 죽으면 색이 영영 안 바뀐다.
        Assert.That(second.GetColor("_BaseColor"), Is.EqualTo(Color.blue));
    }
}
