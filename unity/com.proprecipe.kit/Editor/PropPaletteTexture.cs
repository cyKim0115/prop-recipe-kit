using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PropRecipe
{
    /// <summary>팔레트 텍스처(N×1, 포인트 필터, 밉 없음, 무압축, sRGB, Clamp) PNG를 만든다.</summary>
    public static class PropPaletteTexture
    {
        /// <summary>
        /// folder("Assets/..." 프로젝트 상대 경로)에 PT_&lt;propId&gt;.png를 쓰고 임포트 설정을 맞춘 텍스처 에셋을 돌려준다.
        /// order = 팔레트 칸 순서의 재질 키(PropBakeReport.MaterialOrder), palette에 없는 키는 흰색.
        /// </summary>
        public static Texture2D Create(string propId, IList<string> order, IReadOnlyDictionary<string, PropPaletteEntry> palette, string folder)
        {
            var colors = new Color[order.Count];
            for (int i = 0; i < order.Count; i++)
            {
                colors[i] = palette != null && palette.TryGetValue(order[i], out PropPaletteEntry e) ? e.Color : Color.white;
            }
            return Create(propId, colors, folder);
        }

        public static Texture2D Create(string propId, IList<Color> colors, string folder)
        {
            PropAssetUtil.EnsureFolder(folder);
            var tex = new Texture2D(Mathf.Max(1, colors.Count), 1, TextureFormat.RGB24, false);
            for (int i = 0; i < colors.Count; i++) tex.SetPixel(i, 0, colors[i]);
            tex.Apply();
            string path = folder.TrimEnd('/') + "/PT_" + PropAssetUtil.SafeName(propId) + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti))
            {
                Debug.LogError("[PropPaletteTexture] TextureImporter를 찾지 못함: " + path);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            ti.textureType = TextureImporterType.Default;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.None;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
