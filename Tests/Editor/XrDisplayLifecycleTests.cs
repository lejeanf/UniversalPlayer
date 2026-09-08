using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// The XR display is started and stopped ONLY by the XR loader (XR Management), so a
    /// display start is always paired with the OpenXR session begin. Starting the
    /// XRDisplaySubsystem directly from package code produced a "running" display with
    /// no session behind it on Unity 6000.6 / OpenXR 1.18: black headset while tracking
    /// worked, stereo eye texture leaking into the flat Game view, no re-entry.
    /// </summary>
    public class XrDisplayLifecycleTests
    {
        private const string LifecycleFile = "XrDisplayLifecycle.cs";
        private static readonly Regex DirectStartOrStop = new Regex(@"\.(Start|Stop)\s*\(\s*\)", RegexOptions.Compiled);

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
                "Runtime code must reach the XR display only through XrDisplayLifecycle (loader-routed start/stop, mirror mode, running state):\n" +
                string.Join("\n", offenders));
        }

        [Test]
        public void XrDisplayLifecycle_NeverStartsOrStopsTheDisplayDirectly()
        {
            var offenders = new List<string>();
            foreach (var (path, line, text) in CodeLines())
            {
                if (Path.GetFileName(path) != LifecycleFile) continue;
                if (DirectStartOrStop.IsMatch(text)) offenders.Add($"{path}:{line}: {text.Trim()}");
            }
            Assert.That(offenders, Is.Empty,
                "XrDisplayLifecycle must route every start/stop through XRManagerSettings.StartSubsystems/StopSubsystems, never XRDisplaySubsystem.Start()/Stop():\n" +
                string.Join("\n", offenders));
        }

        [Test]
        public void XrDisplayLifecycle_RoutesStartAndStopThroughXrManagement()
        {
            var path = Path.Combine(PackagePaths.Runtime, "scripts/Hmd", LifecycleFile);
            Assert.That(File.Exists(path), Is.True, $"{LifecycleFile} moved — update this test.");
            var source = File.ReadAllText(path);
            Assert.That(source, Does.Contain("StartSubsystems()"), "Display start must go through XRManagerSettings.StartSubsystems().");
            Assert.That(source, Does.Contain("StopSubsystems()"), "Display stop must go through XRManagerSettings.StopSubsystems().");
        }
    }
}
