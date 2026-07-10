using NUnit.Framework;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Placeholder keeping the play-mode test assembly compiling until the
    /// feature tests land (FadeMask, NoPeeking, control scheme, hands, finger ray).
    /// </summary>
    public class PlayModeSmokeTests
    {
        [Test]
        public void TestAssembly_ReferencesRuntimeAssembly()
        {
            Assert.That(typeof(FadeMask), Is.Not.Null,
                "jeanf.universalplayer runtime assembly is not reachable from the test assembly — check asmdef references.");
        }
    }
}
