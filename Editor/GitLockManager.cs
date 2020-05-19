using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace GitLockPackage.Editor
{
    [InitializeOnLoad]
    public static class GitLockManager
    {
        #region Consts

        private const string gitUpdateLocksCommand = "lfs locks --json";
        private const string gitCollectSubmodulesCommand = "config --file .gitmodules --get-regexp path";
        private const string gitLockCommand = "lfs lock";
        private const string gitUnlockCommand = "lfs unlock";

        private const string gitLockRenewTimeKey = "GitLockRenewTime";
        private const string lastUpdateGitLocksTimeKey = "LastUpdateGitLocksTime";
        private const string lastLocksKey = "LastLocks";
        private const string gitUserNameKey = "GitUserName";
        private const string isEnabledKey = "IsEnabled";

        #endregion

        public static bool IsRenewingLocks => updateLocksCommand != null;

        public static bool IsInitialized => lockedFiles != null;

        public static bool CollectingUsername => isCollectingName;

        public static IEnumerable<LockFile> LockedFiles => lockedFiles;

        public static string Username
        {
            get => userName;
            set
            {
                userName = value;
                EditorPrefs.SetString(gitUserNameKey, userName);
            }
        }
        public static bool IsEnabled
        {
            get => isEnabled;
            set
            {
                isEnabled = value;
                EditorPrefs.SetBool(isEnabledKey, isEnabled);
            }
        }

        private static LockFile[] lockedFiles;
        private static int renewTime = 60;
        private static DateTime lastRenewTime;
        
        private static Task updateLocksCommand;
        private static bool saveLocks = false;

        private static string userName;
        private static bool isCollectingName;

        private static bool isEnabled;

        private static bool saveName = false;
        private static string[] gitRepos;
        private static Dictionary<string, Task<string>> fileTasks = new Dictionary<string, Task<string>>();

        static GitLockManager()
        {
            renewTime = EditorPrefs.GetInt(gitLockRenewTimeKey, 60);
            userName = EditorPrefs.GetString(gitUserNameKey, string.Empty);
            isEnabled = EditorPrefs.GetBool(isEnabledKey, true);

            string lockedFileCache = EditorPrefs.GetString(lastLocksKey, string.Empty);
            if (!string.IsNullOrEmpty(lockedFileCache))
                lockedFiles = JsonUtility.FromJson<LockFiles>(lockedFileCache).files;

            if (!long.TryParse(EditorPrefs.GetString(lastUpdateGitLocksTimeKey, DateTime.UtcNow.AddSeconds(renewTime).Ticks.ToString()),
                out var lastRenewTimeTicks))
                lastRenewTimeTicks = DateTime.UtcNow.AddSeconds(-renewTime).Ticks;

            lastRenewTime = new DateTime(lastRenewTimeTicks);
            CollectRepo(DetectGitRoot());
        }

        private static void Update()
        {
            if (saveLocks)
            {
                EditorPrefs.SetString(lastLocksKey, JsonUtility.ToJson(new LockFiles()
                {
                    files = lockedFiles
                }));
                saveLocks = false;

            }
            if (saveName)
            {
                EditorPrefs.SetString(gitUserNameKey, userName);
                saveName = false;
            }

            TryToUpdateLocks();
        }

        #region PublicMethods

        public static void Lock(string path)
        {
            if(lockedFiles.Any(f=>string.Equals(f.path, path)))
                return;

            var sorterRepos = gitRepos.OrderByDescending(r => r.Length);
            foreach (var repo in sorterRepos)
            {
                if (path.Contains(repo))
                {
                    fileTasks[path] = GitExecuter.RunGitCommand(repo, $"{gitLockCommand} {path.Replace(repo, string.Empty)}");
                    fileTasks[path].ContinueWith((task) => EndFileTask(path, task));
                    return;
                }
            }
        }

        public static void Unlock(string path, bool force = false)
        {
            var lockedFile = lockedFiles.FirstOrDefault(f => string.Equals(f.path, path));
            if (!string.IsNullOrEmpty(lockedFile.path))
            {
                if(!force && lockedFile.owner.name != userName)
                    return;
            }
            else
            {
                return;
            }

            var sorterRepos = gitRepos.OrderByDescending(r => r.Length);
            foreach (var repo in sorterRepos)
            {
                if (path.Contains(repo))
                {
                    fileTasks[path] = GitExecuter.RunGitCommand(repo, $"{gitUnlockCommand} {path.Replace(repo, string.Empty)} {(force ? "--force" : string.Empty)}");
                    fileTasks[path].ContinueWith((task) => EndFileTask(path, task));
                    return;
                }
            }
        }

        public static LockFile IsLock(string path, out LockState lockState)
        {
            if (lockedFiles == null)
            {
                lockState = LockState.Unlocked;
                return default;
            }

            for (int i = 0; i < lockedFiles.Length; i++)
            {
                if (path == lockedFiles[i].path)
                {
                    lockState =string.Equals(lockedFiles[i].owner.name, userName) ? LockState.LockedByYou : LockState.Locked;
                    return lockedFiles[i];
                }
            }

            lockState = LockState.Unlocked;
            return default;
        }

        public static bool IsFileExecuting(string path)
        {
            if (path == null)
                return false;

            return fileTasks.ContainsKey(path);
        }

        public static bool TryToUpdateLocks()
        {
            if (!IsRenewingLocks && ((DateTime.UtcNow - lastRenewTime).TotalSeconds > renewTime || lockedFiles == null))
            {
                ForceUpdateLocks();
                return true;
            }

            return false;
        }

        public static void ForceUpdateLocks()
        {
            if(gitRepos == null)
                return;

            if (updateLocksCommand != null)
                GitExecuter.StopCommandExecution(updateLocksCommand);
            

            lastRenewTime = DateTime.UtcNow;
            EditorPrefs.SetString(lastUpdateGitLocksTimeKey, DateTime.UtcNow.Ticks.ToString());
            var nextTask = GitExecuter.RunManyPathGitCommand(gitRepos, gitUpdateLocksCommand);
            nextTask.ContinueWith(CollectLocks);

            updateLocksCommand = nextTask;
        }

        public static void GetGitUserName()
        {
            if (File.Exists(Path.Combine(gitRepos[0], ".gitattributes")))
            {
                isCollectingName = true;
                GitExecuter.RunGitCommand(gitRepos[0], $"{gitLockCommand} .gitattributes").ContinueWith(task =>
                {
                    GitExecuter.RunGitCommand(gitRepos[0], gitUpdateLocksCommand).ContinueWith(locksTask =>
                    {
                        var results = JsonUtility.FromJson<LockFiles>("{\"files\":" + locksTask.Result + "}").files;
                        var lockAttributes = results.FirstOrDefault(r => r.path == ".gitattributes");
                        if (!string.IsNullOrEmpty(lockAttributes.owner.name))
                        {
                            userName = lockAttributes.owner.name;
                            saveName = true;
                        }

                        if (string.IsNullOrEmpty(userName))
                            Debug.LogWarning("Git username wasn't initialized! Write it by yourself at launch another try to collect it from Tools/GitLock Settings");
                        isCollectingName = false;
                        GitExecuter.RunGitCommand(gitRepos[0], $"{gitUnlockCommand} .gitattributes --force");
                    });
                });

            }
        }

        #endregion

        #region PrivateMethods

        private static void CollectLocks(Task<Tuple<string[], string[]>> locksCommand)
        {
            updateLocksCommand = null;
            
            List<LockFile> locked = new List<LockFile>();

            for (int i = 0; i < locksCommand.Result.Item1.Length; i++)
            {
                var results = JsonUtility.FromJson<LockFiles>("{\"files\":" + locksCommand.Result.Item2[i] + "}").files;
                for (var index = 0; index < results.Length; index++)
                {
                    results[index].path = Path.Combine(locksCommand.Result.Item1[i], results[index].path.Replace("/", "\\"));
                }

                locked.AddRange(results);
            }

            lockedFiles = locked.ToArray();
            saveLocks = true;
        }

        private static string DetectGitRoot()
        {
            var path = Application.dataPath;
            while (!Directory.GetDirectories(path).Any(d=>d.Contains(".git")))
            {
                var next = Directory.GetParent(path);
                if(next == null)
                    throw new Exception("Project isn't a git repo!");

                path = next.FullName;
            }

            return path;
        }

        private static void CollectRepo(string rootPath)
        {
            GitExecuter.RunGitCommand(rootPath, gitCollectSubmodulesCommand).ContinueWith((t) => ParceRepos(rootPath, t));
        }

        private static void ParceRepos(string rootPath, Task<string> reposCommand)
        {
            var output = reposCommand.Result;
            if (string.IsNullOrEmpty(output))
            {
                gitRepos = new[] {rootPath};
                return;
            }

            output = output.Replace("/", "\\");
            List<string> additionalPaths = new List<string>();
            additionalPaths.Add(rootPath);

            string toBeSearched = "path";
            var indexOfNext = output.IndexOf(toBeSearched, StringComparison.Ordinal);
            output = output.Substring(indexOfNext + toBeSearched.Length);
            while (indexOfNext > 0)
            {
                int indexOfSubmodules = output.IndexOf("submodule", StringComparison.Ordinal);
                if (indexOfSubmodules < 0)
                {
                    additionalPaths.Add(Path.Combine(rootPath, output.Trim()));
                    break;
                }

                additionalPaths.Add(Path.Combine(rootPath, output.Remove(indexOfSubmodules - 1, output.Length - indexOfSubmodules).Trim()));
                indexOfNext = output.IndexOf(toBeSearched, StringComparison.Ordinal);
                output = output.Substring(indexOfNext + toBeSearched.Length);
            }


            gitRepos = additionalPaths.ToArray();
            EditorApplication.update += Update;
            
            if (string.IsNullOrEmpty(userName))
                GetGitUserName();
        }

        private static void EndFileTask(string path, Task<string> task)
        {
            if (fileTasks.ContainsKey(path))
                fileTasks.Remove(path);

            if (task.Result.Contains("Unlocked"))
                lockedFiles = lockedFiles.Where(f => !string.Equals(f.path, path)).ToArray();
            else if (task.Result.Contains("Locked"))
            {
                var tempLockedFiles = lockedFiles.ToList();
                tempLockedFiles.Add(new LockFile()
                {
                    locked_at = DateTime.Now.ToString("s"),
                    owner = new LockFile.Owner()
                    {
                        name = userName
                    },
                    path = path
                });
                lockedFiles = tempLockedFiles.ToArray();
            }
        }

        #endregion

        [Serializable]
        private struct LockFiles
        {
            public LockFile[] files;
        }

        [Serializable]
        public struct LockFile
        {
            public string path;

            public Owner owner;

            public string locked_at;

            [Serializable]
            public struct Owner
            {
                public string name;
            }
        }

        public enum LockState
        {
            Unlocked,
            LockedByYou,
            Locked,

        }
    }
}