using System;
using UnityEngine;

/// <summary>
/// Controles de la vista 3D/AR: Día/Noche, Flujo de Agua y Reset de Cámara.
/// Los tres métodos públicos originales se conservan (los botones de Tarjeta_ControlesAR
/// ya están conectados a ellos).
///
/// CAMBIOS:
/// - Día/Noche: la lógica vive en AplicarModo(); DynamicGI.UpdateEnvironment() solo se
///   llama al CAMBIAR de estado (nunca en Update). Sin allocations.
/// - Al volver a "día" se restauran los valores ORIGINALES de la escena (la luz direccional
///   está en 2, no en 1; antes quedaba más oscura tras un ciclo noche→día).
/// - Persistencia: el modo Día/Noche elegido se guarda en PlayerPrefs.
/// - Ahorro: al perder el foco o pausar la app, el agua se oculta y el frame rate baja a 30.
///   Al volver se restaura el estado anterior.
/// - Reset de cámara: avisa a SimpleTouchMove para que el siguiente giro no salte.
/// - targetFrameRate se mantiene en 60 en la vista 3D.
///
/// CAMBIOS (Tareas 3-5):
/// - 30 FPS en pantallas de menú (Splash, Menú, Objetivo, Créditos) para ahorrar batería.
///   Durante las transiciones sube a 60 para que los fundidos se vean suaves.
///   Prioridad: ahorro (30) > transición (60) > menú (30) > vista 3D (60).
/// - Expone EsNoche / AguaActiva y el evento EstadoCambiado (lo usa EstadoControlesAR
///   para resaltar los botones activos).
/// </summary>
public class InteractiveController : MonoBehaviour
{
    [Header("Iluminación (Día / Noche)")]
    public Light luzDireccional;
    public GameObject grupoSpotLights;
    public GameObject camaraPrincipal;

    [Header("Materiales de Cielo (Opcional)")]
    [Tooltip("Si se deja vacío, se usa el skybox original de la escena.")]
    public Material skyboxDia;

    [Tooltip("Luz ambiental de noche (antes 0.05). 0.15 deja ver el jardín exterior sin romper el ambiente nocturno.")]
    [Range(0f, 0.5f)] public float intensidadAmbienteNoche = 0.15f;

    [Header("Simulación de Agua")]
    public GameObject sistemaAguaPVC;

    [Header("Frame rate")]
    [Tooltip("FPS objetivo en uso normal. NO cambiar sin revisar la propuesta en PASOS_UNITY.md.")]
    public int fpsObjetivo = 60;
    [Tooltip("FPS mientras la app no tiene el foco (ahorro de batería).")]
    public int fpsEnPausa = 30;
    [Tooltip("FPS en pantallas de menú (contenido estático).")]
    public int fpsMenus = 30;
    [Tooltip("Activar solo para probar la pausa en el Editor (si no, al hacer clic en el Inspector se ocultaría el agua).")]
    public bool probarPausaEnEditor = false;

    // Clave pública para que UIManager pueda borrarla desde su menú de pruebas.
    public const string KEY_MODO_NOCHE = "EcoCultivo_ModoNoche";

    private bool esNoche = false;
    private bool aguaActiva = false;
    private bool enAhorro = false;
    private bool enMenu = false;
    private bool enTransicionUI = false;
    private UIManager ui;
    private RiegoNFT riego;

    /// <summary>Animación de riego por etapas (null si no hay sistema de agua).</summary>
    public RiegoNFT Riego { get { return riego; } }

    /// <summary>Estado actual (solo lectura) para la UI.</summary>
    public bool EsNoche { get { return esNoche; } }
    public bool AguaActiva { get { return aguaActiva; } }

    /// <summary>Se dispara al cambiar Día/Noche o el Flujo de Agua.</summary>
    public event Action EstadoCambiado;

    // Caché de componentes para optimizar rendimiento de CPU (Cero GC Alloc en runtime)
    private Vector3 posicionInicialCamara;
    private Quaternion rotacionInicialCamara;
    private Camera camComponente;
    private Transform transformCamara;
    private SimpleTouchMove controlCamara;

    // Valores originales de la escena (se restauran al volver a "día").
    private float intensidadLuzDia = 1f;
    private float intensidadAmbienteDia = 1f;
    private Material skyboxOriginal;

    // Color oscuro para la noche (Azul noche profundo, no blanco)
    private readonly Color colorNoche = new Color(0.02f, 0.04f, 0.08f);

    private void Awake()
    {
        // Forzar 60 FPS fijos y desactivar VSync para Android
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = fpsObjetivo;

        if (camaraPrincipal == null && Camera.main != null)
        {
            camaraPrincipal = Camera.main.gameObject;
        }

        if (camaraPrincipal != null)
        {
            transformCamara = camaraPrincipal.transform;
            posicionInicialCamara = transformCamara.position;
            rotacionInicialCamara = transformCamara.rotation;
            camComponente = camaraPrincipal.GetComponent<Camera>();
            controlCamara = camaraPrincipal.GetComponent<SimpleTouchMove>();

            // RESGUARDAR QUE LA CÁMARA MANTENGA EL SKYBOX AL INICIAR
            if (camComponente != null)
            {
                camComponente.clearFlags = CameraClearFlags.Skybox;
            }
        }

        ui = GetComponent<UIManager>(); // ambos viven en UIManager_Object

        // Riego NFT por etapas: se usa el que esté en Grupo_Agua_NFT_ALL o se agrega en runtime
        // (la escena guardada no cambia).
        if (sistemaAguaPVC != null)
        {
            riego = sistemaAguaPVC.GetComponent<RiegoNFT>();
            if (riego == null) riego = sistemaAguaPVC.AddComponent<RiegoNFT>();
        }

        // Guardar el "día" tal como está configurado en la escena.
        if (luzDireccional != null) intensidadLuzDia = luzDireccional.intensity;
        intensidadAmbienteDia = RenderSettings.ambientIntensity;
        skyboxOriginal = RenderSettings.skybox;
    }

    private void OnEnable()
    {
        if (ui != null)
        {
            ui.TransicionIniciada += AlIniciarTransicion;
            ui.PanelCambiado += AlCambiarPanel;
        }
    }

    private void OnDisable()
    {
        if (ui != null)
        {
            ui.TransicionIniciada -= AlIniciarTransicion;
            ui.PanelCambiado -= AlCambiarPanel;
        }
    }

    private void Start()
    {
        if (riego != null)
        {
            // Con riego por etapas el grupo queda activo; cada canal se oculta/llena por separado.
            sistemaAguaPVC.SetActive(true);
            riego.FijarInmediato(false);
        }
        else if (sistemaAguaPVC != null)
        {
            sistemaAguaPVC.SetActive(false);
        }

        // Restaurar el modo elegido la última vez. Si es "día" no se toca nada,
        // así el arranque queda idéntico al de antes.
        if (PlayerPrefs.GetInt(KEY_MODO_NOCHE, 0) == 1)
        {
            AplicarModo(true);
        }
    }

    // 1. Botón Día / Noche
    public void AlternarDiaNoche()
    {
        AplicarModo(!esNoche);

        PlayerPrefs.SetInt(KEY_MODO_NOCHE, esNoche ? 1 : 0);
        PlayerPrefs.Save();
    }

    // 2. Botón Flujo de Agua
    public void AlternarFlujoAgua()
    {
        aguaActiva = !aguaActiva;

        if (riego != null)
        {
            // Llenado/vaciado canal por canal (si la app está en ahorro, se anima al volver).
            riego.Regar(aguaActiva);
        }
        else if (sistemaAguaPVC != null && !enAhorro)
        {
            sistemaAguaPVC.SetActive(aguaActiva);
        }

        if (EstadoCambiado != null) EstadoCambiado();
    }

    // 3. Botón Reset Cámara
    public void ResetearCamara()
    {
        if (transformCamara != null)
        {
            transformCamara.position = posicionInicialCamara;
            transformCamara.rotation = rotacionInicialCamara;

            // Sin esto, SimpleTouchMove conserva su yaw/pitch anterior y el siguiente
            // giro devuelve la cámara a donde estaba antes del reset.
            if (controlCamara != null) controlCamara.SincronizarRotacion();
        }
    }

    /// <summary>Aplica Día (false) o Noche (true). Solo hace trabajo si el estado cambia.</summary>
    private void AplicarModo(bool noche)
    {
        if (noche == esNoche) return; // nada que hacer: evita llamar a DynamicGI sin cambio
        esNoche = noche;

        if (luzDireccional != null)
        {
            luzDireccional.intensity = esNoche ? 0.0f : intensidadLuzDia;
        }

        if (grupoSpotLights != null)
        {
            grupoSpotLights.SetActive(esNoche);
        }

        if (camComponente != null)
        {
            if (esNoche)
            {
                camComponente.clearFlags = CameraClearFlags.SolidColor;
                camComponente.backgroundColor = colorNoche;
            }
            else
            {
                camComponente.clearFlags = CameraClearFlags.Skybox;
                RenderSettings.skybox = skyboxDia != null ? skyboxDia : skyboxOriginal;
            }
        }

        RenderSettings.ambientIntensity = esNoche ? intensidadAmbienteNoche : intensidadAmbienteDia;

        // Una sola vez por cambio de estado (recalcula la iluminación ambiental).
        DynamicGI.UpdateEnvironment();

        if (EstadoCambiado != null) EstadoCambiado();
    }

    // ===================== Frame rate según pantalla =====================

    private void AlIniciarTransicion(GameObject destino)
    {
        enTransicionUI = true;
        ActualizarFrameRate();
    }

    private void AlCambiarPanel(GameObject panel)
    {
        enTransicionUI = false;
        enMenu = ui != null && ui.panelAR != null && panel != ui.panelAR;
        ActualizarFrameRate();
    }

    private void ActualizarFrameRate()
    {
        int fps;
        if (enAhorro) fps = fpsEnPausa;
        else if (enMenu && !enTransicionUI) fps = fpsMenus;
        else fps = fpsObjetivo;

        if (Application.targetFrameRate != fps) Application.targetFrameRate = fps;
    }

    // ===================== Ahorro de batería =====================

    // Android: se llama al ir a segundo plano (Home, cambiar de app, apagar pantalla).
    private void OnApplicationPause(bool pausa)
    {
        if (!PausaPermitida()) return;
        if (pausa) EntrarAhorro(); else SalirAhorro();
    }

    // Android: se llama al perder el foco SIN ir a segundo plano
    // (barra de notificaciones, diálogo del sistema). Aquí la app sigue dibujando,
    // así que es donde bajar a 30 FPS realmente ahorra batería.
    private void OnApplicationFocus(bool tieneFoco)
    {
        if (!PausaPermitida()) return;
        if (tieneFoco) SalirAhorro(); else EntrarAhorro();
    }

    private bool PausaPermitida()
    {
#if UNITY_EDITOR
        return probarPausaEnEditor;
#else
        return true;
#endif
    }

    private void EntrarAhorro()
    {
        if (enAhorro) return;
        enAhorro = true;

        ActualizarFrameRate();
        if (riego != null) riego.Congelado = true; // pausa la animación del agua
        else if (sistemaAguaPVC != null && aguaActiva) sistemaAguaPVC.SetActive(false);
    }

    private void SalirAhorro()
    {
        if (!enAhorro) return;
        enAhorro = false;

        ActualizarFrameRate();
        if (riego != null) riego.Congelado = false;
        else if (sistemaAguaPVC != null) sistemaAguaPVC.SetActive(aguaActiva);
        if (controlCamara != null) controlCamara.SincronizarRotacion(); // suelta dedos "atascados"
    }
}
