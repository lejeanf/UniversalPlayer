using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Battery warnings for every mode (ships wired on the Player prefab, zero setup):
    /// - XR: a small camera-anchored panel warns when the headset or a controller runs
    ///   low (levels from the XR devices, same source XrHealthMonitor logs from);
    /// - Gamepad: a screen-space overlay warns when the gamepad runs low (Windows
    ///   XInput — other platforms report Unknown and stay silent).
    /// At the critical level the game is paused through PlayerEvents.PauseRequested
    /// (the bridge forwards it to the project's pause channel) and a failsafe countdown
    /// starts: when it elapses, control is forced back to mouse and keyboard so a dying
    /// device can never strand the player.
    /// Probes are injectable (Func seams) so tests run without hardware.
    /// </summary>
    public class BatteryWarningSystem : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer.XR]";

        public enum GamepadBatteryState { Unknown, Wired, Ok, Low, Critical }

        [Header("Thresholds")]
        [Range(0.05f, 0.5f)][SerializeField] private float warnLevel = 0.2f;
        [Range(0.01f, 0.2f)][SerializeField] private float criticalLevel = 0.08f;
        [SerializeField] private float pollIntervalSeconds = 10f;

        [Header("Critical failsafe")]
        [Tooltip("Pause the game (PlayerEvents.PauseRequested -> pause channel on the bridge) when any battery goes critical.")]
        [SerializeField] private bool pauseOnCritical = true;
        [Tooltip("Seconds of persisting critical battery before control is forced back to mouse & keyboard. 0 disables the failsafe.")]
        [SerializeField] private float failsafeSwitchSeconds = 30f;

        /// <summary>Test seam: (label, 0..1 level) per XR device that reports a battery.</summary>
        public Func<List<(string label, float level)>> XrBatteryProbe;
        /// <summary>Test seam: coarse gamepad battery state.</summary>
        public Func<GamepadBatteryState> GamepadBatteryProbe;

        private float _nextPoll;
        private string _warningText = "";
        private bool _critical;
        private float _criticalSince;
        private bool _pauseRaised;
        private bool _failsafeFired;
        private Text _vrText;
        private Text _overlayText;
        private GameObject _vrPanel;
        private GameObject _overlayPanel;

        private void Awake()
        {
            XrBatteryProbe ??= ReadXrBatteries;
            GamepadBatteryProbe ??= ReadGamepadBattery;
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextPoll)
            {
                _nextPoll = Time.unscaledTime + pollIntervalSeconds;
                Poll();
            }
            UpdateUi();
            UpdateFailsafe();
        }

        private void Poll()
        {
            var warnings = new List<string>();
            var critical = false;
            var scheme = BroadcastControlsStatus.controlScheme;

            if (scheme == BroadcastControlsStatus.ControlScheme.XR)
            {
                foreach (var (label, level) in XrBatteryProbe())
                {
                    // 0 (or less) means "could not read" on several runtimes (Quest Link
                    // among them) — a genuinely empty headset is already off. Treating it
                    // as critical would pause the game forever on a healthy setup.
                    if (level <= 0f || level > warnLevel) continue;
                    warnings.Add($"{label} battery {Mathf.RoundToInt(level * 100)}%");
                    if (level <= criticalLevel) critical = true;
                }
            }
            else if (scheme == BroadcastControlsStatus.ControlScheme.Gamepad)
            {
                var state = GamepadBatteryProbe();
                if (state == GamepadBatteryState.Low) warnings.Add("Controller battery low");
                else if (state == GamepadBatteryState.Critical)
                {
                    warnings.Add("Controller battery critical");
                    critical = true;
                }
            }

            _warningText = string.Join("\n", warnings);

            if (critical && !_critical)
            {
                _criticalSince = Time.unscaledTime;
                _failsafeFired = false;
                Debug.LogWarning($"{LogPrefix} BatteryWarningSystem: CRITICAL battery — {_warningText.Replace('\n', ';')}. " +
                    (pauseOnCritical ? "Pausing the game. " : "") +
                    (failsafeSwitchSeconds > 0 ? $"Switching to mouse & keyboard in {failsafeSwitchSeconds:F0}s unless it recovers." : ""));
                if (pauseOnCritical && !_pauseRaised)
                {
                    _pauseRaised = true;
                    PlayerEvents.RaisePause(true);
                }
            }
            _critical = critical;
        }

        private void UpdateFailsafe()
        {
            if (!_critical || _failsafeFired || failsafeSwitchSeconds <= 0) return;
            if (Time.unscaledTime - _criticalSince < failsafeSwitchSeconds) return;

            _failsafeFired = true;
            var broadcaster = GetComponentInParent<BroadcastControlsStatus>();
            if (broadcaster == null) broadcaster = GetComponentInChildren<BroadcastControlsStatus>(true);
            if (broadcaster != null) broadcaster.ForceDesktopControls("battery critical for too long");
            else Debug.LogWarning($"{LogPrefix} BatteryWarningSystem: failsafe elapsed but no BroadcastControlsStatus found — cannot switch to desktop controls.", this);
        }

        private void UpdateUi()
        {
            var scheme = BroadcastControlsStatus.controlScheme;
            var text = _warningText;
            if (_critical && failsafeSwitchSeconds > 0 && !_failsafeFired)
            {
                var remaining = Mathf.Max(0f, failsafeSwitchSeconds - (Time.unscaledTime - _criticalSince));
                text += $"\nSwitching to keyboard in {Mathf.CeilToInt(remaining)}s";
            }

            var showVr = scheme == BroadcastControlsStatus.ControlScheme.XR && text.Length > 0;
            var showOverlay = scheme == BroadcastControlsStatus.ControlScheme.Gamepad && text.Length > 0;

            if (showVr && _vrPanel == null) BuildVrPanel();
            if (showOverlay && _overlayPanel == null) BuildOverlayPanel();
            if (_vrPanel != null && _vrPanel.activeSelf != showVr) _vrPanel.SetActive(showVr);
            if (_overlayPanel != null && _overlayPanel.activeSelf != showOverlay) _overlayPanel.SetActive(showOverlay);
            if (showVr && _vrText != null) _vrText.text = text;
            if (showOverlay && _overlayText != null) _overlayText.text = text;
        }

        // ---- runtime-built UI (no prefab assets, no wiring) ----

        private void BuildVrPanel()
        {
            var camera = Camera.main;
            if (camera == null) return;

            _vrPanel = new GameObject("BatteryWarning_VR");
            _vrPanel.transform.SetParent(camera.transform, false);
            _vrPanel.transform.localPosition = new Vector3(0f, -0.12f, 1.2f);
            var canvas = _vrPanel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)_vrPanel.transform;
            rect.sizeDelta = new Vector2(600f, 160f);
            rect.localScale = Vector3.one * 0.0006f;
            _vrText = BuildText(_vrPanel.transform, 40);
        }

        private void BuildOverlayPanel()
        {
            _overlayPanel = new GameObject("BatteryWarning_Overlay");
            _overlayPanel.transform.SetParent(transform, false);
            var canvas = _overlayPanel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var text = BuildText(_overlayPanel.transform, 24);
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(420f, 120f);
            text.alignment = TextAnchor.UpperRight;
            _overlayText = text;
        }

        private static Text BuildText(Transform parent, int size)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = new Color(1f, 0.55f, 0.15f);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return text;
        }

        // ---- default probes ----

        private static List<(string, float)> ReadXrBatteries()
        {
            var result = new List<(string, float)>();
            var devices = new List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevices(devices);
            foreach (var device in devices)
            {
                if (!device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.batteryLevel, out var level)) continue;
                var isHead = (device.characteristics & UnityEngine.XR.InputDeviceCharacteristics.HeadMounted) != 0;
                var isLeft = (device.characteristics & UnityEngine.XR.InputDeviceCharacteristics.Left) != 0;
                var label = isHead ? "Headset" : isLeft ? "Left controller" : "Right controller";
                result.Add((label, level));
            }
            return result;
        }

        private static GamepadBatteryState ReadGamepadBattery()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (XInputGetBatteryInformation(0, 0 /*gamepad*/, out var info) != 0) return GamepadBatteryState.Unknown;
            return info.BatteryType switch
            {
                0 => GamepadBatteryState.Unknown, // disconnected
                1 => GamepadBatteryState.Wired,
                _ => info.BatteryLevel switch
                {
                    0 => GamepadBatteryState.Critical, // empty
                    1 => GamepadBatteryState.Low,
                    _ => GamepadBatteryState.Ok,
                },
            };
#else
            return GamepadBatteryState.Unknown;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct XinputBatteryInformation
        {
            public byte BatteryType;
            public byte BatteryLevel;
        }

        [System.Runtime.InteropServices.DllImport("xinput1_4.dll", EntryPoint = "XInputGetBatteryInformation")]
        private static extern int XInputGetBatteryInformation(int userIndex, byte deviceType, out XinputBatteryInformation info);
#endif
    }
}
