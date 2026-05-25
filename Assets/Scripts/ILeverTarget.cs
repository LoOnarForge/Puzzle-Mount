using UnityEngine;

/// Interface for any object controlled by a lever.
/// Implement Toggle() and SetPowerColor() in the object's own behaviour script.
public interface ILeverTarget
{
    void SetPowerColor(Color color);
    void Toggle();
}