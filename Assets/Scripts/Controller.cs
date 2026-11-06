using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class Controller : MonoBehaviour
{
    public Image panelOscuro;
    public float duracionOscurecimiento = 2f;

    void Start()
    {
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
        StartCoroutine(TransicionEscena(NombreScena));
    }

    IEnumerator TransicionEscena(string NombreScena)
    {
        float tiempo = 0f;

        Color color = panelOscuro.color;

        while (tiempo < duracionOscurecimiento)
        {
            tiempo += Time.unscaledDeltaTime;

            color.a = Mathf.Lerp(0f, 1f, tiempo / duracionOscurecimiento);
            panelOscuro.color = color;

            yield return null;
        }

        // Asegurar que la pantalla esté completamente negra
        color.a = 1f;
        panelOscuro.color = color;

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