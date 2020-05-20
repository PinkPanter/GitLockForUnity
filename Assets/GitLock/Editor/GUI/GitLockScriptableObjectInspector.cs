using UnityEditor;
using UnityEngine;

namespace GitLockPackage.Editor
{
    [CustomEditor(typeof(ScriptableObject), true)]
    [CanEditMultipleObjects]
    public class GitLockScriptableObjectInspector : UnityEditor.Editor
    {
        protected override void OnHeaderGUI()
        {
            base.OnHeaderGUI();
            GitLockGUI.OnInspectorGUI(target);
        }
    }
}