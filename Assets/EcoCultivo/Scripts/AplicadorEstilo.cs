using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pinta toda la UI con el estilo central (EstiloUI) al iniciar la app.
/// Solo cambia la APARIENCIA: colores, esquinas redondeadas, sombras, tipografía y
/// estados de botón. No crea, mueve ni renombra objetos de la jerarquía.
///
/// Uso: agregar a Main_UI_Canvas y arrastrar el asset EstiloUI al campo "Estilo".
/// Si "Estilo" está vacío, usa los valores por defecto de EstiloUI.
///
/// - Esquinas redondeadas SIN sprites externos: se genera UNA textura de 64×64 (16 KB)
///   en memoria y se reutiliza en todas las tarjetas y botones (comparten textura, así que
///   uGUI puede agruparlos en el mismo batch).
/// - Sombras suaves: componente Shadow de uGUI (duplica los vértices de esa imagen; no agrega draw calls).
/// - Los cambios se aplican en runtime; en modo edición la escena se ve como antes.
/// - Todo se hace una vez en Awake (nada en Update).
/// </summary>
[DefaultExecutionOrder(-100)] // antes que AplicadorTextos, UIManager y demás
public class AplicadorEstilo : MonoBehaviour
{
    [Tooltip("Asset de estilo (Create > EcoCultivo > Estilo de UI).")]
    public EstiloUI estilo;

    [Tooltip("Raíz de la UI. Vacío = este objeto (Main_UI_Canvas).")]
    public Transform raizCanvas;

    [Tooltip("Margen interior (X, Y) del texto de Objetivo dentro de su tarjeta.")]
    public Vector2 margenTarjeta = new Vector2(44f, 36f);

    // Textura/sprite compartidos por toda la app (se crean una sola vez).
    private static Sprite spriteRedondeado;
    private const int TAM_TEXTURA = 64;
    private const int RADIO_PX = 24; // borde 9-slice en píxeles de la textura

    private enum TipoBoton { Primario, Secundario, Control, Circular }

    private void Awake()
    {
        if (estilo == null) estilo = ScriptableObject.CreateInstance<EstiloUI>();
        estilo.Activar();
        Aplicar();
    }

    [ContextMenu("Aplicar estilo ahora (solo en Play)")]
    public void Aplicar()
    {
        Transform r = raizCanvas != null ? raizCanvas : transform;
        EstiloUI e = estilo;

        // ---------------- Fondos de panel (pantalla completa, sin redondear)
        ColorDeFondo(r, "Panel_Splash", e.blancoCalido);
        ColorDeFondo(r, "Panel_Menu", e.verdeHojaOscuro);
        ColorDeFondo(r, "Panel_Objetivo", e.verdeHojaOscuro);
        ColorDeFondo(r, "Panel_Creditos", e.verdeHojaOscuro);
        // Panel_AR_UI se deja transparente (deja ver la escena 3D).

        // ---------------- Tarjetas
        Tarjeta(r, "Panel_Objetivo/Tarjeta_Contenido", e.blancoCalido);
        Tarjeta(r, "Panel_Creditos/Tarjeta_Creditos", e.blancoCalido);
        Tarjeta(r, "Panel_AR_UI/Tarjeta_ControlesAR", e.fondoControles);

        // ---------------- Botones
        Boton(r, "Panel_Menu/Btn_AR", TipoBoton.Primario);
        Boton(r, "Panel_Menu/Btn_Objetivo", TipoBoton.Secundario);
        Boton(r, "Panel_Menu/Btn_Creditos", TipoBoton.Secundario);
        Boton(r, "Panel_Objetivo/Btn_Volver_Objetivo", TipoBoton.Secundario);
        Boton(r, "Panel_Creditos/Btn_Volver_Creditos", TipoBoton.Secundario);
        Boton(r, "Panel_AR_UI/Btn_MenuAR", TipoBoton.Circular);
        Boton(r, "Panel_AR_UI/Tarjeta_ControlesAR/Btn_Luces", TipoBoton.Control);
        Boton(r, "Panel_AR_UI/Tarjeta_ControlesAR/Btn_FlujoAgua", TipoBoton.Control);
        Boton(r, "Panel_AR_UI/Tarjeta_ControlesAR/Btn_CamaraReset", TipoBoton.Control);

        // ---------------- Tipografía: jerarquía Portada > Título > Subtítulo > Cuerpo
        Titulo(r, "Panel_Splash/Text_TituloSplash", e.tamanoPortada, e.verdeHojaOscuro);
        Titulo(r, "Panel_Menu/Text_TituloMenu", e.tamanoTitulo, e.blancoCalido);
        Titulo(r, "Panel_Objetivo/Text_TituloObjetivo", e.tamanoTitulo, e.blancoCalido);
        Titulo(r, "Panel_Creditos/Text_TituloCreditos", e.tamanoTitulo, e.blancoCalido);

        // El texto de Objetivo ocupa toda su tarjeta (con margen interior): más espacio = letra más grande.
        Cuerpo(r, "Panel_Objetivo/Tarjeta_Contenido/Text_DescripcionObjetivo", true);
        // En Créditos el texto comparte la tarjeta con el logo: se respeta su recuadro actual.
        Cuerpo(r, "Panel_Creditos/Tarjeta_Creditos/Text_DatosAlumno", false);
    }

    // =====================================================================
    // Piezas de estilo
    // =====================================================================

    private void ColorDeFondo(Transform raiz, string ruta, Color color)
    {
        Image img = Componente<Image>(raiz, ruta);
        if (img != null) img.color = color;
    }

    private void Tarjeta(Transform raiz, string ruta, Color color)
    {
        Image img = Componente<Image>(raiz, ruta);
        if (img == null) return;

        Redondear(img, estilo.radioTarjeta);
        img.color = color;
        PonerSombra(img.gameObject);
    }

    private void Boton(Transform raiz, string ruta, TipoBoton tipo)
    {
        Transform t = raiz.Find(ruta);
        if (t == null) { Aviso(ruta); return; }

        EstiloUI e = estilo;
        Image img = t.GetComponent<Image>();
        Button btn = t.GetComponent<Button>();
        TMP_Text etiqueta = t.GetComponentInChildren<TMP_Text>(true);

        Color relleno, texto;
        switch (tipo)
        {
            case TipoBoton.Primario: relleno = e.verdeHoja; texto = e.blancoCalido; break;
            default: relleno = e.blancoCalido; texto = e.grisCarbon; break;
        }

        if (img != null)
        {
            Redondear(img, tipo == TipoBoton.Circular ? e.radioBotonCircular : e.radioBoton);
            img.color = relleno;
            PonerSombra(img.gameObject);
        }

        // Estados por color: normal / presionado / deshabilitado.
        // ColorTint MULTIPLICA el color de la imagen, así que se usan tonos neutros.
        // "Selected" = normal para que en móvil el botón no quede "marcado" tras tocarlo.
        if (btn != null)
        {
            btn.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = btn.colors;
            float p = e.factorColorPresionado;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            cb.pressedColor = new Color(p, p, p, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.8f, 0.8f, 0.8f, e.alfaDeshabilitado);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = e.duracionColorBoton;
            btn.colors = cb;
        }

        if (etiqueta != null)
        {
            etiqueta.color = texto;
            if (tipo != TipoBoton.Circular) // el "≡" conserva su tamaño propio
            {
                etiqueta.fontSize = e.tamanoBoton;
                etiqueta.fontStyle = FontStyles.Bold;
            }
        }
    }

    private void Titulo(Transform raiz, string ruta, float tamano, Color color)
    {
        TMP_Text tmp = Componente<TMP_Text>(raiz, ruta);
        if (tmp == null) return;

        tmp.enableAutoSizing = false;
        tmp.fontSize = tamano;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
        // Una sola línea: un título no debe partirse en pantallas angostas.
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void Cuerpo(Transform raiz, string ruta, bool llenarTarjeta)
    {
        TMP_Text tmp = Componente<TMP_Text>(raiz, ruta);
        if (tmp == null) return;

        if (llenarTarjeta)
        {
            RectTransform rt = tmp.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margenTarjeta.x, margenTarjeta.y);
            rt.offsetMax = new Vector2(-margenTarjeta.x, -margenTarjeta.y);
        }

        EstiloUI e = estilo;
        tmp.color = e.grisCarbon;
        tmp.fontStyle = FontStyles.Normal;
        tmp.horizontalAlignment = HorizontalAlignmentOptions.Left;   // párrafos: mejor lectura alineados a la izquierda
        tmp.verticalAlignment = VerticalAlignmentOptions.Top;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.paragraphSpacing = e.espaciadoParrafo;
        tmp.richText = true;

        // Auto-ajuste: si el texto no cabe (pantallas bajas como 1080×2400), TMP reduce el
        // tamaño hasta tamanoCuerpoMinimo. Solo se recalcula cuando cambia el texto, no por frame.
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = e.tamanoCuerpo;
        tmp.fontSizeMin = e.tamanoCuerpoMinimo;
        tmp.fontSize = e.tamanoCuerpo;

        // Red de seguridad: si aun al mínimo no cabe, termina en "…" en vez de salirse de la tarjeta.
        tmp.overflowMode = TextOverflowModes.Ellipsis;
    }

    // =====================================================================
    // Utilidades
    // =====================================================================

    private void Redondear(Image img, float radioEnUnidades)
    {
        img.sprite = ObtenerSpriteRedondeado();
        img.type = Image.Type.Sliced;
        img.fillCenter = true;
        // Con sprite de 100 PPU y Canvas de 100 PPU, el borde de 24 px mide 24 unidades.
        // El multiplicador lo escala al radio deseado.
        img.pixelsPerUnitMultiplier = RADIO_PX / Mathf.Max(1f, radioEnUnidades);
    }

    private void PonerSombra(GameObject go)
    {
        // Ojo: Outline hereda de Shadow, por eso se busca el tipo exacto.
        Shadow sombra = null;
        Shadow[] existentes = go.GetComponents<Shadow>();
        for (int i = 0; i < existentes.Length; i++)
        {
            if (existentes[i].GetType() == typeof(Shadow)) { sombra = existentes[i]; break; }
        }
        if (sombra == null) sombra = go.AddComponent<Shadow>();

        sombra.effectColor = estilo.sombra;
        sombra.effectDistance = estilo.desplazamientoSombra;
        sombra.useGraphicAlpha = true;
    }

    /// <summary>Sprite blanco con esquinas redondeadas y borde suavizado, generado en memoria.</summary>
    public static Sprite ObtenerSpriteRedondeado()
    {
        if (spriteRedondeado != null) return spriteRedondeado;

        const int S = TAM_TEXTURA;
        const float R = RADIO_PX;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        {
            name = "EcoCultivo_Redondeado",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32[] px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float cx = Mathf.Clamp(fx, R, S - R);
                float cy = Mathf.Clamp(fy, R, S - R);
                float dx = fx - cx, dy = fy - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = (byte)(Mathf.Clamp01(R - d + 0.5f) * 255f); // antialias de 1 px
                px[y * S + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true); // sube a GPU y libera la copia en CPU

        spriteRedondeado = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0,
                                         SpriteMeshType.FullRect, new Vector4(R, R, R, R));
        spriteRedondeado.name = "EcoCultivo_Redondeado";
        return spriteRedondeado;
    }

    private static T Componente<T>(Transform raiz, string ruta) where T : Component
    {
        Transform t = raiz.Find(ruta);
        if (t == null) { Aviso(ruta); return null; }
        T c = t.GetComponent<T>();
        if (c == null) Debug.LogWarning("[AplicadorEstilo] " + ruta + " no tiene " + typeof(T).Name, t);
        return c;
    }

    private static void Aviso(string ruta)
    {
        Debug.LogWarning("[AplicadorEstilo] No se encontró: " + ruta);
    }
}
