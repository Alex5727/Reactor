using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 80f;
    public float lifetime = 3f;
    public float damage = 10f;
    public LayerMask hitMask = ~0;

    public  GameObject prefabExplosion;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        float distance = speed * Time.deltaTime;

        // Raycast por frame para que las balas rápidas no atraviesen paredes
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, distance, hitMask, QueryTriggerInteraction.Ignore))
        {
            OnHit(hit);
            return;
        }

        transform.position += transform.forward * distance;
    }

    void OnHit(RaycastHit hit)
    {
        // Si el objeto tiene un método TakeDamage(float), lo llama (si no, lo ignora)
        hit.collider.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

        if (prefabExplosion != null)
        { 
        CrearExplosion(hit.point);
        }

        // Aquí puedes instanciar partículas / marca de impacto en hit.point
        Destroy(gameObject);
    }

    public void CrearExplosion(Vector3 posicion)
    {
        GameObject explosion = Instantiate(
            prefabExplosion,
            posicion,
            Quaternion.identity
        );

        Destroy(explosion, 3f);
    }

}