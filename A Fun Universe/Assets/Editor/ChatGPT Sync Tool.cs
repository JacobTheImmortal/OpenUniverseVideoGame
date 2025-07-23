#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;

public class ChatGPTSyncTool : EditorWindow
{
    private const string kDefaultPath = "ChatGPTSync";
    private string exportPath = kDefaultPath;
    private static readonly StringBuilder logBuffer = new();

    [MenuItem("Tools/ChatGPT Sync Tool")]
    public static void ShowWindow()
    {
        GetWindow<ChatGPTSyncTool>("ChatGPT Sync");
    }

    private void OnEnable() => Application.logMessageReceived += OnLog;
    private void OnDisable() => Application.logMessageReceived -= OnLog;

    private void OnGUI()
    {
        GUILayout.Label("Export Project Snapshot for ChatGPT", EditorStyles.boldLabel);
        exportPath = EditorGUILayout.TextField("Export Folder", exportPath);

        if (GUILayout.Button("Capture Snapshot"))
            CaptureEverything();
    }

    #region Capture helpers
    private void CaptureEverything()
    {
        Directory.CreateDirectory(exportPath);
        CaptureSceneView();
        CaptureGameView();
        ExportDebugInfo();
        Debug.Log($"ChatGPT snapshot exported to: <b>{Path.GetFullPath(exportPath)}</b>");
        AssetDatabase.Refresh();
    }

    private void CaptureSceneView()
    {
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null) { Debug.LogWarning("No active SceneView"); return; }

        var rt = new RenderTexture(1920, 1080, 24);
        var cam = sceneView.camera;
        cam.targetTexture = rt;
        cam.Render();

        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        File.WriteAllBytes(Path.Combine(exportPath, "SceneView.png"), tex.EncodeToPNG());

        RenderTexture.active = null;
        cam.targetTexture = null;
        DestroyImmediate(rt);
        DestroyImmediate(tex);
    }

    private void CaptureGameView()
        => ScreenCapture.CaptureScreenshot(Path.Combine(exportPath, "GameView.png"));

    private void ExportDebugInfo()
    {
        Camera mainCam = Camera.main;
        GameObject player = GameObject.FindWithTag("Player");

        string info = $"Time: {System.DateTime.Now}\n" +
                      $"MainCam Position: {mainCam?.transform.position}\n" +
                      $"MainCam Rotation: {mainCam?.transform.rotation.eulerAngles}\n" +
                      $"Player Position: {player?.transform.position}\n" +
                      $"Skybox: {RenderSettings.skybox?.name}\n\n" +
                      $"---- Console Log (this session) ----\n" +
                      logBuffer;

        File.WriteAllText(Path.Combine(exportPath, "SnapshotInfo.txt"), info);
        logBuffer.Clear(); // reset for next run
    }

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        logBuffer.AppendLine($"[{type}] {condition}");
        if (type == LogType.Exception || type == LogType.Error)
            logBuffer.AppendLine(stackTrace);
    }
    #endregion
}
#endif
