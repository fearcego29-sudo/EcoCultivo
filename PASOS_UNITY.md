# PASOS_UNITY — EcoCultivo

## ✔ Ya hecho

- **Rondas 1–5:** cámara, UI y estilo, calidad Alta/Baja, menú ≡, iluminación nocturna, escenario urbano.
- **Ronda 6:** riego NFT por etapas. El jardín se borró.

---

# Ronda 7 — Joystick virtual, organización y GitHub

## 1. Joystick virtual (móvil)

**Antes:** la mitad izquierda movía la cámara solo mientras el dedo se arrastraba, sin nada visible en pantalla.
**Ahora:**
- **Izquierda:** **joystick flotante**. Aparece donde apoyas el dedo, y mientras lo mantienes inclinado la cámara avanza de forma continua, más rápido cuanto más lejos del centro.
- **Derecha:** arrastrar para girar la vista, con un indicador que sigue al dedo.
- En reposo se ven ambos controles tenues en las esquinas inferiores, como pista.

1. Selecciona `UIManager_Object` > **Add Component** > `JoystickVisual`.
   - No requiere referencias.
   - Solo se muestra en móvil o en el Device Simulator. Para verlo en PC, marca **Mostrar En PC**.
2. **Ctrl+S**.
3. Prueba: **Window > General > Device Simulator** > elige un teléfono Android > **Play**.
   - [ ] Al tocar a la izquierda aparece el joystick y la cámara avanza mientras lo inclinas.
   - [ ] A la derecha, arrastrar gira la vista.
   - [ ] Los botones siguen funcionando, y tocarlos no mueve la cámara.

**Ajustes** en `Main Camera` > **Simple Touch Move**:
- **Radio Joystick** = 0.12 (fracción del alto de la pantalla)
- **Zona Muerta** = 0.12
- **Move Speed** = 4

**Costo:** 4 imágenes de UI (+1–2 batches de UI), sin GC.

## 2. Organizar carpetas (para la tarea de GitHub)

4. **Guarda la escena** (Ctrl+S).
5. Menú **EcoCultivo > Organizar carpetas del proyecto**.
   - Aparece la lista de movimientos > **Organizar**.
   - Todo lo del proyecto queda en `Assets/EcoCultivo/` (Escenas, Scripts, Editor, Modelos, Materiales, Texturas, Shaders, UI, Datos, EntornoUrbano).
   - Unity conserva las referencias (GUID): materiales, escenas, scripts y Build Settings siguen funcionando.
   - No borra ningún archivo.
6. Espera a que compile. En la Consola, **0 errores**.
7. Abre `Assets/EcoCultivo/Escenas/Escena3` y dale **Play** para confirmar que todo sigue igual.
8. **File > Build Settings**: `EcoCultivo/Escenas/Escena3` debe seguir marcada.

**Limpieza opcional (requiere tu confirmación; no la hice):**
- `Assets/TutorialInfo` y `Assets/Readme.asset` son restos de la plantilla de Unity, no del proyecto. Si quieres quitarlos: clic derecho > **Delete**.

## 3. Subir a GitHub

> Ya dejé en la raíz del proyecto **`.gitignore`**, que evita subir `Library`, `Logs`, `build`, los APK, etc. (pesan GB y Unity los regenera), **`.gitattributes`** y **`README.md`**, la portada del repositorio.
> Ningún archivo pasa de 100 MB, así que **no hace falta Git LFS**. El repositorio pesará aproximadamente 15–25 MB.

### 3.1 Crear la cuenta e instalar Git (solo la primera vez)

9. Crea tu cuenta en **https://github.com/signup**.
10. Instala Git desde **https://git-scm.com/download/win** (opciones por defecto). Incluye *Git Credential Manager*, que abre el navegador para iniciar sesión al subir.
11. Abre **PowerShell** y configura tu nombre y correo (los mismos de GitHub):
    ```powershell
    git config --global user.name "Felipe Cervera"
    git config --global user.email "TU_CORREO_DE_GITHUB"
    ```

### 3.2 Crear el repositorio en GitHub

12. En GitHub: **+** (arriba a la derecha) > **New repository**.
    - **Repository name:** `EcoCultivo`
    - **Public** ✔ (lo pide la tarea)
    - **No** marques "Add a README", ".gitignore" ni "license": ya están en el proyecto.
    - **Create repository**.

### 3.3 Subir el proyecto

13. **Cierra Unity**, para que no esté escribiendo archivos mientras se sube.
14. En PowerShell (cambia `TU_USUARIO` por tu usuario de GitHub):
    ```powershell
    cd "C:\Users\Felipe\OneDrive\Documentos\Marista\Uni semestre 5\EcoCultivo_FelipeCervera"
    git init
    git branch -M main
    git add .
    git status
    git commit -m "EcoCultivo: invernadero hidropónico interactivo (proyecto organizado)"
    git remote add origin https://github.com/TU_USUARIO/EcoCultivo.git
    git push -u origin main
    ```
    - `git status` debe listar `Assets/`, `Packages/`, `ProjectSettings/`, `Docs/`, `README.md`, `.gitignore`… y **no** `Library/` ni `build/`.
    - En el `push` se abre el navegador para iniciar sesión en GitHub: acepta.
15. Abre `https://github.com/TU_USUARIO/EcoCultivo`: debe verse el README con la captura.
16. **Esa URL es la que entregas en la tarea.**

> **OneDrive:** el proyecto está dentro de OneDrive. Funciona, pero si `git` marca "permission denied" o "file in use", pausa la sincronización de OneDrive mientras subes.

### 3.4 Para subir cambios después

```powershell
git add .
git commit -m "Describe el cambio"
git push
```

---

<details>
<summary>Referencia: Ronda 6 (riego NFT)</summary>

## Ronda 6 — Riego NFT por etapas + limpieza

## 1. Borrar el jardín (aprobado)

`Jardin_Exterior` ya no está en la escena (lo quitó la ronda 5). Solo quedan sus archivos:

1. En **Project**, clic derecho en la carpeta **`Assets/Jardin`** > **Delete** > **Delete**.
   - Contiene la paleta, `Mat_Jardin` y la malla. Ningún otro objeto ni script los usa.
2. Clic derecho en **`Assets/Editor/CrearJardinExterior.cs`** > **Delete** > **Delete**.
   - Es el generador del jardín. Al borrarlo desaparece el menú *EcoCultivo > Crear/Quitar jardín exterior*.
3. Espera a que compile y revisa la Consola: **0 errores**.

> Unity manda lo borrado a la Papelera de Windows, así que se puede recuperar si hiciera falta.

## 2. Riego NFT canal por canal

**No requiere clics:** `InteractiveController` agrega el componente **Riego NFT** a `Grupo_Agua_NFT_ALL` al darle Play. La escena guardada no cambia.

**Cómo funciona** (como un sistema NFT real, por gravedad):
- Al pulsar **Agua**, en cada macetero el agua entra por el **canal superior**, avanza de un extremo al otro y **baja al siguiente nivel en zigzag** hasta el canal inferior.
- Mientras avanza, la **lámina de agua sube de nivel** dentro de cada canal: el macetero se va llenando poco a poco.
- El botón se pone azul y muestra **"Llenando 0–100 %"**; al terminar dice **"Agua: Encendida"**.
- Al apagar, los canales se **vacían en orden inverso** con **"Vaciando …%"**, y termina en "Agua: Apagada".
- Si se pulsa a la mitad, la animación da la vuelta desde donde iba, sin saltos.
- Con la app en segundo plano o sin foco, la animación se **pausa** y continúa al volver.

**Ajustes opcionales:** se agrega el componente a mano para que se guarden los valores.
4. Selecciona `Grupo_Agua_NFT_ALL` > **Add Component** > `RiegoNFT`.
5. Valores por defecto:
   - **Duracion** = 4 s (tiempo total de llenado)
   - **Ventana Por Canal** = 0.36 (más alto = los niveles se llenan más a la vez)
   - **Nivel Inicial** = 0.2
6. **Ctrl+S**.

**Costo:**

| | Impacto |
|---|---|
| CPU | ≈0.02 ms **solo durante los ~4 s de animación** (36 transformaciones por frame). En reposo, ≈0. |
| GC | 0 B por frame (el porcentaje usa `TMP_Text.SetText` con número) |
| Triángulos / Batches / SetPass | **sin cambio** (son los mismos 36 objetos `Agua_Tuberia`; solo cambian su escala y posición) |
| Memoria | <1 KB |

**Textos:** `Assets/Settings/TextosApp` > *Llenando {0:0} %* / *Vaciando {0:0} %*. `{0:0}` es el porcentaje que pone el código.

## 3. Probar (prueba final)

7. **Play** > Visualizador 3D:
   - [ ] **Agua:** cada macetero se llena del canal superior al inferior, en zigzag, y el nivel sube. El botón muestra el porcentaje.
   - [ ] **Agua otra vez:** se vacía en orden inverso.
   - [ ] **Agua dos veces seguidas rápido:** la animación se invierte sin saltos.
   - [ ] Día/Noche, Reset Cámara, menú ≡, calidad Alta/Baja y la ciudad siguen funcionando.
8. **Build final** (APK):
   - **File > Build Settings**: Android, **Development Build desmarcado**, escena **Escena3** marcada.
   - **Build**.
   - Desinstala versiones anteriores del teléfono antes de instalar.

---

<details>
<summary>Referencia: Ronda 5 (escenario urbano)</summary>

## Ronda 5 — Escenario urbano: azotea en la ciudad

El proyecto trata sobre agricultura urbana, así que el campo se sustituye por **la azotea de un edificio en una ciudad**.

## 1. Crear el entorno urbano

1. Respaldo: **Ctrl+D** sobre `Assets/Scenes/Escena3` en Project.
2. Menú superior **EcoCultivo > Crear entorno urbano (azotea)**.
   - Aparece un aviso: *"Se quitará Jardin_Exterior de la escena…"* > **Continuar**.
   - En la Consola: `[EcoCultivo] Entorno urbano creado. Triángulos: …`.
   - En la Hierarchy aparece **`Entorno_Urbano`** con 5 hijos: `Ciudad`, `Ventanas_Noche` (inactivo), `Nubes`, `Aves` y `Turbina_Rotor`.
   - En Project aparece la carpeta **`Assets/Urbano`**: paleta, 2 materiales y 5 mallas.
3. **Ctrl+S**.

> Todo queda conectado automáticamente: el componente **Ambiente Urbano** de `Entorno_Urbano` ya tiene sus 4 referencias. No hay que arrastrar nada.

**Qué incluye:**
- **Azotea (unos 6 pisos sobre la calle):**
  - Impermeabilizante terracota, pretil de 1 m y andadores desde las dos puertas.
  - **Paneles solares** (2 filas), **2 tinacos**, cubo de escalera con puerta, 3 equipos de A/C y antena.
  - **Jardineras de huerto urbano** junto al pretil y una **mini turbina eólica**.
- **Ciudad:**
  - Calles y banquetas en cuadrícula, y edificios de 3 a 30 pisos con fachadas variadas (algunas torres más altas que el tuyo).
  - Franjas de ventanas en los cercanos, **un parque** y un **skyline** lejano en el horizonte.
- **Movimiento:**
  - Nubes que giran muy lento.
  - Parvada de 6 aves volando en círculo y aleteando; de noche se ocultan.
  - El rotor de la turbina gira.
- **Noche:** ventanas iluminadas en unos 40 % de los pisos. Es una malla con material Unlit, **no son luces**, y de día está desactivada.

**Ajustes** (componente **Ambiente Urbano** en `Entorno_Urbano`):
- **Velocidad Nubes:** 0.8 °/s
- **Velocidad Aves:** 14 °/s
- **Frecuencia Aleteo:** 7
- **Velocidad Rotor:** 120 °/s

**Costo estimado:**

| | Día | Noche |
|---|---|---|
| Batches / draw calls | **+4** (ciudad, nubes, aves, rotor) | **+4** (aves ocultas, ventanas activas) |
| SetPass | **+0–1** (copia de `Mat_Macetero`, misma variante URP Lit) | **+1–2** (las ventanas usan URP Unlit) |
| Triángulos | **≈ +12k** (≈ +1.2 %) | ≈ +13k |
| CPU | ≈0.01 ms (3 transformaciones por frame, 0 GC) | igual |
| Memoria | ≈ 1.5 MB (mallas) + 128 bytes (paleta) | igual |
| Luces / sombras | **ninguna nueva / no proyecta sombras** | igual |

**Si quieres volver atrás:**
- **EcoCultivo > Quitar entorno urbano**, o **Ctrl+Z** justo después.

## 2. Probar

4. **Play**:
   - [ ] **Día:** desde la vista 3D, hacia fuera se ven el pretil, los paneles, los tinacos y la ciudad alrededor. Las nubes se mueven, las aves vuelan y la turbina gira.
   - [ ] **Noche:** el cielo se oscurece, aparecen ventanas encendidas en los edificios y las aves desaparecen.
   - [ ] La cámara, la UI, el menú ≡, las transiciones y las calidades Alta y Baja funcionan igual que antes.

---


</details>

---

</details>

---

## Pendiente de aprobación

**Decisiones cerradas:**
- **Bloom:** apagado.
- **Jardín:** borrado.
- **Riego:** por etapas.
- **LOD / Static Batching:** sin cambios en todo el proyecto.

**Solo si en el teléfono de gama media la vista 3D baja de ~45 FPS** (medido con el Profiler). Ordenadas de mayor a menor impacto; ninguna está aplicada porque tocan cosas que pediste no cambiar:

| # | Propuesta | Ganancia esperada | Qué toca |
|---|---|---|---|
| R1 | Reducir triángulos de las plantas/maceteros (decimar en Blender al 40–50 % o agregar LOD) | La mayor: hoy hay 712k–1M triángulos, alto para gama media | Modelos FBX / LOD |
| R2 | MSAA 4x → 2x en `Mobile_RPAsset` | ≈ 10–20 % de GPU | RP Asset |
| R3 | Render Scale 1.0 → 0.85 | ≈ 20–25 % menos píxeles | RP Asset |
| R4 | Distancia de sombras de la luz direccional 50 → 25 m | Menor costo del mapa de sombras | RP Asset |

---

## Verificación final (Profiler, en el teléfono)

| Métrica | Esperado (Baja, vista 3D, día) | Notas |
|---|---|---|
| FPS / frame time | **57–104 FPS · 9.56–17.61 ms** | El riego agrega ≈0.02 ms solo mientras se anima |
| FPS en menús | 30 FPS (≈33 ms) | intencional |
| Triángulos | **≈ 724k–1.03M** | +≈12k de la ciudad |
| Vértices | **≈ 1.24M–1.66M** | |
| SetPass Calls | **25–56** (noche: hasta ~57) | |
| Batches | **124–244** | +4 del entorno |
| Memoria | +≈1.5 MB (mallas del entorno) | Calidad Alta: +10–40 MB en Render Textures |
| GC Alloc | **0 B por frame** | Revisar `RiegoNFT.Update` y `EstadoControlesAR` durante el llenado |
