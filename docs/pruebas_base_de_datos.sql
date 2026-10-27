-- =====================================================================
-- Pruebas de las reglas implementadas en PostgreSQL (triggers, CHECK,
-- auditoría, vista y función). Se ejecutan directo contra la base, sin
-- pasar por la API, para mostrar que la base se defiende sola.
--
--   docker compose exec -T db psql -U postgres -d estudio_contable < docs/pruebas_base_de_datos.sql
--
-- Cada prueba corre dentro de un bloque que captura el error esperado y
-- luego se deshace: el script NO deja datos nuevos en la base.
-- Imprime "OK" o "FALLA" por cada caso.
-- =====================================================================
\set ON_ERROR_STOP off
SET client_min_messages = notice;

-- 1. Un asiento Confirmado desbalanceado es rechazado (trigger diferido)
DO $$
DECLARE v_id bigint;
BEGIN
    BEGIN
        INSERT INTO contabilidad.asientos_contables
            (cliente_id, numero_asiento, fecha, descripcion, estado, usuario_creador_id, fecha_creacion)
        VALUES (1, 900001, current_date, 'Prueba desbalanceado', 'Confirmado', 1, now())
        RETURNING "Id" INTO v_id;

        INSERT INTO contabilidad.lineas_asiento (asiento_id, cuenta_id, debe, haber, orden) VALUES
            (v_id, 101, 100, 0, 0),
            (v_id, 104, 0, 90, 1);

        SET CONSTRAINTS ALL IMMEDIATE;  -- fuerza la validación que normalmente ocurre al COMMIT
        RAISE NOTICE 'FALLA 1: se aceptó un asiento desbalanceado';
    EXCEPTION WHEN check_violation THEN
        RAISE NOTICE 'OK 1: asiento desbalanceado rechazado -> %', SQLERRM;
    END;
END $$;

-- 2. Un asiento Confirmado balanceado se acepta (y se deshace al final)
DO $$
DECLARE v_id bigint;
BEGIN
    BEGIN
        INSERT INTO contabilidad.asientos_contables
            (cliente_id, numero_asiento, fecha, descripcion, estado, usuario_creador_id, fecha_creacion)
        VALUES (1, 900002, current_date, 'Prueba balanceado', 'Confirmado', 1, now())
        RETURNING "Id" INTO v_id;

        INSERT INTO contabilidad.lineas_asiento (asiento_id, cuenta_id, debe, haber, orden) VALUES
            (v_id, 101, 100, 0, 0),
            (v_id, 104, 0, 100, 1);

        SET CONSTRAINTS ALL IMMEDIATE;
        RAISE NOTICE 'OK 2: asiento balanceado aceptado';

        -- 3. ...pero una vez confirmado no se puede modificar
        BEGIN
            UPDATE contabilidad.lineas_asiento SET debe = 50 WHERE asiento_id = v_id AND debe > 0;
            RAISE NOTICE 'FALLA 3: se modificó una línea de un asiento confirmado';
        EXCEPTION WHEN raise_exception THEN
            RAISE NOTICE 'OK 3: modificar línea de asiento confirmado rechazado -> %', SQLERRM;
        END;

        -- 4. ...ni borrar
        BEGIN
            DELETE FROM contabilidad.asientos_contables WHERE "Id" = v_id;
            RAISE NOTICE 'FALLA 4: se borró un asiento confirmado';
        EXCEPTION WHEN raise_exception THEN
            RAISE NOTICE 'OK 4: borrar asiento confirmado rechazado -> %', SQLERRM;
        END;

        -- 5. ...ni volver a Borrador
        BEGIN
            UPDATE contabilidad.asientos_contables SET estado = 'Borrador' WHERE "Id" = v_id;
            RAISE NOTICE 'FALLA 5: un asiento confirmado volvió a borrador';
        EXCEPTION WHEN raise_exception THEN
            RAISE NOTICE 'OK 5: volver a borrador rechazado -> %', SQLERRM;
        END;

        RAISE EXCEPTION USING ERRCODE = 'P9999';  -- deshace el asiento de prueba
    EXCEPTION WHEN SQLSTATE 'P9999' THEN
        NULL;
    END;
END $$;

-- 6. CHECK: una línea no puede tener importe en Debe y en Haber a la vez
DO $$
DECLARE v_id bigint;
BEGIN
    BEGIN
        -- Asiento borrador propio, para que la prueba funcione también con la base vacía
        INSERT INTO contabilidad.asientos_contables
            (cliente_id, numero_asiento, fecha, descripcion, estado, usuario_creador_id, fecha_creacion)
        VALUES (1, 900003, current_date, 'Prueba línea inválida', 'Borrador', 1, now())
        RETURNING "Id" INTO v_id;

        INSERT INTO contabilidad.lineas_asiento (asiento_id, cuenta_id, debe, haber, orden)
        VALUES (v_id, 101, 10, 10, 0);
        RAISE NOTICE 'FALLA 6: se aceptó una línea con Debe y Haber';
    EXCEPTION WHEN check_violation THEN
        RAISE NOTICE 'OK 6: línea con Debe y Haber rechazada -> %', SQLERRM;
    END;
END $$;

-- 7. CHECK: el total de una factura debe ser subtotal + IVA
DO $$
BEGIN
    BEGIN
        INSERT INTO contabilidad."Facturas"
            ("Fecha", "TipoOperacion", "CondicionPago", "TipoComprobante", "Cliente", "Subtotal", "Iva", "Total", "Anulada")
        VALUES (now(), 'Venta', 'ContadoCaja', 'A', 'Prueba', 100, 21, 999, false);
        RAISE NOTICE 'FALLA 7: se aceptó una factura con total incorrecto';
    EXCEPTION WHEN check_violation THEN
        RAISE NOTICE 'OK 7: factura con total incorrecto rechazada -> %', SQLERRM;
    END;
END $$;

-- 8. CHECK: el stock no puede quedar negativo
DO $$
BEGIN
    BEGIN
        UPDATE inventario."Productos" SET "StockActual" = -1 WHERE "Id" = (SELECT MIN("Id") FROM inventario."Productos");
        RAISE NOTICE 'FALLA 8: se aceptó stock negativo';
    EXCEPTION WHEN check_violation THEN
        RAISE NOTICE 'OK 8: stock negativo rechazado -> %', SQLERRM;
    END;
END $$;

-- 9. Auditoría: un cambio hecho por SQL directo queda registrado con el usuario de PostgreSQL
DO $$
DECLARE v_antes bigint; v_despues bigint; v_usuario text; v_campos text[];
BEGIN
    BEGIN
        SELECT COUNT(*) INTO v_antes FROM contabilidad.auditoria;
        UPDATE contabilidad."Planes" SET "PrecioMensual" = "PrecioMensual" + 1 WHERE "Id" = (SELECT MIN("Id") FROM contabilidad."Planes");
        SELECT COUNT(*) INTO v_despues FROM contabilidad.auditoria;
        SELECT usuario, campos_modificados INTO v_usuario, v_campos FROM contabilidad.auditoria ORDER BY id DESC LIMIT 1;

        IF NOT FOUND OR v_despues = v_antes THEN
            RAISE NOTICE 'OK 9: (no hay planes cargados para probar la auditoría)';
        ELSIF v_campos = ARRAY['PrecioMensual'] THEN
            RAISE NOTICE 'OK 9: cambio auditado por "%" en los campos %', v_usuario, v_campos;
        ELSE
            RAISE NOTICE 'FALLA 9: registro de auditoría inesperado: % %', v_usuario, v_campos;
        END IF;

        RAISE EXCEPTION USING ERRCODE = 'P9999';  -- deshace el cambio de prueba
    EXCEPTION WHEN SQLSTATE 'P9999' THEN
        NULL;
    END;
END $$;

-- 10. La auditoría no se puede alterar
DO $$
BEGIN
    BEGIN
        DELETE FROM contabilidad.auditoria;
        RAISE NOTICE 'FALLA 10: se pudo borrar la auditoría';
    EXCEPTION WHEN raise_exception THEN
        RAISE NOTICE 'OK 10: borrar auditoría rechazado -> %', SQLERRM;
    END;
END $$;

-- 11. Vista y función: la partida doble se cumple en el total del Libro Mayor,
--     y la ecuación patrimonial cierra (Activo = Pasivo + PN + Resultado).
DO $$
DECLARE v_saldo_mayor numeric; v_ecuacion numeric;
BEGIN
    SELECT COALESCE(SUM(saldo), 0) INTO v_saldo_mayor FROM contabilidad.v_libro_mayor WHERE cliente_id = 1;

    SELECT COALESCE(SUM(CASE WHEN naturaleza = 0 THEN saldo_natural ELSE -saldo_natural END), 0)
    INTO v_ecuacion
    FROM contabilidad.fn_sumas_y_saldos(1);

    IF v_saldo_mayor = 0 AND v_ecuacion = 0 THEN
        RAISE NOTICE 'OK 11: Debe = Haber en el Mayor y la ecuación patrimonial cierra';
    ELSE
        RAISE NOTICE 'FALLA 11: saldo del mayor = %, diferencia de la ecuación = %', v_saldo_mayor, v_ecuacion;
    END IF;
END $$;

-- 12. Dos ventas no pueden tener el mismo número de comprobante (índice único parcial)
DO $$
BEGIN
    BEGIN
        INSERT INTO contabilidad."Facturas" ("Fecha", "TipoOperacion", "CondicionPago", "TipoComprobante", "Cliente",
                                             "Subtotal", "Iva", "Total", "Anulada", "PuntoVenta", "NumeroComprobante")
        VALUES (now(), 'Venta', 'ContadoCaja', 'B', 'Prueba', 100, 0, 100, false, 9999, 1),
               (now(), 'Venta', 'ContadoCaja', 'B', 'Prueba', 100, 0, 100, false, 9999, 1);
        RAISE NOTICE 'FALLA 12: se aceptaron dos ventas B 9999-00000001';
    EXCEPTION WHEN unique_violation THEN
        RAISE NOTICE 'OK 12: número de venta repetido rechazado -> %', SQLERRM;
    END;
END $$;

-- 13. El número de comprobante tiene que estar en el rango de AFIP (CHECK)
DO $$
BEGIN
    BEGIN
        INSERT INTO contabilidad."Facturas" ("Fecha", "TipoOperacion", "CondicionPago", "TipoComprobante", "Cliente",
                                             "Subtotal", "Iva", "Total", "Anulada", "PuntoVenta", "NumeroComprobante")
        VALUES (now(), 'Compra', 'ContadoCaja', 'A', 'Prueba', 100, 0, 100, false, 1, 123456789);
        RAISE NOTICE 'FALLA 13: se aceptó un número de comprobante de 9 dígitos';
    EXCEPTION WHEN check_violation THEN
        RAISE NOTICE 'OK 13: número fuera de rango rechazado -> %', SQLERRM;
    END;
END $$;

-- Consultas de ejemplo para mostrar en la defensa
SELECT codigo, nombre, total_debe, total_haber, saldo FROM contabilidad.v_libro_mayor WHERE cliente_id = 1 ORDER BY codigo;
SELECT codigo, nombre, naturaleza, total_debe, total_haber, saldo_natural FROM contabilidad.fn_sumas_y_saldos(1) WHERE saldo_natural <> 0;
SELECT fecha, usuario, tabla, operacion, registro_id, campos_modificados FROM contabilidad.auditoria ORDER BY id DESC LIMIT 15;
