using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GitLockPackage.Editor
{
    public static class GitLockGUI
    {
        #region Consts

        private const int AnimationSpeed = 20;

        private const string ResourcesIconsPath = "\\Editor Resources\\Icons\\";
        private const string ResourcesLoadingPath = "\\Editor Resources\\Loading\\";
        private const string lockedIconName = "Locked.png";
        private const string lockedByYouIconName = "LockedByYou.png";
        private const string unlockedIconName = "Unlocked.png";
        private const string unlockedWithChangesIconName = "UnlockedWithChanges.png";
        private const string updatedIconName = "Updated.png";
        
        #endregion

        private static Texture lockedIcon;
        private static Texture lockedByYouIcon;
        private static Texture unlockIcon; 
        private static Texture unlockWithChangesIcon;
        private static Texture updatedIcon;
        private static Texture[] loadingImages;
        private static int currentFrame;


        static GitLockGUI()
        {
            var path = GetPackageRelativePath();
            path = path.Replace(Application.dataPath.Replace("/", "\\"), string.Empty);

            lockedIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(path + ResourcesIconsPath, lockedIconName));
            lockedByYouIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(path + ResourcesIconsPath, lockedByYouIconName));
            unlockIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(path + ResourcesIconsPath, unlockedIconName));
            unlockWithChangesIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(path + ResourcesIconsPath, unlockedWithChangesIconName));
            updatedIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(path + ResourcesIconsPath, updatedIconName));

            var imageCount = Directory.GetFiles(path + ResourcesLoadingPath).Length / 2;
            loadingImages = new Texture[imageCount];
            for (int i = 0; i < imageCount; i++)
            {
                loadingImages[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(path + ResourcesLoadingPath, $"{i}.png"));
            }
        }

        public static void OnInspectorGUI(Object target)
        {
            if(!GitLockManager.IsEnabled)
                return;

            var path = GetFilePath(target);
            if (path != null)
            {
                DrawGUI(target, path);
            }
        }

        public static string GetFilePath(Object target)
        {
            string pathToReturn = null;

            var tempPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target);
            if (tempPath.EndsWith(".prefab"))
            {
                pathToReturn = tempPath;
            }

            tempPath = AssetDatabase.GetAssetPath(target);
            if (tempPath.EndsWith(".unity") ||
                (tempPath.EndsWith(".asset") && target is ScriptableObject))
            {
                pathToReturn = tempPath;
            }

            if (pathToReturn != null)
            {
                pathToReturn = pathToReturn.Replace("Assets/", string.Empty);
                pathToReturn = Path.Combine(Application.dataPath, pathToReturn).Replace("/", "\\");
            }

            return pathToReturn;
        }

        private static void DrawGUI(Object target, string path)
        {
            if (currentFrame >= loadingImages.Length * AnimationSpeed)
                currentFrame = 0;


            Rect space = EditorGUILayout.BeginHorizontal(GUILayout.MaxHeight(32));

            DrawLockGUI(path);
            DrawUpdateGUI();

            EditorGUILayout.EndHorizontal();

            currentFrame++;
        }

        private static void DrawLockGUI(string path)
        {
            string text = string.Empty;
            var lockedFile = GitLockManager.IsLock(path, out var locked);
            if (!GitLockManager.IsInitialized)
            {
                GUILayout.Button(loadingImages[currentFrame / AnimationSpeed], GUILayout.MaxHeight(32), GUILayout.MaxWidth(32));
                text = "Initializing";
            }
            else if (GitLockManager.IsFileExecuting(path))
            {
                GUILayout.Button(loadingImages[currentFrame / AnimationSpeed], GUILayout.MaxHeight(32), GUILayout.MaxWidth(32));
                text = "Executing command";
            }
            else
            {
                if (locked == GitLockManager.LockState.Locked)
                {
                    GUILayout.Button(lockedIcon, GUILayout.MaxHeight(32), GUILayout.MaxWidth(32));
                    text = $"Locked by {lockedFile.owner.name} at {lockedFile.locked_at}";
                }
                else if (locked == GitLockManager.LockState.LockedByYou)
                {
                    if(GUILayout.Button(lockedByYouIcon, GUILayout.MaxHeight(32), GUILayout.MaxWidth(32)))
                        GitLockManager.Unlock(path);
                    text = $"Locked by {lockedFile.owner.name}(You) at {lockedFile.locked_at}";
                }
                else
                {
                    if (GUILayout.Button(unlockIcon, GUILayout.MaxHeight(32), GUILayout.MaxWidth(32)))
                        GitLockManager.Lock(path);
                    text = $"Unlocked";
                }
            }
            GUILayout.Label(text, GUILayout.MaxHeight(32));
        }

        private static void DrawUpdateGUI()
        {
            GUILayout.FlexibleSpace();
            bool isUpdating = GitLockManager.IsRenewingLocks;
            if (isUpdating)
            {
                GUILayout.Button(loadingImages[currentFrame / AnimationSpeed], GUILayout.MaxHeight(32), GUILayout.MaxWidth(32));
            }
            else if (GUILayout.Button(updatedIcon, GUILayout.MaxHeight(32), GUILayout.MaxWidth(32)))
                GitLockManager.ForceUpdateLocks();
        }

        private static string GetPackageRelativePath()
        {
            // Check for potential UPM package
            string packagePath = Path.GetFullPath("Packages/com.PinkPanter.GitLock");
            if (Directory.Exists(packagePath))
            {
                return "Packages/com.PinkPanter.GitLock";
            }


            packagePath = Path.GetFullPath("Assets/..");
            if (Directory.Exists(packagePath))
            {
                // Search default location for development package
                if (Directory.Exists(packagePath + "/Assets/GitLock/Editor Resources"))
                {
                    return "Assets/GitLock";
                }

                // Search for potential alternative locations in the user project
                string[] matchingPaths = Directory.GetDirectories(packagePath, "GitLock", SearchOption.AllDirectories);
                packagePath = ValidateLocation(matchingPaths, packagePath);
                if (packagePath != null)
                    return packagePath;
            }

            return null;
        }

        private static string ValidateLocation(string[] matchingPaths, string packagePath)
        {
            foreach (var matchingPath in matchingPaths)
            {
                if (File.Exists(Path.Combine(matchingPath, "Editor Resources/Icons/Locked.png")))
                    return matchingPath;
            }

            return null;
        }
    }
}
