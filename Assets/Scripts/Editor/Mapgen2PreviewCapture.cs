using System.IO;
using Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// Mapgen2 预览截图工具
    /// </summary>
    [InitializeOnLoad]
    public static class Mapgen2PreviewCapture
    {
        // 该菜单项同时作为自动视觉验证的入口点
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string RequestPath = "Temp/Mapgen2Capture.request";
        private const string OutputPath = "Artifacts/mapgen2-unity-preview.png";

        static Mapgen2PreviewCapture()
        {
            EditorApplication.update += CaptureWhenRequested;
        }

        private static void CaptureWhenRequested()
        {
            if (!File.Exists(RequestPath)) return;
            File.Delete(RequestPath);
            EditorApplication.delayCall += Capture;
        }

        [MenuItem("Tools/Mapgen2/Capture Preview")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var generator = Object.FindObjectOfType<VoronoiGenerator>();
            var camera = Camera.main;
            if (!generator || !camera)
            {
                Debug.LogError("Mapgen2 preview capture requires a VoronoiGenerator and Main Camera.");
                return;
            }

            generator.Generate();
            camera.orthographic = true;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.orthographicSize = Mathf.Max(generator.size.x, generator.size.y) * 0.5125f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0x44 / 255f, 0x44 / 255f, 0x7a / 255f, 1f);

            var target = RenderTexture.GetTemporary(1024, 1024, 24, RenderTextureFormat.ARGB32);
            var previousActive = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, 1024f, 1024f), 0, 0);
            image.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath) ?? "Artifacts");
            File.WriteAllBytes(OutputPath, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            Debug.Log("Mapgen2 preview captured: " + Path.GetFullPath(OutputPath));
        }
    }
}
