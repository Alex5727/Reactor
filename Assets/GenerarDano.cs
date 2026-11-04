using UnityEngine;

public class SetPlayerHealth : MonoBehaviour
{

    public float vidaObjetivo = 30f;

    public AudioClip sonidoActivacion;
    [Range(0f, 1f)]
    public float volumenSonido = 1f;

    private void OnTriggerEnter(Collider other)
    {
        // Comprobar si el objeto que entra es el jugador
        PlayerMovement player = other.GetComponent<PlayerMovement>();

        if (player != null && !player.IsDead)
        {
            // Establecer la vida exactamente en 30
            player.SetHealth(vidaObjetivo);

            if (sonidoActivacion != null)
            {
                AudioSource.PlayClipAtPoint(
                    sonidoActivacion,
                    transform.position,
                    volumenSonido
                );
            }
        }
    }
}