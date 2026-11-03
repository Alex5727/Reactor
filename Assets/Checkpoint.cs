using System.Collections.Generic;
using UnityEngine;

// Va en cada checkpoint, con un Collider (se marca como trigger solo).
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    public static readonly List<Checkpoint> All = new List<Checkpoint>();

    [Header("Reaparición")]
    public Transform respawnPoint;           // empty donde queda el CENTRO del jugador (si está vacío usa este objeto)

    [Header("Visual (opcional)")]
    public GameObject inactiveVisual;        // se ve mientras NO es el checkpoint actual
    public GameObject activeVisual;          // se ve cuando ES el checkpoint actual
    public AudioClip activateSound;

    public string playerTag = "Player";

    public Vector3 SpawnPosition => (respawnPoint != null ? respawnPoint : transform).position;
    public float SpawnYaw => (respawnPoint != null ? respawnPoint : transform).eulerAngles.y;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        SetActiveVisual(false);
    }

    void OnEnable()
    {
        All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag) && !other.transform.root.CompareTag(playerTag)) return;

        if (CheckpointManager.Instance != null)
            CheckpointManager.Instance.ActivateCheckpoint(this);
    }

    public void SetActiveVisual(bool active)
    {
        if (activeVisual != null) activeVisual.SetActive(active);
        if (inactiveVisual != null) inactiveVisual.SetActive(!active);
    }

    public void PlayActivateSound()
    {
        if (activateSound != null) AudioSource.PlayClipAtPoint(activateSound, transform.position);
    }

    // Muestra dónde quedará el jugador (cápsula de 2 m de alto)
    void OnDrawGizmos()
    {
        Transform p = respawnPoint != null ? respawnPoint : transform;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(p.position + Vector3.up * 0.5f, 0.5f);
        Gizmos.DrawWireSphere(p.position - Vector3.up * 0.5f, 0.5f);
        Gizmos.DrawRay(p.position, p.forward * 1.5f);
    }
}