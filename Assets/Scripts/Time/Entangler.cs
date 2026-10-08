using System.Collections.Generic;
using UnityEngine;

public class Entangler : MonoBehaviour
{
    void Start()
    {
        GlobalState.get().entangled.Add(this);
        UpdatePastOrigObject();
        OnTimeChange();
    }
    
    void OnDestroy()
    {
        GlobalState.get().entangled.Remove(this);
        Destroy(pastOrigObject);
    }

    void UpdatePastOrigObject()
    {
        if (pastOrigObject != null)
            Destroy(pastOrigObject);
        pastOrigObject = Instantiate(pastObject, this.transform);
        pastOrigObject.SetActive(false);
    }

    [SerializeField]
    private GameObject pastObject;
    private GameObject pastOrigObject = null;

    [SerializeField]
    private GameObject futureObject;
    
    [SerializeField]
    private CausalityController causalityController;

    public void OnTimeChange()
    {
        switch (GlobalState.get().now)
        {
            case TimeState.Future:
                pastObject.SetActive(false);
                futureObject.SetActive(true);
                if (causalityController.OnTimeChange(pastOrigObject, pastObject, futureObject))
                {
                    UpdatePastOrigObject();
                }
                break;
            case TimeState.Past:
                pastObject.SetActive(true);
                futureObject.SetActive(false);
                break;
        }
    }
}
