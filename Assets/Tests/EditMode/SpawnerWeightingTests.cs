using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace OpenNinja.Tests
{
    public class SpawnerWeightingTests
    {
        private static CubeMaterial Mat(string name, int basePoints)
        {
            var m = ScriptableObject.CreateInstance<CubeMaterial>();
            m.displayName = name;
            m.basePoints = basePoints;
            m.mass = 1f;
            m.displayScale = 1f;
            m.bouncinessMultiplier = 1f;
            return m;
        }

        [Test]
        public void PickMaterial_TwoEntriesEqualWeight_FiftyFifty()
        {
            var go = new GameObject("Spawner");
            var spawner = go.AddComponent<CubeSpawner>();
            spawner.SetEntriesForTest(new List<CubeSpawner.MaterialEntry>
            {
                new() { material = Mat("A", 1), weightOverTime = AnimationCurve.Constant(0, 60, 1f) },
                new() { material = Mat("B", 2), weightOverTime = AnimationCurve.Constant(0, 60, 1f) },
            });

            Random.InitState(12345);
            int aCount = 0, bCount = 0;
            for (int i = 0; i < 2000; i++)
            {
                var picked = spawner.PickMaterial(0f);
                if (picked.displayName == "A") aCount++;
                else if (picked.displayName == "B") bCount++;
            }
            float ratio = (float)aCount / (aCount + bCount);
            Assert.That(ratio, Is.EqualTo(0.5f).Within(0.05f),
                "Equal weights should yield ~50/50 with 5% tolerance");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void PickMaterial_ZeroWeight_NeverPicked()
        {
            var go = new GameObject("Spawner");
            var spawner = go.AddComponent<CubeSpawner>();
            spawner.SetEntriesForTest(new List<CubeSpawner.MaterialEntry>
            {
                new() { material = Mat("Active", 1), weightOverTime = AnimationCurve.Constant(0, 60, 1f) },
                new() { material = Mat("Zero",   2), weightOverTime = AnimationCurve.Constant(0, 60, 0f) },
            });

            Random.InitState(42);
            for (int i = 0; i < 500; i++)
            {
                Assert.AreEqual("Active", spawner.PickMaterial(0f).displayName);
            }
            Object.DestroyImmediate(go);
        }

        [Test]
        public void PickMaterial_TimeChangesDistribution()
        {
            var go = new GameObject("Spawner");
            var spawner = go.AddComponent<CubeSpawner>();
            spawner.SetEntriesForTest(new List<CubeSpawner.MaterialEntry>
            {
                new() { material = Mat("Early", 1), weightOverTime = AnimationCurve.Linear(0, 1, 60, 0) },
                new() { material = Mat("Late",  2), weightOverTime = AnimationCurve.Linear(0, 0, 60, 1) },
            });

            Random.InitState(7);
            int earlyAt0 = 0;
            for (int i = 0; i < 500; i++)
                if (spawner.PickMaterial(0f).displayName == "Early") earlyAt0++;
            int lateAt60 = 0;
            for (int i = 0; i < 500; i++)
                if (spawner.PickMaterial(60f).displayName == "Late") lateAt60++;

            Assert.That(earlyAt0, Is.GreaterThan(450), "At t=0, Early should dominate");
            Assert.That(lateAt60, Is.GreaterThan(450), "At t=60, Late should dominate");
            Object.DestroyImmediate(go);
        }
    }
}
