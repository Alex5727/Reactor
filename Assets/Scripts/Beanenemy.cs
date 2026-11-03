using UnityEngine;

public class BeamEnemy : MonoBehaviour
{
    public enum State { Idle, Engaging, Charging, Firing, Cooldown }

    [Header("Referencias")]
    public Transform target;                 // si está vacío, busca el objeto con tag "Player"
    public Transform firePoint;              // empty de donde sale el rayo (si está vacío usa el centro)

    public GameObject prefabExplosion;
    public AudioClip sonidoMuerte;
    public AudioSource audioSource;

    public AudioClip sonidoCarga;
    public AudioClip sonidoDisparo;
    public float VolumenSon = 4;

    public GameObject healingOrbPrefab;

    [Range(0f, 1f)]
    public float dropChance = 0.75f;

    [Header("Vida")]
    public float maxHealth = 80f;

    [Header("Detección y distancia")]
    public float detectionRange = 60f;       // rango de visión (necesita línea de vista, no ve a través de paredes)
    public float attackRange = 22f;          // a esta distancia (o menos) empieza a cargar el rayo
    public float preferredDistance = 15f;    // distancia ideal al jugador
    public float distanceTolerance = 3f;     // margen +/- donde no se acerca ni se aleja

    [Header("Movimiento")]
    public float strafeSpeed = 6f;
    public float approachSpeed = 6f;
    public float acceleration = 12f;
    public Vector2 strafeChangeInterval = new Vector2(1.5f, 3f);
    [Range(0f, 1f)] public float attackMoveMultiplier = 0.5f; // multiplicador de velocidad mientras carga/dispara

    [Header("Altura")]
    public float hoverHeight = 3f;
    public float verticalSpeed = 3f;
    public float bobAmplitude = 0.4f;
    public float bobFrequency = 1.5f;
    public float minGroundClearance = 1.5f;

    [Header("Colisiones")]
    public float radius = 0.6f;
    public float skin = 0.05f;
    public LayerMask obstacleMask = ~0;      // paredes y suelo (el jugador y el propio enemigo se ignoran solos)

    [Header("Ataque - tiempos")]
    public float chargeTime = 2f;            // duración del rayo de advertencia
    public float fireDuration = 3f;          // duración del rayo de daño
    public float cooldownTime = 4f;          // espera después de disparar
    public float loseSightTime = 1.5f;       // si pierde de vista al jugador cargando más de esto, cancela

    [Header("Ataque - puntería (grados por segundo)")]
    public float normalTurnSpeed = 180f;     // cuando no está atacando
    public float chargeTurnSpeed = 35f;      // mientras carga (rayo de advertencia)
    public float fireTurnSpeed = 20f;        // mientras dispara (más lento todavía)
    public float maxStartAngle = 15f;        // debe estar apuntando casi al jugador para empezar a cargar
    public Vector3 aimOffset = Vector3.zero; // ajusta si el pivot del jugador no está al centro

    [Header("Ataque - rayo")]
    public float maxBeamLength = 80f;
    public float warningWidth = 0.06f;       // grosor del rayo de advertencia
    public float beamWidth = 1.5f;           // grosor del rayo de daño
    public float damagePerSecond = 60f;
    public float damageTickInterval = 0.1f;
    public float beamRampUp = 0.15f;         // tiempo que tarda el rayo en "abrirse"
    public float beamRampDown = 0.3f;        // tiempo que tarda en apagarse
    public LayerMask beamHitMask = ~0;       // qué golpea el rayo (idealmente sin la capa Enemy)

    [Header("Visual del rayo")]
    public Material warningMaterial;         // opcional (si está vacío usa uno por defecto)
    public Material beamMaterial;            // opcional
    public Color warningColor = new Color(1f, 0.15f, 0.1f, 1f);
    public Color beamColor = new Color(1f, 0.25f, 0.05f, 1f);
    public Color coreColor = Color.white;

    public State CurrentState => state;

    private State state = State.Idle;
    private float health;

    private Vector3 velocity;
    private float strafeDir = 1f;
    private float strafeTimer;
    private float bobOffset;

    private Vector3 aimDir;
    private float stateTimer;
    private float lostSightTimer;
    private float tickTimer;

    private LineRenderer warningLine;
    private LineRenderer beamLine;
    private LineRenderer coreLine;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    // =============================================================== Ciclo de vida

    void Awake()
    {
        health = maxHealth;
        bobOffset = Random.value * 10f;
        aimDir = transform.forward;

        Material fallback = null;
        if (warningMaterial == null || beamMaterial == null) fallback = CreateDefaultMaterial();

        warningLine = CreateLine("WarningBeam", warningMaterial != null ? warningMaterial : fallback);
        beamLine = CreateLine("Beam", beamMaterial != null ? beamMaterial : fallback);
        coreLine = CreateLine("BeamCore", beamMaterial != null ? beamMaterial : fallback);
    }

    void Start()
    {
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }

        strafeDir = Random.value > 0.5f ? 1f : -1f;
        ResetStrafeTimer();
    }

    void Update()
    {
        if (target == null) return;
        float dt = Time.deltaTime;

        if (state == State.Idle)
        {
            UpdateIdle();
            return;
        }

        bool attacking = state == State.Charging || state == State.Firing;
        Vector3 toTarget = target.position - transform.position;

        // Movimiento (igual que el otro volador: lateral + distancia + sin atravesar paredes)
        Vector3 desiredVelocity = ComputeDesiredVelocity(toTarget, dt, attacking ? attackMoveMultiplier : 1f);
        velocity = Vector3.MoveTowards(velocity, desiredVelocity, acceleration * dt);
        MoveWithCollisions(dt);

        UpdateAim(dt);

        switch (state)
        {
            case State.Engaging: UpdateEngaging(); break;
            case State.Charging: UpdateCharging(dt); break;
            case State.Firing: UpdateFiring(dt); break;
            case State.Cooldown: UpdateCooldown(dt); break;
        }
    }

    // =============================================================== Estados

    void UpdateIdle()
    {
        velocity = Vector3.zero;

        Vector3 targetPoint = target.position + aimOffset;
        if ((targetPoint - transform.position).sqrMagnitude > detectionRange * detectionRange) return;

        if (HasLineOfSight(FireOrigin(), targetPoint)) Wake();
    }

    void Wake()
    {
        if (state != State.Idle) return;
        state = State.Engaging;
    }

    // Se acerca hasta estar en rango y apuntando al jugador, entonces empieza a cargar
    void UpdateEngaging()
    {
        Vector3 origin = FireOrigin();
        Vector3 targetPoint = target.position + aimOffset;
        Vector3 to = targetPoint - origin;

        if (to.magnitude > attackRange) return;
        if (Vector3.Angle(aimDir, to) > maxStartAngle) return;
        if (!HasLineOfSight(origin, targetPoint)) return;

        state = State.Charging;
        stateTimer = chargeTime;
        lostSightTimer = 0f;


        //SONIDO CARGA
        AudioSource.PlayClipAtPoint(
                    sonidoCarga,
                    transform.position,
                    VolumenSon
                );
    }

    // Rayo de advertencia (inofensivo): sigue al jugador despacio
    void UpdateCharging(float dt)
    {
        stateTimer -= dt;

        Vector3 origin = FireOrigin();
        Vector3 targetPoint = target.position + aimOffset;

        bool inRange = (targetPoint - origin).magnitude <= attackRange * 1.4f;
        lostSightTimer = HasLineOfSight(origin, targetPoint) ? 0f : lostSightTimer + dt;

        if (!inRange || lostSightTimer > loseSightTime)
        {
            HideBeams();
            state = State.Engaging;
            return;
        }

        float progress = 1f - Mathf.Clamp01(stateTimer / chargeTime);
        DrawWarning(origin, progress);

        if (stateTimer <= 0f)
        {
            warningLine.enabled = false;
            state = State.Firing;
            stateTimer = fireDuration;
            tickTimer = 0f;

            AudioSource.PlayClipAtPoint(
           sonidoDisparo,
           transform.position,
           VolumenSon
       );
        }
    }

    // Rayo grande con daño: sigue al jugador todavía más despacio
    void UpdateFiring(float dt)
    {
        stateTimer -= dt;

        float elapsed = fireDuration - stateTimer;
        float envelope = Mathf.Clamp01(elapsed / Mathf.Max(beamRampUp, 0.01f))
                       * Mathf.Clamp01(stateTimer / Mathf.Max(beamRampDown, 0.01f));

        DrawBeam(FireOrigin(), envelope, dt);

        if (stateTimer <= 0f)
        {
            HideBeams();
            state = State.Cooldown;
            stateTimer = cooldownTime;
        }
    }

    void UpdateCooldown(float dt)
    {
        stateTimer -= dt;
        if (stateTimer <= 0f) state = State.Engaging;   // vuelve a cargar si está en rango
    }

    // =============================================================== Puntería

    void UpdateAim(float dt)
    {
        Vector3 desired = (target.position + aimOffset) - FireOrigin();
        if (desired.sqrMagnitude < 0.0001f) return;
        desired.Normalize();

        float turnSpeed = normalTurnSpeed;
        if (state == State.Charging) turnSpeed = chargeTurnSpeed;
        else if (state == State.Firing) turnSpeed = fireTurnSpeed;

        // Gira hacia el jugador con una velocidad angular limitada
        aimDir = Vector3.RotateTowards(aimDir, desired, turnSpeed * Mathf.Deg2Rad * dt, 0f);
        transform.rotation = Quaternion.LookRotation(aimDir, Vector3.up);
    }

    Vector3 FireOrigin()
    {
        return firePoint != null ? firePoint.position : transform.position;
    }

    // =============================================================== Rayos (visual + daño)

    void DrawWarning(Vector3 origin, float progress)
    {

        CastBeam(origin, aimDir, 0f, out Vector3 end, out bool warningHitsPlayer);

        float width = warningWidth * (1f + progress * 1.5f);
        float alpha = Mathf.Lerp(0.35f, 1f, progress);
        if (progress > 0.75f && Mathf.Sin(Time.time * 35f) < 0f) alpha *= 0.3f;   // parpadea justo antes de disparar

        Color c = warningColor;
        c.a = alpha;
        SetLine(warningLine, origin, end, width, c);
    }

    void DrawBeam(Vector3 origin, float envelope, float dt)
    {
        CastBeam(origin, aimDir, beamWidth * 0.4f, out Vector3 end, out bool hitsPlayer);

        float flicker = 1f + 0.08f * Mathf.Sin(Time.time * 45f);
        float width = beamWidth * envelope * flicker;

        SetLine(beamLine, origin, end, width, beamColor);
        SetLine(coreLine, origin, end, width * 0.35f, coreColor);

        // Daño continuo mientras el jugador esté dentro del rayo
        tickTimer -= dt;
        if (hitsPlayer && envelope > 0.5f && tickTimer <= 0f)
        {
            target.SendMessage("TakeDamage", damagePerSecond * damageTickInterval, SendMessageOptions.DontRequireReceiver);
            tickTimer = damageTickInterval;
        }
    }

    // Lanza el rayo: se detiene en paredes/suelo, pero atraviesa visualmente al jugador.
    // hitsPlayer = true si el jugador está en la trayectoria antes de una pared.
    void CastBeam(Vector3 origin, Vector3 dir, float hitRadius, out Vector3 end, out bool hitsPlayer)
    {
        

        end = origin + dir * maxBeamLength;
        hitsPlayer = false;

        int count = hitRadius > 0.001f
            ? Physics.SphereCastNonAlloc(origin, hitRadius, dir, hitBuffer, maxBeamLength, beamHitMask.value, QueryTriggerInteraction.Ignore)
            : Physics.RaycastNonAlloc(origin, dir, hitBuffer, maxBeamLength, beamHitMask.value, QueryTriggerInteraction.Ignore);

        float wallDist = float.MaxValue;
        float playerDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            Transform t = h.collider.transform;
            if (t.IsChildOf(transform)) continue;                 // el propio enemigo

            float along = h.distance <= 0f ? 0f : Mathf.Max(h.distance, Vector3.Dot(h.point - origin, dir));

            if (t.IsChildOf(target))
            {
                if (along < playerDist) playerDist = along;
            }
            else if (along < wallDist)
            {
                wallDist = along;
            }
        }

        if (wallDist < float.MaxValue) end = origin + dir * wallDist;
        hitsPlayer = playerDist < float.MaxValue && playerDist <= wallDist;
    }

    void SetLine(LineRenderer line, Vector3 from, Vector3 to, float width, Color color)
    {
        line.enabled = true;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
    }

    void HideBeams()
    {
        warningLine.enabled = false;
        beamLine.enabled = false;
        coreLine.enabled = false;
    }

    LineRenderer CreateLine(string lineName, Material mat)
    {
        GameObject go = new GameObject(lineName);
        go.transform.SetParent(transform, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.numCapVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        if (mat != null) lr.sharedMaterial = mat;
        lr.enabled = false;
        return lr;
    }

    Material CreateDefaultMaterial()
    {
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        return sh != null ? new Material(sh) : null;
    }

    // =============================================================== Movimiento (igual que el otro volador)

    Vector3 ComputeDesiredVelocity(Vector3 toTarget, float dt, float speedMul)
    {
        Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
        float flatDist = flat.magnitude;
        Vector3 flatDir = flatDist > 0.01f ? flat / flatDist : transform.forward;

        // Cambia de lado cada cierto tiempo
        strafeTimer -= dt;
        if (strafeTimer <= 0f)
        {
            strafeDir = -strafeDir;
            ResetStrafeTimer();
        }

        Vector3 lateralAxis = Vector3.Cross(Vector3.up, flatDir).normalized;
        Vector3 strafe = lateralAxis * strafeDir * strafeSpeed;

        // Se acerca / aleja para mantener la distancia ideal
        float error = flatDist - preferredDistance;
        float radialFactor = Mathf.Clamp01((Mathf.Abs(error) - distanceTolerance) / Mathf.Max(distanceTolerance, 0.01f))
                             * Mathf.Sign(error);
        Vector3 radial = flatDir * radialFactor * approachSpeed;

        // Altura
        float targetY = target.position.y + hoverHeight + Mathf.Sin((Time.time + bobOffset) * bobFrequency) * bobAmplitude;
        Vector3 vertical = Vector3.up * VerticalVelocity(targetY);

        return (strafe + radial) * speedMul + vertical;
    }

    float VerticalVelocity(float targetY)
    {
        float vy = Mathf.Clamp((targetY - transform.position.y) * 2f, -verticalSpeed, verticalSpeed);

        // No bajar de la distancia mínima al suelo
        if (RaycastClean(transform.position, Vector3.down, minGroundClearance + 1f, obstacleMask.value, out RaycastHit groundHit)
            && groundHit.distance < minGroundClearance)
        {
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

            if (CastSphere(transform.position, radius, dir, dist + skin, out RaycastHit hit))
            {
                float allowed = Mathf.Max(0f, hit.distance - skin);
                transform.position += dir * allowed;
                remaining -= dir * allowed;

                remaining = Vector3.ProjectOnPlane(remaining, hit.normal);
                velocity = Vector3.ProjectOnPlane(velocity, hit.normal);

                // Si una pared bloquea el lateral, cambia de lado
                Vector3 flatNormal = new Vector3(hit.normal.x, 0f, hit.normal.z);
                if (flatNormal.sqrMagnitude > 0.1f)
                {
                    Vector3 toTargetFlat = new Vector3(target.position.x - transform.position.x, 0f,
                                                       target.position.z - transform.position.z);
                    Vector3 strafeVec = Vector3.Cross(Vector3.up, toTargetFlat).normalized * strafeDir;
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

    void ResetStrafeTimer()
    {
        strafeTimer = Random.Range(strafeChangeInterval.x, strafeChangeInterval.y);
    }

    // =============================================================== Helpers de detección

    // Ignora sus propios colliders y al jugador
    bool IsIgnored(Collider c)
    {
        Transform t = c.transform;
        return t.IsChildOf(transform) || (target != null && t.IsChildOf(target));
    }

    bool CastSphere(Vector3 origin, float castRadius, Vector3 dir, float distance, out RaycastHit best)
    {
        best = default;
        bool found = false;
        float bestDist = float.MaxValue;

        int count = Physics.SphereCastNonAlloc(origin, castRadius, dir, hitBuffer, distance,
                                               obstacleMask.value, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (h.distance <= 0f) continue;
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

    // ¿Hay una pared entre los dos puntos?
    bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float dist = d.magnitude;
        if (dist < 0.01f) return true;
        return !RaycastClean(from, d / dist, dist, obstacleMask.value, out RaycastHit losHit);
    }

    // =============================================================== Vida

    // Las balas del jugador llaman a este método automáticamente
    public void TakeDamage(float amount)
    {
        health -= amount;
        Wake();                                   // si lo golpean estando quieto, se activa
        if (health <= 0f) Die();
    }

    void Die()
    {
        // efectos al morir
        AudioSource.PlayClipAtPoint(
                    sonidoMuerte,
                    transform.position,
                    1
                );

        CrearExplosion(transform.position);
        DropHealingOrb();
        Destroy(gameObject);
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

    // =============================================================== Debug

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);

        if (target != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(target.position, preferredDistance);
        }
    }
}
