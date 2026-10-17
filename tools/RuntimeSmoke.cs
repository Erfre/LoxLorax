// Test-only plugin. Never included in the distributable mod.
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;

[BepInPlugin("com.loxlorax.tests.smoke", "Lox Lorax runtime test", "1.0.0")]
public sealed class RuntimeSmoke : BaseUnityPlugin
{
    private IEnumerator Start()
    {
        yield return new WaitForSeconds(8f);
        int exitCode = 1;
        try
        {
            string path = Environment.GetEnvironmentVariable("LOXLORAX_TEST_DIR");
            string resultPath = Path.Combine(path, "runtime-pass.txt");
            if (File.Exists(resultPath)) File.Delete(resultPath);
            var type = typeof(LoxLorax.Plugin).Assembly.GetType("LoxLorax.ModelAsset", true);
            var model = type.GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { Path.Combine(path, "lorax.llmesh") });
            var root = new GameObject("Isolated Lorax runtime test");
            root.transform.position = new Vector3(0f, 10000f, 0f);
            var visual = (Transform)type.GetMethod("Instantiate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(model, new object[] { root.transform, 30 });
            var camera = new GameObject("Lorax test camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.06f, .08f, .10f);
            camera.transform.position = root.transform.position + new Vector3(-1f, 1.1f, 4f);
            camera.transform.LookAt(root.transform.position + Vector3.up * .48f);
            camera.orthographic = true;
            camera.orthographicSize = .60f;
            camera.nearClipPlane = .1f; camera.farClipPlane = 20f;
            var light = new GameObject("Lorax test light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.2f; light.cullingMask = 1 << 30;
            light.transform.rotation = Quaternion.Euler(35, 145, 0);
            RenderSettings.ambientLight = new Color(.40f,.40f,.40f);
            var target = new RenderTexture(900, 900, 24);
            camera.targetTexture = target;
            // Warm the newly created render target and shader before sampling pixels.
            camera.Render();
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(900, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(path, "unity-preview.png"), ImageConversion.EncodeToPNG(image));
            int coloredPixels = 0;
            foreach (var pixel in image.GetPixels32())
                if (pixel.r > 80 && pixel.r > pixel.b * 1.4f) coloredPixels++;
            if (coloredPixels < 40000) throw new Exception("Model render is blank or incomplete: " + coloredPixels + " orange/gold pixels.");
            File.WriteAllText(Path.Combine(path, "runtime-pass.txt"),
                "PASS: model loaded, textures decoded, meshes instantiated, game shader rendered. Parts: " + visual.GetComponentsInChildren<MeshRenderer>().Length
                + "; shader: " + visual.GetComponentInChildren<MeshRenderer>().sharedMaterial.shader.name + "; colored pixels: " + coloredPixels);
            Logger.LogInfo("LOXLORAX_RUNTIME_PASS");
            exitCode = 0;
        }
        catch (Exception error)
        {
            Logger.LogError("LOXLORAX_RUNTIME_FAIL: " + error);
        }
        Application.Quit(exitCode);
    }
}
