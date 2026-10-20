using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private EnemySimpleAI enemyPrefab;
    [SerializeField] private Customer customerPrefab;
    [SerializeField] private GlobalGameSettings settings;
    [SerializeField] private Transform player;
    [SerializeField] private FoodTruckHealth foodTruck;
    [SerializeField] private Transform[] spawnPoints;
    private readonly List<SpawnedEntity> aliveEnemies = new List<SpawnedEntity>();
    private struct SpawnedEntity
    {
        public GameObject entity;
        public EnemyHealth zombie;
        public Customer customer;
        public SpawnedEntity(GameObject instance)
        { entity = instance; zombie = instance.GetComponent<EnemyHealth>(); customer = instance.GetComponent<Customer>(); }
        public bool Finished => entity == null || (zombie != null && zombie.IsDead) ||
            (customer != null && (customer.IsPurified || customer.IsDead));
    }
    private int spawnedCount;
    private float lastSpawnTime;
    private bool hasSpawned;
    private TruckShop coinWallet;
    private TruckShop ResolveCoinWallet()
    {
        if (coinWallet != null && coinWallet.gameObject.scene == gameObject.scene) return coinWallet;
        coinWallet = null;
        foreach (var wallet in FindObjectsByType<TruckShop>(FindObjectsInactive.Include))
        {
            if (wallet.gameObject.scene != gameObject.scene) continue;
            if (coinWallet != null) { coinWallet = null; return null; }
            coinWallet = wallet;
        }
        return coinWallet;
    }
    private void TrackEntity(GameObject instance)
    {
        var entry = new SpawnedEntity(instance);
        if (entry.customer != null) entry.customer.ConfigureCoinWallet(ResolveCoinWallet());
        aliveEnemies.Add(entry);
    }
    public void Configure(EnemySimpleAI prefab, Transform target, Transform[] points)
    { enemyPrefab = prefab; player = target; spawnPoints = points; }
    public void ConfigureCustomers(Customer prefab) => customerPrefab = prefab;
    public void ConfigureFoodTruck(FoodTruckHealth truck) => foodTruck = truck;

    private void Start()
    {
        if (!GlobalGameSettings.Require(this, settings)) return;
        if (enemyPrefab == null || player == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("EnemySpawner needs enemyPrefab, player and spawnPoints.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (!GlobalGameSettings.Require(this, settings)) return;
        if (hasSpawned && Time.time - lastSpawnTime < settings.SpawnInterval) return;
        aliveEnemies.RemoveAll(entity => entity.Finished);
        if (aliveEnemies.Count >= settings.MaximumAliveEnemies) return;
        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        if (point == null) return;
        lastSpawnTime = Time.time;
        hasSpawned = true;
        spawnedCount++;
        if (customerPrefab != null && spawnedCount % settings.CustomerEveryNthSpawn == 0)
        {
            Customer customer = Instantiate(customerPrefab, point.position, Quaternion.identity);
            if (customer.TryGetComponent(out EnemySimpleAI movement)) movement.SetTarget(player);
            TrackEntity(customer.gameObject);
            if (settings != null && settings.GameplayDebugLogs) Debug.Log("Customer spawned", customer);
        }
        else
        {
            EnemySimpleAI enemy = Instantiate(enemyPrefab, point.position, Quaternion.identity);
            enemy.SetTarget(player);
            enemy.SetFoodTruckTarget(foodTruck);
            TrackEntity(enemy.gameObject);
            if (settings != null && settings.GameplayDebugLogs) Debug.Log("Zombie spawned", enemy);
        }
    }
}
