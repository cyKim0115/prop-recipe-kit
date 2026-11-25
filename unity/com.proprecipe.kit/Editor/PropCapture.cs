using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PropRecipe
{
    /// <summary>
    /// 프리뷰 씬에서 직교 카메라로 소품을 찍는다. 네 방향(앞·오른쪽·뒤·왼쪽, 30도 내려봄) 색 + 마스크(흰색 무광),
    /// 한 바퀴 턴테이블(25도 내려봄). 재질은 호출한 쪽이 준다(PropMaterials.ForBake 등).
    /// </summary>
    public static class PropCapture
    {
        private static readonly Color BACKGROUND = new Color(0.62f, 0.65f, 0.63f);

        /// <summary>
        /// outDir에 &lt;prefix&gt;__{front|right|back|left}.png와 __mask.png 8장을 쓴다.
        /// frame = 화면 크기를 맞출 기준 경계(비교 촬영이면 같은 기준 메시 경계를 준다, 기본 = mesh.bounds).
        /// maskMaterial = null이면 흰색 Unlit 재질을 만든다.
        /// </summary>
        public static string Capture(Mesh mesh, Material[] materials, string outDir, string prefix, Bounds? frame = null, int px = 512,
            Material maskMaterial = null)
        {
            Bounds fb = frame ?? mesh.bounds;
            float ortho = Mathf.Max(fb.size.x, fb.size.y, fb.size.z) * 0.72f;
            var scene = EditorSceneManager.NewPreviewScene();
            var made = new List<Object>();
            try
            {
                var go = new GameObject("obj");
                SceneManager.MoveGameObjectToScene(go, scene);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = materials;
                AddLight(scene);
                Camera cam = AddCamera(scene, ortho, true);
                var rt = new RenderTexture(px, px, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                made.Add(rt);
                cam.targetTexture = rt;
                Material white = maskMaterial;
                if (white == null)
                {
                    white = CreateMaskMaterial();
                    made.Add(white);
                }
                Directory.CreateDirectory(outDir);
                string[] names = { "front", "right", "back", "left" };
                float[] yaw = { 180f, 270f, 0f, 90f };
                Vector3 center = fb.center;
                foreach (bool mask in new[] { false, true })
                {
                    mr.sharedMaterials = mask ? Enumerable.Repeat(white, materials.Length).ToArray() : materials;
                    cam.backgroundColor = mask ? Color.black : BACKGROUND;
                    for (int i = 0; i < 4; i++)
                    {
                        cam.transform.rotation = Quaternion.Euler(30f, yaw[i], 0f);
                        cam.transform.position = center - cam.transform.forward * 10f;
                        cam.Render();
                        RenderTexture.active = rt;
                        var shot = new Texture2D(px, px, TextureFormat.RGB24, false);
                        shot.ReadPixels(new Rect(0, 0, px, px), 0, 0);
                        shot.Apply();
                        RenderTexture.active = null;
                        File.WriteAllBytes(Path.Combine(outDir, $"{prefix}__{names[i]}{(mask ? "__mask" : "")}.png"), shot.EncodeToPNG());
                        Object.DestroyImmediate(shot);
                    }
                }
                cam.targetTexture = null;
                return $"{prefix}: 8 images (ortho {ortho:F3}) -> {outDir}";
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (var o in made) if (o != null) Object.DestroyImmediate(o);
            }
        }

        /// <summary>한 바퀴 도는 모습을 frames장 PNG(&lt;prefix&gt;__000.png …)로 찍는다. 카메라·조명은 Capture와 같고 25도 내려봄.</summary>
        public static string Turntable(Mesh mesh, Material[] materials, string outDir, string prefix, Bounds? frame = null, int frames = 90, int px = 480)
        {
            Bounds fb = frame ?? mesh.bounds;
            float ortho = Mathf.Max(fb.size.x, fb.size.y, fb.size.z) * 0.72f;
            var scene = EditorSceneManager.NewPreviewScene();
            var rt = new RenderTexture(px, px, 24, RenderTextureFormat.ARGB32);
            try
            {
                var go = new GameObject("obj");
                SceneManager.MoveGameObjectToScene(go, scene);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = materials;
                AddLight(scene);
                Camera cam = AddCamera(scene, ortho, false);
                cam.backgroundColor = BACKGROUND;
                cam.targetTexture = rt;
                cam.transform.rotation = Quaternion.Euler(25f, 180f, 0f);
                cam.transform.position = fb.center - cam.transform.forward * 10f;
                Directory.CreateDirectory(outDir);
                var shot = new Texture2D(px, px, TextureFormat.RGB24, false);
                for (int f = 0; f < frames; f++)
                {
                    go.transform.rotation = Quaternion.Euler(0f, 360f * f / frames, 0f);
                    cam.Render();
                    RenderTexture.active = rt;
                    shot.ReadPixels(new Rect(0, 0, px, px), 0, 0);
                    shot.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(outDir, $"{prefix}__{f:D3}.png"), shot.EncodeToPNG());
                }
                Object.DestroyImmediate(shot);
                cam.targetTexture = null;
                return $"{prefix}: {frames} frames -> {outDir}";
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(rt);
            }
        }

        /// <summary>마스크용 흰색 무광 재질(URP Unlit → HDRP Unlit → Unlit/Color 순으로 찾는다).</summary>
        public static Material CreateMaskMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("HDRP/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Debug.LogWarning("[PropCapture] Unlit 셰이더가 없어 기본 셰이더로 마스크를 찍음");
                shader = PropDefaultMaterialProvider.ResolveShader();
            }
            var mat = new Material(shader) { name = "PropCaptureMask" };
            foreach (string prop in new[] { "_BaseColor", "_Color", "_UnlitColor" }) if (mat.HasProperty(prop)) mat.SetColor(prop, Color.white);
            return mat;
        }

        private static void AddLight(Scene scene)
        {
            var lightGo = new GameObject("light");
            SceneManager.MoveGameObjectToScene(lightGo, scene);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        /// <summary>setClip = 원본 Capture처럼 near 0.01·far 50으로 둔다(Turntable은 카메라 기본값).</summary>
        private static Camera AddCamera(Scene scene, float ortho, bool setClip)
        {
            var camGo = new GameObject("cam");
            SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.AddComponent<Camera>();
            cam.scene = scene;
            cam.orthographic = true;
            cam.orthographicSize = ortho;
            cam.clearFlags = CameraClearFlags.SolidColor;
            if (setClip)
            {
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 50f;
            }
            return cam;
        }
    }
}
