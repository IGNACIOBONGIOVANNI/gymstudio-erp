namespace EstudioContable.Infrastructure.Migrations
{
    /// <summary>
    /// Objetos de PostgreSQL que EF Core no genera solo (triggers, funciones y vistas).
    /// Los aplica la migración ObjetosDeBaseDeDatos. Documentados en docs/BASE_DE_DATOS.md.
    /// </summary>
    internal static class SqlObjetosDeBaseDeDatos
    {
        /// <summary>
        /// Corrige datos heredados de versiones anteriores que violarían las restricciones
        /// CHECK nuevas. Se ejecuta antes de crearlas.
        /// </summary>
        public const string CorregirDatos = """
            -- Cuentas duplicadas de un seed viejo ("Caja" cargada como Pasivo e
            -- "IVA a Pagar"). Sólo se borran si no tienen ningún movimiento.
            DELETE FROM contabilidad."CuentasContables" c
            WHERE c."Id" IN (1, 2)
              AND NOT EXISTS (SELECT 1 FROM contabilidad.lineas_asiento l WHERE l.cuenta_id = c."Id");

            -- Planes con duración 0 (quedaron así al renombrar DuracionDias a
            -- DuracionMeses). El sistema ya los trataba como de 1 mes.
            UPDATE contabilidad."Planes" SET "DuracionMeses" = 1 WHERE "DuracionMeses" <= 0;

            -- Membresías cuyo vencimiento quedó microsegundos antes del inicio por un
            -- error de cálculo ya corregido (se tomaba la hora actual dos veces).
            UPDATE contabilidad."Membresias" SET "FechaVencimiento" = "FechaInicio"
            WHERE "FechaVencimiento" < "FechaInicio";

            -- Referencias huérfanas que impedirían crear las claves foráneas nuevas.
            UPDATE contabilidad."Facturas" f SET "AsientoContableId" = NULL
            WHERE f."AsientoContableId" IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM contabilidad.asientos_contables a WHERE a."Id" = f."AsientoContableId");

            UPDATE contabilidad."FacturaItems" i SET "ProductoId" = NULL
            WHERE i."ProductoId" IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM inventario."Productos" p WHERE p."Id" = i."ProductoId");
            """;

        public const string Crear = """
            -- =====================================================================
            -- 1. Partida doble: un asiento Confirmado debe balancear (Debe = Haber)
            --    y tener al menos dos líneas.
            --    Es un CONSTRAINT TRIGGER diferido: se evalúa al hacer COMMIT, cuando
            --    el asiento y todas sus líneas ya fueron insertados en la transacción.
            -- =====================================================================
            CREATE OR REPLACE FUNCTION contabilidad.fn_validar_balance_asiento()
            RETURNS trigger
            LANGUAGE plpgsql AS $$
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

            CREATE CONSTRAINT TRIGGER trg_validar_balance_lineas
                AFTER INSERT OR UPDATE OR DELETE ON contabilidad.lineas_asiento
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_validar_balance_asiento();

            CREATE CONSTRAINT TRIGGER trg_validar_balance_asiento
                AFTER INSERT OR UPDATE OF estado ON contabilidad.asientos_contables
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_validar_balance_asiento();

            -- =====================================================================
            -- 2. Inmutabilidad: un asiento Confirmado o Anulado no se borra ni se
            --    modifica (ni él ni sus líneas). Para corregirlo se registra un
            --    contraasiento. Sólo se permite pasar de Confirmado a Anulado.
            -- =====================================================================
            CREATE OR REPLACE FUNCTION contabilidad.fn_proteger_asiento_confirmado()
            RETURNS trigger
            LANGUAGE plpgsql AS $$
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

            CREATE TRIGGER trg_proteger_lineas_asiento
                BEFORE UPDATE OR DELETE ON contabilidad.lineas_asiento
                FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_proteger_asiento_confirmado();

            CREATE TRIGGER trg_proteger_asiento
                BEFORE UPDATE OR DELETE ON contabilidad.asientos_contables
                FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_proteger_asiento_confirmado();

            -- =====================================================================
            -- 3. Auditoría: cada INSERT, UPDATE o DELETE de las tablas auditadas queda
            --    registrado en contabilidad.auditoria con la fila antes y después, los
            --    campos modificados y el usuario. La API informa el usuario logueado
            --    con set_config('app.usuario', ...); si el cambio no viene de la API se
            --    registra el usuario de PostgreSQL.
            -- =====================================================================
            CREATE OR REPLACE FUNCTION contabilidad.fn_auditar()
            RETURNS trigger
            LANGUAGE plpgsql AS $$
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

            CREATE TRIGGER trg_auditar_facturas         AFTER INSERT OR UPDATE OR DELETE ON contabilidad."Facturas"         FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();
            CREATE TRIGGER trg_auditar_asientos         AFTER INSERT OR UPDATE OR DELETE ON contabilidad.asientos_contables FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();
            CREATE TRIGGER trg_auditar_cuentas          AFTER INSERT OR UPDATE OR DELETE ON contabilidad."CuentasContables" FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();
            CREATE TRIGGER trg_auditar_usuarios         AFTER INSERT OR UPDATE OR DELETE ON contabilidad."Usuario"          FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();
            CREATE TRIGGER trg_auditar_planes           AFTER INSERT OR UPDATE OR DELETE ON contabilidad."Planes"           FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();
            CREATE TRIGGER trg_auditar_socios           AFTER INSERT OR UPDATE OR DELETE ON contabilidad."Socios"           FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();
            CREATE TRIGGER trg_auditar_productos        AFTER INSERT OR UPDATE OR DELETE ON inventario."Productos"          FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditar();

            -- La auditoría es de sólo lectura: ni siquiera la propia base permite
            -- modificar o borrar registros.
            CREATE OR REPLACE FUNCTION contabilidad.fn_auditoria_solo_lectura()
            RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'La tabla de auditoría es de sólo lectura (operación % rechazada).', TG_OP;
            END;
            $$;

            CREATE TRIGGER trg_auditoria_solo_lectura
                BEFORE UPDATE OR DELETE ON contabilidad.auditoria
                FOR EACH ROW EXECUTE FUNCTION contabilidad.fn_auditoria_solo_lectura();

            CREATE TRIGGER trg_auditoria_sin_truncate
                BEFORE TRUNCATE ON contabilidad.auditoria
                FOR EACH STATEMENT EXECUTE FUNCTION contabilidad.fn_auditoria_solo_lectura();

            -- =====================================================================
            -- 4. Vista del Libro Mayor: totales y saldo por cuenta de los asientos
            --    confirmados (los contraasientos también suman, y dejan en cero lo
            --    que revierten).
            -- =====================================================================
            CREATE OR REPLACE VIEW contabilidad.v_libro_mayor AS
            SELECT a.cliente_id,
                   c."Id"           AS cuenta_id,
                   c."Codigo"::text AS codigo,
                   c."Nombre"::text AS nombre,
                   c."Naturaleza"   AS naturaleza,
                   SUM(l.debe)           AS total_debe,
                   SUM(l.haber)          AS total_haber,
                   SUM(l.debe - l.haber) AS saldo
            FROM contabilidad.lineas_asiento l
            JOIN contabilidad.asientos_contables a ON a."Id" = l.asiento_id
            JOIN contabilidad."CuentasContables" c ON c."Id" = l.cuenta_id
            WHERE a.estado = 'Confirmado'
            GROUP BY a.cliente_id, c."Id", c."Codigo", c."Nombre", c."Naturaleza";

            -- =====================================================================
            -- 5. Balance de sumas y saldos entre fechas (parámetros opcionales).
            --    Devuelve todas las cuentas del cliente, con el saldo expresado según
            --    su naturaleza: Debe - Haber para el Activo (0) y Haber - Debe para
            --    Pasivo (1), Patrimonio Neto (2) y Resultado (3).
            -- =====================================================================
            CREATE OR REPLACE FUNCTION contabilidad.fn_sumas_y_saldos(
                p_cliente_id bigint,
                p_desde      date DEFAULT NULL,
                p_hasta      date DEFAULT NULL)
            RETURNS TABLE (
                cuenta_id     bigint,
                codigo        text,
                nombre        text,
                naturaleza    integer,
                total_debe    numeric,
                total_haber   numeric,
                saldo_natural numeric)
            LANGUAGE sql STABLE AS $$
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
            """;

        public const string Eliminar = """
            DROP FUNCTION IF EXISTS contabilidad.fn_sumas_y_saldos(bigint, date, date);
            DROP VIEW IF EXISTS contabilidad.v_libro_mayor;

            DROP TRIGGER IF EXISTS trg_auditoria_sin_truncate ON contabilidad.auditoria;
            DROP TRIGGER IF EXISTS trg_auditoria_solo_lectura ON contabilidad.auditoria;
            DROP FUNCTION IF EXISTS contabilidad.fn_auditoria_solo_lectura();

            DROP TRIGGER IF EXISTS trg_auditar_productos ON inventario."Productos";
            DROP TRIGGER IF EXISTS trg_auditar_socios    ON contabilidad."Socios";
            DROP TRIGGER IF EXISTS trg_auditar_planes    ON contabilidad."Planes";
            DROP TRIGGER IF EXISTS trg_auditar_usuarios  ON contabilidad."Usuario";
            DROP TRIGGER IF EXISTS trg_auditar_cuentas   ON contabilidad."CuentasContables";
            DROP TRIGGER IF EXISTS trg_auditar_asientos  ON contabilidad.asientos_contables;
            DROP TRIGGER IF EXISTS trg_auditar_facturas  ON contabilidad."Facturas";
            DROP FUNCTION IF EXISTS contabilidad.fn_auditar();

            DROP TRIGGER IF EXISTS trg_proteger_asiento        ON contabilidad.asientos_contables;
            DROP TRIGGER IF EXISTS trg_proteger_lineas_asiento ON contabilidad.lineas_asiento;
            DROP FUNCTION IF EXISTS contabilidad.fn_proteger_asiento_confirmado();

            DROP TRIGGER IF EXISTS trg_validar_balance_asiento ON contabilidad.asientos_contables;
            DROP TRIGGER IF EXISTS trg_validar_balance_lineas  ON contabilidad.lineas_asiento;
            DROP FUNCTION IF EXISTS contabilidad.fn_validar_balance_asiento();
            """;
    }
}
