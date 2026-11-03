using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public interface IDamageable
{
    void TakeDamage(float amount);
}

public class Weapon : MonoBehaviour
{
    [Header("Referencias")]
    public Camera playerCamera;
    public Transform muzzle;                 // GameObject en la boquilla del arma
    public Transform weaponTransform;        // Transform del modelo del arma
    public GameObject destello;
    public AudioSource audioSource;
    public AudioClip sonidoShoot;
    public AudioClip RechardSound;
    public AudioClip hitSound;               // "tic" al golpear un enemigo

    [Header("Disparo")]
    public float fireRate = 3.5f;            // disparos por segundo
    public bool automatic = false;           // false = un disparo por click
    public float damage = 10f;
    public float maxAimDistance = 200f;
    public LayerMask aimMask = ~0;           // quita la capa del Player

    [Header("Efectos de disparo")]
    public BulletTracer tracerPrefab;        // prefab con LineRenderer + BulletTracer
    public GameObject impactPrefab;          // partículas de impacto
    public float hitStopTime = 0.035f;

    [Header("Recarga")]
    public float AmountBullets = 6;
    public float ContBullets;
    public float TimeRechard = 1.5f;
    public float TimerRechard;
    public Image barraRecarga;
    public Image barraRecarga2;
    public TMP_Text textoContadorBalas;

    [Header("Retroceso")]
    public float recoildForce = 0.2f;        // retroceso hacia atrás
    public float recoilRotation = 6f;        // grados hacia arriba
    public float recoilReturnSpeed = 12f;

    [Header("Cámara")]
    public float fovPunch = 2.5f;            // el FOV se abre al disparar
    public float fovReturnSpeed = 12f;

    private bool IsRecarging;
    private float nextFireTime;

    private Vector3 initialWeaponPosition;
    private Vector3 recoilPosition;
    private Quaternion initialWeaponRotation;
    private Quaternion recoilRot;
    private float baseFov;
    private Coroutine hitStopRoutine;

    void Reset()
    {
        playerCamera = GetComponentInParent<Camera>();
    }

    void Start()
    {
        ContBullets = AmountBullets;
        IsRecarging = false;
        TimerRechard = 0f;

        initialWeaponPosition = weaponTransform.localPosition;
        recoilPosition = initialWeaponPosition;
        initialWeaponRotation = weaponTransform.localRotation;
        recoilRot = initialWeaponRotation;
        baseFov = playerCamera.fieldOfView;

        BulltsUI();
    }

    void Update()
    {
        // Esto va primero para que siga funcionando aunque el disparo esté bloqueado
        UpdateRecoil();
        UpdateReload();

        if (Mouse.current == null || SpecialRoomManager.ShootingLocked) return;

        bool wantsToShoot = automatic
            ? Mouse.current.leftButton.isPressed
            : Mouse.current.leftButton.wasPressedThisFrame;

        if (wantsToShoot && Time.time >= nextFireTime && !IsRecarging && ContBullets > 0)
        {
            nextFireTime = Time.time + 1f / fireRate;
            Shoot();
        }

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            Rechair();
        }
    }

    // ---------------- DISPARO ----------------

    void Shoot()
    {
        Recoil();

        // Rayo desde el centro de la cámara (donde está la mira)
        // 1) Rayo de la cámara: define el punto al que apunta la mira
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 aimPoint = TryGetHit(ray.origin, ray.direction, maxAimDistance, aimMask, out RaycastHit aimHit)
            ? aimHit.point
            : ray.origin + ray.direction * maxAimDistance;

        // 2) Rayo desde la boquilla hacia ese punto: detecta el impacto real (como hacía la bala)
        Vector3 toAim = aimPoint - muzzle.position;
        bool didHit = TryGetHit(muzzle.position, toAim.normalized, toAim.magnitude + 0.5f, ~0, out RaycastHit hit);
        Vector3 end = didHit ? hit.point : aimPoint;

        if (didHit)
        {
            bool confirmedHit = false;
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();

            if (target != null)
            {
                target.TakeDamage(damage);
                confirmedHit = true;
            }
            else
            {
                hit.collider.SendMessageUpwards("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            }

            if (impactPrefab != null)
            {
                GameObject fx = Instantiate(impactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(fx, 2f);
            }

            if (confirmedHit)
            {
                if (hitSound != null) audioSource.PlayOneShot(hitSound, 0.8f);
                HitStop();
            }
        }

        // Trazo visual desde la boquilla hasta el punto de impacto
        if (tracerPrefab != null)
        {
            BulletTracer tracer = Instantiate(tracerPrefab);
            tracer.Init(muzzle.position, end);
        }

        // Sonido con variación de tono para que no suene repetitivo
        audioSource.pitch = Random.Range(0.95f, 1.05f);
        audioSource.PlayOneShot(sonidoShoot);

        // Destello
        if (destello != null)
        {
            GameObject flash = Instantiate(
                destello,
                muzzle.position,
                muzzle.rotation * Quaternion.Euler(0f, 90f, 0f),
                muzzle);
            Destroy(flash, 0.1f);
        }

        // Punch de FOV
        playerCamera.fieldOfView = baseFov + fovPunch;

        ContBullets--;
        BulltsUI();

        if (ContBullets <= 0)
            StartReload();
    }

    // ---------------- RECARGA ----------------

    public void Rechair()
    {
        if (IsRecarging || ContBullets >= AmountBullets) return;
        StartReload();
    }

    void StartReload()
    {
        IsRecarging = true;
        TimerRechard = TimeRechard;

        audioSource.pitch = 1f;
        audioSource.PlayOneShot(RechardSound);

        BulltsUI();
    }

    void UpdateReload()
    {
        if (IsRecarging)
        {
            TimerRechard -= Time.deltaTime;

            if (TimerRechard <= 0f)
            {
                TimerRechard = 0f;
                ContBullets = AmountBullets;
                IsRecarging = false;
                BulltsUI();
            }
        }

        float fill = IsRecarging ? TimerRechard / TimeRechard : 0f;
        if (barraRecarga != null) barraRecarga.fillAmount = fill;
        if (barraRecarga2 != null) barraRecarga2.fillAmount = fill;
    }

    // ---------------- RETROCESO Y FEEDBACK ----------------

    void Recoil()
    {
        recoilPosition += Vector3.back * recoildForce;
        recoilRot *= Quaternion.Euler(-recoilRotation, Random.Range(-1f, 1f), 0f);
    }

    void UpdateRecoil()
    {
        float dt = Time.deltaTime;

        // El arma sigue al objetivo de retroceso...
        weaponTransform.localPosition = Vector3.Lerp(
            weaponTransform.localPosition, recoilPosition, recoilReturnSpeed * dt);
        weaponTransform.localRotation = Quaternion.Slerp(
            weaponTransform.localRotation, recoilRot, recoilReturnSpeed * 2f * dt);

        // ...y el objetivo vuelve gradualmente a la posición inicial
        recoilPosition = Vector3.Lerp(recoilPosition, initialWeaponPosition, recoilReturnSpeed * dt);
        recoilRot = Quaternion.Slerp(recoilRot, initialWeaponRotation, recoilReturnSpeed * dt);

        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, baseFov, fovReturnSpeed * dt);
    }

    void HitStop()
    {
        // No hacer hit-stop si el juego está en pausa
        if (Time.timeScale < 0.5f && hitStopRoutine == null) return;

        if (hitStopRoutine != null) StopCoroutine(hitStopRoutine);
        hitStopRoutine = StartCoroutine(HitStopRoutine());
    }

    IEnumerator HitStopRoutine()
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(hitStopTime);
        Time.timeScale = 1f;
        hitStopRoutine = null;
    }

    void OnDisable()
    {
        // Evita que el juego se quede en cámara lenta si el arma se desactiva en pleno hit-stop
        if (hitStopRoutine != null)
        {
            Time.timeScale = 1f;
            hitStopRoutine = null;
        }
    }

    // ---------------- UI ----------------

    void BulltsUI()
    {
        if (textoContadorBalas == null) return;

        textoContadorBalas.text = IsRecarging
            ? "Recargando..."
            : "Balas: " + ContBullets;
    }

    bool TryGetHit(Vector3 origin, Vector3 dir, float dist, int mask, out RaycastHit result)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, dir, dist, mask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Transform myRoot = transform.root;
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.root == myRoot) continue; // ignora al jugador y su arma
            result = h;
            return true;
        }

        result = default;
        return false;
    }
}