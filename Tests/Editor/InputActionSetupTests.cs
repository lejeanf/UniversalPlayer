using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace jeanf.universalplayer.tests.editor
{
    /// <summary>
    /// A project that runs its Player on a local copy of the package input actions,
    /// or that references actions its InputActionManager never enables, gets hands
    /// that pose but never move. These checks name the missing pieces and their
    /// fixes add them.
    /// </summary>
    public class InputActionSetupTests
    {
        private InputActionAsset _reference;
        private InputActionAsset _local;
        private GameObject _rig;

        [SetUp]
        public void SetUp()
        {
            _reference = ScriptableObject.CreateInstance<InputActionAsset>();
            _reference.name = "Reference";
            _reference.AddControlScheme("XR").WithRequiredDevice("<XRHMD>");
            _reference.AddControlScheme("Keyboard&Mouse").WithRequiredDevice("<Keyboard>");
            var hand = _reference.AddActionMap("XRI LeftHand");
            var position = hand.AddAction("Position", InputActionType.Value, expectedControlLayout: "Vector3");
            position.AddCompositeBinding("Vector3Fallback")
                .With("first", "<XRController>{LeftHand}/pointerPosition", "XR")
                .With("second", "<XRController>{LeftHand}/devicePosition", "XR");
            hand.AddAction("Select", InputActionType.Button).AddBinding("<XRController>{LeftHand}/gripPressed", groups: "XR");
            var fps = _reference.AddActionMap("FPS");
            fps.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2").AddBinding("<Gamepad>/leftStick", groups: "Gamepad");

            _local = ScriptableObject.CreateInstance<InputActionAsset>();
            _local.name = "Local";
            _local.AddControlScheme("Keyboard&Mouse").WithRequiredDevice("<Keyboard>");
            _local.AddActionMap("FPS").AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2").AddBinding("<Gamepad>/leftStick", groups: "Gamepad");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_reference);
            Object.DestroyImmediate(_local);
            if (_rig != null) Object.DestroyImmediate(_rig);
        }

        [Test]
        public void Compare_NamesEveryMissingMapActionBindingAndScheme()
        {
            var report = InputActionAssetChecks.Compare(_local, _reference);

            Assert.That(report.IsComplete, Is.False);
            Assert.That(report.LacksActions, Is.True);
            Assert.That(report.MissingMaps, Is.EqualTo(new[] { "XRI LeftHand" }));
            Assert.That(report.MissingSchemes, Is.EqualTo(new[] { "XR" }));
            Assert.That(report.MissingActions, Is.Empty, "Actions of a missing map are reported once, as the map.");
        }

        [Test]
        public void Compare_ReportsAMissingCompositeAndAMissingPlainBinding()
        {
            var hand = _local.AddActionMap("XRI LeftHand");
            hand.AddAction("Position", InputActionType.Value, expectedControlLayout: "Vector3").AddBinding("<XRController>{LeftHand}/devicePosition");
            hand.AddAction("Select", InputActionType.Button);

            var report = InputActionAssetChecks.Compare(_local, _reference);

            Assert.That(report.MissingMaps, Is.Empty);
            Assert.That(report.MissingActions, Is.Empty);
            Assert.That(report.MissingBindings.Any(b => b.StartsWith("XRI LeftHand/Position:") && b.Contains("Vector3Fallback")), Is.True,
                "The Vector3Fallback composite (pointer position with device position fallback) is what drives the tracked pose driver.");
            Assert.That(report.MissingBindings.Any(b => b.StartsWith("XRI LeftHand/Select:") && b.Contains("gripPressed")), Is.True);
            Assert.That(report.LacksActions, Is.False, "Missing bindings alone are a warning, not a failure.");
        }

        [Test]
        public void AddMissing_CompletesTheLocalAsset_WithoutDuplicates()
        {
            var added = InputActionAssetChecks.AddMissing(_local, _reference);
            Assert.That(added, Is.GreaterThan(0));

            var report = InputActionAssetChecks.Compare(_local, _reference);
            Assert.That(report.IsComplete, Is.True, string.Join(" | ", report.Summary()));

            Assert.That(InputActionAssetChecks.AddMissing(_local, _reference), Is.EqualTo(0),
                "A second repair must find nothing to add.");
            Assert.That(_local.FindActionMap("FPS").actions.Count, Is.EqualTo(1));
            Assert.That(_local.FindActionMap("FPS").FindAction("Move").bindings.Count, Is.EqualTo(1),
                "An existing binding must never be duplicated by the repair.");
            var position = _local.FindActionMap("XRI LeftHand").FindAction("Position");
            Assert.That(position.bindings.Count, Is.EqualTo(3), "One composite header plus its two parts.");
            Assert.That(position.bindings[0].isComposite, Is.True);
            Assert.That(position.bindings[2].path, Is.EqualTo("<XRController>{LeftHand}/devicePosition"));
            Assert.That(_local.controlSchemes.Select(s => s.name), Does.Contain("XR"));
        }

        [Test]
        public void Wiring_FailsWhenAReferencedAssetIsNotEnabled_AndTheFixAddsIt()
        {
            _rig = new GameObject("Rig");
            _rig.AddComponent<PlayerInput>().actions = _local;
            var manager = new GameObject("InputActionManager").AddComponent<InputActionManager>();
            manager.transform.SetParent(_rig.transform);
            manager.actionAssets = new System.Collections.Generic.List<InputActionAsset> { _local };
            var controller = new GameObject("Left Controller");
            controller.transform.SetParent(_rig.transform);
            var driver = controller.AddComponent<TrackedPoseDriver>();
            var position = _reference.FindActionMap("XRI LeftHand").FindAction("Position");
            driver.positionInput = new InputActionProperty(InputActionReference.Create(position));

            var before = InputActionAssetChecks.CheckInputActionWiring(_rig);
            Assert.That(before.Severity, Is.EqualTo(SetupValidator.Severity.Fail));
            Assert.That(before.Message, Does.Contain("Reference"));

            Assert.That(InputActionAssetChecks.WireInputActionAssets(_rig), Is.EqualTo(1));
            Assert.That(InputActionAssetChecks.EnabledActionAssets(manager), Does.Contain(_reference));
            Assert.That(InputActionAssetChecks.CheckInputActionWiring(_rig).Severity, Is.EqualTo(SetupValidator.Severity.Pass));
        }

        [Test]
        public void Wiring_FailsWithoutAnInputActionManager()
        {
            _rig = new GameObject("Rig");
            _rig.AddComponent<PlayerInput>().actions = _local;

            var result = InputActionAssetChecks.CheckInputActionWiring(_rig);
            Assert.That(result.Severity, Is.EqualTo(SetupValidator.Severity.Fail));
            Assert.That(result.Message, Does.Contain("InputActionManager"));
        }
    }

    public class TrackedControllerChecksTests
    {
        private GameObject _rig;
        private GameObject _controller;
        private TrackedPoseDriver _driver;

        [SetUp]
        public void SetUp()
        {
            _rig = new GameObject("Rig");
            var hands = new GameObject("Hands");
            hands.transform.SetParent(_rig.transform);
            _controller = new GameObject("Right Controller");
            _controller.transform.SetParent(hands.transform);
            _driver = _controller.AddComponent<TrackedPoseDriver>();
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = asset.AddActionMap("XRI RightHand");
            _driver.positionInput = new InputActionProperty(InputActionReference.Create(map.AddAction("Position", InputActionType.Value)));
            _driver.rotationInput = new InputActionProperty(InputActionReference.Create(map.AddAction("Rotation", InputActionType.Value)));
            var physics = new GameObject("RightHandPhysics");
            physics.transform.SetParent(_rig.transform);
            var hand = physics.AddComponent<HandsPhysics>();
            var serialized = new UnityEditor.SerializedObject(hand);
            serialized.FindProperty("target").objectReferenceValue = _controller.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_rig);

        [Test]
        public void ActiveControllerWithEnabledDriver_Passes()
        {
            Assert.That(TrackedControllerChecks.Check(_rig).Severity, Is.EqualTo(SetupValidator.Severity.Pass));
        }

        [Test]
        public void InactiveController_Fails_AndRepairActivatesIt()
        {
            _controller.SetActive(false);

            var result = TrackedControllerChecks.Check(_rig);
            Assert.That(result.Severity, Is.EqualTo(SetupValidator.Severity.Fail));
            Assert.That(result.Message, Does.Contain("Right Controller"));
            Assert.That(result.Message, Does.Contain("inactive"));

            Assert.That(TrackedControllerChecks.Repair(_rig), Is.EqualTo(1));
            Assert.That(_controller.activeSelf, Is.True);
            Assert.That(TrackedControllerChecks.Check(_rig).Severity, Is.EqualTo(SetupValidator.Severity.Pass));
        }

        [Test]
        public void DisabledDriver_Fails_AndRepairEnablesIt()
        {
            _driver.enabled = false;

            Assert.That(TrackedControllerChecks.Check(_rig).Severity, Is.EqualTo(SetupValidator.Severity.Fail));
            Assert.That(TrackedControllerChecks.Repair(_rig), Is.EqualTo(1));
            Assert.That(_driver.enabled, Is.True);
        }

        [Test]
        public void MissingPoseAction_FailsWithoutAMechanicalFix()
        {
            _driver.rotationInput = new InputActionProperty();

            var result = TrackedControllerChecks.Check(_rig);
            Assert.That(result.Severity, Is.EqualTo(SetupValidator.Severity.Fail));
            Assert.That(result.Message, Does.Contain("no position or rotation action"));
            Assert.That(TrackedControllerChecks.Repair(_rig), Is.EqualTo(0));
        }
    }
}
