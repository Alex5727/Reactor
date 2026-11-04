using UnityEngine;

public class TriggerCambioEscena : MonoBehaviour
{
    public Controller controller;
    public string nombreEscena;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            controller.OscurecerYCargarEscena(nombreEscena);
        }
    }
}