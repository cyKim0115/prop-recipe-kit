using System.Collections.Generic;

namespace PropRecipe
{
    /// <summary>소품 사양 한 건(사양 JSON과 1:1). Palette는 재질 키 → 색·발광.</summary>
    public sealed class PropSpec
    {
        public string Id;
        public readonly List<PropPart> Parts = new List<PropPart>();
        public readonly List<PropGroup> Groups = new List<PropGroup>();
        public readonly List<PropPoint> Points = new List<PropPoint>();
        public readonly Dictionary<string, PropPaletteEntry> Palette = new Dictionary<string, PropPaletteEntry>();

        /// <summary>부품 등장 순서대로 쓰인 재질 키(그룹 부품 포함).</summary>
        public List<string> UsedMaterials()
        {
            var used = new List<string>();
            foreach (PropPart part in Parts) if (!used.Contains(part.Mat)) used.Add(part.Mat);
            return used;
        }
    }
}
