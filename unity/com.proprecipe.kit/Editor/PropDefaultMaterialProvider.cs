using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PropRecipe
{
    /// <summary>
    /// 셰이더에 묶이지 않은 기본 재질 공급자. 현재 렌더 파이프라인의 기본 재질 셰이더(없으면 "Standard")로 재질을 만들고
    /// _BaseColor/_Color, _BaseMap/_MainTex, (발광) _EmissionColor 중 있는 속성만 채운다.
    /// assetFolder를 주면 그 폴더에 .mat 에셋으로 저장·갱신하고, 없으면 메모리 재질만 만든다.
    /// </summary>
    public sealed class PropDefaultMaterialProvider : IPropMaterialProvider
    {
        private static readonly string[] COLOR_PROPS = { "_BaseColor", "_Color" };
        private static readonly string[] TEXTURE_PROPS = { "_BaseMap", "_MainTex" };
        private const string EMISSION_PROP = "_EmissionColor";

        private readonly string _assetFolder;
        private readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();

        public PropDefaultMaterialProvider(string assetFolder = null)
        {
            _assetFolder = string.IsNullOrEmpty(assetFolder) ? null : assetFolder.Replace('\\', '/').TrimEnd('/');
        }

        /// <summary>현재 파이프라인 기본 재질의 셰이더, 없으면 Built-in "Standard".</summary>
        public static Shader ResolveShader()
        {
            RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null)
            {
                Material dm = rp.defaultMaterial;
                if (dm != null && dm.shader != null) return dm.shader;
            }
            Shader standard = Shader.Find("Standard");
            if (standard == null) Debug.LogError("[PropDefaultMaterialProvider] 쓸 셰이더를 찾지 못함");
            return standard;
        }

        public Material GetMaterial(string key, Color color, float glow)
        {
            Material mat = Obtain("PropM_" + key);
            SetColor(mat, color);
            if (mat.HasProperty(EMISSION_PROP))
            {
                if (glow > 0f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    mat.SetColor(EMISSION_PROP, new Color(color.r * glow, color.g * glow, color.b * glow, 1f));
                }
                else
                {
                    mat.DisableKeyword("_EMISSION");
                    mat.SetColor(EMISSION_PROP, Color.black);
                }
            }
            Commit(mat);
            return mat;
        }

        public Material GetPaletteMaterial(string propId, Texture2D palette)
        {
            Material mat = Obtain("PropM_" + propId + "_palette");
            SetColor(mat, Color.white);
            foreach (string prop in TEXTURE_PROPS) if (mat.HasProperty(prop)) mat.SetTexture(prop, palette);
            Commit(mat);
            return mat;
        }

        private static void SetColor(Material mat, Color color)
        {
            foreach (string prop in COLOR_PROPS) if (mat.HasProperty(prop)) mat.SetColor(prop, color);
        }

        private Material Obtain(string name)
        {
            if (_cache.TryGetValue(name, out Material cached) && cached != null) return cached;
            Material mat = null;
            string path = null;
            if (_assetFolder != null)
            {
                path = _assetFolder + "/" + name + ".mat";
                mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            if (mat == null)
            {
                mat = new Material(ResolveShader()) { name = name };
                if (path != null)
                {
                    PropAssetUtil.EnsureFolder(_assetFolder);
                    AssetDatabase.CreateAsset(mat, path);
                }
            }
            _cache[name] = mat;
            return mat;
        }

        private void Commit(Material mat)
        {
            if (_assetFolder != null) EditorUtility.SetDirty(mat);
        }
    }
}
