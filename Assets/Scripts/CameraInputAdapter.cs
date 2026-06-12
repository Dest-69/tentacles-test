using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(CinemachineCamera))]
public class CameraInputAdapter : MonoBehaviour
{
    [Header("Components")]
    public CinemachineOrbitalFollow orbitalFollow;
    
    [Header("Sensitivity")]
    public float lookSensitivity = 0.2f;

    private void Start()
    {
        if (orbitalFollow == null)
        {
            Debug.LogWarning("[CameraInputAdapter] orbitalFollow is NOT assigned in the Inspector!", this);
        }
    }

    private void Update()
    {
        if (orbitalFollow == null)
        {
            return;
        }

        // Get the raw mouse delta from the static input class
        Vector2 lookInput = LocalInput.Look; 

        // FALLBACK: If LocalInput is broken, try reading the mouse directly!
        if (lookInput == Vector2.zero && UnityEngine.InputSystem.Mouse.current != null)
        {
            lookInput = UnityEngine.InputSystem.Mouse.current.delta.ReadValue();
        }

        if (lookInput != Vector2.zero)
        {
            // Apply values to the camera axes
            orbitalFollow.HorizontalAxis.Value += lookInput.x * lookSensitivity;
            
            // Subtract Y so the camera is not inverted vertically 
            // (or add if inversion is needed)
            orbitalFollow.VerticalAxis.Value -= lookInput.y * lookSensitivity; 
        }
    }
}
