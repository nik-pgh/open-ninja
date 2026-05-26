using NUnit.Framework;
using UnityEngine;

namespace OpenNinja.Tests
{
    public class CubeTests
    {
        private GameObject _gmGO;
        private GameManager _gm;

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
        }

        [Test]
        public void GreenCube_HandleSlice_RegistersHit()
        {
            var go = new GameObject("Green");
            var cube = go.AddComponent<Cube>();
            cube.ConfigureForTests(CubeType.Green);
            cube.HandleSlice(Vector3.zero);
            Assert.AreEqual(1, _gm.Score);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void BlackCube_HandleSlice_DeductsLife()
        {
            var go = new GameObject("Black");
            var cube = go.AddComponent<Cube>();
            cube.ConfigureForTests(CubeType.Black);
            cube.HandleSlice(Vector3.zero);
            Assert.AreEqual(2, _gm.Lives);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Cube_DoubleSlice_OnlyAwardsOnce()
        {
            var go = new GameObject("Green");
            var cube = go.AddComponent<Cube>();
            cube.ConfigureForTests(CubeType.Green);
            cube.HandleSlice(Vector3.zero);
            cube.HandleSlice(Vector3.zero); // second call: already consumed
            Assert.AreEqual(1, _gm.Score);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void GreenCube_HandleFellOff_RegistersMiss()
        {
            var go = new GameObject("Green");
            var cube = go.AddComponent<Cube>();
            cube.ConfigureForTests(CubeType.Green);
            cube.HandleFellOff();
            Assert.AreEqual(2, _gm.Lives);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void BlackCube_HandleFellOff_NoPenalty()
        {
            var go = new GameObject("Black");
            var cube = go.AddComponent<Cube>();
            cube.ConfigureForTests(CubeType.Black);
            cube.HandleFellOff();
            Assert.AreEqual(3, _gm.Lives);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Cube_FellOffAfterSlice_NoExtraPenalty()
        {
            var go = new GameObject("Green");
            var cube = go.AddComponent<Cube>();
            cube.ConfigureForTests(CubeType.Green);
            cube.HandleSlice(Vector3.zero);
            cube.HandleFellOff();
            Assert.AreEqual(3, _gm.Lives); // no double-count
            Object.DestroyImmediate(go);
        }
    }
}
