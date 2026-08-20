using AL.Data.Runtime;
using AL.UI.CharacterCreation;
using NUnit.Framework;

namespace AL.Tests.EditMode.CharacterCreation
{
    public sealed class CharacterCreationIdentityTests
    {
        [SetUp]
        public void SetUp()
        {
            CharacterCreationIdentity.ResetClaims();
            SliceRunState.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            CharacterCreationIdentity.ResetClaims();
            SliceRunState.Reset();
        }

        [Test]
        public void RejectsBlankUsername()
        {
            Assert.IsFalse(CharacterCreationIdentity.TryNormalize("   ", out _, out string error));
            Assert.That(error, Does.Contain("Enter a username"));
        }

        [Test]
        public void RejectsTooShortAndTooLong()
        {
            Assert.IsFalse(CharacterCreationIdentity.TryNormalize("ab", out _, out _));
            Assert.IsFalse(CharacterCreationIdentity.TryNormalize(new string('a', 17), out _, out _));
        }

        [Test]
        public void RejectsInvalidCharacters()
        {
            Assert.IsFalse(CharacterCreationIdentity.TryNormalize("bad name", out _, out _));
            Assert.IsFalse(CharacterCreationIdentity.TryNormalize("bad-name", out _, out _));
        }

        [Test]
        public void AcceptsLegalUsername()
        {
            Assert.IsTrue(CharacterCreationIdentity.TryNormalize("Lord_01", out string normalized, out string error));
            Assert.AreEqual("Lord_01", normalized);
            Assert.IsEmpty(error);
        }

        [Test]
        public void RejectsInProcessDuplicate()
        {
            Assert.IsTrue(CharacterCreationIdentity.TryClaim("Aelthra", string.Empty, out _, out _));
            Assert.IsFalse(CharacterCreationIdentity.TryClaim("aelthra", string.Empty, out _, out string error));
            Assert.That(error, Does.Contain("already taken"));
        }

        [Test]
        public void AllowsReclaimOfOwnUsername()
        {
            Assert.IsTrue(CharacterCreationIdentity.TryClaim("Stonewarden", string.Empty, out string first, out _));
            Assert.IsTrue(CharacterCreationIdentity.TryClaim("Stonewarden", first, out string second, out string error));
            Assert.AreEqual(first, second);
            Assert.IsEmpty(error);
        }

        [Test]
        public void PersistedUsernameBlocksLocalDuplicate()
        {
            CharacterCreationIdentity.RememberPersisted("Banner_01");
            Assert.IsFalse(
                CharacterCreationIdentity.TryClaim("banner_01", string.Empty, out _, out string error));
            Assert.That(error, Does.Contain("already taken"));
        }
    }
}
