using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CausalityController : MonoBehaviour
{
    void Setup()
    {
        causalities = Array.ConvertAll<Causality.CausalityType, ICausality>(_backingCausalityTypes, it => it.toImplementation()).ToList<ICausality>();
    }

    void OnValidate()
    {
        causalities = Array.ConvertAll<Causality.CausalityType, ICausality>(_backingCausalityTypes, it => it.toImplementation()).ToList<ICausality>();
    }

    [SerializeField]
    private Causality.CausalityType[] _backingCausalityTypes;
    public Causality.CausalityType[] causalityTypes
    {
        get => _backingCausalityTypes;
        set
        {
            _backingCausalityTypes = value;
            causalities = _backingCausalityTypes.ToList().ConvertAll<ICausality>(it => it.toImplementation());
        }
    }

    private List<ICausality> causalities;

    public bool OnTimeChange(GameObject orig, GameObject curr, GameObject future)
    {
        var changed = causalities.Any(it => it.StateChangedSubstantially(orig, curr));
        if (!changed) return false;
        
        causalities.ForEach(it => it.UpdateFutureState(curr, future));
        return true;
    } 
}

public static class Causality
{
    public enum CausalityType
    {
        Spatial
    }

    public static ICausality toImplementation(this CausalityType type) => type switch
    {
        CausalityType.Spatial => new SpatialCausality(),
        _ => throw new ArgumentOutOfRangeException(nameof(type), $"Not expected causality value: {type}"),
    };
  
}

public interface ICausality
{
    public bool StateChangedSubstantially(GameObject orig, GameObject curr);

    public void UpdateFutureState(GameObject pastState, GameObject futureState);
}

public class SpatialCausality : ICausality
{
    public bool StateChangedSubstantially(GameObject orig, GameObject curr)
    {
        var delta = orig.transform.delta(curr.transform);
        return delta.position.magnitude > 0.1f || delta.rotationAngle > 5f || delta.scale.magnitude > 0.1f;
    }

    // Method Marked as Virtual so that we can override it with something if need be.
    public virtual void UpdateFutureState(GameObject pastState, GameObject futureState)
    {
        Debug.Log("Future Position being updated");
        futureState.transform.setTo(pastState.transform);
    }
}