using System;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Script helper pour débugger les problèmes de FunctionTimer
/// Attache ce script à un GameObject dans ta scène pour voir les timers actifs
/// </summary>
public class FunctionTimerDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showGuiOverlay = true;
    [SerializeField] private float updateInterval = 0.5f;
    
    private float nextUpdateTime;
    private List<TimerInfo> cachedTimerInfos = new List<TimerInfo>();
    
    private struct TimerInfo
    {
        public string name;
        public float remainingTime;
        public bool isActive;
        public bool isDestroyed;
    }
    
    private void Update()
    {
        if (Time.time >= nextUpdateTime)
        {
            nextUpdateTime = Time.time + updateInterval;
            UpdateTimerInfos();
        }
    }
    
    private void UpdateTimerInfos()
    {
        cachedTimerInfos.Clear();
        
        // Utiliser reflection pour accéder à la liste privée
        var activeTimerListField = typeof(FunctionTimer).GetField("activeTimerList", 
            BindingFlags.Static | BindingFlags.NonPublic);
        
        if (activeTimerListField == null)
        {
            if (showDebugLogs)
                Debug.LogWarning("[FunctionTimerDebugger] Cannot access activeTimerList");
            return;
        }
        
        var activeTimers = activeTimerListField.GetValue(null) as List<FunctionTimer>;
        
        if (activeTimers == null)
        {
            if (showDebugLogs)
                Debug.Log("[FunctionTimerDebugger] No timer list initialized");
            return;
        }
        
        if (showDebugLogs)
            Debug.Log($"[FunctionTimerDebugger] Active timers count: {activeTimers.Count}");
        
        // Récupérer les infos de chaque timer
        for (int i = 0; i < activeTimers.Count; i++)
        {
            var timer = activeTimers[i];
            
            var info = new TimerInfo
            {
                name = GetTimerName(timer),
                remainingTime = GetRemainingTime(timer),
                isActive = GetIsActive(timer),
                isDestroyed = GetIsDestroyed(timer)
            };
            
            cachedTimerInfos.Add(info);
            
            if (showDebugLogs)
            {
                Debug.Log($"  Timer [{i}]: Name='{info.name}', " +
                         $"Remaining={info.remainingTime:F2}s, " +
                         $"Active={info.isActive}, " +
                         $"Destroyed={info.isDestroyed}");
            }
        }
    }
    
    private string GetTimerName(FunctionTimer timer)
    {
        var field = typeof(FunctionTimer).GetField("timerName", 
            BindingFlags.Instance | BindingFlags.NonPublic);
        return (field?.GetValue(timer) as string) ?? "unnamed";
    }
    
    private float GetRemainingTime(FunctionTimer timer)
    {
        var field = typeof(FunctionTimer).GetField("timer", 
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null ? (float)field.GetValue(timer) : 0f;
    }
    
    private bool GetIsActive(FunctionTimer timer)
    {
        var prop = typeof(FunctionTimer).GetProperty("IsActive");
        return prop != null ? (bool)prop.GetValue(timer) : false;
    }
    
    private bool GetIsDestroyed(FunctionTimer timer)
    {
        var field = typeof(FunctionTimer).GetField("isDestroyed", 
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null ? (bool)field.GetValue(timer) : true;
    }
    
    private void OnGUI()
    {
        if (!showGuiOverlay) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 400, Screen.height - 20));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label($"<b>FunctionTimer Debug</b> ({cachedTimerInfos.Count} timers)", 
            new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 });
        
        GUILayout.Space(10);
        
        if (cachedTimerInfos.Count == 0)
        {
            GUILayout.Label("No active timers");
        }
        else
        {
            for (int i = 0; i < cachedTimerInfos.Count; i++)
            {
                var info = cachedTimerInfos[i];
                
                Color color = info.isDestroyed ? Color.red : 
                             info.isActive ? Color.green : Color.yellow;
                
                GUI.color = color;
                GUILayout.Label($"[{i}] {info.name}: {info.remainingTime:F2}s " +
                               $"(Active: {info.isActive}, Destroyed: {info.isDestroyed})");
                GUI.color = Color.white;
            }
        }
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Refresh Now"))
        {
            UpdateTimerInfos();
        }
        
        if (GUILayout.Button("Stop All Timers"))
        {
            FunctionTimer.StopAllTimers();
            UpdateTimerInfos();
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}

/// <summary>
/// Extension pour faciliter le debug dans ton code
/// </summary>
public static class FunctionTimerDebugExtensions
{
    /// <summary>
    /// Crée un timer avec logging automatique
    /// </summary>
    public static FunctionTimer CreateDebug(Action action, float time, string name, GameObject caller = null)
    {
        string callerName = caller != null ? caller.name : "Unknown";
        string fullName = $"{name} (from {callerName})";
        
        Debug.Log($"[FunctionTimer] Creating timer '{fullName}' for {time}s");
        
        return FunctionTimer.Create(() =>
        {
            Debug.Log($"[FunctionTimer] Timer '{fullName}' completed!");
            action?.Invoke();
        }, time, fullName);
    }
    
    /// <summary>
    /// Stop avec logging
    /// </summary>
    public static void StopTimerDebug(string timerName)
    {
        Debug.Log($"[FunctionTimer] Stopping timer '{timerName}'");
        FunctionTimer.StopTimer(timerName);
    }
}