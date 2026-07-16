using System.Collections.Generic;
using ColorBlocks.Core;
using UnityEditor;
using UnityEngine;

namespace ColorBlocks.Editor
{
    [CustomEditor(typeof(LevelDefinition))]
    public sealed class LevelDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(8f);

            LevelDefinition level = (LevelDefinition)target;
            List<string> errors = LevelValidator.Validate(level);
            if (errors.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    $"Valid level: {level.Cells.Count} occupied cells, {CountBlocks(level)} total blocks.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Error);
            }

            if (GUILayout.Button("Rebuild All Prototype Levels"))
            {
                ProjectSetup.RebuildPrototypeContent();
            }
        }

        private static int CountBlocks(LevelDefinition level)
        {
            int count = 0;
            for (int i = 0; i < level.Cells.Count; i++) count += level.Cells[i].Layers.Count;
            return count;
        }
    }
}
