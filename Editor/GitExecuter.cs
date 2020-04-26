using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace GitLockPackage.Editor
{
    [InitializeOnLoad]
    public static class GitExecuter
    {
        public static bool IsExecuting => isWorkingOnTask;

        private static bool isWorkingOnTask;

        private static Process currentProcess;

        private static Queue<Task> tasksQueue = new Queue<Task>();

        static GitExecuter()
        {
            EditorApplication.update += Update;
            EditorApplication.quitting += OnScriptsReloaded;
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            currentProcess?.Close();
        }

        public static Task<string> RunGitCommand(string path, string gitCommand)
        {
            Task<string> currentTask = new Task<string>(() => GitCommandExecution($"-C {path} {gitCommand}"));

            if(tasksQueue.Count == 0)
                currentTask.Start();
            else
                tasksQueue.Enqueue(currentTask);

            return currentTask;
        }

        public static Task<Tuple<string[], string[]>> RunManyPathGitCommand(string[] paths, string gitCommand)
        {
            Task<Tuple<string[], string[]>> currentTask = new Task<Tuple<string[], string[]>>(() =>
            {
                string[] outpups = new string[paths.Length];
                for (int i = 0; i < paths.Length; i++)
                {
                    outpups[i] = GitCommandExecution($"-C {paths[i]} {gitCommand}");
                }
                return new Tuple<string[], string[]>(paths, outpups);
            });

            if (tasksQueue.Count == 0)
                currentTask.Start();
            else
                tasksQueue.Enqueue(currentTask);

            return currentTask;
        }

        public static void StopCommandExecution(Task command)
        {
            if (tasksQueue.Contains(command))
                tasksQueue = new Queue<Task>(tasksQueue.Where(p => p != command));
        }

        private static void Update()
        {
            if (!isWorkingOnTask && tasksQueue.Count != 0)
            {
                tasksQueue.Dequeue().Start();
            }
        }

        private static string GitCommandExecution(string command)
        {
            isWorkingOnTask = true;

            // Strings that will catch the output from our process.
            string output = "no-git";
            string errorOutput = "no-git";

            //Debug.Log(command);
            // Set up our processInfo to run the git command and log to output and errorOutput.
            ProcessStartInfo processInfo = new ProcessStartInfo("git", command)
            {
                CreateNoWindow = true,          // We want no visible pop-ups
                UseShellExecute = false,        // Allows us to redirect input, output and error streams
                RedirectStandardOutput = true,  // Allows us to read the output stream
                RedirectStandardError = true    // Allows us to read the error stream
            };

            // Set up the Process
            currentProcess = new Process
            {
                StartInfo = processInfo
            };

            try
            {
                currentProcess.Start();  // Try to start it, catching any exceptions if it fails
            }
            catch (Exception e)
            {
                isWorkingOnTask = false;
                // For now just assume its failed cause it can't find git.
                Debug.LogError("Git is not set-up correctly, required to be on PATH, and to be a git project.");
                throw e;
            }

            // Read the results back from the process so we can get the output and check for errors
            output = currentProcess.StandardOutput.ReadToEnd();
            errorOutput = currentProcess.StandardError.ReadToEnd();

            currentProcess.WaitForExit();  // Make sure we wait till the process has fully finished.
            currentProcess.Close();        // Close the process ensuring it frees it resources.

            isWorkingOnTask = false;
            // Check for failure due to no git setup in the project itself or other fatal errors from git.
            if (output.Contains("fatal") || output == "no-git")
            {
                Debug.LogError($"Command: git {command} Failed\n {errorOutput}");
                throw new Exception($"Command: git {command} Failed\n {errorOutput}");
            }
            // Log any errors.
            if (errorOutput != "")
            {
                Debug.LogError($"Git Error: Command: git {command} - {errorOutput}");
                throw new Exception($"Command: git {command} Failed\n {errorOutput}");

            }

            return output;  // Return the output from git.
        }
    }
}