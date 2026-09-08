using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;

namespace jeanf.universalplayer
{
    public static class XrDisplayLifecycle
    {
        private static readonly List<XRDisplaySubsystem> Displays = new List<XRDisplaySubsystem>();

        public static int StartRequests { get; private set; }
        public static int StopRequests { get; private set; }

        public static void ResetRequestHistory()
        {
            StartRequests = 0;
            StopRequests = 0;
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

        public static bool StartDisplay()
        {
            StartRequests++;
            SubsystemManager.GetSubsystems(Displays);
            var started = false;
            for (int i = 0; i < Displays.Count; i++)
            {
                var display = Displays[i];
                if (display == null || display.running) continue;
                display.Start();
                started = true;
            }
            if (started) DpadLayoutGuard.RepairIfNeeded();
            return started;
        }

        public static bool StopDisplay()
        {
            StopRequests++;
            SubsystemManager.GetSubsystems(Displays);
            var stopped = false;
            for (int i = 0; i < Displays.Count; i++)
            {
                var display = Displays[i];
                if (display == null || !display.running) continue;
                display.Stop();
                stopped = true;
            }
            return stopped;
        }
    }
}
