---
paths:
  - "frontend/src/**/*.ts"
  - "frontend/src/**/*.html"
---

# Reglas del frontend (Angular 22)

## Estructura

- El proyecto usa **NgModule** a propósito: los componentes llevan `standalone: false` y se
  declaran en el módulo de su funcionalidad. No los conviertas a standalone salvo que se pida.
- Componentes y pipes reutilizables van en `shared/` y se declaran y exportan en `SharedModule`.
  Cada módulo de funcionalidad importa solo `SharedModule`.
- Servicios en `core/services/` con `@Injectable({ providedIn: 'root' })`.
- Rutas nuevas en el `*-routing.module.ts` de su funcionalidad, con `title: '... · Kadree Bank'`.
  Las rutas protegidas usan las guardas funcionales de `core/guards/auth.guards.ts` con `canMatch`.
- Prefijo de selectores: `kb-`.

## Código

- Dependencias con `inject()`, no por constructor.
- Estado de la pantalla con **signals** (`signal`, `computed`). Miembros usados en la plantilla
  con `protected readonly`.
- Peticiones HTTP con RxJS. Desenvuelve la respuesta con `map(unwrap)` y cierra las suscripciones
  con `takeUntilDestroyed()`.
- Formularios reactivos **tipados** (`FormControl<T>` con `nonNullable: true`).
- Errores de la API: muéstralos con `toErrorMessage()` de `core/utils/api-error.ts`; los errores
  por campo vienen en `validationErrors`.
- Montos: usa `parseCopAmount()` para leerlos y el pipe `cop` para mostrarlos.
- Fechas: llegan en UTC; muéstralas con el pipe `bankDate`.
- No uses `any`. Los modelos de `core/models/` deben coincidir exactamente con los DTOs de C#.
- Formato con Prettier (`.prettierrc`): ancho 100 y comillas simples.

## Plantillas y accesibilidad

- Usa el control de flujo nuevo (`@if`, `@for` con `track`).
- Cada campo con su `label for`; los errores asociados con `aria-describedby` y `aria-invalid`.
- Textos para el usuario en español y tuteando, como en el resto de la app.
