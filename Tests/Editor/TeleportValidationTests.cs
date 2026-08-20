using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using jeanf.validationTools;
using NUnit.Framework;
using UnityEngine;

namespace jeanf.universalplayer.tests.editor
{
    /// <summary>
    /// Locks the mode-aware validation of TeleportOnEvent: the player field is only
    /// required while the listener actually teleports the player — an object-only
    /// listener (Teleports Player off) must scan clean with no player assigned.
    /// </summary>
    public class TeleportValidationTests
    {
        private GameObject _go;
        private TeleportOnEvent _listener;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TeleportOnEvent_ValidationTest");
            _go.SetActive(false); // no OnEnable subscriptions in edit mode tests
            _listener = _go.AddComponent<TeleportOnEvent>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        private List<ValidationIssue> Scan()
        {
            var issues = new List<ValidationIssue>();
            ValidationScanner.GetIssues(_listener, issues);
            return issues;
        }

        [Test]
        public void PlayerTeleporter_WithoutPlayer_IsFlagged()
        {
            Assert.That(Scan().Any(i => i.FieldName == "player"), Is.True,
                "A listener that teleports the player no longer flags a missing player root — " +
                "the RequiredIf gate on TeleportOnEvent.player is broken or the field was renamed.");
        }

        [Test]
        public void ObjectOnlyListener_WithoutPlayer_ScansClean()
        {
            typeof(TeleportOnEvent)
                .GetField("teleportsPlayer", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_listener, false);

            Assert.That(Scan(), Is.Empty,
                "An object-only listener (Teleports Player off) is still flagged for setup issues — " +
                "the player requirement must not apply when the listener never moves the player.");
        }
    }
}
