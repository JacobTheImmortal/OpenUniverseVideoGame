using UnityEditor;
using UnityEngine;
using System.IO;

public class ChatGPTSyncTool : EditorWindow
{
    private string exportPath = "ChatGPTSync";

    [MenuItem("Tools/ChatGPT Sync Tool")]
    public static void ShowWindow()
    {
        GetWindow<ChatGPTSyncTool>("ChatGPT Sync");
    }

    void OnGUI()
    {
        GUILayout.Label("Export Project Snapshot for ChatGPT", EditorStyles.boldLabel);

        exportPath = EditorGUILayout.TextField("Export Folder", exportPath);

        if (GUILayout.Button("Capture Snapshot"))
        {
            Directory.CreateDirectory(exportPath);

            CaptureSceneView();
            CaptureGameView();
            ExportDebugInfo();
        }
    }

    void CaptureSceneView()
    {
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            var rt = new RenderTexture(1920, 1080, 24);
            sceneView.camera.targetTexture = rt;
            sceneView.camera.Render();
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(Path.Combine(exportPath, "SceneView.png"), bytes);

            RenderTexture.active = null;
            sceneView.camera.targetTexture = null;
        }
    }

    void CaptureGameView()
    {
        ScreenCapture.CaptureScreenshot(Path.Combine(exportPath, "GameView.png"));
    }

    void ExportDebugInfo()
    {
        var mainCam = Camera.main;
        var player = GameObject.FindWithTag("Player");

        string info =
            $"Time: {System.DateTime.Now}\n" +
            $"Main Camera Pos: {mainCam?.transform.position}\n" +
            $"Main Camera Rot: {mainCam?.transform.rotation}\n" +
            $"Player Pos: {player?.transform.position}\n" +
            $"Skybox: {RenderSettings.skybox?.name}\n";

        File.WriteAllText(Path.Combine(exportPath, "SnapshotInfo.txt"), info);
    }
}
