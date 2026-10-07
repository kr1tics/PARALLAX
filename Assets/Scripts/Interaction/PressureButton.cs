using UnityEngine;

public class PressureButton : MonoBehaviour
{
    [SerializeField] private Trigger[] triggers;
    [SerializeField] private float gravityRequirement = 4.5f;
    private bool held = false;
    void OnCollisionEnter()
    {
        if (GlobalState.get().gravity > gravityRequirement && !held)
        {
            foreach (var trigger in triggers)
            {
            
                trigger.onStartTrigger();
                held = true;
            }
        }
    }

    void OnCollisionStay()
    {
        foreach (var trigger in triggers)
        {
            if (GlobalState.get().gravity > gravityRequirement && !held)
            {
                trigger.onStartTrigger();
                held = true;
            }
            else if (GlobalState.get().gravity < gravityRequirement && held)
            {
                trigger.onEndTrigger();
                held = false;
            }
        }
    }

    void OnCollisionExit()
    {
        if (held)
        {
            foreach (var trigger in triggers)
            {
                trigger.onEndTrigger();
                held = false;
            }
        }
    }
}
