using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GitLockPackage.Editor
{
    [CustomEditor(typeof(GameObject), true)]
    [CanEditMultipleObjects]
    public class GitLockGameObjectInspector : UnityEditor.Editor
    {
        UnityEditor.Editor defaultEditor;

        MethodInfo inspectorMethod;

        void OnEnable()
        {
            EditorApplication.update += Update;

            var goInspectorType = Type.GetType("UnityEditor.GameObjectInspector, UnityEditor");

            defaultEditor = CreateEditor(targets, goInspectorType);

            MethodInfo enableMethod = goInspectorType.GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            enableMethod?.Invoke(defaultEditor, null);

            inspectorMethod = goInspectorType.GetMethod("OnHeaderGUI", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.NonPublic);
        }

        void OnDisable()
        {
            EditorApplication.update -= Update;
        }


        private void Update()
        {
            if (GitLockManager.IsRenewingLocks || !GitLockManager.IsInitialized || GitLockManager.IsFileExecuting(GitLockGUI.GetFilePath(target)))
                Repaint();
        }

        protected override void OnHeaderGUI()
        {
            inspectorMethod.Invoke(defaultEditor, new object[0]{});
        }

        public override void OnInspectorGUI()
        {
            GitLockGUI.OnInspectorGUI(target);
        }
    }
}