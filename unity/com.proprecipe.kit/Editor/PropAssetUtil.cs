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

        /// <summary>
        /// 에셋 파일 이름으로 쓸 수 있게 정리한다(경로 구분자·상위 폴더 표기·파일 이름에 못 쓰는 문자는 밑줄로).
        /// 레시피·사양 JSON에서 온 키가 지정한 폴더 밖을 가리키지 못하게 한다.
        /// </summary>
        public static string SafeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "unnamed";
            char[] invalid = Path.GetInvalidFileNameChars();
            var builder = new System.Text.StringBuilder(name.Length);
            foreach (char ch in name)
            {
                bool bad = ch == Path.DirectorySeparatorChar || ch == Path.AltDirectorySeparatorChar || ch == (char)92
                    || ch == ':' || char.IsControl(ch) || System.Array.IndexOf(invalid, ch) >= 0;
                builder.Append(bad ? '_' : ch);
            }
            string safe = builder.ToString().Replace("..", "_").Trim(' ', '.');
            return safe.Length == 0 ? "unnamed" : safe;
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
