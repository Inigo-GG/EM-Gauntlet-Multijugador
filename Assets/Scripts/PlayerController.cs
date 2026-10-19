using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : CharController
{
    protected int damageToEnemy;
    protected float attackCooldown;

    private PlayerControls controls;

    /// Indica si este PlayerController pertenece al cliente local.
    public bool IsLocalPlayer { get; set; } = false;

    public bool IsAttacking { get; private set; } = false;
    public int DamageToEnemy => damageToEnemy;

    /// Inicializa componentes base. La activación del input se hace cuando
    /// PlayerNetwork confirma que este controller pertenece al cliente local.
    protected override void Awake()
    {
        base.Awake();
        controls = new PlayerControls();
        controls.Player.Move.performed += ctx => movement = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += _ => movement = Vector2.zero;
    }

    /// Inicializa estado del jugador y notifica los valores iniciales al HUD si es local.
    protected override void Start()
    {
        base.Start();

        if (IsLocalPlayer)
        {
            GameEvents.HealthChanged(health);
            GameEvents.KeysChanged();
            GameEvents.DiamondsChanged();
        }

        IsAttacking = false;
    }

    /// Actualiza animación y orientación. El input solo se aplica si es el jugador local.
    protected override void Update()
    {
        animator.SetFloat("speed", movement.sqrMagnitude);

        if (movement.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
        }

        if (IsLocalPlayer) checkDeath();
    }

    /// Activa los controles del jugador local. Llamado desde PlayerNetwork.OnNetworkSpawn.
    public void EnableLocalInput()
    {
        IsLocalPlayer = true;
        controls.Enable();
        controls.Player.Attack.performed += onAttack;
    }

    /// Desactiva los controles si estaban activos.
    public void DisableLocalInput()
    {
        if (!IsLocalPlayer) return;
        controls.Player.Attack.performed -= onAttack;
        controls.Disable();
        IsLocalPlayer = false;
    }

    /// Asegura que los controles se desactivan si el objeto se destruye.
    private void OnDisable()
    {
        if (IsLocalPlayer)
        {
            controls.Player.Attack.performed -= onAttack;
            controls.Disable();
        }
    }

    /// Gestiona la muerte del jugador local y lanza el flujo de fin de partida.
    public override void Die()
    {
        base.Die();

        if (IsLocalPlayer)
        {
            GameEvents.PlayerDied();
            GameManager.Instance?.TriggerGameOver();
        }
    }

    /// Aplica daño al jugador y notifica el cambio de salud al HUD si es local.
    public override void TakeDamage(int amount, Vector2 knockbackDir)
    {
        base.TakeDamage(amount, knockbackDir);
        if (IsLocalPlayer) GameEvents.HealthChanged(health);
    }

    /// Aplica un conjunto de estadísticas de personaje y recarga sus valores activos.
    public void ApplyCharacterStats(PlayerStats newStats)
    {
        if (newStats == null) return;
        stats = newStats;
        LoadStats();
    }

    /// Carga estadísticas del personaje, aplicando bonus y stats específicas del jugador.
    protected override void LoadStats()
    {
        base.LoadStats();

        PlayerStats playerStats = stats as PlayerStats;
        if (playerStats != null)
        {
            moveSpeed *= playerStats.speedBonus;
            damageToEnemy = playerStats.attackDamage;
            attackCooldown = playerStats.attackCooldown;
        }
        else
        {
            damageToEnemy = 50;
            attackCooldown = 0.5f;
            moveSpeed *= 1.25f;
        }
    }

    /// Verifica si la salud ha llegado a cero y ejecuta la muerte una sola vez.
    private void checkDeath()
    {
        if (health <= 0 && !isDead) Die();
    }

    /// Inicia la animación de ataque del jugador local y programa su final.
    private void onAttack(InputAction.CallbackContext context)
    {
        animator.SetTrigger("Attack");
        IsAttacking = true;
        Invoke(nameof(endAttack), attackCooldown);
    }

    /// Finaliza el estado de ataque del jugador.
    private void endAttack()
    {
        IsAttacking = false;
    }
}