using TMPro;
using UnityEditor;
using UnityEngine;

namespace OpenNinja.EditorSetup
{
    public static class ComboPopupSetup
    {
        public static string Execute()
        {
            // Build a UI element with a RectTransform root + TMP_Text child.
            var root = new GameObject("ComboPopup", typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(200, 60);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(root.transform, false);
            var labelRT = (RectTransform)labelGO.transform;
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "+1";
            tmp.fontSize = 48f;
            tmp.alignment = TextAlignmentOptions.Center;
            // Notebook ink amber — distinctly readable on the cream backdrop.
            // The previous near-pure-yellow disappeared against the paper.
            tmp.color = LabNotebookTheme.InkAmber;
            tmp.fontStyle = FontStyles.Bold;
            // Dark outline so the text stays legible if a cube ever lands behind it.
            tmp.outlineColor = LabNotebookTheme.InkDark;
            tmp.outlineWidth = 0.2f;

            var popup = root.AddComponent<OpenNinja.ComboPopup>();

            // Assign the label via serialized property so it round-trips into the prefab.
            var so = new SerializedObject(popup);
            var labelProp = so.FindProperty("label");
            if (labelProp != null)
            {
                labelProp.objectReferenceValue = tmp;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Object.DestroyImmediate(root);
                return "ERROR: ComboPopup.label SerializedProperty not found";
            }

            const string prefabPath = "Assets/Prefabs/ComboPopup.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);
            if (prefab == null)
            {
                Object.DestroyImmediate(root);
                return "ERROR: failed to save ComboPopup prefab";
            }

            Object.DestroyImmediate(root);
            return $"ComboPopup saved to {prefabPath}";
        }
    }
}
