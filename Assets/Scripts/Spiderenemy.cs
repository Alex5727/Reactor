using UnityEngine;

public class SpiderEnemy : MonoBehaviour
{
    public enum State { Idle, Chasing, Climbing }

    [Header("Referencias")]
    public Transform target;                 // si está vacío, busca el objeto con tag "Player"
    public Transform eyePoint;               // opcional: desde dónde "ve" (si está vacío usa el centro)
    public GameObject explosionPrefab;       // opcional: efecto al explotar
    public GameObject deathSpiExplision;       // opcional: efecto al explotar al morir
    public GameObject healingOrbPrefab;

  

    Animator anim;

    [Header("Vida")]
    public float maxHealth = 30f;
    [Range(0f, 1f)]
    public float dropChance = 0.75f;

    [Header("Visión (estado inactivo)")]
    public float sightRange = 25f;
    [Range(10f, 360f)] public float fieldOfView = 100f;   // ángulo total del cono de visión
    public float wakeUpDelay = 0.4f;         // tiempo de reacción antes de empezar a perseguir

    [Header("Movimiento en suelo")]
    public float moveSpeed = 6f;
    public float acceleration = 25f;
    public float turnSpeed = 10f;
    public float gravity = -25f;
    public float maxWalkSlope = 50f;         // pendientes más empinadas se tratan como pared
    public float groundSnapDistance = 0.3f;

    [Header("Rodear paredes")]
    public float probeDistance = 2f;         // a qué distancia "ve" las paredes al frente
    public float avoidCommitTime = 0.8f;     // tiempo mínimo rodeando por un lado (evita titubeos)

    [Header("Escalar paredes")]
    public float climbSpeed = 4.5f;
    public float climbMinHeightDifference = 1.5f; // el jugador debe estar al menos esto más arriba
    public float climbEngageDistance = 0.4f; // qué tan cerca de la pared empieza a trepar
    public float minWallAngle = 60f;         // inclinación mínima para considerar una superficie escalable
    public float maxWallAngle = 105f;        // más allá de esto es techo/desplome: no escala
    public float climbExitBelow = 1.5f;      // si el jugador queda tan por debajo, se suelta
    public float climbCooldown = 1f;
    public float stickReach = 0.6f;
    public LayerMask climbableMask = ~0;     // qué capas puede escalar

    [Header("Colisiones")]
    public float radius = 0.5f;              // radio del cuerpo (el pivot debe estar al centro del cuerpo)
    public float skin = 0.03f;
    public LayerMask obstacleMask = ~0;      // idealmente excluye capas Enemy y Player

    [Header("Explosión")]
    public float explodeDistance = 1.8f;     // distancia centro a centro
    public float contactDistance = 0.4f;     // distancia entre superficies (collider con collider)
    public float damage = 25f;
    public Vector3 aimOffset = Vector3.zero; // ajusta si el pivot del jugador no está al centro

    public State CurrentState => state;

    private State state = State.Idle;
    private float health;
    private bool exploded;

    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private bool grounded;

    private int avoidSide;
    private float avoidUntil;
    private float chaseStartTime;

    private Vector3 surfaceNormal = Vector3.up;
    private float nextClimbTime;

    private Vector3 stuckRefPos;
    private float stuckTimer;

    private Collider ownCollider;
    private Collider targetCollider;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    // =============================================================== Ciclo de vida

    void Awake()
    {
        health = maxHealth;
    }

    void Start()
    {
        anim = GetComponent<Animator>();
        anim.SetBool("IsMoving", false);

        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }

        ownCollider = GetComponentInChildren<Collider>();
        if (target != null)
        {
            targetCollider = target.GetComponent<Collider>();
            if (targetCollider == null) targetCollider = target.GetComponentInChildren<Collider>();
        }

        SnapToGround();
        stuckRefPos = transform.position;
    }

    void Update()
    {
        if (target == null || exploded) return;
        float dt = Time.deltaTime;

        switch (state)
        {
            case State.Idle: UpdateIdle(dt); break;
            case State.Chasing: UpdateChasing(dt); break;
            case State.Climbing: UpdateClimbing(dt); break;
        }
    }

    // =============================================================== INACTIVA

    void UpdateIdle(float dt)
    {
        ApplyGravity(dt);
        if (CanSeeTarget()) Wake();
    }

    void Wake()
    {
        if (state != State.Idle) return;
        state = State.Chasing;
        chaseStartTime = Time.time + wakeUpDelay;
    }

    bool CanSeeTarget()
    {
        Vector3 eye = eyePoint != null ? eyePoint.position : transform.position;
        Vector3 targetPoint = target.position + aimOffset;
        Vector3 toTarget = targetPoint - eye;

        if (toTarget.magnitude > sightRange) return false;
        if (Vector3.Angle(transform.forward, toTarget) > fieldOfView * 0.5f) return false;

        // ¿hay una pared en medio?
        float dist = toTarget.magnitude;
        if (dist < 0.01f) return true;
        return !RaycastClean(eye, toTarget / dist, dist, obstacleMask.value, out RaycastHit sightBlock);
    }

    // =============================================================== PERSIGUIENDO (suelo)

    void UpdateChasing(float dt)
    {
        anim.SetBool("IsMoving", true);

        Vector3 targetPoint = target.position + aimOffset;
        Vector3 toTarget = targetPoint - transform.position;

        if (ShouldExplode())
        {
            Explode();
            return;
        }

        Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
        Vector3 flatDir = flat.sqrMagnitude > 0.0001f ? flat.normalized : transform.forward;

        // Tiempo de reacción: se gira hacia el jugador antes de arrancar
        if (Time.time < chaseStartTime)
        {
            FaceDirection(flatDir, dt);
            ApplyGravity(dt);
            return;
        }

        // ¿Hay una pared escalable en el camino y el jugador está arriba?
        if (TryStartClimb(flatDir)) return;

        Vector3 moveDir = SteerGround(flatDir);

        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, moveDir * moveSpeed, acceleration * dt);
        SlideMove(horizontalVelocity * dt);
        ApplyGravity(dt);

        FaceDirection(moveDir, dt);
        CheckStuck(dt);
    }

    // Decide hacia dónde caminar: directo al jugador, o rodeando la pared si hay una al frente
    Vector3 SteerGround(Vector3 desired)
    {
        bool blocked = ProbeObstacle(desired, probeDistance, out RaycastHit blockHit);

        // Pared escalable y el jugador está arriba: camina directo hacia ella (para trepar)
        if (blocked && WantsToClimb(blockHit, desired)) return desired;

        if (avoidSide != 0)
        {
            // Sigue rodeando hasta que el camino directo esté libre (con margen largo)
            if (Time.time >= avoidUntil && !IsBlocked(desired, probeDistance * 2f))
                avoidSide = 0;
        }
        else if (blocked)
        {
            avoidSide = PickSide(desired);
            avoidUntil = Time.time + avoidCommitTime;
        }

        if (avoidSide == 0) return desired;

        // Busca la dirección libre más cercana al objetivo, del lado elegido
        for (int angle = 0; angle <= 150; angle += 30)
        {
            Vector3 dir = Quaternion.AngleAxis(angle * avoidSide, Vector3.up) * desired;
            if (!IsBlocked(dir, probeDistance)) return dir;
        }

        // Todo bloqueado: cambia de lado y retrocede un poco
        avoidSide = -avoidSide;
        avoidUntil = Time.time + avoidCommitTime;
        return -desired;
    }

    int PickSide(Vector3 desired)
    {
        int left = FirstClearAngle(desired, -1);
        int right = FirstClearAngle(desired, 1);
        if (left == right) return Random.value < 0.5f ? -1 : 1;
        return right < left ? 1 : -1;
    }

    int FirstClearAngle(Vector3 desired, int side)
    {
        for (int angle = 30; angle <= 150; angle += 30)
        {
            if (!IsBlocked(Quaternion.AngleAxis(angle * side, Vector3.up) * desired, probeDistance))
                return angle;
        }
        return 999;
    }

    // Si lleva rato sin avanzar, cambia de lado para rodear
    void CheckStuck(float dt)
    {
        stuckTimer += dt;
        if (stuckTimer < 0.6f) return;

        if ((transform.position - stuckRefPos).sqrMagnitude < 0.2f * 0.2f)
        {
            avoidSide = avoidSide == 0 ? (Random.value < 0.5f ? -1 : 1) : -avoidSide;
            avoidUntil = Time.time + 1.5f;
        }
        stuckRefPos = transform.position;
        stuckTimer = 0f;
    }

    // =============================================================== ESCALANDO

    bool TryStartClimb(Vector3 flatDir)
    {
        if (!ProbeObstacle(flatDir, climbEngageDistance, out RaycastHit wallHit)) return false;
        if (!WantsToClimb(wallHit, flatDir)) return false;

        StartClimb(wallHit);
        return true;
    }

    // Solo escala si: el jugador está bastante más arriba, la superficie es una pared escalable
    // y está justo en el camino hacia el jugador. Si no, la rodea.
    bool WantsToClimb(RaycastHit wallHit, Vector3 flatDir)
    {
        if (Time.time < nextClimbTime) return false;

        float heightDiff = (target.position.y + aimOffset.y) - transform.position.y;
        if (heightDiff < climbMinHeightDifference) return false;

        float angle = Vector3.Angle(wallHit.normal, Vector3.up);
        if (angle < minWallAngle || angle > maxWallAngle) return false;

        if (((1 << wallHit.collider.gameObject.layer) & climbableMask.value) == 0) return false;

        // La pared debe estar de frente al camino, no solo rozándola
        if (Vector3.Dot(-wallHit.normal, flatDir) < 0.3f) return false;

        return true;
    }

    void StartClimb(RaycastHit wallHit)
    {
        state = State.Climbing;
        surfaceNormal = wallHit.normal;
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        grounded = false;

        // Se pega a la pared (solo ajusta la distancia, no la altura)
        Vector3 pos = transform.position;
        float dist = Vector3.Dot(pos - wallHit.point, wallHit.normal);
        transform.position = pos + wallHit.normal * (radius + skin - dist);
    }

    void UpdateClimbing(float dt)
    {
        Vector3 targetPoint = target.position + aimOffset;

        if (ShouldExplode())
        {
            Explode();
            return;
        }

        // Si el jugador quedó muy por debajo, ya no le sirve estar en la pared
        if (targetPoint.y < transform.position.y - climbExitBelow)
        {
            ExitClimb();
            return;
        }

        // --- Dirección de movimiento sobre la pared ---
        Vector3 toTarget = targetPoint - transform.position;
        Vector3 wallUp = Vector3.ProjectOnPlane(Vector3.up, surfaceNormal);
        wallUp = wallUp.sqrMagnitude > 0.001f ? wallUp.normalized : transform.forward;
        Vector3 along = Vector3.ProjectOnPlane(toTarget, surfaceNormal);

        bool playerAbove = targetPoint.y > transform.position.y + 0.3f;
        Vector3 dir;
        if (playerAbove)
            dir = wallUp + (along.sqrMagnitude > 0.001f ? along.normalized * 0.4f : Vector3.zero);
        else
            dir = along;

        if (dir.sqrMagnitude < 0.001f) dir = wallUp;
        dir.Normalize();

        // Cuerpo alineado a la pared
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(dir, surfaceNormal), turnSpeed * dt);

        float stepLen = climbSpeed * dt;
        int climbRayMask = obstacleMask.value & climbableMask.value;

        // 1) Algo al frente (esquina interior, otra pared, techo, suelo)
        if (CastSphere(transform.position, radius * 0.9f, dir, stepLen + skin, false, out RaycastHit ahead))
        {
            transform.position += dir * Mathf.Max(0f, ahead.distance - skin);

            if (Vector3.Angle(ahead.normal, Vector3.up) <= maxWalkSlope || ahead.normal.y < -0.3f)
                ExitClimb();                     // suelo: camina / techo: se suelta
            else
                surfaceNormal = ahead.normal;    // otra pared: sigue trepando por ella
            return;
        }

        transform.position += dir * stepLen;

        // 2) Mantenerse pegado a la superficie
        Vector3 pos = transform.position;
        if (RaycastClean(pos, -surfaceNormal, radius + stickReach, climbRayMask, out RaycastHit wall) && wall.normal.y > -0.3f)
        {
            surfaceNormal = Vector3.Slerp(surfaceNormal, wall.normal, 15f * dt).normalized;
            float dist = Vector3.Dot(pos - wall.point, wall.normal);
            transform.position = pos + wall.normal * (radius + skin - dist);
            return;
        }

        // 3) La pared terminó (borde): intenta rodear la esquina / subirse encima
        Vector3 origin = pos - surfaceNormal * (radius + 0.05f);
        if (RaycastClean(origin, -dir, radius + 0.3f, climbRayMask, out RaycastHit edge) && edge.normal.y > -0.3f)
        {
            transform.position = edge.point + edge.normal * (radius + skin);

            if (Vector3.Angle(edge.normal, Vector3.up) <= maxWalkSlope)
                ExitClimb();                     // llegó arriba: vuelve a caminar
            else
                surfaceNormal = edge.normal;     // esquina vertical: sigue por el otro lado
            return;
        }

        // 4) Perdió la superficie: se suelta
        ExitClimb();
    }

    void ExitClimb()
    {
        state = State.Chasing;
        verticalVelocity = 0f;
        horizontalVelocity = Vector3.zero;
        grounded = false;
        avoidSide = 0;
        nextClimbTime = Time.time + climbCooldown;
    }

    // =============================================================== Movimiento / colisiones

    // Se mueve y se desliza por paredes y pendientes sin atravesarlas
    void SlideMove(Vector3 delta)
    {
        for (int i = 0; i < 3; i++)
        {
            float dist = delta.magnitude;
            if (dist < 0.0001f) break;

            Vector3 dir = delta / dist;

            if (CastSphere(transform.position, radius, dir, dist + skin, false, out RaycastHit hit))
            {
                float allowed = Mathf.Max(0f, hit.distance - skin);
                transform.position += dir * allowed;
                Vector3 remaining = dir * (dist - allowed);

                Vector3 n = hit.normal;
                if (Vector3.Angle(n, Vector3.up) > maxWalkSlope)
                {
                    // Pared: desliza solo en horizontal
                    n = new Vector3(n.x, 0f, n.z);
                    if (n.sqrMagnitude < 0.0001f) break;
                    n.Normalize();
                    horizontalVelocity = Vector3.ProjectOnPlane(horizontalVelocity, n);
                }

                delta = Vector3.ProjectOnPlane(remaining, n);
            }
            else
            {
                transform.position += delta;
                break;
            }
        }
    }

    void ApplyGravity(float dt)
    {
        verticalVelocity = Mathf.Max(verticalVelocity + gravity * dt, -50f);
        float fall = -verticalVelocity * dt;
        float castDist = fall + (grounded ? groundSnapDistance : 0f) + skin;

        if (CastSphere(transform.position, radius, Vector3.down, castDist, false, out RaycastHit groundHit))
        {
            transform.position += Vector3.down * Mathf.Max(0f, groundHit.distance - skin);

            if (Vector3.Angle(groundHit.normal, Vector3.up) <= maxWalkSlope)
            {
                grounded = true;
                verticalVelocity = 0f;
            }
            else
            {
                grounded = false;
            }
        }
        else
        {
            transform.position += Vector3.down * fall;
            grounded = false;
        }
    }

    void FaceDirection(Vector3 dir, float dt)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(dir.normalized, Vector3.up), turnSpeed * dt);
    }

    void SnapToGround()
    {
        Vector3 origin = transform.position + Vector3.up * radius;
        if (RaycastClean(origin, Vector3.down, radius * 2f + 1f, obstacleMask.value, out RaycastHit groundHit)
            && Vector3.Angle(groundHit.normal, Vector3.up) <= maxWalkSlope)
        {
            transform.position = groundHit.point + Vector3.up * (radius + skin);
            grounded = true;
        }
    }

    // =============================================================== Detección (helpers)

    // Ignora sus propios colliders y al jugador (el jugador es la meta, no un obstáculo)
    bool IsIgnored(Collider c)
    {
        Transform t = c.transform;
        return t.IsChildOf(transform) || (target != null && t.IsChildOf(target));
    }

    // ¿Hay una pared (superficie empinada) en esa dirección?
    bool ProbeObstacle(Vector3 dir, float dist, out RaycastHit hit)
    {
        return CastSphere(transform.position, radius * 0.9f, dir, dist, true, out hit);
    }

    bool IsBlocked(Vector3 dir, float dist)
    {
        return ProbeObstacle(dir, dist, out RaycastHit unused);
    }

    bool CastSphere(Vector3 origin, float castRadius, Vector3 dir, float distance, bool steepOnly, out RaycastHit best)
    {
        best = default;
        bool found = false;
        float bestDist = float.MaxValue;

        int count = Physics.SphereCastNonAlloc(origin, castRadius, dir, hitBuffer, distance,
                                               obstacleMask.value, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (h.distance <= 0f) continue;                  // ya estaba traslapando
            if (IsIgnored(h.collider)) continue;
            if (steepOnly && Vector3.Angle(h.normal, Vector3.up) <= maxWalkSlope) continue;

            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                best = h;
                found = true;
            }
        }
        return found;
    }

    bool RaycastClean(Vector3 origin, Vector3 dir, float distance, int mask, out RaycastHit best)
    {
        best = default;
        bool found = false;
        float bestDist = float.MaxValue;

        int count = Physics.RaycastNonAlloc(origin, dir, hitBuffer, distance, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (IsIgnored(h.collider)) continue;

            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                best = h;
                found = true;
            }
        }
        return found;
    }

    // =============================================================== Explosión y vida

    // Explota por distancia al centro del jugador O por contacto real entre colliders,
    // así funciona aunque el modelo/collider de la araña sea grande.
    bool ShouldExplode()
    {
        Vector3 center = transform.position;

        if (Vector3.Distance(center, target.position + aimOffset) <= explodeDistance)
            return true;

        if (targetCollider != null)
        {
            Vector3 closest = targetCollider.bounds.ClosestPoint(center);
            float gap = Vector3.Distance(center, closest);

            float bodyRadius = radius;
            if (ownCollider != null)
            {
                Vector3 e = ownCollider.bounds.extents;
                bodyRadius = Mathf.Max(radius, e.x, e.y, e.z);
            }

            if (gap - bodyRadius <= contactDistance) return true;
        }

        return false;
    }

    void Explode()
    {
        if (exploded) return;
        exploded = true;

        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(
                explosionPrefab,
                transform.position,
                Quaternion.identity
            );

            Destroy(explosion, 2f);
        }
        // Mismo método que usan las balas: el jugador solo necesita un TakeDamage(float)
        if (target != null)
            target.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

        Destroy(gameObject);
    }

    //dtop vida
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

    // Las balas del jugador llaman a este método automáticamente
    public void TakeDamage(float amount)
    {
        health -= amount;
        Wake();                                   // si lo golpean estando quieto, se activa
        if (health <= 0f)
        {
            DropHealingOrb();
            Destroy(gameObject);    // muere sin explotar


                   GameObject explosion = Instantiate(
                deathSpiExplision,
                transform.position,
                Quaternion.identity
            );

            Destroy(explosion, 2f);

        }
    }

    // =============================================================== Debug

    void OnDrawGizmosSelected()
    {
        Vector3 pos = transform.position;

        Gizmos.color = Color.yellow;
        Vector3 left = Quaternion.AngleAxis(-fieldOfView * 0.5f, Vector3.up) * transform.forward;
        Vector3 right = Quaternion.AngleAxis(fieldOfView * 0.5f, Vector3.up) * transform.forward;
        Gizmos.DrawRay(pos, left * sightRange);
        Gizmos.DrawRay(pos, right * sightRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(pos, radius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pos, explodeDistance);
    }
}