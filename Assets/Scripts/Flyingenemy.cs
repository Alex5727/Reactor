using UnityEngine;

public class FlyingEnemy : MonoBehaviour
{
    [Header("Referencias")]
    public Transform target;                 // si está vacío, busca el objeto con tag "Player"
    public Transform firePoint;              // empty de donde salen las balas
    public GameObject bulletPrefab;

    [Header("Vida")]
    public float maxHealth = 50f;

    [Header("Distancia al jugador")]
    public float detectionRange = 60f;       // fuera de este rango se queda flotando
    public float preferredDistance = 14f;    // distancia ideal
    public float distanceTolerance = 3f;     // margen +/- donde no se acerca ni se aleja

    [Header("Movimiento")]
    public float strafeSpeed = 7f;           // velocidad lateral
    public float approachSpeed = 6f;         // velocidad al acercarse / alejarse
    public float acceleration = 12f;         // qué tan suave cambia de velocidad
    public Vector2 strafeChangeInterval = new Vector2(1f, 2.5f); // cada cuánto cambia de lado (seg)
    public float turnSpeed = 8f;
    public float velocidadGiro = 3f;

    [Header("Altura")]
    public float hoverHeight = 3f;           // altura respecto al jugador
    public float verticalSpeed = 3f;
    public float bobAmplitude = 0.4f;        // vaivén vertical
    public float bobFrequency = 1.5f;
    public float minGroundClearance = 1.5f;  // distancia mínima al suelo

    [Header("Colisiones")]
    public float radius = 0.5f;              // radio con el que "ve" paredes
    public float skin = 0.05f;
    public LayerMask obstacleMask = ~0;      // idealmente excluye capas Enemy y Player

    [Header("Disparo")]
    public float fireRate = 1.5f;            // disparos por segundo
    public float fireRateRandomness = 0.3f;  // variación aleatoria del intervalo (0 a 1)
    public float bulletSpeed = 35f;
    public float aimInaccuracy = 2f;         // grados de dispersión
    [Range(0f, 1f)] public float leadFactor = 0.5f; // 0 = apunta directo, 1 = predice el movimiento del jugador
    public float maxShootAngle = 20f;        // solo dispara si ya está apuntando hacia el jugador
    public Vector3 aimOffset = Vector3.zero; // ajusta si el pivot del jugador no está al centro

    public GameObject prefabExplosion;


    [Header("Drop de curación")]
    public GameObject healingOrbPrefab;

    [Range(0f, 1f)]
    public float dropChance = 0.75f;


    private float health;
    private Vector3 velocity;
    private float strafeDir = 1f;
    private float strafeTimer;
    private float nextFireTime;
    private float bobOffset;

    private Vector3 lastTargetPos;
    private Vector3 targetVelocity;

    void Awake()
    {
        health = maxHealth;
        bobOffset = Random.value * 10f;
    }

    void Start()
    {
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }
        if (target != null) lastTargetPos = target.position;
        strafeDir = Random.value > 0.5f ? 1f : -1f;
        ResetStrafeTimer();
    }

    void Update()
    {
        if (target == null) return;

       
        float dt = Time.deltaTime;

        //Apuntar al jugador
        Vector3 direccion = target.position - transform.position;

        // Calcula la rotación necesaria para apuntar al jugador
        if (direccion.sqrMagnitude > 0.001f)
        {
            Quaternion rotacionObjetivo =
                Quaternion.LookRotation(direccion);

            

            // Gira suavemente hacia la rotación deseada
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                rotacionObjetivo,
                velocidadGiro * dt
            );
        }


        // Velocidad del jugador (para predecir hacia dónde se mueve)
        Vector3 rawVel = (target.position - lastTargetPos) / Mathf.Max(dt, 0.0001f);
        targetVelocity = Vector3.Lerp(targetVelocity, rawVel, 10f * dt);
        lastTargetPos = target.position;

        Vector3 toTarget = target.position - transform.position;
        bool inRange = toTarget.sqrMagnitude <= detectionRange * detectionRange;

        Vector3 desiredVelocity = Vector3.zero;

        if (inRange)
        {
            desiredVelocity = ComputeDesiredVelocity(toTarget, dt);
            AimAndShoot();
        }
        else
        {
            desiredVelocity = new Vector3(0f, VerticalVelocity(target.position.y + hoverHeight), 0f);
        }

        // Suavizado: cambia de velocidad gradualmente, sin tirones
        velocity = Vector3.MoveTowards(velocity, desiredVelocity, acceleration * dt);

        MoveWithCollisions(dt);
        RotateTowards(toTarget, dt);
    }

    // ---------------------------------------------------------------- Movimiento

    Vector3 ComputeDesiredVelocity(Vector3 toTarget, float dt)
    {
        Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
        float flatDist = flat.magnitude;
        Vector3 flatDir = flatDist > 0.01f ? flat / flatDist : transform.forward;

        // Cambiar de lado cada cierto tiempo
        strafeTimer -= dt;
        if (strafeTimer <= 0f)
        {
            strafeDir = -strafeDir;
            ResetStrafeTimer();
        }

        // Lateral (perpendicular al jugador)
        Vector3 lateralAxis = Vector3.Cross(Vector3.up, flatDir).normalized;
        Vector3 strafe = lateralAxis * strafeDir * strafeSpeed;

        // Acercarse / alejarse según la distancia ideal
        float error = flatDist - preferredDistance;
        float radialFactor = Mathf.Clamp01((Mathf.Abs(error) - distanceTolerance) / Mathf.Max(distanceTolerance, 0.01f))
                             * Mathf.Sign(error);
        Vector3 radial = flatDir * radialFactor * approachSpeed;

        // Vertical
        float targetY = target.position.y + hoverHeight + Mathf.Sin((Time.time + bobOffset) * bobFrequency) * bobAmplitude;
        Vector3 vertical = Vector3.up * VerticalVelocity(targetY);

        return strafe + radial + vertical;
    }

    float VerticalVelocity(float targetY)
    {
        float vy = Mathf.Clamp((targetY - transform.position.y) * 2f, -verticalSpeed, verticalSpeed);

        // No bajar de la distancia mínima al suelo
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, minGroundClearance + 1f,
                            obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.distance < minGroundClearance)
                vy = Mathf.Max(vy, verticalSpeed);
        }
        return vy;
    }

    // Se mueve y se desliza por paredes, suelo y techo en vez de atravesarlos
    void MoveWithCollisions(float dt)
    {
        Vector3 remaining = velocity * dt;

        for (int i = 0; i < 3; i++)
        {
            float dist = remaining.magnitude;
            if (dist < 0.0001f) break;

            Vector3 dir = remaining / dist;

            if (Physics.SphereCast(transform.position, radius, dir, out RaycastHit hit, dist + skin,
                                   obstacleMask, QueryTriggerInteraction.Ignore))
            {
                float allowed = Mathf.Max(0f, hit.distance - skin);
                transform.position += dir * allowed;
                remaining -= dir * allowed;

                // Quitar la parte del movimiento que va contra la superficie (deslizar)
                remaining = Vector3.ProjectOnPlane(remaining, hit.normal);
                velocity = Vector3.ProjectOnPlane(velocity, hit.normal);

                // Si una pared bloquea el lateral, cambiar de lado
                Vector3 flatNormal = new Vector3(hit.normal.x, 0f, hit.normal.z);
                if (flatNormal.sqrMagnitude > 0.1f)
                {
                    Vector3 strafeVec = Vector3.Cross(Vector3.up, new Vector3(target.position.x - transform.position.x, 0f,
                                                                           target.position.z - transform.position.z)).normalized * strafeDir;
                    if (Vector3.Dot(strafeVec, flatNormal.normalized) < -0.3f)
                    {
                        strafeDir = -strafeDir;
                        ResetStrafeTimer();
                    }
                }
            }
            else
            {
                transform.position += remaining;
                break;
            }
        }
    }

    void RotateTowards(Vector3 toTarget, float dt)
    {
        if (toTarget.sqrMagnitude < 0.01f) return;
        Quaternion look = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * dt);
    }

    void ResetStrafeTimer()
    {
        strafeTimer = Random.Range(strafeChangeInterval.x, strafeChangeInterval.y);
    }

    // ---------------------------------------------------------------- Disparo

    void AimAndShoot()
    {
        if (firePoint == null || bulletPrefab == null) return;
        if (Time.time < nextFireTime) return;

        Vector3 aimPoint = target.position + aimOffset;

        // Predicción: apunta un poco adelante de donde va el jugador
        float travelTime = Vector3.Distance(firePoint.position, aimPoint) / bulletSpeed;
        aimPoint += targetVelocity * travelTime * leadFactor;

        Vector3 dir = (aimPoint - firePoint.position).normalized;

        // Solo dispara si ya está mirando hacia el jugador
        if (Vector3.Angle(transform.forward, dir) > maxShootAngle) return;

        // Solo dispara si no hay una pared en medio
        if (Physics.Linecast(firePoint.position, target.position + aimOffset, out RaycastHit hit,
                             obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(target)) return;
        }

        // Dispersión
        Quaternion spread = Quaternion.Euler(Random.Range(-aimInaccuracy, aimInaccuracy),
                                             Random.Range(-aimInaccuracy, aimInaccuracy), 0f);
        Quaternion rot = Quaternion.LookRotation(dir) * spread;

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, rot);
        if (bullet.TryGetComponent(out Bullet b)) b.speed = bulletSpeed;

        float interval = 1f / fireRate;
        nextFireTime = Time.time + interval * Random.Range(1f - fireRateRandomness, 1f + fireRateRandomness);
    }

    // ---------------------------------------------------------------- Vida

    // Las balas del jugador llaman a este método automáticamente
    public void TakeDamage(float amount)
    {
        health -= amount;
        if (health <= 0f) {
            DropHealingOrb();
            Die();
        }


    }

    void Die()
    {

        CrearExplosion(transform.position);

        // Aquí puedes agregar explosión, sonido, drops, etc.
        Destroy(gameObject);
    }

    // ---------------------------------------------------------------- Debug

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
        if (target != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(target.position, preferredDistance);
        }
    }

    void DropHealingOrb()
    {
        if (healingOrbPrefab == null)
            return;

        if (Random.value > dropChance)
            return;

        Vector3 spawnPosition =
            transform.position + Vector3.up * 0.5f;

        Instantiate(
            healingOrbPrefab,
            spawnPosition,
            Quaternion.identity
        );
    }

    public void CrearExplosion(Vector3 posicion)
    {
       
        GameObject explosion = Instantiate(
            prefabExplosion,
            posicion,
            Quaternion.identity
        );

        Destroy(explosion, 2f);
    }
}