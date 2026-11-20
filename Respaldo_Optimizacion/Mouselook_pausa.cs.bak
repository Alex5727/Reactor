
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MouseLook : MonoBehaviour
{
    public Transform playerBody;
    public float sensitivity = 0.1f;

    public Slider SliderSensitivity;

    [Header("Menu de pausa")]
    public GameObject menuPausa;

    private float xRotation;
    private bool enPausa = false;


    void Start()
    {
        sensitivity = SliderSensitivity.value;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        menuPausa.SetActive(false);
    }

    void Update()
    {
        sensitivity = SliderSensitivity.value;

        // Detectar la tecla P
        if (Keyboard.current != null &&
            Keyboard.current.pKey.wasPressedThisFrame)
        {
            if (enPausa)
                Reanudar();
            else
                Pausar();
        }

        // Si está pausado, no mover la cámara
        if (enPausa) return;

        if (Mouse.current == null) return;

        Vector2 delta = Mouse.current.delta.ReadValue() * sensitivity;

        xRotation = Mathf.Clamp(xRotation - delta.y, -89f, 89f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        playerBody.Rotate(Vector3.up * delta.x);
    }

    public void Pausar()
    {
        enPausa = true;

        Time.timeScale = 0f;

        menuPausa.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Reanudar()
    {
        enPausa = false;

        Time.timeScale = 1f;

        menuPausa.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
