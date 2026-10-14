---
name: cambiar-esquema-bd
description: Cambia el esquema de PostgreSQL de Kadree Bank (tablas, columnas, restricciones o índices) y lo sincroniza con EF Core y las pruebas.
disable-model-invocation: true
argument-hint: "[descripción del cambio]"
---

# Cambiar el esquema de la base de datos

Cambio pedido: $ARGUMENTS

Este proyecto **no usa migraciones de EF**: el esquema está en `db/01_schema.sql`. Es un cambio
delicado, así que **muestra primero el plan con el SQL exacto y espera confirmación**.

## Pasos

1. **SQL** en `db/01_schema.sql`:
   - Nombres en `snake_case`; restricciones con prefijo `ck_`, `uq_`, `fk_`, `ix_`.
   - Fechas `timestamptz`, montos `numeric(18,2)`.
   - Un comentario corto que explique por qué existe cada restricción nueva.
2. **Datos de prueba:** si la tabla tiene datos en `db/02_seed.sql`, actualízalos.
3. **EF Core:** actualiza la entidad en `Models/Entities/` y su mapeo en `Data/KadreeBankDbContext.cs`.
4. **DTOs y frontend:** si el cambio llega a la API, actualiza el DTO y su interfaz en
   `frontend/src/app/core/models/`.
5. **Pruebas:** agrega una de integración que confirme la restricción nueva (por ejemplo, que un
   `CHECK` rechace un valor inválido).
6. **Documentación:** actualiza la tabla de la sección "Base de datos" en `docs/TECNICA-BACKEND.md`.

## Aplicar el cambio en local

Los scripts de `db/` solo se ejecutan cuando el volumen de la base está vacío, así que hay que
recrearlo. **Esto borra todos los datos locales: pide confirmación antes de correrlo.**

```bash
docker compose down -v
docker compose up -d --build
```

## Verificación

```bash
cd backend && dotnet build && dotnet test
```

Las pruebas de integración aplican los scripts en un PostgreSQL nuevo en cada ejecución.
