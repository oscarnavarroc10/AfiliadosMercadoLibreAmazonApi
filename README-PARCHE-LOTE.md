# Ofertas Relámpago — lote de hasta 5 ofertas

Este parche se construyó sobre el ZIP `ofertas-proyecto-actual.zip` más los parches MSI y botones de Telegram de esta conversación. Sobrescribe `frontend/src/App.vue` y `frontend/src/style.css`, además de agregar el controlador de lote y el almacenamiento de borradores.

## Nuevos endpoints
- `POST /api/batch/analyze`: multipart `images` (1–5 archivos PNG/JPG). Respuesta `{results:[{index,status,offer?,error?}]}`. Cada imagen se analiza independientemente.
- `POST /api/batch/publish`: multipart `images` (1–5) + `offersJson` (JSON array de ofertas en el mismo orden). Respuesta `{results:[{index,status,error?}]}`. Cada elemento se valida y publica independientemente.
- Ambos exigen `X-API-Key` y límites de tamaño.

## Estados
- `analyzed`: análisis correcto; se muestran datos para revisar.
- `failed`: validación o análisis falló; el elemento se conserva editable.
- `published`: Telegram confirmó la publicación; no se vuelve a enviar desde la interfaz.
- `uncertain`: fallo de red/Telegram de confirmación desconocida. Revisar canal antes de reintentar. No se reenvía automáticamente.

## Consideraciones
- El procesamiento del lote es secuencial, para limitar llamadas a Copilot y Telegram.
- Se conserva la imagen JPG/PNG original, sin recorte.
- El borrador usa localStorage + IndexedDB y no guarda la API Key.
- La firma gratuita iOS/PWA no cambia el requisito de acceso a la API.
- No hay idempotencia persistente del lado del servidor; ante un resultado incierto verificar Telegram para evitar duplicados.
- Los archivos WebP se rechazan porque el publicador actual de Telegram solo acepta JPG/PNG.
- Si hay alguna oferta inválida, las demás válidas se envían; al finalizar se mantienen los errores para corregir.
- El botón Reintentar vuelve a enviar solo ofertas que no figuran como publicadas ni inciertas.

## Verificación local
`dotnet test backend/AfiliadosMercadoLibreAmazon.sln`
`cd frontend && npm ci && npm run build`

**No se pudieron ejecutar estas compilaciones en el entorno de generación**: .NET no está instalado y la caché de npm no contiene `vue-tsc`. Probar primero en local, preferiblemente con un canal de Telegram de pruebas.
