using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Menú del Editor: EcoCultivo > Crear perfil de post-procesado.
/// Crea Assets/Settings/EcoCultivo_PostFX.asset con los valores de la Tarea 3.
/// Después solo hay que arrastrarlo al campo Profile del "Global Volume" (ver PASOS_UNITY.md).
///
/// Si el perfil ya existe, lo ACTUALIZA en su lugar (conserva el mismo archivo y GUID),
/// así el Global Volume no pierde la referencia.
///
/// Versión para GAMA MEDIA: Bloom queda en intensidad 0 (URP omite su pasada por completo).
/// Para activarlo en un teléfono de gama alta: Bloom > Intensity 0.15, Threshold 1.5, Clamp 8.
///
/// Es un script de EDITOR: no se incluye en el APK ni afecta al rendimiento.
/// No modifica la escena ni Mobile_RPAsset; solo crea un asset nuevo.
/// </summary>
public static class CrearPerfilPostFX
{
    // Ubicación tras "Organizar carpetas". Si el perfil está en otro lugar, se encuentra por nombre.
    private const string RUTA_POR_DEFECTO = "Assets/EcoCultivo/Datos/EcoCultivo_PostFX.asset";
    private static string RUTA;

    [MenuItem("EcoCultivo/Crear perfil de post-procesado")]
    private static void Crear()
    {
        RUTA = BuscarRuta("EcoCultivo_PostFX t:VolumeProfile", RUTA_POR_DEFECTO);
        OrganizarProyecto.CrearCarpeta(RUTA.Substring(0, RUTA.LastIndexOf('/')));
        VolumeProfile perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RUTA);
        if (perfil != null)
        {
            bool actualizar = EditorUtility.DisplayDialog(
                "EcoCultivo",
                "Ya existe " + RUTA + ".\n¿Actualizarlo con los valores recomendados?\n(Se conserva el archivo: el Global Volume sigue apuntando a él.)",
                "Actualizar", "Cancelar");
            if (!actualizar) return;

            // Vaciar los efectos actuales sin borrar el archivo.
            for (int i = perfil.components.Count - 1; i >= 0; i--)
            {
                VolumeComponent c = perfil.components[i];
                if (c != null)
                {
                    AssetDatabase.RemoveObjectFromAsset(c);
                    UnityEngine.Object.DestroyImmediate(c, true);
                }
            }
            perfil.components.Clear();
        }
        else
        {
            perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(perfil, RUTA);
        }

        // ---------------- Bloom: APAGADO para gama media (intensidad 0 = URP no ejecuta su pasada).
        // Se deja configurado y con límite de brillo (Clamp) para encenderlo sin sorpresas en gama alta.
        Bloom bloom = Agregar<Bloom>(perfil);
        bloom.threshold.Override(1.5f);
        bloom.intensity.Override(0f);
        bloom.scatter.Override(0.5f);
        bloom.clamp.Override(8f); // evita que una zona muy brillante "explote" en un resplandor enorme
        bloom.highQualityFiltering.Override(false);
        // Estos dos se fijan por nombre para no depender del tipo exacto del parámetro en tu versión de URP.
        FijarPorNombre(bloom, "downscale", "Quarter");
        FijarPorNombre(bloom, "maxIterations", 3);

        // ---------------- Tonemapping ACES
        Tonemapping tonemapping = Agregar<Tonemapping>(perfil);
        tonemapping.mode.Override(TonemappingMode.ACES);

        // ---------------- Vignette suave
        Vignette vignette = Agregar<Vignette>(perfil);
        vignette.color.Override(Color.black);
        vignette.center.Override(new Vector2(0.5f, 0.5f));
        vignette.intensity.Override(0.2f);
        vignette.smoothness.Override(0.4f);
        vignette.rounded.Override(false);

        // ---------------- Color Adjustments leves
        ColorAdjustments color = Agregar<ColorAdjustments>(perfil);
        color.postExposure.Override(0.1f); // compensa el leve oscurecimiento de ACES
        color.contrast.Override(8f);
        color.saturation.Override(6f);

        // ---------------- Desactivados explícitamente (aunque otro Volume los encienda)
        MotionBlur motionBlur = Agregar<MotionBlur>(perfil);
        motionBlur.intensity.Override(0f);

        DepthOfField dof = Agregar<DepthOfField>(perfil);
        dof.mode.Override(DepthOfFieldMode.Off);

        FilmGrain grano = Agregar<FilmGrain>(perfil);
        grano.intensity.Override(0f);

        EditorUtility.SetDirty(perfil);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = perfil;
        EditorGUIUtility.PingObject(perfil);
        Debug.Log("[EcoCultivo] Perfil creado: " + RUTA + ". Arrástralo al campo Profile de 'Global Volume'.");
    }

    /// <summary>Ruta de un asset existente encontrado por nombre; si no existe, la ruta por defecto.</summary>
    private static string BuscarRuta(string filtro, string porDefecto)
    {
        string[] guids = AssetDatabase.FindAssets(filtro);
        return guids.Length > 0 ? AssetDatabase.GUIDToAssetPath(guids[0]) : porDefecto;
    }

    private static T Agregar<T>(VolumeProfile perfil) where T : VolumeComponent
    {
        T c = perfil.Add<T>(false);
        c.name = typeof(T).Name;
        c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(c, perfil);
        return c;
    }

    /// <summary>Activa un parámetro por su nombre y le asigna un valor (enum por nombre o número).</summary>
    private static void FijarPorNombre(VolumeComponent comp, string campo, object valor)
    {
        FieldInfo f = comp.GetType().GetField(campo, BindingFlags.Public | BindingFlags.Instance);
        VolumeParameter p = f != null ? f.GetValue(comp) as VolumeParameter : null;
        PropertyInfo prop = p != null ? p.GetType().GetProperty("value", BindingFlags.Public | BindingFlags.Instance) : null;

        if (prop == null)
        {
            Debug.LogWarning("[EcoCultivo] Parámetro '" + campo + "' no disponible en esta versión de URP; se deja por defecto.");
            return;
        }

        try
        {
            Type tipo = prop.PropertyType;
            object v = tipo.IsEnum ? Enum.Parse(tipo, valor.ToString()) : Convert.ChangeType(valor, tipo);
            prop.SetValue(p, v);
            p.overrideState = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[EcoCultivo] No se pudo fijar '" + campo + "': " + e.Message);
        }
    }
}
