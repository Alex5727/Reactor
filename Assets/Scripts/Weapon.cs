using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

public class Weapon : MonoBehaviour
{
    [Header("Referencias")]
    public Camera playerCamera;          // la cámara del jugador
    public Transform muzzle;             // GameObject en la boquilla del arma
    public GameObject bulletPrefab;

    [Header("Disparo")]
    public float fireRate = 8f;          // balas por segundo
    public bool automatic = true;        // true = mantener click, false = un disparo por click
    public float bulletSpeed = 80f;
    public float maxAimDistance = 200f;  // a qué distancia apunta si no hay nada enfrente
    public LayerMask aimMask = ~0;       // qué capas puede "ver" la mira (quita la capa del Player)


    private float nextFireTime;

    [Header("Recarga")]
    public float AmountBullets;
    public float ContBullets;
    public float TimeRechard = 3;
    public float TimerRechard;
    private bool IsRecarging;
    public Image barraRecarga;
    public Image barraRecarga2;


    [Header("Retroceso")]
    public float recoildForce;
    public Transform weaponTransform;  // Transform del modelo del arma
    public float recoilReturnSpeed = 8f; // Velocidad de regreso

    private Vector3 initialWeaponPosition;
    private Vector3 recoilPosition;


    public TMP_Text textoContadorBalas;

    void Start()
    {
        ContBullets = AmountBullets;
        IsRecarging = false;

        initialWeaponPosition = weaponTransform.localPosition;
        recoilPosition = initialWeaponPosition;
    }


    void Reset()
    {
        playerCamera = GetComponentInParent<Camera>();
    }

    void Update()
    {
        if (ContBullets <= 0)
        {
            IsRecarging = true;
        }

        if (TimerRechard > 0) {
            TimerRechard -= Time.deltaTime;
        }

        if (TimerRechard < 0 && IsRecarging == true)
        {
            ContBullets = AmountBullets;
            IsRecarging = false;
        }


        if (Mouse.current == null) return;

        bool wantsToShoot = automatic
            ? Mouse.current.leftButton.isPressed
            : Mouse.current.leftButton.wasPressedThisFrame;

        if (wantsToShoot && Time.time >= nextFireTime && TimerRechard <= 0 && ContBullets > 0)
        {
            nextFireTime = Time.time + 1f / fireRate;
            Shoot();
        }

        textoContadorBalas.text = "Balas: " + ContBullets;

        if (Keyboard.current != null &&
           Keyboard.current.rKey.wasPressedThisFrame && ContBullets != AmountBullets && IsRecarging == false)
        {
            ContBullets = 0;
            IsRecarging = true;
            TimerRechard = TimeRechard;
        }


        barraRecarga.fillAmount = TimerRechard / TimeRechard;
        barraRecarga2.fillAmount = TimerRechard / TimeRechard;


        weaponTransform.localPosition = Vector3.Lerp(
    weaponTransform.localPosition,
    recoilPosition,
    recoilReturnSpeed * Time.deltaTime
);

        // Recupera gradualmente la posición inicial
        recoilPosition = Vector3.Lerp(
            recoilPosition,
            initialWeaponPosition,
            recoilReturnSpeed * Time.deltaTime
        );



    }

    void Shoot()
    {
        Recoil();

        // 1) Rayo desde el CENTRO de la cámara (donde está la mira)
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        Vector3 targetPoint;
        if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore))
            targetPoint = hit.point;
        else
            targetPoint = ray.origin + ray.direction * maxAimDistance;

        // 2) La bala sale de la boquilla pero va hacia el punto que apunta la mira
        Vector3 direction = (targetPoint - muzzle.position).normalized;

        GameObject bullet = Instantiate(bulletPrefab, muzzle.position, Quaternion.LookRotation(direction));

        if (bullet.TryGetComponent(out Bullet b))
            b.speed = bulletSpeed;

        ContBullets = ContBullets - 1;

        if (ContBullets <= 0)
        {
            TimerRechard = TimeRechard;
            IsRecarging = true;
        }
    }

    private void Recoil()
    {
        recoilPosition += Vector3.back * recoildForce;
    }


}