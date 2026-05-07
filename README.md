# El Consejo de las IAs

Aplicación de escritorio para **Windows** que convoca a varios modelos de lenguaje (en la nube o locales vía **Ollama**) como «hermanos» en experiencias de juego con estética de scriptorio medieval: **consejo deliberativo**, **modo impostor** e **enigma de los prisioneros**.

---

## Requisitos

- **Windows** (WPF, escritorio).
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) para compilar.

Opcional:

- API keys de proveedores en la nube (OpenAI, Anthropic, Gemini, Grok, Mistral).
- **[Ollama](https://ollama.com)** en ejecución para modelos **locales**.

---

## Instalación y ejecución

En la raíz del repositorio (`TheIACouncil.sln`):

```bash
dotnet build TheIACouncil.sln -c Release
dotnet run --project TheIACouncil\TheIACouncil.csproj -c Release
```

Ejecutable típico: `TheIACouncil\bin\Release\net8.0-windows\TheIACouncil.exe`

**Si no recompila:** cierra `TheIACouncil.exe` si está abierto (Windows bloquea la copia del `.exe`).

### Datos guardados

| Archivo | Ubicación |
|--------|-----------|
| `config.json` | `%AppData%\TheIACouncil\` |
| `achievements.json` | `%AppData%\TheIACouncil\` |

Ahí van claves (solo en tu máquina), modelos, personalidades y logros.

---

## Modos de juego

Desde el menú principal entras a **Modos de juego** y eliges:

### 1. El consejo (clásico)

1. **Configuración:** activa proveedores, claves, modelos; en Ollama, actualiza el catálogo y marca los modelos que participan. Asigna **personalidades** por hermano.
2. **Guardar** y en **Jugar** escribe la **pregunta** y pulsa **Convocar al consejo**.
3. **Deliberación por turnos:** cada IA ve la pregunta, la personalidad y lo que ya dijeron las anteriores; responde con un párrafo acotado.
4. **Votación:** todas reciben la pregunta y el resumen de intervenciones; deben votar **SÍ** o **NO** (el acta interpreta matices y votos ambiguos).

Las llamadas al modelo son **sin memoria de chat en el servidor**: cada petición lleva el contexto necesario en el texto del prompt (no hay sesión persistente en Ollama entre turnos).

### 2. El impostor

- Mínimo **3 IAs** activas.
- Un hermano es el **culpable** (puede mentir); el resto tiene hechos coherentes (coartadas, testigos, etc.).
- Fase inicial: **declaraciones en paralelo** (con límite de concurrencia; ver más abajo).
- Luego **interrogas** por turnos y puedes **acusar** a quien creas.
- Respuestas orientadas a **un párrafo breve, hasta 100 palabras** (recorte por palabras, sin puntos suspensivos artificiales en el acta).
- Si un modelo **repite el prompt** en lugar de interpretar el rol, se sustituye por un mensaje narrativo corto para no romper la partida.
- **Colores por monje:** cada hermano tiene fondo y acento propios en la ficha y en las entradas del registro (declaraciones, interrogatorios, réplicas).
- Hasta que no termina el **primer lote** de declaraciones, no se puede **Interrogar** ni **Acusar**.

### 3. El enigma de los prisioneros

- Dos IAs actúan como guardianes; el enigma clásico de lógica (respuestas acotadas).
- Pensado para pocas llamadas en paralelo; no exige el mismo ajuste de GPU que ocho modelos a la vez.

---

## Paralelismo y Ollama / GPU

Con **muchas IAs locales** en la misma GPU, disparar todas las inferencias a la vez puede provocar errores **CUDA** u **OOM**.

En **Configuración** hay un bloque **«Concilio — paralelismo (Ollama / GPU)»**:

- **Máximo de llamadas al modelo a la vez** (`MaxConcurrentLlmRequests`, por defecto **3**, rango 1–32 en la UI).
- Afecta a la **fase de votación del consejo** y a las **declaraciones iniciales del impostor** (cola con semáforo, no todas las peticiones simultáneas).

El valor se guarda en `config.json` (propiedad en camelCase por el serializador JSON).

---

## Cómo se juega (consejo) — pantalla

- Tarjetas de hermanos con retratos (`Assets/Monks/`) y personalidad · proveedor.
- Ventanas de **razonamiento** y **veredicto** con texto legible y ancho acotado al panel.
- Colores de voto en el roster cuando corresponde al ritmo de la animación.
- **Logros** desde el botón del trofeo en el menú.

---

## Estructura del proyecto

### Tecnología

- **.NET 8**, **WPF**, **C#**.
- Tema visual en `Themes/Manuscript.xaml` (pergamino, latón, Georgia).

### Carpetas relevantes

| Ruta | Rol |
|------|-----|
| `App.xaml.cs` | Servicios: `Config`, `LlmFactory`, `CouncilGame`, `Ollama`, `Achievements`. |
| `MainWindow.xaml(.cs)` | Contenedor; navega entre vistas. |
| `Views/` | `MainMenuView`, `GameModesView`, `PlayView`, `ImpostorView`, `PrisonerRiddleView`, `ConfigView`, `AchievementsView`, `AboutView`. |
| `Services/` | `CouncilGameService`, clientes LLM, `VoteParser`, `ConfigService`, `LLMClientFactory`, logros, etc. |
| `Helpers/` | `LlmConcurrency` (límite de paralelismo), `ReadableLogText` (logs legibles), `MonkMotes`, etc. |
| `Models/` | `AppSettings` (incl. `MaxConcurrentLlmRequests`), proveedores, turnos, votos, personalidades. |
| `Assets/Monks/` | PNG de monjes para UI. |

### Flujo técnico del consejo

1. `PlayView` obtiene clientes con `LLMClientFactory.CreateEnabledClients` y pasa `MaxConcurrentLlmRequests` a `CouncilGameService.RunAsync`.
2. Opiniones **secuenciales** por hermano; votos con **`LlmConcurrency.RunParallelLimitedAsync`**.
3. `VoteParser` normaliza SÍ/NO ante respuestas largas o ambiguas.

---

## Personalidades

Definidas en `BrotherPersonalityCatalog` (monje por defecto). Afectan sobre todo al **tono del debate**, no al formato mecánico del voto.

---

## Solución de problemas

- **No hay hermanos activos:** revisa proveedores, claves y modelos Ollama marcados.
- **CUDA / Ollama inestable con muchas IAs:** baja **Máximo de llamadas al modelo a la vez** en Configuración.
- **Timeouts / HTTP:** red, claves y límites de la API.

---

## Licencia y créditos

Revisa si existe `LICENSE` en el repositorio. Los modelos son responsabilidad de sus proveedores y de tu uso de las APIs. Ilustraciones y narrativa forman parte de la experiencia «El Consejo de las IAs».
