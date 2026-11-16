# Ofertas Relámpago Studio — corrección del frontend real

Basado en el ZIP `ofertas-frontend-actual.zip` entregado por el usuario.

## Cambios
- Reemplazados los **dos** `window.confirm` de `App.vue`: publicación por lote y descarte de borrador.
- Modal propio, animado, responsive, con confirmación y cancelación; Escape o clic fuera cancelan.
- La confirmación de publicar vuelve a validar ofertas para evitar reenvíos.
- Conservados login de API Key, borradores IndexedDB, Haiku, edición y publicación por lote.
- `VITE_API_BASE_URL` para la **URL pública** del backend Azure, sin secretos en la compilación.
- Los errores de autenticación siguen apareciendo debajo del campo y los resultados siguen usando modal existente.

## Aplicación
Desde la raíz del repositorio:

```bash
unzip -o ~/Downloads/ofertas-studio-frontend-fix-v3.zip
cd frontend
npm ci
npm run build
npm run dev
```

Prueba «Limpiar formulario» con una captura cargada y «Publicar» con una oferta válida: ambas acciones deben mostrar modal de la app, **nunca** diálogo nativo de Chrome.

## Azure: preparación (no desplegado ni configurado aún)

1. Desplegar el backend .NET en Azure App Service con HTTPS.
2. Configurar **Managed Identity** para el App Service y concederle el rol `Key Vault Secrets User` sobre un Azure Key Vault con RBAC.
3. Crear secretos en Key Vault para `Security:ApiKey`, token de Telegram y las demás credenciales que realmente use el backend. No meterlos en el frontend ni en Git.
4. En App Service → Environment variables → App settings, usar referencias Key Vault, por ejemplo:
   - `Security__ApiKey` = `@Microsoft.KeyVault(SecretUri=https://TU-VAULT.vault.azure.net/secrets/Security--ApiKey/)`
   - Usar **nombres reales de configuración** para Telegram/Copilot, tras revisar el backend.
   - El nombre de secreto `Security--ApiKey` es solo una convención propuesta; debe coincidir con el secreto que crees.
5. Permitir en CORS del backend exclusivamente `https://studio.stadiomaxclub.com` para el frontend (y localhost para desarrollo). `X-API-Key` activa preflight OPTIONS; no usar `*` como origen.
6. Crear `frontend/.env.production` con `VITE_API_BASE_URL=https://TU-API.azurewebsites.net` (solo URL pública) y ejecutar `npm run build`.
7. Subir **contenido** de `frontend/dist` al document root del subdominio en Namecheap, una vez activo su certificado SSL.
8. Confirmar que `GET /api/auth/validate` existe en backend: este ZIP **solo contiene frontend** y no agrega endpoints ni configuración Key Vault al servidor.

**Seguridad:** el modo «Recordar» actual guarda la API Key en `localStorage`. Es cómodo, pero JavaScript del mismo origen puede leerla; no equivale a SecureStorage de MAUI ni a cookie HttpOnly. Para producción robusta conviene evolucionar a sesión HttpOnly/BFF, agregar rate limiting, rotación de claves y protección del acceso al panel. Nunca poner `Security:ApiKey` ni tokens en `VITE_*`.

**Verificación:** se revisaron los usos nativos en `frontend/src` y se comprobó el ZIP. No se pudo ejecutar `npm run build` en este entorno porque `vue-tsc` no estaba en caché npm (`npm ci --offline` falla); ejecútalo en tu Mac antes de desplegar.
