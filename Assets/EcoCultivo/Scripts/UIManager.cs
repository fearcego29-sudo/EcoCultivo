using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Navegación entre paneles de Main_UI_Canvas.
///
/// Se conservan TODOS los métodos públicos originales (AbrirMenu, AbrirObjetivo,
/// AbrirCreditos, AbrirVista3D_AR), así que los OnClick ya conectados siguen funcionando.
///
/// CAMBIOS:
/// - Solo un panel activo a la vez (garantizado al iniciar y en cada transición).
/// - Transición con CanvasGroup: fade out del panel anterior + fade in del nuevo
///   (duracionTransicion = 0.25 s en total). Durante la transición se bloquea la
///   interacción para evitar el doble toque.
///   Si un panel no tiene CanvasGroup, se agrega en runtime (la escena guardada no cambia).
/// - Historial: Escape (PC) / Back (Android) regresa al panel anterior, o al menú si no
///   hay historial. Durante el Splash, Back no hace nada. En el menú (raíz), la primera
///   pulsación muestra "Presiona Atrás de nuevo para salir" y la segunda cierra la app.
/// - Persistencia: recuerda en PlayerPrefs si el Splash ya se vio.
/// - PermitirControlCamara: true solo con la vista 3D/AR abierta (lo usa SimpleTouchMove).
///
/// CAMBIOS (Tareas 3-5):
/// - Splash con entrada suave (fade in + logos que crecen levemente) y salida suave.
///   Todo corre en corrutinas: no bloquea la carga.
/// - Duraciones tomadas de EstiloUI (si existe); si no, de los campos de este componente.
/// - Eventos TransicionIniciada / PanelCambiado: los usan InteractiveController (30 FPS en
///   menús) y MenuDesplegable (se cierra al cambiar de panel).
/// - InterceptorAtras: permite que el menú desplegable se cierre con Atrás antes de navegar.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Páneles UI")]
    public GameObject panelSplash;
    public GameObject panelMenu;
    public GameObject panelObjetivo;
    public GameObject panelCreditos;
    public GameObject panelAR;

    [Header("Ajustes")]
    public float tiempoSplash = 2.5f;

    [Tooltip("Duración TOTAL de la transición (mitad fade out, mitad fade in).")]
    public float duracionTransicion = 0.25f;

    [Tooltip("Si está activo, el Splash solo se muestra la primera vez que se abre la app.")]
    public bool omitirSplashSiYaSeVio = true;

    [Header("Salir con Atrás (Android)")]
    [Tooltip("En el menú principal, pulsar Atrás dos veces seguidas cierra la app (convención de Android).")]
    public bool salirConDobleAtras = true;
    [Tooltip("Segundos máximos entre las dos pulsaciones.")]
    public float ventanaDobleAtras = 2f;
    [Tooltip("Opcional: asset TextosApp, para tomar de ahí el aviso \"Presiona Atrás de nuevo para salir\".")]
    public TextosApp textos;

    // ---------- Estado compartido (lectura desde otros scripts) ----------

    /// <summary>True solo cuando la vista 3D/AR está abierta y no hay transición en curso.
    /// Vale true por defecto para que, sin UIManager en escena, la cámara funcione como antes.</summary>
    public static bool PermitirControlCamara { get; private set; } = true;

    /// <summary>Panel visible actualmente (null durante una transición).</summary>
    public static GameObject PanelActual { get; private set; }

    /// <summary>Se dispara al EMPEZAR una transición (argumento: panel destino).</summary>
    public event Action<GameObject> TransicionIniciada;
    /// <summary>Se dispara cuando un panel queda visible e interactivo.</summary>
    public event Action<GameObject> PanelCambiado;
    /// <summary>Si devuelve true, el botón Atrás ya fue atendido (p. ej. cerrar el menú desplegable).</summary>
    public Func<bool> InterceptorAtras;

    // ---------- Claves de PlayerPrefs ----------
    private const string KEY_SPLASH_VISTO = "EcoCultivo_SplashVisto";

    // ---------- Estado interno ----------
    private GameObject panelActual;
    private bool enTransicion;
    private readonly Stack<GameObject> historial = new Stack<GameObject>(8);
    private readonly Dictionary<GameObject, CanvasGroup> grupos = new Dictionary<GameObject, CanvasGroup>(8);
    private GameObject[] todosLosPaneles;
    private float tiempoUltimoAtras = -100f;

    // Reinicia los estáticos al entrar a Play (por si "Enter Play Mode Options" desactiva el domain reload).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarEstaticos()
    {
        PermitirControlCamara = true;
        PanelActual = null;
    }

    private void Awake()
    {
        todosLosPaneles = new GameObject[] { panelSplash, panelMenu, panelObjetivo, panelCreditos, panelAR };

        // Caché de CanvasGroup (una sola búsqueda, fuera de Update).
        for (int i = 0; i < todosLosPaneles.Length; i++)
        {
            GameObject p = todosLosPaneles[i];
            if (p == null || grupos.ContainsKey(p)) continue;

            CanvasGroup cg = p.GetComponent<CanvasGroup>();
            if (cg == null) cg = p.AddComponent<CanvasGroup>();
            grupos.Add(p, cg);
        }
    }

    private void Start()
    {
        OcultarTodos();

        bool splashVisto = PlayerPrefs.GetInt(KEY_SPLASH_VISTO, 0) == 1;

        if (panelSplash != null && !(omitirSplashSiYaSeVio && splashVisto))
        {
            StartCoroutine(SecuenciaSplash());
        }
        else
        {
            // Sin Splash: el menú también aparece con un fundido corto.
            StartCoroutine(EntradaInicial(panelMenu, DuracionTransicion));
        }
    }

    private void Update()
    {
        // En Android, el botón Back llega como KeyCode.Escape (Input clásico, activo en modo "Both").
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Regresar();
        }
    }

    private void OnDisable()
    {
        // Si el objeto se desactiva a media transición, no dejar la UI bloqueada.
        enTransicion = false;
    }

    // ---------- Duraciones (EstiloUI manda si existe) ----------
    private float DuracionTransicion
    {
        get { return EstiloUI.Activo != null ? EstiloUI.Activo.duracionTransicion : duracionTransicion; }
    }
    private float DuracionEntradaSplash
    {
        get { return EstiloUI.Activo != null ? EstiloUI.Activo.duracionEntradaSplash : 0.5f; }
    }
    private float DuracionSalidaSplash
    {
        get { return EstiloUI.Activo != null ? EstiloUI.Activo.duracionSalidaSplash : 0.45f; }
    }

    /// <summary>Splash: entrada suave → espera → salida suave al menú.</summary>
    private IEnumerator SecuenciaSplash()
    {
        yield return StartCoroutine(EntradaInicial(panelSplash, DuracionEntradaSplash));
        yield return new WaitForSecondsRealtime(tiempoSplash);

        PlayerPrefs.SetInt(KEY_SPLASH_VISTO, 1);
        PlayerPrefs.Save();

        // Salida más lenta que una transición normal (el doble de la duración total = fade out + fade in).
        if (DestinoValido(panelMenu) && !enTransicion)
            StartCoroutine(Transicion(panelActual, panelMenu, DuracionSalidaSplash * 2f));
    }

    private bool DestinoValido(GameObject p)
    {
        return p != null && p != panelActual;
    }

    /// <summary>Muestra el primer panel con fade in. En el Splash, los logos crecen de 92% a 100%.</summary>
    private IEnumerator EntradaInicial(GameObject panel, float duracion)
    {
        if (panel == null) yield break;

        enTransicion = true;
        PermitirControlCamara = false;
        if (TransicionIniciada != null) TransicionIniciada(panel);

        OcultarTodosExcepto(panel);
        CanvasGroup cg = Grupo(panel);
        if (cg != null)
        {
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
        panel.SetActive(true);

        // Logos del Splash (solo escala; no cambia la jerarquía).
        Transform logo1 = panel == panelSplash ? panel.transform.Find("Img_LogoEcoCultivo") : null;
        Transform logo2 = panel == panelSplash ? panel.transform.Find("Img_LogoMarista") : null;
        Vector3 escala1 = logo1 != null ? logo1.localScale : Vector3.one;
        Vector3 escala2 = logo2 != null ? logo2.localScale : Vector3.one;

        float t = 0f;
        while (t < duracion)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, duracion));
            float suave = 1f - (1f - k) * (1f - k); // ease-out
            if (cg != null) cg.alpha = suave;
            float esc = Mathf.Lerp(0.92f, 1f, suave);
            if (logo1 != null) logo1.localScale = escala1 * esc;
            if (logo2 != null) logo2.localScale = escala2 * esc;
            yield return null;
        }
        if (logo1 != null) logo1.localScale = escala1;
        if (logo2 != null) logo2.localScale = escala2;

        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
        FijarPanelActual(panel);
        enTransicion = false;
    }

    // =====================================================================
    // Métodos públicos (los mismos que ya usan los botones en el Inspector)
    // =====================================================================

    public void AbrirMenu()
    {
        Navegar(panelMenu, false);
    }

    public void AbrirObjetivo()
    {
        Navegar(panelObjetivo, true);
    }

    public void AbrirCreditos()
    {
        Navegar(panelCreditos, true);
    }

    public void AbrirVista3D_AR()
    {
        Navegar(panelAR, true);
    }

    /// <summary>Regresa al panel anterior; si no hay historial, al menú.
    /// En el menú (raíz): primera pulsación muestra un aviso, segunda (en menos de 2 s) cierra la app.
    /// Lo usan Escape/Back y se puede conectar a cualquier botón "Volver".</summary>
    public void Regresar()
    {
        // Primero, quien haya pedido atender Atrás (p. ej. el menú desplegable abierto).
        if (InterceptorAtras != null && InterceptorAtras()) return;

        if (enTransicion) return;
        if (panelActual == null || panelActual == panelSplash) return; // no interrumpir el Splash

        if (panelActual == panelMenu)
        {
            IntentarSalir();
            return;
        }

        if (historial.Count > 0)
            Navegar(historial.Pop(), false);
        else
            AbrirMenu();
    }

    // =====================================================================
    // Salir de la app (doble Atrás en el menú)
    // =====================================================================

    private void IntentarSalir()
    {
        if (!salirConDobleAtras) return;

        float ahora = Time.unscaledTime;
        if (ahora - tiempoUltimoAtras <= ventanaDobleAtras)
        {
            Debug.Log("[UIManager] Saliendo de la app (en el Editor no hace nada).");
            Application.Quit();
            return;
        }

        tiempoUltimoAtras = ahora;
        TextosApp t = textos != null ? textos : TextosApp.Activo;
        string mensaje = (t != null && !string.IsNullOrEmpty(t.mensajeSalir))
            ? t.mensajeSalir
            : "Presiona Atrás de nuevo para salir";
        MostrarAvisoNativo(mensaje);
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // Actividad de Android en caché: se usa desde el hilo de UI de Android,
    // así que NO se libera con "using" (se liberaría antes de mostrarse el aviso).
    private static AndroidJavaObject actividadAndroid;
#endif

    /// <summary>Muestra un "Toast" nativo de Android (no requiere objetos en la escena).
    /// En el Editor solo escribe el mensaje en la Consola.</summary>
    private static void MostrarAvisoNativo(string mensaje)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (actividadAndroid == null)
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    actividadAndroid = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                }
            }

            AndroidJavaObject actividad = actividadAndroid;
            actividad.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                using (AndroidJavaClass toastCls = new AndroidJavaClass("android.widget.Toast"))
                using (AndroidJavaObject toast = toastCls.CallStatic<AndroidJavaObject>("makeText", actividad, mensaje, 0)) // 0 = LENGTH_SHORT
                {
                    toast.Call("show");
                }
            }));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[UIManager] No se pudo mostrar el aviso: " + e.Message);
        }
#else
        Debug.Log("[UIManager] " + mensaje);
#endif
    }

    // =====================================================================
    // Lógica interna
    // =====================================================================

    private void Navegar(GameObject destino, bool registrarEnHistorial)
    {
        if (destino == null || enTransicion || destino == panelActual) return;

        if (registrarEnHistorial && panelActual != null && panelActual != panelSplash)
            historial.Push(panelActual);

        // El menú es la raíz: al llegar a él se limpia el historial.
        if (destino == panelMenu)
            historial.Clear();

        StartCoroutine(Transicion(panelActual, destino, DuracionTransicion));
    }

    private IEnumerator Transicion(GameObject origen, GameObject destino, float duracionTotal)
    {
        enTransicion = true;
        panelActual = null;
        PanelActual = null;
        PermitirControlCamara = false;
        if (TransicionIniciada != null) TransicionIniciada(destino);

        float mitad = Mathf.Max(0f, duracionTotal * 0.5f);

        // 1. Fade out del panel anterior (sin interacción).
        if (origen != null)
        {
            CanvasGroup cgOrigen = Grupo(origen);
            if (cgOrigen != null)
            {
                cgOrigen.interactable = false;
                cgOrigen.blocksRaycasts = false;
                yield return StartCoroutine(Fundido(cgOrigen, cgOrigen.alpha, 0f, mitad));
            }
            origen.SetActive(false);
        }

        // Garantía: ningún otro panel queda activo.
        OcultarTodosExcepto(destino);

        // 2. Fade in del panel nuevo (la interacción se habilita al terminar).
        CanvasGroup cgDestino = Grupo(destino);
        if (cgDestino != null)
        {
            cgDestino.alpha = 0f;
            cgDestino.interactable = false;
            cgDestino.blocksRaycasts = false;
        }
        destino.SetActive(true);

        if (cgDestino != null)
        {
            yield return StartCoroutine(Fundido(cgDestino, 0f, 1f, mitad));
            cgDestino.interactable = true;
            cgDestino.blocksRaycasts = true;
        }

        FijarPanelActual(destino);
        enTransicion = false;
    }

    private IEnumerator Fundido(CanvasGroup cg, float desde, float hasta, float duracion)
    {
        if (duracion <= 0f)
        {
            cg.alpha = hasta;
            yield break;
        }

        float t = 0f;
        while (t < duracion)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(desde, hasta, t / duracion);
            yield return null;
        }
        cg.alpha = hasta;
    }

    private void MostrarInmediato(GameObject panel)
    {
        if (panel == null) return;

        OcultarTodosExcepto(panel);

        CanvasGroup cg = Grupo(panel);
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
        panel.SetActive(true);
        FijarPanelActual(panel);
    }

    private void FijarPanelActual(GameObject panel)
    {
        panelActual = panel;
        PanelActual = panel;
        // Si panelAR no está asignado, se conserva el comportamiento anterior (cámara siempre libre).
        PermitirControlCamara = panelAR == null || panel == panelAR;
        if (PanelCambiado != null) PanelCambiado(panel);
    }

    private CanvasGroup Grupo(GameObject panel)
    {
        if (panel == null) return null;
        CanvasGroup cg;
        if (grupos.TryGetValue(panel, out cg)) return cg;
        return null;
    }

    private void OcultarTodos()
    {
        OcultarTodosExcepto(null);
    }

    private void OcultarTodosExcepto(GameObject excepcion)
    {
        if (todosLosPaneles == null) return;
        for (int i = 0; i < todosLosPaneles.Length; i++)
        {
            GameObject p = todosLosPaneles[i];
            if (p != null && p != excepcion) p.SetActive(false);
        }
    }

    // Utilidad para pruebas: clic derecho sobre el componente en el Inspector.
    [ContextMenu("Borrar preferencias guardadas (Splash, Día/Noche y Calidad)")]
    private void BorrarPreferencias()
    {
        PlayerPrefs.DeleteKey(KEY_SPLASH_VISTO);
        PlayerPrefs.DeleteKey(InteractiveController.KEY_MODO_NOCHE);
        PlayerPrefs.DeleteKey(CalidadGrafica.KEY_CALIDAD);
        PlayerPrefs.Save();
        Debug.Log("[UIManager] Preferencias de EcoCultivo borradas.");
    }
}
