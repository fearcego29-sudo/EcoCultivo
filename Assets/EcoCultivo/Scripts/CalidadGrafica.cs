using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public enum NivelCalidad
{
    Baja = 0,
    Alta = 1
}

/// <summary>
/// Perfil de calidad gráfica.
///
/// - BAJA (gama media): post-procesado APAGADO. Es exactamente el rendimiento medido hoy
///   (57-104 FPS, 25-55 SetPass, 120-240 Batches).
/// - ALTA (gama alta): post-procesado ENCENDIDO en la cámara y Global Volume activo
///   (Bloom, Tonemapping ACES, Vignette, Color Adjustments).
///
/// Selección:
/// 1. Automática al primer arranque: Alta si SystemInfo.systemMemorySize >= umbralMemoriaAltaMB
///    (7000 MB ≈ teléfonos de 8 GB, que reportan algo menos de su RAM nominal).
/// 2. Manual: el menú ≡ (MenuDesplegable) llama a Alternar(). La elección se guarda en PlayerPrefs.
///
/// No modifica Mobile_RPAsset, Render Scale ni MSAA: solo el interruptor "Post Processing"
/// de la cámara y el componente Volume.
/// </summary>
public class CalidadGrafica : MonoBehaviour
{
    [Tooltip("Vacío = Camera.main.")]
    public Camera camara;
    [Tooltip("Vacío = el primer Volume de la escena (Global Volume).")]
    public Volume volumen;

    [Tooltip("RAM mínima (MB) para elegir Alta automáticamente.")]
    public int umbralMemoriaAltaMB = 7000;

    /// <summary>-1 = automático, 0 = Baja, 1 = Alta.</summary>
    public const string KEY_CALIDAD = "EcoCultivo_Calidad";

    public NivelCalidad Nivel { get; private set; } = NivelCalidad.Baja;
    public event Action<NivelCalidad> CalidadCambiada;

    private UniversalAdditionalCameraData datosCamara;

    private void Awake()
    {
        if (camara == null) camara = Camera.main;
        if (volumen == null) volumen = FindFirstObjectByType<Volume>();
        if (camara != null) datosCamara = camara.GetUniversalAdditionalCameraData();

        int guardado = PlayerPrefs.GetInt(KEY_CALIDAD, -1);
        Aplicar(guardado < 0 ? Detectar() : (NivelCalidad)Mathf.Clamp(guardado, 0, 1), false);
    }

    /// <summary>Nivel recomendado para este dispositivo según su memoria RAM.</summary>
    public NivelCalidad Detectar()
    {
        return SystemInfo.systemMemorySize >= umbralMemoriaAltaMB ? NivelCalidad.Alta : NivelCalidad.Baja;
    }

    /// <summary>Cambia entre Alta y Baja y guarda la elección (botón del menú ≡).</summary>
    public void Alternar()
    {
        Aplicar(Nivel == NivelCalidad.Alta ? NivelCalidad.Baja : NivelCalidad.Alta, true);
    }

    /// <summary>Olvida la elección manual y vuelve a la detección automática.</summary>
    public void UsarAutomatico()
    {
        PlayerPrefs.DeleteKey(KEY_CALIDAD);
        PlayerPrefs.Save();
        Aplicar(Detectar(), false);
    }

    private void Aplicar(NivelCalidad nivel, bool guardar)
    {
        Nivel = nivel;
        bool alta = nivel == NivelCalidad.Alta;

        if (datosCamara != null) datosCamara.renderPostProcessing = alta;
        if (volumen != null) volumen.enabled = alta; // en Baja ni siquiera se evalúa el Volume

        if (guardar)
        {
            PlayerPrefs.SetInt(KEY_CALIDAD, (int)nivel);
            PlayerPrefs.Save();
        }

        if (CalidadCambiada != null) CalidadCambiada(nivel);
    }
}
