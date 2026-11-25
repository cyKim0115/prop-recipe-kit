using UnityEngine;

namespace PropRecipe
{
    /// <summary>재질 키·팔레트 텍스처 → 재질. 프로젝트 셰이더(툰 등)에 맞춘 구현을 넣는다.</summary>
    public interface IPropMaterialProvider
    {
        /// <summary>재질 키 하나의 재질. glow &gt; 0이면 발광.</summary>
        Material GetMaterial(string key, Color color, float glow);

        /// <summary>팔레트 모드(서브메시 1개)용 재질. palette = N×1 색 텍스처.</summary>
        Material GetPaletteMaterial(string propId, Texture2D palette);
    }
}
