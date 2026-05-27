using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenNinja.EditorSetup
{
    /// <summary>
    /// Re-runnable Editor utility. Builds Assets/Scenes/StartScene.unity from
    /// scratch with title / best score / cube info table / nickname input /
    /// start button. Also builds Assets/Prefabs/CubeInfoRow.prefab and adds
    /// StartScene to Build Settings as index 0.
    /// </summary>
    public static class StartSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/StartScene.unity";
        private const string RowPrefabPath = "Assets/Prefabs/CubeInfoRow.prefab";
        private static readonly string[] MaterialNames =
            { "Wood", "Stone", "Metal", "Crystal", "Spiked", "Rubber" };

        public static string Execute()
        {
            var log = new List<string>();

            BuildRowPrefab();
            log.Add("row prefab built");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "StartScene";

            ConfigureCamera();
            log.Add("camera configured");

            var canvas = BuildCanvas();
            var title = BuildTitleLabel(canvas);
            var best  = BuildBestScoreLabel(canvas);
            var (table, rowContainer) = BuildInfoTable(canvas);
            var input = BuildNicknameInput(canvas);
            var button = BuildStartButton(canvas);
            EnsureEventSystem();
            log.Add("canvas built");

            WireStartScreen(canvas, title, best, table, rowContainer, input, button);
            log.Add("start screen wired");

            EditorSceneManager.SaveScene(scene, ScenePath);
            UpdateBuildSettings();
            log.Add($"scene saved + build settings updated → {ScenePath}");

            return string.Join(" | ", log);
        }

        // ---- Row prefab ----

        private static void BuildRowPrefab()
        {
            EnsureFolder("Assets/Prefabs");
            DeleteIfExists(RowPrefabPath);

            // RectTransform root with HorizontalLayoutGroup.
            var root = new GameObject("CubeInfoRow",
                typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(920, 120);
            var hlg = root.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16;
            hlg.padding = new RectOffset(24, 24, 16, 16);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            // Optional faint background.
            var bg = root.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.06f);

            // Icon.
            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(root.transform, false);
            var iconRT = (RectTransform)iconGO.transform;
            iconRT.sizeDelta = new Vector2(80, 80);
            var iconImg = iconGO.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.color = Color.white;

            // Name.
            var nameGO = NewTmpChild(root.transform, "Name", "Material", 40,
                preferredWidth: 320, alignment: TextAlignmentOptions.Left);

            // Points.
            var pointsGO = NewTmpChild(root.transform, "Points", "+1", 40,
                preferredWidth: 200, alignment: TextAlignmentOptions.Right);

            // Role badge: small Image with a child TMP.
            var badge = new GameObject("RoleBadge",
                typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            badge.transform.SetParent(root.transform, false);
            var badgeRT = (RectTransform)badge.transform;
            badgeRT.sizeDelta = new Vector2(160, 56);
            var badgeLE = badge.GetComponent<LayoutElement>();
            badgeLE.minWidth = 160; badgeLE.minHeight = 56;
            var badgeBg = badge.GetComponent<Image>();
            badgeBg.color = new Color(0.26f, 0.63f, 0.28f, 1f);

            var badgeText = NewTmpChild(badge.transform, "Text", "NORMAL", 28,
                preferredWidth: 0, alignment: TextAlignmentOptions.Center);
            var badgeTextRT = (RectTransform)badgeText.transform;
            badgeTextRT.anchorMin = Vector2.zero;
            badgeTextRT.anchorMax = Vector2.one;
            badgeTextRT.offsetMin = Vector2.zero;
            badgeTextRT.offsetMax = Vector2.zero;
            var badgeTmp = badgeText.GetComponent<TMP_Text>();
            badgeTmp.color = Color.white;
            badgeTmp.fontStyle = FontStyles.Bold;

            // Attach the row script and wire serialized references.
            var rowScript = root.AddComponent<CubeInfoRow>();
            var so = new SerializedObject(rowScript);
            so.FindProperty("icon").objectReferenceValue = iconImg;
            so.FindProperty("nameLabel").objectReferenceValue = nameGO.GetComponent<TMP_Text>();
            so.FindProperty("pointsLabel").objectReferenceValue = pointsGO.GetComponent<TMP_Text>();
            so.FindProperty("roleBadge").objectReferenceValue = badgeText.GetComponent<TMP_Text>();
            so.FindProperty("roleBadgeBackground").objectReferenceValue = badgeBg;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
            Object.DestroyImmediate(root);
        }

        // ---- Scene pieces ----

        private static void ConfigureCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.12f, 1f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
        }

        private static GameObject BuildCanvas()
        {
            var canvasGO = new GameObject("StartCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            return canvasGO;
        }

        private static GameObject BuildTitleLabel(GameObject canvas)
        {
            var go = NewTmpChild(canvas.transform, "Title", "MATERIAL NINJA", 120,
                preferredWidth: 1000, alignment: TextAlignmentOptions.Center);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -120);
            rt.sizeDelta = new Vector2(1000, 160);
            var tmp = go.GetComponent<TMP_Text>();
            tmp.color = new Color(1f, 0.9f, 0.2f, 1f);
            tmp.fontStyle = FontStyles.Bold;
            return go;
        }

        private static GameObject BuildBestScoreLabel(GameObject canvas)
        {
            var go = NewTmpChild(canvas.transform, "BestScore", "Best: 0", 48,
                preferredWidth: 800, alignment: TextAlignmentOptions.Center);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -300);
            rt.sizeDelta = new Vector2(800, 70);
            var tmp = go.GetComponent<TMP_Text>();
            tmp.color = new Color(1f, 1f, 1f, 0.75f);
            return go;
        }

        private static (GameObject table, RectTransform rowContainer) BuildInfoTable(GameObject canvas)
        {
            var table = new GameObject("CubeInfoTable", typeof(RectTransform));
            table.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)table.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -400);
            rt.sizeDelta = new Vector2(920, 800);

            var container = new GameObject("RowContainer",
                typeof(RectTransform), typeof(VerticalLayoutGroup));
            container.transform.SetParent(table.transform, false);
            var crt = (RectTransform)container.transform;
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            var vlg = container.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;

            return (table, crt);
        }

        private static GameObject BuildNicknameInput(GameObject canvas)
        {
            // Container holding the label and the input.
            var wrap = new GameObject("NicknameWrap", typeof(RectTransform));
            wrap.transform.SetParent(canvas.transform, false);
            var wrt = (RectTransform)wrap.transform;
            wrt.anchorMin = new Vector2(0.5f, 1f);
            wrt.anchorMax = new Vector2(0.5f, 1f);
            wrt.pivot = new Vector2(0.5f, 1f);
            wrt.anchoredPosition = new Vector2(0, -1340);
            wrt.sizeDelta = new Vector2(700, 200);

            // Label.
            var label = NewTmpChild(wrap.transform, "Label", "NICKNAME", 36,
                preferredWidth: 700, alignment: TextAlignmentOptions.Center);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = new Vector2(0.5f, 1f);
            lrt.anchorMax = new Vector2(0.5f, 1f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.anchoredPosition = new Vector2(0, 0);
            lrt.sizeDelta = new Vector2(700, 50);

            // Input.
            var inputGO = new GameObject("NicknameInput",
                typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            inputGO.transform.SetParent(wrap.transform, false);
            var irt = (RectTransform)inputGO.transform;
            irt.anchorMin = new Vector2(0.5f, 1f);
            irt.anchorMax = new Vector2(0.5f, 1f);
            irt.pivot = new Vector2(0.5f, 1f);
            irt.anchoredPosition = new Vector2(0, -70);
            irt.sizeDelta = new Vector2(600, 100);
            var inputBg = inputGO.GetComponent<Image>();
            inputBg.color = new Color(1f, 1f, 1f, 0.1f);

            // Text Area + child Text required by TMP_InputField.
            var textArea = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(inputGO.transform, false);
            var tart = (RectTransform)textArea.transform;
            tart.anchorMin = Vector2.zero;
            tart.anchorMax = Vector2.one;
            tart.offsetMin = new Vector2(20, 0);
            tart.offsetMax = new Vector2(-20, 0);

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(textArea.transform, false);
            var trt = (RectTransform)textGO.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var text = textGO.GetComponent<TextMeshProUGUI>();
            text.fontSize = 48;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;

            var placeholderGO = new GameObject("Placeholder",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            placeholderGO.transform.SetParent(textArea.transform, false);
            var prt = (RectTransform)placeholderGO.transform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            var placeholder = placeholderGO.GetComponent<TextMeshProUGUI>();
            placeholder.text = "Type a nickname...";
            placeholder.fontSize = 48;
            placeholder.color = new Color(1f, 1f, 1f, 0.4f);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.fontStyle = FontStyles.Italic;

            var inputField = inputGO.GetComponent<TMP_InputField>();
            inputField.textViewport = (RectTransform)textArea.transform;
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.characterLimit = 16;
            inputField.contentType = TMP_InputField.ContentType.Standard;

            return inputGO;
        }

        private static GameObject BuildStartButton(GameObject canvas)
        {
            var btn = new GameObject("StartButton",
                typeof(RectTransform), typeof(Image), typeof(Button));
            btn.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -1700);
            rt.sizeDelta = new Vector2(400, 120);
            var img = btn.GetComponent<Image>();
            img.color = new Color(0.13f, 0.59f, 0.95f, 1f);

            var label = NewTmpChild(btn.transform, "Label", "START", 56,
                preferredWidth: 400, alignment: TextAlignmentOptions.Center);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var ltmp = label.GetComponent<TMP_Text>();
            ltmp.color = Color.white;
            ltmp.fontStyle = FontStyles.Bold;

            return btn;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            _ = es;
        }

        // ---- Wiring ----

        private static void WireStartScreen(GameObject canvas, GameObject titleGO, GameObject bestGO,
            GameObject tableGO, RectTransform rowContainer, GameObject inputGO, GameObject buttonGO)
        {
            var screen = canvas.AddComponent<StartScreen>();
            var rowPrefab = AssetDatabase.LoadAssetAtPath<CubeInfoRow>(RowPrefabPath);

            var materials = new CubeMaterial[MaterialNames.Length];
            for (int i = 0; i < MaterialNames.Length; i++)
            {
                materials[i] = AssetDatabase.LoadAssetAtPath<CubeMaterial>(
                    $"Assets/Data/CubeMaterials/{MaterialNames[i]}.asset");
            }

            var tableComp = tableGO.AddComponent<CubeInfoTable>();
            var tso = new SerializedObject(tableComp);
            tso.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
            tso.FindProperty("rowContainer").objectReferenceValue = rowContainer;
            var matsProp = tso.FindProperty("materials");
            matsProp.arraySize = materials.Length;
            for (int i = 0; i < materials.Length; i++)
                matsProp.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
            tso.ApplyModifiedPropertiesWithoutUndo();

            var sso = new SerializedObject(screen);
            sso.FindProperty("titleLabel").objectReferenceValue = titleGO.GetComponent<TMP_Text>();
            sso.FindProperty("bestScoreLabel").objectReferenceValue = bestGO.GetComponent<TMP_Text>();
            sso.FindProperty("nicknameInput").objectReferenceValue = inputGO.GetComponent<TMP_InputField>();
            sso.FindProperty("startButton").objectReferenceValue = buttonGO.GetComponent<Button>();
            sso.FindProperty("infoTable").objectReferenceValue = tableComp;
            sso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpdateBuildSettings()
        {
            const string mainScenePath = "Assets/Scenes/MainScene.unity";
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));

            // Ensure MainScene is present (at any non-zero index).
            if (!scenes.Exists(s => s.path == mainScenePath))
                scenes.Add(new EditorBuildSettingsScene(mainScenePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---- Helpers ----

        private static GameObject NewTmpChild(Transform parent, string name, string text,
            float fontSize, float preferredWidth, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            if (preferredWidth > 0)
            {
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = preferredWidth;
                le.preferredHeight = fontSize * 1.4f;
            }
            return go;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(assetPath);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void DeleteIfExists(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);
        }
    }
}
