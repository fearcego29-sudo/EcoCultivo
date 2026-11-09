using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menú del Editor: EcoCultivo > Organizar carpetas del proyecto.
///
/// Reúne todo lo propio del proyecto en Assets/EcoCultivo con nombres claros:
///
///   Assets/
///   ├── EcoCultivo/
///   │   ├── Escenas/            Escena3 (principal)
///   │   │   └── Anteriores/     Escena1, Escena2 y respaldos
///   │   ├── Scripts/            código de la app
///   │   ├── Editor/             herramientas del menú EcoCultivo
///   │   ├── Modelos/            FBX
///   │   ├── Materiales/
///   │   ├── Texturas/
///   │   ├── Shaders/
///   │   ├── UI/                 logos e imágenes de interfaz
///   │   ├── Datos/              TextosApp, EstiloUI, EcoCultivo_PostFX
///   │   └── EntornoUrbano/      mallas, paleta y materiales de la ciudad
///   ├── Settings/               configuración de URP (sin cambios)
///   ├── TextMesh Pro/           paquete (sin cambios)
///   └── InputSystem_Actions     (sin cambios)
///
/// Usa AssetDatabase.MoveAsset: Unity conserva los GUID, así que NINGUNA referencia se rompe
/// (materiales, escenas, scripts, Build Settings). No borra archivos; solo quita carpetas que
/// quedan vacías después de mover.
/// </summary>
public static class OrganizarProyecto
{
    private const string RAIZ = "Assets/EcoCultivo";

    private struct Movimiento
    {
        public string desde, hacia;
        public Movimiento(string d, string h) { desde = d; hacia = h; }
    }

    [MenuItem("EcoCultivo/Organizar carpetas del proyecto")]
    private static void Organizar()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        List<Movimiento> plan = Planear();
        if (plan.Count == 0)
        {
            EditorUtility.DisplayDialog("EcoCultivo", "El proyecto ya está organizado. No hay nada que mover.", "OK");
            return;
        }

        StringBuilder sb = new StringBuilder("Se moverán (las referencias se conservan):\n\n");
        for (int i = 0; i < plan.Count; i++)
            sb.Append("• ").Append(plan[i].desde.Replace("Assets/", "")).Append("  →  ").Append(plan[i].hacia.Replace("Assets/", "")).Append('\n');
        sb.Append("\nNo se borra ningún archivo. Haz esto ANTES de subir a GitHub.");

        if (!EditorUtility.DisplayDialog("EcoCultivo — Organizar carpetas", sb.ToString(), "Organizar", "Cancelar")) return;

        // 1) Crear primero todas las carpetas destino (fuera de un lote, para que existan al mover).
        for (int i = 0; i < plan.Count; i++) CrearCarpeta(Padre(plan[i].hacia));

        // 2) Mover. MoveAsset conserva el GUID: las referencias no se rompen.
        List<string> errores = new List<string>();
        for (int i = 0; i < plan.Count; i++)
        {
            string error = AssetDatabase.MoveAsset(plan[i].desde, plan[i].hacia);
            if (!string.IsNullOrEmpty(error)) errores.Add(plan[i].desde + ": " + error);
        }

        BorrarSiVacia("Assets/Scenes");
        BorrarSiVacia("Assets/Resources");
        ActualizarBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (errores.Count == 0)
        {
            Debug.Log("[EcoCultivo] Proyecto organizado en " + RAIZ + " (" + plan.Count + " movimientos).");
            EditorUtility.DisplayDialog("EcoCultivo", "Listo: " + plan.Count + " elementos organizados en " + RAIZ + ".", "OK");
        }
        else
        {
            Debug.LogWarning("[EcoCultivo] Organización con avisos:\n" + string.Join("\n", errores));
            EditorUtility.DisplayDialog("EcoCultivo", "Terminado con " + errores.Count + " aviso(s). Revisa la Consola.", "OK");
        }
    }

    private static List<Movimiento> Planear()
    {
        List<Movimiento> plan = new List<Movimiento>();

        // Carpetas completas
        Agregar(plan, "Assets/Scripts", RAIZ + "/Scripts");
        Agregar(plan, "Assets/FBX", RAIZ + "/Modelos");
        Agregar(plan, "Assets/Materials", RAIZ + "/Materiales");
        Agregar(plan, "Assets/Textures", RAIZ + "/Texturas");
        Agregar(plan, "Assets/Shaders", RAIZ + "/Shaders");
        Agregar(plan, "Assets/UI", RAIZ + "/UI");
        Agregar(plan, "Assets/Urbano", RAIZ + "/EntornoUrbano");
        Agregar(plan, "Assets/Editor", RAIZ + "/Editor");

        // Escenas: la principal arriba, el resto en "Anteriores"
        if (AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
            for (int i = 0; i < guids.Length; i++)
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guids[i]);
                string nombre = System.IO.Path.GetFileNameWithoutExtension(ruta);
                string destino = nombre == "Escena3"
                    ? RAIZ + "/Escenas/Escena3.unity"
                    : RAIZ + "/Escenas/Anteriores/" + nombre.Replace(" ", "_") + ".unity";
                Agregar(plan, ruta, destino);
            }
        }

        // Datos del proyecto que hoy viven entre la configuración de URP
        Agregar(plan, "Assets/Settings/TextosApp.asset", RAIZ + "/Datos/TextosApp.asset");
        Agregar(plan, "Assets/Settings/EstiloUI.asset", RAIZ + "/Datos/EstiloUI.asset");
        Agregar(plan, "Assets/Settings/EcoCultivo_PostFX.asset", RAIZ + "/Datos/EcoCultivo_PostFX.asset");

        return plan;
    }

    private static void Agregar(List<Movimiento> plan, string desde, string hacia)
    {
        bool existe = AssetDatabase.IsValidFolder(desde) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(desde, AssetPathToGUIDOptions.OnlyExistingAssets));
        bool destinoLibre = !AssetDatabase.IsValidFolder(hacia) && string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(hacia, AssetPathToGUIDOptions.OnlyExistingAssets));
        if (existe && destinoLibre) plan.Add(new Movimiento(desde, hacia));
    }

    private static void ActualizarBuildSettings()
    {
        EditorBuildSettingsScene[] escenas = EditorBuildSettings.scenes;
        for (int i = 0; i < escenas.Length; i++)
        {
            string ruta = AssetDatabase.GUIDToAssetPath(escenas[i].guid.ToString());
            if (!string.IsNullOrEmpty(ruta)) escenas[i] = new EditorBuildSettingsScene(ruta, escenas[i].enabled);
        }
        EditorBuildSettings.scenes = escenas;
    }

    private static void BorrarSiVacia(string carpeta)
    {
        if (!AssetDatabase.IsValidFolder(carpeta)) return;
        if (AssetDatabase.FindAssets("", new[] { carpeta }).Length == 0 && AssetDatabase.GetSubFolders(carpeta).Length == 0)
            AssetDatabase.DeleteAsset(carpeta);
    }

    private static string Padre(string ruta)
    {
        int i = ruta.LastIndexOf('/');
        return i > 0 ? ruta.Substring(0, i) : ruta;
    }

    /// <summary>Crea la carpeta (y sus padres) si no existe.</summary>
    public static void CrearCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = Padre(ruta);
        if (!AssetDatabase.IsValidFolder(padre)) CrearCarpeta(padre);
        AssetDatabase.CreateFolder(padre, ruta.Substring(padre.Length + 1));
    }
}
