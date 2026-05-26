using UnityEditor;
using UnityEngine;

namespace OpenNinja.EditorSetup
{
    public static class SliceBurstSetup
    {
        public static string Execute()
        {
            var go = GameObject.Find("SliceBurst");
            if (go == null) return "ERROR: SliceBurst not found in scene";

            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null) return "ERROR: SliceBurst has no ParticleSystem";

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 0.6f;
            main.startSpeed = 4f;
            main.startSize = 0.1f;
            main.gravityModifier = 1.5f;
            main.maxParticles = 50;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            // Save prefab.
            const string prefabPath = "Assets/Prefabs/SliceBurst.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, InteractionMode.AutomatedAction);
            if (prefab == null) return "ERROR: failed to save prefab";

            return $"SliceBurst configured and saved to {prefabPath}";
        }
    }
}
