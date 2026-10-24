# Recorrido en navegador real

`capture.js` recorre la aplicación con Microsoft Edge (vía Playwright) como lo haría un usuario y:

- Verifica el formulario de ventas en el navegador: agregar y quitar filas, índices consecutivos y total calculado.
- Comprueba la confirmación antes de anular una venta.
- Revisa que la barra de navegación quepa en una línea.
- Falla si hay errores de JavaScript, violaciones de la Content-Security-Policy o respuestas HTTP con error.
- Guarda las capturas de `docs/screenshots` usadas en el README.

## Uso

1. Levanta la aplicación en modo Production con datos de demostración sobre una base nueva:

   ```bash
   ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://localhost:5330 \
   SeedDemoUser=true SeedDemoData=true HttpsRedirection__Enabled=false \
   ConnectionStrings__SupermarketDB="Data Source=/tmp/demo.db" \
   dotnet run --project src/SupermarketWEB
   ```

2. En otra terminal:

   ```bash
   cd tools/e2e
   npm install
   npm run capture
   ```

Usa el Edge instalado en el sistema (`channel: 'msedge'`), así que no descarga navegadores. La URL se puede cambiar con `BASE_URL`.
