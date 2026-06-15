using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(CinemachineCamera))]
public class CameraInputAdapter : MonoBehaviour
{
    [Header("Components")]
    public CinemachineOrbitalFollow orbitalFollow;
    public CinemachinePanTilt panTilt;
    
    [Header("Sensitivity")]
    public float lookSensitivity = 0.2f;

    private void Start()
    {
        if (orbitalFollow == null && panTilt == null)
        {
            Debug.LogWarning("[CameraInputAdapter] Neither orbitalFollow nor panTilt is assigned in the Inspector!", this);
        }
    }

    private void Update()
    {
        if (orbitalFollow == null && panTilt == null)
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
            // Apply values to orbital follow if assigned
            if (orbitalFollow != null)
            {
                orbitalFollow.HorizontalAxis.Value += lookInput.x * lookSensitivity;
                orbitalFollow.VerticalAxis.Value -= lookInput.y * lookSensitivity; 
            }
            
            // Apply values to pan tilt (First Person Perspective) if assigned
            if (panTilt != null)
            {
                panTilt.PanAxis.Value += lookInput.x * lookSensitivity;
                panTilt.TiltAxis.Value -= lookInput.y * lookSensitivity;
            }
        }
    }
}
