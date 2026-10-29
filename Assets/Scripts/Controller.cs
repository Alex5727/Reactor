using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Controller : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    public void CargarEscena(string NombreScena)
    {
        SceneManager.LoadScene(NombreScena);
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void Salir()
    {
        Application.Quit();
    }
}