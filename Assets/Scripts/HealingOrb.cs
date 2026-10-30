
using UnityEngine;

public class HealingOrb : MonoBehaviour
{
    [Header("Curación")]
    public int healAmount = 15;

    [Header("Movimiento")]
    public float floatAmplitude = 0.15f;
    public float floatSpeed = 2f;
    public float rotationSpeed = 90f;

    [Header("Sonido")]
    public AudioClip SonidoHealing;

    //caida
    private float gravity = 15f;
    private float groundOffset = 0.5f;
    private float raycastDistance = 100f;

    private float verticalVelocity = 0f;
    private bool hasLanded = false;


    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        if (!hasLanded)
        {
            if (Physics.Raycast(
                transform.position,
                Vector3.down,
                out RaycastHit hit,
                raycastDistance))
            {
                float targetY = hit.point.y + groundOffset;

                // Aplicar gravedad hasta llegar a la altura deseada
                if (transform.position.y > targetY)
                {
                    verticalVelocity -= gravity * Time.deltaTime;

                    Vector3 position = transform.position;
                    position.y += verticalVelocity * Time.deltaTime;

                    // Evitar que atraviese el suelo
                    position.y = Mathf.Max(position.y, targetY);

                    transform.position = position;
                }
                else
                {
                    hasLanded = true;
                    startPosition = transform.position;
                }

                if (transform.position.y <= targetY)
                {
                    hasLanded = true;
                    startPosition = transform.position;
                }
            }
        }
        else
        {
            // Flotar suavemente después de caer
            float offset = Mathf.Sin(Time.time * floatSpeed)
                           * floatAmplitude;

            transform.position = startPosition
                               + Vector3.up * offset;
        }

        // Girar constantemente
        transform.Rotate(
            Vector3.up,
            rotationSpeed * Time.deltaTime,
            Space.World
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();

        if (player != null)
        {
            if (SonidoHealing != null)
            {
                AudioSource.PlayClipAtPoint(
                    SonidoHealing,
                    transform.position,
                    1f
                );
            }

            player.Heal(healAmount);

            Destroy(gameObject);
        }
    }

}
