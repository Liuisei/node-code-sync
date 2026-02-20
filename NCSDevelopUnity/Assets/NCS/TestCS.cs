using System;
using UnityEngine;

public class TestCS : MonoBehaviour
{
    private const string _constString = "Hello, World!";
    public static readonly int StaticReadonlyInt = 42;
    private int _counter = 0;

    public int Counter
    {
        get => _counter;
        set
        {
            if (value >= 0)
            {
                _counter = value;
                Debug.Log($"Counter updated to: {_counter}");
            }
            else
            {
                Debug.LogWarning("Counter cannot be negative.");
            }
        }
    }

    delegate void MyDelegate(string message);
    Action<string> myAction = (message) => Debug.Log(message);

    //Unity clall
    void Start()
    {
        Debug.Log("Start method called.");
        Debug.Log($"Constant String: {_constString}");
        Debug.Log(_constString);
    }

    private int  sum(int a , int b , int c = 0)
    {
        return a + b + c;
    }
}
