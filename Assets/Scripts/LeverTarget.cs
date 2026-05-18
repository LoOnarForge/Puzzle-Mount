using UnityEngine;

/// <summary>
/// Base class for any object controlled by a lever.
/// Extend this and implement Toggle() to define what the object does when the lever fires.
/// Override SetPowerColor() if the object needs to react to the power color.
/// </summary>
public abstract class LeverTarget : MonoBehaviour
{
    /// <summary>Called when power is connected. Override to use the incoming color.</summary>
    public virtual void SetPowerColor(Color color) { }

    /// <summary>Flips the current state of this object.</summary>
    public abstract void Toggle();
}
