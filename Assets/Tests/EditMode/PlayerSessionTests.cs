using NUnit.Framework;
using UnityEngine;

namespace OpenNinja.Tests
{
    public class PlayerSessionTests
    {
        private const string NicknameKey  = "OpenNinja.Nickname";
        private const string BestScoreKey = "OpenNinja.BestScore";

        [SetUp]
        public void Setup()
        {
            PlayerPrefs.DeleteKey(NicknameKey);
            PlayerPrefs.DeleteKey(BestScoreKey);
        }

        [TearDown]
        public void Teardown()
        {
            PlayerPrefs.DeleteKey(NicknameKey);
            PlayerPrefs.DeleteKey(BestScoreKey);
        }

        [Test]
        public void Nickname_DefaultsToEmptyString()
        {
            Assert.AreEqual("", PlayerSession.Nickname);
        }

        [Test]
        public void Nickname_RoundtripsThroughPlayerPrefs()
        {
            PlayerSession.Nickname = "Alice";
            Assert.AreEqual("Alice", PlayerSession.Nickname);
        }

        [Test]
        public void Nickname_TrimsWhitespace()
        {
            PlayerSession.Nickname = "   Bob   ";
            Assert.AreEqual("Bob", PlayerSession.Nickname);
        }

        [Test]
        public void Nickname_TruncatesToMaxChars()
        {
            // 16 char limit
            PlayerSession.Nickname = "abcdefghijklmnopqrstuvwxyz";
            Assert.AreEqual("abcdefghijklmnop", PlayerSession.Nickname);
            Assert.AreEqual(16, PlayerSession.Nickname.Length);
        }

        [Test]
        public void Nickname_NullBecomesEmpty()
        {
            PlayerSession.Nickname = null;
            Assert.AreEqual("", PlayerSession.Nickname);
        }

        [Test]
        public void BestScore_DefaultsToZero()
        {
            Assert.AreEqual(0, PlayerSession.BestScore);
        }

        [Test]
        public void BestScore_RoundtripsThroughPlayerPrefs()
        {
            PlayerSession.BestScore = 42;
            Assert.AreEqual(42, PlayerSession.BestScore);
        }

        [Test]
        public void BestScore_NegativeClampsToZero()
        {
            PlayerSession.BestScore = -5;
            Assert.AreEqual(0, PlayerSession.BestScore);
        }

        [Test]
        public void TrySetBestScore_HigherValue_UpdatesAndReturnsTrue()
        {
            PlayerSession.BestScore = 10;
            bool wasNewBest = PlayerSession.TrySetBestScore(20);
            Assert.IsTrue(wasNewBest);
            Assert.AreEqual(20, PlayerSession.BestScore);
        }

        [Test]
        public void TrySetBestScore_EqualValue_DoesNotUpdateAndReturnsFalse()
        {
            PlayerSession.BestScore = 10;
            bool wasNewBest = PlayerSession.TrySetBestScore(10);
            Assert.IsFalse(wasNewBest);
            Assert.AreEqual(10, PlayerSession.BestScore);
        }

        [Test]
        public void TrySetBestScore_LowerValue_DoesNotUpdateAndReturnsFalse()
        {
            PlayerSession.BestScore = 10;
            bool wasNewBest = PlayerSession.TrySetBestScore(5);
            Assert.IsFalse(wasNewBest);
            Assert.AreEqual(10, PlayerSession.BestScore);
        }

        [Test]
        public void TrySetBestScore_FiresEventOnUpdate()
        {
            int eventValue = -1;
            System.Action<int> handler = v => eventValue = v;
            PlayerSession.OnBestScoreChanged += handler;
            try
            {
                PlayerSession.TrySetBestScore(99);
                Assert.AreEqual(99, eventValue);
            }
            finally
            {
                PlayerSession.OnBestScoreChanged -= handler;
            }
        }

        [Test]
        public void TrySetBestScore_DoesNotFireEventWhenNoUpdate()
        {
            PlayerSession.BestScore = 50;
            int eventCallCount = 0;
            System.Action<int> handler = _ => eventCallCount++;
            PlayerSession.OnBestScoreChanged += handler;
            try
            {
                PlayerSession.TrySetBestScore(30);
                Assert.AreEqual(0, eventCallCount);
            }
            finally
            {
                PlayerSession.OnBestScoreChanged -= handler;
            }
        }
    }
}
