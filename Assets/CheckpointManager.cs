
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Uno solo por escena.
// Ya NO recarga la escena: al morir el jugador, reinicia en el sitio todo lo que no esté asegurado
// por un checkpoint (enemigos y habitaciones especiales) y coloca al jugador en el último checkpoint.
public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    [Header("Efecto de muerte")]
    public Image deathOverlay;
    [Range(0f, 1f)]
    public float maxRedAlpha = 0.45f;
    public AudioSource audioSource;
    public AudioClip SonidoMuerte;

    public float pulseSpeed = 4f;
    public float fadeOutDuration = 0.6f;

    [Header("UI")]
    public GameObject miraCanceladaUI;
    public GameObject miraUI;



    public string playerTag = "Player";
    public float respawnDelay = 1.5f;        // tiempo entre morir y reaparecer
    public bool debugLog = false;            // activa mensajes en la consola para ver qué pasa

    private Transform player;
    private CharacterController playerController;

    private Vector3 initialPosition;         // donde empieza el jugador si muere antes de tocar un checkpoint
    private float initialYaw;

    private Checkpoint currentCheckpoint;
    private Vector3 respawnPosition;
    private float respawnYaw;

    private bool respawning;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p == null)
        {
            Debug.LogWarning("CheckpointManager: no encontré al jugador (tag '" + playerTag + "').", this);
            return;
        }

        player = p.transform;
        playerController = p.GetComponent<CharacterController>();

        initialPosition = player.position;
        initialYaw = player.eulerAngles.y;
    }

    // Lo llama el checkpoint cuando el jugador lo toca
    public void ActivateCheckpoint(Checkpoint cp)
    {
        if (respawning) return;

        bool isNew = currentCheckpoint != cp;

        currentCheckpoint = cp;
        respawnPosition = cp.SpawnPosition;
        respawnYaw = cp.SpawnYaw;

        // Asegura lo que pasó hasta ahora: enemigos muertos y habitaciones superadas
        int enemies = TrackedEnemy.CommitDeaths();
        int rooms = 0;
        foreach (SpecialRoom room in SpecialRoom.All)
            if (room.CommitCompletion()) rooms++;

        if (isNew)
        {
            foreach (Checkpoint other in Checkpoint.All)
                other.SetActiveVisual(other == cp);
            cp.PlayActivateSound();
        }

        if (debugLog)
            Debug.Log("[Checkpoint] Tocado '" + cp.name + "'. Enemigos asegurados: " + enemies + ", habitaciones aseguradas: " + rooms);
    }

    // Lo llama PlayerHealth cuando el jugador muere
    public void OnPlayerDied()
    {
        if (respawning) return;

        respawning = true;

        // Bloquear disparos inmediatamente

        //sonido morir
        audioSource.PlayOneShot(SonidoMuerte);

        // Iniciar el efecto visual
        if (deathOverlay != null)
            StartCoroutine(ScreenDeathEffect());

        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSecondsRealtime(respawnDelay);

        // --- Mundo: vuelve a su estado del último checkpoint ---
        int enemies = TrackedEnemy.RespawnAll();

        foreach (SpecialRoom room in SpecialRoom.All)
            room.ResetRoom();

        if (SpecialRoomManager.Instance != null)
        {
            SpecialRoomManager.Instance.CancelAll();   // quita el bloqueo de disparo y el contador
            miraCanceladaUI.SetActive(false);
            miraUI.SetActive(true);

        }

        ClearBullets();

        // --- Jugador ---
        Vector3 pos = currentCheckpoint != null ? respawnPosition : initialPosition;
        float yaw = currentCheckpoint != null ? respawnYaw : initialYaw;
        PlacePlayer(pos, yaw);

        

        // Avisa al jugador para que recupere la vida: llama al método Revive() del script que la controle
        // (PlayerMovement, PlayerHealth o el que sea). Si no tiene ninguno, simplemente no pasa nada.
        if (player != null) player.SendMessage("Revive", SendMessageOptions.DontRequireReceiver);

        if (debugLog)
            Debug.Log("[Checkpoint] Reaparecido. Enemigos recreados: " + enemies +
                      (currentCheckpoint != null ? " en '" + currentCheckpoint.name + "'" : " (sin checkpoint, en el inicio)"));


        RemoveHealingObjects();

        respawning = false;
    }

    void PlacePlayer(Vector3 pos, float yaw)
    {
        if (player == null) return;

        if (playerController != null) playerController.enabled = false;   // si está activo, ignora cambios de posición
        player.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        if (playerController != null) playerController.enabled = true;

        Physics.SyncTransforms();
    }

    // Quita las balas que siguen en el aire para que no golpeen al jugador al reaparecer
    void ClearBullets()
    {
#if UNITY_2022_2_OR_NEWER
        Bullet[] bullets = FindObjectsByType<Bullet>(FindObjectsSortMode.None);
#else
        Bullet[] bullets = FindObjectsOfType<Bullet>();
#endif
        foreach (Bullet b in bullets) Destroy(b.gameObject);
    }

    IEnumerator ScreenDeathEffect()
    {
        // Parpadeo rojo mientras esperamos el respawn
        while (respawning)
        {
            float alpha = Mathf.Lerp(
                0f,
                maxRedAlpha,
                (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f
            );

            deathOverlay.color = new Color(1f, 0f, 0f, alpha);

            yield return null;
        }

        // Desvanecer el rojo cuando el jugador reaparezca
        float startAlpha = deathOverlay.color.a;
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float alpha = Mathf.Lerp(
                startAlpha,
                0f,
                elapsed / fadeOutDuration
            );

            deathOverlay.color = new Color(1f, 0f, 0f, alpha);

            yield return null;
        }

        deathOverlay.color = new Color(1f, 0f, 0f, 0f);
    }






    void RemoveHealingObjects()
    {
        GameObject[] healingObjects = GameObject.FindGameObjectsWithTag("Healing");

        foreach (GameObject obj in healingObjects)
        {
            Destroy(obj);
        }
    }

}