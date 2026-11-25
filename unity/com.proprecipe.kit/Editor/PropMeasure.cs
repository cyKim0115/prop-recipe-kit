using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PropRecipe
{
    /// <summary>메시 크기·구성·해시를 잰다. 해시는 원본 파일럿과 같은 바이트 배치를 쓴다.</summary>
    public static class PropMeasure
    {
        public static PropMeasureResult Measure(Mesh m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            var attrs = m.GetVertexAttributes().Select(a => a.attribute + ":" + a.format + "x" + a.dimension);
            string path = AssetDatabase.GetAssetPath(m);
            return new PropMeasureResult
            {
                Tris = m.triangles.Length / 3,
                Verts = m.vertexCount,
                Submeshes = m.subMeshCount,
                Attributes = string.Join(" ", attrs),
                IndexFormat = m.indexFormat.ToString(),
                RuntimeBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(m),
                AssetPath = string.IsNullOrEmpty(path) ? null : path,
                FileBytes = !string.IsNullOrEmpty(path) && File.Exists(path) ? new FileInfo(path).Length : -1,
                Size = m.bounds.size,
                Center = m.bounds.center,
                Hash = Hash(m),
            };
        }

        /// <summary>정점 x,y,z(float, BitConverter 바이트) → 서브메시 0..n-1의 GetTriangles 인덱스(int 바이트) → SHA1 → 16진 앞 16글자.</summary>
        public static string Hash(Mesh m)
        {
            using (var sha = System.Security.Cryptography.SHA1.Create())
            {
                var bytes = new List<byte>();
                foreach (var v in m.vertices) { bytes.AddRange(BitConverter.GetBytes(v.x)); bytes.AddRange(BitConverter.GetBytes(v.y)); bytes.AddRange(BitConverter.GetBytes(v.z)); }
                for (int s = 0; s < m.subMeshCount; s++) foreach (int i in m.GetTriangles(s)) bytes.AddRange(BitConverter.GetBytes(i));
                return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").Substring(0, 16);
            }
        }
    }
}
