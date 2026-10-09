# Kadree Bank: documentación técnica del frontend

Aplicación **Angular 22** organizada con **NgModule** (`standalone: false`) y módulos de funcionalidad con carga diferida. Usa signals, formularios reactivos tipados, detección de cambios **zoneless** y el design system de Kadree Bank. Para la instalación y ejecución, ver el [README](../README.md).

---

## 1. Estructura

```
frontend/
├── angular.json, package.json, tsconfig*.json
├── proxy.conf.json                  # ng serve: /api → http://localhost:8080
├── nginx/default.conf.template      # producción: SPA + proxy /api → API
├── Dockerfile                       # build con Node y servidor Nginx
└── src/
    ├── index.html                   # <html lang="es" data-theme="marino">, Roboto y Material Symbols
    ├── main.ts                      # platformBrowser().bootstrapModule(AppModule)
    ├── styles.scss                  # importa el design system + diseño de página
    ├── styles/
    │   ├── kadree-tokens.css        # tokens (colores, tipografía, espacios) por tema
    │   └── kadree-components.css    # clases .kb-* del design system
    └── app/
        ├── app.module.ts            # módulo raíz
        ├── app-routing.module.ts    # rutas raíz con carga diferida
        ├── app.component.ts         # <kb-header> (con sesión) + <router-outlet>
        ├── layout/header.component.ts
        ├── core/                    # singleton: modelos, servicios, interceptor, guardas, utilidades
        │   ├── models/              # interfaces = DTOs del backend
        │   ├── services/            # AuthService, AccountsService, ReportsService, FlowStateService
        │   ├── interceptors/auth.interceptor.ts
        │   ├── guards/auth.guards.ts
        │   └── utils/               # api-error, formatting, pin-rules (+ specs)
        ├── shared/                  # SharedModule: componentes y pipes reutilizables
        │   ├── components/          # kb-alert, kb-pin-field, kb-movement-list
        │   └── pipes/               # cop, bankDate
        └── features/
            ├── auth/                # AuthModule      (/acceso)
            ├── banking/             # BankingModule   (/banca)
            └── reports/             # ReportsModule   (/reportes)
```

---

## 2. Módulos

| Módulo | Declara | Importa | Notas |
|---|---|---|---|
| `AppModule` | `AppComponent`, `HeaderComponent` | `BrowserModule`, `AppRoutingModule`, `SharedModule` | Providers: `provideZonelessChangeDetection()`, `provideHttpClient(withInterceptorsFromDi())`, `HTTP_INTERCEPTORS → AuthInterceptor`, `LOCALE_ID = 'es-CO'`, `provideAppInitializer(() => inject(AuthService).restoreSession())` |
| `SharedModule` | `AlertComponent`, `PinFieldComponent`, `MovementListComponent`, `CopPipe`, `BankDatePipe` | `CommonModule` | **Exporta** también `CommonModule`, `FormsModule`, `ReactiveFormsModule` y `RouterModule`, para que cada módulo de funcionalidad importe solo `SharedModule` |
| `AuthModule` | `LoginComponent`, `RegisterComponent`, `RegisterSuccessComponent` | `SharedModule`, `AuthRoutingModule` | Carga diferida |
| `BankingModule` | `HomeComponent`, `AccountDetailComponent`, `OperationComponent`, `ReceiptComponent`, `StatementComponent` | `SharedModule`, `BankingRoutingModule` | Carga diferida |
| `ReportsModule` | `ReportsComponent` | `SharedModule`, `ReportsRoutingModule` | Carga diferida |

Los servicios de `core/` usan `@Injectable({ providedIn: 'root' })`, así que son singletons sin un `CoreModule`.

---

## 3. Rutas

| Ruta | Componente | Guardas | Descripción |
|---|---|---|---|
| `/` | — | — | Redirige a `/banca/inicio` |
| `/acceso/ingresar` | `LoginComponent` | `guestGuard` | Inicio de sesión. Query params: `volver` (adónde ir después) y `motivo=expirada` (aviso) |
| `/acceso/registro` | `RegisterComponent` | `guestGuard` | Registro en 3 pasos |
| `/acceso/registro/listo` | `RegisterSuccessComponent` | — | Cuenta creada; sin registro reciente vuelve a `/acceso/registro` |
| `/banca/inicio` | `HomeComponent` | `authGuard` (en `/banca`) | Inicio (Cliente) o todas las cuentas (Banco) |
| `/banca/cuentas/:number` | `AccountDetailComponent` | `authGuard` | Detalle y 50 movimientos |
| `/banca/consignar`, `/banca/retirar` | `OperationComponent` (`data.kind` = `deposit` / `withdrawal`) | `authGuard`, `clienteGuard` | Query param `cuenta` para preseleccionar |
| `/banca/comprobante` | `ReceiptComponent` | `authGuard`, `clienteGuard` | Sin operación reciente vuelve al inicio |
| `/banca/extractos` | `StatementComponent` | `authGuard` | Query param `cuenta`; tema claro |
| `/admin` | `ReportsComponent` | `authGuard`, `bancoGuard` | **Administración** (solo el administrador): transacciones por sede, reportes 1 y 2, descargas en Excel/PDF |
| `/reportes` | — | — | Redirige a `/admin` (ruta anterior) |
| `**` | — | — | Redirige a `/banca/inicio` |

Las guardas (`core/guards/auth.guards.ts`) son funcionales (`CanMatchFn`). Con `canMatch`, un usuario sin permiso **ni siquiera descarga** el módulo diferido. Las guardas solo mejoran la experiencia: **la seguridad real está en la API**, que responde 401, 403 o 404.

| Guarda | Regla |
|---|---|
| `authGuard` | Exige sesión; si no hay, redirige a `/acceso/ingresar?volver=<url>` |
| `guestGuard` | Con sesión, redirige a `homeRoute()` |
| `bancoGuard` | Exige el rol Banco |
| `clienteGuard` | Rechaza al rol Banco (consulta pero no opera) |

---

## 4. Contratos con el backend

Las interfaces de `core/models/` reflejan **exactamente** los DTOs de C#: mismos nombres de propiedad (el backend serializa en camelCase), mismos enums como texto y los mismos endpoints.

| Archivo TS | DTOs de C# |
|---|---|
| `api-response.model.ts` | `ApiResponse<T> { success, message, errorCode, errors, validationErrors, data }`, `ApiErrorCode` |
| `auth.models.ts` | `LoginRequestDto`, `LoginResultDto`, `AuthUserDto`, `RegisterRequestDto`, `RegisterResultDto`, `UsernameAvailabilityDto`; tipos `UserRole`, `CustomerType`, `DocumentType` |
| `account.models.ts` | `CityDto`, `AccountSummaryDto`, `MovementDto`, `OperationRequestDto`, `OperationResultDto`, `StatementDto`; tipos `AccountType`, `MovementType` y `OperationKind` (este último solo del frontend) |
| `report.models.ts` | `CustomerTransactionCountDto`, `OutOfCityWithdrawalsDto` |

Las fechas llegan como texto ISO en UTC (`createdAtUtc`, `periodStartUtc`…) y se muestran en hora de Bogotá con el pipe `bankDate`. Los montos son `number`.

**Si cambias un DTO en el backend, actualiza su interfaz aquí en el mismo cambio.**

---

## 5. Servicios

| Servicio | Métodos | Endpoints |
|---|---|---|
| `AuthService` | `login`, `refresh`, `restoreSession`, `logout`, `endSession`, `register`, `checkUsername`; signals `user`, `isLoggedIn`, `isBanco`; getter `accessToken`; `homeRoute()` | `/api/auth/*` |
| `AccountsService` | `cities`, `accounts`, `account(number)`, `movements(number, limit)`, `statement(number, year, month)`, `operate(number, kind, request)` | `/api/cities`, `/api/accounts/*` |
| `ReportsService` | `transactionsByBranch({ year, month, cityId?, type? })`, `transactionsByCustomer(year, month)`, `outOfCityWithdrawals(minTotal, from?, to?)` | `/api/reports/*` |
| `fetchAllPages(fetchPage)` | Función de `reports.service.ts`: pide todas las páginas de 100 en 100 (con `expand`) y junta sus elementos; la usa la exportación | — |
| `ExportService` | `toExcel(table)`, `toPdf(table)`; funciones puras `safeFileName` y `toTextRows` | — (genera el archivo en el navegador) |
| `FlowStateService` | Signals `lastOperation` y `lastRegistration` | — (pasa datos entre pantallas de un flujo) |

Todos devuelven `Observable<T>` ya desenvuelto con `map(unwrap)`, que extrae `data` del `ApiResponse`. Los errores llegan como `HttpErrorResponse`, y los componentes usan:

- `toErrorMessage(e)`: el mensaje para mostrar. Prioriza `validationErrors`, luego `message`, luego el estado HTTP. Si no hay conexión, explica que la API no responde.
- `errorCode(e)`: el código estable para reaccionar, por ejemplo `acceso_bloqueado` o `saldo_insuficiente`.

---

## 6. Sesión en el navegador

- El **access token vive solo en memoria** (un signal privado de `AuthService`); nunca se guarda en `localStorage`. El **refresh token** está en la cookie HttpOnly `kb_refresh`, que JavaScript no puede leer.
- **Al cargar la app**, `provideAppInitializer` llama a `restoreSession()`, que intenta `POST /api/auth/refresh`. Si la cookie es válida, el usuario sigue dentro tras recargar; si no, queda sin sesión, sin mostrar error.
- **`AuthInterceptor`** (clase registrada con `HTTP_INTERCEPTORS`):
  1. Agrega `Authorization: Bearer` a las llamadas a `/api/`, excepto login, refresh, logout y register.
  2. Ante un **401**, llama una vez a `auth.refresh()` y reintenta la petición original.
  3. Si la renovación falla, llama a `endSession('expirada')`, que limpia la sesión y navega a `/acceso/ingresar?motivo=expirada`.
- `refresh()` **comparte una sola llamada en vuelo** (`shareReplay`): si varias peticiones reciben 401 a la vez, se hace un único refresh. Esto importa porque el backend **rota** el token, y dos refresh simultáneos con la misma cookie se verían como una reutilización y revocarían la sesión.
- **Salir** llama a `POST /api/auth/logout`, que revoca la familia de tokens y borra la cookie, y luego limpia el estado local.
- Lo único que se guarda en `localStorage` son preferencias, con `try/catch`: el usuario recordado (`kb_usuario_recordado`) y si se ocultan los saldos (`kb_ocultar_saldos`).

---

## 7. Pantallas

| Componente | Comportamiento clave |
|---|---|
| `LoginComponent` | Detecta si el identificador es usuario o documento. El 423 se muestra como advertencia de bloqueo y el 401, 429 o un error de red, como error. Navega a `volver` o a `homeRoute()` |
| `RegisterComponent` | 3 pasos con dos `FormGroup` (`datos`, `acceso`). Persona natural o empresa ajusta los tipos de documento. Consulta la disponibilidad del usuario al salir del campo. Las reglas de la contraseña se evalúan en vivo con `checkPinRules`, espejo de `PinRules.cs`. Los `validationErrors` del 400 y el 409 se pintan en su campo y llevan al paso que contiene el error |
| `HomeComponent` | Saldo total con opción de ocultar, accesos rápidos (no para Banco), tarjetas de producto y 5 movimientos recientes |
| `AccountDetailComponent` | `forkJoin` de cuenta y 50 movimientos; reacciona a cambios de `:number` |
| `OperationComponent` | Formulario y luego confirmación. El monto se escribe en formato colombiano (`parseCopAmount`). Valida rango y, en retiros, el saldo antes de llamar a la API. La ciudad por defecto es la de origen, con aviso si es otra. Ante `saldo_insuficiente` o `validacion` vuelve al formulario |
| `ReceiptComponent` | Lee `FlowStateService.lastOperation` |
| `StatementComponent` | Cuenta y mes (`<input type="month">`, con máximo el mes actual en Bogotá). Muestra el resumen y los movimientos e imprime con `window.print()`. Envuelve la página en `data-theme="claro"` |
| `ReportsComponent` | Administración. En «Transacciones por sede», `branchGroups` agrupa las filas consecutivas de un mismo cliente y sede; la tabla usa un `<tbody>` por grupo y `rowspan` en sede, cliente, documento y total, con una fila por transacción (fecha y hora, motivo, monto, cuenta y descripción). Tres tarjetas independientes, cada una con su formulario, estado de carga, error y botones **Excel** y **PDF**: transacciones por sede (mes, sede y tipo), transacciones por cliente y retiros fuera de la ciudad. Las columnas exportables se definen una vez como `ExportColumn<T>[]` |

### Descargas en Excel y PDF

`ExportService` (`core/services/export.service.ts`) recibe un `ExportTable<T>` con nombre de archivo, título, filtros, columnas (`text`, `number` o `money`) y filas.

- **Excel**: usa **ExcelJS**. Escribe título, filtros y fecha de generación, un encabezado con color y filtro automático, y fija el formato numérico y el ancho de cada columna.
- **PDF**: usa **jsPDF** con **jspdf-autotable**, en A4 horizontal, con tabla paginada y número de página. Los montos se formatean con `Intl` en es-CO.
- Las librerías se cargan con `import()` **solo al exportar**, así no aumentan la carga inicial; quedan en chunks diferidos.
- `angular.json` declara sus dependencias CommonJS en `allowedCommonJsDependencies`.
- `package.json` fuerza `uuid@^11.1.1` para ExcelJS con `overrides`, por el aviso GHSA-w5hq-g745-h8pq.

### Componentes compartidos

| Selector | Entradas | Uso |
|---|---|---|
| `kb-alert` | `type` (`info`, `success`, `warning`, `error`), `heading`, `message`; contenido proyectado | Mensajes. Usa `role="alert"` para los errores |
| `kb-pin-field` | `inputId` (requerido), `autocomplete`, `error`, `describedBy` | Contraseña de 4 casillas. Implementa `ControlValueAccessor`, así que se usa con `formControlName`. Es **un solo** `<input type="password" inputmode="numeric" maxlength="4">` real, dibujado como 4 casillas: es accesible y compatible con el pegado y los gestores de contraseñas. Tiene botón para mostrar u ocultar |
| `kb-paginator` | `page`, `pageSize`, `totalItems`, `totalPages` (requeridos), `itemLabel`, `sizes`, `disabled`; salidas `pageChange` y `pageSizeChange` | Paginador sin estado: «Mostrando 21–40 de 135», anterior/siguiente, páginas con huecos (`pageSlots`) y tamaño de página. El padre pide la página al servidor; cada reporte guarda los filtros consultados, así cambiar de página no toma valores a medio editar del formulario |
| `kb-movement-list` | `movements` (requerido), `heading`, `showFilters`, `emptyText` | Lista agrupada por día en hora de Bogotá («HOY»), con filtros por tipo |

| Pipe | Ejemplo |
|---|---|
| `cop` | `1250000.5 \| cop` → `$ 1.250.000,5`; `'signed-in'` → `+$ …`; `'signed-out'` → `−$ …` |
| `bankDate` | `iso \| bankDate: 'datetime' \| 'date' \| 'time' \| 'day'` en `America/Bogota` |

---

## 8. Design system y estilos

- Los tokens están en `styles/kadree-tokens.css`. Los temas son `data-theme="marino"` (oscuro, por defecto en `<html>`) y `data-theme="claro"` (extractos). Los colores **siempre** se usan con variables (`var(--text)`, `var(--action)`…), nunca con valores fijos.
- Los componentes CSS están en `styles/kadree-components.css`, con clases `kb-*` en notación BEM:
  - Botones (`kb-btn--primary`, `--secondary`, `--tertiary`) y campos (`kb-field`, `kb-input-box`, `kb-input`).
  - Saldos y productos (`kb-balance`, `kb-product`, `kb-tile`), movimientos (`kb-movements`, `kb-move`), alertas (`kb-alert--*`) y pasos (`kb-steps`).
- Las clases de diseño de página están en `styles.scss`: `kb-page`, `kb-page__title`, `kb-summary`, `kb-table`, `kb-grid-2`, `kb-products`, `kb-auth-page`…
- Los espacios usan los tokens `--space-1/2/3/4/6/8/12/16`. **No existe `--space-5`.**
- Los íconos son **Material Symbols Rounded**, cargados con `icon_names` en `index.html` (solo los que se usan). **Si usas un ícono nuevo, agrégalo a esa lista**; si no, se verá su nombre como texto.
- **Accesibilidad (WCAG AA)**:
  - Cada campo tiene su `label for` y los errores se asocian con `aria-describedby` o `aria-invalid`.
  - Hay `aria-current` en la navegación y los pasos, `aria-live` en el saldo y `role="status"` en el comprobante.
  - Las tablas tienen `caption` (`kb-sr`) y el foco es visible.
  - Se respeta `prefers-reduced-motion`.

---

## 9. Pruebas y compilación

```bash
cd frontend
npm ci
npx ng test --watch=false     # Vitest + jsdom (builder @angular/build:unit-test)
npx ng build                  # salida en dist/frontend
npx ng serve                  # desarrollo en http://localhost:4200 con proxy a :8080
```

| Spec | Cubre |
|---|---|
| `core/utils/api-error.spec.ts` | Mensajes desde `ApiResponse`, `validationErrors` y error de red |
| `core/utils/pin-rules.spec.ts` | Las 4 reglas de la contraseña, incluido un NIT con guion |
| `core/utils/formatting.spec.ts` | `parseCopAmount` y los límites de mes y día en hora de Bogotá |
| `shared/pipes/cop.pipe.spec.ts` | Formato de pesos y signos |
| `core/services/export.service.spec.ts` | Nombre de archivo seguro y formato de celdas para el PDF |

**Node**: Angular 22 exige Node **22.22+ o 24.15+** (Node 23 no sirve). Alternativa con Docker:

```bash
docker run --rm -v "$PWD:/app" -v kb_fe_nm:/app/node_modules -w /app node:24-alpine sh -c "npm ci && npx ng test --watch=false && npx ng build"
```

**Producción**: el `Dockerfile` compila con Node y sirve `dist/` con Nginx. `nginx/default.conf.template` hace tres cosas:

- Envía `/api/` a `${API_UPSTREAM}`, así la SPA y la API comparten origen: no hace falta CORS y la cookie `SameSite=Strict` funciona.
- Pasa `X-Forwarded-For`, que la API usa para el rate limiting.
- Sirve `index.html` para las rutas de Angular.

---

## 10. Cómo agregar una pantalla

1. Crea el componente con `standalone: false` dentro de su feature, por ejemplo `features/banking/transfer/`.
2. Decláralo en el módulo de la feature y agrega su ruta en `<feature>-routing.module.ts`, con `title` y las guardas que necesite.
3. Si consume un endpoint nuevo:
   - Agrega la interfaz en `core/models/`, idéntica al DTO de C#.
   - Agrega el método en el servicio correspondiente, con `map(unwrap)`.
4. Usa los componentes y clases del design system (`kb-alert`, `kb-field`, `kb-btn`…) y signals para el estado.
5. Muestra los errores con `toErrorMessage`. Reacciona a casos concretos con `errorCode`.
6. Agrega specs para la lógica pura (utilidades y pipes).
