using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// UI del lobby en MainMenu. Refleja el listado sincronizado de jugadores,
/// gestiona la selección de personaje y arranca la partida si es Host.
public class LobbyUI : MonoBehaviour
{
    [Header("Slots de jugador (orden 0..3)")]
    [SerializeField] private TMP_Text[] slotNameTexts;
    [SerializeField] private TMP_Text[] slotCharacterTexts;

    [Header("Botones de personaje (orden 0..3)")]
    [SerializeField] private Button[] characterButtons;

    [Header("Botones de partida")]
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button disconnectButton;

    [Header("Mapeo personaje -> nombre visible")]
    [SerializeField] private string[] characterNames = { "Green", "Purple", "Red", "Yellow" };

    [Header("Mapa")]
    [SerializeField] private TMP_Dropdown mapDropdown;

    [Header("Colores de personaje (orden 0..3)")]
    [SerializeField] private Color[] characterColors =
    {
        Color.green,
        new Color(0.6f, 0f, 1f), // Purple
        Color.red,
        Color.yellow
    };

    private bool ignoreDropdownCallback;

    /// Suscribe la UI al lobby cuando este ya está disponible en red.
    private void Start()
    {
        /// Botones de personaje: el índice del array es el índice del personaje.
        for (int i = 0; i < characterButtons.Length; i++)
        {
            int index = i;
            characterButtons[i].onClick.AddListener(() => onCharacterClicked(index));
        }

        if (startGameButton != null) startGameButton.onClick.AddListener(onStartGameClicked);
        if (disconnectButton != null) disconnectButton.onClick.AddListener(onDisconnectClicked);

        if (mapDropdown != null) mapDropdown.onValueChanged.AddListener(onMapDropdownChanged);

        /// El LobbyManager puede no estar disponible justo al cargar la escena
        /// (el cliente puede entrar antes de que se replique). Reintentamos
        /// hasta que aparezca.
        InvokeRepeating(nameof(trySubscribeToLobby), 0f, 0.2f);
    }

    /// Limpia las suscripciones cuando la UI se destruye.
    private void OnDestroy()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnPlayersChanged -= refreshUI;
            LobbyManager.Instance.SelectedMapIndex.OnValueChanged -= onSelectedMapChanged;
        }
    }

    /// Intenta suscribirse al LobbyManager; si todavía no existe, lo reintenta.
    private void trySubscribeToLobby()
    {
        if (LobbyManager.Instance == null) return;

        LobbyManager.Instance.OnPlayersChanged += refreshUI;
        LobbyManager.Instance.SelectedMapIndex.OnValueChanged += onSelectedMapChanged;

        initializeMapDropdown();

        CancelInvoke(nameof(trySubscribeToLobby));
        refreshUI();
    }

    /// Refleja en el dropdown el nuevo mapa elegido (todos los clientes).
    private void onSelectedMapChanged(int previous, int current)
    {
        if (mapDropdown != null)
        {
            ignoreDropdownCallback = true;
            mapDropdown.SetValueWithoutNotify(current);
            ignoreDropdownCallback = false;
        }
    }

    /// Redibuja los slots, los botones de personaje y el botón Start.
    private void refreshUI()
    {
        LobbyManager lobby = LobbyManager.Instance;
        if (lobby == null) return;

        // Slots: pintar los jugadores en orden de insercion.
        for (int slot = 0; slot < slotNameTexts.Length; slot++)
        {
            if (slot < lobby.Players.Count)
            {
                LobbyPlayerData data = lobby.Players[slot];

                slotNameTexts[slot].text = $"Player {data.ClientId}";

                if (data.CharacterIndex >= 0)
                {
                    slotCharacterTexts[slot].text = characterNames[data.CharacterIndex];

                    // Color segun personaje
                    slotCharacterTexts[slot].color =
                        characterColors[data.CharacterIndex];
                }
                else
                {
                    slotCharacterTexts[slot].text = "(eligiendo...)";
                    slotCharacterTexts[slot].color = Color.white;
                }
            }
            else
            {
                slotNameTexts[slot].text = "—";
                slotCharacterTexts[slot].text = "";
            }
        }

        /// Botones de personaje: deshabilitar los ya tomados por otros.
        ulong localId = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.LocalClientId
            : 0;

        for (int i = 0; i < characterButtons.Length; i++)
        {
            bool takenByOther = false;
            for (int p = 0; p < lobby.Players.Count; p++)
            {
                if (lobby.Players[p].CharacterIndex == i && lobby.Players[p].ClientId != localId)
                {
                    takenByOther = true;
                    break;
                }
            }
            characterButtons[i].interactable = !takenByOther;
        }

        /// Botón Start: solo visible/activo en el Host y solo si todos han elegido.
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(isHost);
            startGameButton.interactable = isHost && lobby.AllPlayersHaveCharacter();
        }

        /// Solo el Host puede cambiar el mapa.
        if (mapDropdown != null)
        {
            bool hostCanChange = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
            mapDropdown.interactable = hostCanChange;
        }
    }

    /// El usuario pulsa un personaje: pide al servidor la asignación.
    private void onCharacterClicked(int characterIndex)
    {
        if (LobbyManager.Instance == null) return;
        LobbyManager.Instance.RequestSelectCharacterRpc(characterIndex);
    }

    /// El Host pulsa Start: arranca la partida.
    private void onStartGameClicked()
    {
        if (LobbyManager.Instance == null) return;
        LobbyManager.Instance.StartGameAsHost();
    }

    /// Cierra la sesión y vuelve a ConnectionScene.
    private void onDisconnectClicked()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        SceneManager.LoadScene(SceneNames.ConnectionScene);
    }

    /// El Host elige otro mapa en el dropdown: lo pide al servidor.
     /*
    private void onMapDropdownChanged(int index)
    {
        if (ignoreDropdownCallback) return;
        if (LobbyManager.Instance == null) return;

        if (!NetworkManager.Singleton.IsHost) return;

        LobbyManager.Instance.SetMapServer(index);
    }
    */

    private void onMapDropdownChanged(int index)
    {
        Debug.Log($"Dropdown -> {index}");

        if (ignoreDropdownCallback)
            return;

        if (LobbyManager.Instance == null)
            return;

        if (!NetworkManager.Singleton.IsHost)
            return;

        LobbyManager.Instance.RequestSelectMapRpc(index);
    }

    /// Rellena el dropdown con los mapas disponibles del lobby (una sola vez).
    private void initializeMapDropdown()
    {
        if (mapDropdown == null || LobbyManager.Instance == null) return;
        if (mapDropdown.options.Count > 0) return; // ya inicializado

        int count = LobbyManager.Instance.GetMapCount();
        var options = new System.Collections.Generic.List<TMP_Dropdown.OptionData>();
        for (int i = 0; i < count; i++)
        {
            options.Add(new TMP_Dropdown.OptionData(LobbyManager.Instance.GetMapName(i)));
        }
        mapDropdown.ClearOptions();
        mapDropdown.AddOptions(options);
    }
}