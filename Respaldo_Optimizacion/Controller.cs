using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class Controller : MonoBehaviour
{
    [Tooltip("Opcional: si se deja vacio, la escena se carga sin oscurecer la pantalla.")]
    public Image panelOscuro;
    public float duracionOscurecimiento = 2f;

    void Start()
    {
        // Sin panel no hay nada que preparar
        if (panelOscuro == null) return;

        // Comienza con la pantalla transparente
        Color color = panelOscuro.color;
        color.a = 0f;
        panelOscuro.color = color;
    }

    public void CargarEscena(string NombreScena)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(NombreScena);
    }

    public void OscurecerYCargarEscena(string NombreScena)
    {
        // Sin panel (o con el objeto inactivo) se carga directo, sin transicion
        if (panelOscuro == null || !isActiveAndEnabled)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            CargarEscena(NombreScena);
            return;
        }
        StartCoroutine(TransicionEscena(NombreScena));
    }

    IEnumerator TransicionEscena(string NombreScena)
    {
        float tiempo = 0f;

        Color color = panelOscuro.color;

        while (tiempo < duracionOscurecimiento)
        {
            tiempo += Time.unscaledDeltaTime;

            if (panelOscuro == null) break;   // el panel se destruyo durante la transicion
            color.a = Mathf.Lerp(0f, 1f, tiempo / duracionOscurecimiento);
            panelOscuro.color = color;

            yield return null;
        }

        // Asegurar que la pantalla esté completamente negra
        color.a = 1f;
        if (panelOscuro != null) panelOscuro.color = color;

        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(NombreScena);
    }

    public void Salir()
    {
        Application.Quit();
    }
}