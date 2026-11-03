using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// Va en la raíz de cada habitación especial, junto con un Collider que cubra el interior.
[RequireComponent(typeof(Collider))]
public class SpecialRoom : MonoBehaviour
{
    public static readonly List<SpecialRoom> All = new List<SpecialRoom>();

    [Header("Efecto")]
    public AudioSource audioSource;
    public AudioClip SonidoCerrar;
    public AudioClip SonidoAbrir;

    [Header("Mensaje UI")]
    public GameObject mensajeCierreUI;
    public GameObject miraCanceladaUI;
    public GameObject miraUI;
    public float duracionMensaje = 3f;

    private Coroutine mensajeCoroutine;

    [Header("Puertas")]
    public Transform[] doorPoints;           // un empty por cada puerta (posición y rotación)
    public GameObject openDoorPrefab;
    public GameObject closedDoorPrefab;

    [Header("Reglas")]
    public float countdownSeconds = 15f;     // contador que empieza al agarrar la llave
    public float lockDelay = 0.4f;           // espera antes de cerrar las puertas (que el jugador entre del todo)
    public bool oneTimeOnly = true;          // false = se puede volver a activar al reentrar
    public string playerTag = "Player";

    [Header("Llave")]
    public GameObject key;                   // opcional: si está vacío, usa el hijo que tenga RoomKey

    private enum RoomState { Waiting, Locking, Locked, Countdown, Done }

    private RoomState state = RoomState.Waiting;
    private bool playerInside;
    private GameObject[] spawnedDoors;

    // Progreso para los checkpoints
    private bool completedPending;           // superada después del último checkpoint
    private bool completedCommitted;         // superada antes del último checkpoint: se queda superada

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        if (key == null)
        {
            RoomKey k = GetComponentInChildren<RoomKey>(true);
            if (k != null) key = k.gameObject;
        }
    }

    void OnEnable()
    {
        All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void Start()
    {
        if (SpecialRoomManager.Instance == null)
            Debug.LogWarning("SpecialRoom: no hay un SpecialRoomManager en la escena.", this);

        SetDoors(false);
    }

    void OnDestroy()
    {
        // Si se destruye con el jugador dentro, no dejar el disparo bloqueado
        if ((state == RoomState.Locked || state == RoomState.Countdown) && SpecialRoomManager.Instance != null)
            SpecialRoomManager.Instance.RemoveLock();
    }

    // ---------------------------------------------------------------- Entrada / salida

    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        playerInside = true;

        if (state == RoomState.Waiting) StartCoroutine(LockRoutine());
    }

    void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other)) playerInside = false;
    }

    public bool IsPlayer(Collider other)
    {
        return other.CompareTag(playerTag) || other.transform.root.CompareTag(playerTag);
    }

    IEnumerator LockRoutine()
    {
        state = RoomState.Locking;
        yield return new WaitForSeconds(lockDelay);

        // Si el jugador se echó para atrás antes de que cerraran, se cancela
        if (!playerInside)
        {
            state = RoomState.Waiting;
            yield break;
        }


        state = RoomState.Locked;
        SetDoors(true);



        MostrarMensajeCierre();

        // La mira cancelada permanece visible mientras la sala esté cerrada
        ActivarMiraCancelada(true);


        audioSource.PlayOneShot(SonidoCerrar);

        if (SpecialRoomManager.Instance != null) SpecialRoomManager.Instance.AddLock();
    }

    // ---------------------------------------------------------------- Llave y contador

    // Lo llama la llave. Devuelve true si se aceptó (solo vale con la habitación cerrada).
    public bool CollectKey()
    {
        if (state != RoomState.Locked) return false;

        state = RoomState.Countdown;

        if (SpecialRoomManager.Instance != null)
            SpecialRoomManager.Instance.StartCountdown(countdownSeconds, Unlock);
        else
            Unlock();

        return true;
    }

    void Unlock()
    {
        if (this == null) return;   // la habitación fue destruida mientras corría el contador

        SetDoors(false);

        ActivarMiraCancelada(false);

        audioSource.PlayOneShot(SonidoAbrir);

        if (SpecialRoomManager.Instance != null) SpecialRoomManager.Instance.RemoveLock();

        if (oneTimeOnly)
        {
            state = RoomState.Done;
            completedPending = true;
        }
        else
        {
            state = RoomState.Waiting;
            if (key != null) key.SetActive(true);
        }

    }

    // ---------------------------------------------------------------- Checkpoints

    // Al tocar un checkpoint: si ya la había superado, queda superada para siempre.
    // Devuelve true si acaba de asegurarse.
    public bool CommitCompletion()
    {
        if (!completedPending) return false;

        completedPending = false;
        completedCommitted = true;
        return true;
    }

    // Al reaparecer el jugador: deja la habitación como nueva, salvo que la haya superado
    // antes del último checkpoint.
    public void ResetRoom()
    {
        if (completedCommitted) return;

        StopAllCoroutines();

        state = RoomState.Waiting;
        playerInside = false;
        completedPending = false;

        SetDoors(false);
        if (key != null) key.SetActive(true);
    }

    // ---------------------------------------------------------------- Puertas

    void SetDoors(bool closed)
    {
        if (doorPoints == null) return;
        if (spawnedDoors == null || spawnedDoors.Length != doorPoints.Length)
            spawnedDoors = new GameObject[doorPoints.Length];

        GameObject prefab = closed ? closedDoorPrefab : openDoorPrefab;

        for (int i = 0; i < doorPoints.Length; i++)
        {
            if (spawnedDoors[i] != null) Destroy(spawnedDoors[i]);
            if (prefab == null || doorPoints[i] == null) continue;

            spawnedDoors[i] = Instantiate(prefab, doorPoints[i].position, doorPoints[i].rotation, doorPoints[i]);
        }
    }


    void MostrarMensajeCierre()
    {
        if (mensajeCierreUI == null) return;

        if (mensajeCoroutine != null)
            StopCoroutine(mensajeCoroutine);

        mensajeCoroutine = StartCoroutine(MensajeCierreTemporal());
    }

    IEnumerator MensajeCierreTemporal()
    {
        mensajeCierreUI.SetActive(true);

        yield return new WaitForSeconds(duracionMensaje);

        if (mensajeCierreUI != null)
            mensajeCierreUI.SetActive(false);

        mensajeCoroutine = null;
    }

    void ActivarMiraCancelada(bool activar)
    {
        if (miraCanceladaUI != null)
            miraCanceladaUI.SetActive(activar);
            miraUI.SetActive(!activar);
    }


}