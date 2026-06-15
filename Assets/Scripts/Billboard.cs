using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform playerTransform;

    private void Start()
    {
        if (GameManager.Instance != null && GameManager.Instance.Player != null)
        {
            playerTransform = GameManager.Instance.Player.transform;
        }
    }

    private void LateUpdate()
    {
        if (playerTransform != null)
        {
            // Point the sprite so it faces the player
            // Sprites by default are visible from the -Z direction
            Vector3 directionToFace = transform.position - playerTransform.position;
            if (directionToFace != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(directionToFace);
            }
        }
        else if (Camera.main != null)
        {
            // Fallback to camera
            Vector3 directionToFace = transform.position - Camera.main.transform.position;
            if (directionToFace != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(directionToFace);
            }
        }
    }
}
