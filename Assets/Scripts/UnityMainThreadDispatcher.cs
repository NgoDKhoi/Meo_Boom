using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();

    public static UnityMainThreadDispatcher Instance()
    {
        if (_instance == null)
        {
            var go = new GameObject("UnityMainThreadDispatcher");
            _instance = go.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(go);
        }
        return _instance;
    }

    public void Enqueue(Action action)
    {
        if (action == null) return;
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }

    void Update()
    {
        if (_executionQueue.Count == 0) return;
        Action[] actions;
        lock (_executionQueue)
        {
            actions = _executionQueue.ToArray();
            _executionQueue.Clear();
        }
        for (int i = 0; i < actions.Length; i++)
        {
            try { actions[i]?.Invoke(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }
}
