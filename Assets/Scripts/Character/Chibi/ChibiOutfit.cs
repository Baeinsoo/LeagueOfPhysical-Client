using UnityEngine;

namespace LOP
{
    public readonly struct ChibiColors
    {
        public readonly Color Top, Skin, Hair, Bottom, Shoe, Outline;

        public ChibiColors(Color top, Color skin, Color hair, Color bottom, Color shoe)
        {
            Top = top; Skin = skin; Hair = hair; Bottom = bottom; Shoe = shoe;
            Outline = new Color(top.r * 0.4f, top.g * 0.4f, top.b * 0.4f, 1f);
        }
    }

    /// <summary>정해진 색(<see cref="ChibiColors"/>)을 치비 몸에 칠한다 — 색을 고르는 일은 <see cref="ChibiLookApplier.ColorsFor"/>가 한다.</summary>
    public static class ChibiOutfit
    {
        public static void Apply(GameObject visual, ChibiColors c)
        {
            var block = new MaterialPropertyBlock();
            foreach (var r in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.GetPropertyBlock(block);
                block.SetColor("_TopColor", c.Top);
                block.SetColor("_SleeveColor", c.Top);
                block.SetColor("_BottomColor", c.Bottom);
                block.SetColor("_ShoeColor", c.Shoe);
                block.SetColor("_SkinColor", c.Skin);
                block.SetColor("_HairColor", c.Hair);
                block.SetColor("_OutlineColor", c.Outline);
                r.SetPropertyBlock(block);
            }
        }
    }
}
