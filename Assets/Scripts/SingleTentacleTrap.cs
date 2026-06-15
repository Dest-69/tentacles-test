using UnityEngine;

public class SingleTentacleTrap : MonoBehaviour
{
    [Header("Settings")]
    public TentacleController tentaclePrefab;
    [Tooltip("The point in the world where the tentacle will throw the player.")]
    public Transform throwTargetPoint;

    private TentacleController spawnedTentacle;

    private void Start()
    {
        SpawnTentacle();
    }

    private void SpawnTentacle()
    {
        if (tentaclePrefab == null)
        {
            Debug.LogError("SingleTentacleTrap: Tentacle Prefab is not assigned!");
            return;
        }

        // Instantiate the tentacle at this object's position and rotation
        spawnedTentacle = Instantiate(tentaclePrefab, transform.position, transform.rotation);
        
        // Assign the exact throw target so it overrides the local throw direction
        if (throwTargetPoint != null)
        {
            spawnedTentacle.throwTargetWorld = throwTargetPoint;
        }

        // Initialize it using the standard pipeline with a huge lifetime and no manager
        spawnedTentacle.Initialize(null, 999999f);
    }

    private void Update()
    {
        if (spawnedTentacle != null)
        {
            spawnedTentacle.Tick(Time.deltaTime);
        }
    }
}
