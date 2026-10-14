---
name: revisar-cambios
description: Revisa los cambios sin commit (o los de una rama) con la lista de chequeo de Kadree Bank y reporta los problemas por prioridad, sin modificar archivos.
disable-model-invocation: true
allowed-tools: Bash(git diff *) Bash(git status *) Bash(git log *) Read Grep Glob
---

# Revisar cambios

## Cambios a revisar

!`git status --short`

!`git diff HEAD --stat`

Lee el diff completo con `git diff HEAD` (o `git diff main...HEAD` si se pide revisar una rama).
**No modifiques archivos**: solo reporta.

## Lista de chequeo

**Dinero y datos**
- ¿Algo puede dejar el saldo negativo o cambiarlo fuera de `Account.Deposit` / `Withdraw`?
- ¿Las operaciones de dinero usan transacción y bloqueo de fila?
- ¿Algún SQL armado concatenando texto?
- ¿Montos con `double` o `float` en vez de `decimal` / `number` parseado con `parseCopAmount`?

**Seguridad**
- ¿Un cliente podría ver o modificar datos de otro? (debe responder 404)
- ¿El endpoint tiene la autorización correcta (sesión, `Banco`, `AllowAnonymous`)?
- ¿Hay secretos, tokens, contraseñas o datos personales en el código o en logs?

**Contratos**
- ¿Se cambió un DTO de C# sin actualizar su interfaz en `frontend/src/app/core/models/`?
- ¿Los errores salen como `ApiResponse` con un `errorCode` conocido?

**Calidad**
- ¿Hay pruebas para el comportamiento nuevo, incluido lo que **no** debe pasar?
- ¿Se cambió lo que espera una prueba solo para que pase?
- ¿Miembros públicos sin `/// <summary>`? ¿Acciones sin `[ProducesResponseType]`?
- ¿Colores o espacios fijos en vez de tokens del design system? ¿Íconos nuevos sin agregar en `index.html`?
- ¿Campos sin `label` o errores sin `aria-describedby`?

## Formato del reporte

Agrupa los hallazgos así, con archivo y línea, y una sugerencia concreta para cada uno:

1. **Crítico** (debe corregirse antes de subir)
2. **Importante** (debería corregirse)
3. **Sugerencia** (opcional)

Si no encuentras problemas en una categoría, dilo en una línea. Termina con un veredicto corto:
listo para subir, o qué falta.
