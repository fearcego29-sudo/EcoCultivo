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
///
/// CAMBIOS (Ronda 8 — sombras de las lámparas):
/// - Las 8 Spot Lights proyectan sombras de noche. Calidad BAJA: sombras duras (más
///   baratas). Calidad ALTA: sombras suaves. Se ajusta solo al cambiar la calidad en el menú ≡.
/// - shadowNearPlane se sube para que la pantalla de la lámpara no bloquee su propia luz.
/// - Requiere en Mobile_RPAsset: Additional Lights > Cast Shadows activado y
///   Per Object Limit = 8 (ver PASOS_UNITY.md, Ronda 8).
///
/// CAMBIOS (Ronda 9 — rendimiento de noche, conservando las sombras):
/// - Sombra con malla simple (SombrasLOD): los maceteros proyectan su sombra con la malla
///   del último LOD (~8 mil triángulos en lugar de ~41 mil). Se ven igual de detallados.
/// - De noche el sol (luz direccional) deja de calcular sombras: con intensidad 0 no se
///   ve, pero seguía dibujando su mapa de sombras. Al volver al día se restaura.
///
/// CAMBIOS (Ronda 10 — conos de las lámparas y sombras en calidad Baja):
/// - Conos más cerrados (100° → 70°) y alcance 5 → 4 m: cada lámpara ilumina sobre todo su
///   macetero, en vez de casi todo el invernadero. Menos objetos por sombra y menos pixeles
///   que calcular. Las 8 lámparas siguen encendidas.
/// - Calidad BAJA: solo 4 lámparas proyectan sombra (la más cercana a cada macetero). Las 8
///   siguen alumbrando. Calidad ALTA: las 8 con sombra suave, como antes.
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

    [Header("Sombras de las lámparas (Ronda 8)")]
    [Tooltip("Las Spot Lights proyectan sombras en modo noche.")]
    public bool sombrasLamparas = true;
    [Tooltip("Distancia mínima (m) desde la luz a la que empieza a calcular sombras. Evita que la pantalla de la lámpara tape su propia luz.")]
    [Range(0.05f, 1f)] public float sombraPlanoCercano = 0.3f;
    [Tooltip("Intensidad de la sombra (1 = negra; menos = deja pasar algo de luz ambiental).")]
    [Range(0f, 1f)] public float fuerzaSombra = 0.85f;
    [Tooltip("Ronda 9: los maceteros proyectan sombra con su malla simple (último LOD). Desmarcar si aparecen manchas de sombra raras sobre los canales.")]
    public bool sombraConMallaSimple = true;

    [Header("Conos de las lámparas (Ronda 10)")]
    [Tooltip("Cerrar los conos de las Spot Lights al iniciar. Desmarcar para dejar los valores de la escena (100°, 5 m).")]
    public bool ajustarConos = true;
    [Tooltip("Apertura del cono (grados). En la escena estaba en 100.")]
    [Range(30f, 120f)] public float anguloCono = 70f;
    [Tooltip("Alcance de la luz (m). En la escena estaba en 5.")]
    [Range(2f, 8f)] public float alcanceLampara = 4f;
    [Tooltip("En calidad Baja, solo la lámpara más cercana a cada macetero proyecta sombra (4 de 8).")]
    public bool sombrasReducidasEnBaja = true;

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
    private CalidadGrafica calidad;
    private Light[] lucesSpot = new Light[0];
    // true = esta lámpara proyecta sombra también en calidad Baja (una por macetero).
    private bool[] sombraEnBaja = new bool[0];

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
    private LightShadows sombraSolDia = LightShadows.Soft;
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

        // Spot Lights de las lámparas (incluye las inactivas: el grupo está apagado de día).
        if (grupoSpotLights != null)
        {
            lucesSpot = grupoSpotLights.GetComponentsInChildren<Light>(true);
        }

        // Guardar el "día" tal como está configurado en la escena.
        if (luzDireccional != null)
        {
            intensidadLuzDia = luzDireccional.intensity;
            sombraSolDia = luzDireccional.shadows;
        }
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
        if (calidad != null) calidad.CalidadCambiada += AplicarSombras;
    }

    private void OnDisable()
    {
        if (ui != null)
        {
            ui.TransicionIniciada -= AlIniciarTransicion;
            ui.PanelCambiado -= AlCambiarPanel;
        }
        if (calidad != null) calidad.CalidadCambiada -= AplicarSombras;
    }

    private void Start()
    {
        // Ronda 10: conos más cerrados y elección de las lámparas con sombra en Baja.
        if (ajustarConos) AjustarConos();
        ElegirSombrasEnBaja();

        // Sombras según la calidad actual (CalidadGrafica ya eligió su nivel en su Awake).
        calidad = GetComponent<CalidadGrafica>();
        if (calidad == null)
        {
            // Búsqueda en la escena sin APIs obsoletas (solo una vez, al iniciar).
            GameObject[] raices = gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < raices.Length && calidad == null; i++)
                calidad = raices[i].GetComponentInChildren<CalidadGrafica>(true);
        }
        if (calidad != null)
        {
            calidad.CalidadCambiada -= AplicarSombras; // por si OnEnable ya lo suscribió
            calidad.CalidadCambiada += AplicarSombras;
            AplicarSombras(calidad.Nivel);
        }
        else
        {
            AplicarSombras(NivelCalidad.Baja);
        }

        // Ronda 9: sombras de los maceteros con la malla simple (una sola vez).
        if (sombraConMallaSimple)
        {
            int n = SombrasLOD.Aplicar(gameObject.scene);
            if (n > 0) Debug.Log("[InteractiveController] Sombra con malla simple en " + n + " maceteros.", this);
        }

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

    /// <summary>Sombras de las lámparas: duras en Baja, suaves en Alta (o ninguna si se desactivan).</summary>
    private void AplicarSombras(NivelCalidad nivel)
    {
        LightShadows tipo = !sombrasLamparas ? LightShadows.None
                          : nivel == NivelCalidad.Alta ? LightShadows.Soft
                          : LightShadows.Hard;

        bool baja = nivel != NivelCalidad.Alta;
        for (int i = 0; i < lucesSpot.Length; i++)
        {
            Light l = lucesSpot[i];
            if (l == null) continue;
            // Ronda 10: en Baja, solo una lámpara por macetero proyecta sombra.
            bool conSombra = !(baja && sombrasReducidasEnBaja && i < sombraEnBaja.Length && !sombraEnBaja[i]);
            l.shadows = conSombra ? tipo : LightShadows.None;
            l.shadowNearPlane = sombraPlanoCercano;
            l.shadowStrength = fuerzaSombra;
        }
    }

    /// <summary>Ronda 10: cierra el cono y el alcance de las Spot Lights (la escena guardada no cambia).</summary>
    private void AjustarConos()
    {
        for (int i = 0; i < lucesSpot.Length; i++)
        {
            Light l = lucesSpot[i];
            if (l == null || l.type != LightType.Spot) continue;
            // Mantiene la proporción entre el cono interior y el exterior (70/100 en la escena).
            float proporcion = l.spotAngle > 0f ? Mathf.Clamp01(l.innerSpotAngle / l.spotAngle) : 0.7f;
            l.spotAngle = anguloCono;
            l.innerSpotAngle = anguloCono * proporcion;
            l.range = alcanceLampara;
        }
    }

    /// <summary>
    /// Ronda 10: marca, para cada macetero (LOD Group), la lámpara más cercana en el plano
    /// horizontal. Esas proyectan sombra en calidad Baja. Sin LOD Groups: una de cada dos.
    /// </summary>
    private void ElegirSombrasEnBaja()
    {
        sombraEnBaja = new bool[lucesSpot.Length];

        var grupos = new System.Collections.Generic.List<LODGroup>(8);
        GameObject[] raices = gameObject.scene.GetRootGameObjects();
        for (int i = 0; i < raices.Length; i++)
            grupos.AddRange(raices[i].GetComponentsInChildren<LODGroup>(true));

        if (grupos.Count == 0)
        {
            for (int i = 0; i < sombraEnBaja.Length; i++) sombraEnBaja[i] = i % 2 == 0;
            return;
        }

        for (int g = 0; g < grupos.Count; g++)
        {
            Vector3 centro = grupos[g].transform.TransformPoint(grupos[g].localReferencePoint);
            int mejor = -1;
            float mejorDist = float.MaxValue;
            for (int i = 0; i < lucesSpot.Length; i++)
            {
                if (lucesSpot[i] == null || sombraEnBaja[i]) continue;
                Vector3 d = lucesSpot[i].transform.position - centro;
                d.y = 0f;
                float dist = d.sqrMagnitude;
                if (dist < mejorDist) { mejorDist = dist; mejor = i; }
            }
            if (mejor >= 0) sombraEnBaja[mejor] = true;
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
            // Ronda 9: de noche el sol no se ve, así que tampoco calcula sombras.
            luzDireccional.shadows = esNoche ? LightShadows.None : sombraSolDia;
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
