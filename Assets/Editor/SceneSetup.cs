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
    /// Builds the entire SampleScene from scratch: camera, killzone, systems, canvas,
    /// and wires every script reference. Run once. Idempotent — clears the current
    /// scene and reconstructs everything.
    /// </summary>
    public static class SceneSetup
    {
        public static string Execute()
        {
            var log = new List<string>();

            // ---- Fresh scene ----
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "SampleScene";

            // ---- Camera ----
            var cam = Camera.main;
            cam.transform.position = new Vector3(0f, 4f, -12f);
            cam.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            cam.fieldOfView = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.12f, 1f);
            log.Add("camera positioned");

            // ---- KillZone ----
            var killZone = new GameObject("KillZone");
            killZone.transform.position = new Vector3(0f, -8f, 0f);
            var killCol = killZone.AddComponent<BoxCollider>();
            killCol.size = new Vector3(40f, 2f, 4f);
            killCol.isTrigger = true;
            killZone.AddComponent<KillZone>();
            log.Add("killzone created");

            // ---- Systems root ----
            var systems = new GameObject("Systems");

            var gmGO = new GameObject("GameManager");
            gmGO.transform.SetParent(systems.transform, false);
            gmGO.AddComponent<GameManager>();

            // CubeSpawner + spawn line transforms.
            var spawnerGO = new GameObject("CubeSpawner");
            spawnerGO.transform.SetParent(systems.transform, false);
            var spawner = spawnerGO.AddComponent<CubeSpawner>();

            var spawnLeft = new GameObject("SpawnLeft");
            spawnLeft.transform.SetParent(spawnerGO.transform, false);
            spawnLeft.transform.position = new Vector3(-7f, -6f, 0f);

            var spawnRight = new GameObject("SpawnRight");
            spawnRight.transform.SetParent(spawnerGO.transform, false);
            spawnRight.transform.position = new Vector3(7f, -6f, 0f);

            var greenPrefab = AssetDatabase.LoadAssetAtPath<Cube>("Assets/Prefabs/Cube_Green.prefab");
            var redPrefab = AssetDatabase.LoadAssetAtPath<Cube>("Assets/Prefabs/Cube_Red.prefab");
            var blackPrefab = AssetDatabase.LoadAssetAtPath<Cube>("Assets/Prefabs/Cube_Black.prefab");

            var spawnerSO = new SerializedObject(spawner);
            SetRef(spawnerSO, "greenPrefab", greenPrefab);
            SetRef(spawnerSO, "redPrefab", redPrefab);
            SetRef(spawnerSO, "blackPrefab", blackPrefab);
            SetRef(spawnerSO, "spawnLineLeft", spawnLeft.transform);
            SetRef(spawnerSO, "spawnLineRight", spawnRight.transform);
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
            scaler.referenceResolution = new Vector2(1920, 1080);

            // EventSystem.
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                _ = es;
            }

            // ScorePanel (TMP_Text in top-left).
            var scoreGO = NewTMP(canvasGO.transform, "ScorePanel", "Score: 0", 48,
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(0, 1), pivot: new Vector2(0, 1),
                anchored: new Vector2(40, -40), size: new Vector2(400, 80),
                color: Color.white, alignment: TextAlignmentOptions.TopLeft);
            var scoreView = scoreGO.AddComponent<ScoreView>();
            var scoreSO = new SerializedObject(scoreView);
            SetRef(scoreSO, "label", scoreGO.GetComponent<TMP_Text>());
            scoreSO.ApplyModifiedPropertiesWithoutUndo();

            // ComboBadge wrapper (always active, holds the script).
            // Visual child is what gets toggled when the multiplier is 1.
            var comboBadge = new GameObject("ComboBadge", typeof(RectTransform));
            comboBadge.transform.SetParent(canvasGO.transform, false);
            var cbRT = (RectTransform)comboBadge.transform;
            cbRT.anchorMin = new Vector2(0.5f, 1f);
            cbRT.anchorMax = new Vector2(0.5f, 1f);
            cbRT.pivot = new Vector2(0.5f, 1f);
            cbRT.anchoredPosition = new Vector2(0, -40);
            cbRT.sizeDelta = new Vector2(400, 80);

            var badgeVisual = new GameObject("Visual", typeof(RectTransform));
            badgeVisual.transform.SetParent(comboBadge.transform, false);
            var bvRT = (RectTransform)badgeVisual.transform;
            bvRT.anchorMin = Vector2.zero;
            bvRT.anchorMax = Vector2.one;
            bvRT.offsetMin = Vector2.zero;
            bvRT.offsetMax = Vector2.zero;

            var badgeLabel = NewTMP(badgeVisual.transform, "Label", "Combo x2", 48,
                anchorMin: Vector2.zero, anchorMax: Vector2.one, pivot: new Vector2(0.5f, 0.5f),
                anchored: Vector2.zero, size: Vector2.zero,
                color: new Color(1f, 0.85f, 0.2f, 1f), alignment: TextAlignmentOptions.Center);

            // Combo timer bar (drains as the window expires).
            var timerBg = new GameObject("TimerBg", typeof(RectTransform), typeof(Image));
            timerBg.transform.SetParent(badgeVisual.transform, false);
            var tbRT = (RectTransform)timerBg.transform;
            tbRT.anchorMin = new Vector2(0.1f, 0f);
            tbRT.anchorMax = new Vector2(0.9f, 0f);
            tbRT.pivot = new Vector2(0.5f, 0f);
            tbRT.anchoredPosition = new Vector2(0, -4);
            tbRT.sizeDelta = new Vector2(0, 8);
            timerBg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

            var timerFillGO = new GameObject("TimerFill", typeof(RectTransform), typeof(Image));
            timerFillGO.transform.SetParent(timerBg.transform, false);
            var tfRT = (RectTransform)timerFillGO.transform;
            tfRT.anchorMin = Vector2.zero;
            tfRT.anchorMax = Vector2.one;
            tfRT.offsetMin = Vector2.zero;
            tfRT.offsetMax = Vector2.zero;
            var timerFillImage = timerFillGO.GetComponent<Image>();
            timerFillImage.color = new Color(1f, 0.85f, 0.2f, 1f);
            timerFillImage.type = Image.Type.Filled;
            timerFillImage.fillMethod = Image.FillMethod.Horizontal;
            timerFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            timerFillImage.fillAmount = 1f;

            var badgeView = comboBadge.AddComponent<ComboBadgeView>();
            var badgeSO = new SerializedObject(badgeView);
            SetRef(badgeSO, "label", badgeLabel.GetComponent<TMP_Text>());
            SetRef(badgeSO, "root", badgeVisual);
            SetRef(badgeSO, "timerFill", timerFillImage);
            badgeSO.ApplyModifiedPropertiesWithoutUndo();
            badgeVisual.SetActive(false);

            // LivesRow (top-right, 3 heart images).
            var livesRow = new GameObject("LivesRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            livesRow.transform.SetParent(canvasGO.transform, false);
            var lrRT = (RectTransform)livesRow.transform;
            lrRT.anchorMin = new Vector2(1, 1);
            lrRT.anchorMax = new Vector2(1, 1);
            lrRT.pivot = new Vector2(1, 1);
            lrRT.anchoredPosition = new Vector2(-40, -40);
            lrRT.sizeDelta = new Vector2(280, 80);
            var hlg = livesRow.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12;
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            var hearts = new Graphic[3];
            for (int i = 0; i < 3; i++)
            {
                var heartGO = new GameObject($"Heart_{i + 1}", typeof(RectTransform));
                heartGO.transform.SetParent(livesRow.transform, false);
                var heartRT = (RectTransform)heartGO.transform;
                heartRT.sizeDelta = new Vector2(72, 72);
                var tmp = heartGO.AddComponent<TextMeshProUGUI>();
                tmp.text = "♥"; // ♥
                tmp.fontSize = 72f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = new Color(1f, 0.25f, 0.3f, 1f);
                tmp.enableWordWrapping = false;
                hearts[i] = tmp;
            }
            var livesView = livesRow.AddComponent<LivesView>();
            var livesSO = new SerializedObject(livesView);
            var heartsArr = livesSO.FindProperty("hearts");
            heartsArr.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                heartsArr.GetArrayElementAtIndex(i).objectReferenceValue = hearts[i];
            }
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

            // GameOver wrapper (always active, holds the script).
            // The dim Panel child is what Awake hides and OnGameOver shows.
            var gameOverHost = new GameObject("GameOver", typeof(RectTransform));
            gameOverHost.transform.SetParent(canvasGO.transform, false);
            var gohRT = (RectTransform)gameOverHost.transform;
            gohRT.anchorMin = Vector2.zero;
            gohRT.anchorMax = Vector2.one;
            gohRT.offsetMin = Vector2.zero;
            gohRT.offsetMax = Vector2.zero;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(gameOverHost.transform, false);
            var pnlRT = (RectTransform)panel.transform;
            pnlRT.anchorMin = Vector2.zero;
            pnlRT.anchorMax = Vector2.one;
            pnlRT.offsetMin = Vector2.zero;
            pnlRT.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0, 0, 0, 0.7f);

            var finalScore = NewTMP(panel.transform, "FinalScore", "Score: 0", 96,
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f),
                anchored: new Vector2(0, 60), size: new Vector2(800, 160),
                color: Color.white, alignment: TextAlignmentOptions.Center);

            var btnGO = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(panel.transform, false);
            var btnRT = (RectTransform)btnGO.transform;
            btnRT.anchorMin = new Vector2(0.5f, 0.5f);
            btnRT.anchorMax = new Vector2(0.5f, 0.5f);
            btnRT.pivot = new Vector2(0.5f, 0.5f);
            btnRT.anchoredPosition = new Vector2(0, -80);
            btnRT.sizeDelta = new Vector2(320, 100);
            btnGO.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.8f, 1f);

            var btnLabel = NewTMP(btnGO.transform, "Label", "Restart", 48,
                anchorMin: Vector2.zero, anchorMax: Vector2.one, pivot: new Vector2(0.5f, 0.5f),
                anchored: Vector2.zero, size: Vector2.zero,
                color: Color.white, alignment: TextAlignmentOptions.Center);

            var goView = gameOverHost.AddComponent<GameOverView>();
            var goSO = new SerializedObject(goView);
            SetRef(goSO, "panelRoot", panel);
            SetRef(goSO, "finalScoreLabel", finalScore.GetComponent<TMP_Text>());
            SetRef(goSO, "restartButton", btnGO.GetComponent<Button>());
            SetRef(goSO, "spawner", spawner);
            goSO.ApplyModifiedPropertiesWithoutUndo();
            panel.SetActive(false);

            // ---- Save scene + add to build settings ----
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!buildScenes.Exists(s => s.path == scenePath))
            {
                buildScenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }

            log.Add($"scene saved to {scenePath}");
            return string.Join(" | ", log);
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
