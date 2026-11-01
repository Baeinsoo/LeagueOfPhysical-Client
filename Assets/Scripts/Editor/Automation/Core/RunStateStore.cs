using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace LOP.EditorTools.Automation
{
    /// <summary>state.json(이어 돌기)과 report.json(사람이 보는 요약)을 실행 폴더에 쓴다.</summary>
    public static class RunStateStore
    {
        public const string StateFile = "state.json";
        public const string ReportFile = "report.json";

        private static readonly JsonSerializerSettings StateSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = { new StringEnumConverter() },
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
        };

        private static readonly JsonSerializerSettings ReportSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = { new StringEnumConverter() },
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            //  칸 이름만 camelCase — 캡처 이름(사전 키)은 시나리오에 적힌 그대로 둔다.
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false } },
        };

        public static void Save(AutomationRunState state)
        {
            Directory.CreateDirectory(state.RunDir);
            WriteReplacing(Path.Combine(state.RunDir, StateFile), JsonConvert.SerializeObject(state, StateSettings));
            WriteReplacing(Path.Combine(state.RunDir, ReportFile), JsonConvert.SerializeObject(state.Report, ReportSettings));
        }

        public static AutomationRunState Load(string runDir)
        {
            string path = Path.Combine(runDir, StateFile);
            if (!File.Exists(path)) return null;
            try
            {
                return JsonConvert.DeserializeObject<AutomationRunState>(File.ReadAllText(path), StateSettings);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        //  임시 파일에 다 쓴 뒤 바꿔 단다 — 쓰는 도중 도메인이 다시 불러와져도 반쪽 JSON이 남지 않는다.
        //  File.Replace는 Delete+Move를 한 파일시스템 호출로 묶어 그 사이에 죽는 창을 없앤다.
        //  대상이 아직 없으면(첫 저장) Replace가 못 쓰므로 Move로 떨어진다.
        private static void WriteReplacing(string path, string text)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, text);
            if (File.Exists(path))
            {
                File.Replace(temp, path, null);
            }
            else
            {
                File.Move(temp, path);
            }
        }
    }
}
