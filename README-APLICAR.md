# Parche: acceso de una sola vez para Ofertas Relámpago Studio

Este ZIP es **incremental sobre el parche de lotes V1**. No reemplaza otros archivos ni elimina la funcionalidad de 5 ofertas.

## Cambios
- Pantalla de acceso independiente, oscura, responsive, con logo de Ofertas Relámpago.
- Verificación real de la API Key con `GET /api/auth/validate` (no consume créditos ni publica mensajes).
- Opción **Recordar en este dispositivo** activada por defecto: persiste en localStorage. Al reabrir la PWA, valida la clave antes de mostrar el Studio.
- Si la clave es inválida, se borra la copia guardada y aparece el error. Botón **Cambiar clave / Salir**.
- Los borradores de ofertas siguen guardándose por separado, sin incorporar la clave.

## Aplicar
Desde la raíz del repositorio:

```bash
unzip -o ~/Downloads/ofertas-auth-once-patch.zip
dotnet test backend/AfiliadosMercadoLibreAmazon.sln
cd frontend
npm install
npm run build
npm run dev
```

## Seguridad (importante)
Esto es **recordar una API Key**, no una sesión con cookie HttpOnly ni un login de usuarios. `localStorage` es legible por JavaScript del origen; usar solo en dispositivo personal de confianza. Una vulnerabilidad XSS o una extensión maliciosa podría robar la clave. No insertar la clave en `VITE_*`, en Git ni en el bundle. HTTPS obligatorio en producción. Para mayor seguridad futura: BFF/sesión HttpOnly y controles de acceso.

## Despliegue
El frontend usa `/api` y el proxy de Vite solo existe en desarrollo. En Namecheap hay que configurar proxy seguro al backend o adaptar la URL del API y CORS antes de publicar; este parche **no** despliega Azure ni habilita por sí solo el frontend en producción. No publicar sobre HTTP.
