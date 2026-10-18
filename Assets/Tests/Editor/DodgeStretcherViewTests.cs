using System.Collections.Generic;
using System.Reflection;
using GameFramework.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 들것 몸의 룩 — 서버가 탈락과 디스폰을 같은 상태 메시지로 보내면, 들것을 띄우는 시점엔
    /// World 엔티티가 이미 지워져 있을 수 있다(DodgeStretcherView.Spawn 주석 참고). 그때도
    /// "몸이 살아 있던 동안 본 룩"을 들것 몸에 입혀야 한다.
    /// </summary>
    public class DodgeStretcherViewTests
    {
        private const string ChibiPath = "Assets/Characters/Chibi/Chibi.prefab";

        private static DodgeStateToC AliveMsg(int version, string id) =>
            new DodgeStateToC { Version = version }
                .With(id, lives: 3, eliminatedTick: -1);

        private static DodgeStateToC EliminatedMsg(int version, string id, long eliminatedTick) =>
            new DodgeStateToC { Version = version }
                .With(id, lives: 0, eliminatedTick: eliminatedTick);

        private static void SetChibiPrefab(DodgeStretcherView view)
        {
            var field = typeof(DodgeStretcherView).GetField("chibiPrefab", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(view, AssetDatabase.LoadAssetAtPath<GameObject>(ChibiPath));
        }

        // 위치 캐시(lastSeen)는 이 시험의 관심사가 아니다 — 룩 캐시만 검증하려고 직접 심어 둔다.
        private static void SeedLastSeen(DodgeStretcherView view, string id, Vector2 at)
        {
            var field = typeof(DodgeStretcherView).GetField("lastSeen", BindingFlags.NonPublic | BindingFlags.Instance);
            var lastSeen = (Dictionary<string, Vector2>)field.GetValue(view);
            lastSeen[id] = at;
        }

        [Test]
        public void 몸이_사라진_뒤_탈락해도_들것_몸에_룩이_붙는다()
        {
            const string id = "9";
            var state = new DodgeClientState();
            state.Apply(AliveMsg(1, id));

            var entityRegistry = new EntityRegistry();
            var entity = new Entity(id);
            entity.Add(new PlayerLook(new Dictionary<string, string> { ["hat"] = "hat_cube_red" }, "테스터", 1));
            entityRegistry.Add(entity);

            var catalog = new CosmeticCatalog(TestEconomyTables.Cosmetics, TestEconomyTables.CosmeticSlots, TestEconomyTables.Currencies);
            var view = new DodgeStretcherView(state, new ActorRegistry(), null, entityRegistry, catalog, new AppearanceColorPalette(TestEconomyTables.AppearanceColors));
            SetChibiPrefab(view);

            // 1틱: 몸(엔티티)이 아직 살아 있다 — 여기서 룩을 기억해 둬야 한다.
            view.LateTick();

            // 서버가 탈락과 함께 몸을 지운다 — 디스폰이 먼저 도착한 상황을 흉내낸다.
            entityRegistry.Remove(id);
            SeedLastSeen(view, id, new Vector2(1f, 2f));
            state.Apply(EliminatedMsg(2, id, eliminatedTick: 5));

            GameObject stretcher = null;
            try
            {
                // 2틱: 엔티티는 이미 없다 — 그래도 기억해 둔 룩으로 들것 몸을 입혀야 한다.
                view.LateTick();

                stretcher = GameObject.Find("Stretcher");
                Assert.IsNotNull(stretcher, "들것이 떠야 한다");
                var animator = stretcher.GetComponentInChildren<Animator>();
                Assert.IsNotNull(animator, "치비 몸이 들것에 붙어야 한다");
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                Assert.IsNotNull(head.Find("Look_hat"), "엔티티가 사라진 뒤에도 룩(모자)이 들것 몸에 남아야 한다");
            }
            finally
            {
                if (stretcher != null) Object.DestroyImmediate(stretcher);
            }
        }
    }

    internal static class DodgeStateToCTestExtensions
    {
        public static DodgeStateToC With(this DodgeStateToC message, string entityId, int lives, long eliminatedTick)
        {
            message.Players.Add(new DodgePlayerWire { EntityId = entityId, Lives = lives, EliminatedTick = eliminatedTick });
            return message;
        }
    }
}
