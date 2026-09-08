using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace jeanf.universalplayer
{
    public static class XrDisplayLifecycle
    {
        private static readonly List<XRDisplaySubsystem> Displays = new List<XRDisplaySubsystem>();

        public static int StartRequests { get; private set; }
        public static int StopRequests { get; private set; }
        public static int LastRequestedMirrorBlitMode { get; private set; } = XRMirrorViewBlitMode.Default;

        public static void ResetRequestHistory()
        {
            StartRequests = 0;
            StopRequests = 0;
            LastRequestedMirrorBlitMode = XRMirrorViewBlitMode.Default;
        }

        public static XRDisplaySubsystem FirstDisplay
        {
            get
            {
                SubsystemManager.GetSubsystems(Displays);
                return Displays.Count > 0 ? Displays[0] : null;
            }
        }

        public static bool HasDisplay => FirstDisplay != null;

        public static bool IsDisplayRunning
        {
            get
            {
                var display = FirstDisplay;
                return display != null && display.running;
            }
        }

        public static bool SessionFocused
        {
            get
            {
                try { return OpenXRUtility.IsSessionFocused; }
                catch (Exception) { return false; }
            }
        }

        public static bool HasActiveLoader => ActiveManager() != null;

        public static bool RequestStart()
        {
            StartRequests++;
            var manager = ActiveManager();
            if (manager == null) return false;
            manager.StartSubsystems();
            DpadLayoutGuard.RepairIfNeeded();
            return true;
        }

        public static bool RequestStop()
        {
            StopRequests++;
            var manager = ActiveManager();
            if (manager == null) return false;
            manager.StopSubsystems();
            return true;
        }

        public static void RequestMirrorBlitMode(int mirrorBlitMode)
        {
            LastRequestedMirrorBlitMode = mirrorBlitMode;
            SubsystemManager.GetSubsystems(Displays);
            for (int i = 0; i < Displays.Count; i++)
            {
                if (Displays[i] != null) Displays[i].SetPreferredMirrorBlitMode(mirrorBlitMode);
            }
        }

        private static XRManagerSettings ActiveManager()
        {
            var settings = XRGeneralSettings.Instance;
            var manager = settings != null ? settings.Manager : null;
            if (manager == null || !manager.isInitializationComplete || manager.activeLoader == null) return null;
            return manager;
        }
    }
}
