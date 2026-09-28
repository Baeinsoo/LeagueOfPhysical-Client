using UnityEngine;

namespace LOP.LookDev
{
    public enum LookDevExpression { Normal, Surprise, Cheer, Despair, Focus }

    /// <summary>표정 아틀라스(3열×2행)에서 표정 칸의 텍스처 스케일·오프셋. 위 줄부터 왼쪽→오른쪽.</summary>
    public static class LookDevFaceAtlas
    {
        public const int Columns = 3;
        public const int Rows = 2;

        public static Vector4 CellST(LookDevExpression expression)
        {
            int i = (int)expression;
            int col = i % Columns;
            int row = i / Columns;
            return new Vector4(1f / Columns, 1f / Rows, col / (float)Columns, (Rows - 1 - row) / (float)Rows);
        }
    }
}
