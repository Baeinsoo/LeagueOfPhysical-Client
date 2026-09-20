#if UNITY_IOS
using System.IO;
using LOP.EditorTools;
using NUnit.Framework;
using UnityEditor.iOS.Xcode;

public class IOSBuildPostProcessTests
{
    private string plistPath;

    [SetUp]
    public void SetUp()
    {
        plistPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".plist");
        var plist = new PlistDocument();
        plist.root.SetString("CFBundleName", "test");
        plist.WriteToFile(plistPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(plistPath))
        {
            File.Delete(plistPath);
        }
    }

    private PlistDocument Read()
    {
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        return plist;
    }

    [Test]
    public void 개발빌드는_평문http를_허용한다()
    {
        IOSBuildPostProcess.Apply(plistPath, development: true);

        var ats = Read().root["NSAppTransportSecurity"].AsDict();
        Assert.IsTrue(ats["NSAllowsArbitraryLoads"].AsBoolean());
    }

    [Test]
    public void 개발빌드가_아니면_평문http를_열지_않는다()
    {
        IOSBuildPostProcess.Apply(plistPath, development: false);

        Assert.IsFalse(Read().root.values.ContainsKey("NSAppTransportSecurity"));
    }

    [Test]
    public void 암호화_수출_항목은_개발빌드가_아니어도_박힌다()
    {
        IOSBuildPostProcess.Apply(plistPath, development: false);

        Assert.IsFalse(Read().root["ITSAppUsesNonExemptEncryption"].AsBoolean());
    }
}
#endif
