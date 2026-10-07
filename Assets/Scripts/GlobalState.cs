using System.Collections.Generic;
using UnityEngine;

public class GlobalState
{
    #region Singleton Instance Stuff

    private static GlobalState gs = null;

    public static GlobalState get()
    {
        if (gs == null)
        {
            gs = new GlobalState();
        }

        return gs;
    }

    #endregion
    
    #region Physics
    
    public float gravity = 9.81f;
    
    #endregion


    #region TimeTravel

    public TimeState now = TimeState.Future;
    public HashSet<Entangler> entangled = new();

	// Variables related to effect.
	private Material shader;
	private const float transitionDuration = 1.0f;
	private float cooldown = transitionDuration;
	private float lastSwap = -999.0f;
	private bool actuallySwapped = true;

    public void SwapTime()
    {
		float time = Time.timeSinceLevelLoad;

		if (time < lastSwap + cooldown) {
			Debug.Log("Time Swap on Cooldown");
			return;
		}

		lastSwap = time;
		actuallySwapped = false;
		shader.SetFloat("_Duration", transitionDuration);
		shader.SetFloat("_StartTime", lastSwap);
    }

	private void ActuallySwapTime() {
		switch (now)
    	{
        	case TimeState.Future:
            	now = TimeState.Past;
            	break;
        	case TimeState.Past:
            	now = TimeState.Future;
            	break;
    	}

    	foreach (Entangler e in entangled)
    	{
        	e.OnTimeChange();
    	}
	}

    #endregion

    public void Setup(Material _shader)
    {
	    shader = _shader;
	    shader.SetFloat("_StartTime", lastSwap);
    }
    public void Update()
    {
	    float time = Time.timeSinceLevelLoad;
	    if (time > lastSwap + (cooldown / 2) && !actuallySwapped) {
		    ActuallySwapTime();			

		    actuallySwapped = true;
	    }
    }
}

public enum TimeState
{
    Past,
    Future
}