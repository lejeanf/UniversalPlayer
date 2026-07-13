using System;
using System.Collections.Generic;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.XR;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Watches XR hardware while the game runs and reports problems the moment they
    /// happen: headset or controller disconnects (Link drop, powered off, battery died)
    /// and low battery levels. Every issue produces a console entry and, when the
    /// optional channels are assigned, an event the project can display to the user.
    /// Add this to the Player prefab (or its project variant).
    /// </summary>
    public class XrHealthMonitor : MonoBehaviour, IDebugBehaviour
    {
        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }
        [SerializeField] private bool _isDebug = false;

        [Header("Battery")]
        [Tooltip("Battery fraction (0-1) under which a low-battery warning is raised once per device.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float lowBatteryThreshold = 0.2f;
        [Tooltip("How often (seconds) battery levels are polled.")]
        [SerializeField] private float batteryCheckInterval = 30f;

        // Issue messages + HMD connection go through PlayerEvents; the PlayerEventBridge
        // forwards them onto the project's channels.

        public enum HealthEvent
        {
            HmdConnected,
            HmdDisconnected,
            ControllerConnected,
            ControllerDisconnected,
            LowBattery
        }

        /// <summary>Raised for every detected issue or recovery, with a human-readable message.</summary>
        public static event Action<HealthEvent, string> OnHealthEvent;

        // low-battery latch per device so a device warns once until it recovers/recharges
        private readonly HashSet<string> _lowBatteryNotified = new HashSet<string>();
        private static readonly List<InputDevice> DeviceBuffer = new List<InputDevice>();

        private void OnEnable()
        {
            InputDevices.deviceConnected += OnDeviceConnected;
            InputDevices.deviceDisconnected += OnDeviceDisconnected;
            InvokeRepeating(nameof(CheckBatteries), batteryCheckInterval, batteryCheckInterval);
        }

        private void OnDisable()
        {
            InputDevices.deviceConnected -= OnDeviceConnected;
            InputDevices.deviceDisconnected -= OnDeviceDisconnected;
            CancelInvoke(nameof(CheckBatteries));
        }

        private void OnDeviceConnected(InputDevice device)
        {
            _lowBatteryNotified.Remove(DeviceKey(device));

            if (IsHmd(device))
            {
                Raise(HealthEvent.HmdConnected, $"Headset connected: {device.name}.", isError: false);
                PlayerEvents.RaiseHmdConnection(true);
            }
            else if (IsController(device))
            {
                Raise(HealthEvent.ControllerConnected, $"{ControllerSide(device)} controller connected: {device.name}.", isError: false);
            }
        }

        private void OnDeviceDisconnected(InputDevice device)
        {
            if (IsHmd(device))
            {
                Raise(HealthEvent.HmdDisconnected,
                    "Headset disconnected — Link/Air Link dropped, cable unplugged, headset went to sleep, or its battery died.",
                    isError: true);
                PlayerEvents.RaiseHmdConnection(false);
            }
            else if (IsController(device))
            {
                Raise(HealthEvent.ControllerDisconnected,
                    $"{ControllerSide(device)} controller disconnected — powered off, battery dead, or out of tracking range.",
                    isError: true);
            }
        }

        private void CheckBatteries()
        {
            DeviceBuffer.Clear();
            InputDevices.GetDevices(DeviceBuffer);
            foreach (var device in DeviceBuffer)
            {
                if (!device.isValid) continue;
                if (!device.TryGetFeatureValue(CommonUsages.batteryLevel, out var level)) continue;

                var key = DeviceKey(device);
                if (level <= lowBatteryThreshold && !_lowBatteryNotified.Contains(key))
                {
                    _lowBatteryNotified.Add(key);
                    Raise(HealthEvent.LowBattery,
                        $"{DeviceLabel(device)} battery at {Mathf.RoundToInt(level * 100f)}% — charge it or swap batteries soon.",
                        isError: true);
                }
                else if (level > lowBatteryThreshold + 0.05f)
                {
                    _lowBatteryNotified.Remove(key); // recovered (recharged/swapped) — allow a future warning
                }
            }
        }

        private void Raise(HealthEvent healthEvent, string message, bool isError)
        {
            if (isError) Debug.LogWarning($"{XrStartupDiagnostics.LogPrefix} {message}", this);
            else if (_isDebug) Debug.Log($"{XrStartupDiagnostics.LogPrefix} {message}", this);

            OnHealthEvent?.Invoke(healthEvent, message);
            PlayerEvents.RaiseXrIssue(message);
        }

        private static bool IsHmd(InputDevice device) =>
            (device.characteristics & InputDeviceCharacteristics.HeadMounted) != 0;

        private static bool IsController(InputDevice device) =>
            (device.characteristics & InputDeviceCharacteristics.Controller) != 0;

        private static string ControllerSide(InputDevice device)
        {
            if ((device.characteristics & InputDeviceCharacteristics.Left) != 0) return "Left";
            if ((device.characteristics & InputDeviceCharacteristics.Right) != 0) return "Right";
            return "A";
        }

        private static string DeviceLabel(InputDevice device)
        {
            if (IsHmd(device)) return "Headset";
            if (IsController(device)) return $"{ControllerSide(device)} controller";
            return device.name;
        }

        private static string DeviceKey(InputDevice device) => $"{device.characteristics}:{device.name}";
    }
}
