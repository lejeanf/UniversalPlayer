using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// FootstepSetupChecks: every footstep failure mode is SILENCE, so the validator
    /// must turn each one into a loud, named result — missing wiring, wired-but-no-
    /// sounds, and surface tags that do not exist in the project. Also pins the
    /// IValidatable contract the inspector banner relies on.
    /// </summary>
    public class FootstepSetupChecksTests
    {
        private GameObject _go;
        private FootstepAudio _footsteps;
        private AudioClip _clip;

        [SetUp]
        public void CreateRig()
        {
            _go = new GameObject("FootstepChecksRig");
            _footsteps = _go.AddComponent<FootstepAudio>();
            _clip = AudioClip.Create("TestClip", 441, 1, 44100, false);
        }

        [TearDown]
        public void DestroyRig()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_clip);
        }

        private void Wire()
        {
            SetField("movement", _go.AddComponent<PlayerMovement>());
            var source = new GameObject("Source").transform;
            source.SetParent(_go.transform, false);
            SetField("footstepSource", source.gameObject.AddComponent<AudioSource>());
        }

        private void SetField(string field, object value)
        {
            var info = typeof(FootstepAudio).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null,
                $"Field '{field}' not found on FootstepAudio — was it renamed? Update FootstepSetupChecksTests alongside the refactor.");
            info.SetValue(_footsteps, value);
        }

        private FootstepAudio.SurfaceProfile Profile(string tag, bool footsteps = false, bool scuffs = false)
        {
            return new FootstepAudio.SurfaceProfile
            {
                surfaceTag = tag,
                footsteps = footsteps ? _clip : null,
                scuffs = scuffs ? _clip : null,
            };
        }

        [Test]
        public void UnwiredComponent_Fails_NamingTheMissingFields()
        {
            var result = FootstepSetupChecks.CheckFootsteps(_footsteps);
            Assert.That(result.Severity, Is.EqualTo(SetupValidator.Severity.Fail));
            Assert.That(result.Message, Does.Contain("movement").And.Contain("footstepSource"),
                "The failure must name exactly which wiring is missing.");
        }

        [Test]
        public void WiredButNoSounds_Fails_AsSilent()
        {
            Wire();
            var result = FootstepSetupChecks.CheckFootsteps(_footsteps);
            Assert.That(result.Severity, Is.EqualTo(SetupValidator.Severity.Fail),
                "Wired-but-silent is the worst footstep failure mode (no error anywhere at runtime) — it must FAIL, not warn.");
            Assert.That(result.Message, Does.Contain("silent").IgnoreCase);
        }

        [Test]
        public void WiredWithADefaultSound_Passes()
        {
            Wire();
            SetField("defaultSurface", Profile(null, footsteps: true));
            var result = FootstepSetupChecks.CheckFootsteps(_footsteps);
            Assert.That(result.Severity, Is.EqualTo(SetupValidator.Severity.Pass), result.Message);
        }

        [Test]
        public void UndefinedSurfaceTag_Warns_AndNamesTheTag()
        {
            SetField("surfaces", new System.Collections.Generic.List<FootstepAudio.SurfaceProfile>
            {
                Profile("NoSuchTag_FootstepTest", footsteps: true),
            });
            var result = FootstepSetupChecks.CheckFootstepSurfaces(_footsteps);
            Assert.That(result.Severity, Is.EqualTo(SetupValidator.Severity.Warning));
            Assert.That(result.Message, Does.Contain("NoSuchTag_FootstepTest"),
                "The warning must name the undefined tag so the user knows what to add or rename.");
        }

        [Test]
        public void DefinedTags_WithScuffSounds_Pass()
        {
            // 'Untagged' always exists in every project's tag list.
            SetField("surfaces", new System.Collections.Generic.List<FootstepAudio.SurfaceProfile>
            {
                Profile("Untagged", footsteps: true, scuffs: true),
            });
            var result = FootstepSetupChecks.CheckFootstepSurfaces(_footsteps);
            Assert.That(result.Severity, Is.EqualTo(SetupValidator.Severity.Pass), result.Message);
        }

        [Test]
        public void NoScuffSoundsAnywhere_Warns_ThatTheFrictionLayerIsSilent()
        {
            SetField("surfaces", new System.Collections.Generic.List<FootstepAudio.SurfaceProfile>
            {
                Profile("Untagged", footsteps: true),
            });
            var result = FootstepSetupChecks.CheckFootstepSurfaces(_footsteps);
            Assert.That(result.Severity, Is.EqualTo(SetupValidator.Severity.Warning));
            Assert.That(result.Message, Does.Contain("friction").IgnoreCase);
        }

        [Test]
        public void ValidateSetup_IncludesTheFootstepChecks()
        {
            // Guards the hookup itself: a check that exists but is never run validates nothing.
            var names = FootstepSetupChecks.RunFootstepChecks().Select(r => r.Name).ToArray();
            Assert.That(names, Is.Not.Empty);
            Assert.That(names.All(n => n.StartsWith("Scene: footstep")), Is.True,
                $"Unexpected result names: {string.Join(", ", names)}");
        }

        [Test]
        public void IsValid_TracksWiringAndAudibility()
        {
            // The inspector banner (ValidationScanner / IValidatable) keys off this.
            Assert.That(_footsteps.IsValid, Is.False, "Unwired must be invalid.");
            Wire();
            Assert.That(_footsteps.IsValid, Is.False, "Wired but silent must be invalid.");
            SetField("defaultSurface", Profile(null, footsteps: true));
            Assert.That(_footsteps.IsValid, Is.True, "Wired and audible must be valid.");
        }
    }
}
