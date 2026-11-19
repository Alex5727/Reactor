using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Proteccion: los enemigos se mueven, asi que NUNCA deben estar marcados como Static.
// Si un enemigo tiene la marca Static (Batching Static), Unity junta su malla con el escenario
// al iniciar: el modelo se queda congelado donde estaba y el enemigo real se mueve invisible.
// Este script revisa la escena cada vez que das Play y les quita la marca automaticamente.
[InitializeOnLoad]
public static class EnemigosNoEstaticos
{
    static EnemigosNoEstaticos()
    {
        EditorApplication.playModeStateChanged += AlCambiarModo;
    }

    static void AlCambiarModo(PlayModeStateChange estado)
    {
        if (estado == PlayModeStateChange.ExitingEditMode) Revisar(false);
    }

    [MenuItem("Tools/Quitar Static de los enemigos")]
    static void DesdeMenu()
    {
        Revisar(true);
    }

    static void Revisar(bool avisarSiempre)
    {
        int corregidos = 0;
        foreach (TrackedEnemy e in Object.FindObjectsByType<TrackedEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // Desde la raiz del enemigo (algunos tienen el script en el modelo hijo)
            Transform raiz = e.transform;
            // (solo sube si el padre es un contenedor con un unico hijo, como en RayEnemy)
            while (raiz.parent != null && raiz.parent.childCount == 1)
                raiz = raiz.parent;

            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetStaticEditorFlags(t.gameObject) == 0) continue;
                Undo.RecordObject(t.gameObject, "Quitar Static de enemigos");
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                corregidos++;
            }
        }

        if (corregidos > 0)
            Debug.LogWarning("EnemigosNoEstaticos: se quito la marca Static a " + corregidos +
                             " objetos de enemigos (los enemigos se mueven y no deben ser Static). Guarda la escena.");
        else if (avisarSiempre)
            Debug.Log("EnemigosNoEstaticos: ningun enemigo tenia la marca Static.");
    }
}
