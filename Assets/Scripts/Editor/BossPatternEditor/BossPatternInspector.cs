using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Chopper.EditorTools
{
    /// <summary>
    /// 패턴 에셋(BossPattern)의 인스펙터. 맨 위에 패턴 에디터로 여는 버튼을 두고, 그 아래에 모든 값을 그대로 보여 준다.
    /// Project 창에서 패턴 에셋을 더블클릭해도 패턴 에디터가 열린다.
    /// </summary>
    [CustomEditor(typeof(BossPattern))]
    public sealed class BossPatternInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button(new GUIContent("패턴 에디터에서 열기", "미리보기와 타임라인으로 이 패턴을 편집한다"), GUILayout.Height(28f)))
            {
                BossPatternEditorWindow.OpenPattern((BossPattern)target);
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.HelpBox("값을 여기서 직접 고쳐도 됩니다. 눈으로 확인하면서 편집하려면 패턴 에디터를 쓰세요.\n" +
                                    "(메뉴: Tools > Chopper > Boss Pattern Editor)", MessageType.None);
            EditorGUILayout.Space();

            DrawDefaultInspector();
        }

        [OnOpenAsset]
        private static bool OnOpenAsset(EntityId entityId, int line)
        {
            var pattern = EditorUtility.EntityIdToObject(entityId) as BossPattern;
            if (pattern == null) return false;

            BossPatternEditorWindow.OpenPattern(pattern);
            return true;
        }
    }
}
