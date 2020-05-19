using GitLockPackage.Editor;
using UnityEditor;
using UnityEngine;

public class GitLockSettingsEditor : EditorWindow
{
    private Vector2 scrollPoss;
    private static GUIStyle boldLabel;
    private static GUIStyle tableElementStyle;
    private static GUIStyle unlockButtonStyle;

    [MenuItem("Tools/GitLock Settings")]

    public static void ShowWindow()
    {
        var window = EditorWindow.GetWindow(typeof(GitLockSettingsEditor));
        window.minSize = new Vector2(90 + 128, 32);
    }

    void OnGUI()
    {
        CheckStyles();
        var basicColor = GUI.backgroundColor;

        var oldEnable = GitLockManager.IsEnabled;
        GUI.backgroundColor = oldEnable ? Color.green : Color.red;
        if (GUILayout.Button(oldEnable ? "Enable" : "Disable"))
            GitLockManager.IsEnabled = !oldEnable;

        GUI.backgroundColor = basicColor;

        EditorGUILayout.BeginHorizontal(GUILayout.MaxHeight(16));

        EditorGUILayout.LabelField("Git Username", GUILayout.MaxWidth(90));

        var oldUsername = GitLockManager.Username;
        var newUserName = EditorGUILayout.TextField(oldUsername);
        if (!string.Equals(oldUsername, newUserName))
            GitLockManager.Username = newUserName;

        if (GUILayout.Button(GitLockManager.CollectingUsername ? "Finding" : "Find username", GUILayout.MaxWidth(128)))
        {
            if(!GitLockManager.CollectingUsername)
                GitLockManager.GetGitUserName();
        }

        EditorGUILayout.EndHorizontal();
        //EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal(GUILayout.MaxHeight(32));
        DrawUILine(Color.gray);
        EditorGUILayout.EndHorizontal();

        int index = 0;
        if (GitLockManager.IsEnabled)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.MaxHeight(32));

            EditorGUILayout.LabelField("Locked files" + (GitLockManager.IsRenewingLocks ? "(Collecting locks...)" : string.Empty), boldLabel, GUILayout.ExpandWidth(true));

            EditorGUILayout.EndHorizontal();

            scrollPoss = EditorGUILayout.BeginScrollView(scrollPoss);
            var pathToRemove = Application.dataPath.Replace("/", "\\").Replace("\\Assets", string.Empty) + "\\";
            foreach (var file in GitLockManager.LockedFiles)
            {
                var normalizedPath = file.path.Replace(pathToRemove, string.Empty);
                tableElementStyle.normal.background = index % 2 == 0 ? Texture2D.grayTexture : Texture2D.whiteTexture;
                EditorGUILayout.BeginHorizontal(tableElementStyle, GUILayout.MaxHeight(50));

                GUILayout.Label(AssetDatabase.GetCachedIcon(normalizedPath),
                    GUILayout.MaxWidth(50), GUILayout.MaxHeight(50));

                EditorGUILayout.BeginVertical(tableElementStyle, GUILayout.MaxHeight(50));

                EditorGUILayout.BeginHorizontal(tableElementStyle, GUILayout.MaxHeight(50));
                EditorGUILayout.LabelField("Path:", GUILayout.MaxWidth(32));
                GUI.enabled = false;
                EditorGUILayout.TextField(normalizedPath);
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal(tableElementStyle, GUILayout.MaxHeight(50));
                EditorGUILayout.LabelField("By", GUILayout.MaxWidth(32));
                GUI.enabled = false;
                EditorGUILayout.TextField(file.owner.name, GUILayout.MaxWidth(128));
                GUI.enabled = true;
                EditorGUILayout.LabelField("at " + file.locked_at);

                bool isWorkingOnFile = GitLockManager.IsFileExecuting(file.path);
                GUI.backgroundColor = isWorkingOnFile ? Color.yellow : Color.red;

                if (GUILayout.Button(isWorkingOnFile ? "Unlocking..." : "Unlock forced", unlockButtonStyle, GUILayout.MaxWidth(128)))
                {
                    if (!isWorkingOnFile)
                        GitLockManager.Unlock(file.path, true);
                }

                GUI.backgroundColor = basicColor;

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
                index++;
            }

            EditorGUILayout.EndScrollView();
        }


        this.maxSize = new Vector2(800, 16 + 32 * 3 + index * 50);
    }

    private void CheckStyles()
    {
        if(boldLabel == null)
            boldLabel = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        if(tableElementStyle == null)
            tableElementStyle = new GUIStyle();
        if (unlockButtonStyle == null)
        {
            unlockButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }
    }

    public static void DrawUILine(Color color, int thickness = 2, int padding = 10)
    {
        Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
        r.height = thickness;
        r.y += padding / 2;
        r.x -= 2;
        r.width += 6;
        EditorGUI.DrawRect(r, color);
    }
}
