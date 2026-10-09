-- =====================================================================
-- Kadree Bank - Datos de prueba
-- Los movimientos se generan con la misma regla de la aplicación
-- (bloquear cuenta, validar saldo, actualizar y registrar saldo resultante),
-- así saldos y extractos son coherentes por construcción.
-- Las fechas están en hora de Bogotá (-05).
-- =====================================================================

INSERT INTO cities (id, name) VALUES
    (1, 'Bogotá'),
    (2, 'Medellín'),
    (3, 'Cali'),
    (4, 'Barranquilla'),
    (5, 'Cartagena'),
    (6, 'Bucaramanga');

INSERT INTO customers (document_number, full_name, customer_type, document_type, email, phone) VALUES
    ('1012345678',  'Ana María Gómez',                 'NATURAL',  'CC',  'ana.gomez@correo.com',        '3001234542'),
    ('71234567',    'Carlos Andrés Pérez',             'NATURAL',  'CC',  'carlos.perez@correo.com',     '3109876501'),
    ('29876543',    'Laura Restrepo',                  'NATURAL',  'CC',  'laura.restrepo@correo.com',   '3155550199'),
    ('80123456',    'Jorge Martínez',                  'NATURAL',  'CC',  'jorge.martinez@correo.com',   '3201112233'),
    ('900123456-1', 'Comercializadora Andina S.A.S.',  'JURIDICA', 'NIT', 'tesoreria@andina.com.co',     '6017654321'),
    ('900654321-7', 'Tecnologías del Caribe S.A.',     'JURIDICA', 'NIT', 'finanzas@tecnocaribe.com.co', '6053334455');

-- Usuarios de la banca en línea. La contraseña NO está aquí: 'PENDIENTE' se reemplaza al
-- arrancar la API por el hash de Auth:DemoPin (solo si está configurado; ver README).
INSERT INTO users (username, customer_id, role, pin_hash)
SELECT v.username, c.id, 'CLIENTE', 'PENDIENTE'
FROM (VALUES
    ('ana.gomez',        '1012345678'),
    ('carlos.perez',     '71234567'),
    ('laura.restrepo',   '29876543'),
    ('jorge.martinez',   '80123456'),
    ('andina.sas',       '900123456-1'),
    ('tecnocaribe',      '900654321-7')
) AS v (username, document_number)
JOIN customers c ON c.document_number = v.document_number;

INSERT INTO users (username, customer_id, role, pin_hash) VALUES ('admin.banco', NULL, 'BANCO', 'PENDIENTE');

INSERT INTO accounts (number, account_type, customer_id, customer_type, origin_city_id, opened_at)
SELECT v.number, v.account_type, c.id, c.customer_type, v.origin_city_id, timestamptz '2026-07-15 09:00-05'
FROM (VALUES
    ('AH-1001', 'AHORROS',   '1012345678',  1),
    ('AH-1006', 'AHORROS',   '1012345678',  2),
    ('AH-1002', 'AHORROS',   '71234567',    2),
    ('AH-1003', 'AHORROS',   '29876543',    3),
    ('AH-1004', 'AHORROS',   '80123456',    1),
    ('CC-2001', 'CORRIENTE', '900123456-1', 1),
    ('CC-2002', 'CORRIENTE', '900654321-7', 4)
) AS v (number, account_type, document_number, origin_city_id)
JOIN customers c ON c.document_number = v.document_number;

-- Función temporal (solo vive en esta sesión) que aplica una operación.
CREATE FUNCTION pg_temp.seed_op(p_account varchar, p_type varchar, p_amount numeric,
                                p_city integer, p_at timestamptz, p_description varchar DEFAULT NULL)
RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    v_id      bigint;
    v_balance numeric(18,2);
BEGIN
    SELECT id, balance INTO v_id, v_balance FROM accounts WHERE number = p_account FOR UPDATE;
    v_balance := v_balance + CASE p_type WHEN 'CONSIGNACION' THEN p_amount ELSE -p_amount END;
    UPDATE accounts SET balance = v_balance WHERE id = v_id;  -- el CHECK rechaza saldos negativos
    INSERT INTO movements (account_id, movement_type, amount, city_id, balance_after, description, created_at)
    VALUES (v_id, p_type, p_amount, p_city, v_balance, p_description, p_at);
END;
$$;

-- Ana María Gómez: cuenta en Bogotá (AH-1001) y en Medellín (AH-1006).
SELECT pg_temp.seed_op('AH-1001', 'CONSIGNACION', 3000000, 1, '2026-08-01 08:00-05', 'Nómina agosto');
SELECT pg_temp.seed_op('AH-1001', 'RETIRO',        150000, 1, '2026-08-12 18:20-05', 'Cajero');
SELECT pg_temp.seed_op('AH-1001', 'CONSIGNACION', 3000000, 1, '2026-09-01 08:00-05', 'Nómina septiembre');
SELECT pg_temp.seed_op('AH-1001', 'RETIRO',        600000, 2, '2026-09-05 12:10-05', 'Retiro en viaje');
SELECT pg_temp.seed_op('AH-1001', 'RETIRO',        300000, 3, '2026-09-14 16:45-05', 'Retiro en viaje');
SELECT pg_temp.seed_op('AH-1001', 'RETIRO',        200000, 1, '2026-09-20 10:00-05', 'Cajero');
SELECT pg_temp.seed_op('AH-1001', 'CONSIGNACION', 3000000, 1, '2026-10-01 08:00-05', 'Nómina octubre');
SELECT pg_temp.seed_op('AH-1001', 'RETIRO',        120000, 1, '2026-10-03 19:30-05', 'Cajero');
SELECT pg_temp.seed_op('AH-1006', 'CONSIGNACION', 1000000, 2, '2026-08-10 11:00-05', 'Apertura');
SELECT pg_temp.seed_op('AH-1006', 'RETIRO',        250000, 1, '2026-09-22 09:40-05', 'Retiro fuera de Medellín');

-- Carlos Andrés Pérez: cuenta en Medellín.
SELECT pg_temp.seed_op('AH-1002', 'CONSIGNACION', 5000000, 2, '2026-08-02 09:00-05', 'Venta vehículo');
SELECT pg_temp.seed_op('AH-1002', 'RETIRO',       1200000, 1, '2026-08-25 14:00-05', 'Retiro en Bogotá');
SELECT pg_temp.seed_op('AH-1002', 'RETIRO',        450000, 5, '2026-09-10 17:15-05', 'Vacaciones');
SELECT pg_temp.seed_op('AH-1002', 'CONSIGNACION',  800000, 2, '2026-09-28 10:30-05', 'Honorarios');
SELECT pg_temp.seed_op('AH-1002', 'RETIRO',        100000, 2, '2026-10-02 08:50-05', 'Cajero');

-- Laura Restrepo: cuenta en Cali (retiros grandes, pero casi todos en su ciudad).
SELECT pg_temp.seed_op('AH-1003', 'CONSIGNACION', 4000000, 3, '2026-08-05 10:00-05', 'Cesantías');
SELECT pg_temp.seed_op('AH-1003', 'RETIRO',       2000000, 3, '2026-09-03 11:00-05', 'Pago matrícula');
SELECT pg_temp.seed_op('AH-1003', 'RETIRO',        400000, 1, '2026-09-18 15:20-05', 'Retiro en Bogotá');
SELECT pg_temp.seed_op('AH-1003', 'CONSIGNACION',  600000, 3, '2026-09-25 09:10-05', 'Transferencia');

-- Jorge Martínez: retira EXACTAMENTE $1.000.000 fuera de su ciudad (no debe salir en el reporte 2: "supere").
SELECT pg_temp.seed_op('AH-1004', 'CONSIGNACION', 2500000, 1, '2026-08-15 09:00-05', 'Prima');
SELECT pg_temp.seed_op('AH-1004', 'RETIRO',       1000000, 6, '2026-09-08 13:00-05', 'Retiro en Bucaramanga');
SELECT pg_temp.seed_op('AH-1004', 'CONSIGNACION',  200000, 1, '2026-10-05 10:00-05', 'Reembolso');

-- Comercializadora Andina: cuenta corriente en Bogotá.
SELECT pg_temp.seed_op('CC-2001', 'CONSIGNACION', 20000000, 1, '2026-08-01 07:30-05', 'Recaudo');
SELECT pg_temp.seed_op('CC-2001', 'CONSIGNACION',  5000000, 1, '2026-09-02 07:30-05', 'Recaudo');
SELECT pg_temp.seed_op('CC-2001', 'RETIRO',        2000000, 4, '2026-09-09 10:00-05', 'Pago proveedores costa');
SELECT pg_temp.seed_op('CC-2001', 'RETIRO',        1500000, 4, '2026-09-16 10:00-05', 'Pago proveedores costa');
SELECT pg_temp.seed_op('CC-2001', 'RETIRO',         800000, 1, '2026-09-23 10:00-05', 'Caja menor');
-- 22:30 del 30 de septiembre en Bogotá = 03:30 UTC del 1 de octubre: debe contar en SEPTIEMBRE.
SELECT pg_temp.seed_op('CC-2001', 'CONSIGNACION',  4200000, 1, '2026-09-30 22:30-05', 'Recaudo cierre de mes');

-- Tecnologías del Caribe: cuenta corriente en Barranquilla, todo en su ciudad.
SELECT pg_temp.seed_op('CC-2002', 'CONSIGNACION', 9000000, 4, '2026-08-20 09:00-05', 'Capital de trabajo');
SELECT pg_temp.seed_op('CC-2002', 'RETIRO',       3000000, 4, '2026-09-12 11:00-05', 'Nómina');
SELECT pg_temp.seed_op('CC-2002', 'RETIRO',       1000000, 4, '2026-10-06 11:00-05', 'Proveedores');
