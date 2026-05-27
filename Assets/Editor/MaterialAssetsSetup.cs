using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

namespace OpenNinja.EditorSetup
{
    public static class MaterialAssetsSetup
    {
        private const string MaterialsDir = "Assets/Materials";
        private const string CubeMaterialsDir = "Assets/Data/CubeMaterials";
        private const string DataDir = "Assets/Data";
        private const string PrefabsDir = "Assets/Prefabs";
        private const string PhysicMaterialPath = "Assets/Data/BouncyWall.physicMaterial";

        public static string Execute()
        {
            EnsureFolder(MaterialsDir);
            EnsureFolder(DataDir);
            EnsureFolder(CubeMaterialsDir);
            EnsureFolder(PrefabsDir);
            EnsureTag("Wall");
            EnsureLayer("Cube");

            // 1) Delete legacy cube color prefabs + materials.
            DeleteIfExists("Assets/Prefabs/Cube_Green.prefab");
            DeleteIfExists("Assets/Prefabs/Cube_Red.prefab");
            DeleteIfExists("Assets/Prefabs/Cube_Black.prefab");
            DeleteIfExists("Assets/Materials/Cube_Green.mat");
            DeleteIfExists("Assets/Materials/Cube_Red.mat");
            DeleteIfExists("Assets/Materials/Cube_Black.mat");

            // 2) Render materials.
            var woodMat    = MakeMaterial("Wood",    new Color(0.55f, 0.35f, 0.2f, 1f), 0.7f, 0f);
            var stoneMat   = MakeMaterial("Stone",   new Color(0.55f, 0.55f, 0.55f, 1f), 0.5f, 0f);
            var metalMat   = MakeMaterial("Metal",   new Color(0.25f, 0.25f, 0.27f, 1f), 0.85f, 1f);
            var crystalMat = MakeMaterial("Crystal", new Color(0.55f, 0.85f, 1f, 1f), 0.95f, 0.3f);
            var spikedMat  = MakeMaterial("Spiked",  new Color(0.1f, 0.1f, 0.1f, 1f), 0.3f, 0f);
            var rubberMat  = MakeMaterial("Rubber",  new Color(1f, 0.9f, 0.2f, 1f), 0.15f, 0f);

            // 3) BouncyWall physic material.
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicMaterialPath);
            if (pm == null) pm = new PhysicsMaterial("BouncyWall");
            pm.bounciness = 0.75f;
            pm.dynamicFriction = 0.1f;
            pm.staticFriction = 0.1f;
            pm.bounceCombine = PhysicsMaterialCombine.Maximum;
            pm.frictionCombine = PhysicsMaterialCombine.Minimum;
            if (AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicMaterialPath) == null)
                AssetDatabase.CreateAsset(pm, PhysicMaterialPath);
            else
                EditorUtility.SetDirty(pm);

            // 4) CubeMaterial SO assets.
            MakeCubeMaterial("Wood",    role: CubeRole.Normal, basePts: 1, mass: 0.4f, scale: 0.7f,
                renderMat: woodMat,    burst: new Color(0.65f, 0.45f, 0.25f, 1f),
                bounceMult: 1.0f, launchOverride: new Vector2(6f, 12f));
            MakeCubeMaterial("Stone",   role: CubeRole.Normal, basePts: 2, mass: 1.0f, scale: 1.0f,
                renderMat: stoneMat,   burst: new Color(0.7f, 0.7f, 0.7f, 1f),
                bounceMult: 1.0f, launchOverride: Vector2.zero);
            MakeCubeMaterial("Metal",   role: CubeRole.Bonus,  basePts: 3, mass: 3.0f, scale: 1.4f,
                renderMat: metalMat,   burst: new Color(0.5f, 0.55f, 0.6f, 1f),
                bounceMult: 0.9f, launchOverride: new Vector2(10f, 14f));
            MakeCubeMaterial("Crystal", role: CubeRole.Bonus,  basePts: 5, mass: 0.3f, scale: 0.9f,
                renderMat: crystalMat, burst: new Color(0.7f, 0.95f, 1f, 1f),
                bounceMult: 1.0f, launchOverride: Vector2.zero);
            MakeCubeMaterial("Spiked",  role: CubeRole.Danger, basePts: 0, mass: 1.2f, scale: 1.0f,
                renderMat: spikedMat,  burst: new Color(0.4f, 0.0f, 0.4f, 1f),
                bounceMult: 1.0f, launchOverride: Vector2.zero);
            MakeCubeMaterial("Rubber",  role: CubeRole.Normal, basePts: 2, mass: 0.5f, scale: 0.9f,
                renderMat: rubberMat,  burst: new Color(1f, 0.95f, 0.3f, 1f),
                bounceMult: 1.4f, launchOverride: Vector2.zero);

            // 5) Base Cube.prefab.
            string cubePrefabPath = "Assets/Prefabs/Cube.prefab";
            DeleteIfExists(cubePrefabPath);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Cube";
            go.layer = LayerMask.NameToLayer("Cube");
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.1f;
            var cube = go.AddComponent<OpenNinja.Cube>();
            // burstPrefab assignment wired by SceneSetup; left null here.
            PrefabUtility.SaveAsPrefabAsset(go, cubePrefabPath);
            Object.DestroyImmediate(go);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "Created materials, CubeMaterial SOs, BouncyWall.physicMaterial, Cube.prefab; deleted legacy color cubes.";
        }

        private static Material MakeMaterial(string name, Color color, float smoothness, float metallic)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void MakeCubeMaterial(string name, CubeRole role, int basePts, float mass,
            float scale, Material renderMat, Color burst, float bounceMult, Vector2 launchOverride)
        {
            string path = $"{CubeMaterialsDir}/{name}.asset";
            var so = AssetDatabase.LoadAssetAtPath<OpenNinja.CubeMaterial>(path);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<OpenNinja.CubeMaterial>();
                AssetDatabase.CreateAsset(so, path);
            }
            so.displayName = name;
            so.role = role;
            so.basePoints = basePts;
            so.mass = mass;
            so.displayScale = scale;
            so.renderMaterial = renderMat;
            so.burstTint = burst;
            so.bouncinessMultiplier = bounceMult;
            so.launchImpulseOverride = launchOverride;
            so.audioPitchAtMassOne = 1f;
            so.audioVolume = 0.7f;
            EditorUtility.SetDirty(so);
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            string leaf = Path.GetFileName(assetPath);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void EnsureTag(string tag)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset");
            var so = new SerializedObject(asset);
            var tagsProp = so.FindProperty("tags");
            for (int i = 0; i < tagsProp.arraySize; i++)
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) return;
            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureLayer(string layer)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset");
            var so = new SerializedObject(asset);
            var layersProp = so.FindProperty("layers");
            for (int i = 8; i < layersProp.arraySize; i++)
            {
                var prop = layersProp.GetArrayElementAtIndex(i);
                if (prop.stringValue == layer) return;
                if (string.IsNullOrEmpty(prop.stringValue))
                {
                    prop.stringValue = layer;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);
        }
    }
}
