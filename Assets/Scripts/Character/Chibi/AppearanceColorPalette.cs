using System.Collections.Generic;
using LOP.MasterData;
using UnityEngine;

namespace LOP
{
    public readonly struct AppearanceColorOption
    {
        public readonly int Id;
        public readonly string Name;
        public readonly Color Color;

        public AppearanceColorOption(int id, string name, Color color)
        {
            Id = id; Name = name; Color = color;
        }
    }

    /// <summary>
    /// 캐릭터 외형 색표(마스터데이터 TbAppearanceColor) — 번호를 색으로 바꾼다.
    /// 0(모름)·없는 번호·다른 부위의 번호는 그 부위 기본색으로 그린다 — 옛 서버나 지워진 색이 화면을 깨뜨리지 않게.
    /// </summary>
    public sealed class AppearanceColorPalette
    {
        public const string SkinPart = "skin";
        public const string HairPart = "hair";

        private readonly Dictionary<int, AppearanceColor> _byId = new();
        private readonly Dictionary<string, List<AppearanceColorOption>> _optionsByPart = new();
        private readonly Dictionary<string, int> _defaultByPart = new();

        [VContainer.Inject]
        public AppearanceColorPalette(LOPMasterData masterData) : this(masterData.Tables.TbAppearanceColor)
        {
        }

        public AppearanceColorPalette(TbAppearanceColor table)
        {
            var rows = new List<AppearanceColor>(table.DataList);
            rows.Sort((a, b) => a.DisplayOrder != b.DisplayOrder ? a.DisplayOrder.CompareTo(b.DisplayOrder) : a.Id.CompareTo(b.Id));
            foreach (var row in rows)
            {
                _byId[row.Id] = row;
                if (!_optionsByPart.TryGetValue(row.Part, out var options))
                {
                    _optionsByPart[row.Part] = options = new List<AppearanceColorOption>();
                }
                options.Add(new AppearanceColorOption(row.Id, row.Name, ParseHex(row.Hex)));
                if (row.IsDefault)
                {
                    _defaultByPart[row.Part] = row.Id;
                }
            }
        }

        public Color Skin(int id) => ColorOf(SkinPart, id);
        public Color Hair(int id) => ColorOf(HairPart, id);

        public IReadOnlyList<AppearanceColorOption> Options(string part) =>
            _optionsByPart.TryGetValue(part, out var options) ? options : (IReadOnlyList<AppearanceColorOption>)System.Array.Empty<AppearanceColorOption>();

        public int DefaultId(string part) => _defaultByPart.TryGetValue(part, out var id) ? id : 0;

        public string NameOf(int id) => _byId.TryGetValue(id, out var row) ? row.Name : "";

        /// <summary>그 부위의 번호가 맞으면 그 번호, 아니면 기본 번호.</summary>
        public int Resolve(string part, int id) => _byId.TryGetValue(id, out var row) && row.Part == part ? id : DefaultId(part);

        private Color ColorOf(string part, int id)
        {
            int resolved = Resolve(part, id);
            return _byId.TryGetValue(resolved, out var row) ? ParseHex(row.Hex) : Color.magenta;
        }

        private static Color ParseHex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
