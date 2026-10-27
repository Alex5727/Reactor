using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    
    [Header("Velocidades")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 10f;
    public float groundAcceleration = 60f;
    public float airAcceleration = 25f;

    [Header("Salto")]
    public float jumpHeight = 1.6f;
    public int maxAirJumps = 1;              // 1 = doble salto
    public float gravity = -28f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;

    [Header("Dash")]
    public Key dashKey = Key.CapsLock;       // cámbiala si CapsLock te falla
    public float dashSpeed = 30f;
    public float dashDecayGround = 7f;
    public float dashDecayAir = 4f;
    public float dashCooldown = 0.8f;

    [Header("Vida")]
    public float MaxHealt;
    public TMP_Text MotrarVida;
    public Image BarraDeVida;

    private CharacterController controller;
    private Vector3 horizontalVelocity;
    private Vector3 dashVelocity;
    private float verticalVelocity;

    private float coyoteTimer;
    private float jumpBufferTimer;
    private int airJumpsLeft;
    private float dashCooldownTimer;
    private float health;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        airJumpsLeft = maxAirJumps;
        health = MaxHealt;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        float dt = Time.deltaTime;
        bool grounded = controller.isGrounded;

        // ---------- Input ----------
        Vector2 input = Vector2.zero;
        if (kb.wKey.isPressed) input.y += 1f;
        if (kb.sKey.isPressed) input.y -= 1f;
        if (kb.dKey.isPressed) input.x += 1f;
        if (kb.aKey.isPressed) input.x -= 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 wishDir = transform.right * input.x + transform.forward * input.y;

        bool sprinting = kb.leftShiftKey.isPressed && input.y > 0f;
        float targetSpeed = sprinting ? sprintSpeed : walkSpeed;

        if (kb.spaceKey.wasPressedThisFrame) jumpBufferTimer = jumpBufferTime;

        // ---------- Suelo ----------
        if (grounded)
        {
            coyoteTimer = coyoteTime;
            airJumpsLeft = maxAirJumps;
            if (verticalVelocity < 0f) verticalVelocity = -2f;
        }

        coyoteTimer -= dt;
        jumpBufferTimer -= dt;
        dashCooldownTimer -= dt;

        // ---------- Movimiento horizontal ----------
        Vector3 targetVelocity = wishDir * targetSpeed;
        float accel = grounded ? groundAcceleration : airAcceleration;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, accel * dt);

        // ---------- Salto / doble salto ----------
        if (jumpBufferTimer > 0f)
        {
            if (coyoteTimer > 0f)
            {
                Jump();
                coyoteTimer = 0f;
            }
            else if (airJumpsLeft > 0)
            {
                Jump();
                airJumpsLeft--;
            }
        }

        // ---------- Dash (se suma, no cancela nada) ----------
        if (kb[dashKey].wasPressedThisFrame && dashCooldownTimer <= 0f)
        {
            Vector3 dashDir = wishDir.sqrMagnitude > 0.01f ? wishDir.normalized : transform.forward;
            dashVelocity += dashDir * dashSpeed;
            dashCooldownTimer = dashCooldown;
        }

        float decay = grounded ? dashDecayGround : dashDecayAir;
        dashVelocity = Vector3.Lerp(dashVelocity, Vector3.zero, 1f - Mathf.Exp(-decay * dt));

        // ---------- Gravedad ----------
        verticalVelocity += gravity * dt;

        // ---------- Mover: todo se suma ----------
        Vector3 finalVelocity = horizontalVelocity + dashVelocity + Vector3.up * verticalVelocity;
        controller.Move(finalVelocity * dt);

        if ((controller.collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;


        //Interfaz
        MotrarVida.text = health + "/" + MaxHealt;
        BarraDeVida.fillAmount = health / MaxHealt;
    }

    void Jump()
    {
        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        jumpBufferTimer = 0f;
    }

    // Las balas llaman a este método automáticamente
    public void TakeDamage(float amount)
    {
        health -= amount;
        if (health <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}