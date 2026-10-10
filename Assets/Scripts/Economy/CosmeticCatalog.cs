using System.Collections.Generic;
using LOP.MasterData;

namespace LOP
{
    /// <summary>
    /// <c>TbCosmetic</c>/<c>TbCosmeticSlot</c>/<c>TbCurrency</c>를 코드로 찾기 쉽게 색인해 두는
    /// side-local 어댑터. 품목·슬롯·가격은 전부 마스터데이터가 진실원본이고, 여기선 숫자를
    /// 하나도 박지 않는다(코인 통화 id도 코드 "COIN"으로 찾는다).
    /// </summary>
    public class CosmeticCatalog
    {
        private readonly Dictionary<string, Cosmetic> _byCode = new();
        private readonly Dictionary<int, Cosmetic> _byId = new();
        private readonly Dictionary<int, List<Cosmetic>> _itemsBySlot = new();
        private readonly Dictionary<int, CosmeticSlot> _slotsById = new();
        private readonly Dictionary<string, CosmeticSlot> _slotsByCode = new();
        private readonly List<CosmeticSlot> _slotsInOrder = new();
        private readonly int _coinCurrencyId;

        //  코인으로 지불하는 통화의 코드. 마스터데이터 쪽 명명(infrastructure/table/Datas/#Currency.xlsx)과 맞춘다.
        private const string CoinCurrencyCode = "COIN";

        [VContainer.Inject]
        public CosmeticCatalog(LOPMasterData masterData)
            : this(masterData.Tables.TbCosmetic, masterData.Tables.TbCosmeticSlot, masterData.Tables.TbCurrency)
        {
        }

        public CosmeticCatalog(TbCosmetic cosmetics, TbCosmeticSlot slots, TbCurrency currencies)
        {
            foreach (var slot in slots.DataList)
            {
                _slotsById[slot.Id] = slot;
                _slotsByCode[slot.Code] = slot;
                _slotsInOrder.Add(slot);
            }
            //  표의 display_order대로 탭을 그리기 위한 정렬. 같은 순서 값이면 id로 묶어 결정적으로 둔다.
            _slotsInOrder.Sort((a, b) => a.DisplayOrder != b.DisplayOrder ? a.DisplayOrder.CompareTo(b.DisplayOrder) : a.Id.CompareTo(b.Id));

            foreach (var item in cosmetics.DataList)
            {
                _byCode[item.Code] = item;
                _byId[item.Id] = item;

                if (_itemsBySlot.TryGetValue(item.SlotId, out var list) == false)
                {
                    list = new List<Cosmetic>();
                    _itemsBySlot[item.SlotId] = list;
                }
                list.Add(item);
            }

            foreach (var currency in currencies.DataList)
            {
                if (currency.Code == CoinCurrencyCode)
                {
                    _coinCurrencyId = currency.Id;
                    break;
                }
            }
        }

        /// <summary>코인으로 지불하는 통화의 id. 가격 줄에서 이 id를 찾아 코인 가격을 뽑는다.</summary>
        public int CoinCurrencyId => _coinCurrencyId;

        public Cosmetic ByCode(string code) => code != null && _byCode.TryGetValue(code, out var item) ? item : null;

        public Cosmetic ById(int id) => _byId.TryGetValue(id, out var item) ? item : null;

        /// <summary>그 슬롯에 속한 품목 전부(기본 품목 포함). 표 순서를 그대로 유지한다.</summary>
        public IReadOnlyList<Cosmetic> ItemsOfSlot(int slotId) =>
            _itemsBySlot.TryGetValue(slotId, out var list) ? list : System.Array.Empty<Cosmetic>();

        /// <summary>그 슬롯의 기본 품목(장착한 보유 인스턴스가 없을 때 대신 그릴 것).</summary>
        public Cosmetic DefaultOf(int slotId) =>
            _slotsById.TryGetValue(slotId, out var slot) ? ById(slot.DefaultCosmeticId) : null;

        /// <summary>코인 가격. 코인으로 안 파는 품목(비매품·다른 통화 전용)이면 null.</summary>
        public long? CoinPriceOf(Cosmetic item)
        {
            if (item == null) return null;

            foreach (var price in item.Prices)
            {
                if (price.CurrencyId == _coinCurrencyId) return price.Amount;
            }
            return null;
        }

        public CosmeticSlot SlotByCode(string code) => code != null && _slotsByCode.TryGetValue(code, out var slot) ? slot : null;

        /// <summary>표시 순서(display_order)대로 슬롯 전체. 상점 탭을 이 순서로 그린다.</summary>
        public IReadOnlyList<CosmeticSlot> Slots => _slotsInOrder;
    }
}
