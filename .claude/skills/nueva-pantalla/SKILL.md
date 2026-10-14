---
name: nueva-pantalla
description: Crea una pantalla o componente nuevo en el frontend Angular de Kadree Bank siguiendo la estructura de módulos, el design system y las reglas de accesibilidad. Úsala cuando pidan una vista, página o componente nuevo.
---

# Crear una pantalla o componente en Angular

Antes de empezar, define con el usuario: a qué funcionalidad pertenece (`auth`, `banking`,
`reports` o una nueva), la ruta, quién puede verla y qué datos de la API usa.

## Pasos

1. **Datos:** confirma que el endpoint existe y que su interfaz está en `core/models/`. Si no,
   usa primero la skill `nuevo-endpoint`.
2. **Componente** en `features/<funcionalidad>/<nombre>/`:
   - `standalone: false`, selector con prefijo `kb-`, `templateUrl`.
   - Dependencias con `inject()`; estado con `signal` y `computed` (`protected readonly`).
   - Carga de datos con el servicio de `core/services/` y `takeUntilDestroyed()`.
   - Estados de carga, error (`toErrorMessage`) y vacío.
3. **Declaración:** agrégalo en `declarations` del módulo de la funcionalidad. Si es reutilizable,
   va en `shared/components/` y se declara y exporta en `SharedModule`.
4. **Ruta** en el `*-routing.module.ts` de la funcionalidad, con `title: '<Nombre> · Kadree Bank'`
   y las guardas que correspondan (`clienteGuard`, `bancoGuard`) con `canMatch`.
5. **Plantilla:**
   - Reutiliza clases del design system (`kb-page`, `kb-btn--primary`, `kb-field`, `kb-table`...).
   - Colores con variables y espacios con tokens; nada de valores fijos.
   - `@if` y `@for` con `track`.
   - Accesibilidad: `label for` en cada campo, `aria-describedby` en los errores, `caption` en tablas.
   - Si usa un ícono nuevo, agrégalo a `icon_names` en `src/index.html`.
6. **Pruebas:** si la pantalla tiene lógica propia (cálculos, validaciones), muévela a una función en
   `core/utils/` y pruébala con un `*.spec.ts`.

## Verificación

```bash
cd frontend
npx prettier --write "src/app/features/<funcionalidad>/**"
npx ng test --watch=false
npx ng build
```

Revisa la pantalla con `npx ng serve` en los temas `marino` y `claro`, y en ancho de celular.
