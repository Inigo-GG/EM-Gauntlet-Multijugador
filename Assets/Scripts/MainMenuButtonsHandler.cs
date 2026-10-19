using Unity.Netcode;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuButtonsHandler : MonoBehaviour
{
    [Header("Map Configs disponibles")]
    [SerializeField] private MapConfig[] availableMaps;

    [Header("UI")]
    [SerializeField] private TMP_Dropdown mapsDropdown;

    private bool isInitialized;

    /// <summary>
    /// Inicializa el dropdown de mapas al cargar el menú principal.
    /// </summary>
    private void Start()
    {
        initializeMapDropdown();
    }

    /// <summary>
    /// Libera la suscripción del dropdown al destruir el objeto.
    /// </summary>
    private void OnDestroy()
    {
        if (mapsDropdown != null)
            mapsDropdown.onValueChanged.RemoveListener(onMapDropdownChanged);
    }

    /// <summary>
    /// Navega a la escena de selección de personaje si hay mapa seleccionado.
    /// </summary>
    public void OnButtonPlayClicked()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[MainMenu] GameManager no existe.");
            return;
        }

        if (GameManager.Instance?.SelectedMapConfig == null)
        {
            Debug.LogWarning("[MainMenu] No hay mapa seleccionado.");
            return;
        }

        SceneManager.LoadScene(SceneNames.CharSelection);
    }

    /// <summary>
    /// Registra la acción del botón de opciones del menú principal.
    /// </summary>
    public void OnOptionsButtonClicked()
    {
        Debug.Log("Options button pressed");
    }

    /// <summary>
    /// Cierra la aplicación o detiene la ejecución en el editor.
    /// </summary>
    public void OnExitButtonClicked()
    {
        Debug.Log("Exit button pressed");
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Configura las opciones del dropdown y establece el mapa inicial seleccionado.
    /// </summary>
    private void initializeMapDropdown()
    {
        if (isInitialized) return;

        if (mapsDropdown == null || availableMaps == null || availableMaps.Length == 0)
        {
            Debug.LogWarning("[MainMenu] Dropdown de mapas no configurado.");
            return;
        }

        isInitialized = true;

        mapsDropdown.onValueChanged.RemoveListener(onMapDropdownChanged);

        mapsDropdown.ClearOptions();

        var options = new List<TMP_Dropdown.OptionData>();

        for (int i = 0; i < availableMaps.Length; i++)
        {
            string name = availableMaps[i] != null ? availableMaps[i].mapName : "Sin nombre";
            options.Add(new TMP_Dropdown.OptionData(name));
        }

        mapsDropdown.AddOptions(options);

        // IMPORTANTE: primero listener, luego value
        mapsDropdown.onValueChanged.AddListener(onMapDropdownChanged);

        int startIndex = 0;

        // si ya había selección previa, la respetas
        if (GameManager.Instance != null && GameManager.Instance.SelectedMapConfig != null)
        {
            for (int i = 0; i < availableMaps.Length; i++)
            {
                if (availableMaps[i] == GameManager.Instance.SelectedMapConfig)
                {
                    startIndex = i;
                    break;
                }
            }
        }

        mapsDropdown.SetValueWithoutNotify(startIndex);
        //applySelectedMap(startIndex);
    }

    /// <summary>
    /// Aplica el mapa seleccionado cuando cambia el valor del dropdown.
    /// </summary>
    private void onMapDropdownChanged(int index)
    {
        applySelectedMap(index);
    }

    /// <summary>
    /// Guarda en GameManager el mapa correspondiente al índice indicado.
    /// </summary>
    private void applySelectedMap(int index)
    {
        if (LobbyManager.Instance == null) return;

        //GameManager.Instance.SelectedMapConfig = availableMaps[index];

        LobbyManager.Instance.RequestSelectMapRpc(index);

        Debug.Log($"[MainMenu] Map selected: {index}");
    }
}
