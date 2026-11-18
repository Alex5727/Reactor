using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class Controller : MonoBehaviour
{
    [Tooltip("Opcional: si se deja vacio, la escena se carga sin oscurecer la pantalla.")]
    public Image panelOscuro;
    public float duracionOscurecimiento = 2f;

    [Tooltip("Activalo en escenas de menu: al iniciar libera el cursor y quita la pausa.")]
    public bool liberarCursorAlIniciar = false;

    void Start()
    {
        if (liberarCursorAlIniciar)
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Sin panel no hay nada que preparar
        if (panelOscuro == null) return;

        // Comienza con la pantalla transparente
        Color color = panelOscuro.color;
        color.a = 0f;
        panelOscuro.color = color;
    }

    public void CargarEscena(string NombreScena)
    {
        string escena = BuscarEscena(NombreScena);
        if (escena == null) return;

        Time.timeScale = 1f;
        SceneManager.LoadScene(escena);
    }

    // Busca la escena en la lista del build sin importar mayusculas/minusculas
    // ("Nivel" encuentra "NIvel"). Si no existe, avisa en la consola en vez de fallar en silencio.
    static string BuscarEscena(string nombre)
    {
        if (string.IsNullOrEmpty(nombre))
        {
            Debug.LogError("Controller: no se indico el nombre de la escena a cargar.");
            return null;
        }

        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string ruta = SceneUtility.GetScenePathByBuildIndex(i);
            string n = System.IO.Path.GetFileNameWithoutExtension(ruta);
            if (string.Equals(n, nombre, System.StringComparison.OrdinalIgnoreCase))
                return n;
        }

        Debug.LogError("Controller: la escena '" + nombre + "' no existe o no esta agregada en File > Build Profiles > Scene List.");
        return null;
    }

    public void OscurecerYCargarEscena(string NombreScena)
    {
        if (BuscarEscena(NombreScena) == null) return;

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
        SceneManager.LoadScene(BuscarEscena(NombreScena));
    }

    public void Salir()
    {
        Application.Quit();
    }
}