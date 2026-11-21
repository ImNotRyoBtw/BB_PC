# BB_PC — proyecto Unity del port 6.0.6

Editor: Unity2022.3.62f3. Plataforma objetivo: Windows x64, scripting backend Mono. Objetivo completo y dependencias: [plan maestro](../../docs/implementation-plan.md). Esta base es parte permanente del port, no el producto final ni una demo sustitutiva.

## Estado vigente P2 — parcial

Se conservó Core P1 y se añadieron91 recursos originales, datos de138 objetos, Splash/MainRoom reconstruibles y el prefab de fila SongCellView como datos. Código y escena en el proyecto; recorrido hasta partida todavía bloqueado. [Informe/evidencia P2](../../docs/p2-report.md).

Compilación Core/Runtime/Editor PASS;4539 aserciones P1 +11 de selección/preparación y928 comprobaciones de artefactos. Esto **no acredita todavía importación, render ni Play Mode**: falta recibo del Editor gráfico. No se presupone bloqueo de licencia.

1. Unity2022.3.62f3: Assets → Refresh; esperar compilación. Menú **BB_PC → P2 → Build and validate recovered scene** si no se ejecuta la validación automática.
2. Abrir **Assets/BBPC/Scenes/RecoveredFrontend.unity** y Play.
3. Inspeccionar Splash/MainRoom mediante controles temporales. Catálogo comunica la dependencia remota; no hay canciones inventadas ni partida simulada.

Resultados del Editor: analysis/p2/unity-results.json y splash.png/mainroom.png. Se conserva Untitled. Adaptaciones temporales de Canvas/cámara/fuente builtin y scripts no reconstruidos constan en el informe.

Verificación actual: **rtk proxy python -B tools/p2/verify_project.py** y **rtk proxy python -B tools/p2/audit_delivery.py**. Los originales y recursos comerciales recuperados permanecen locales; no se incluyen DummyDll en Assets. Red: cero respuestas HTTP; socket local WinError10013 y alternativa web inaccesible. Servicios, catálogo real, caché, canción/chart, pipeline de partida, modos y progreso siguen pendientes.

Lo siguiente es el registro histórico de P1; las limitaciones de presentación y el batch allí descritos no sustituyen este estado vigente.

## Estado comprobado P1 (histórico)

Core, pruebas, adaptador Unity y herramientas Editor compilan sin warnings usando csc/Mono y las referencias del editor instalado. El código de producción pasa 11 comprobaciones integradas y 1493 casos diferenciales (4539 aserciones). [Recibos](../../analysis/implementation-p1/).

**Importación Unity no comprobada:** la única ejecución batch salió1 antes de importar por ausencia de licencia utilizable en ese entorno. No se concluye que la instalación del usuario carezca de licencia en otra sesión. [Log saneado](../../analysis/implementation-p1/unity-editor.log). No se alteraron permisos, licencias ni configuración global. No se construyó un player.

La escena Bootstrap y sus .meta están escritos y referenciados en EditorBuildSettings. Contienen PortBootstrap; no hay canción, menú ni sesión fingida. La escena todavía requiere validación de importación/Play Mode en un Editor autorizado.

## Reproducción

Desde la raíz BB_PC, con Python y RTK ya instalados:

~~~text
rtk proxy python -B tools/verify_port.py
~~~

Genera fixtures numéricas sintéticas desde los modelos M1 conservados, compila y ejecuta C# real, y compila Runtime/Editor contra las APIs locales. No descarga nada ni reescribe resultados M0/M1-A/Etapas1/2A. No ejecuta binarios del juego.

Con licencia válida accesible al proceso batch:

~~~text
rtk proxy python -B tools/verify_port.py --unity
~~~

El entrypoint BBPC.EditorTools.ProjectValidation.Run selecciona StandaloneWindows64/Mono/.NETStandard2.1, corre las mismas pruebas, comprueba el adaptador del reloj y conserva/añade Bootstrap a las escenas de build. No compila un player ni inicia otro hito. Puede fallar por el bloqueo ya registrado. El script utiliza un entorno local de tarea y un log dentro del workspace; no modifica ACL ni intenta activar Unity.

## Componentes de producción

| Archivo | Implementación | Límite explícito |
|---|---|---|
| Core/RecoveredAudioClock.cs | PlayingTimestamp, GetTime y PlayingTime; conversiones ARM64, wrap, truncado y rebase | Recibe estado explícito. No inventa Prepare/Play/Pause/Resume, reproducción ni sincronizador de red |
| Core/NoteJudgement.cs | Juicio tap, cabeza hold, búsqueda de checkpoint, cierre/calidad y deadline miss | Reglas funcionales; falta controlador completo, generación/sort de checkpoints, input y callbacks |
| Core/GameplayTypes.cs | Enums separados, HitResult, Detection y ResultCounts | Snapshots del port; no son objetos IL2CPP ni recuperan su identidad/mutabilidad completa |
| Core/NormalScoreLedger.cs | Registro normal, IDs, contadores, CPU, score/combo/sangre/gracia | No Challenge/online. Devuelve notificaciones, no ejecuta handlers desconocidos |
| Runtime/UnityAudioClock.cs | AudioSettings.dspTime y Time.time exactos del motor | No usa timeAsDouble/unscaledTime ni reemplaza la suma por DSP |
| Runtime/PortBootstrap.cs | Inicializa servicios del núcleo al cargar Bootstrap | Sin selección/carga automática de contenido ni modo jugable |

La fuente de cada regla y su alcance están en [evidence-map.json](../../analysis/implementation-p1/evidence-map.json) y [M1-A](../../docs/m1/report.md). En P1 no se copiaron DummyDll ni assets comerciales a Assets; P2 incorpora recursos originales locales descritos arriba.

## Reglas de integración

El destino final del proyecto completo y fuente canónica de los cambios integrados es `C:/Users/elpri/Downloads/BB_PC/unity/BB_PC`, incluida su carpeta `Builds` para la entrega final. Las carpetas `analysis` y `validation` conservan evidencia y candidatos desechables; no son ramas de desarrollo. Toda corrección se realiza primero en este proyecto y después se sincroniza al candidato de forma controlada, conservando manifest, hashes y logs de validación. La validación de un snapshot integrado no demuestra que todas las novedades experimentales estén consolidadas: queda pendiente inventariar sus diferencias únicas y migrar lo necesario con procedencia, sin sobrescribir funcionalidades. Un build o una suite aprobada no acredita fidelidad al original sin auditoría de su evidencia y alcance.

- El controlador conserva identidad de touch y aplica los estados/eventos observados. Un retorno DetectionState.Miss no muta por sí solo el modelo. No equivale a CellState.Miss.
- HoldHead es solo la rama de cabeza; None significa seguir evaluando. FindCheckpoint supone que el caller ya comprobó cabeza, tecla e identidad de touch. QualityAfterCheckpoint recibe la validez de Count−3, todavía no implementada desde IsValidResult.
- NormalRecord lleva snapshots coherentes de un modelo estable y contadores reales; nunca calcular conteos a partir de porcentajes. NormalScoreLedger es para una sesión nueva normal: persistir solo NormalScoreState sin los conjuntos de deduplicación no restaura una sesión.
- RecordOutcome distingue aceptación y notificaciones Score/Blood/ResultChanged. Sus flags no prometen handlers ejecutados, UI actualizada ni orden externo completado. Modelos/handlers siguen pendientes. Los IDs usan Int32.ToString sin proveedor como el cuerpo nativo; la cultura de bootstrap del original aún no está recuperada.
- RealTime exige proveedor explícito y falla si falta. No simula servidor, sesión rival ni autenticación. La unidad/epoch del proveedor todavía requiere evidencia.
- El guardado, catálogo real, loaders, descifrado, presentación, personajes, modos y VS están registrados como pendientes en el plan maestro. No se crean clases vacías para declararlos terminados.

## Validación y límites

Los oráculos reutilizan los modelos Python derivados de M1-A, no el original ejecutado. Se comparan decisiones, cambios de estado, orden de muestras, tipos, enums, presencia de referencias y deduplicación. Esto prueba el código nuevo dentro de esa especificación; no acredita fidelidad total, sincronización audible, teclado, UI ni canción real.

Se detectó y corrigió que Mono puede conservar precisión mayor en expresiones float. Float32Arithmetic fuerza el almacenamiento binario32 usando un struct explícito sin allocations, antes de comparar límites, igual que las instrucciones ARM64. El caso de borde queda en los fixtures. No se cambió el esperado para ocultarlo.

Las carpetas Library/Temp/Logs/UserSettings/Builds y binarios de pruebas locales se excluyen de Git. Los .meta son parte del proyecto y sus GUID se conservan. No incluir muestras originales, capturas privadas ni recursos comerciales en este proyecto o una distribución.
