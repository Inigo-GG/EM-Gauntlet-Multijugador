using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

/// Gestiona la pantalla de conexión: arranca el Host o el Client de NGO
/// según el botón pulsado y muestra el estado de conexión al usuario.
public class ConnectionMenuHandler : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private TMP_InputField portInputField;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Valores por defecto")]
    [SerializeField] private string defaultIp = "127.0.0.1";
    [SerializeField] private ushort defaultPort = 7777;

    [Header("Network")]
    [SerializeField] private GameObject lobbyPrefab;

    /// Inicializa la UI y se suscribe a los eventos del NetworkManager.
    private void Start()
    {
        DontDestroyOnLoad(gameObject);

        if (ipInputField != null) ipInputField.text = defaultIp;
        if (portInputField != null) portInputField.text = defaultPort.ToString();

        hostButton.onClick.AddListener(onHostClicked);
        clientButton.onClick.AddListener(onClientClicked);
        if (quitButton != null) quitButton.onClick.AddListener(onQuitClicked);

        setStatus("Selecciona Host para crear partida o Client para unirte.");

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted += onServerStarted;
            NetworkManager.Singleton.OnClientConnectedCallback += onClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += onClientDisconnected;
        }
        else
        {
            setStatus("ERROR: no se encontró NetworkManager en la escena.");
        }
    }

    /// Libera las suscripciones a los eventos del NetworkManager.
    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= onServerStarted;
            NetworkManager.Singleton.OnClientConnectedCallback -= onClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= onClientDisconnected;
        }
    }

    /// Inicia el modo Host (servidor + cliente local).
    private void onHostClicked()
    {
        if (!tryApplyTransportConfig()) return;

        setStatus("Iniciando Host...");
        setButtonsInteractable(false);

        if (!NetworkManager.Singleton.StartHost())
        {
            setStatus("No se pudo iniciar el Host.");
            setButtonsInteractable(true);
        }
    }

    /// Inicia el modo Client e intenta conectarse al servidor indicado.
    private void onClientClicked()
    {
        if (!tryApplyTransportConfig()) return;

        setStatus("Conectando como Client...");
        setButtonsInteractable(false);

        if (!NetworkManager.Singleton.StartClient())
        {
            setStatus("No se pudo iniciar el Client.");
            setButtonsInteractable(true);
        }
    }

    /// Cierra la aplicación o detiene la ejecución en el editor.
    private void onQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// Lee la IP y el puerto de la UI y los aplica al UnityTransport del NetworkManager.
    private bool tryApplyTransportConfig()
    {
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            setStatus("Falta el componente UnityTransport en NetworkManager.");
            return false;
        }

        string ip = (ipInputField != null && !string.IsNullOrWhiteSpace(ipInputField.text))
            ? ipInputField.text.Trim()
            : defaultIp;

        ushort port = defaultPort;
        if (portInputField != null && !string.IsNullOrWhiteSpace(portInputField.text))
        {
            if (!ushort.TryParse(portInputField.text.Trim(), out port))
            {
                setStatus("Puerto no válido.");
                return false;
            }
        }

        // El tercer parámetro es la dirección de escucha del servidor.
        // "0.0.0.0" hace que el Host escuche en todas las interfaces (útil para LAN).
        transport.SetConnectionData(ip, port, "0.0.0.0");
        return true;
    }

    /// Callback de NGO disparado cuando el servidor arranca correctamente.
    /// Spawnea el Lobby en red y pide a NGO que cargue MainMenu para todos.
    private void onServerStarted()
    {
        setStatus("Host iniciado. Cargando lobby...");

        // Suscribirse para spawnear el Lobby justo cuando MainMenu termine de cargar.
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += onMainMenuLoaded;

        NetworkManager.Singleton.SceneManager.LoadScene(
            SceneNames.MainMenu,
            UnityEngine.SceneManagement.LoadSceneMode.Single
        );
    }

    /// Tras cargar MainMenu en red, spawnea el prefab Lobby como NetworkObject.
    private void onMainMenuLoaded(
        string sceneName,
        UnityEngine.SceneManagement.LoadSceneMode loadSceneMode,
        System.Collections.Generic.List<ulong> clientsCompleted,
        System.Collections.Generic.List<ulong> clientsTimedOut)
    {
        if (sceneName != SceneNames.MainMenu) return;

        // Esta suscripción solo nos interesa una vez.
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= onMainMenuLoaded;

        Debug.Log($"[Connection] MainMenu cargada. lobbyPrefab asignado? {lobbyPrefab != null}");

        if (lobbyPrefab == null)
        {
            Debug.LogError("[Connection] Falta asignar lobbyPrefab en el Inspector.");
            return;
        }

        GameObject lobbyInstance = Instantiate(lobbyPrefab);
        NetworkObject netObj = lobbyInstance.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("[Connection] El prefab Lobby no tiene NetworkObject.");
            return;
        }

        netObj.Spawn(false);
        Debug.Log("[Connection] Lobby spawneado en red dentro de MainMenu.");
    }

    /// Callback disparado cuando un cliente se conecta correctamente.
    /// En el servidor se llama por cada cliente; en el cliente, solo para sí mismo.
    private void onClientConnected(ulong clientId)
    {
        bool isLocalClient = clientId == NetworkManager.Singleton.LocalClientId;
        bool isPureClient = NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer;

        if (isLocalClient && isPureClient)
        {
            setStatus("Conectado al Host.");
            // El cambio de escena lo dirige el servidor; este cliente será arrastrado automáticamente.
        }
    }

    /// Callback disparado cuando un cliente se desconecta.
    /// Si es el cliente local, muestra el motivo y reactiva los botones.
    private void onClientDisconnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            string reason = NetworkManager.Singleton.DisconnectReason;
            setStatus(string.IsNullOrEmpty(reason)
                ? "Desconectado."
                : $"Desconectado: {reason}");
            setButtonsInteractable(true);
        }
    }

    /// Actualiza el texto de estado de la UI y lo replica en consola.
    private void setStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log($"[Connection] {message}");
    }

    /// Habilita o deshabilita los botones de Host y Client.
    private void setButtonsInteractable(bool value)
    {
        if (hostButton != null) hostButton.interactable = value;
        if (clientButton != null) clientButton.interactable = value;
    }
}