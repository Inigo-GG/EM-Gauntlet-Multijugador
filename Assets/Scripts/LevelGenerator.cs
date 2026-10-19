using Unity.Netcode;
using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class WeightedTilemapFiller
{
    public TilemapFiller tilemapFiller;
    [Range(0, 100)] public float weight = 1f;
}

[System.Serializable]
public class RingSettings
{
    [Header("Configuración visual del anillo (prefabs)")]
    [SerializeField] public string name = "Anillo";
    [SerializeField] public WeightedTile[] weightedTiles;
    [SerializeField] public GameObject wallPrefab;
    [SerializeField] public GameObject cornerPrefab;
    [SerializeField] public GameObject openDoor;
    [SerializeField] public GameObject closedDoor;
    [SerializeField] public GameObject decorativeElement;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Porcentaje de tiles del anillo que tendrán elemento decorativo (0 = ninguno, 1 = todos)")]
    public float decorativeElementPercentage = 0.05f;
}

public class LevelGenerator : MonoBehaviour
{
    [SerializeField] private TilemapFiller tilemapFiller;

    [Header("Configuración por defecto (fallback sin selección de menú)")]
    [SerializeField] private MapConfig defaultMapConfig;

    [Header("Sala del tesoro (prefabs)")]
    [SerializeField] private GameObject treasurePrefab;
    [SerializeField] private WeightedTile[] treasureRoomTiles;
    [SerializeField] private GameObject treasureRoomWallPrefab;
    [SerializeField] private GameObject treasureRoomCornerPrefab;
    [SerializeField] public GameObject treasureRoomOpenDoor;
    [SerializeField] public GameObject treasureRoomClosedDoor;

    [Header("Prefabs de spawners por tipo de enemigo")]
    [SerializeField] private GameObject dragonSpawnerPrefab;
    [SerializeField] private GameObject goatSpawnerPrefab;

    [Header("Anillos del castillo (solo prefabs, en orden fijo)")]
    [Tooltip("Orden: 0=Decorada, 1=Baldosas, 2=Madera, 3=Baldosas rotas, 4=Patio, 5=Bosque exterior")]
    [SerializeField] private RingSettings[] castleRings;

    private Tilemap tilemap;
    private bool generated = false;

    /// <summary>
    /// MapConfig activo: prioriza la selección sincronizada del lobby, luego la
    /// guardada en GameManager (host), y por último el defaultMapConfig.
    /// </summary>
    private MapConfig activeConfig
    {
        get
        {
            if (LobbyManager.Instance != null)
            {
                MapConfig lobbyMap = LobbyManager.Instance.GetSelectedMap();
                if (lobbyMap != null) return lobbyMap;
            }
            if (GameManager.Instance != null && GameManager.Instance.SelectedMapConfig != null)
                return GameManager.Instance.SelectedMapConfig;
            return defaultMapConfig;
        }
    }

    /// <summary>
    /// Cachea el Tilemap de la escena.
    /// </summary>
    private void Awake()
    {
        tilemap = FindFirstObjectByType<Tilemap>();
    }

    /// <summary>
    /// Intenta generar el nivel ya. Si la semilla todavía no está disponible,
    /// espera al callback del LobbyManager.
    /// </summary>
    private void Start()
    {
        int seed = 0;
        if (LobbyManager.Instance != null)
        {
            seed = LobbyManager.Instance.MapSeed.Value;
        }
        else
        {
            Debug.LogWarning("[LevelGenerator] LobbyManager no disponible. Generando con semilla 0.");
        }

        Debug.Log($"[LevelGenerator] activeConfig al generar: {(activeConfig != null ? activeConfig.mapName : "null")}");
        tryGenerateWithSeed(seed);
    }

    /// <summary>
    /// Genera el nivel una sola vez usando la semilla indicada.
    /// </summary>
    private void tryGenerateWithSeed(int seed)
    {
        if (generated) return;
        generated = true;

        System.Random rng = new System.Random(seed);
        Debug.Log($"[LevelGenerator] Generando nivel con semilla {seed}.");

        if (tilemapFiller != null) tilemapFiller.SetRandom(rng);

        generateTreasureRoom(rng);
        generateRings(rng);
    }

    /// <summary>
    /// Construye la sala del tesoro e instancia el cofre en el centro.
    /// </summary>
    private void generateTreasureRoom(System.Random rng)
    {
        if (tilemapFiller == null)
        {
            Debug.LogWarning("[LevelGenerator] No se ha asignado el TilemapFiller.");
            return;
        }

        int roomSize = activeConfig != null ? activeConfig.treasureRoomSize : 7;

        tilemapFiller.BuildSquareRoom(
            tilemap,
            roomSize,
            treasureRoomTiles,
            null,
            treasureRoomWallPrefab,
            treasureRoomCornerPrefab,
            treasureRoomOpenDoor,
            treasureRoomClosedDoor
        );

        Vector3 center = new Vector3(0f, 0f, -0.1f);
        if (treasurePrefab != null)
        {
            GameObject chest = Instantiate(treasurePrefab, center, Quaternion.identity);
            UniqueEntity uniqueEntity = chest.GetComponent<UniqueEntity>();
            if (uniqueEntity != null) uniqueEntity.RegenerateIdOnSpawn();
        }
    }

    /// <summary>
    /// Construye los anillos activos según la configuración de mapa seleccionada.
    /// </summary>
    private void generateRings(System.Random rng)
    {
        if (castleRings == null || castleRings.Length == 0 || tilemapFiller == null) return;

        MapConfig cfg = activeConfig;
        int roomSize = cfg != null ? cfg.treasureRoomSize : 7;
        Vector2Int innerSize = new Vector2Int(roomSize, roomSize);

        for (int i = 0; i < castleRings.Length; i++)
        {
            RingSettings ring = castleRings[i];
            if (ring == null) continue;

            bool isLastRing = i == castleRings.Length - 1;
            bool isEnabled = isLastRing || isRingEnabled(cfg, i);

            if (!isEnabled)
            {
                Debug.Log($"[LevelGenerator] Anillo '{ring.name}' desactivado por MapConfig");
                continue;
            }

            int ringWidth = getRingWidth(cfg, i);
            GameObject[] spawners = buildSpawnersArray(cfg, i);
            float decorativePercentage = getDecorativePercentage(cfg, i, ring);

            tilemapFiller.BuildRectangularRingRoom(
                tilemap,
                innerSize,
                ringWidth,
                ring.weightedTiles,
                spawners,
                ring.wallPrefab,
                ring.cornerPrefab,
                isLastRing ? null : ring.openDoor,
                isLastRing ? null : ring.closedDoor,
                ring.decorativeElement,
                decorativePercentage
            );

            innerSize = new Vector2Int(
                innerSize.x + 2 * ringWidth,
                innerSize.y + 2 * ringWidth
            );
        }
    }

    // ---------------- Helpers (sin cambios respecto a tu versión) ----------------

    private bool isRingEnabled(MapConfig cfg, int index)
    {
        if (cfg == null) return true;
        return index switch
        {
            0 => cfg.decoratedRoom.enabled,
            1 => cfg.tileRoom.enabled,
            2 => cfg.woodRoom.enabled,
            3 => cfg.brokenTileRoom.enabled,
            4 => cfg.castleYard.enabled,
            _ => true
        };
    }

    private int getRingWidth(MapConfig cfg, int index)
    {
        if (cfg == null) return 8;
        return index switch
        {
            0 => cfg.decoratedRoom.ringWidth,
            1 => cfg.tileRoom.ringWidth,
            2 => cfg.woodRoom.ringWidth,
            3 => cfg.brokenTileRoom.ringWidth,
            4 => cfg.castleYard.ringWidth,
            5 => cfg.outerForest.ringWidth,
            _ => 8
        };
    }

    private float getDecorativePercentage(MapConfig cfg, int index, RingSettings ring)
    {
        if (cfg == null) return ring.decorativeElementPercentage;
        return index switch
        {
            0 => cfg.decoratedRoom.decorativePercentage,
            1 => cfg.tileRoom.decorativePercentage,
            2 => cfg.woodRoom.decorativePercentage,
            3 => cfg.brokenTileRoom.decorativePercentage,
            4 => cfg.castleYard.decorativePercentage,
            5 => cfg.outerForest.decorativePercentage,
            _ => ring.decorativeElementPercentage
        };
    }

    private GameObject[] buildSpawnersArray(MapConfig cfg, int index)
    {
        if (cfg == null) return null;
        int dragons = 0;
        int goats = 0;
        switch (index)
        {
            case 0: dragons = cfg.decoratedRoom.dragonSpawnerCount; goats = cfg.decoratedRoom.goatSpawnerCount; break;
            case 1: dragons = cfg.tileRoom.dragonSpawnerCount; goats = cfg.tileRoom.goatSpawnerCount; break;
            case 2: dragons = cfg.woodRoom.dragonSpawnerCount; goats = cfg.woodRoom.goatSpawnerCount; break;
            case 3: dragons = cfg.brokenTileRoom.dragonSpawnerCount; goats = cfg.brokenTileRoom.goatSpawnerCount; break;
            case 4: dragons = cfg.castleYard.dragonSpawnerCount; goats = cfg.castleYard.goatSpawnerCount; break;
            case 5: dragons = cfg.outerForest.dragonSpawnerCount; goats = cfg.outerForest.goatSpawnerCount; break;
        }
        int total = dragons + goats;
        if (total == 0) return null;
        GameObject[] spawners = new GameObject[total];
        int idx = 0;
        for (int d = 0; d < dragons; d++) spawners[idx++] = dragonSpawnerPrefab;
        for (int g = 0; g < goats; g++) spawners[idx++] = goatSpawnerPrefab;
        return spawners;
    }

    /// <summary>
    /// Callback que mantiene compatibilidad con el flujo anterior; vacío en multijugador.
    /// </summary>
    private void onLocalPlayerRegistered(PlayerController player)
    {
        // En multijugador, el spawn de jugadores lo gestiona LobbyManager.
    }
}