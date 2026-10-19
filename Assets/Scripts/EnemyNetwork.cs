using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(EnemyController))]
public class EnemyNetwork : NetworkBehaviour
{
    private EnemyController enemyController; // Referencia a enemyController para acceder a el
    
    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
    }

    private void Update()
    {
        if (!IsServer) // Solo el servidor ejecuta
            return;

        // Aqui la IA
    }
}
