using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Comando secreto del menu: escribir "jefe" (sin campo de texto, no se ve nada en pantalla)
// carga la escena BossFight. Se crea solo cuando se abre la escena "Menu"; no hay que ponerlo en la escena.
public class CodigoSecreto : MonoBehaviour
{
    const string EscenaMenu = "Menu";
    const string Codigo = "jefe";
    const string EscenaDestino = "BossFight";
    const float TiempoEntreLetras = 2f;   // si tardas mas que esto entre letras, se reinicia

    string escrito = "";
    float ultimaLetra;
    bool cargando;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Registrar()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
        AlCargarEscena(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        if (!string.Equals(escena.name, EscenaMenu, System.StringComparison.OrdinalIgnoreCase)) return;
        if (FindFirstObjectByType<CodigoSecreto>() != null) return;
        GameObject go = new GameObject("CodigoSecreto");
        SceneManager.MoveGameObjectToScene(go, escena);
        go.AddComponent<CodigoSecreto>();
    }

    void OnEnable()
    {
        if (Keyboard.current != null) Keyboard.current.onTextInput += AlEscribir;
    }

    void OnDisable()
    {
        if (Keyboard.current != null) Keyboard.current.onTextInput -= AlEscribir;
    }

    void AlEscribir(char c)
    {
        if (cargando || char.IsControl(c)) return;
        if (Time.unscaledTime - ultimaLetra > TiempoEntreLetras) escrito = "";
        ultimaLetra = Time.unscaledTime;

        escrito += char.ToLowerInvariant(c);
        if (escrito.Length > Codigo.Length) escrito = escrito.Substring(escrito.Length - Codigo.Length);

        if (escrito == Codigo)
        {
            cargando = true;
            Controller controller = FindFirstObjectByType<Controller>();
            if (controller != null) controller.OscurecerYCargarEscena(EscenaDestino);
            else { Time.timeScale = 1f; SceneManager.LoadScene(EscenaDestino); }
        }
    }
}
