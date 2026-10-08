using UnityEngine;

public class Door : Trigger
{
    [SerializeField] private Collider col;
    [SerializeField] private Renderer ren;

    public override void onStartTrigger()
    {
        col.enabled = false;
        ren.enabled = false;
    }

    public override void onEndTrigger()
    {
        col.enabled = true;
        ren.enabled = true;
    }
}
