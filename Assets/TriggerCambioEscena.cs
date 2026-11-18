using UnityEngine;

public class TriggerCambioEscena : MonoBehaviour
{
    public Controller controller;
    public string nombreEscena;

    private bool activado;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !activado)
        {
            activado = true;
            // Primero la pantalla de puntuacion; al continuar se carga la escena
            ScoreManager.MostrarResultados(() => controller.OscurecerYCargarEscena(nombreEscena));
        }
    }
}