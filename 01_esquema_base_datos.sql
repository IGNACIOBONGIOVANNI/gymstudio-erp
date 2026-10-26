-- =====================================================================
-- Esquema REAL de la base de datos (PostgreSQL), generado con pg_dump
-- --schema-only sobre una base con todas las migraciones aplicadas.
--
-- La fuente de verdad son las migraciones de EF Core en
-- backend/src/EstudioContable.Infrastructure/Migrations; este archivo es
-- una foto para leer o presentar. Para regenerarlo ver docs/BASE_DE_DATOS.md.
-- El diseño inicial (previo a la implementación) está en
-- docs/diseno_inicial_esquema.sql.
-- =====================================================================

--
-- PostgreSQL database dump
--



SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: banco; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA banco;


--
-- Name: caja; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA caja;


--
-- Name: compras; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA compras;


--
-- Name: contabilidad; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA contabilidad;


--
-- Name: inventario; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA inventario;


--
-- Name: ventas; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA ventas;


--
-- Name: fn_auditar(); Type: FUNCTION; Schema: contabilidad; Owner: -
--

CREATE FUNCTION contabilidad.fn_auditar() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE
    -- Nunca se guarda el hash de la contraseña; el último login no es un
    -- cambio de datos y generaría un registro por cada inicio de sesión.
    c_ignorados CONSTANT text[] := ARRAY['PasswordHash', 'UltimoLogin'];
    v_anterior  jsonb;
    v_nuevo     jsonb;
    v_campos    text[];
BEGIN
    IF TG_OP <> 'INSERT' THEN
        v_anterior := to_jsonb(OLD) - c_ignorados;
    END IF;
    IF TG_OP <> 'DELETE' THEN
        v_nuevo := to_jsonb(NEW) - c_ignorados;
    END IF;

    IF TG_OP = 'UPDATE' THEN
        SELECT array_agg(n.key ORDER BY n.key) INTO v_campos
        FROM jsonb_each(v_nuevo) n
        WHERE v_anterior -> n.key IS DISTINCT FROM n.value;

        -- El cambio de contraseña se registra, sin guardar el hash.
        IF (to_jsonb(OLD) ->> 'PasswordHash') IS DISTINCT FROM (to_jsonb(NEW) ->> 'PasswordHash') THEN
            v_campos := array_append(COALESCE(v_campos, '{}'), 'Contraseña');
        END IF;

        -- Sólo cambiaron campos ignorados: no hay nada que auditar.
        IF v_campos IS NULL THEN
            RETURN NULL;
        END IF;
    END IF;

    INSERT INTO contabilidad.auditoria
        (tabla, operacion, registro_id, datos_anteriores, datos_nuevos, campos_modificados, usuario, fecha)
    VALUES (
        TG_TABLE_SCHEMA || '.' || TG_TABLE_NAME,
        TG_OP,
        COALESCE(v_nuevo, v_anterior) ->> 'Id',
        v_anterior,
        v_nuevo,
        COALESCE(v_campos, '{}'),
        COALESCE(NULLIF(current_setting('app.usuario', true), ''), session_user),
        now());

    RETURN NULL;
END;
$$;


--
-- Name: fn_auditoria_solo_lectura(); Type: FUNCTION; Schema: contabilidad; Owner: -
--

CREATE FUNCTION contabilidad.fn_auditoria_solo_lectura() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    RAISE EXCEPTION 'La tabla de auditoría es de sólo lectura (operación % rechazada).', TG_OP;
END;
$$;


--
-- Name: fn_proteger_asiento_confirmado(); Type: FUNCTION; Schema: contabilidad; Owner: -
--

CREATE FUNCTION contabilidad.fn_proteger_asiento_confirmado() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_estado text;
BEGIN
    IF TG_TABLE_NAME = 'lineas_asiento' THEN
        SELECT estado INTO v_estado
        FROM contabilidad.asientos_contables
        WHERE "Id" = OLD.asiento_id;

        IF v_estado IN ('Confirmado', 'Anulado') THEN
            RAISE EXCEPTION 'Las líneas del asiento % no pueden modificarse porque está %.', OLD.asiento_id, v_estado;
        END IF;
    ELSIF OLD.estado <> 'Borrador' THEN
        IF TG_OP = 'DELETE' THEN
            RAISE EXCEPTION 'El asiento N° % está % y no puede eliminarse: corresponde registrar un contraasiento.',
                OLD.numero_asiento, OLD.estado;
        END IF;

        IF NEW.estado = 'Borrador'
           OR (OLD.estado = 'Anulado' AND NEW.estado <> 'Anulado')
           OR NEW.fecha <> OLD.fecha
           OR NEW.numero_asiento <> OLD.numero_asiento
           OR NEW.descripcion <> OLD.descripcion
           OR NEW.cliente_id <> OLD.cliente_id THEN
            RAISE EXCEPTION 'El asiento N° % está % y no puede modificarse.', OLD.numero_asiento, OLD.estado;
        END IF;
    END IF;

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$;


--
-- Name: fn_sumas_y_saldos(bigint, date, date); Type: FUNCTION; Schema: contabilidad; Owner: -
--

CREATE FUNCTION contabilidad.fn_sumas_y_saldos(p_cliente_id bigint, p_desde date DEFAULT NULL::date, p_hasta date DEFAULT NULL::date) RETURNS TABLE(cuenta_id bigint, codigo text, nombre text, naturaleza integer, total_debe numeric, total_haber numeric, saldo_natural numeric)
    LANGUAGE sql STABLE
    AS $$
    SELECT c."Id",
           c."Codigo"::text,
           c."Nombre"::text,
           c."Naturaleza",
           COALESCE(SUM(m.debe), 0),
           COALESCE(SUM(m.haber), 0),
           CASE WHEN c."Naturaleza" = 0
                THEN COALESCE(SUM(m.debe), 0) - COALESCE(SUM(m.haber), 0)
                ELSE COALESCE(SUM(m.haber), 0) - COALESCE(SUM(m.debe), 0)
           END
    FROM contabilidad."CuentasContables" c
    LEFT JOIN (
        SELECT l.cuenta_id, l.debe, l.haber
        FROM contabilidad.lineas_asiento l
        JOIN contabilidad.asientos_contables a ON a."Id" = l.asiento_id
        WHERE a.estado = 'Confirmado'
          AND a.cliente_id = p_cliente_id
          AND (p_desde IS NULL OR a.fecha >= p_desde)
          AND (p_hasta IS NULL OR a.fecha <= p_hasta)
    ) m ON m.cuenta_id = c."Id"
    WHERE c."ClienteId" = p_cliente_id
    GROUP BY c."Id", c."Codigo", c."Nombre", c."Naturaleza"
    ORDER BY c."Codigo";
$$;


--
-- Name: fn_validar_balance_asiento(); Type: FUNCTION; Schema: contabilidad; Owner: -
--

CREATE FUNCTION contabilidad.fn_validar_balance_asiento() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_asiento_id bigint;
    v_estado     text;
    v_debe       numeric(18,2);
    v_haber      numeric(18,2);
    v_lineas     integer;
BEGIN
    IF TG_TABLE_NAME = 'asientos_contables' THEN
        v_asiento_id := NEW."Id";
    ELSIF TG_OP = 'DELETE' THEN
        v_asiento_id := OLD.asiento_id;
    ELSE
        v_asiento_id := NEW.asiento_id;
    END IF;

    SELECT estado INTO v_estado
    FROM contabilidad.asientos_contables
    WHERE "Id" = v_asiento_id;

    -- Los borradores pueden estar desbalanceados mientras se cargan.
    IF v_estado IS DISTINCT FROM 'Confirmado' THEN
        RETURN NULL;
    END IF;

    SELECT COALESCE(SUM(debe), 0), COALESCE(SUM(haber), 0), COUNT(*)
    INTO v_debe, v_haber, v_lineas
    FROM contabilidad.lineas_asiento
    WHERE asiento_id = v_asiento_id;

    IF v_lineas < 2 OR v_debe = 0 OR v_debe <> v_haber THEN
        RAISE EXCEPTION 'El asiento % no cumple la partida doble (Debe = %, Haber = %, líneas = %).',
            v_asiento_id, v_debe, v_haber, v_lineas
            USING ERRCODE = 'check_violation';
    END IF;

    RETURN NULL;
END;
$$;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: movimientos_banco; Type: TABLE; Schema: banco; Owner: -
--

CREATE TABLE banco.movimientos_banco (
    "Id" bigint NOT NULL,
    "ClienteId" character varying(50) NOT NULL,
    "Fecha" date NOT NULL,
    "Detalle" character varying(255) NOT NULL,
    "TipoMovimiento" integer NOT NULL,
    "Importe" numeric(18,2) NOT NULL,
    "FacturaId" bigint,
    CONSTRAINT "CK_movimientos_banco_importe_positivo" CHECK (("Importe" > (0)::numeric))
);


--
-- Name: movimientos_banco_Id_seq; Type: SEQUENCE; Schema: banco; Owner: -
--

ALTER TABLE banco.movimientos_banco ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME banco."movimientos_banco_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: movimientos_caja; Type: TABLE; Schema: caja; Owner: -
--

CREATE TABLE caja.movimientos_caja (
    "Id" bigint NOT NULL,
    "ClienteId" character varying(50) NOT NULL,
    "Fecha" date NOT NULL,
    "Detalle" character varying(255) NOT NULL,
    "TipoMovimiento" integer NOT NULL,
    "Importe" numeric(18,2) NOT NULL,
    "FacturaId" bigint,
    CONSTRAINT "CK_movimientos_caja_importe_positivo" CHECK (("Importe" > (0)::numeric))
);


--
-- Name: movimientos_caja_Id_seq; Type: SEQUENCE; Schema: caja; Owner: -
--

ALTER TABLE caja.movimientos_caja ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME caja."movimientos_caja_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: facturas_compra; Type: TABLE; Schema: compras; Owner: -
--

CREATE TABLE compras.facturas_compra (
    "Id" bigint NOT NULL,
    cliente_id bigint NOT NULL,
    fecha date NOT NULL,
    tipo_factura character varying(3) NOT NULL,
    numero_factura character varying(20) NOT NULL,
    proveedor_nombre character varying(200) NOT NULL,
    cuit character varying(20) NOT NULL,
    neto numeric(18,2) NOT NULL,
    iva numeric(18,2) NOT NULL,
    total numeric(18,2) NOT NULL,
    "CondicionPago" integer DEFAULT 0 NOT NULL,
    "AsientoContableId" bigint,
    "ProveedorId" bigint
);


--
-- Name: facturas_compra_Id_seq; Type: SEQUENCE; Schema: compras; Owner: -
--

ALTER TABLE compras.facturas_compra ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME compras."facturas_compra_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Cliente; Type: TABLE; Schema: contabilidad; Owner: -
--

CREATE TABLE contabilidad."Cliente" (
    "Id" bigint NOT NULL,
    "RazonSocial" text NOT NULL,
    "NombreFantasia" text,
    "Cuit" text NOT NULL,
    "TipoContribuyente" integer NOT NULL,
    "Email" text,
    "Telefono" text,
    "DomicilioFiscal" text,
    "Activo" boolean NOT NULL,
    "FechaAlta" timestamp with time zone NOT NULL
);


--
-- Name: Cliente_Id_seq; Type: SEQUENCE; Schema: contabilidad; Owner: -
--

ALTER TABLE contabilidad."Cliente" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME contabilidad."Cliente_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ClientesComerciales; Type: TABLE; Schema: contabilidad; Owner: -
--

CREATE TABLE contabilidad."ClientesComerciales" (
    "Id" bigint NOT NULL,
    "RazonSocial" text NOT NULL,
    "Direccion" text NOT NULL,
    "Localidad" text NOT NULL,
    "Cuit" text NOT NULL,
    "CondicionIva" text NOT NULL,
    "Activo" boolean DEFAULT false NOT NULL
);


--
-- Name: ClientesComerciales_Id_seq; Type: SEQUENCE; Schema: contabilidad; Owner: -
--

ALTER TABLE contabilidad."ClientesComerciales" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME contabilidad."ClientesComerciales_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: CuentasContables; Type: TABLE; Schema: contabilidad; Owner: -
--

CREATE TABLE contabilidad."CuentasContables" (
    "Id" bigint NOT NULL,
    "ClienteId" bigint NOT NULL,
    "Codigo" text NOT NULL,
    "Nombre" text NOT NULL,
    "Naturaleza" integer NOT NULL,
    "CuentaPadreId" bigint,
    "Activa" boolean NOT NULL
);


--
-- Name: CuentasContables_Id_seq; Type: SEQUENCE; Schema: contabilidad; Owner: -
--

ALTER TABLE contabilidad."CuentasContables" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME contabilidad."CuentasContables_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: FacturaDetalles; Type: TABLE; Schema: contabilidad; Owner: -
--

CREATE TABLE contabilidad."FacturaDetalles" (
    "Id" bigint NOT NULL,
    "FacturaVentaId" bigint,
    "FacturaCompraId" bigint,
    "ProductoId" bigint NOT NULL,
    "Cantidad" integer NOT NULL,
    "PrecioUnitario" numeric NOT NULL,
    "Subtotal" numeric NOT NULL
);


--
-- Name: FacturaDetalles_Id_seq; Type: SEQUENCE; Schema: contabilidad; Owner: -
--

ALTER TABLE contabilidad."FacturaDetalles" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME contabilidad."FacturaDetalles_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: FacturaItems; Type: TABLE; Schema: contabilidad; Owner: -
--

CREATE TABLE contabilidad."FacturaItems" (
    "Id" bigint NOT NULL,
    "FacturaId" bigint NOT NULL,
    "Descripcion" text NOT NULL,
    "Cantidad" integer NOT NULL,
    "PrecioUnitario" numeric NOT NULL,
    "Subtotal" numeric NOT NULL,
    "ProductoId" bigint
);


--
-- Name: FacturaItems_Id_seq; Type: SEQUENCE; Schema: contabilidad; Owner: -
--

ALTER TABLE contabilidad."FacturaItems" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME contabilidad."FacturaItems_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Facturas; Type: TABLE; Schema: contabilidad; Owner: -
--

CREATE TABLE contabilidad."Facturas" (
    "Id" bigint NOT NULL,
    "Fecha" timestamp with time zone NOT NULL,
    "TipoComprobante" text NOT NULL,
    "Cliente" text NOT NULL,
    "Subtotal" numeric NOT NULL,
    "Iva" numeric NOT NULL,
    "Total" numeric NOT NULL,
    "TipoOperacion" text DEFAULT ''::text NOT NULL,
    "CondicionPago" text DEFAULT ''::text NOT NULL,
    "AsientoContableId" bigint,
    "ClienteComercialId" bigint,
    "ProveedorId" bigint,
    "Cae" text,
    "FechaVencimientoCae" timestamp with time zone,
    "SocioId" bigint,
    "Anulada" boolean DEFAULT false NOT NULL,
    "FechaAnulacion" timestamp with time zone,
    CONSTRAINT "CK_Facturas_ImportesNoNegativos" CHECK ((("Subtotal" >= (0)::numeric) AND ("Iva" >= (0)::numeric))),
    CONSTRAINT "CK_Facturas_TotalEsSubtotalMasIva" CHECK (("Total" = ("Subtotal" + "Iva")))
);


--
-- Name: Facturas_Id_seq; Type: SEQUENCE; Schema: contabilidad; Owner: -
--

ALTER TABLE contabilidad."Facturas" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME contabilidad."Facturas_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Membresias; Type: TABLE; Schema: contabilidad; Owner: -
--

CREATE TABLE contabilidad."Membresias" (
    "Id" bigint NOT NULL,
    "SocioId" bigint NOT NULL,
    "PlanId" bigint NOT NULL,
    "FechaInicio" timestamp with time zone NOT NULL,
    "FechaVencimiento" timestamp with time zone NOT NULL,
    "EstaPagada" boolean NOT NULL,
    CONSTRAINT "CK_Membresias_Vigencia" CHECK (("FechaVencimiento" >= "FechaInicio"))
);


--
-- Name: Membresias_Id_seq; Type: SEQUENCE; Schema: contabilidad; Owner: -
--

ALTER TABLE contabilidad."Membresias" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME contabilidad."Membresias_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Planes; Type: TABLE; Schema: contabilidad; Owner: -
--

CREATE TABLE contabilidad."Planes" (
    "Id" bigint NOT NULL,
    "Nombre" text NOT NULL,
    "PrecioMensual" numeric NOT NULL,
    "Activo" boolean NOT NULL,
    "DuracionMeses" integer DEFAULT 0 CONSTRAINT "Planes_DuracionDias_not_null" NOT NULL,
    CONSTRAINT "CK_Planes_Valores" CHECK ((("DuracionMeses" > 0) AND ("PrecioMensual" >= (0)::numeric)))
);


--
-- Name: Planes_Id_seq; Type: SEQUENCE; Schema: contabilidad; Owner: -
--

ALTER TABLE contabilidad."Planes" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME contabilidad."Planes_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Proveedores; Type: TABLE; Schema: contabilidad; Owner: -
--

CREATE TABLE contabilidad."Proveedores" (
    "Id" bigint NOT NULL,
    "RazonSocial" text NOT NULL,
    "Direccion" text NOT NULL,
    "Localidad" text NOT NULL,
    "Cuit" text NOT NULL,
    "CondicionIva" text NOT NULL,
    "Activo" boolean NOT NULL
);


--
-- Name: Proveedores_Id_seq; Type: SEQUENCE; Schema: contabilidad; Owner: -
--

ALTER TABLE contabilidad."Proveedores" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME contabilidad."Proveedores_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Socios; Type: TABLE; Schema: contabilidad; Owner: -
--

CREATE TABLE contabilidad."Socios" (
    "Id" bigint NOT NULL,
    "Nombre" text NOT NULL,
    "Apellido" text NOT NULL,
    "Dni" text NOT NULL,
    "Telefono" text NOT NULL,
    "FechaAlta" timestamp with time zone NOT NULL,
    "Activo" boolean NOT NULL,
    "Estado" text DEFAULT ''::text NOT NULL,
    "FechaVencimiento" timestamp with time zone,
    "PlanActualId" bigint
);


--
-- Name: Socios_Id_seq; Type: SEQUENCE; Schema: contabilidad; Owner: -
--

ALTER TABLE contabilidad."Socios" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME contabilidad."Socios_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Usuario; Type: TABLE; Schema: contabilidad; Owner: -
--

CREATE TABLE contabilidad."Usuario" (
    "Id" bigint NOT NULL,
    "NombreCompleto" character varying(150) NOT NULL,
    "Email" character varying(254) NOT NULL,
    "PasswordHash" text NOT NULL,
    "Rol" integer NOT NULL,
    "ClienteId" bigint,
    "Activo" boolean NOT NULL,
    "UltimoLogin" timestamp with time zone,
    "FechaCreacion" timestamp with time zone NOT NULL,
    "EsPrincipal" boolean DEFAULT false NOT NULL,
    "Telefono" character varying(50)
);


--
-- Name: Usuario_Id_seq; Type: SEQUENCE; Schema: contabilidad; Owner: -
--

ALTER TABLE contabilidad."Usuario" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME contabilidad."Usuario_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: asientos_contables; Type: TABLE; Schema: contabilidad; Owner: -
--

CREATE TABLE contabilidad.asientos_contables (
    "Id" bigint NOT NULL,
    cliente_id bigint NOT NULL,
    numero_asiento bigint NOT NULL,
    fecha date NOT NULL,
    descripcion character varying(500) NOT NULL,
    estado character varying(20) NOT NULL,
    usuario_creador_id bigint NOT NULL,
    fecha_creacion timestamp with time zone NOT NULL,
    fecha_confirmacion timestamp with time zone,
    usuario_confirmacion_id bigint,
    asiento_revertido_id bigint
);


--
-- Name: asientos_contables_Id_seq; Type: SEQUENCE; Schema: contabilidad; Owner: -
--

ALTER TABLE contabilidad.asientos_contables ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME contabilidad."asientos_contables_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: auditoria; Type: TABLE; Schema: contabilidad; Owner: -
--

CREATE TABLE contabilidad.auditoria (
    id bigint NOT NULL,
    tabla character varying(100) NOT NULL,
    operacion character varying(10) NOT NULL,
    registro_id character varying(50),
    datos_anteriores jsonb,
    datos_nuevos jsonb,
    campos_modificados text[] NOT NULL,
    usuario character varying(254) NOT NULL,
    fecha timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: auditoria_id_seq; Type: SEQUENCE; Schema: contabilidad; Owner: -
--

ALTER TABLE contabilidad.auditoria ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME contabilidad.auditoria_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: lineas_asiento; Type: TABLE; Schema: contabilidad; Owner: -
--

CREATE TABLE contabilidad.lineas_asiento (
    "Id" bigint NOT NULL,
    asiento_id bigint NOT NULL,
    cuenta_id bigint NOT NULL,
    descripcion character varying(300),
    debe numeric(18,2) NOT NULL,
    haber numeric(18,2) NOT NULL,
    orden smallint NOT NULL,
    CONSTRAINT "CK_lineas_asiento_debe_o_haber" CHECK (((debe > (0)::numeric) <> (haber > (0)::numeric))),
    CONSTRAINT "CK_lineas_asiento_importes_no_negativos" CHECK (((debe >= (0)::numeric) AND (haber >= (0)::numeric)))
);


--
-- Name: lineas_asiento_Id_seq; Type: SEQUENCE; Schema: contabilidad; Owner: -
--

ALTER TABLE contabilidad.lineas_asiento ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME contabilidad."lineas_asiento_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: v_libro_mayor; Type: VIEW; Schema: contabilidad; Owner: -
--

CREATE VIEW contabilidad.v_libro_mayor AS
 SELECT a.cliente_id,
    c."Id" AS cuenta_id,
    c."Codigo" AS codigo,
    c."Nombre" AS nombre,
    c."Naturaleza" AS naturaleza,
    sum(l.debe) AS total_debe,
    sum(l.haber) AS total_haber,
    sum((l.debe - l.haber)) AS saldo
   FROM ((contabilidad.lineas_asiento l
     JOIN contabilidad.asientos_contables a ON ((a."Id" = l.asiento_id)))
     JOIN contabilidad."CuentasContables" c ON ((c."Id" = l.cuenta_id)))
  WHERE ((a.estado)::text = 'Confirmado'::text)
  GROUP BY a.cliente_id, c."Id", c."Codigo", c."Nombre", c."Naturaleza";


--
-- Name: MovimientosStock; Type: TABLE; Schema: inventario; Owner: -
--

CREATE TABLE inventario."MovimientosStock" (
    "Id" bigint NOT NULL,
    "ProductoId" bigint NOT NULL,
    "Fecha" timestamp with time zone NOT NULL,
    "Detalle" text NOT NULL,
    "Ingreso" integer NOT NULL,
    "Salida" integer NOT NULL,
    "Saldo" integer NOT NULL
);


--
-- Name: MovimientosStock_Id_seq; Type: SEQUENCE; Schema: inventario; Owner: -
--

ALTER TABLE inventario."MovimientosStock" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME inventario."MovimientosStock_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Productos; Type: TABLE; Schema: inventario; Owner: -
--

CREATE TABLE inventario."Productos" (
    "Id" bigint NOT NULL,
    "Nombre" text NOT NULL,
    "PrecioUnitario" numeric NOT NULL,
    "PrecioVenta" numeric NOT NULL,
    "StockActual" integer NOT NULL,
    "Calorias" numeric,
    "Carbohidratos" numeric,
    "Grasas" numeric,
    "Marca" text,
    "Proteinas" numeric,
    "Sabor" text,
    CONSTRAINT "CK_Productos_StockNoNegativo" CHECK (("StockActual" >= 0))
);


--
-- Name: Productos_Id_seq; Type: SEQUENCE; Schema: inventario; Owner: -
--

ALTER TABLE inventario."Productos" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME inventario."Productos_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL
);


--
-- Name: facturas_venta; Type: TABLE; Schema: ventas; Owner: -
--

CREATE TABLE ventas.facturas_venta (
    "Id" bigint NOT NULL,
    cliente_id bigint NOT NULL,
    fecha date NOT NULL,
    tipo_factura character varying(3) NOT NULL,
    numero_factura character varying(20) NOT NULL,
    cliente_nombre character varying(200) NOT NULL,
    cuit character varying(20) NOT NULL,
    neto numeric(18,2) NOT NULL,
    iva numeric(18,2) NOT NULL,
    total numeric(18,2) NOT NULL,
    "CondicionPago" integer DEFAULT 0 NOT NULL,
    "AsientoContableId" bigint
);


--
-- Name: facturas_venta_Id_seq; Type: SEQUENCE; Schema: ventas; Owner: -
--

ALTER TABLE ventas.facturas_venta ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME ventas."facturas_venta_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: movimientos_banco PK_movimientos_banco; Type: CONSTRAINT; Schema: banco; Owner: -
--

ALTER TABLE ONLY banco.movimientos_banco
    ADD CONSTRAINT "PK_movimientos_banco" PRIMARY KEY ("Id");


--
-- Name: movimientos_caja PK_movimientos_caja; Type: CONSTRAINT; Schema: caja; Owner: -
--

ALTER TABLE ONLY caja.movimientos_caja
    ADD CONSTRAINT "PK_movimientos_caja" PRIMARY KEY ("Id");


--
-- Name: facturas_compra PK_facturas_compra; Type: CONSTRAINT; Schema: compras; Owner: -
--

ALTER TABLE ONLY compras.facturas_compra
    ADD CONSTRAINT "PK_facturas_compra" PRIMARY KEY ("Id");


--
-- Name: Cliente PK_Cliente; Type: CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."Cliente"
    ADD CONSTRAINT "PK_Cliente" PRIMARY KEY ("Id");


--
-- Name: ClientesComerciales PK_ClientesComerciales; Type: CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."ClientesComerciales"
    ADD CONSTRAINT "PK_ClientesComerciales" PRIMARY KEY ("Id");


--
-- Name: CuentasContables PK_CuentasContables; Type: CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."CuentasContables"
    ADD CONSTRAINT "PK_CuentasContables" PRIMARY KEY ("Id");


--
-- Name: FacturaDetalles PK_FacturaDetalles; Type: CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."FacturaDetalles"
    ADD CONSTRAINT "PK_FacturaDetalles" PRIMARY KEY ("Id");


--
-- Name: FacturaItems PK_FacturaItems; Type: CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."FacturaItems"
    ADD CONSTRAINT "PK_FacturaItems" PRIMARY KEY ("Id");


--
-- Name: Facturas PK_Facturas; Type: CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."Facturas"
    ADD CONSTRAINT "PK_Facturas" PRIMARY KEY ("Id");


--
-- Name: Membresias PK_Membresias; Type: CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."Membresias"
    ADD CONSTRAINT "PK_Membresias" PRIMARY KEY ("Id");


--
-- Name: Planes PK_Planes; Type: CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."Planes"
    ADD CONSTRAINT "PK_Planes" PRIMARY KEY ("Id");


--
-- Name: Proveedores PK_Proveedores; Type: CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."Proveedores"
    ADD CONSTRAINT "PK_Proveedores" PRIMARY KEY ("Id");


--
-- Name: Socios PK_Socios; Type: CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."Socios"
    ADD CONSTRAINT "PK_Socios" PRIMARY KEY ("Id");


--
-- Name: Usuario PK_Usuario; Type: CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."Usuario"
    ADD CONSTRAINT "PK_Usuario" PRIMARY KEY ("Id");


--
-- Name: asientos_contables PK_asientos_contables; Type: CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad.asientos_contables
    ADD CONSTRAINT "PK_asientos_contables" PRIMARY KEY ("Id");


--
-- Name: auditoria PK_auditoria; Type: CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad.auditoria
    ADD CONSTRAINT "PK_auditoria" PRIMARY KEY (id);


--
-- Name: lineas_asiento PK_lineas_asiento; Type: CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad.lineas_asiento
    ADD CONSTRAINT "PK_lineas_asiento" PRIMARY KEY ("Id");


--
-- Name: MovimientosStock PK_MovimientosStock; Type: CONSTRAINT; Schema: inventario; Owner: -
--

ALTER TABLE ONLY inventario."MovimientosStock"
    ADD CONSTRAINT "PK_MovimientosStock" PRIMARY KEY ("Id");


--
-- Name: Productos PK_Productos; Type: CONSTRAINT; Schema: inventario; Owner: -
--

ALTER TABLE ONLY inventario."Productos"
    ADD CONSTRAINT "PK_Productos" PRIMARY KEY ("Id");


--
-- Name: __EFMigrationsHistory PK___EFMigrationsHistory; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."__EFMigrationsHistory"
    ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");


--
-- Name: facturas_venta PK_facturas_venta; Type: CONSTRAINT; Schema: ventas; Owner: -
--

ALTER TABLE ONLY ventas.facturas_venta
    ADD CONSTRAINT "PK_facturas_venta" PRIMARY KEY ("Id");


--
-- Name: IX_movimientos_banco_FacturaId; Type: INDEX; Schema: banco; Owner: -
--

CREATE INDEX "IX_movimientos_banco_FacturaId" ON banco.movimientos_banco USING btree ("FacturaId");


--
-- Name: IX_movimientos_caja_FacturaId; Type: INDEX; Schema: caja; Owner: -
--

CREATE INDEX "IX_movimientos_caja_FacturaId" ON caja.movimientos_caja USING btree ("FacturaId");


--
-- Name: IX_facturas_compra_ProveedorId; Type: INDEX; Schema: compras; Owner: -
--

CREATE INDEX "IX_facturas_compra_ProveedorId" ON compras.facturas_compra USING btree ("ProveedorId");


--
-- Name: IX_FacturaDetalles_FacturaCompraId; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE INDEX "IX_FacturaDetalles_FacturaCompraId" ON contabilidad."FacturaDetalles" USING btree ("FacturaCompraId");


--
-- Name: IX_FacturaDetalles_FacturaVentaId; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE INDEX "IX_FacturaDetalles_FacturaVentaId" ON contabilidad."FacturaDetalles" USING btree ("FacturaVentaId");


--
-- Name: IX_FacturaDetalles_ProductoId; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE INDEX "IX_FacturaDetalles_ProductoId" ON contabilidad."FacturaDetalles" USING btree ("ProductoId");


--
-- Name: IX_FacturaItems_FacturaId; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE INDEX "IX_FacturaItems_FacturaId" ON contabilidad."FacturaItems" USING btree ("FacturaId");


--
-- Name: IX_FacturaItems_ProductoId; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE INDEX "IX_FacturaItems_ProductoId" ON contabilidad."FacturaItems" USING btree ("ProductoId");


--
-- Name: IX_Facturas_AsientoContableId; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE INDEX "IX_Facturas_AsientoContableId" ON contabilidad."Facturas" USING btree ("AsientoContableId");


--
-- Name: IX_Facturas_ClienteComercialId; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE INDEX "IX_Facturas_ClienteComercialId" ON contabilidad."Facturas" USING btree ("ClienteComercialId");


--
-- Name: IX_Facturas_ProveedorId; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE INDEX "IX_Facturas_ProveedorId" ON contabilidad."Facturas" USING btree ("ProveedorId");


--
-- Name: IX_Facturas_SocioId; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE INDEX "IX_Facturas_SocioId" ON contabilidad."Facturas" USING btree ("SocioId");


--
-- Name: IX_Membresias_PlanId; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE INDEX "IX_Membresias_PlanId" ON contabilidad."Membresias" USING btree ("PlanId");


--
-- Name: IX_Membresias_SocioId; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE INDEX "IX_Membresias_SocioId" ON contabilidad."Membresias" USING btree ("SocioId");


--
-- Name: IX_Socios_PlanActualId; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE INDEX "IX_Socios_PlanActualId" ON contabilidad."Socios" USING btree ("PlanActualId");


--
-- Name: IX_Usuario_ClienteId; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE INDEX "IX_Usuario_ClienteId" ON contabilidad."Usuario" USING btree ("ClienteId");


--
-- Name: IX_Usuario_Email; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE UNIQUE INDEX "IX_Usuario_Email" ON contabilidad."Usuario" USING btree ("Email");


--
-- Name: IX_asientos_contables_asiento_revertido_id; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE UNIQUE INDEX "IX_asientos_contables_asiento_revertido_id" ON contabilidad.asientos_contables USING btree (asiento_revertido_id);


--
-- Name: IX_asientos_contables_cliente_id_fecha; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE INDEX "IX_asientos_contables_cliente_id_fecha" ON contabilidad.asientos_contables USING btree (cliente_id, fecha);


--
-- Name: IX_asientos_contables_cliente_id_numero_asiento; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE UNIQUE INDEX "IX_asientos_contables_cliente_id_numero_asiento" ON contabilidad.asientos_contables USING btree (cliente_id, numero_asiento);


--
-- Name: IX_auditoria_fecha; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE INDEX "IX_auditoria_fecha" ON contabilidad.auditoria USING btree (fecha);


--
-- Name: IX_auditoria_tabla_registro_id; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE INDEX "IX_auditoria_tabla_registro_id" ON contabilidad.auditoria USING btree (tabla, registro_id);


--
-- Name: IX_lineas_asiento_asiento_id; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE INDEX "IX_lineas_asiento_asiento_id" ON contabilidad.lineas_asiento USING btree (asiento_id);


--
-- Name: IX_lineas_asiento_cuenta_id; Type: INDEX; Schema: contabilidad; Owner: -
--

CREATE INDEX "IX_lineas_asiento_cuenta_id" ON contabilidad.lineas_asiento USING btree (cuenta_id);


--
-- Name: IX_MovimientosStock_ProductoId; Type: INDEX; Schema: inventario; Owner: -
--

CREATE INDEX "IX_MovimientosStock_ProductoId" ON inventario."MovimientosStock" USING btree ("ProductoId");


--
-- Name: asientos_contables trg_auditar_asientos; Type: TRIGGER; Schema: contabilidad; Owner: -
--

CREATE TRIGGER trg_auditar_asientos AFTER INSERT OR DELETE OR UPDATE ON contabilidad.asientos_contables FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();


--
-- Name: CuentasContables trg_auditar_cuentas; Type: TRIGGER; Schema: contabilidad; Owner: -
--

CREATE TRIGGER trg_auditar_cuentas AFTER INSERT OR DELETE OR UPDATE ON contabilidad."CuentasContables" FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();


--
-- Name: Facturas trg_auditar_facturas; Type: TRIGGER; Schema: contabilidad; Owner: -
--

CREATE TRIGGER trg_auditar_facturas AFTER INSERT OR DELETE OR UPDATE ON contabilidad."Facturas" FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();


--
-- Name: Planes trg_auditar_planes; Type: TRIGGER; Schema: contabilidad; Owner: -
--

CREATE TRIGGER trg_auditar_planes AFTER INSERT OR DELETE OR UPDATE ON contabilidad."Planes" FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();


--
-- Name: Socios trg_auditar_socios; Type: TRIGGER; Schema: contabilidad; Owner: -
--

CREATE TRIGGER trg_auditar_socios AFTER INSERT OR DELETE OR UPDATE ON contabilidad."Socios" FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();


--
-- Name: Usuario trg_auditar_usuarios; Type: TRIGGER; Schema: contabilidad; Owner: -
--

CREATE TRIGGER trg_auditar_usuarios AFTER INSERT OR DELETE OR UPDATE ON contabilidad."Usuario" FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();


--
-- Name: auditoria trg_auditoria_sin_truncate; Type: TRIGGER; Schema: contabilidad; Owner: -
--

CREATE TRIGGER trg_auditoria_sin_truncate BEFORE TRUNCATE ON contabilidad.auditoria FOR EACH STATEMENT EXECUTE FUNCTION contabilidad.fn_auditoria_solo_lectura();


--
-- Name: auditoria trg_auditoria_solo_lectura; Type: TRIGGER; Schema: contabilidad; Owner: -
--

CREATE TRIGGER trg_auditoria_solo_lectura BEFORE DELETE OR UPDATE ON contabilidad.auditoria FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditoria_solo_lectura();


--
-- Name: asientos_contables trg_proteger_asiento; Type: TRIGGER; Schema: contabilidad; Owner: -
--

CREATE TRIGGER trg_proteger_asiento BEFORE DELETE OR UPDATE ON contabilidad.asientos_contables FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_proteger_asiento_confirmado();


--
-- Name: lineas_asiento trg_proteger_lineas_asiento; Type: TRIGGER; Schema: contabilidad; Owner: -
--

CREATE TRIGGER trg_proteger_lineas_asiento BEFORE DELETE OR UPDATE ON contabilidad.lineas_asiento FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_proteger_asiento_confirmado();


--
-- Name: asientos_contables trg_validar_balance_asiento; Type: TRIGGER; Schema: contabilidad; Owner: -
--

CREATE CONSTRAINT TRIGGER trg_validar_balance_asiento AFTER INSERT OR UPDATE OF estado ON contabilidad.asientos_contables DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_validar_balance_asiento();


--
-- Name: lineas_asiento trg_validar_balance_lineas; Type: TRIGGER; Schema: contabilidad; Owner: -
--

CREATE CONSTRAINT TRIGGER trg_validar_balance_lineas AFTER INSERT OR DELETE OR UPDATE ON contabilidad.lineas_asiento DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_validar_balance_asiento();


--
-- Name: Productos trg_auditar_productos; Type: TRIGGER; Schema: inventario; Owner: -
--

CREATE TRIGGER trg_auditar_productos AFTER INSERT OR DELETE OR UPDATE ON inventario."Productos" FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();


--
-- Name: movimientos_banco FK_movimientos_banco_Facturas_FacturaId; Type: FK CONSTRAINT; Schema: banco; Owner: -
--

ALTER TABLE ONLY banco.movimientos_banco
    ADD CONSTRAINT "FK_movimientos_banco_Facturas_FacturaId" FOREIGN KEY ("FacturaId") REFERENCES contabilidad."Facturas"("Id") ON DELETE CASCADE;


--
-- Name: movimientos_caja FK_movimientos_caja_Facturas_FacturaId; Type: FK CONSTRAINT; Schema: caja; Owner: -
--

ALTER TABLE ONLY caja.movimientos_caja
    ADD CONSTRAINT "FK_movimientos_caja_Facturas_FacturaId" FOREIGN KEY ("FacturaId") REFERENCES contabilidad."Facturas"("Id") ON DELETE CASCADE;


--
-- Name: facturas_compra FK_facturas_compra_Proveedores_ProveedorId; Type: FK CONSTRAINT; Schema: compras; Owner: -
--

ALTER TABLE ONLY compras.facturas_compra
    ADD CONSTRAINT "FK_facturas_compra_Proveedores_ProveedorId" FOREIGN KEY ("ProveedorId") REFERENCES contabilidad."Proveedores"("Id");


--
-- Name: FacturaDetalles FK_FacturaDetalles_Productos_ProductoId; Type: FK CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."FacturaDetalles"
    ADD CONSTRAINT "FK_FacturaDetalles_Productos_ProductoId" FOREIGN KEY ("ProductoId") REFERENCES inventario."Productos"("Id") ON DELETE CASCADE;


--
-- Name: FacturaDetalles FK_FacturaDetalles_facturas_compra_FacturaCompraId; Type: FK CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."FacturaDetalles"
    ADD CONSTRAINT "FK_FacturaDetalles_facturas_compra_FacturaCompraId" FOREIGN KEY ("FacturaCompraId") REFERENCES compras.facturas_compra("Id") ON DELETE CASCADE;


--
-- Name: FacturaDetalles FK_FacturaDetalles_facturas_venta_FacturaVentaId; Type: FK CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."FacturaDetalles"
    ADD CONSTRAINT "FK_FacturaDetalles_facturas_venta_FacturaVentaId" FOREIGN KEY ("FacturaVentaId") REFERENCES ventas.facturas_venta("Id") ON DELETE CASCADE;


--
-- Name: FacturaItems FK_FacturaItems_Facturas_FacturaId; Type: FK CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."FacturaItems"
    ADD CONSTRAINT "FK_FacturaItems_Facturas_FacturaId" FOREIGN KEY ("FacturaId") REFERENCES contabilidad."Facturas"("Id") ON DELETE CASCADE;


--
-- Name: FacturaItems FK_FacturaItems_Productos_ProductoId; Type: FK CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."FacturaItems"
    ADD CONSTRAINT "FK_FacturaItems_Productos_ProductoId" FOREIGN KEY ("ProductoId") REFERENCES inventario."Productos"("Id") ON DELETE RESTRICT;


--
-- Name: Facturas FK_Facturas_ClientesComerciales_ClienteComercialId; Type: FK CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."Facturas"
    ADD CONSTRAINT "FK_Facturas_ClientesComerciales_ClienteComercialId" FOREIGN KEY ("ClienteComercialId") REFERENCES contabilidad."ClientesComerciales"("Id");


--
-- Name: Facturas FK_Facturas_Proveedores_ProveedorId; Type: FK CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."Facturas"
    ADD CONSTRAINT "FK_Facturas_Proveedores_ProveedorId" FOREIGN KEY ("ProveedorId") REFERENCES contabilidad."Proveedores"("Id");


--
-- Name: Facturas FK_Facturas_Socios_SocioId; Type: FK CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."Facturas"
    ADD CONSTRAINT "FK_Facturas_Socios_SocioId" FOREIGN KEY ("SocioId") REFERENCES contabilidad."Socios"("Id");


--
-- Name: Facturas FK_Facturas_asientos_contables_AsientoContableId; Type: FK CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."Facturas"
    ADD CONSTRAINT "FK_Facturas_asientos_contables_AsientoContableId" FOREIGN KEY ("AsientoContableId") REFERENCES contabilidad.asientos_contables("Id") ON DELETE RESTRICT;


--
-- Name: Membresias FK_Membresias_Planes_PlanId; Type: FK CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."Membresias"
    ADD CONSTRAINT "FK_Membresias_Planes_PlanId" FOREIGN KEY ("PlanId") REFERENCES contabilidad."Planes"("Id") ON DELETE CASCADE;


--
-- Name: Membresias FK_Membresias_Socios_SocioId; Type: FK CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."Membresias"
    ADD CONSTRAINT "FK_Membresias_Socios_SocioId" FOREIGN KEY ("SocioId") REFERENCES contabilidad."Socios"("Id") ON DELETE CASCADE;


--
-- Name: Socios FK_Socios_Planes_PlanActualId; Type: FK CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."Socios"
    ADD CONSTRAINT "FK_Socios_Planes_PlanActualId" FOREIGN KEY ("PlanActualId") REFERENCES contabilidad."Planes"("Id") ON DELETE SET NULL;


--
-- Name: Usuario FK_Usuario_Cliente_ClienteId; Type: FK CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad."Usuario"
    ADD CONSTRAINT "FK_Usuario_Cliente_ClienteId" FOREIGN KEY ("ClienteId") REFERENCES contabilidad."Cliente"("Id");


--
-- Name: asientos_contables FK_asientos_contables_asientos_contables_asiento_revertido_id; Type: FK CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad.asientos_contables
    ADD CONSTRAINT "FK_asientos_contables_asientos_contables_asiento_revertido_id" FOREIGN KEY (asiento_revertido_id) REFERENCES contabilidad.asientos_contables("Id") ON DELETE RESTRICT;


--
-- Name: lineas_asiento FK_lineas_asiento_CuentasContables_cuenta_id; Type: FK CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad.lineas_asiento
    ADD CONSTRAINT "FK_lineas_asiento_CuentasContables_cuenta_id" FOREIGN KEY (cuenta_id) REFERENCES contabilidad."CuentasContables"("Id") ON DELETE RESTRICT;


--
-- Name: lineas_asiento FK_lineas_asiento_asientos_contables_asiento_id; Type: FK CONSTRAINT; Schema: contabilidad; Owner: -
--

ALTER TABLE ONLY contabilidad.lineas_asiento
    ADD CONSTRAINT "FK_lineas_asiento_asientos_contables_asiento_id" FOREIGN KEY (asiento_id) REFERENCES contabilidad.asientos_contables("Id") ON DELETE CASCADE;


--
-- Name: MovimientosStock FK_MovimientosStock_Productos_ProductoId; Type: FK CONSTRAINT; Schema: inventario; Owner: -
--

ALTER TABLE ONLY inventario."MovimientosStock"
    ADD CONSTRAINT "FK_MovimientosStock_Productos_ProductoId" FOREIGN KEY ("ProductoId") REFERENCES inventario."Productos"("Id") ON DELETE CASCADE;


--
-- PostgreSQL database dump complete
--


