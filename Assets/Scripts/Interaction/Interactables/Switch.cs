using UnityEngine;
using UnityEngine.Events;

public class Switch : Interactable
{
    [SerializeField] private Trigger[] triggers;
    [SerializeField] private UnityEvent[] sideEffects;
    
    [SerializeField] private bool on = false;

    void Start()
    {
        if (on)
        {
            foreach (var effect in sideEffects)
            {
                effect.Invoke();
            }
        }
    }

    public override void onInteract(Interact i)
    {
        foreach (var effect in sideEffects)
        {
            effect.Invoke();
        }
        on = !on;
        foreach (var trigger in triggers)
        {
            if (on)
            {
                trigger.onStartTrigger();
            }
            else
            {
                trigger.onEndTrigger();
            }
        }
    }
}
