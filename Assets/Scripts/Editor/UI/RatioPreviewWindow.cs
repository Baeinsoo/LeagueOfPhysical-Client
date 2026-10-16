using System.Collections.Generic;
using LOP.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.EditorTools
{
    /// <summary>
    /// 카탈로그의 모든 화면을 세 비율(16:9 · 20:9 폰 · 4:3 태블릿)로 찍는다 — 패널 크기 설정을 바꿀 때 한눈에 보는 용도.
    /// 데이터 없이 UXML·USS만 띄우므로 글자는 기본값 그대로다. 배치(넘침·겹침·모서리 붙음)만 본다.
    /// </summary>
    public class RatioPreviewWindow : EditorWindow
    {
        private const string Catalog = "Assets/UI/UIViewCatalog.asset";

        //  패널은 Expand — 기준(1920×1080)보다 작아지지 않고 남는 쪽으로만 넓어진다.
        private static readonly (int width, int height, string tag)[] Sizes =
        {
            (1920, 1080, "16x9"),
            (2400, 1080, "20x9"),
            (1920, 1440, "4x3"),
        };

        [MenuItem("LOP/UI/비율 미리보기 PNG 저장 (전체 화면)")]
        public static void SaveAll()
        {
            var shots = new List<(System.Func<EditorWindow>, string, int, int)>();
            var entries = new SerializedObject(AssetDatabase.LoadAssetAtPath<UIViewCatalog>(Catalog)).FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                var e = entries.GetArrayElementAtIndex(i);
                var uxml = e.FindPropertyRelative("uxml").objectReferenceValue as VisualTreeAsset;
                if (uxml == null) continue;
                var uss = e.FindPropertyRelative("uss").objectReferenceValue as StyleSheet;
                string view = e.FindPropertyRelative("viewName").stringValue;
                foreach (var (width, height, tag) in Sizes)
                {
                    shots.Add((() =>
                    {
                        var w = GetWindow<RatioPreviewWindow>("비율 미리보기");
                        w.Build(uxml, uss, width, height, view, ranked: false);
                        return w;
                    }, $"output/ratio/{view}-{tag}.png", width, height));
                    //  상점은 구매 확인 겹창이 열린 모습도 따로 본다.
                    if (view == nameof(ShopView))
                    {
                        shots.Add((() =>
                        {
                            var w = GetWindow<RatioPreviewWindow>("비율 미리보기");
                            w.Build(uxml, uss, width, height, view, ranked: true);
                            return w;
                        }, $"output/ratio/{view}-confirm-{tag}.png", width, height));
                    }
                    //  결과판은 랭크 반전판도 따로 본다.
                    if (view == nameof(MatchResultView))
                    {
                        shots.Add((() =>
                        {
                            var w = GetWindow<RatioPreviewWindow>("비율 미리보기");
                            w.Build(uxml, uss, width, height, view, ranked: true);
                            return w;
                        }, $"output/ratio/{view}-ranked-{tag}.png", width, height));
                    }
                }
            }
            UiSnapshot.SaveAll(shots);
        }

        private void Build(VisualTreeAsset uxml, StyleSheet uss, int width, int height, string view, bool ranked)
        {
            var root = rootVisualElement;
            root.Clear();
            root.styleSheets.Clear();
            root.style.backgroundColor = Color.black;
            //  게임과 같은 테마(유니티 기본 테마 + Theme + Kit)를 쓴다 — 기본 테마가 빠지면 꺼진 버튼처럼
            //  기본 테마 규칙이 이기는 모습을 미리보기에서 못 본다.
            var theme = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/Theme/LopTheme.tss");
            if (theme != null) root.styleSheets.Add(theme);
            var host = UiSnapshot.AddHost(root, width, height);
            //  WindowManager.Open과 같은 모양 — 화면 전체에 absolute로 깔고 뷰 USS를 붙인다.
            var viewRoot = uxml.Instantiate();
            viewRoot.style.position = Position.Absolute;
            viewRoot.style.left = 0;
            viewRoot.style.top = 0;
            viewRoot.style.right = 0;
            viewRoot.style.bottom = 0;
            if (uss != null) viewRoot.styleSheets.Add(uss);
            host.Add(viewRoot);
            if (view == nameof(MatchResultView)) FillResultBoard(viewRoot, ranked);
            //  메뉴 셸은 긴 이름을 섞은 가짜 값으로 채운다(넘침·겹침 확인용). ranked = 상점의 확인 창 열린 판.
            if (view == nameof(ShopView)) FillShop(viewRoot, confirmOpen: ranked);
            if (view == nameof(LockerView)) FillLocker(viewRoot);
            if (view == nameof(ProfileView)) FillProfile(viewRoot);
            if (view == nameof(ChangeDisplayNameView)) FillRename(viewRoot);
            FillShell(viewRoot, view);
        }

        private const string LongItem = "아주아주 긴 이름의 반짝반짝 황금 왕관 모자";
        private const string LongPlayer = "가나다라마바사아자차카타#1234";

        private static void FillShell(VisualElement root, string view)
        {
            var title = root.Q<Label>("shell-title");
            if (title == null) return;
            title.text = view switch
            {
                nameof(ShopView) => "상점",
                nameof(LockerView) => "라커룸",
                nameof(ProfileView) => "선수 카드",
                _ => "설정",
            };
            var coin = root.Q<Label>("shell-coin-text");
            if (coin != null) coin.text = "🪙 12,340";
        }

        private static void FillShop(VisualElement root, bool confirmOpen)
        {
            var tabs = root.Q("shop-tabs");
            string[] slots = { "모자", "상의", "신발" };
            for (int i = 0; i < slots.Length; i++)
            {
                var tab = new Button();
                tab.AddToClassList("kit-press");
                tab.AddToClassList("kit-chip");
                tab.AddToClassList("shop-tab");
                if (i == 0) tab.AddToClassList("kit-chip--picked");
                var text = new Label(slots[i]);
                text.AddToClassList("kit-chip__text");
                tab.Add(text);
                tabs.Add(tab);
            }

            var grid = root.Q<ScrollView>("shop-grid");
            (string name, string state, bool dim)[] cards =
            {
                ("빨간 큐브 모자", "🪙 300", false), ("파란 큐브 모자", "🪙 20,000", true), ("초록 모자", "✓ 보유", false),
                ("노란 모자", "★ 장착 중", false), ("모자 없음", "기본", false), ("이벤트 모자", "비매품", false),
                (LongItem, "🪙 1,200", false), ("줄무늬 모자", "🪙 500", false),
            };
            for (int i = 0; i < cards.Length; i++)
            {
                var card = ShopView.BuildCard(cards[i].name, i, null, out var state);
                state.text = cards[i].state;
                state.EnableInClassList("shop-card__state--dim", cards[i].dim);
                card.EnableInClassList("shop-card--picked", i == 6);
                grid.Add(card);
            }

            KitEllipsis.SetText(root.Q<Label>("shop-bar-name"), LongItem);
            root.Q<Label>("shop-bar-state").text = "🪙 1,200";
            root.Q<Label>("shop-message").text = "코인이 부족합니다";
            root.Q<Label>("shop-message").style.display = DisplayStyle.Flex;
            //  꺼진 버튼 모양도 본다(잔액 부족이라 [구매] 잠김).
            root.Q<Button>("buy-button").SetEnabled(false);

            if (!confirmOpen) return;
            root.Q("shop-confirm").AddToClassList("shop-confirm--open");
            root.Q<Label>("confirm-question").text = ShopFormat.ConfirmQuestion(LongItem, 1200);
            root.Q<Label>("confirm-remaining").text = ShopFormat.RemainingCoins(12340, 1200);
        }

        private static void FillLocker(VisualElement root)
        {
            var slots = root.Q<ScrollView>("locker-slots");
            (string slot, string item)[] rows = { ("모자", LongItem), ("상의", "초록 셔츠"), ("하의", "기본 바지"), ("신발", "기본 신발"), ("장갑", "기본 장갑") };
            for (int i = 0; i < rows.Length; i++)
            {
                var button = new Button();
                button.AddToClassList("kit-press");
                button.AddToClassList("locker-slot");
                if (i == 0) button.AddToClassList("locker-slot--picked");
                var sticker = new StickerElement();
                var name = new Label(rows[i].slot);
                name.AddToClassList("locker-slot__name");
                var equipped = new Label();
                equipped.AddToClassList("kit-title");
                equipped.AddToClassList("locker-slot__equipped");
                KitEllipsis.SetText(equipped, rows[i].item);
                sticker.Add(name);
                sticker.Add(equipped);
                button.Add(sticker);
                slots.Add(button);
            }

            var grid = root.Q<ScrollView>("locker-grid");
            var items = new[] { new LockerItem(1, "모자 없음", false, true), new LockerItem(2, LongItem, true, false), new LockerItem(3, "빨간 큐브 모자", false, false), new LockerItem(4, "초록 모자", false, false) };
            for (int i = 0; i < items.Length; i++)
            {
                var card = ShopView.BuildCard(items[i].Name, i, null, out var state);
                state.text = LockerView.StateLine(items[i]);
                card.EnableInClassList("shop-card--picked", items[i].Equipped);
                grid.Add(card);
            }
            root.Q<Label>("locker-message").text = "잠시 뒤 다시 시도";
            //  응답을 기다리는 동안의 모습 — [전부 기본으로]와 카드가 꺼진다.
            root.Q<Button>("locker-reset").SetEnabled(false);
            foreach (var child in grid.Children()) child.SetEnabled(false);
        }

        private static void FillProfile(VisualElement root)
        {
            KitEllipsis.SetText(root.Q<Label>("profile-name"), LongPlayer);
            root.Q<Label>("profile-status").style.display = DisplayStyle.None;
            root.Q<Label>("profile-level").text = ProfileView.LevelLine("Lv 8", "360 / 1,000");

            var rank = root.Q("profile-rank");
            var line = new Label("골드 II · 45 LP");
            line.AddToClassList("kit-title");
            line.AddToClassList("profile-rank__line");
            line.AddToClassList("rank-tier--gold");
            rank.Add(line);
            var peak = new Label("이번 시즌 최고: 골드 I");
            peak.AddToClassList("profile-rank__peak");
            rank.Add(peak);

            var records = root.Q("profile-records");
            foreach (var stats in new[] { new ProfileQueueStats("일반", true, 128, 31, "2.7"), new ProfileQueueStats("랭크", true, 40, 9, "3.1") })
            {
                var record = new Label();
                record.AddToClassList("profile-records__line");
                KitEllipsis.SetText(record, ProfileView.RecordLine(stats));
                records.Add(record);
            }
            KitEllipsis.SetText(root.Q<Label>("profile-equipped"), "장착 중 · 모자: " + LongItem + " · 상의: 초록 셔츠 · 신발: 기본 신발");

            var history = root.Q<ScrollView>("profile-history");
            var rows = new List<MatchResultRow>();
            string[] names = { "1P 선수", LongPlayer, "3P 선수", "4P 선수", "5P 선수", "6P 선수", "7P 선수", "8P 선수" };
            for (int i = 0; i < names.Length; i++) rows.Add(new MatchResultRow(i + 1, names[i], i == 1, false, false, 0, 0, 0, isLeft: i == 7));
            history.Add(ProfileView.BuildMatchCard(new ProfileMatchEntry("플래피 레이스", "일반 · 아주 긴 경기장 이름의 광산", "10-16 21:04", "2등", true, rows), open: true));
            history.Add(ProfileView.BuildMatchCard(new ProfileMatchEntry("스카이다이브", "랭크 · 피라미드", "10-16 20:51", "1등", true, rows), open: false));
            history.Add(ProfileView.BuildMatchCard(new ProfileMatchEntry("활쏘기", "일반 · 한 발 승부", "10-15 23:10", "5등", true, rows), open: false));
        }

        private static void FillRename(VisualElement root)
        {
            root.Q<TextField>("cdn-input").value = "재나르도";
            var message = root.Q<Label>("cdn-message");
            message.text = "이름은 2~12자여야 합니다";
            message.style.display = DisplayStyle.Flex;
        }

        //  결과판은 하얀 덮개·숨김으로 시작한다 — 등장이 끝난 모습을 가짜 8인 결과(나 2위, 1명 나감)로 채운다.
        private static void FillResultBoard(VisualElement root, bool ranked)
        {
            root.Q("matchresult-root")?.EnableInClassList("ranked", ranked);
            root.Q("matchresult-flash")?.RemoveFromClassList("matchresult-flash--on");
            root.Q("matchresult-board")?.RemoveFromClassList("matchresult-board--out");
            root.Q("confirm-button")?.RemoveFromClassList("matchresult-confirm--out");
            root.Query(className: "matchresult-lt--out").ForEach(e => e.RemoveFromClassList("matchresult-lt--out"));
            root.Q<Label>("matchresult-tag").text = ranked ? "RANKED" : "LIVE";
            root.Q<Label>("matchresult-title").text = ranked ? "리그 경기 결과" : "경기 결과";
            root.Q<Label>("matchresult-subtitle").text = ranked ? "스카이다이브 · 피라미드" : "플래피 레이스 · 광산";
            root.Q<Label>("matchresult-rank-tag").text = "승급";
            root.Q<Label>("matchresult-rank-tag").AddToClassList("matchresult-lt__tag--promoted");
            root.Q<Label>("matchresult-rank-body").text = "골드 II → 골드 I  +18 LP";
            root.Q("matchresult-rank").style.display = ranked ? DisplayStyle.Flex : DisplayStyle.None;
            root.Q<Label>("matchresult-reward-body").text = "오늘 첫 승 ×3 · +150 코인 · +360 XP · Lv 7 → 8";

            var rows = root.Q("matchresult-rows");
            string[] names = { "가나다라마바사아자차카타#1234", "재나르도#1234", "3P 선수", "4P 선수", "5P 선수", "6P 선수", "7P 선수", "8P 선수" };
            for (int i = 0; i < names.Length; i++)
            {
                var line = new VisualElement();
                line.AddToClassList("matchresult-row");
                if (i == 1) line.AddToClassList("matchresult-row--me");
                if (i == 7) line.AddToClassList("matchresult-row--gone");
                var rank = new Label((i + 1).ToString());
                rank.AddToClassList("kit-title");
                rank.AddToClassList("matchresult-row__rank");
                line.Add(rank);
                var name = new Label();
                name.AddToClassList("matchresult-row__name");
                line.Add(name);
                KitEllipsis.SetText(name, names[i]);
                if (i == 7)
                {
                    var left = new Label("나감");
                    left.AddToClassList("matchresult-row__left");
                    line.Add(left);
                }
                rows.Add(line);
            }
        }
    }
}
