using System.Collections.Generic;
using UnityEngine;

namespace PropRecipe
{
    /// <summary>구운 메시의 서브메시 순서에 맞는 재질 배열을 공급자에서 받는다.</summary>
    public static class PropMaterials
    {
        /// <summary>
        /// PerMaterial: report.MaterialOrder 순서대로 GetMaterial(key, 색, 발광). palette에 없는 키는 흰색.
        /// Palette: GetPaletteMaterial(propId, paletteTexture) 하나(서브메시 수만큼 반복).
        /// </summary>
        public static Material[] ForBake(IPropMaterialProvider provider, PropBakeReport report,
            IReadOnlyDictionary<string, PropPaletteEntry> palette, string propId, Texture2D paletteTexture = null)
        {
            if (report.Mode == PropBakeMode.Palette)
            {
                if (paletteTexture == null) Debug.LogWarning("[PropMaterials] 팔레트 모드인데 paletteTexture가 없음");
                Material pm = provider.GetPaletteMaterial(propId, paletteTexture);
                var arr = new Material[Mathf.Max(1, report.Submeshes)];
                for (int i = 0; i < arr.Length; i++) arr[i] = pm;
                return arr;
            }
            var mats = new Material[report.MaterialOrder.Count];
            for (int i = 0; i < mats.Length; i++)
            {
                string key = report.MaterialOrder[i];
                PropPaletteEntry e = palette != null && palette.TryGetValue(key, out PropPaletteEntry pe) ? pe : new PropPaletteEntry(Color.white, 0f);
                mats[i] = provider.GetMaterial(key, e.Color, e.Glow);
            }
            return mats;
        }
    }
}
