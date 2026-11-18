using UnityEngine;

// Proyectil del jefe. Se crea por codigo (no necesita prefab).
// - Normal: va en linea recta.
// - Teledirigido (orbe): persigue al jugador girando poco a poco; el jugador puede destruirlo a balazos.
public class BossProjectile : MonoBehaviour, IDamageable
{
    float velocidad;
    float dano;
    float radio;
    float vidaRestante;
    bool teledirigido;
    float giroGradosPorSegundo;
    float radioExplosion;
    float vidaOrbe = 1f;
    Transform objetivo;
    Transform duenio;
    Color color;

    static Material materialBase;

    // ------------------------------------------------------------ Creacion

    public static BossProjectile Disparar(Vector3 posicion, Vector3 direccion, float velocidad, float dano, Color color,
                                          Transform duenio, float tamano = 0.25f, float vida = 5f)
    {
        BossProjectile p = Crear(posicion, direccion, color, tamano);
        p.velocidad = velocidad;
        p.dano = dano;
        p.radio = tamano * 0.5f;
        p.vidaRestante = vida;
        p.duenio = duenio;
        return p;
    }

    public static BossProjectile Orbe(Vector3 posicion, Vector3 direccion, float velocidad, float dano, Color color,
                                      Transform duenio, Transform objetivo, float giro = 110f, float vida = 7f)
    {
        BossProjectile p = Crear(posicion, direccion, color, 0.7f);
        p.velocidad = velocidad;
        p.dano = dano;
        p.radio = 0.45f;
        p.vidaRestante = vida;
        p.duenio = duenio;
        p.teledirigido = true;
        p.objetivo = objetivo;
        p.giroGradosPorSegundo = giro;
        p.radioExplosion = 2.2f;

        // Collider para que las balas del jugador lo puedan destruir
        SphereCollider c = p.gameObject.AddComponent<SphereCollider>();
        c.radius = 0.5f;
        c.isTrigger = false;
        Rigidbody rb = p.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        return p;
    }

    static BossProjectile Crear(Vector3 posicion, Vector3 direccion, Color color, float tamano)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "ProyectilJefe";
        // Se quita el collider del primitivo (los orbes agregan uno propio despues)
        Destroy(go.GetComponent<Collider>());

        go.transform.position = posicion;
        go.transform.rotation = Quaternion.LookRotation(direccion.sqrMagnitude > 0.0001f ? direccion : Vector3.forward);
        go.transform.localScale = Vector3.one * tamano;

        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = MaterialEfectos();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        mpb.SetColor("_Color", color);
        r.SetPropertyBlock(mpb);

        TrailRenderer trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.18f;
        trail.startWidth = tamano * 0.9f;
        trail.endWidth = 0f;
        trail.sharedMaterial = MaterialEfectos();
        trail.startColor = color;
        trail.endColor = new Color(color.r, color.g, color.b, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        BossProjectile p = go.AddComponent<BossProjectile>();
        p.color = color;
        return p;
    }

    public static Material MaterialEfectos()
    {
        if (materialBase == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            materialBase = new Material(sh);
        }
        return materialBase;
    }

    // ------------------------------------------------------------ Movimiento

    void Update()
    {
        float dt = Time.deltaTime;
        vidaRestante -= dt;
        if (vidaRestante <= 0f) { Explotar(false); return; }

        if (teledirigido && objetivo != null)
        {
            Vector3 hacia = (objetivo.position + Vector3.up * 0.9f) - transform.position;
            Quaternion deseada = Quaternion.LookRotation(hacia);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, deseada, giroGradosPorSegundo * dt);
        }

        float paso = velocidad * dt;
        if (Physics.SphereCast(transform.position, radio, transform.forward, out RaycastHit hit, paso, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!EsDelDuenio(hit.collider))
            {
                transform.position = hit.point;
                Golpear(hit.collider);
                return;
            }
        }
        transform.position += transform.forward * paso;
    }

    bool EsDelDuenio(Collider c)
    {
        if (c == null) return false;
        if (c.transform == transform) return true;
        if (duenio != null && c.transform.IsChildOf(duenio)) return true;
        if (c.GetComponentInParent<BossProjectile>() != null) return true;
        return false;
    }

    void Golpear(Collider c)
    {
        if (teledirigido)
        {
            Explotar(true);
            return;
        }
        PlayerMovement pm = c.GetComponentInParent<PlayerMovement>();
        if (pm != null) pm.TakeDamage(dano);
        Impacto(transform.position, color, 0.6f);
        Destroy(gameObject);
    }

    void Explotar(bool hacerDano)
    {
        if (teledirigido)
        {
            if (hacerDano)
            {
                foreach (Collider c in Physics.OverlapSphere(transform.position, radioExplosion, ~0, QueryTriggerInteraction.Ignore))
                {
                    PlayerMovement pm = c.GetComponentInParent<PlayerMovement>();
                    if (pm != null) { pm.TakeDamage(dano); break; }
                }
            }
            Impacto(transform.position, color, radioExplosion);
        }
        Destroy(gameObject);
    }

    // Las balas del jugador pueden destruir los orbes
    public void TakeDamage(float amount)
    {
        if (!teledirigido) return;
        vidaOrbe -= amount;
        if (vidaOrbe <= 0f)
        {
            Impacto(transform.position, Color.white, 1f);
            Destroy(gameObject);
        }
    }

    // Destello simple de impacto
    public static void Impacto(Vector3 pos, Color color, float tamano)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        go.name = "ImpactoJefe";
        go.transform.position = pos;
        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = MaterialEfectos();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<ImpactoJefe>().Iniciar(color, tamano);
    }
}

// Esfera que crece y se desvanece
public class ImpactoJefe : MonoBehaviour
{
    float t, duracion = 0.25f, tamano;
    Color color;
    Renderer r;
    MaterialPropertyBlock mpb;

    public void Iniciar(Color c, float tam)
    {
        color = c; tamano = tam;
        r = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
        transform.localScale = Vector3.one * tamano * 0.3f;
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = t / duracion;
        transform.localScale = Vector3.one * Mathf.Lerp(tamano * 0.3f, tamano, k);
        Color c = color; c.a = 1f - k;
        mpb.SetColor("_Color", c);
        r.SetPropertyBlock(mpb);
        if (k >= 1f) Destroy(gameObject);
    }
}
