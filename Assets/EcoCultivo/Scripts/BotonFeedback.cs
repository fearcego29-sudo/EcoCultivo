using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Retroalimentación al tocar un botón (sin paquetes de terceros):
/// - Escala: baja a 0.95 al presionar y regresa a 1.0 al soltar, en 0.1 s.
/// - Háptica opcional: vibración corta (~25 ms) al hacer clic, solo en Android (#if UNITY_ANDROID).
///
/// Uso: agregar este componente al mismo GameObject que tiene el Button.
/// No modifica el OnClick del botón; solo reacciona a los eventos del puntero.
///
/// Rendimiento: sin allocations. Update solo trabaja durante los 0.1 s de animación.
///
/// (Tarea 4) Si existe un EstiloUI activo, la escala y la duración salen de ahí, para que
/// todos los botones (incluidos los del menú desplegable) se comporten igual.
/// El estado deshabilitado no se anima: se distingue por color/opacidad (ColorBlock).
/// </summary>
[DisallowMultipleComponent]
public class BotonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Escala")]
    [Range(0.8f, 1f)] public float escalaPresionado = 0.95f;
    public float duracion = 0.1f;

    [Header("Háptica (solo Android)")]
    [Tooltip("Activar solo en los botones principales.")]
    public bool vibrar = false;
    [Tooltip("Duración de la vibración en milisegundos (Android 8+ / API 26+).")]
    [Range(10, 100)] public int duracionVibracionMs = 25;

    private Transform tr;
    private Selectable selectable;
    private Vector3 escalaOriginal;
    private float factorActual = 1f;
    private float factorObjetivo = 1f;
    private bool presionado;

    private void Awake()
    {
        tr = transform;
        selectable = GetComponent<Selectable>();
        escalaOriginal = tr.localScale;
    }

    private void OnDisable()
    {
        // Al ocultarse el panel, el botón vuelve a su tamaño normal.
        presionado = false;
        factorActual = 1f;
        factorObjetivo = 1f;
        if (tr != null) tr.localScale = escalaOriginal;
    }

    private void Update()
    {
        if (factorActual == factorObjetivo) return; // nada que animar

        float paso = (1f - EscalaPresion) / Mathf.Max(0.0001f, DuracionPresion) * Time.unscaledDeltaTime;
        factorActual = Mathf.MoveTowards(factorActual, factorObjetivo, paso);
        tr.localScale = escalaOriginal * factorActual;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!Interactuable()) return;
        presionado = true;
        factorObjetivo = EscalaPresion;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        presionado = false;
        factorObjetivo = 1f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (presionado) factorObjetivo = 1f;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!vibrar || !Interactuable()) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        VibrarCorto(duracionVibracionMs);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // ---------------------------------------------------------------------
    // Vibración corta con la API nativa de Android (sin paquetes de terceros).
    // Handheld.Vibrate() dura ~400-500 ms en muchos teléfonos; aquí se usa
    // VibrationEffect.createOneShot(ms) para un "toque" de ~25 ms.
    // Si algo falla, se recurre a Handheld.Vibrate() (que además hace que Unity
    // agregue el permiso VIBRATE al AndroidManifest automáticamente).
    // El servicio Vibrator se busca UNA sola vez y se reutiliza.
    // ---------------------------------------------------------------------
    private static AndroidJavaObject vibrador;
    private static bool vibradorBuscado;
    private static bool soportaEfecto;

    private static void VibrarCorto(int milisegundos)
    {
        try
        {
            if (!vibradorBuscado)
            {
                vibradorBuscado = true;
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject actividad = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    vibrador = actividad.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    soportaEfecto = version.GetStatic<int>("SDK_INT") >= 26;
                }
            }

            if (vibrador == null)
            {
                Handheld.Vibrate();
                return;
            }

            long ms = milisegundos;
            if (soportaEfecto)
            {
                using (AndroidJavaClass efectoCls = new AndroidJavaClass("android.os.VibrationEffect"))
                using (AndroidJavaObject efecto = efectoCls.CallStatic<AndroidJavaObject>("createOneShot", ms, -1)) // -1 = amplitud por defecto
                {
                    vibrador.Call("vibrate", efecto);
                }
            }
            else
            {
                vibrador.Call("vibrate", ms);
            }
        }
        catch (System.Exception)
        {
            Handheld.Vibrate();
        }
    }
#endif

    private float EscalaPresion
    {
        get { return EstiloUI.Activo != null ? EstiloUI.Activo.escalaPresionado : escalaPresionado; }
    }

    private float DuracionPresion
    {
        get { return EstiloUI.Activo != null ? EstiloUI.Activo.duracionPresion : duracion; }
    }

    private bool Interactuable()
    {
        return selectable == null || selectable.IsInteractable();
    }
}
