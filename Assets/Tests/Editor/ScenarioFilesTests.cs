using System;
using System.IO;
using System.Linq;
using LOP.EditorTools.Automation;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>--scenario 인자(spec §7.1) — 이름은 Automation/Scenarios 아래, .json 경로도 되지만 프로젝트 밖은 안 된다.</summary>
    public class ScenarioFilesTests
    {
        private string _project;

        [SetUp]
        public void SetUp()
        {
            _project = Path.Combine(Path.GetTempPath(), "lop-automation-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_project, ScenarioFiles.Folder));
            File.WriteAllText(Path.Combine(_project, ScenarioFiles.Folder, "b.json"), "{}");
            File.WriteAllText(Path.Combine(_project, ScenarioFiles.Folder, "a.json"), "{}");
            File.WriteAllText(Path.Combine(_project, "other.json"), "{}");
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_project, true);

        [Test]
        public void 이름은_Automation_Scenarios_아래_json이다()
        {
            Assert.IsTrue(ScenarioFiles.TryResolve("a", _project, out var path, out _));
            Assert.AreEqual(Path.GetFullPath(Path.Combine(_project, ScenarioFiles.Folder, "a.json")), path);
        }

        [Test]
        public void json_경로도_되지만_프로젝트_밖은_안_된다()
        {
            Assert.IsTrue(ScenarioFiles.TryResolve("other.json", _project, out _, out _));

            Assert.IsFalse(ScenarioFiles.TryResolve("../x.json", _project, out _, out var why));
            StringAssert.Contains("밖", why);
            Assert.IsFalse(ScenarioFiles.TryResolve("/etc/hosts.json", _project, out _, out _));
        }

        [Test]
        public void 없는_파일은_이유와_함께_거절한다()
        {
            Assert.IsFalse(ScenarioFiles.TryResolve("nope", _project, out _, out var why));
            StringAssert.Contains("없다", why);
        }

        [Test]
        public void 목록은_이름순이고_확장자가_없다()
        {
            CollectionAssert.AreEqual(new[] { "a", "b" }, ScenarioFiles.List(_project).ToArray());
        }
    }
}
