using NUnit.Framework;
using UnityEngine;

namespace OpenNinja.Tests
{
    public class GameManagerTests
    {
        private GameObject _go;
        private GameManager _gm;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("GameManager");
            _gm = _go.AddComponent<GameManager>();
            _gm.ConfigureForTests(comboWindowSeconds: 0.5f, startingLives: 3, maxComboMultiplier: 8);
            _gm.ResetGame();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Time.timeScale = 1f;
        }

        [Test]
        public void FirstHit_AwardsBasePointsAtMultiplierOne()
        {
            _gm.RegisterHit(1, Vector3.zero);
            Assert.AreEqual(1, _gm.Score);
        }

        [Test]
        public void SecondHitWithinWindow_AwardsBasePointsTimesTwo()
        {
            _gm.RegisterHit(1, Vector3.zero);
            _gm.Tick(0.1f);
            _gm.RegisterHit(1, Vector3.zero);
            Assert.AreEqual(3, _gm.Score);
        }

        [Test]
        public void ThirdHitWithinWindow_AwardsBasePointsTimesThree()
        {
            _gm.RegisterHit(1, Vector3.zero);
            _gm.Tick(0.1f);
            _gm.RegisterHit(1, Vector3.zero);
            _gm.Tick(0.1f);
            _gm.RegisterHit(1, Vector3.zero);
            Assert.AreEqual(6, _gm.Score);
        }

        [Test]
        public void RedCube_AwardsTwoBasePoints()
        {
            _gm.RegisterHit(2, Vector3.zero);
            Assert.AreEqual(2, _gm.Score);
        }

        [Test]
        public void ComboTimer_ExpiresAfterWindow_ResetsMultiplier()
        {
            _gm.RegisterHit(1, Vector3.zero);
            Assert.AreEqual(2, _gm.ComboMultiplier);
            _gm.Tick(0.6f);
            Assert.AreEqual(1, _gm.ComboMultiplier);
        }

        [Test]
        public void ComboMultiplier_CapsAtMaxConfigured()
        {
            for (int i = 0; i < 20; i++)
            {
                _gm.RegisterHit(1, Vector3.zero);
                _gm.Tick(0.1f);
            }
            Assert.AreEqual(8, _gm.ComboMultiplier);
        }

        [Test]
        public void RegisterDangerClick_DeductsLifeAndResetsComboAndFiresEvents()
        {
            int livesEventValue = -1;
            _gm.OnLivesChanged += v => livesEventValue = v;
            _gm.RegisterHit(1, Vector3.zero);
            _gm.RegisterDangerClick(Vector3.zero);
            Assert.AreEqual(2, _gm.Lives);
            Assert.AreEqual(1, _gm.ComboMultiplier);
            Assert.AreEqual(2, livesEventValue);
        }

        [Test]
        public void RegisterMiss_DeductsLifeAndResetsCombo()
        {
            _gm.RegisterHit(1, Vector3.zero);
            _gm.RegisterMiss();
            Assert.AreEqual(2, _gm.Lives);
            Assert.AreEqual(1, _gm.ComboMultiplier);
        }

        [Test]
        public void GameOver_FiresAfterLivesReachZero()
        {
            int finalScoreReported = -1;
            _gm.OnGameOver += s => finalScoreReported = s;
            _gm.RegisterHit(1, Vector3.zero);
            _gm.RegisterMiss();
            _gm.RegisterMiss();
            _gm.RegisterMiss();
            Assert.IsTrue(_gm.IsGameOver);
            Assert.AreEqual(1, finalScoreReported);
        }

        [Test]
        public void ResetGame_RestoresInitialState()
        {
            _gm.RegisterHit(1, Vector3.zero);
            _gm.RegisterMiss();
            _gm.ResetGame();
            Assert.AreEqual(0, _gm.Score);
            Assert.AreEqual(3, _gm.Lives);
            Assert.AreEqual(1, _gm.ComboMultiplier);
            Assert.IsFalse(_gm.IsGameOver);
        }

        [Test]
        public void OnHit_ReportsAwardedPointsAndMultiplierUsed()
        {
            int reportedPoints = 0;
            int reportedMult = 0;
            _gm.OnHit += (pts, mult, _) => { reportedPoints = pts; reportedMult = mult; };
            _gm.RegisterHit(1, Vector3.zero);
            Assert.AreEqual(1, reportedPoints);
            Assert.AreEqual(1, reportedMult);

            _gm.RegisterHit(2, Vector3.zero);
            Assert.AreEqual(4, reportedPoints);
            Assert.AreEqual(2, reportedMult);
        }

        [Test]
        public void RegisterHit_AfterGameOver_IsNoOp()
        {
            _gm.RegisterMiss();
            _gm.RegisterMiss();
            _gm.RegisterMiss();
            Assert.IsTrue(_gm.IsGameOver);

            int scoreBefore = _gm.Score;
            _gm.RegisterHit(1, Vector3.zero);
            Assert.AreEqual(scoreBefore, _gm.Score);
        }
    }
}
