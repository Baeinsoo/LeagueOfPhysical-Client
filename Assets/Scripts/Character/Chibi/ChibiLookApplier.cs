using System.Globalization;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 룩(슬롯 코드→품목 코드)을 치비 몸에 반영한다 — 상·하의는 색을 덮고, 모자·장식은 원시도형(아트 없는
    /// 더미)을 붙이고, 얼굴은 표정을 고른다. 마스터데이터에 없는 코드·형식이 맞지 않는 값은 그 슬롯만
    /// 기본값으로 두고 나머지에 영향을 주지 않는다.
    /// </summary>
    public static class ChibiLookApplier
    {
        private const string HatSlotCode = "hat";
        private const string AccessorySlotCode = "accessory";
        private const string TopSlotCode = "top";
        private const string BottomSlotCode = "bottom";
        private const string FaceSlotCode = "face";

        private const string HatChildName = "Look_hat";
        private const string AccessoryChildName = "Look_accessory";
        private static readonly Vector3 HatLocalOffset = new Vector3(0f, 0.12f, 0f);
        private static readonly Vector3 AccessoryLocalOffset = new Vector3(0f, 0f, 0.08f);

        public readonly struct PrimitiveSpec
        {
            public readonly PrimitiveType Type;
            public readonly Color Color;
            public readonly float Size;

            public PrimitiveSpec(PrimitiveType type, Color color, float size)
            {
                Type = type;
                Color = color;
                Size = size;
            }
        }

        /// <summary>"#RRGGBB" 형식만 받는다 — 알파가 붙은 8자리·"#" 없는 값은 거부.</summary>
        public static bool TryParseHex(string hex, out Color color)
        {
            if (hex != null && hex.Length == 7 && hex[0] == '#' && ColorUtility.TryParseHtmlString(hex, out color))
            {
                return true;
            }
            color = Color.black;
            return false;
        }

        /// <summary>"cube:#RRGGBB:크기" / "sphere:#RRGGBB:크기" 형식. 모르는 도형·칸 수 불일치·숫자 아님은 거부.</summary>
        public static bool TryParsePrimitive(string key, out PrimitiveSpec spec)
        {
            spec = default;
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            var parts = key.Split(':');
            if (parts.Length != 3)
            {
                return false;
            }

            PrimitiveType type;
            if (parts[0] == "cube") { type = PrimitiveType.Cube; }
            else if (parts[0] == "sphere") { type = PrimitiveType.Sphere; }
            else { return false; }

            if (!TryParseHex(parts[1], out var color))
            {
                return false;
            }
            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var size))
            {
                return false;
            }

            spec = new PrimitiveSpec(type, color, size);
            return true;
        }

        /// <summary>엔티티 기본 색에서 시작해, 룩의 상·하의 틴트 품목이 있으면 그 슬롯만 덮는다.</summary>
        public static ChibiColors ColorsFor(string entityId, PlayerLook look, CosmeticCatalog catalog)
        {
            var baseColors = ChibiOutfit.ColorsFor(entityId);
            if (look == null || catalog == null)
            {
                return baseColors;
            }

            var top = TintOverride(look, catalog, TopSlotCode) ?? baseColors.Top;
            var bottom = TintOverride(look, catalog, BottomSlotCode) ?? baseColors.Bottom;
            return new ChibiColors(top, baseColors.Skin, baseColors.Hair, bottom, baseColors.Shoe);
        }

        private static Color? TintOverride(PlayerLook look, CosmeticCatalog catalog, string slotCode)
        {
            var item = catalog.ByCode(look.SlotOrNull(slotCode));
            if (item != null && item.AssetKind == "tint" && TryParseHex(item.AssetKey, out var color))
            {
                return color;
            }
            return null;
        }

        /// <summary>룩의 얼굴 품목이 표정(expression)이고 코드가 <see cref="ChibiExpression"/> 이름이면 그 표정, 아니면 기본(Normal).</summary>
        public static ChibiExpression ExpressionFor(PlayerLook look, CosmeticCatalog catalog)
        {
            if (look != null && catalog != null)
            {
                var item = catalog.ByCode(look.SlotOrNull(FaceSlotCode));
                if (item != null && item.AssetKind == "expression"
                    && System.Enum.TryParse<ChibiExpression>(item.AssetKey, out var expression))
                {
                    return expression;
                }
            }
            return ChibiExpression.Normal;
        }

        /// <summary>모자(머리 뼈)·장식(가슴 뼈)을 룩대로 다시 붙인다. 이미 붙어 있으면 지우고 새로 붙인다(재입힘 안전).</summary>
        public static void ApplyPrimitives(GameObject visual, PlayerLook look, CosmeticCatalog catalog)
        {
            ApplySlot(look, catalog, HatSlotCode, HatChildName, FindHead(visual), HatLocalOffset);
            ApplySlot(look, catalog, AccessorySlotCode, AccessoryChildName, FindChest(visual), AccessoryLocalOffset);
        }

        private static void ApplySlot(PlayerLook look, CosmeticCatalog catalog, string slotCode,
            string childName, Transform parent, Vector3 localOffset)
        {
            if (parent != null)
            {
                var existing = parent.Find(childName);
                if (existing != null)
                {
                    DestroySafely(existing.gameObject);
                }
            }

            if (look == null || catalog == null || parent == null)
            {
                return;
            }

            var item = catalog.ByCode(look.SlotOrNull(slotCode));
            if (item == null || item.AssetKind != "primitive" || !TryParsePrimitive(item.AssetKey, out var spec))
            {
                return;
            }

            var go = GameObject.CreatePrimitive(spec.Type);
            go.name = childName;

            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                DestroySafely(collider);
            }

            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = localOffset;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * spec.Size;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(renderer.sharedMaterial) { color = spec.Color };
        }

        /// <summary>
        /// 휴머노이드면 머리 뼈, 아니면(더미 리그 등) 이름이 "Head"인 자손으로 대신한다 — <see cref="ChibiFace"/>와
        /// 같은 방식.
        /// </summary>
        public static Transform FindHead(GameObject visual) => FindBone(visual, HumanBodyBones.Head, "Head");

        private static Transform FindChest(GameObject visual)
        {
            var bone = FindBone(visual, HumanBodyBones.Chest, null);
            if (bone != null)
            {
                return bone;
            }
            bone = FindBone(visual, HumanBodyBones.Spine, null);
            if (bone != null)
            {
                return bone;
            }
            return FindChildByName(visual.transform, "Chest") ?? FindChildByName(visual.transform, "Spine");
        }

        private static Transform FindBone(GameObject visual, HumanBodyBones bone, string fallbackName)
        {
            var animator = visual != null ? visual.GetComponent<Animator>() : null;
            if (animator != null && animator.isHuman)
            {
                var transform = animator.GetBoneTransform(bone);
                if (transform != null)
                {
                    return transform;
                }
            }
            return fallbackName != null ? FindChildByName(visual.transform, fallbackName) : null;
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            foreach (Transform child in root)
            {
                if (string.Equals(child.name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
                var found = FindChildByName(child, name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private static void DestroySafely(Object obj)
        {
            if (UnityEngine.Application.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
        }
    }
}
