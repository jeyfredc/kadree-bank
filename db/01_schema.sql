-- =====================================================================
-- Kadree Bank - Esquema (PostgreSQL 17)
-- Se ejecuta automáticamente al crear el contenedor de base de datos
-- (docker-entrypoint-initdb.d) y desde las pruebas de integración.
-- =====================================================================

-- Ciudades: catálogo para la ciudad de origen de la cuenta y la ciudad de cada movimiento.
CREATE TABLE cities (
    id    integer      PRIMARY KEY,
    name  varchar(80)  NOT NULL,
    CONSTRAINT uq_cities_name UNIQUE (name)
);

CREATE TABLE customers (
    id               bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    document_number  varchar(20)   NOT NULL,
    full_name        varchar(150)  NOT NULL,
    customer_type    varchar(10)   NOT NULL,
    document_type    varchar(3)    NOT NULL,
    email            varchar(150),
    phone            varchar(15),
    created_at       timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT uq_customers_document UNIQUE (document_number),
    CONSTRAINT ck_customers_type CHECK (customer_type IN ('NATURAL', 'JURIDICA')),
    -- Cédula, cédula de extranjería y pasaporte para personas; NIT solo para empresas.
    CONSTRAINT ck_customers_document_type CHECK (
        (customer_type = 'NATURAL'  AND document_type IN ('CC', 'CE', 'PA')) OR
        (customer_type = 'JURIDICA' AND document_type = 'NIT')),
    -- Clave candidata usada por la FK compuesta de accounts (ver abajo).
    CONSTRAINT uq_customers_id_type UNIQUE (id, customer_type)
);

CREATE TABLE accounts (
    id              bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    number          varchar(20)    NOT NULL,
    account_type    varchar(10)    NOT NULL,
    customer_id     bigint         NOT NULL,
    customer_type   varchar(10)    NOT NULL,
    origin_city_id  integer        NOT NULL REFERENCES cities (id),
    balance         numeric(18,2)  NOT NULL DEFAULT 0,
    opened_at       timestamptz    NOT NULL DEFAULT now(),
    CONSTRAINT uq_accounts_number UNIQUE (number),
    CONSTRAINT ck_accounts_type CHECK (account_type IN ('AHORROS', 'CORRIENTE')),

    -- REGLA DE NEGOCIO: una cuenta nunca puede quedar con saldo negativo.
    -- Es la última línea de defensa: aunque un error de código intentara sobregirar,
    -- el motor rechaza la transacción completa.
    CONSTRAINT ck_accounts_balance_non_negative CHECK (balance >= 0),

    -- Ahorros solo para personas naturales y corriente solo para empresas.
    -- Se copia customer_type en la cuenta y se ata con una FK compuesta, para que la
    -- regla sea un CHECK declarativo en lugar de un trigger.
    CONSTRAINT fk_accounts_customer FOREIGN KEY (customer_id, customer_type)
        REFERENCES customers (id, customer_type),
    CONSTRAINT ck_accounts_type_matches_customer CHECK (
        (account_type = 'AHORROS'   AND customer_type = 'NATURAL') OR
        (account_type = 'CORRIENTE' AND customer_type = 'JURIDICA'))
);

CREATE INDEX ix_accounts_customer ON accounts (customer_id);

-- Números de las cuentas abiertas desde el registro en línea: AH-5001, CC-5002...
-- (las cuentas de los datos de prueba usan 1xxx y 2xxx).
CREATE SEQUENCE account_number_seq START WITH 5001;

-- Usuarios de la banca en línea. Un cliente tiene a lo sumo un usuario;
-- el personal del banco (rol BANCO) no está ligado a un cliente.
CREATE TABLE users (
    id               bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    username         varchar(20)   NOT NULL,
    customer_id      bigint        REFERENCES customers (id),
    role             varchar(10)   NOT NULL,
    -- Hash de la contraseña de 4 dígitos: PBKDF2 sobre un HMAC con clave secreta (pepper)
    -- que vive fuera de la base de datos. 'PENDIENTE' = usuario de prueba sin clave asignada.
    pin_hash         varchar(200)  NOT NULL,
    failed_attempts  integer       NOT NULL DEFAULT 0,
    locked_until     timestamptz,
    created_at       timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT uq_users_username UNIQUE (username),
    CONSTRAINT uq_users_customer UNIQUE (customer_id),
    CONSTRAINT ck_users_username CHECK (username ~ '^[a-z0-9.]{6,20}$' AND username ~ '[a-z]'),
    CONSTRAINT ck_users_role CHECK (role IN ('CLIENTE', 'BANCO')),
    CONSTRAINT ck_users_role_customer CHECK (
        (role = 'CLIENTE' AND customer_id IS NOT NULL) OR
        (role = 'BANCO'   AND customer_id IS NULL)),
    CONSTRAINT ck_users_failed_attempts CHECK (failed_attempts >= 0)
);

-- El administrador (rol BANCO) es único: un índice único parcial sobre la constante
-- impide insertar un segundo usuario con ese rol.
CREATE UNIQUE INDEX uq_users_single_admin ON users ((true)) WHERE role = 'BANCO';

-- Refresh tokens: solo se guarda su SHA-256, nunca el token. Cada uso lo rota
-- (revoked_at + replaced_by_hash); reutilizar uno revocado invalida toda su familia.
CREATE TABLE refresh_tokens (
    id                bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id           bigint       NOT NULL REFERENCES users (id),
    token_hash        char(64)     NOT NULL,
    family_id         uuid         NOT NULL,
    expires_at        timestamptz  NOT NULL,
    created_at        timestamptz  NOT NULL DEFAULT now(),
    revoked_at        timestamptz,
    replaced_by_hash  char(64),
    CONSTRAINT uq_refresh_tokens_hash UNIQUE (token_hash)
);

CREATE INDEX ix_refresh_tokens_family ON refresh_tokens (family_id);
CREATE INDEX ix_refresh_tokens_user ON refresh_tokens (user_id);

-- Movimientos: libro mayor inmutable (solo inserción).
CREATE TABLE movements (
    id             bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    account_id     bigint         NOT NULL REFERENCES accounts (id),
    movement_type  varchar(12)    NOT NULL,
    amount         numeric(18,2)  NOT NULL,
    city_id        integer        NOT NULL REFERENCES cities (id),
    balance_after  numeric(18,2)  NOT NULL,
    description    varchar(200),
    created_at     timestamptz    NOT NULL DEFAULT now(),
    -- Usuario que hizo la operación (auditoría). Null en los datos de prueba históricos.
    created_by_user_id bigint     REFERENCES users (id),
    CONSTRAINT ck_movements_type CHECK (movement_type IN ('CONSIGNACION', 'RETIRO')),
    CONSTRAINT ck_movements_amount_positive CHECK (amount > 0),
    CONSTRAINT ck_movements_balance_after_non_negative CHECK (balance_after >= 0)
);

-- Movimientos recientes y extractos: filtran por cuenta y ordenan por fecha.
CREATE INDEX ix_movements_account_created ON movements (account_id, created_at DESC, id DESC);
-- Reporte 1: conteo por mes (rango de fechas sobre todas las cuentas).
CREATE INDEX ix_movements_created ON movements (created_at);
-- Reporte 2: solo retiros; índice parcial pequeño con la ciudad para comparar con el origen.
CREATE INDEX ix_movements_withdrawals ON movements (account_id, city_id, created_at)
    INCLUDE (amount) WHERE movement_type = 'RETIRO';

-- Los movimientos no se editan ni se borran: las correcciones son movimientos nuevos.
CREATE FUNCTION fn_movements_append_only() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'Los movimientos son inmutables: % no está permitido', TG_OP
        USING ERRCODE = 'integrity_constraint_violation';
END;
$$;

CREATE TRIGGER tr_movements_append_only
    BEFORE UPDATE OR DELETE ON movements
    FOR EACH ROW EXECUTE FUNCTION fn_movements_append_only();
