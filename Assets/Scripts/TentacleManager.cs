using System;
using UnityEngine;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

public class TentacleManager : MonoBehaviour
{
    [Header("Pool Settings")]
    public TentacleController tentaclePrefab;
    public int maxTentacles = 10;
    
    [Header("Spawn Settings")]
    public float minSpawnRadius = 5f;
    public float maxSpawnRadius = 15f;
    public float spawnInterval = 2f;
    public LayerMask spawnMask;

    [Header("Tentacle Settings")]
    public float tentacleLifetime = 15f;

    public event Action<float> OnTick;

    private ObjectPool<TentacleController> tentaclePool;
    private float spawnTimer;
    private bool isInitialized;

    public void Initialize()
    {
        tentaclePool = new ObjectPool<TentacleController>(
            createFunc: () => Instantiate(tentaclePrefab),
            actionOnGet: tentacle => tentacle.gameObject.SetActive(true),
            actionOnRelease: tentacle => tentacle.gameObject.SetActive(false),
            actionOnDestroy: tentacle => Destroy(tentacle.gameObject),
            collectionCheck: false,
            defaultCapacity: maxTentacles,
            maxSize: maxTentacles
        );
        isInitialized = true;
    }

    private void Update()
    {
        if (!isInitialized) return;

        // Tick all active tentacles
        OnTick?.Invoke(Time.deltaTime);

        // Handle Spawning
        if (tentaclePool.CountActive < maxTentacles)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0)
            {
                spawnTimer = spawnInterval;
                SpawnTentacle();
            }
        }
    }

    private void SpawnTentacle()
    {
        Transform playerTransform = GameManager.Instance.Player.transform;
        Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(minSpawnRadius, maxSpawnRadius);
        Vector3 spawnPos = playerTransform.position + new Vector3(randomCircle.x, 50f, randomCircle.y);

        if (Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 100f, spawnMask))
        {
            TentacleController tentacle = tentaclePool.Get();
            tentacle.transform.position = hit.point;
            tentacle.transform.rotation = Quaternion.identity; // Base rotation
            tentacle.Initialize(this, tentacleLifetime);
        }
    }

    public void Despawn(TentacleController tentacle)
    {
        tentaclePool.Release(tentacle);
    }
}
