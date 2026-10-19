# EM Gauntlet – Conversión a multijugador

Dungeon crawler _top-down_ en Unity 6 (C#) convertido de un juego de un solo jugador a un **juego en red de hasta 4 jugadores** con **Netcode for GameObjects** y Unity Transport. Práctica obligatoria de la asignatura **Entornos Multijugador** (curso 2025/26, Grado en Diseño y Desarrollo de Videojuegos, URJC), realizada en equipo de 5 personas.

> El juego base (singleplayer, inspirado en *Gauntlet*, Atari 1985) fue proporcionado por la asignatura. Todo el trabajo de red descrito aquí es la conversión a multijugador.

## Qué se hizo

- **Modelo de red:** _listen server_ con servidor autoritativo. El Host juega como un cliente más y valida todas las acciones que modifican el mundo (generación del mapa, enemigos, objetos, colisiones importantes y condición de victoria).
- **Sincronización:** `NetworkVariable`, `NetworkList`, `ClientRpc` y `NetworkObject` para vida, ataque, llaves, diamantes, inventario por jugador y estado de las puertas.
- **Escenas en red:** carga de escenas desde el servidor con `NetworkManager.SceneManager` para que todos los clientes permanezcan sincronizados.
- **Mapa procedural:** generado únicamente en el servidor y compartido con los clientes mediante semilla.

### Reparto por fases

| Fase | Contenido | Responsable |
|---|---|---|
| 1 | Host/Client por IP y puerto, lobby, selección de personaje, mapa procedural y movimiento sincronizado | **Íñigo García** |
| 2 | Segundo bloque de requisitos de la práctica (ver memoria) | Silvia Álvaro |
| 3 | Tercer bloque de requisitos: inventario por jugador, puertas y escenas finales de victoria y derrota, entre otros | Henar San Román, Sara Sanz y Julia Moya |

Todo el equipo colaboró en la resolución de problemas entre fases. Más detalle en la memoria de la práctica.

## Tecnologías

Unity 6 LTS · C# · Netcode for GameObjects · Unity Transport · Unity Input System · TextMeshPro

## Ejecución

1. Abre el proyecto con **Unity 6 LTS** desde Unity Hub.
2. Si Unity lo pide al abrir, importa **TMP Essential Resources** (*Window → TextMeshPro → Import TMP Essential Resources*): no se incluyen en el repositorio.
3. Abre la escena `ConnectionScene`.
4. Para probar en local: ejecuta una instancia como **Host** y otra como **Client** (misma IP y puerto), o usa un build y el editor a la vez.

## Equipo

Julia Moya, Henar San Román, Sara Sanz, Silvia Álvaro e Íñigo García.

## Créditos

Arte de [Kenney.nl](https://kenney.nl) (*Scribble Dungeons* y *Scribble Platformer*), licencia CC0. Juego base proporcionado por la asignatura (licencia MIT).
