using UnityEngine;

namespace PropRecipe
{
    /// <summary>재질 키 하나의 색과 발광 배율(0 = 발광 없음).</summary>
    public struct PropPaletteEntry
    {
        public Color Color;
        public float Glow;

        public PropPaletteEntry(Color color, float glow = 0f)
        {
            Color = color;
            Glow = glow;
        }

        /// <summary>"D98A55" 또는 "#D98A55".</summary>
        public static PropPaletteEntry FromHex(string hex, float glow = 0f) => new PropPaletteEntry(Hex(hex), glow);

        public static Color Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.white;
            if (!ColorUtility.TryParseHtmlString(hex[0] == '#' ? hex : "#" + hex, out Color c))
            {
                Debug.LogWarning("[PropPaletteEntry] 색 문자열을 읽지 못함: " + hex);
                return Color.white;
            }
            return c;
        }
    }
}
