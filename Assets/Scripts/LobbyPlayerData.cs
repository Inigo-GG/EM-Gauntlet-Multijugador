using System;
using Unity.Netcode;

/// Datos sincronizados de cada jugador conectado al lobby.
/// Implementa INetworkSerializable para poder viajar dentro de un NetworkList.
[Serializable]
public struct LobbyPlayerData : INetworkSerializable, IEquatable<LobbyPlayerData>
{
    ///Identificador de cliente asignado por NetworkManager.
    public ulong ClientId;

    ///Índice del personaje elegido (-1 = no elegido).
    public int CharacterIndex;

    ///Indica si el jugador ha marcado "listo".
    public bool IsReady;

    /// Crea un dato de jugador con personaje sin elegir.
    public LobbyPlayerData(ulong clientId)
    {
        ClientId = clientId;
        CharacterIndex = -1;
        IsReady = false;
    }

    /// Serializa los campos para envío por red.
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref CharacterIndex);
        serializer.SerializeValue(ref IsReady);
    }

    /// Compara dos datos por igualdad de campos para el NetworkList.
    public bool Equals(LobbyPlayerData other)
    {
        return ClientId == other.ClientId
            && CharacterIndex == other.CharacterIndex
            && IsReady == other.IsReady;
    }
}