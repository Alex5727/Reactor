using UnityEngine;

// Va en la llave (con un Collider). Al tocarla el jugador, avisa a su habitación.
public class RoomKey : MonoBehaviour
{
    public SpecialRoom room;                 // si está vacío, usa la habitación padre
    public AudioClip pickupSound;            // opcional
    public GameObject pickupEffect;          // opcional

    [Header("Animación")]
    public float spinSpeed = 90f;
    public float bobHeight = 0.15f;
    public float bobSpeed = 2f;

    private Vector3 startLocalPos;

    void Awake()
    {
        if (room == null) room = GetComponentInParent<SpecialRoom>();

        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        startLocalPos = transform.localPosition;
    }

    void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        transform.localPosition = startLocalPos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
    }

    void OnTriggerEnter(Collider other)
    {
        if (room == null || !room.IsPlayer(other)) return;

        // Solo se puede agarrar cuando la habitación ya está cerrada
        if (!room.CollectKey()) return;

        if (pickupSound != null) AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        if (pickupEffect != null) Instantiate(pickupEffect, transform.position, Quaternion.identity);

        gameObject.SetActive(false);
    }
}