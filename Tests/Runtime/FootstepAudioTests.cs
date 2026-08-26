using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// FootstepAudio: distance-based steps while actually walking (silent when still,
    /// silent in the air), surface profile resolution by ground tag, and the friction
    /// scuff on an abrupt direction reversal at speed.
    /// </summary>
    public class FootstepAudioTests
    {
        private GameObject _floor;
        private GameObject _player;
        private CharacterController _controller;
        private PlayerMovement _movement;
        private FootstepAudio _footsteps;
        private AudioSource _stepSource;
        private AudioSource _scuffSource;
        private AudioClip _stepClip;
        private AudioClip _scuffClip;
        private AudioClip _jumpClip;
        private AudioClip _landClip;
        private AudioClip _crouchClip;
        private AudioClip _standClip;
        private AudioClip _sneakClip;
        private FootstepAudio.SurfaceProfile _defaultSurface;
        private bool _prevIgnoreDefaultCollision;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _prevIgnoreDefaultCollision = Physics.GetIgnoreLayerCollision(0, 0);
            Physics.IgnoreLayerCollision(0, 0, false);
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;

            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.name = "Floor";
            _floor.transform.localScale = new Vector3(40f, 1f, 40f);
            _floor.transform.position = new Vector3(0f, -0.5f, 0f);

            // Long silent clips so isPlaying stays observable across a few frames.
            _stepClip = AudioClip.Create("TestStep", 44100, 1, 44100, false);
            _scuffClip = AudioClip.Create("TestScuff", 44100, 1, 44100, false);
            _jumpClip = AudioClip.Create("TestJump", 44100, 1, 44100, false);
            _landClip = AudioClip.Create("TestLand", 44100, 1, 44100, false);
            _crouchClip = AudioClip.Create("TestCrouch", 44100, 1, 44100, false);
            _standClip = AudioClip.Create("TestStand", 44100, 1, 44100, false);
            _sneakClip = AudioClip.Create("TestSneak", 44100, 1, 44100, false);

            _player = new GameObject("Player");
            _player.SetActive(false);
            _player.transform.position = new Vector3(0f, 1.1f, 0f);
            _controller = _player.AddComponent<CharacterController>();
            _movement = _player.AddComponent<PlayerMovement>();
            SetField(_movement, "controller", _controller);
            SetField(_movement, "speed", 4f);
            SetField(_movement, "speedChangeRate", 8f); // packaged default — the scuff threshold is calibrated against it

            var stepChild = new GameObject("FootstepSource");
            stepChild.transform.SetParent(_player.transform, false);
            _stepSource = stepChild.AddComponent<AudioSource>();
            _stepSource.playOnAwake = false;
            var scuffChild = new GameObject("ScuffSource");
            scuffChild.transform.SetParent(_player.transform, false);
            _scuffSource = scuffChild.AddComponent<AudioSource>();
            _scuffSource.playOnAwake = false;

            _footsteps = _player.AddComponent<FootstepAudio>();
            SetField(_footsteps, "movement", _movement);
            SetField(_footsteps, "footstepSource", _stepSource);
            SetField(_footsteps, "scuffSource", _scuffSource);
            _defaultSurface = new FootstepAudio.SurfaceProfile { footsteps = _stepClip, scuffs = _scuffClip };
            SetField(_footsteps, "defaultSurface", _defaultSurface);
            SetField(_footsteps, "jumpSound", _jumpClip);
            SetField(_footsteps, "crouchDownSound", _crouchClip);
            SetField(_footsteps, "standUpSound", _standClip);

            _player.SetActive(true);

            // Let gravity settle the controller onto the floor.
            for (var i = 0; i < 10; i++) yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Physics.IgnoreLayerCollision(0, 0, _prevIgnoreDefaultCollision);
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;
            Object.Destroy(_player);
            Object.Destroy(_floor);
            Object.Destroy(_stepClip);
            Object.Destroy(_scuffClip);
            Object.Destroy(_jumpClip);
            Object.Destroy(_landClip);
            Object.Destroy(_crouchClip);
            Object.Destroy(_standClip);
            Object.Destroy(_sneakClip);
            yield return null;
        }

        private static void SetField(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null,
                $"Field '{field}' not found on {target.GetType().Name} — was it renamed? Update FootstepAudioTests alongside the refactor.");
            info.SetValue(target, value);
        }

        private void StartMoving(Vector2 input)
        {
            _movement.SetMoveValue(input);
            _movement.SetIsMoving(true);
        }

        private void StopMoving()
        {
            _movement.SetMoveValue(Vector2.zero);
            _movement.SetIsMoving(false);
        }

        [UnityTest]
        public IEnumerator Footsteps_PlayWhileWalking_AndNotWhileStanding()
        {
            // Standing: a second of stillness must produce no step.
            yield return new WaitForSeconds(1f);
            Assert.That(_stepSource.isPlaying, Is.False, "A footstep played while standing still.");

            // Walking at 4 m/s covers the 0.75 m stride many times over in 1.5 s.
            StartMoving(Vector2.up);
            var stepped = false;
            var deadline = Time.time + 1.5f;
            while (Time.time < deadline && !stepped)
            {
                if (_stepSource.isPlaying) stepped = true;
                yield return null;
            }
            Assert.That(stepped, Is.True,
                "No footstep played after 1.5 s of walking — the stride accumulator or the resource assignment is broken.");
            Assert.That(_stepSource.resource, Is.EqualTo(_stepClip),
                "The footstep source is not playing the default surface's footstep resource.");

            StopMoving();
            yield return new WaitForSeconds(1.5f); // outlives the momentum bleed + the 1 s test clip
            _stepSource.Stop();
            yield return new WaitForSeconds(0.5f);
            Assert.That(_stepSource.isPlaying, Is.False, "Footsteps kept playing after the player stopped.");
        }

        [UnityTest]
        public IEnumerator Scuff_PlaysOnHardReversal_NotOnGentleWalk()
        {
            // Reach full walk speed — no scuff on a clean start/steady walk.
            StartMoving(Vector2.up);
            yield return new WaitForSeconds(1.5f);
            Assert.That(_scuffSource.isPlaying, Is.False,
                "A scuff played during a steady walk — the along-travel acceleration filter is broken.");

            // Yank the stick the other way: commanded velocity fights travel at speedChangeRate.
            StartMoving(Vector2.down);
            var scuffed = false;
            var deadline = Time.time + 0.5f;
            while (Time.time < deadline && !scuffed)
            {
                if (_scuffSource.isPlaying) scuffed = true;
                yield return null;
            }
            Assert.That(scuffed, Is.True,
                "No scuff played on a full-speed direction reversal — the friction detector is not firing.");
            StopMoving();
        }

        [UnityTest]
        public IEnumerator Scuff_IsSuppressedInXr()
        {
            // XRI locomotion snaps velocity — every VR stick reversal would squeak, so the
            // scuff layer must stay desktop-only.
            StartMoving(Vector2.up);
            yield return new WaitForSeconds(1.5f);
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.XR;
            StartMoving(Vector2.down);
            yield return new WaitForSeconds(0.5f);
            Assert.That(_scuffSource.isPlaying, Is.False, "A scuff played in XR mode.");
            StopMoving();
        }

        [UnityTest]
        public IEnumerator Jump_PlaysTheJumpSound()
        {
            _movement.RequestJump();
            var played = false;
            var deadline = Time.time + 0.5f;
            while (Time.time < deadline && !played)
            {
                if (_scuffSource.isPlaying && ReferenceEquals(_scuffSource.resource, _jumpClip)) played = true;
                yield return null;
            }
            Assert.That(played, Is.True,
                "The jump sound did not play on takeoff — is PlayerEvents.PlayerJumped raised and subscribed?");
        }

        [UnityTest]
        public IEnumerator Landing_PlaysTheSurfaceLandingSound()
        {
            _defaultSurface.landings = _landClip;

            // Drop from 3 m: touchdown at ~7.7 m/s, well past the 3 m/s landing threshold.
            _controller.enabled = false;
            _player.transform.position += Vector3.up * 3f;
            _controller.enabled = true;

            var played = false;
            var deadline = Time.time + 3f;
            while (Time.time < deadline && !played)
            {
                if (_stepSource.isPlaying && ReferenceEquals(_stepSource.resource, _landClip)) played = true;
                yield return null;
            }
            Assert.That(played, Is.True,
                "No landing sound after a 3 m fall — is PlayerEvents.PlayerLanded raised with the impact speed?");
        }

        [UnityTest]
        public IEnumerator CrouchTransitions_PlayCrouchAndStandSounds()
        {
            _movement.SetCrouchHeld(true); // toggle mode: crouch down
            var deadline = Time.time + 2f;
            while (Time.time < deadline && !_movement.IsCrouched) yield return null;
            Assert.That(_movement.IsCrouched, Is.True, "The crouch never engaged — cannot test its sound.");
            Assert.That(ReferenceEquals(_scuffSource.resource, _crouchClip), Is.True,
                "Crossing into the crouch did not play the crouch-down sound.");

            _movement.SetCrouchHeld(true); // toggle mode: stand back up
            deadline = Time.time + 2f;
            while (Time.time < deadline && _movement.IsCrouched) yield return null;
            Assert.That(_movement.IsCrouched, Is.False, "The player never stood back up.");
            Assert.That(ReferenceEquals(_scuffSource.resource, _standClip), Is.True,
                "Standing back up did not play the stand-up sound.");
        }

        [UnityTest]
        public IEnumerator CrouchedSteps_UseTheSneakSet_WhenAuthored()
        {
            _defaultSurface.crouchFootsteps = _sneakClip;
            _movement.SetCrouchHeld(true);
            var deadline = Time.time + 2f;
            while (Time.time < deadline && !_movement.IsCrouched) yield return null;

            StartMoving(Vector2.up);
            var sneaked = false;
            deadline = Time.time + 2f; // crouch speed is halved — give the stride time
            while (Time.time < deadline && !sneaked)
            {
                if (_stepSource.isPlaying && ReferenceEquals(_stepSource.resource, _sneakClip)) sneaked = true;
                yield return null;
            }
            Assert.That(sneaked, Is.True,
                "Crouched walking never used the dedicated crouch footstep set.");
            StopMoving();
        }

        [Test]
        public void SurfaceProfiles_MatchByTag_AndFallBackToDefault()
        {
            var concrete = new FootstepAudio.SurfaceProfile { surfaceTag = "Concrete", footsteps = _stepClip };
            var surfaces = new System.Collections.Generic.List<FootstepAudio.SurfaceProfile> { concrete };
            SetField(_footsteps, "surfaces", surfaces);

            var find = typeof(FootstepAudio).GetMethod("FindProfile", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(find, Is.Not.Null, "FindProfile no longer exists on FootstepAudio — update this test alongside the refactor.");

            Assert.That(find.Invoke(_footsteps, new object[] { "Concrete" }), Is.SameAs(concrete),
                "A profile whose tag matches the ground tag must win.");
            var fallback = typeof(FootstepAudio).GetField("defaultSurface", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_footsteps);
            Assert.That(find.Invoke(_footsteps, new object[] { "Untagged" }), Is.SameAs(fallback),
                "An unknown ground tag must fall back to the default surface profile.");
        }
    }
}
