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

        // Step 10: Caveat font cache
        private static TMP_FontAsset _cachedCaveat;

        public static string Execute()
        {
            // Step 1: Assets setup first
            StartScreenAssetsSetup.Execute();

            var log = new List<string>();

            BuildRowPrefab();
            log.Add("row prefab built");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "StartScene";

            ConfigureCamera();
            log.Add("camera configured");

            var canvas = BuildCanvas();

            // Step 4: Build background (graph paper + margin line)
            BuildBackground(canvas);

            // Step 5: Title, subtitle, best score
            var title      = BuildTitleLabel(canvas);
            _ = BuildSubtitle(canvas);
            var best       = BuildBestScoreLabel(canvas);

            // Step 6: Specimens divider
            BuildDivider(canvas, "specimens", new Vector2(0, -640));

            var (table, rowContainer) = BuildInfoTable(canvas);

            // Step 6: Subject divider
            BuildDivider(canvas, "subject", new Vector2(0, -1280));

            var input  = BuildNicknameInput(canvas);
            var button = BuildStartButton(canvas);

            // Step 9: "↗ tap here" arrow
            BuildTapHereArrow(canvas);

            EnsureEventSystem();
            log.Add("canvas built");

            WireStartScreen(canvas, title, best, table, rowContainer, input, button);
            log.Add("start screen wired");

            EditorSceneManager.SaveScene(scene, ScenePath);
            UpdateBuildSettings();
            log.Add($"scene saved + build settings updated → {ScenePath}");

            return string.Join(" | ", log);
        }

        // ---- Row prefab ---- (Step 2: notebook-styled row)

        private static void BuildRowPrefab()
        {
            EnsureFolder("Assets/Prefabs");
            DeleteIfExists(RowPrefabPath);

            var caveat = LoadCaveat();

            var root = new GameObject("CubeInfoRow",
                typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(920, 120);

            var hlg = root.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 18;
            hlg.padding = new RectOffset(20, 20, 12, 12);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            // Icon with offset hard shadow.
            var iconWrap = new GameObject("IconWrap", typeof(RectTransform), typeof(LayoutElement));
            iconWrap.transform.SetParent(root.transform, false);
            var iconWrapLE = iconWrap.GetComponent<LayoutElement>();
            iconWrapLE.preferredWidth = 88; iconWrapLE.preferredHeight = 88;

            var iconShadow = new GameObject("IconShadow", typeof(RectTransform), typeof(Image));
            iconShadow.transform.SetParent(iconWrap.transform, false);
            var iconShadowRT = (RectTransform)iconShadow.transform;
            iconShadowRT.anchorMin = Vector2.zero;
            iconShadowRT.anchorMax = Vector2.one;
            iconShadowRT.offsetMin = new Vector2(LabNotebookTheme.ShadowOffsetSmall,
                                                 -LabNotebookTheme.ShadowOffsetSmall);
            iconShadowRT.offsetMax = new Vector2(LabNotebookTheme.ShadowOffsetSmall,
                                                 -LabNotebookTheme.ShadowOffsetSmall);
            var iconShadowImg = iconShadow.GetComponent<Image>();
            iconShadowImg.color = new Color(LabNotebookTheme.InkDark.r,
                                            LabNotebookTheme.InkDark.g,
                                            LabNotebookTheme.InkDark.b, 0.35f);

            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(iconWrap.transform, false);
            var iconRT = (RectTransform)iconGO.transform;
            iconRT.anchorMin = Vector2.zero;
            iconRT.anchorMax = Vector2.one;
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
            var iconImg = iconGO.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.color = Color.white;

            // Name (Caveat, dark ink, large).
            var nameGO = NewLabel(root.transform, "Name", "Material",
                LabNotebookTheme.RowNameSize, LabNotebookTheme.InkDark,
                TextAlignmentOptions.Left, caveat, FontStyles.Bold,
                preferredWidth: 380);

            // Points (Caveat, color set by Bind, ink-dark default).
            var pointsGO = NewLabel(root.transform, "Points", "+1",
                LabNotebookTheme.RowPointsSize, LabNotebookTheme.InkDark,
                TextAlignmentOptions.Right, caveat, FontStyles.Bold,
                preferredWidth: 200);

            // Role badge — rotated wrapper + Image bg + TMP text inside.
            var badgeWrap = new GameObject("RoleBadgeWrap", typeof(RectTransform), typeof(LayoutElement));
            badgeWrap.transform.SetParent(root.transform, false);
            var badgeWrapRT = (RectTransform)badgeWrap.transform;
            badgeWrapRT.sizeDelta = new Vector2(180, 60);
            var badgeWrapLE = badgeWrap.GetComponent<LayoutElement>();
            badgeWrapLE.minWidth = 180; badgeWrapLE.minHeight = 60;
            badgeWrapRT.localRotation = Quaternion.Euler(0, 0, LabNotebookTheme.BadgeRotation);

            var badgeBg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            badgeBg.transform.SetParent(badgeWrap.transform, false);
            var badgeBgRT = (RectTransform)badgeBg.transform;
            badgeBgRT.anchorMin = Vector2.zero;
            badgeBgRT.anchorMax = Vector2.one;
            badgeBgRT.offsetMin = Vector2.zero;
            badgeBgRT.offsetMax = Vector2.zero;
            var badgeBgImg = badgeBg.GetComponent<Image>();
            badgeBgImg.color = LabNotebookTheme.PaperCream;

            var badgeText = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            badgeText.transform.SetParent(badgeBg.transform, false);
            var badgeTextRT = (RectTransform)badgeText.transform;
            badgeTextRT.anchorMin = Vector2.zero;
            badgeTextRT.anchorMax = Vector2.one;
            badgeTextRT.offsetMin = Vector2.zero;
            badgeTextRT.offsetMax = Vector2.zero;
            var badgeTmp = badgeText.GetComponent<TextMeshProUGUI>();
            badgeTmp.text = "NORMAL";
            badgeTmp.fontSize = LabNotebookTheme.BadgeSize;
            badgeTmp.color = LabNotebookTheme.InkGreen;
            badgeTmp.alignment = TextAlignmentOptions.Center;
            badgeTmp.fontStyle = FontStyles.Bold;
            badgeTmp.outlineColor = LabNotebookTheme.InkGreen;
            badgeTmp.outlineWidth = 0.2f;

            // Attach CubeInfoRow script and wire serialized references.
            var rowScript = root.AddComponent<CubeInfoRow>();
            var so = new SerializedObject(rowScript);
            so.FindProperty("icon").objectReferenceValue = iconImg;
            so.FindProperty("nameLabel").objectReferenceValue = nameGO.GetComponent<TMP_Text>();
            so.FindProperty("pointsLabel").objectReferenceValue = pointsGO.GetComponent<TMP_Text>();
            so.FindProperty("roleBadge").objectReferenceValue = badgeTmp;
            so.FindProperty("roleBadgeBackground").objectReferenceValue = badgeBgImg;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
            Object.DestroyImmediate(root);
        }

        // ---- Scene pieces ----

        // Step 3: notebook camera background
        private static void ConfigureCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = LabNotebookTheme.PaperCream;
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

        // Step 4: graph paper background + red margin line
        private static void BuildBackground(GameObject canvas)
        {
            var paper = AssetDatabase.LoadAssetAtPath<Sprite>(LabNotebookTheme.GraphPaperSpritePath);

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)bg.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = bg.GetComponent<Image>();
            img.sprite = paper;
            img.color = Color.white;
            img.type = Image.Type.Tiled;
            bg.transform.SetAsFirstSibling();

            var margin = new GameObject("MarginLine", typeof(RectTransform), typeof(Image));
            margin.transform.SetParent(canvas.transform, false);
            var mrt = (RectTransform)margin.transform;
            mrt.anchorMin = new Vector2(0, 0);
            mrt.anchorMax = new Vector2(0, 1);
            mrt.pivot = new Vector2(0, 0.5f);
            mrt.anchoredPosition = new Vector2(LabNotebookTheme.MarginLineX, 0);
            mrt.sizeDelta = new Vector2(2.5f, 0);
            margin.GetComponent<Image>().color = LabNotebookTheme.MarginRed;
            margin.transform.SetSiblingIndex(1);
        }

        // Step 5: Caveat title, rotated -2°
        private static GameObject BuildTitleLabel(GameObject canvas)
        {
            var caveat = LoadCaveat();
            var go = NewLabel(canvas.transform, "Title", "Material\nNinja",
                LabNotebookTheme.TitleSize, LabNotebookTheme.InkDark,
                TextAlignmentOptions.Center, caveat, FontStyles.Bold,
                preferredWidth: 0);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(20, -120);
            rt.sizeDelta = new Vector2(960, 320);
            rt.localRotation = Quaternion.Euler(0, 0, LabNotebookTheme.TitleRotation);
            return go;
        }

        // Step 5: italic subtitle "— field notes vol. 1 —"
        private static GameObject BuildSubtitle(GameObject canvas)
        {
            var caveat = LoadCaveat();
            var go = NewLabel(canvas.transform, "Subtitle", "— field notes vol. 1 —",
                LabNotebookTheme.SubtitleSize, LabNotebookTheme.SubduedInk,
                TextAlignmentOptions.Center, caveat, FontStyles.Italic,
                preferredWidth: 0);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -460);
            rt.sizeDelta = new Vector2(800, 60);
            rt.localRotation = Quaternion.Euler(0, 0, -1f);
            return go;
        }

        // Step 5: best score label in ink red
        private static GameObject BuildBestScoreLabel(GameObject canvas)
        {
            var caveat = LoadCaveat();
            var go = NewLabel(canvas.transform, "BestScore", "★ Best: 0",
                LabNotebookTheme.BestSize, LabNotebookTheme.InkRed,
                TextAlignmentOptions.Center, caveat, FontStyles.Bold,
                preferredWidth: 0);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -540);
            rt.sizeDelta = new Vector2(800, 80);
            rt.localRotation = Quaternion.Euler(0, 0, -1f);
            return go;
        }

        // Step 6: section divider with underline
        private static GameObject BuildDivider(GameObject canvas, string label, Vector2 anchored)
        {
            var caveat = LoadCaveat();
            var wrap = new GameObject(label + "Divider", typeof(RectTransform));
            wrap.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)wrap.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchored;
            rt.sizeDelta = new Vector2(920, 50);

            var text = NewLabel(wrap.transform, "Text", label.ToUpperInvariant(),
                LabNotebookTheme.DividerSize, LabNotebookTheme.SubduedInk,
                TextAlignmentOptions.Center, caveat, FontStyles.Bold,
                preferredWidth: 0);
            var trt = (RectTransform)text.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = new Vector2(0, -8);

            var underline = new GameObject("Underline", typeof(RectTransform), typeof(Image));
            underline.transform.SetParent(wrap.transform, false);
            var urt = (RectTransform)underline.transform;
            urt.anchorMin = new Vector2(0, 0);
            urt.anchorMax = new Vector2(1, 0);
            urt.pivot = new Vector2(0.5f, 0);
            urt.anchoredPosition = Vector2.zero;
            urt.sizeDelta = new Vector2(0, 1.5f);
            underline.GetComponent<Image>().color = new Color(
                LabNotebookTheme.GridBlue.r, LabNotebookTheme.GridBlue.g,
                LabNotebookTheme.GridBlue.b, 0.4f);

            return wrap;
        }

        private static (GameObject table, RectTransform rowContainer) BuildInfoTable(GameObject canvas)
        {
            var table = new GameObject("CubeInfoTable", typeof(RectTransform));
            table.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)table.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -700);
            rt.sizeDelta = new Vector2(920, 560);

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

        // Step 7: notebook-styled underline input
        private static GameObject BuildNicknameInput(GameObject canvas)
        {
            var caveat = LoadCaveat();

            var wrap = new GameObject("NicknameWrap", typeof(RectTransform));
            wrap.transform.SetParent(canvas.transform, false);
            var wrt = (RectTransform)wrap.transform;
            wrt.anchorMin = new Vector2(0.5f, 1f);
            wrt.anchorMax = new Vector2(0.5f, 1f);
            wrt.pivot = new Vector2(0.5f, 1f);
            wrt.anchoredPosition = new Vector2(0, -1340);
            wrt.sizeDelta = new Vector2(700, 200);

            var label = NewLabel(wrap.transform, "Label", "Nickname:",
                LabNotebookTheme.SubtitleSize, LabNotebookTheme.InkDark,
                TextAlignmentOptions.Left, caveat, FontStyles.Bold,
                preferredWidth: 0);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = new Vector2(0, 1);
            lrt.anchorMax = new Vector2(0, 1);
            lrt.pivot = new Vector2(0, 1);
            lrt.anchoredPosition = new Vector2(40, 0);
            lrt.sizeDelta = new Vector2(400, 50);
            lrt.localRotation = Quaternion.Euler(0, 0, -1f);

            var inputGO = new GameObject("NicknameInput",
                typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            inputGO.transform.SetParent(wrap.transform, false);
            var irt = (RectTransform)inputGO.transform;
            irt.anchorMin = new Vector2(0.5f, 1f);
            irt.anchorMax = new Vector2(0.5f, 1f);
            irt.pivot = new Vector2(0.5f, 1f);
            irt.anchoredPosition = new Vector2(0, -70);
            irt.sizeDelta = new Vector2(620, 100);
            var inputBg = inputGO.GetComponent<Image>();
            inputBg.color = new Color(0, 0, 0, 0);

            var underline = new GameObject("Underline", typeof(RectTransform), typeof(Image));
            underline.transform.SetParent(inputGO.transform, false);
            var urt = (RectTransform)underline.transform;
            urt.anchorMin = new Vector2(0, 0);
            urt.anchorMax = new Vector2(1, 0);
            urt.pivot = new Vector2(0.5f, 0);
            urt.anchoredPosition = new Vector2(0, 4);
            urt.sizeDelta = new Vector2(-20, 3f);
            underline.GetComponent<Image>().color = LabNotebookTheme.InkDark;

            var textArea = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(inputGO.transform, false);
            var tart = (RectTransform)textArea.transform;
            tart.anchorMin = Vector2.zero;
            tart.anchorMax = Vector2.one;
            tart.offsetMin = new Vector2(16, 0);
            tart.offsetMax = new Vector2(-16, 0);

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(textArea.transform, false);
            var trt = (RectTransform)textGO.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var text = textGO.GetComponent<TextMeshProUGUI>();
            text.font = caveat;
            text.fontSize = LabNotebookTheme.InputSize;
            text.color = LabNotebookTheme.InkDark;
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
            placeholder.font = caveat;
            placeholder.text = "your name here...";
            placeholder.fontSize = LabNotebookTheme.InputSize;
            placeholder.color = new Color(LabNotebookTheme.SubduedInk.r,
                                          LabNotebookTheme.SubduedInk.g,
                                          LabNotebookTheme.SubduedInk.b, 0.55f);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.fontStyle = FontStyles.Italic;

            var inputField = inputGO.GetComponent<TMP_InputField>();
            inputField.textViewport = tart;
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.characterLimit = 16;
            inputField.contentType = TMP_InputField.ContentType.Standard;

            return inputGO;
        }

        // Step 8: outlined button with hard shadow, slight rotation
        private static GameObject BuildStartButton(GameObject canvas)
        {
            var caveat = LoadCaveat();

            var wrap = new GameObject("StartButtonWrap", typeof(RectTransform));
            wrap.transform.SetParent(canvas.transform, false);
            var wrt = (RectTransform)wrap.transform;
            wrt.anchorMin = new Vector2(0.5f, 1f);
            wrt.anchorMax = new Vector2(0.5f, 1f);
            wrt.pivot = new Vector2(0.5f, 1f);
            wrt.anchoredPosition = new Vector2(0, -1700);
            wrt.sizeDelta = new Vector2(450, 140);
            wrt.localRotation = Quaternion.Euler(0, 0, LabNotebookTheme.ButtonRotation);

            var shadow = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
            shadow.transform.SetParent(wrap.transform, false);
            var srt = (RectTransform)shadow.transform;
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(LabNotebookTheme.ShadowOffsetBig,
                                        -LabNotebookTheme.ShadowOffsetBig);
            srt.offsetMax = new Vector2(LabNotebookTheme.ShadowOffsetBig,
                                        -LabNotebookTheme.ShadowOffsetBig);
            shadow.GetComponent<Image>().color = LabNotebookTheme.InkDark;

            var btn = new GameObject("StartButton",
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            btn.transform.SetParent(wrap.transform, false);
            var brt = (RectTransform)btn.transform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            var img = btn.GetComponent<Image>();
            img.color = LabNotebookTheme.PaperCream;
            var outline = btn.GetComponent<Outline>();
            outline.effectColor = LabNotebookTheme.InkDark;
            outline.effectDistance = new Vector2(3, -3);

            var label = NewLabel(btn.transform, "Label", "Start!",
                LabNotebookTheme.ButtonSize, LabNotebookTheme.InkDark,
                TextAlignmentOptions.Center, caveat, FontStyles.Bold,
                preferredWidth: 0);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            return btn;
        }

        // Step 9: "↗ tap here" annotation arrow
        private static GameObject BuildTapHereArrow(GameObject canvas)
        {
            var caveat = LoadCaveat();
            var go = NewLabel(canvas.transform, "TapHereArrow", "↗ tap here",
                LabNotebookTheme.ArrowSize, LabNotebookTheme.InkRed,
                TextAlignmentOptions.Left, caveat, FontStyles.Italic,
                preferredWidth: 0);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0, 1f);
            rt.anchoredPosition = new Vector2(180, -1690);
            rt.sizeDelta = new Vector2(300, 60);
            rt.localRotation = Quaternion.Euler(0, 0, -8f);
            return go;
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

        // Step 10: Caveat font loader
        private static TMP_FontAsset LoadCaveat()
        {
            if (_cachedCaveat == null)
                _cachedCaveat = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LabNotebookTheme.FontAssetPath);
            return _cachedCaveat;
        }

        // Step 10: font-aware label factory
        private static GameObject NewLabel(Transform parent, string name, string text,
            float fontSize, Color color, TextAlignmentOptions alignment,
            TMP_FontAsset font, FontStyles style, float preferredWidth)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = style;
            if (font != null) tmp.font = font;
            tmp.textWrappingMode = TextWrappingModes.Normal;
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
