using NUnit.Framework;
using UnityEngine;

namespace OpenNinja.Tests
{
    public class CubeTests
    {
        private GameObject _gmGO;
        private GameManager _gm;

        private static CubeMaterial MakeMaterial(int basePoints, CubeRole role, float mass = 1f)
        {
            var mat = ScriptableObject.CreateInstance<CubeMaterial>();
            mat.basePoints = basePoints;
            mat.role = role;
            mat.mass = mass;
            mat.displayScale = 1f;
            mat.bouncinessMultiplier = 1f;
            mat.burstTint = Color.white;
            return mat;
        }

        [SetUp]
        public void SetUp()
        {
            _gmGO = new GameObject("GameManager");
            _gm = _gmGO.AddComponent<GameManager>();
            _gm.ConfigureForTests(0.5f, 3, 8);
            _gm.ResetGame();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gmGO);
            Time.timeScale = 1f;
        }

        [Test]
        public void NormalCube_HandleSlice_RegistersHit()
        {
            var go = new GameObject("Green", typeof(Rigidbody), typeof(BoxCollider));
            var cube = go.AddComponent<Cube>();
            cube.Initialize(MakeMaterial(1, CubeRole.Normal));
            cube.HandleSlice(Vector3.zero);
            Assert.AreEqual(1, _gm.Score);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void BonusCube_HandleSlice_AwardsBasePoints()
        {
            var go = new GameObject("Bonus", typeof(Rigidbody), typeof(BoxCollider));
            var cube = go.AddComponent<Cube>();
            cube.Initialize(MakeMaterial(5, CubeRole.Bonus));
            cube.HandleSlice(Vector3.zero);
            Assert.AreEqual(5, _gm.Score);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void DangerCube_HandleSlice_DeductsLife()
        {
            var go = new GameObject("Spiked", typeof(Rigidbody), typeof(BoxCollider));
            var cube = go.AddComponent<Cube>();
            cube.Initialize(MakeMaterial(0, CubeRole.Danger));
            cube.HandleSlice(Vector3.zero);
            Assert.AreEqual(2, _gm.Lives);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Cube_DoubleSlice_OnlyAwardsOnce()
        {
            var go = new GameObject("Wood", typeof(Rigidbody), typeof(BoxCollider));
            var cube = go.AddComponent<Cube>();
            cube.Initialize(MakeMaterial(1, CubeRole.Normal));
            cube.HandleSlice(Vector3.zero);
            cube.HandleSlice(Vector3.zero);
            Assert.AreEqual(1, _gm.Score);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void NormalCube_HandleFellOff_RegistersMiss()
        {
            var go = new GameObject("Wood", typeof(Rigidbody), typeof(BoxCollider));
            var cube = go.AddComponent<Cube>();
            cube.Initialize(MakeMaterial(1, CubeRole.Normal));
            cube.HandleFellOff();
            Assert.AreEqual(2, _gm.Lives);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void DangerCube_HandleFellOff_NoPenalty()
        {
            var go = new GameObject("Spiked", typeof(Rigidbody), typeof(BoxCollider));
            var cube = go.AddComponent<Cube>();
            cube.Initialize(MakeMaterial(0, CubeRole.Danger));
            cube.HandleFellOff();
            Assert.AreEqual(3, _gm.Lives);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Cube_FellOffAfterSlice_NoExtraPenalty()
        {
            var go = new GameObject("Wood", typeof(Rigidbody), typeof(BoxCollider));
            var cube = go.AddComponent<Cube>();
            cube.Initialize(MakeMaterial(1, CubeRole.Normal));
            cube.HandleSlice(Vector3.zero);
            cube.HandleFellOff();
            Assert.AreEqual(3, _gm.Lives);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Initialize_AppliesMassAndScale()
        {
            var go = new GameObject("Heavy", typeof(Rigidbody), typeof(BoxCollider));
            var cube = go.AddComponent<Cube>();
            var mat = MakeMaterial(2, CubeRole.Bonus, mass: 3f);
            mat.displayScale = 1.4f;
            cube.Initialize(mat);
            Assert.AreEqual(3f, go.GetComponent<Rigidbody>().mass);
            Assert.That(go.transform.localScale.x, Is.EqualTo(1.4f).Within(0.001f));
            Object.DestroyImmediate(go);
        }
    }
}
