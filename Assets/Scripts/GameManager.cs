using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Global References")]
    public PlayerController Player;
    public TentacleManager TentacleManager;
    public Camera MainCamera;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        InitializeGame();
    }

    private void InitializeGame()
    {
        if (Player != null)
        {
            LocalInput.Init();
            Player.Initialize();
        }

        if (TentacleManager != null)
        {
            TentacleManager.Initialize();
        }
    }
}
