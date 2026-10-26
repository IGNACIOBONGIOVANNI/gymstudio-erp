-- =====================================================================
-- ERP CONTABLE - ESQUEMA INICIAL DE BASE DE DATOS (PostgreSQL 15+)
-- =====================================================================
-- Convenciones:
--   * PK: BIGINT GENERATED ALWAYS AS IDENTITY (mejor rendimiento que UUID
--     para PK autoincrementales en Postgres, salvo que necesites IDs
--     no secuenciales expuestos públicamente; en ese caso usar UUID).
--   * Montos monetarios: NUMERIC(18,2) -> NUNCA usar FLOAT/DOUBLE para
--     dinero, ya que no son exactos en base binaria y generan errores
--     de redondeo acumulativos.
--   * Timestamps: TIMESTAMPTZ (con zona horaria) para evitar ambigüedad.
-- =====================================================================

CREATE SCHEMA IF NOT EXISTS contabilidad;
SET search_path TO contabilidad;

-- ---------------------------------------------------------------------
-- ENUMS
-- ---------------------------------------------------------------------
CREATE TYPE rol_usuario AS ENUM ('Administrador', 'Contador', 'Cliente');

CREATE TYPE tipo_contribuyente AS ENUM (
    'Monotributista',
    'ResponsableInscripto',
    'ExentoIVA',
    'ConsumidorFinal'
);

CREATE TYPE estado_asiento AS ENUM ('Borrador', 'Confirmado', 'Anulado');

CREATE TYPE naturaleza_cuenta AS ENUM ('Activo', 'Pasivo', 'PatrimonioNeto', 'Resultado');

-- ---------------------------------------------------------------------
-- TABLA: clientes
-- Representa a las empresas / monotributistas atendidos por el estudio.
-- ---------------------------------------------------------------------
CREATE TABLE clientes (
    id                  BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    razon_social        VARCHAR(200)        NOT NULL,
    nombre_fantasia     VARCHAR(200),
    cuit                CHAR(11)            NOT NULL,
    tipo_contribuyente  tipo_contribuyente  NOT NULL,
    email               VARCHAR(150),
    telefono            VARCHAR(30),
    domicilio_fiscal    VARCHAR(250),
    activo              BOOLEAN             NOT NULL DEFAULT TRUE,
    fecha_alta          TIMESTAMPTZ         NOT NULL DEFAULT now(),
    fecha_actualizacion TIMESTAMPTZ         NOT NULL DEFAULT now(),

    CONSTRAINT uq_clientes_cuit UNIQUE (cuit),
    CONSTRAINT ck_clientes_cuit_formato CHECK (cuit ~ '^\d{11}$')
);

CREATE INDEX ix_clientes_razon_social ON clientes (razon_social);

-- ---------------------------------------------------------------------
-- TABLA: usuarios
-- Autenticación y roles. La contraseña NUNCA se guarda en texto plano;
-- se persiste el hash (ej. usando ASP.NET Core Identity + PBKDF2,
-- o BCrypt/Argon2 si se implementa autenticación custom).
-- ---------------------------------------------------------------------
CREATE TABLE usuarios (
    id                  BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre_completo     VARCHAR(150)    NOT NULL,
    email               VARCHAR(150)    NOT NULL,
    password_hash       VARCHAR(500)    NOT NULL,
    rol                 rol_usuario     NOT NULL,
    cliente_id          BIGINT          NULL REFERENCES clientes(id) ON DELETE SET NULL,
    activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    ultimo_login        TIMESTAMPTZ,
    fecha_creacion      TIMESTAMPTZ     NOT NULL DEFAULT now(),

    CONSTRAINT uq_usuarios_email UNIQUE (email),
    -- Un usuario con rol "Cliente" DEBE estar vinculado a un cliente.
    -- Administrador/Contador no deberían tener cliente_id.
    CONSTRAINT ck_usuarios_rol_cliente CHECK (
        (rol = 'Cliente' AND cliente_id IS NOT NULL) OR
        (rol <> 'Cliente' AND cliente_id IS NULL)
    )
);

CREATE INDEX ix_usuarios_cliente_id ON usuarios (cliente_id);

-- ---------------------------------------------------------------------
-- TABLA: plan_de_cuentas
-- Cada cliente tiene su propio plan de cuentas contables.
-- ---------------------------------------------------------------------
CREATE TABLE plan_de_cuentas (
    id              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    cliente_id      BIGINT              NOT NULL REFERENCES clientes(id) ON DELETE CASCADE,
    codigo          VARCHAR(20)         NOT NULL,
    nombre          VARCHAR(150)        NOT NULL,
    naturaleza      naturaleza_cuenta   NOT NULL,
    cuenta_padre_id BIGINT              NULL REFERENCES plan_de_cuentas(id),
    activa          BOOLEAN             NOT NULL DEFAULT TRUE,

    CONSTRAINT uq_plan_cuentas_codigo_cliente UNIQUE (cliente_id, codigo)
);

-- ---------------------------------------------------------------------
-- TABLA: asientos_contables (cabecera del asiento)
-- ---------------------------------------------------------------------
CREATE TABLE asientos_contables (
    id                  BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    cliente_id          BIGINT              NOT NULL REFERENCES clientes(id) ON DELETE RESTRICT,
    numero_asiento      BIGINT              NOT NULL,
    fecha               DATE                NOT NULL,
    descripcion         VARCHAR(500)        NOT NULL,
    estado              estado_asiento      NOT NULL DEFAULT 'Borrador',
    usuario_creador_id  BIGINT              NOT NULL REFERENCES usuarios(id) ON DELETE RESTRICT,
    fecha_creacion      TIMESTAMPTZ         NOT NULL DEFAULT now(),
    fecha_confirmacion  TIMESTAMPTZ         NULL,

    CONSTRAINT uq_asientos_numero_cliente UNIQUE (cliente_id, numero_asiento)
);

CREATE INDEX ix_asientos_cliente_fecha ON asientos_contables (cliente_id, fecha);

-- ---------------------------------------------------------------------
-- TABLA: lineas_asiento (detalle Debe / Haber)
-- Regla de oro contable: en cada línea sólo debe cargarse Debe O Haber
-- (nunca ambos), y la suma de Debe = suma de Haber por asiento.
-- Esa segunda regla se valida a nivel aplicación (ver DbContext) y se
-- refuerza aquí con un trigger de base de datos como última barrera.
-- ---------------------------------------------------------------------
CREATE TABLE lineas_asiento (
    id              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    asiento_id      BIGINT          NOT NULL REFERENCES asientos_contables(id) ON DELETE CASCADE,
    cuenta_id       BIGINT          NOT NULL REFERENCES plan_de_cuentas(id) ON DELETE RESTRICT,
    descripcion     VARCHAR(300),
    debe            NUMERIC(18,2)   NOT NULL DEFAULT 0,
    haber           NUMERIC(18,2)   NOT NULL DEFAULT 0,
    orden           SMALLINT        NOT NULL DEFAULT 0,

    CONSTRAINT ck_lineas_montos_no_negativos CHECK (debe >= 0 AND haber >= 0),
    CONSTRAINT ck_lineas_debe_o_haber CHECK (
        (debe > 0 AND haber = 0) OR (haber > 0 AND debe = 0)
    )
);

CREATE INDEX ix_lineas_asiento_id ON lineas_asiento (asiento_id);
CREATE INDEX ix_lineas_cuenta_id ON lineas_asiento (cuenta_id);

-- ---------------------------------------------------------------------
-- TRIGGER: verificar que el asiento quede balanceado (Debe = Haber)
-- al confirmarse. Es la última línea de defensa a nivel de base de
-- datos; la validación primaria ocurre en la capa de Application/EF.
-- ---------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_validar_balance_asiento()
RETURNS TRIGGER AS $$
DECLARE
    v_total_debe  NUMERIC(18,2);
    v_total_haber NUMERIC(18,2);
    v_estado      estado_asiento;
BEGIN
    SELECT estado INTO v_estado FROM asientos_contables WHERE id = COALESCE(NEW.asiento_id, OLD.asiento_id);

    -- Sólo exigimos balance estricto cuando el asiento está Confirmado.
    IF v_estado = 'Confirmado' THEN
        SELECT COALESCE(SUM(debe), 0), COALESCE(SUM(haber), 0)
        INTO v_total_debe, v_total_haber
        FROM lineas_asiento
        WHERE asiento_id = COALESCE(NEW.asiento_id, OLD.asiento_id);

        IF v_total_debe <> v_total_haber THEN
            RAISE EXCEPTION 'El asiento % no está balanceado: Debe=% Haber=%',
                COALESCE(NEW.asiento_id, OLD.asiento_id), v_total_debe, v_total_haber;
        END IF;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_validar_balance_asiento
AFTER INSERT OR UPDATE OR DELETE ON lineas_asiento
FOR EACH ROW EXECUTE FUNCTION fn_validar_balance_asiento();
