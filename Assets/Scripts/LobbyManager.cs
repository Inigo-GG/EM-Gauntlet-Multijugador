using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Gestor de red del lobby. Mantiene el listado sincronizado de jugadores
/// conectados, gestiona la selección de personaje y la transición al nivel.
public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

    public MapConfig CurrentMap => GetMapForIndex(SelectedMapIndex.Value);

    /// Número máximo de jugadores admitidos en una partida.
    public const int MaxPlayers = 4;

    /// Listado sincronizado de jugadores presentes en el lobby.
    public NetworkList<LobbyPlayerData> Players;

    /// Evento local que se dispara cuando cambia el listado de jugadores.
    public event System.Action OnPlayersChanged;

    /// Inicializa el NetworkList antes de que el objeto haga spawn en red.
    private void Awake()
    {
        /// El NetworkList se construye siempre, en cliente y servidor.
        Players = new NetworkList<LobbyPlayerData>();

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    [Header("Mapas disponibles")]
    [SerializeField] private MapConfig[] availableMaps;

    /// Índice del MapConfig elegido por el Host (0 = primero del array).
    public NetworkVariable<int> SelectedMapIndex = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// Semilla compartida para la generación procedural del mapa.
    /// Solo el servidor la escribe al iniciar la partida; todos los clientes la leen.
    public NetworkVariable<int> MapSeed = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// Hook de red: registra callbacks y, si es servidor, añade a los clientes ya conectados.
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer && SelectedMapIndex.Value == 0)
        {
            Debug.Log("[LobbyManager] Init map = 0");
        }

        Debug.Log($"[LobbyManager] OnNetworkSpawn. IsServer={IsServer}, IsClient={IsClient}");

        Players.OnListChanged += onPlayersListChanged;

        NetworkManager.SceneManager.OnLoadEventCompleted += onSceneLoadCompleted;

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += onClientConnectedServer;
            NetworkManager.Singleton.OnClientDisconnectCallback += onClientDisconnectedServer;

            foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
            {
                addPlayerIfMissing(id);
            }
        }

        OnPlayersChanged?.Invoke();
    }

    /// Hook de red: libera callbacks al despawnear el objeto.
    public override void OnNetworkDespawn()
    {
        Players.OnListChanged -= onPlayersListChanged;

        if (NetworkManager.Singleton != null && NetworkManager.SceneManager != null)
            NetworkManager.SceneManager.OnLoadEventCompleted -= onSceneLoadCompleted;

        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= onClientConnectedServer;
            NetworkManager.Singleton.OnClientDisconnectCallback -= onClientDisconnectedServer;
        }
    }

    /// Limpia el singleton al destruir el objeto.
    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    /// ---------------- Servidor: gestión de conexiones ----------------

    /// Añade al lobby al cliente recién conectado (solo servidor).
    private void onClientConnectedServer(ulong clientId)
    {
        addPlayerIfMissing(clientId);
    }

    /// Elimina del lobby al cliente desconectado (solo servidor).
    private void onClientDisconnectedServer(ulong clientId)
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].ClientId == clientId)
            {
                Players.RemoveAt(i);
                return;
            }
        }
    }

    /// Añade una entrada para el clientId si todavía no existe.
    private void addPlayerIfMissing(ulong clientId)
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].ClientId == clientId) return;
        }

        if (Players.Count >= MaxPlayers) return;
        Players.Add(new LobbyPlayerData(clientId));
    }

    /// ---------------- Cliente -> Servidor: selección de personaje ----------------

    /// El cliente pide al servidor cambiar su personaje. El servidor valida
    /// que el índice esté libre y, si lo está, actualiza el listado.
    [Rpc(SendTo.Server)]
    public void RequestSelectCharacterRpc(int characterIndex, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        if (characterIndex < 0 || characterIndex >= MaxPlayers) return;

        /// Comprobar que nadie más tenga ya ese personaje.
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].CharacterIndex == characterIndex && Players[i].ClientId != senderId)
                return;
        }

        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].ClientId == senderId)
            {
                LobbyPlayerData data = Players[i];
                data.CharacterIndex = characterIndex;
                Players[i] = data;
                return;
            }
        }
    }

    /// ---------------- Servidor: arranque de partida ----------------

    /// Comprueba si todos los jugadores presentes han elegido personaje.
    public bool AllPlayersHaveCharacter()
    {
        if (Players.Count == 0) return false;
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].CharacterIndex < 0) return false;
        }
        return true;
    }

    /// Devuelve el dato del jugador local, o un struct vacío si no existe aún.
    public LobbyPlayerData GetLocalPlayerData()
    {
        ulong localId = NetworkManager.Singleton.LocalClientId;
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].ClientId == localId) return Players[i];
        }
        return new LobbyPlayerData(localId);
    }

    /// Solo Host: copia el listado de jugadores a un snapshot persistente
    /// y pide la carga de PlaygroundLevel a todos los clientes.
    public void StartGameAsHost()
    {
        Debug.Log($"[SERVER] StartGameAsHost SelectedMapIndex={SelectedMapIndex.Value}");

        if (!IsServer) return;

        if (!AllPlayersHaveCharacter())
        {
            Debug.LogWarning("[Lobby] No todos los jugadores han elegido personaje.");
            return;
        }

        // GUARDAR EL MAPA ELEGIDO POR EL HOST
        GameManager.Instance.SelectedMapConfig = GetSelectedMap();
        Debug.Log($"[LobbyManager] Mapa seleccionado: {GetSelectedMap()?.mapName}");

        MapSeed.Value = Random.Range(int.MinValue, int.MaxValue);

        var snapshot = new System.Collections.Generic.List<LobbyPlayerData>(Players.Count);
        for (int i = 0; i < Players.Count; i++)
            snapshot.Add(Players[i]);

        GameManager.Instance.SetPendingPlayers(snapshot);

        Debug.Log("[LobbyManager] Loading PlaygroundLevel...");

        NetworkManager.SceneManager.LoadScene(
            SceneNames.PlaygroundLevel,
            UnityEngine.SceneManagement.LoadSceneMode.Single);
            }

    /// ---------------- Callback común ----------------

    /// Reenvía el cambio de lista como un evento local más cómodo de consumir desde la UI.
    private void onPlayersListChanged(NetworkListEvent<LobbyPlayerData> change)
    {
        OnPlayersChanged?.Invoke();
    }

    /// Callback de NGO al terminar de cargar una escena en todos los clientes.
    /// Aplica el MapConfig sincronizado en todos los participantes y, si el
    /// servidor cargó PlaygroundLevel, spawnea los Players del lobby.
    private void onSceneLoadCompleted(
        string sceneName,
        UnityEngine.SceneManagement.LoadSceneMode mode,
        System.Collections.Generic.List<ulong> clientsCompleted,
        System.Collections.Generic.List<ulong> clientsTimedOut)
    {
        Debug.Log($"[LobbyManager] onSceneLoadCompleted scene='{sceneName}' IsServer={IsServer}");

        if (sceneName != SceneNames.PlaygroundLevel) return;

        if (GameManager.Instance != null)
            GameManager.Instance.InitializeFromLobby();

        if (IsServer) spawnAllPlayersFromSnapshot();
    }

    /// Instancia los Players a partir del snapshot persistente guardado en GameManager.
    private void spawnAllPlayersFromSnapshot()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[LobbyManager] Falta asignar playerPrefab en el prefab Lobby.");
            return;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogError("[LobbyManager] GameManager.Instance es null al spawnear Players.");
            return;
        }

        var snapshot = GameManager.Instance.PendingPlayers;
        Debug.Log($"[LobbyManager] spawnAllPlayersFromSnapshot. snapshot.Count={snapshot.Count}");

        for (int i = 0; i < snapshot.Count; i++)
        {
            LobbyPlayerData data = snapshot[i];
            Vector3 spawnPos = getSpawnPositionForIndex(i);

            Debug.Log($"[LobbyManager] Spawneando Player[{i}] clientId={data.ClientId} char={data.CharacterIndex} pos={spawnPos}");

            GameObject playerInstance = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError("[LobbyManager] El playerPrefab no tiene NetworkObject.");
                Destroy(playerInstance);
                continue;
            }

            netObj.SpawnAsPlayerObject(data.ClientId, destroyWithScene: true);

            PlayerNetwork pn = playerInstance.GetComponent<PlayerNetwork>();
            if (pn != null) pn.ServerSetCharacterIndex(data.CharacterIndex);
        }
    }

    /// Devuelve una posición de spawn inicial separada para cada índice de jugador.
    private Vector3 getSpawnPositionForIndex(int index)
    {
        Vector2[] offsets = new Vector2[]
        {
        new Vector2(-2f, -2f),
        new Vector2( 2f, -2f),
        new Vector2(-2f,  2f),
        new Vector2( 2f,  2f),
        };
        Vector2 offset = offsets[index % offsets.Length];
        return new Vector3(offset.x, offset.y, -0.1f);
    }

    /// Devuelve los PlayerStats correspondientes al índice de personaje, o null si no hay.
    public PlayerStats GetStatsForIndex(int index)
    {
        if (characterStatsByIndex == null) return null;
        if (index < 0 || index >= characterStatsByIndex.Length) return null;
        return characterStatsByIndex[index];
    }

    /// Devuelve el MapConfig correspondiente al índice indicado.
    public MapConfig GetMapForIndex(int index)
    {
        if (availableMaps == null || availableMaps.Length == 0) return null;
        if (index < 0 || index >= availableMaps.Length) return null;
        return availableMaps[index];
    }

    /// Devuelve el MapConfig actualmente seleccionado.
    public MapConfig GetSelectedMap() => GetMapForIndex(SelectedMapIndex.Value);

    /// Devuelve la cantidad de mapas disponibles.
    public int GetMapCount() => availableMaps?.Length ?? 0;

    /// Devuelve el nombre visible de un mapa por índice.
    public string GetMapName(int index)
    {
        MapConfig m = GetMapForIndex(index);
        return m != null ? m.mapName : "—";
    }

    /// El Host pide cambiar el mapa seleccionado del lobby.
    [Rpc(SendTo.Server)]
    public void RequestSelectMapRpc(int mapIndex, RpcParams rpcParams = default)
    {
        if (mapIndex < 0 || mapIndex >= GetMapCount())
            return;

        Debug.Log($"[SERVER] Antes={SelectedMapIndex.Value}");

        SelectedMapIndex.Value = mapIndex;

        Debug.Log($"[SERVER] Despues={SelectedMapIndex.Value}");

        Debug.Log($"[LobbyManager] Map changed to index {mapIndex}");
    }

    [Header("Spawn de jugadores")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private PlayerStats[] characterStatsByIndex;
}