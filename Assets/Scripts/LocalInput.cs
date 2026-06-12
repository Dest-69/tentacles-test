using UnityEngine;

public static class LocalInput
{
    public static InputSystem_Actions Actions { get; private set; }

    public static void Init()
    {
        if (Actions == null)
        {
            Actions = new InputSystem_Actions();
        }
        Actions.UI.Enable();
        Actions.Player.Enable();
        
        // Ensure the mouse device is enabled
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && !mouse.enabled)
        {
            UnityEngine.InputSystem.InputSystem.EnableDevice(mouse);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    public static Vector2 Move => Actions.Player.Move.ReadValue<Vector2>();
    public static Vector2 Look => Actions.Player.Look.ReadValue<Vector2>();
}
