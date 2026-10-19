using Unity.Netcode;
using UnityEngine;

/// Componente de red que pega el PlayerController al ciclo de vida de NGO.
/// Sincroniza la apariencia (personaje elegido), activa el input del jugador
/// local y registra el jugador en GameManager.
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(NetworkObject))]
public class PlayerNetwork : NetworkBehaviour
{
    /// Índice del personaje elegido (0..3). Solo el servidor lo escribe;
    /// todos los clientes lo leen.
    public NetworkVariable<int> CharacterIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private PlayerController playerController;

    /// Cachea la referencia al PlayerController.
    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    /// Cuando el objeto aparece en red: aplica apariencia, activa input local
    /// si es el dueño y registra el jugador en GameManager.
    public override void OnNetworkSpawn()
    {
        Debug.Log($"[PlayerNetwork] OnNetworkSpawn. IsOwner={IsOwner}, OwnerClientId={OwnerClientId}, CharacterIndex={CharacterIndex.Value}");

        /// Suscribirse a futuros cambios del índice (por si llega después del spawn).
        CharacterIndex.OnValueChanged += onCharacterIndexChanged;

        /// Aplicar la apariencia ya, si el servidor escribió el índice antes del spawn.
        if (CharacterIndex.Value >= 0)
        {
            applyCharacterAppearance(CharacterIndex.Value);
        }

        if (IsOwner)
        {
            playerController.EnableLocalInput();

            if (GameManager.Instance != null)
            {
                UniqueEntity entity = GetComponent<UniqueEntity>();
                GameManager.Instance.RegisterLocalPlayer(playerController, entity);
            }
        }
    }

    /// Libera la suscripción y desactiva input local al despawnearse el objeto.
    public override void OnNetworkDespawn()
    {
        CharacterIndex.OnValueChanged -= onCharacterIndexChanged;
        if (IsOwner) playerController.DisableLocalInput();
    }

    /// Asigna el índice de personaje (solo servidor). El cambio se replica a todos.
    public void ServerSetCharacterIndex(int index)
    {
        if (!IsServer) return;
        CharacterIndex.Value = index;
    }

    /// Callback ejecutado en todos los clientes cuando cambia el índice.
    private void onCharacterIndexChanged(int previous, int current)
    {
        applyCharacterAppearance(current);
    }

    /// Aplica los PlayerStats correspondientes al índice indicado.
    private void applyCharacterAppearance(int index)
    {
        if (LobbyManager.Instance == null) return;

        PlayerStats stats = LobbyManager.Instance.GetStatsForIndex(index);
        if (stats == null) return;

        playerController.ApplyCharacterStats(stats);
    }
}