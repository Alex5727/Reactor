using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Configurar Jefe Soldado
// Convierte el objeto "JefeSoldado" de la escena abierta en el jefe jugable:
//  1) Crea el Animator Controller "JefeSoldado.controller" con las animaciones de SciFiWarriorPBRHPPolyart.
//  2) Se lo asigna al Animator del jefe.
//  3) Agrega CharacterController (ajustado al tamano del modelo) y el script BossSoldier.
//  4) Convierte los materiales del pack (shader Standard, que en URP se ve rosa) a URP Lit.
// Despues guarda la escena.
public static class ConfigurarJefe
{
    const string NombreObjeto = "JefeSoldado";
    const string CarpetaPack = "Assets/SciFiWarriorPBRHPPolyart";
    const string RutaController = CarpetaPack + "/Animators/JefeSoldado.controller";

    // estado del controller -> archivo de animacion
    static readonly (string estado, string fbx)[] Estados =
    {
        ("Idle",            "Idle_guard_ar"),
        ("ApuntarQuieto",   "Idle_Shoot_ar"),
        ("Correr",          "Run_guard_AR"),
        ("CaminarAdelante", "WalkFront_Shoot_ar"),
        ("CaminarAtras",    "WalkBack_Shoot_ar"),
        ("CaminarIzq",      "WalkLeft_Shoot_ar"),
        ("CaminarDer",      "WalkRight_Shoot_ar"),
        ("Rafaga",          "Shoot_BurstShot_AR"),
        ("Automatico",      "Shoot_AutoShot_AR"),
        ("DisparoUnico",    "Shoot_SingleShot_AR"),
        ("Agachado",        "Idle_Ducking_ar"),
        ("Salto",           "Jump"),
        ("Recarga",         "Reload"),
        ("Muerte",          "Die"),
    };

    [MenuItem("Tools/Configurar Jefe Soldado")]
    static void Configurar()
    {
        GameObject jefe = BuscarEnEscena(NombreObjeto);
        if (jefe == null)
        {
            EditorUtility.DisplayDialog("Configurar Jefe", "No encontre un objeto llamado \"" + NombreObjeto +
                                        "\" en la escena abierta.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(jefe, "Configurar Jefe");

        // 1) Animator Controller
        AnimatorController controller = CrearController(out List<string> faltantes);

        // 2) Animator
        Animator anim = jefe.GetComponentInChildren<Animator>();
        if (anim == null) anim = jefe.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // 3) CharacterController ajustado al modelo
        CharacterController cc = jefe.GetComponent<CharacterController>();
        if (cc == null) cc = jefe.AddComponent<CharacterController>();
        AjustarCapsula(jefe, cc);

        if (jefe.GetComponent<BossSoldier>() == null) jefe.AddComponent<BossSoldier>();
        if (jefe.CompareTag("Player")) jefe.tag = "Untagged";

        // 4) Materiales
        int convertidos = ConvertirMateriales(jefe);

        EditorSceneManager.MarkSceneDirty(jefe.scene);
        Selection.activeGameObject = jefe;

        string msg = "Listo. \"" + NombreObjeto + "\" ya es el jefe.\n\n" +
                     "- Animator Controller: " + RutaController + "\n" +
                     "- Materiales convertidos a URP: " + convertidos + "\n";
        if (faltantes.Count > 0) msg += "- Animaciones no encontradas: " + string.Join(", ", faltantes) + "\n";
        msg += "\nAjusta los valores en el componente Boss Soldier si quieres y guarda la escena (Ctrl+S).";
        EditorUtility.DisplayDialog("Configurar Jefe", msg, "OK");
    }

    static AnimatorController CrearController(out List<string> faltantes)
    {
        faltantes = new List<string>();
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(RutaController) != null)
            AssetDatabase.DeleteAsset(RutaController);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(RutaController);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        int i = 0;
        foreach (var (estado, fbx) in Estados)
        {
            string ruta = CarpetaPack + "/Animations/" + fbx + ".fbx";
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ruta)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null) { faltantes.Add(fbx); continue; }

            AnimatorState st = sm.AddState(estado, new Vector3(250 + (i % 3) * 230, 60 + (i / 3) * 70, 0));
            st.motion = clip;
            if (estado == "Idle") sm.defaultState = st;
            i++;
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    static void AjustarCapsula(GameObject jefe, CharacterController cc)
    {
        Renderer[] rs = jefe.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);

        Vector3 escala = jefe.transform.lossyScale;
        float sy = Mathf.Max(escala.y, 0.0001f);
        float sx = Mathf.Max(escala.x, 0.0001f);

        float alto = b.size.y / sy;
        float radio = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) * 0.35f / sx, 0.3f, alto * 0.3f);
        Vector3 centroLocal = jefe.transform.InverseTransformPoint(b.center);

        cc.height = alto;
        cc.radius = radio;
        cc.center = new Vector3(0f, centroLocal.y, 0f);
        cc.stepOffset = Mathf.Min(0.4f, alto * 0.25f);
        cc.slopeLimit = 45f;
        cc.skinWidth = Mathf.Max(0.02f, radio * 0.1f);
    }

    static int ConvertirMateriales(GameObject jefe)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) return 0;
        int n = 0;
        var vistos = new HashSet<Material>();
        foreach (Renderer r in jefe.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || !vistos.Add(m)) continue;
                if (m.shader == null || m.shader.name != "Standard") continue;

                Texture principal = Tex(m, "_MainTex");
                Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                Texture normal = Tex(m, "_BumpMap");
                Texture metal = Tex(m, "_MetallicGlossMap");
                Texture oclusion = Tex(m, "_OcclusionMap");
                Texture emision = Tex(m, "_EmissionMap");
                Color colorEmision = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
                float metalico = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
                float suavidad = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.5f;

                Undo.RecordObject(m, "Convertir material");
                m.shader = lit;
                m.SetTexture("_BaseMap", principal);
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Metallic", metalico);
                m.SetFloat("_Smoothness", suavidad);
                if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
                if (metal != null) { m.SetTexture("_MetallicGlossMap", metal); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                if (oclusion != null) { m.SetTexture("_OcclusionMap", oclusion); m.EnableKeyword("_OCCLUSIONMAP"); }
                if (emision != null || colorEmision.maxColorComponent > 0.01f)
                {
                    if (emision != null) m.SetTexture("_EmissionMap", emision);
                    m.SetColor("_EmissionColor", colorEmision);
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                EditorUtility.SetDirty(m);
                n++;
            }
        }
        AssetDatabase.SaveAssets();
        return n;
    }

    static Texture Tex(Material m, string prop) => m.HasProperty(prop) ? m.GetTexture(prop) : null;

    static GameObject BuscarEnEscena(string nombre)
    {
        var escena = EditorSceneManager.GetActiveScene();
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nombre) return t.gameObject;
        return null;
    }
}
