using System.Globalization;
using UnityEngine;

namespace PropRecipe
{
    /// <summary>PropMeasure.Measure 결과. Hash = 정점 xyz float 바이트 + 서브메시별 삼각형 인덱스 바이트의 SHA1 앞 16글자(대문자).</summary>
    public struct PropMeasureResult
    {
        public int Tris;
        public int Verts;
        public int Submeshes;
        /// <summary>"Position:Float32x3 Normal:Float32x3 ..."</summary>
        public string Attributes;
        public string IndexFormat;
        public long RuntimeBytes;
        /// <summary>에셋 파일 크기(에셋이 아니면 -1).</summary>
        public long FileBytes;
        public string AssetPath;
        public Vector3 Size;
        public Vector3 Center;
        public string Hash;

        public string ToJson(string label = null)
        {
            return "{" + $"\"label\":{PropJsonFormat.Str(label ?? "")},\"path\":{PropJsonFormat.Str(AssetPath ?? "")},\"tris\":{Tris},\"verts\":{Verts},\"submeshes\":{Submeshes}," +
                   $"\"attrs\":{PropJsonFormat.Str(Attributes)},\"index\":{PropJsonFormat.Str(IndexFormat)},\"runtime_bytes\":{RuntimeBytes},\"file_bytes\":{FileBytes}," +
                   $"\"size\":[{F(Size.x)},{F(Size.y)},{F(Size.z)}],\"center\":[{F(Center.x)},{F(Center.y)},{F(Center.z)}],\"hash\":\"{Hash}\"" + "}";
        }

        private static string F(float v) => v.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
