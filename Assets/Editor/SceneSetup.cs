using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenNinja.EditorSetup
{
    /// <summary>
    /// Builds the entire SampleScene from scratch: camera, killzone, systems, canvas,
    /// and wires every script reference. Run once. Idempotent — clears the current
    /// scene and reconstructs everything.
    /// </summary>
    public static class SceneSetup
    {
        public static string Execute()
        {
            StartScreenAssetsSetup.Execute();
            RebuildComboPopupPrefab();

            var log = new List<string>();

            // ---- Fresh scene ----
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "MainScene";

            // ---- Skybox & ambient ----
            // Flat pale-grey skybox: this becomes the "ceiling/walls" the
            // back-wall and floor quads sit against, so it never reads as sky.
            var skyMat = CreateWhiteboardSky();
            RenderSettings.skybox = skyMat;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(1.0f, 1.0f, 1.0f, 1f);
            // Default-on fog darkens distant unlit geometry — the back wall
            // at z=10 is far enough from the camera that fog reads as a
            // visibly dimmer grey, ruining the bright-cleanroom feel.
            RenderSettings.fog = false;
            DynamicGI.UpdateEnvironment();
            log.Add("skybox + ambient set");

            // ---- Camera ----
            // Portrait mobile (9:16): wider FOV + lower pitch so the narrow viewport
            // still captures enough horizontal play area without lopping cubes off
            // the bottom of the screen.
            var cam = Camera.main;
            cam.transform.position = new Vector3(0f, 2f, -12f);
            cam.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
            cam.fieldOfView = 75f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = LabNotebookTheme.WhiteboardWall;
            log.Add("camera positioned");

            // ---- Tune the directional light ----
            // Neutral white "fluorescent" lab light, low shadow contrast to keep
            // the whiteboard reading evenly across the playfield.
            var dirLight = Object.FindAnyObjectByType<Light>();
            if (dirLight != null && dirLight.type == LightType.Directional)
            {
                dirLight.transform.rotation = Quaternion.Euler(60f, -20f, 0f);
                dirLight.color = new Color(1.0f, 1.0f, 1.0f);
                dirLight.intensity = 1.1f;
                dirLight.shadows = LightShadows.Soft;
                dirLight.shadowStrength = 0.25f;
                log.Add("directional light tuned");
            }

            // ---- Lab-room backdrop ----
            BuildWhiteboardBackdrop();
            log.Add("whiteboard backdrop built");

            // ---- KillZone ----
            var killZone = new GameObject("KillZone");
            killZone.transform.position = new Vector3(0f, -10f, 0f);
            var killCol = killZone.AddComponent<BoxCollider>();
            killCol.size = new Vector3(20f, 2f, 4f);
            killCol.isTrigger = true;
            killZone.AddComponent<KillZone>();
            log.Add("killzone created");

            // ---- Systems root ----
            var systems = new GameObject("Systems");

            // ---- Walls (bumpy) ----
            var walls = new GameObject("Walls");
            walls.transform.SetParent(systems.transform, false);

            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Data/BouncyWall.physicMaterial");
            // Portrait playfield: ~10 wide, ~20 tall. Side walls at ±5; top at +9.
            BuildWall(walls.transform, "Wall_Left",  position: new Vector3(-5f, 0f, 0f),
                rotation: Quaternion.identity,
                fullSize: new Vector3(1f, 22f, 4f),  segmentCount: 12, pm: pm);
            BuildWall(walls.transform, "Wall_Right", position: new Vector3(5f, 0f, 0f),
                rotation: Quaternion.identity,
                fullSize: new Vector3(1f, 22f, 4f),  segmentCount: 12, pm: pm);
            BuildWall(walls.transform, "Wall_Top",   position: new Vector3(0f, 9f, 0f),
                rotation: Quaternion.identity,
                fullSize: new Vector3(12f, 1f, 4f),  segmentCount: 12, pm: pm);

            log.Add("walls built");

            // ---- Reflection probe ----
            var probeGO = new GameObject("ReflectionProbe");
            probeGO.transform.SetParent(systems.transform, false);
            probeGO.transform.position = Vector3.zero;
            var probe = probeGO.AddComponent<ReflectionProbe>();
            probe.size = new Vector3(14f, 22f, 10f);
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
            probe.resolution = 128;
            probe.importance = 1;
            probe.boxProjection = false;
            log.Add("reflection probe added");

            var gmGO = new GameObject("GameManager");
            gmGO.transform.SetParent(systems.transform, false);
            gmGO.AddComponent<GameManager>();

            // CubeSpawner + spawn line transforms.
            var spawnerGO = new GameObject("CubeSpawner");
            spawnerGO.transform.SetParent(systems.transform, false);
            var spawner = spawnerGO.AddComponent<CubeSpawner>();

            var spawnLeft = new GameObject("SpawnLeft");
            spawnLeft.transform.SetParent(spawnerGO.transform, false);
            spawnLeft.transform.position = new Vector3(-3.5f, -6f, 0f);

            var spawnRight = new GameObject("SpawnRight");
            spawnRight.transform.SetParent(spawnerGO.transform, false);
            spawnRight.transform.position = new Vector3(3.5f, -6f, 0f);

            var cubePrefab = AssetDatabase.LoadAssetAtPath<Cube>("Assets/Prefabs/Cube.prefab");

            var spawnerSO = new SerializedObject(spawner);
            SetRef(spawnerSO, "cubePrefab", cubePrefab);
            SetRef(spawnerSO, "spawnLineLeft", spawnLeft.transform);
            SetRef(spawnerSO, "spawnLineRight", spawnRight.transform);

            // MaterialEntry weight curves over elapsed run-time.
            // Previously Wood (4) and Stone (3) dominated at t=0 with the other
            // four materials at 0.25–0.5 each, so ~80% of early spawns were
            // Wood/Stone and players rarely saw the other materials. Rebalanced
            // so all six show up within the first handful of spawns.
            var entriesProp = spawnerSO.FindProperty("entries");
            entriesProp.arraySize = 6;
            WireEntry(entriesProp, 0, "Wood",    AnimationCurve.Linear(0, 2f,   60, 1f));
            WireEntry(entriesProp, 1, "Stone",   AnimationCurve.Linear(0, 2f,   60, 1.5f));
            WireEntry(entriesProp, 2, "Metal",   AnimationCurve.Linear(0, 1.2f, 60, 2f));
            WireEntry(entriesProp, 3, "Crystal", AnimationCurve.Linear(0, 0.8f, 60, 1.5f));
            WireEntry(entriesProp, 4, "Spiked",  AnimationCurve.Linear(0, 1f,   60, 2f));
            WireEntry(entriesProp, 5, "Rubber",  AnimationCurve.Linear(0, 1f,   60, 1.5f));
            spawnerSO.ApplyModifiedPropertiesWithoutUndo();
            log.Add("spawner wired");

            // BladeController + BladeTip + TrailRenderer.
            var bladeGO = new GameObject("BladeController");
            bladeGO.transform.SetParent(systems.transform, false);
            var blade = bladeGO.AddComponent<BladeController>();

            var bladeTip = new GameObject("BladeTip");
            bladeTip.transform.SetParent(bladeGO.transform, false);
            var trail = bladeTip.AddComponent<TrailRenderer>();
            trail.time = 0.15f;
            trail.minVertexDistance = 0.02f;
            trail.startWidth = 0.15f;
            trail.endWidth = 0f;
            trail.colorGradient = new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f),
                },
            };
            trail.material = new Material(Shader.Find("Sprites/Default")) { name = "BladeTrail" };
            trail.emitting = true;

            int cubeLayer = LayerMask.NameToLayer("Cube");
            int cubeMaskValue = cubeLayer >= 0 ? (1 << cubeLayer) : ~0;

            var bladeSO = new SerializedObject(blade);
            SetRef(bladeSO, "bladeTip", bladeTip.transform);
            SetRef(bladeSO, "bladeTrail", trail);
            SetRef(bladeSO, "gameCamera", cam);
            bladeSO.FindProperty("cubeMask").intValue = cubeMaskValue;
            bladeSO.ApplyModifiedPropertiesWithoutUndo();
            log.Add($"blade wired (cube layer={cubeLayer})");

            // ---- Canvas ----
            var canvasGO = new GameObject("Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Portrait reference resolution (1080×1920) so UI elements scale
            // sensibly on the 9:16 viewport. matchWidthOrHeight = 0.5 averages
            // width/height scaling so neither axis dominates.
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            // EventSystem.
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                _ = es;
            }

            // ScorePanel — sticky note in top-left.
            var scoreCard = BuildStickyNote(canvasGO.transform, "ScorePanel",
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(0, 1), pivot: new Vector2(0, 1),
                anchored: new Vector2(60, -60), size: new Vector2(280, 160),
                rotationDeg: LabNotebookTheme.HudCardRotationLeft);

            var scoreLabelGO = NewTMP(scoreCard, "Label", "SCORE",
                LabNotebookTheme.HudLabelSize,
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1),
                anchored: new Vector2(0, -4), size: new Vector2(0, 30),
                color: LabNotebookTheme.SubduedInk, alignment: TextAlignmentOptions.Center);
            ApplyCaveat(scoreLabelGO);

            var scoreValueGO = NewTMP(scoreCard, "Value", "0",
                LabNotebookTheme.HudValueSize,
                anchorMin: new Vector2(0, 0), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 0.5f),
                anchored: new Vector2(0, -16), size: new Vector2(0, 0),
                color: LabNotebookTheme.InkDark, alignment: TextAlignmentOptions.Center);
            ApplyCaveat(scoreValueGO);

            var scoreView = scoreCard.gameObject.AddComponent<ScoreView>();
            var scoreSO = new SerializedObject(scoreView);
            SetRef(scoreSO, "label", scoreValueGO.GetComponent<TMP_Text>());
            scoreSO.FindProperty("format").stringValue = "{0}";
            scoreSO.ApplyModifiedPropertiesWithoutUndo();

            // NicknameHud — small note under the score card.
            var nickCard = BuildStickyNote(canvasGO.transform, "NicknameHud",
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(0, 1), pivot: new Vector2(0, 1),
                anchored: new Vector2(60, -240), size: new Vector2(320, 70),
                rotationDeg: LabNotebookTheme.HudCardRotationLeft);

            var nickLabel = NewTMP(nickCard, "Label", "<color=#c83232>★</color> Player:",
                LabNotebookTheme.HudNicknameSize,
                anchorMin: Vector2.zero, anchorMax: Vector2.one, pivot: new Vector2(0.5f, 0.5f),
                anchored: Vector2.zero, size: Vector2.zero,
                color: LabNotebookTheme.InkDark, alignment: TextAlignmentOptions.MidlineLeft);
            ApplyCaveat(nickLabel);
            nickLabel.GetComponent<TMP_Text>().richText = true;

            var nickHud = nickCard.gameObject.AddComponent<NicknameHud>();
            var nickSO = new SerializedObject(nickHud);
            SetRef(nickSO, "label", nickLabel.GetComponent<TMP_Text>());
            nickSO.FindProperty("format").stringValue = "<color=#c83232>★</color> Player: {0}";
            nickSO.ApplyModifiedPropertiesWithoutUndo();

            // ComboBadge wrapper — always active, holds the script.
            var comboWrap = new GameObject("ComboBadge", typeof(RectTransform));
            comboWrap.transform.SetParent(canvasGO.transform, false);
            var cwrt = (RectTransform)comboWrap.transform;
            cwrt.anchorMin = new Vector2(0.5f, 1f);
            cwrt.anchorMax = new Vector2(0.5f, 1f);
            cwrt.pivot = new Vector2(0.5f, 1f);
            cwrt.anchoredPosition = new Vector2(0, -260);
            cwrt.sizeDelta = new Vector2(320, 130);

            var comboVisual = BuildStickyNote(comboWrap.transform, "Visual",
                anchorMin: Vector2.zero, anchorMax: Vector2.one, pivot: new Vector2(0.5f, 0.5f),
                anchored: Vector2.zero, size: Vector2.zero,
                rotationDeg: LabNotebookTheme.HudCardRotationLeft);

            var comboLabelGO = NewTMP(comboVisual, "Label", "Combo ×2!",
                LabNotebookTheme.ComboBadgeSize,
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1),
                anchored: new Vector2(0, -4), size: new Vector2(0, 70),
                color: LabNotebookTheme.InkRed, alignment: TextAlignmentOptions.Center);
            ApplyCaveat(comboLabelGO);
            comboLabelGO.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

            var timerBg = new GameObject("TimerBg",
                typeof(RectTransform), typeof(Image));
            timerBg.transform.SetParent(comboVisual, false);
            var tbgRT = (RectTransform)timerBg.transform;
            tbgRT.anchorMin = new Vector2(0, 0);
            tbgRT.anchorMax = new Vector2(1, 0);
            tbgRT.pivot = new Vector2(0.5f, 0);
            tbgRT.anchoredPosition = new Vector2(0, 8);
            tbgRT.sizeDelta = new Vector2(-20, 8);
            timerBg.GetComponent<Image>().color = new Color(0, 0, 0, 0.15f);

            var timerFillGO = new GameObject("TimerFill",
                typeof(RectTransform), typeof(Image));
            timerFillGO.transform.SetParent(timerBg.transform, false);
            var tfRT = (RectTransform)timerFillGO.transform;
            tfRT.anchorMin = Vector2.zero;
            tfRT.anchorMax = Vector2.one;
            tfRT.offsetMin = Vector2.zero;
            tfRT.offsetMax = Vector2.zero;
            var timerFillImg = timerFillGO.GetComponent<Image>();
            timerFillImg.color = LabNotebookTheme.InkRed;
            timerFillImg.type = Image.Type.Filled;
            timerFillImg.fillMethod = Image.FillMethod.Horizontal;
            timerFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            timerFillImg.fillAmount = 1f;

            var badgeView = comboWrap.AddComponent<ComboBadgeView>();
            var badgeSO = new SerializedObject(badgeView);
            SetRef(badgeSO, "label", comboLabelGO.GetComponent<TMP_Text>());
            var comboVisualWrap = comboVisual.transform.parent.gameObject;
            SetRef(badgeSO, "root", comboVisualWrap);
            SetRef(badgeSO, "timerFill", timerFillImg);
            badgeSO.ApplyModifiedPropertiesWithoutUndo();
            comboVisualWrap.SetActive(false);

            // LivesRow — sticky note in top-right with three TMP hearts.
            var livesCard = BuildStickyNote(canvasGO.transform, "LivesRow",
                anchorMin: new Vector2(1, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(1, 1),
                anchored: new Vector2(-60, -60), size: new Vector2(320, 160),
                rotationDeg: LabNotebookTheme.HudCardRotationRight);

            var livesLabelGO = NewTMP(livesCard, "Label", "LIVES",
                LabNotebookTheme.HudLabelSize,
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1),
                anchored: new Vector2(0, -4), size: new Vector2(0, 30),
                color: LabNotebookTheme.SubduedInk, alignment: TextAlignmentOptions.Center);
            ApplyCaveat(livesLabelGO);

            var heartsRow = new GameObject("HeartsRow",
                typeof(RectTransform), typeof(HorizontalLayoutGroup));
            heartsRow.transform.SetParent(livesCard, false);
            var hrt = (RectTransform)heartsRow.transform;
            hrt.anchorMin = new Vector2(0, 0);
            hrt.anchorMax = new Vector2(1, 1);
            hrt.offsetMin = new Vector2(0, 0);
            hrt.offsetMax = new Vector2(0, -32);
            var hhlg = heartsRow.GetComponent<HorizontalLayoutGroup>();
            hhlg.spacing = 4;
            hhlg.childAlignment = TextAnchor.MiddleCenter;
            hhlg.childForceExpandWidth = false;
            hhlg.childForceExpandHeight = false;
            hhlg.childControlWidth = false;
            hhlg.childControlHeight = false;

            var hearts = new Graphic[3];
            for (int i = 0; i < 3; i++)
            {
                var heartGO = new GameObject($"Heart_{i + 1}",
                    typeof(RectTransform));
                heartGO.transform.SetParent(heartsRow.transform, false);
                var heartRT = (RectTransform)heartGO.transform;
                heartRT.sizeDelta = new Vector2(72, 72);
                var tmp = heartGO.AddComponent<TextMeshProUGUI>();
                tmp.text = "♥";
                tmp.fontSize = LabNotebookTheme.HudHeartSize;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = LabNotebookTheme.InkRed;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                ApplyCaveat(heartGO);
                hearts[i] = tmp;
            }

            var livesView = livesCard.gameObject.AddComponent<LivesView>();
            var livesSO = new SerializedObject(livesView);
            var heartsArr = livesSO.FindProperty("hearts");
            heartsArr.arraySize = 3;
            for (int i = 0; i < 3; i++)
                heartsArr.GetArrayElementAtIndex(i).objectReferenceValue = hearts[i];
            livesSO.FindProperty("aliveColor").colorValue = LabNotebookTheme.InkRed;
            livesSO.FindProperty("lostColor").colorValue =
                new Color(LabNotebookTheme.SubduedInk.r,
                          LabNotebookTheme.SubduedInk.g,
                          LabNotebookTheme.SubduedInk.b, 0.35f);
            livesSO.ApplyModifiedPropertiesWithoutUndo();

            // ComboPopupLayer (full-screen anchor for floaters).
            var popupLayer = new GameObject("ComboPopupLayer", typeof(RectTransform));
            popupLayer.transform.SetParent(canvasGO.transform, false);
            var plRT = (RectTransform)popupLayer.transform;
            plRT.anchorMin = Vector2.zero;
            plRT.anchorMax = Vector2.one;
            plRT.offsetMin = Vector2.zero;
            plRT.offsetMax = Vector2.zero;
            var popupSpawner = popupLayer.AddComponent<ComboPopupSpawner>();
            var popupPrefab = AssetDatabase.LoadAssetAtPath<ComboPopup>("Assets/Prefabs/ComboPopup.prefab");
            var psSO = new SerializedObject(popupSpawner);
            SetRef(psSO, "popupPrefab", popupPrefab);
            SetRef(psSO, "layer", plRT);
            SetRef(psSO, "gameCamera", cam);
            psSO.ApplyModifiedPropertiesWithoutUndo();

            // GameOver wrapper — always active, holds the script.
            var gameOverHost = new GameObject("GameOver", typeof(RectTransform));
            gameOverHost.transform.SetParent(canvasGO.transform, false);
            var gohRT = (RectTransform)gameOverHost.transform;
            gohRT.anchorMin = Vector2.zero;
            gohRT.anchorMax = Vector2.one;
            gohRT.offsetMin = Vector2.zero;
            gohRT.offsetMax = Vector2.zero;

            // Dim overlay covering the whole canvas behind the panel.
            var dim = new GameObject("DimOverlay", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(gameOverHost.transform, false);
            var dimRT = (RectTransform)dim.transform;
            dimRT.anchorMin = Vector2.zero;
            dimRT.anchorMax = Vector2.one;
            dimRT.offsetMin = Vector2.zero;
            dimRT.offsetMax = Vector2.zero;
            dim.GetComponent<Image>().color = LabNotebookTheme.GameOverDim;

            // The notebook page card (centered, slightly rotated).
            var panel = BuildStickyNote(gameOverHost.transform, "Panel",
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f),
                anchored: Vector2.zero, size: new Vector2(800, 1100),
                rotationDeg: -1f);

            // Margin line on the panel.
            var pageMargin = new GameObject("MarginLine",
                typeof(RectTransform), typeof(Image));
            pageMargin.transform.SetParent(panel, false);
            var pmRT = (RectTransform)pageMargin.transform;
            pmRT.anchorMin = new Vector2(0, 0);
            pmRT.anchorMax = new Vector2(0, 1);
            pmRT.pivot = new Vector2(0, 0.5f);
            pmRT.anchoredPosition = new Vector2(60, 0);
            pmRT.sizeDelta = new Vector2(2.5f, 0);
            pageMargin.GetComponent<Image>().color = LabNotebookTheme.MarginRed;

            // Title.
            var goTitle = NewTMP(panel, "Title", "Run Complete",
                LabNotebookTheme.GameOverTitleSize,
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1),
                anchored: new Vector2(0, -80), size: new Vector2(0, 160),
                color: LabNotebookTheme.InkDark, alignment: TextAlignmentOptions.Center);
            ApplyCaveat(goTitle);
            goTitle.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

            // Final score.
            var finalScoreGO = NewTMP(panel, "FinalScore", "Score: 0",
                LabNotebookTheme.GameOverScoreSize,
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1),
                anchored: new Vector2(0, -280), size: new Vector2(0, 100),
                color: LabNotebookTheme.InkRed, alignment: TextAlignmentOptions.Center);
            ApplyCaveat(finalScoreGO);
            finalScoreGO.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

            // NEW BEST! stamp (rotated red text with red border Image).
            var newBestWrap = new GameObject("NewBestFlag", typeof(RectTransform), typeof(Image));
            newBestWrap.transform.SetParent(panel, false);
            var nbRT = (RectTransform)newBestWrap.transform;
            nbRT.anchorMin = new Vector2(0.5f, 1);
            nbRT.anchorMax = new Vector2(0.5f, 1);
            nbRT.pivot = new Vector2(0.5f, 1);
            nbRT.anchoredPosition = new Vector2(0, -400);
            nbRT.sizeDelta = new Vector2(280, 70);
            nbRT.localRotation = Quaternion.Euler(0, 0, LabNotebookTheme.StampRotation);
            newBestWrap.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            var newBestOutline = newBestWrap.AddComponent<Outline>();
            newBestOutline.effectColor = LabNotebookTheme.InkRed;
            newBestOutline.effectDistance = new Vector2(2, -2);
            var newBestText = NewTMP(newBestWrap.transform, "Text", "NEW BEST!",
                LabNotebookTheme.NewBestStampSize,
                anchorMin: Vector2.zero, anchorMax: Vector2.one, pivot: new Vector2(0.5f, 0.5f),
                anchored: Vector2.zero, size: Vector2.zero,
                color: LabNotebookTheme.InkRed, alignment: TextAlignmentOptions.Center);
            ApplyCaveat(newBestText);
            newBestText.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

            // "next?" divider.
            var nextDiv = BuildDividerOnPanel(panel, "next?", new Vector2(0, -540));

            // Restart button.
            var restartBtn = BuildPanelButton(panel, "RestartButton", "Try Again",
                new Vector2(0, -640), LabNotebookTheme.InkDark);

            // Exit button.
            var exitBtn = BuildPanelButton(panel, "QuitButton", "Pack Up",
                new Vector2(0, -780), LabNotebookTheme.InkRed);

            // Attach GameOverView and wire references.
            var goView = gameOverHost.AddComponent<GameOverView>();
            var goSO = new SerializedObject(goView);
            var panelWrap = panel.transform.parent.gameObject;
            SetRef(goSO, "panelRoot", panelWrap);
            SetRef(goSO, "finalScoreLabel", finalScoreGO.GetComponent<TMP_Text>());
            SetRef(goSO, "restartButton", restartBtn);
            SetRef(goSO, "quitButton", exitBtn);
            SetRef(goSO, "newBestFlag", newBestWrap);
            SetRef(goSO, "spawner", spawner);
            goSO.ApplyModifiedPropertiesWithoutUndo();
            panelWrap.SetActive(false);

            // Wire the slice burst prefab into the unified Cube prefab.
            var cubePrefabAsset = AssetDatabase.LoadAssetAtPath<Cube>("Assets/Prefabs/Cube.prefab");
            var burstPrefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>("Assets/Prefabs/SliceBurst.prefab");
            if (cubePrefabAsset != null && burstPrefab != null)
            {
                var prefabSO = new SerializedObject(cubePrefabAsset);
                var burstProp = prefabSO.FindProperty("burstPrefab");
                if (burstProp != null)
                {
                    burstProp.objectReferenceValue = burstPrefab;
                    prefabSO.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(cubePrefabAsset);
                    AssetDatabase.SaveAssets();
                }
            }

            // ---- Save scene + add to build settings ----
            const string scenePath = "Assets/Scenes/MainScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!buildScenes.Exists(s => s.path == scenePath))
            {
                buildScenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }

            // ---- Bake reflection probe ----
            // Re-fetch the probe from the saved scene; the local reference may have been
            // invalidated by the scene save.
            var bakedProbe = Object.FindAnyObjectByType<ReflectionProbe>();
            if (bakedProbe != null)
            {
                string probePath = "Assets/Scenes/MainScene/ReflectionProbe-0.exr";
                var probeDir = System.IO.Path.GetDirectoryName(probePath);
                if (!AssetDatabase.IsValidFolder(probeDir))
                {
                    AssetDatabase.CreateFolder(
                        System.IO.Path.GetDirectoryName(probeDir),
                        System.IO.Path.GetFileName(probeDir));
                }
                Lightmapping.BakeReflectionProbe(bakedProbe, probePath);
                log.Add("reflection probe baked");
            }

            log.Add($"scene saved to {scenePath}");
            return string.Join(" | ", log);
        }

        /// <summary>
        /// Creates a small "sticky note" card: a paper-graph background with an
        /// offset hard-shadow Image behind, slightly rotated. Returns the inner
        /// content RectTransform — callers parent their labels into that.
        /// </summary>
        private static RectTransform BuildStickyNote(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchored, Vector2 size, float rotationDeg)
        {
            var paper = AssetDatabase.LoadAssetAtPath<Sprite>(LabNotebookTheme.GraphPaperSpritePath);

            var wrap = new GameObject(name, typeof(RectTransform));
            wrap.transform.SetParent(parent, false);
            var wrt = (RectTransform)wrap.transform;
            wrt.anchorMin = anchorMin;
            wrt.anchorMax = anchorMax;
            wrt.pivot = pivot;
            wrt.anchoredPosition = anchored;
            wrt.sizeDelta = size;
            wrt.localRotation = Quaternion.Euler(0, 0, rotationDeg);

            var shadow = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
            shadow.transform.SetParent(wrap.transform, false);
            var srt = (RectTransform)shadow.transform;
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(LabNotebookTheme.ShadowOffsetSmall,
                                        -LabNotebookTheme.ShadowOffsetSmall);
            srt.offsetMax = new Vector2(LabNotebookTheme.ShadowOffsetSmall,
                                        -LabNotebookTheme.ShadowOffsetSmall);
            shadow.GetComponent<Image>().color = new Color(0, 0, 0, 0.35f);

            var bg = new GameObject("Paper", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(wrap.transform, false);
            var brt = (RectTransform)bg.transform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            var bgImg = bg.GetComponent<Image>();
            bgImg.sprite = paper;
            bgImg.color = Color.white;
            bgImg.type = Image.Type.Tiled;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(wrap.transform, false);
            var crt = (RectTransform)content.transform;
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2(12, 8);
            crt.offsetMax = new Vector2(-12, -8);
            return crt;
        }

        private static RectTransform BuildDividerOnPanel(RectTransform parent, string label, Vector2 anchored)
        {
            var wrap = new GameObject($"Divider_{label}", typeof(RectTransform));
            wrap.transform.SetParent(parent, false);
            var rt = (RectTransform)wrap.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = anchored;
            rt.sizeDelta = new Vector2(-40, 50);

            var text = NewTMP(wrap.transform, "Text", label.ToUpperInvariant(),
                LabNotebookTheme.DividerSize,
                anchorMin: Vector2.zero, anchorMax: Vector2.one, pivot: new Vector2(0.5f, 0.5f),
                anchored: new Vector2(0, 4), size: Vector2.zero,
                color: LabNotebookTheme.SubduedInk, alignment: TextAlignmentOptions.Center);
            ApplyCaveat(text);

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

            return rt;
        }

        private static Button BuildPanelButton(RectTransform parent, string name, string label,
            Vector2 anchored, Color outlineColor)
        {
            var wrap = new GameObject($"{name}Wrap", typeof(RectTransform));
            wrap.transform.SetParent(parent, false);
            var wrt = (RectTransform)wrap.transform;
            wrt.anchorMin = new Vector2(0.5f, 1);
            wrt.anchorMax = new Vector2(0.5f, 1);
            wrt.pivot = new Vector2(0.5f, 1);
            wrt.anchoredPosition = anchored;
            wrt.sizeDelta = new Vector2(600, 110);
            wrt.localRotation = Quaternion.Euler(0, 0, -0.5f);

            var shadow = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
            shadow.transform.SetParent(wrap.transform, false);
            var srt = (RectTransform)shadow.transform;
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(LabNotebookTheme.ShadowOffsetSmall,
                                        -LabNotebookTheme.ShadowOffsetSmall);
            srt.offsetMax = new Vector2(LabNotebookTheme.ShadowOffsetSmall,
                                        -LabNotebookTheme.ShadowOffsetSmall);
            shadow.GetComponent<Image>().color = outlineColor;

            var btnGO = new GameObject(name,
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            btnGO.transform.SetParent(wrap.transform, false);
            var brt = (RectTransform)btnGO.transform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            btnGO.GetComponent<Image>().color = LabNotebookTheme.PaperCream;
            var outline = btnGO.GetComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            var labelGO = NewTMP(btnGO.transform, "Label", label,
                LabNotebookTheme.GameOverButtonSize,
                anchorMin: Vector2.zero, anchorMax: Vector2.one, pivot: new Vector2(0.5f, 0.5f),
                anchored: Vector2.zero, size: Vector2.zero,
                color: outlineColor, alignment: TextAlignmentOptions.Center);
            ApplyCaveat(labelGO);
            labelGO.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

            return btnGO.GetComponent<Button>();
        }

        private static void RebuildComboPopupPrefab()
        {
            const string path = "Assets/Prefabs/ComboPopup.prefab";
            var caveat = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LabNotebookTheme.FontAssetPath);
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);

            var root = new GameObject("ComboPopup", typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(300, 100);

            var tmp = root.AddComponent<TextMeshProUGUI>();
            tmp.text = "+1";
            if (caveat != null) tmp.font = caveat;
            tmp.fontSize = LabNotebookTheme.ComboPopupSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = LabNotebookTheme.InkRed;
            tmp.fontStyle = FontStyles.Bold;

            var popup = root.AddComponent<ComboPopup>();
            var so = new SerializedObject(popup);
            so.FindProperty("label").objectReferenceValue = tmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static TMP_FontAsset _cachedCaveat;
        private static TMP_FontAsset LoadCaveat()
        {
            if (_cachedCaveat == null)
                _cachedCaveat = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LabNotebookTheme.FontAssetPath);
            return _cachedCaveat;
        }
        private static void ApplyCaveat(GameObject tmpGO)
        {
            var tmp = tmpGO.GetComponent<TMP_Text>();
            if (tmp != null && LoadCaveat() != null) tmp.font = LoadCaveat();
        }

        private static Material CreateWhiteboardSky()
        {
            const string path = "Assets/Materials/WhiteboardSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, path);
            }
            // Pale, flat skybox — both tints near-white so the procedural
            // gradient disappears. The back wall + floor quads provide all
            // visible surface.
            sky.SetFloat("_SunDisk", 0);                                       // no sun
            sky.SetFloat("_AtmosphereThickness", 0.3f);
            sky.SetColor("_SkyTint", LabNotebookTheme.WhiteboardWall);
            sky.SetColor("_GroundColor", LabNotebookTheme.WhiteboardWall);
            sky.SetFloat("_Exposure", 1.0f);
            EditorUtility.SetDirty(sky);
            return sky;
        }

        // ---- Whiteboard backdrop (gameplay scene) ----

        private static void BuildWhiteboardBackdrop()
        {
            var root = new GameObject("Backdrop");
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // Back wall: a 40×30 quad far enough behind the playfield (z=10) to
            // fill the camera's FOV and act as a flat whiteboard surface.
            // Unity's Quad primitive faces +Z; we need it facing -Z so the
            // camera (at z=-12 looking forward) sees the front. Without this
            // flip, backface culling makes the wall invisible and we end up
            // looking straight through to the procedural skybox.
            var wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            wall.name = "Whiteboard";
            wall.transform.SetParent(root.transform, false);
            wall.transform.position = new Vector3(0f, 2f, 10f);
            wall.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            wall.transform.localScale = new Vector3(40f, 30f, 1f);
            Object.DestroyImmediate(wall.GetComponent<Collider>());
            wall.GetComponent<MeshRenderer>().sharedMaterial =
                CreateUnlitColorMaterial("WhiteboardWall", LabNotebookTheme.WhiteboardWall);

            // Rubber floor: a 40×20 horizontal quad below the killzone (y=-10),
            // tilted 90° so it lies flat. Slightly darker grey than the wall.
            // Floor: tilted 90° about X so its +Z face becomes +Y (pointing
            // up at the camera). The 180° flip around Y is unnecessary here
            // because the tilted +Z is already facing up.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floor.name = "RubberFloor";
            floor.transform.SetParent(root.transform, false);
            floor.transform.position = new Vector3(0f, -10f, 5f);
            floor.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            floor.transform.localScale = new Vector3(40f, 20f, 1f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.GetComponent<MeshRenderer>().sharedMaterial =
                CreateUnlitColorMaterial("RubberFloor", LabNotebookTheme.RubberFloor);

            // Chemistry formula decals — scattered Caveat handwriting in red/blue
            // marker, positioned just in front of the whiteboard. Font sizes are
            // chosen to read at the 1080x1920 portrait viewport (~5 world units
            // tall = roughly thumbnail-readable at gameplay distance).
            BuildFormulaDecal(root.transform, "H2O",      new Vector3(-10f,   9f, 9.95f), 5.0f, LabNotebookTheme.MarkerBlue, -6f);
            BuildFormulaDecal(root.transform, "C6H12O6",  new Vector3(  6f,  8f, 9.95f), 4.4f, LabNotebookTheme.MarkerBlue, +4f);
            BuildFormulaDecal(root.transform, "NaCl",     new Vector3(-11f,  3f, 9.95f), 4.8f, LabNotebookTheme.MarkerRed,  +8f);
            BuildFormulaDecal(root.transform, "pH 7",     new Vector3(  9f,  3.5f, 9.95f), 4.2f, LabNotebookTheme.MarkerRed,  -3f);
            BuildFormulaDecal(root.transform, "CO2 + H2O",new Vector3(-8f,  -2f, 9.95f), 4.4f, LabNotebookTheme.MarkerBlue, +2f);
            BuildFormulaDecal(root.transform, "Fe -> Fe2O3", new Vector3( 7f, -3f, 9.95f), 4.0f, LabNotebookTheme.MarkerRed,  -5f);
            BuildFormulaDecal(root.transform, "100°C",    new Vector3(-4f, -7f, 9.95f), 5.6f, LabNotebookTheme.MarkerBlue, +7f);
        }

        private static void BuildFormulaDecal(Transform parent, string text, Vector3 worldPos,
            float fontSize, Color color, float zRotationDegrees)
        {
            var go = new GameObject("Formula_" + text);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, zRotationDegrees);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            var caveat = LoadCaveat();
            if (caveat != null) tmp.font = caveat;
        }

        private static Material CreateUnlitColorMaterial(string name, Color color)
        {
            string path = $"Assets/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Unlit/Color"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_Color", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void WireEntry(SerializedProperty arr, int index, string materialName, AnimationCurve curve)
        {
            var entry = arr.GetArrayElementAtIndex(index);
            string path = $"Assets/Data/CubeMaterials/{materialName}.asset";
            var so = AssetDatabase.LoadAssetAtPath<CubeMaterial>(path);
            entry.FindPropertyRelative("material").objectReferenceValue = so;
            entry.FindPropertyRelative("weightOverTime").animationCurveValue = curve;
        }

        /// <summary>
        /// Builds a wall composed of `segmentCount` BoxCollider children, each tilted by
        /// a random angle in [-8, +8] degrees around the wall's local Z axis so that
        /// reflections vary segment to segment.
        /// </summary>
        private static void BuildWall(Transform parent, string name, Vector3 position,
            Quaternion rotation, Vector3 fullSize, int segmentCount, PhysicsMaterial pm)
        {
            var wall = new GameObject(name);
            wall.transform.SetParent(parent, false);
            wall.transform.SetPositionAndRotation(position, rotation);
            wall.tag = "Wall";

            // Visible mesh: one stretched cube. Cosmetic only.
            var visualGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visualGO.name = "Visual";
            Object.DestroyImmediate(visualGO.GetComponent<Collider>()); // collider is on segments
            visualGO.transform.SetParent(wall.transform, false);
            visualGO.transform.localScale = fullSize;
            // Walls are physics-only. Hide the visual; the player should see cubes
            // bounce against an invisible boundary, not a tilted slab at the edge of view.
            var mr = visualGO.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;

            // Segment colliders.
            bool isHorizontal = fullSize.x > fullSize.y;
            float length = isHorizontal ? fullSize.x : fullSize.y;
            float segmentLength = length / segmentCount;
            var rng = new System.Random(name.GetHashCode());

            for (int i = 0; i < segmentCount; i++)
            {
                var seg = new GameObject($"Seg_{i}", typeof(BoxCollider));
                seg.transform.SetParent(wall.transform, false);
                seg.tag = "Wall";

                float t = (i + 0.5f) / segmentCount; // [0,1) centers
                float localOffset = Mathf.Lerp(-length * 0.5f, length * 0.5f, t);
                Vector3 localPos = isHorizontal
                    ? new Vector3(localOffset, 0f, 0f)
                    : new Vector3(0f, localOffset, 0f);

                float angleDeg = (float)(rng.NextDouble() * 16.0 - 8.0); // [-8, +8]
                seg.transform.localPosition = localPos;
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);

                var col = seg.GetComponent<BoxCollider>();
                col.size = isHorizontal
                    ? new Vector3(segmentLength * 1.05f, fullSize.y, fullSize.z)
                    : new Vector3(fullSize.x, segmentLength * 1.05f, fullSize.z);
                if (pm != null) col.material = pm;
            }
        }

        private static void SetRef(SerializedObject so, string name, Object obj)
        {
            var prop = so.FindProperty(name);
            if (prop == null)
                throw new System.InvalidOperationException($"Missing serialized property '{name}' on {so.targetObject}");
            prop.objectReferenceValue = obj;
        }

        private static GameObject NewTMP(Transform parent, string name, string text, float fontSize,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchored, Vector2 size,
            Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchored;
            rt.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = FontStyles.Bold;
            return go;
        }
    }
}
