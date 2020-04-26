using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GitLockPackage.Editor
{
    [CustomEditor(typeof(SceneAsset), true)]
    [CanEditMultipleObjects]
    public class GitLockSceneInspector : UnityEditor.Editor
    {
        private void OnEnable()
        {
            EditorApplication.update += Update;
        }
        private void OnDisable()
        {
            EditorApplication.update -= Update;
        }

        private void Update()
        {
            if (GitLockManager.IsRenewingLocks || !GitLockManager.IsInitialized || GitLockManager.IsFileExecuting(GitLockGUI.GetFilePath(target)))
                Repaint();
        }

        public override void OnInspectorGUI()
        {
            GUI.enabled = true;
            GitLockGUI.OnInspectorGUI(target);
            base.OnInspectorGUI();
        }

    }
}
