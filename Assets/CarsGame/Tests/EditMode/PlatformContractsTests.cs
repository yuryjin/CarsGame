using NUnit.Framework;
using RacingGame.Core.Platform;

namespace RacingGame.Tests
{
    public class PlatformContractsTests
    {
        [Test]
        public void ResultSuccessState()
        {
            var r = Result.Success;
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(ResultCategory.Ok, r.Category);
            Assert.IsFalse(r.Retryable);
            Assert.IsNull(r.Message);
            Assert.AreEqual("Ok", r.ToString());
        }

        [Test]
        public void ResultFailState()
        {
            var r = Result.Fail(ResultCategory.Timeout, "network timeout", true);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(ResultCategory.Timeout, r.Category);
            Assert.IsTrue(r.Retryable);
            Assert.AreEqual("network timeout", r.Message);
            Assert.AreEqual("Timeout: network timeout", r.ToString());
        }

        [Test]
        public void GenericResultSuccessAndFail()
        {
            var ok = Result<int>.Success(123);
            Assert.IsTrue(ok.Ok);
            Assert.AreEqual(123, ok.Value);
            Assert.AreEqual("Ok(123)", ok.ToString());

            var fail = Result<string>.Fail(ResultCategory.Unauthorized, "no token");
            Assert.IsFalse(fail.Ok);
            Assert.AreEqual(ResultCategory.Unauthorized, fail.Category);
            Assert.IsNull(fail.Value);
            Assert.AreEqual("Unauthorized: no token", fail.ToString());
        }

        [Test]
        public void PlayerInfoGuestDefaults()
        {
            var guest = PlayerInfo.Guest;
            Assert.AreEqual("guest", guest.Id);
            Assert.AreEqual("Guest", guest.Name);
            Assert.IsNull(guest.AvatarUrl);
            Assert.IsFalse(guest.IsAuthorized);
        }

        [Test]
        public void PlatformFeatureFlagsBitwise()
        {
            var flags = PlatformFeature.Ads | PlatformFeature.CloudSave;
            Assert.IsTrue(flags.HasFlag(PlatformFeature.Ads));
            Assert.IsTrue(flags.HasFlag(PlatformFeature.CloudSave));
            Assert.IsFalse(flags.HasFlag(PlatformFeature.Payments));
        }
    }
}
