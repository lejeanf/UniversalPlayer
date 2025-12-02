using System;
using System.Collections.Generic;
using jeanf.EventSystem;
using UnityEngine;

public class FunctionTimer
{
    private static List<FunctionTimer> activeTimerList;
    private static MonoBehaviourHook timerManager;
    
    private static Stack<FunctionTimer> timerPool = new Stack<FunctionTimer>(32);
    private const int INITIAL_POOL_SIZE = 16;

    private static void InitIfNeeded()
    {
        if (timerManager == null)
        {
            GameObject initGameObject = new GameObject("FunctionTimer_Manager");
            UnityEngine.Object.DontDestroyOnLoad(initGameObject);
            
            timerManager = initGameObject.AddComponent<MonoBehaviourHook>();
            activeTimerList = new List<FunctionTimer>(32);
            
            for (int i = 0; i < INITIAL_POOL_SIZE; i++)
            {
                timerPool.Push(new FunctionTimer());
            }
        }
    }

    private static FunctionTimer GetFromPool()
    {
        if (timerPool.Count > 0)
        {
            return timerPool.Pop();
        }
        return new FunctionTimer();
    }

    private static void ReturnToPool(FunctionTimer timer)
    {
        timer.Reset();
        timerPool.Push(timer);
    }

    public static FunctionTimer Create(Action action, float timer, string timerName = null)
    {
        InitIfNeeded();
        
        FunctionTimer functionTimer = GetFromPool();
        functionTimer.Initialize(action, timer, timerName);

        activeTimerList.Add(functionTimer);

        return functionTimer;
    }

    private static void RemoveTimer(FunctionTimer functionTimer)
    {
        if (activeTimerList == null) return;
        
        activeTimerList.Remove(functionTimer);
        
        ReturnToPool(functionTimer);
    }

    public static void StopTimer(string timerName)
    {
        if (activeTimerList == null || timerName == null) return;

        for (int i = activeTimerList.Count - 1; i >= 0; i--)
        {
            if (activeTimerList[i].timerName == timerName)
            {
                activeTimerList[i].DestroySelf();
            }
        }
    }

    private static void CleanupDestroyedTimers()
    {
        if (activeTimerList == null) return;
        
        activeTimerList.RemoveAll(t => t.isDestroyed);
    }

    public static void StopAllTimers()
    {
        if (activeTimerList == null) return;
        
        for (int i = activeTimerList.Count - 1; i >= 0; i--)
        {
            activeTimerList[i].DestroySelf();
        }
        
        activeTimerList.Clear();
    }

    private class MonoBehaviourHook : MonoBehaviour
    {
        private int frameCounter = 0;
        private const int CLEANUP_INTERVAL = 60;

        private void Update()
        {
            if (activeTimerList == null || activeTimerList.Count == 0) return;

            float deltaTime = Time.deltaTime;

            for (int i = activeTimerList.Count - 1; i >= 0; i--)
            {
                if (i >= activeTimerList.Count) continue;
                
                FunctionTimer timer = activeTimerList[i];
                
                if (timer.isDestroyed) continue;

                timer.timer -= deltaTime;
                
                if (timer.timer <= 0f)
                {
                    try
                    {
                        timer.action?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[FunctionTimer] Error in timer '{timer.timerName}': {e.Message}");
                    }
                    
                    timer.DestroySelf();
                }
            }
        }

        private void OnDestroy()
        {
            StopAllTimers();
        }
    }

    private Action action;
    private float timer;
    private string timerName;
    private bool isDestroyed;
    private FloatEventChannelSO validationChannel; 
    private float remainingTime;

    private FunctionTimer()
    {
        isDestroyed = true;
    }

    private void Initialize(Action action, float timer, string timerName)
    {
        this.action = action;
        this.timer = timer;
        this.timerName = timerName;
        this.isDestroyed = false;
    }

    private void Reset()
    {
        this.action = null;
        this.timer = 0f;
        this.timerName = null;
        this.isDestroyed = true;
        this.validationChannel = null;
    }

    private void DestroySelf()
    {
        if (isDestroyed) return;
        
        isDestroyed = true;
        
        RemoveTimer(this);
    }

    public float RemainingTime => isDestroyed ? 0f : timer;
    public bool IsActive => !isDestroyed;
    public string Name => timerName;

    public void AddTime(float additionalTime)
    {
        if (!isDestroyed)
        {
            timer += additionalTime;
        }
    }

    public void SetTime(float newTime)
    {
        if (!isDestroyed)
        {
            timer = newTime;
        }
    }
}

#if UNITY_EDITOR
public static class FunctionTimerDebug
{
    [UnityEditor.MenuItem("Tools/Function Timer/Show Active Timers")]
    private static void ShowActiveTimers()
    {
        var timers = typeof(FunctionTimer)
            .GetField("activeTimerList", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(null) as List<FunctionTimer>;

        if (timers == null || timers.Count == 0)
        {
            Debug.Log("[FunctionTimer] No active timers");
            return;
        }

        for (int i = 0; i < timers.Count; i++)
        {
            var timer = timers[i];
        }
    }

    [UnityEditor.MenuItem("Tools/Function Timer/Stop All Timers")]
    private static void StopAllTimersMenu()
    {
        FunctionTimer.StopAllTimers();
    }
}
#endif