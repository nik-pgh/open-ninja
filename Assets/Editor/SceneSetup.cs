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
            var log = new List<string>();

            // ---- Fresh scene ----
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "MainScene";

            // ---- Skybox & ambient ----
            var skyMat = CreateProceduralSky();
            RenderSettings.skybox = skyMat;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
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
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.12f, 1f);
            log.Add("camera positioned");

            // ---- Tune the directional light ----
            var dirLight = Object.FindAnyObjectByType<Light>();
            if (dirLight != null && dirLight.type == LightType.Directional)
            {
                dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                dirLight.color = new Color(1.0f, 0.95f, 0.85f);
                dirLight.intensity = 1.0f;
                dirLight.shadows = LightShadows.Soft;
                dirLight.shadowStrength = 0.6f;
                log.Add("directional light tuned");
            }

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

            // ScorePanel (TMP_Text in top-left).
            var scoreGO = NewTMP(canvasGO.transform, "ScorePanel", "Score: 0", 48,
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(0, 1), pivot: new Vector2(0, 1),
                anchored: new Vector2(40, -40), size: new Vector2(400, 80),
                color: Color.white, alignment: TextAlignmentOptions.TopLeft);
            var scoreView = scoreGO.AddComponent<ScoreView>();
            var scoreSO = new SerializedObject(scoreView);
            SetRef(scoreSO, "label", scoreGO.GetComponent<TMP_Text>());
            scoreSO.ApplyModifiedPropertiesWithoutUndo();

            // NicknameHud (label under the score; reads PlayerSession.Nickname).
            var nickGO = NewTMP(canvasGO.transform, "NicknameHud", "Player:", 36,
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(0, 1), pivot: new Vector2(0, 1),
                anchored: new Vector2(40, -120), size: new Vector2(700, 60),
                color: new Color(1f, 1f, 1f, 0.7f), alignment: TextAlignmentOptions.TopLeft);
            var nickHud = nickGO.AddComponent<NicknameHud>();
            var nickSO = new SerializedObject(nickHud);
            SetRef(nickSO, "label", nickGO.GetComponent<TMP_Text>());
            nickSO.ApplyModifiedPropertiesWithoutUndo();

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
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
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

        private static Material CreateProceduralSky()
        {
            const string path = "Assets/Materials/ProceduralSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, path);
            }
            sky.SetFloat("_SunDisk", 2);                                       // small sun disk
            sky.SetFloat("_AtmosphereThickness", 0.9f);                        // slight haze
            sky.SetColor("_SkyTint", new Color(0.5f, 0.7f, 0.95f, 1f));
            sky.SetColor("_GroundColor", new Color(0.2f, 0.18f, 0.16f, 1f));
            sky.SetFloat("_Exposure", 1.0f);
            EditorUtility.SetDirty(sky);
            return sky;
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
