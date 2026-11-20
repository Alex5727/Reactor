using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Va en el Toggle "SprintConfig" del Canvas.
// Activado   -> el dash del jugador se hace con Shift (izquierdo).
// Desactivado -> el dash se hace con Bloq Mayus (CapsLock).
[RequireComponent(typeof(Toggle))]
public class SprintConfig : MonoBehaviour
{
    public Key teclaActivado = Key.LeftShift;
    public Key teclaDesactivado = Key.CapsLock;
    public PlayerMovement jugador;     // opcional: si esta vacio se busca solo

    const string Clave = "DashConShift";
    Toggle toggle;

    void Awake()
    {
        toggle = GetComponent<Toggle>();
        // Recupera la opcion guardada (se conserva entre escenas y al cerrar el juego)
        if (PlayerPrefs.HasKey(Clave)) toggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(Clave) == 1);
        toggle.onValueChanged.AddListener(Aplicar);
        toggle.onValueChanged.AddListener(Guardar);
    }

    void Guardar(bool activado)
    {
        PlayerPrefs.SetInt(Clave, activado ? 1 : 0);
        PlayerPrefs.Save();
    }

    void Start()
    {
        Aplicar(toggle.isOn);
    }

    void OnDestroy()
    {
        if (toggle != null) { toggle.onValueChanged.RemoveListener(Aplicar); toggle.onValueChanged.RemoveListener(Guardar); }
    }

    void Aplicar(bool activado)
    {
        if (jugador == null) jugador = FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        if (jugador == null) return;
        jugador.dashKey = activado ? teclaActivado : teclaDesactivado;
    }
}
