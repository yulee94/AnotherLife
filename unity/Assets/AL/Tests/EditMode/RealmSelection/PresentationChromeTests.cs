using AL.UI.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.RealmSelection
{
    public sealed class PresentationChromeTests
    {
        [Test]
        public void SharedTokensStayTouchableAndNeutral()
        {
            Assert.That(PresentationChrome.MinHit, Is.GreaterThanOrEqualTo(48f));
            Assert.That(PresentationChrome.DisplaySize, Is.EqualTo(40));
            Assert.That(PresentationChrome.TitleSize, Is.EqualTo(26));
            Assert.That(PresentationChrome.ActionSize, Is.EqualTo(16));
            Assert.That(PresentationChrome.StoneVoid.grayscale, Is.LessThan(0.12f));
            Assert.That(
                Mathf.Abs(PresentationChrome.MetalEdge.r - PresentationChrome.MetalEdge.b),
                Is.LessThan(0.2f));
        }

        [Test]
        public void PresentationFontIsNotLegacyRuntime()
        {
            Font font = PresentationChrome.ResolveFont();
            Assert.That(font, Is.Not.Null);
            Assert.That(font.name, Does.Not.Contain("LegacyRuntime"));
        }
    }
}
