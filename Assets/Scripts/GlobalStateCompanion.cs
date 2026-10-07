using UnityEngine;

public class GlobalStateCompanion : MonoBehaviour
{
    private static bool unique = true; 
    
    [SerializeField] private Material shader;
    void Start()
    {
        if (!unique)
        {
            Debug.LogError("Cannot have multiple companions.");
        }

        unique = false;

        GlobalState.get().Setup(shader);
    }
    
    void Update()
    {
        GlobalState.get().Update();
    }

    void Destroy()
    {
        unique = true;
    }
}
