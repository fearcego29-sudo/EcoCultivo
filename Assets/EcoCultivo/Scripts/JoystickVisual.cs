using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dibuja los controles táctiles de la vista 3D (solo en móvil / Device Simulator):
/// - Izquierda: JOYSTICK FLOTANTE (base + perilla). En reposo se ve tenue en la esquina
///   inferior izquierda como pista; al tocar, aparece donde se apoya el dedo y la perilla
///   sigue al dedo dentro del círculo.
/// - Derecha: indicador de "girar vista" (tenue en reposo; sigue al dedo al arrastrar).
///
/// La lógica de movimiento está en SimpleTouchMove; este script SOLO dibuja su estado.
/// Los objetos se crean en runtime como hijos de Panel_AR_UI (la escena guardada no cambia),
/// con Raycast Target desactivado para no bloquear los toques.
///
/// Uso: agregar a UIManager_Object. Las referencias se encuentran solas.
/// Rendimiento: 4 imágenes de UI; Update solo reposiciona 2-4 RectTransform (sin GC).
/// </summary>
[DefaultExecutionOrder(30)]
public class JoystickVisual : MonoBehaviour
{
    public UIManager ui;
    public SimpleTouchMove control;

    [Tooltip("Mostrar también en PC (normalmente solo en móvil / Device Simulator).")]
    public bool mostrarEnPC = false;

    [Header("Posición de reposo (unidades del Canvas, desde las esquinas inferiores)")]
    public Vector2 reposoJoystick = new Vector2(230f, 250f);
    public Vector2 reposoVista = new Vector2(-230f, 250f);

    [Header("Opacidad")]
    [Range(0f, 1f)] public float alfaReposo = 0.3f;
    [Range(0f, 1f)] public float alfaActivo = 0.85f;

    private RectTransform rtPanel, rtBase, rtPerilla, rtVista;
    private Image imgBase, imgPerilla, imgVista, imgVistaCentro;
    private Canvas canvas;
    private bool construido;

    private void Awake()
    {
        if (ui == null) ui = GetComponent<UIManager>();
        if (control == null && Camera.main != null) control = Camera.main.GetComponent<SimpleTouchMove>();
    }

    private void Start()
    {
        bool movil = Application.isMobilePlatform || Input.touchSupported;
        if (!movil && !mostrarEnPC) return;
        if (ui == null || ui.panelAR == null || control == null)
        {
            Debug.LogWarning("[JoystickVisual] Falta UIManager, panelAR o SimpleTouchMove en la cámara.", this);
            return;
        }
        Construir();
    }

    private void Construir()
    {
        rtPanel = (RectTransform)ui.panelAR.transform;
        canvas = rtPanel.GetComponentInParent<Canvas>();

        EstiloUI e = EstiloUI.Activo;
        Color claro = e != null ? e.blancoCalido : new Color(0.98f, 0.965f, 0.933f);
        Color acento = e != null ? e.verdeHoja : new Color(0.25f, 0.56f, 0.29f);

        float radio = RadioCanvas();

        // Joystick: base (círculo claro) + perilla (círculo verde)
        imgBase = Circulo("Joystick_Base", rtPanel, radio * 2f, claro, new Vector2(0f, 0f));
        rtBase = imgBase.rectTransform;
        imgPerilla = Circulo("Joystick_Perilla", rtBase, radio * 0.9f, acento, new Vector2(0.5f, 0.5f));
        rtPerilla = imgPerilla.rectTransform;

        // Indicador de vista: aro (círculo claro con centro más pequeño)
        imgVista = Circulo("Vista_Indicador", rtPanel, radio * 1.3f, claro, new Vector2(1f, 0f));
        rtVista = imgVista.rectTransform;
        imgVistaCentro = Circulo("Vista_Centro", rtVista, radio * 0.45f, acento, new Vector2(0.5f, 0.5f));

        construido = true;
        Reposo();
    }

    private Image Circulo(string nombre, RectTransform padre, float diametro, Color color, Vector2 ancla)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        rt.anchorMin = ancla;
        rt.anchorMax = ancla;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(diametro, diametro);
        rt.SetAsFirstSibling(); // detrás de los botones existentes

        Image img = go.GetComponent<Image>();
        img.sprite = AplicadorEstilo.ObtenerSpriteRedondeado();
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 24f / Mathf.Max(1f, diametro * 0.5f); // radio = mitad → círculo
        img.color = color;
        img.raycastTarget = false; // no bloquea los toques
        return img;
    }

    private void LateUpdate()
    {
        if (!construido || !rtPanel.gameObject.activeInHierarchy) return;

        // ---------------- Joystick
        if (control.JoystickActivo)
        {
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rtPanel, control.JoystickOrigen, null, out local))
            {
                // La base usa ancla (0,0): convertir del centro del panel a la esquina inferior izquierda.
                rtBase.anchoredPosition = local - rtPanel.rect.min;
            }
            float radio = RadioCanvas();
            rtPerilla.anchoredPosition = control.JoystickVector * radio;
            Alfa(imgBase, alfaActivo * 0.6f);
            Alfa(imgPerilla, alfaActivo);
        }
        else
        {
            rtBase.anchoredPosition = reposoJoystick;
            rtPerilla.anchoredPosition = Vector2.zero;
            Alfa(imgBase, alfaReposo * 0.6f);
            Alfa(imgPerilla, alfaReposo);
        }

        // ---------------- Indicador de vista
        if (control.VistaActiva)
        {
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rtPanel, control.VistaPosicion, null, out local))
            {
                // Ancla (1,0): relativo a la esquina inferior derecha.
                rtVista.anchoredPosition = local - new Vector2(rtPanel.rect.xMax, rtPanel.rect.yMin);
            }
            Alfa(imgVista, alfaActivo * 0.5f);
            Alfa(imgVistaCentro, alfaActivo);
        }
        else
        {
            rtVista.anchoredPosition = reposoVista;
            Alfa(imgVista, alfaReposo * 0.5f);
            Alfa(imgVistaCentro, alfaReposo);
        }
    }

    private void Reposo()
    {
        rtBase.anchoredPosition = reposoJoystick;
        rtVista.anchoredPosition = reposoVista;
    }

    /// <summary>Radio del joystick en unidades del Canvas (el de SimpleTouchMove está en píxeles).</summary>
    private float RadioCanvas()
    {
        float escala = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        return control.RadioJoystickPx / escala;
    }

    private static void Alfa(Image img, float a)
    {
        Color c = img.color;
        if (!Mathf.Approximately(c.a, a))
        {
            c.a = a;
            img.color = c;
        }
    }
}
