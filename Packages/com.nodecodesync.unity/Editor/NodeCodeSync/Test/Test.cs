using UnityEngine;

public class Test : MonoBehaviour
{
    private readonly int num = 10;


    public int Num { get => num; }

    void Start()
    {
        float a = 5f;
        for (int i = 0; i < 10; i++)
        {
            
        }
    }
    /// 足し算
    public float Sum(float a, float b)
    {
        Debug.Log(a + b);
        return a + b;
    }
}

