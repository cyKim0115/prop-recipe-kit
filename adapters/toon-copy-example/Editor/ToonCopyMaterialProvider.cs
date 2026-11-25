using System.Collections.Generic;
using PropRecipe;
using UnityEditor;
using UnityEngine;

namespace PropRecipe.Adapters.ToonCopy
{
    /// <summary>
    /// 예시 어댑터: 「밝은 색 + 그림자 색」 두 색 속성을 받는 툰 셰이더 프로젝트용 재질 공급자.
    /// 새 셰이더·새 재질을 만들지 않고, 지정한 원본 툰 재질을 복사해 밝은 색·그림자 색만 바꾼다.
    /// 그림자 색은 밝은 색에서 계산한다(색상 살짝 붉게, 채도 ×1.25 + 0.08, 명도 ×0.72). 발광은 밝은 색 × 배율.
    /// 팔레트 모드는 색 텍스처 속성에 팔레트를 넣고 밝은 색을 흰색으로 둔다. 그림자 색이 재질당 하나인 셰이더에서는
    /// 색별 그림자가 사라져 모습이 달라진다. 색이 중요하면 PerMaterial로 굽는다.
    /// 속성 이름은 셰이더마다 다르므로 생성자에서 받는다(재질 인스펙터의 Debug 모드나 셰이더 소스에서 확인).
    /// </summary>
    public sealed class ToonCopyMaterialProvider : IPropMaterialProvider
    {
        private readonly string _sourceMaterialPath;
        private readonly string _materialFolder;
        private readonly string _litColorProperty;
        private readonly string _shadedColorProperty;
        private readonly string _litMapProperty;
        private readonly Color _paletteShaded;
        private readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();

        /// <param name="sourceMaterialPath">복사할 원본 툰 재질(.mat) 경로</param>
        /// <param name="materialFolder">복사본을 둘 폴더(Assets/...)</param>
        /// <param name="litColorProperty">밝은 색 속성 이름</param>
        /// <param name="shadedColorProperty">그림자 색 속성 이름</param>
        /// <param name="litMapProperty">색 텍스처 속성 이름(팔레트 모드에서만 씀, 없으면 null)</param>
        /// <param name="paletteShaded">팔레트 재질의 그림자 색(텍스처에 곱해짐)</param>
        public ToonCopyMaterialProvider(
            string sourceMaterialPath,
            string materialFolder,
            string litColorProperty,
            string shadedColorProperty,
            string litMapProperty = null,
            Color? paletteShaded = null)
        {
            _sourceMaterialPath = sourceMaterialPath;
            _materialFolder = materialFolder.Replace('\\', '/').TrimEnd('/');
            _litColorProperty = litColorProperty;
            _shadedColorProperty = shadedColorProperty;
            _litMapProperty = litMapProperty;
            _paletteShaded = paletteShaded ?? new Color(0.72f, 0.66f, 0.66f, 1f);
        }

        public Material GetMaterial(string key, Color color, float glow)
        {
            Material mat = CopyOfSource("PropM_" + key);
            if (mat == null) return null;
            Color lit = color;
            Color shaded = ShadeOf(color);
            if (glow > 0f)
            {
                lit = new Color(color.r * glow, color.g * glow, color.b * glow, 1f);
                shaded = new Color(lit.r * 0.85f, lit.g * 0.85f, lit.b * 0.85f, 1f);
            }
            mat.SetColor(_litColorProperty, lit);
            mat.SetColor(_shadedColorProperty, shaded);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public Material GetPaletteMaterial(string propId, Texture2D palette)
        {
            Material mat = CopyOfSource("PropM_" + propId + "_palette");
            if (mat == null) return null;
            if (!string.IsNullOrEmpty(_litMapProperty) && mat.HasProperty(_litMapProperty))
            {
                mat.SetTexture(_litMapProperty, palette);
            }
            else
            {
                Debug.LogWarning("[ToonCopyMaterialProvider] 원본 재질에 색 텍스처 속성이 없음: " + _sourceMaterialPath);
            }
            mat.SetColor(_litColorProperty, Color.white);
            mat.SetColor(_shadedColorProperty, _paletteShaded);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>밝은 색 → 그림자 색(점토 느낌).</summary>
        public static Color ShadeOf(Color lit)
        {
            Color.RGBToHSV(lit, out float h, out float s, out float v);
            h = Mathf.Repeat(h - 0.025f, 1f);
            s = Mathf.Clamp01(s * 1.25f + 0.08f);
            v *= 0.72f;
            return Color.HSVToRGB(h, s, v);
        }

        private Material CopyOfSource(string name)
        {
            if (_cache.TryGetValue(name, out Material cached) && cached != null) return cached;
            string path = _materialFolder + "/" + PropAssetUtil.SafeName(name) + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                PropAssetUtil.EnsureFolder(_materialFolder);
                if (!AssetDatabase.CopyAsset(_sourceMaterialPath, path))
                {
                    Debug.LogError("[ToonCopyMaterialProvider] 원본 재질 복사 실패: " + _sourceMaterialPath);
                    return null;
                }
                mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            _cache[name] = mat;
            return mat;
        }
    }
}
