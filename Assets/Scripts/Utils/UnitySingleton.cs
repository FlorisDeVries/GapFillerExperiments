using UnityEngine;

public class UnitySingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null)
                throw new System.NullReferenceException($"The instance of {typeof(T)} has not been instantiated, add it to the scene and/or check whether the awake has been overriden.");
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance != null)
        {
            Debug.LogError($"There is another {typeof(T)} in the scene");
            return;
        }
        _instance = this as T;
    }
}
