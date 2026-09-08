using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// The XR display is reached from ONE place, XrDisplayLifecycle, and that place
    /// drives the XRDisplaySubsystem directly (start an idle display, stop a running
    /// one) exactly as v1.16.6 did — the version the maintainer validated on Unity
    /// 6000.6 with a working VR / desktop switch. Routing the start through XR
    /// Management (v1.18.1, StartSubsystems/StopSubsystems) made the switch unstable
    /// and is not allowed back without a runtime validation.
    /// </summary>
    public class XrDisplayLifecycleTests
    {
        private const string LifecycleFile = "XrDisplayLifecycle.cs";

        private static IEnumerable<(string path, int line, string text)> CodeLines()
        {
            foreach (var file in Directory.GetFiles(PackagePaths.Runtime, "*.cs", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var trimmed = lines[i].Trim();
                    if (trimmed.StartsWith("//")) continue;
                    yield return (file.Replace('\\', '/'), i + 1, lines[i]);
                }
            }
        }

        [Test]
        public void OnlyXrDisplayLifecycle_TouchesTheXrDisplaySubsystem()
        {
            var offenders = new List<string>();
            foreach (var (path, line, text) in CodeLines())
            {
                if (Path.GetFileName(path) == LifecycleFile) continue;
                if (text.Contains("XRDisplaySubsystem")) offenders.Add($"{path}:{line}: {text.Trim()}");
            }
            Assert.That(offenders, Is.Empty,
                "Runtime code must reach the XR display only through XrDisplayLifecycle (start/stop, running state, session focus):\n" +
                string.Join("\n", offenders));
        }

        [Test]
        public void NoRuntimeCode_RestartsTheXrLoader()
        {
            var offenders = new List<string>();
            foreach (var (path, line, text) in CodeLines())
            {
                if (text.Contains("StartSubsystems(") || text.Contains("StopSubsystems(")
                    || text.Contains("InitializeLoader") || text.Contains("DeinitializeLoader"))
                    offenders.Add($"{path}:{line}: {text.Trim()}");
            }
            Assert.That(offenders, Is.Empty,
                "The XR loader is started once by XR Management at launch and never restarted by the package; the display itself is driven through XrDisplayLifecycle:\n" +
                string.Join("\n", offenders));
        }

        [Test]
        public void XrDisplayLifecycle_DrivesTheDisplayDirectly()
        {
            var path = Path.Combine(PackagePaths.Runtime, "scripts/Hmd", LifecycleFile);
            Assert.That(File.Exists(path), Is.True, $"{LifecycleFile} moved — update this test.");
            var source = File.ReadAllText(path);
            Assert.That(source, Does.Contain("display.Start()"), "An idle display is started directly (v1.16.6 keeper warm-up).");
            Assert.That(source, Does.Contain("display.Stop()"), "A running display is stopped directly (Stop Xr Display On Desktop = Always / EditorOnly).");
            Assert.That(source, Does.Contain("DpadLayoutGuard.RepairIfNeeded()"), "A display start re-registers OpenXR layouts; the DPad gamepad layout must be repaired right after.");
        }
    }
}
