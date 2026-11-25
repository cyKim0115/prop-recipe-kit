using System.IO;
using UnityEditor;
using UnityEngine;

namespace PropRecipe
{
    /// <summary>에셋 저장 도우미.</summary>
    public static class PropAssetUtil
    {
        /// <summary>"Assets/..." 폴더를 AssetDatabase로 만든다.</summary>
        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent))
            {
                Debug.LogError("[PropAssetUtil] 폴더를 만들 수 없음: " + path);
                return;
            }
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>메시를 에셋으로 저장한다. 같은 경로가 있으면 내용을 덮어 GUID를 유지하고 기존 에셋을 돌려준다(이때 넘긴 mesh는 그대로 남으니 필요 없으면 호출한 쪽이 지운다).</summary>
        public static Mesh SaveMesh(Mesh mesh, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null)
            {
                EditorUtility.CopySerialized(mesh, old);
                EditorUtility.SetDirty(old);
                AssetDatabase.SaveAssets();
                return old;
            }
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            return mesh;
        }
    }
}
