using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menú hamburguesa (botón ≡ de Panel_AR_UI): panel desplegable animado que se cierra
/// al tocar fuera, al pulsar Atrás o al cambiar de pantalla.
///
/// Opciones: Menú principal · Objetivo del proyecto · Créditos · Calidad gráfica (Alta/Baja).
///
/// Uso:
/// 1. Agregar este componente a UIManager_Object.
/// 2. En Btn_MenuAR > On Click(), cambiar UIManager.AbrirMenu por MenuDesplegable.Alternar
///    (ver PASOS_UNITY.md). Si no se cambia, el ≡ sigue yendo directo al menú como antes.
///
/// Los objetos del desplegable se CREAN EN RUNTIME (una sola vez, en Start) como hijos de
/// Panel_AR_UI. La escena guardada no cambia y no hay que crear nada a mano.
///
/// Rendimiento: 1 imagen de fondo + 1 bloqueador transparente + 4 botones. Inactivos mientras
/// el menú está cerrado (no se dibujan). La animación dura 0.18 s y no genera basura.
/// </summary>
[DefaultExecutionOrder(20)]
public class MenuDesplegable : MonoBehaviour
{
    public UIManager ui;
    public CalidadGrafica calidad;

    [Header("Posición (unidades del Canvas, desde la esquina superior derecha)")]
    public Vector2 posicion = new Vector2(-80f, -190f);
    public float ancho = 470f;
    public float altoOpcion = 84f;
    public float separacion = 8f;
    public float relleno = 16f;

    private GameObject bloqueador;
    private GameObject panel;
    private CanvasGroup grupo;
    private RectTransform rtPanel;
    private TMP_Text txtCalidad;
    private bool abierto;
    private Coroutine animacion;

    private void Awake()
    {
        if (ui == null) ui = GetComponent<UIManager>();
        if (calidad == null) calidad = GetComponent<CalidadGrafica>();
    }

    private void Start()
    {
        if (ui == null || ui.panelAR == null)
        {
            Debug.LogWarning("[MenuDesplegable] Falta UIManager o panelAR; el menú desplegable no se creará.", this);
            return;
        }

        Construir();
        ui.TransicionIniciada += AlCambiarPantalla;
        ui.InterceptorAtras = AtenderAtras;
        if (calidad != null) calidad.CalidadCambiada += ActualizarEtiquetaCalidad;
    }

    private void OnDestroy()
    {
        if (ui != null)
        {
            ui.TransicionIniciada -= AlCambiarPantalla;
            if (ui.InterceptorAtras == (System.Func<bool>)AtenderAtras) ui.InterceptorAtras = null;
        }
        if (calidad != null) calidad.CalidadCambiada -= ActualizarEtiquetaCalidad;
    }

    // =====================================================================
    // API pública (para el OnClick de Btn_MenuAR)
    // =====================================================================

    public void Alternar()
    {
        if (abierto) Cerrar(); else Abrir();
    }

    public void Abrir()
    {
        if (panel == null || abierto) return;
        if (UIManager.PanelActual != ui.panelAR) return; // solo desde la vista 3D

        abierto = true;
        bloqueador.SetActive(true);
        panel.SetActive(true);
        bloqueador.transform.SetAsLastSibling();
        panel.transform.SetAsLastSibling();
        Animar(true);
    }

    public void Cerrar()
    {
        if (panel == null || !abierto) return;
        abierto = false;
        Animar(false);
    }

    public void CerrarInmediato()
    {
        if (panel == null) return;
        abierto = false;
        if (animacion != null) { StopCoroutine(animacion); animacion = null; }
        grupo.alpha = 0f;
        panel.SetActive(false);
        bloqueador.SetActive(false);
    }

    // =====================================================================
    // Construcción (una sola vez)
    // =====================================================================

    private void Construir()
    {
        EstiloUI e = EstiloUI.Activo;
        TextosApp t = TextosApp.Activo;
        Transform padre = ui.panelAR.transform;

        Color blanco = e != null ? e.blancoCalido : new Color(0.98f, 0.965f, 0.933f);
        Color carbon = e != null ? e.grisCarbon : new Color(0.18f, 0.196f, 0.212f);
        Color sombra = e != null ? e.sombra : new Color(0f, 0f, 0f, 0.28f);
        Vector2 offSombra = e != null ? e.desplazamientoSombra : new Vector2(0f, -6f);
        float radio = e != null ? e.radioTarjeta : 28f;

        // Fuente: la misma que ya usa el botón ≡ (consistencia visual).
        TMP_Text referencia = padre.GetComponentInChildren<TMP_Text>(true);

        // --- Bloqueador transparente a pantalla completa: tocar fuera = cerrar.
        //     (SimpleTouchMove lo trata como UI, así que tocarlo no mueve la cámara.)
        bloqueador = new GameObject("Menu_Bloqueador", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rtB = (RectTransform)bloqueador.transform;
        rtB.SetParent(padre, false);
        rtB.anchorMin = Vector2.zero;
        rtB.anchorMax = Vector2.one;
        rtB.offsetMin = Vector2.zero;
        rtB.offsetMax = Vector2.zero;
        Image imgB = bloqueador.GetComponent<Image>();
        imgB.color = new Color(0f, 0f, 0f, 0f);
        Button btnB = bloqueador.GetComponent<Button>();
        btnB.transition = Selectable.Transition.None;
        btnB.onClick.AddListener(Cerrar);

        // --- Panel desplegable
        int opciones = calidad != null ? 4 : 3;
        float alto = relleno * 2f + opciones * altoOpcion + (opciones - 1) * separacion;

        panel = new GameObject("Menu_Desplegable", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        rtPanel = (RectTransform)panel.transform;
        rtPanel.SetParent(padre, false);
        rtPanel.anchorMin = new Vector2(1f, 1f);
        rtPanel.anchorMax = new Vector2(1f, 1f);
        rtPanel.pivot = new Vector2(1f, 1f); // crece desde la esquina del botón ≡
        rtPanel.anchoredPosition = posicion;
        rtPanel.sizeDelta = new Vector2(ancho, alto);

        Image fondo = panel.GetComponent<Image>();
        Redondear(fondo, radio);
        fondo.color = blanco;
        Shadow s = panel.AddComponent<Shadow>();
        s.effectColor = sombra;
        s.effectDistance = offSombra;

        grupo = panel.GetComponent<CanvasGroup>();
        grupo.alpha = 0f; // la primera apertura también debe hacer el fundido

        // --- Opciones
        float y = -relleno;
        CrearOpcion(t != null ? t.menuPrincipal : "Menú principal", y, IrAMenu, referencia, carbon, blanco); y -= altoOpcion + separacion;
        CrearOpcion(t != null ? t.menuObjetivo : "Objetivo del proyecto", y, IrAObjetivo, referencia, carbon, blanco); y -= altoOpcion + separacion;
        CrearOpcion(t != null ? t.menuCreditos : "Créditos", y, IrACreditos, referencia, carbon, blanco); y -= altoOpcion + separacion;
        if (calidad != null)
        {
            txtCalidad = CrearOpcion("", y, AlternarCalidad, referencia, carbon, blanco);
            ActualizarEtiquetaCalidad(calidad.Nivel);
        }

        panel.SetActive(false);
        bloqueador.SetActive(false);
    }

    private TMP_Text CrearOpcion(string texto, float y, UnityEngine.Events.UnityAction accion,
                                 TMP_Text referencia, Color colorTexto, Color colorFondo)
    {
        EstiloUI e = EstiloUI.Activo;

        GameObject go = new GameObject("Opcion", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(rtPanel, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(-relleno * 2f, altoOpcion);

        Image img = go.GetComponent<Image>();
        Redondear(img, e != null ? e.radioBoton : 35f);
        img.color = colorFondo;

        Button btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = btn.colors;
        float p = e != null ? e.factorColorPresionado : 0.82f;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.94f, 0.94f, 0.94f, 1f);
        cb.pressedColor = new Color(p, p, p, 1f);
        cb.selectedColor = Color.white;
        cb.fadeDuration = e != null ? e.duracionColorBoton : 0.08f;
        btn.colors = cb;
        btn.onClick.AddListener(accion);

        go.AddComponent<BotonFeedback>(); // misma escala al presionar que el resto de botones

        GameObject goTxt = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rtTxt = (RectTransform)goTxt.transform;
        rtTxt.SetParent(rt, false);
        rtTxt.anchorMin = Vector2.zero;
        rtTxt.anchorMax = Vector2.one;
        rtTxt.offsetMin = new Vector2(28f, 0f);
        rtTxt.offsetMax = new Vector2(-20f, 0f);

        TextMeshProUGUI tmp = goTxt.GetComponent<TextMeshProUGUI>();
        if (referencia != null) tmp.font = referencia.font;
        tmp.text = texto;
        tmp.fontSize = e != null ? e.tamanoBoton : 32f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = colorTexto;
        tmp.horizontalAlignment = HorizontalAlignmentOptions.Left;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false; // el toque lo recibe la imagen del botón
        return tmp;
    }

    private static void Redondear(Image img, float radio)
    {
        img.sprite = AplicadorEstilo.ObtenerSpriteRedondeado();
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 24f / Mathf.Max(1f, radio);
    }

    // =====================================================================
    // Acciones
    // =====================================================================

    private void IrAMenu()
    {
        CerrarInmediato();
        ui.AbrirMenu();
    }

    private void IrAObjetivo()
    {
        CerrarInmediato();
        ui.AbrirObjetivo(); // Atrás desde Objetivo regresa a la vista 3D
    }

    private void IrACreditos()
    {
        CerrarInmediato();
        ui.AbrirCreditos();
    }

    private void AlternarCalidad()
    {
        if (calidad != null) calidad.Alternar(); // la etiqueta se actualiza por evento; el menú sigue abierto
    }

    private void ActualizarEtiquetaCalidad(NivelCalidad nivel)
    {
        if (txtCalidad == null) return;
        TextosApp t = TextosApp.Activo;
        txtCalidad.text = nivel == NivelCalidad.Alta
            ? (t != null ? t.calidadAlta : "Calidad gráfica: Alta")
            : (t != null ? t.calidadBaja : "Calidad gráfica: Baja");
    }

    private void AlCambiarPantalla(GameObject destino)
    {
        CerrarInmediato();
    }

    private bool AtenderAtras()
    {
        if (!abierto) return false;
        Cerrar();
        return true;
    }

    // =====================================================================
    // Animación: fundido + escala desde la esquina del ≡
    // =====================================================================

    private void Animar(bool abrir)
    {
        if (animacion != null) StopCoroutine(animacion);
        animacion = StartCoroutine(RutinaAnimacion(abrir));
    }

    private IEnumerator RutinaAnimacion(bool abrir)
    {
        float dur = EstiloUI.Activo != null ? EstiloUI.Activo.duracionMenuDesplegable : 0.18f;
        float desde = grupo.alpha;
        float hasta = abrir ? 1f : 0f;
        grupo.interactable = abrir;
        grupo.blocksRaycasts = abrir;

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, dur));
            float suave = 1f - (1f - k) * (1f - k); // ease-out
            float a = Mathf.Lerp(desde, hasta, suave);
            grupo.alpha = a;
            rtPanel.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, a);
            yield return null;
        }

        grupo.alpha = hasta;
        rtPanel.localScale = Vector3.one;
        if (!abrir)
        {
            panel.SetActive(false);
            bloqueador.SetActive(false);
        }
        animacion = null;
    }
}
