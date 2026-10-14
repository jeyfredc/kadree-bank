---
name: verify
description: Compila y corre las pruebas de la parte de Kadree Bank que cambió (backend, frontend o ambos) y reporta el resultado. Úsala antes de cada commit o cuando pidan verificar que todo funciona.
---

# Verificar antes de un commit

1. Mira qué cambió con `git status --short` para saber qué partes verificar.

2. **Si cambió algo en `backend/` o `db/`:**

   ```bash
   cd backend
   dotnet build
   dotnet test KadreeBank.UnitTests
   ```

   Si cambió SQL, un DAO o el flujo de dinero y Docker está disponible, corre también `dotnet test`
   (incluye integración con PostgreSQL real).

3. **Si cambió algo en `frontend/`:**

   ```bash
   cd frontend
   npx prettier --check <archivos del frontend que cambiaron>
   npx ng test --watch=false
   npx ng build
   ```

   Si `node_modules` no existe, corre `npm ci` primero.

4. **Reporta** en pocas líneas: qué se corrió, qué pasó y qué falló. Si algo falla, muestra el error
   relevante (no el log completo) y propone la corrección, pero **no cambies lo que espera una
   prueba para que pase** sin explicar por qué.

Si solo cambiaron documentos (`*.md`, `docs/`), no hace falta correr nada.
