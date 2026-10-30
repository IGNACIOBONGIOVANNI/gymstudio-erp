using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstudioContable.Infrastructure.Migrations
{
    /// <summary>
    /// La auditoría guarda también desde qué equipo se hizo cada cambio: IP, sistema operativo y navegador.
    /// Las tres columnas son opcionales (los registros anteriores quedan vacíos), así que agregarlas es
    /// instantáneo y la versión anterior de la API sigue funcionando: no las informa y el trigger las deja en NULL.
    /// El trigger las toma de las variables de sesión que la API escribe en cada conexión (app.ip,
    /// app.sistema_operativo, app.navegador) y las recorta, para que un dato de equipo nunca haga fallar
    /// el cambio que se está auditando.
    /// </summary>
    public partial class AuditoriaConEquipo : Migration
    {
        private const string FnAuditarConEquipo = """
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
                    (tabla, operacion, registro_id, datos_anteriores, datos_nuevos, campos_modificados, usuario, fecha,
                     ip, sistema_operativo, navegador)
                VALUES (
                    TG_TABLE_SCHEMA || '.' || TG_TABLE_NAME,
                    TG_OP,
                    COALESCE(v_nuevo, v_anterior) ->> 'Id',
                    v_anterior,
                    v_nuevo,
                    COALESCE(v_campos, '{}'),
                    COALESCE(NULLIF(current_setting('app.usuario', true), ''), session_user),
                    now(),
                    left(NULLIF(current_setting('app.ip', true), ''), 45),
                    left(NULLIF(current_setting('app.sistema_operativo', true), ''), 60),
                    left(NULLIF(current_setting('app.navegador', true), ''), 60));

                RETURN NULL;
            END;
            $$;
            """;

        // La versión anterior, para poder volver atrás.
        private const string FnAuditarAnterior = """
            CREATE OR REPLACE FUNCTION contabilidad.fn_auditar()
            RETURNS trigger
            LANGUAGE plpgsql AS $$
            DECLARE
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

                    IF (to_jsonb(OLD) ->> 'PasswordHash') IS DISTINCT FROM (to_jsonb(NEW) ->> 'PasswordHash') THEN
                        v_campos := array_append(COALESCE(v_campos, '{}'), 'Contraseña');
                    END IF;

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
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ip",
                schema: "contabilidad",
                table: "auditoria",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "navegador",
                schema: "contabilidad",
                table: "auditoria",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sistema_operativo",
                schema: "contabilidad",
                table: "auditoria",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.Sql(FnAuditarConEquipo);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Primero la función vieja (no usa las columnas) y después se sacan las columnas.
            migrationBuilder.Sql(FnAuditarAnterior);

            migrationBuilder.DropColumn(
                name: "ip",
                schema: "contabilidad",
                table: "auditoria");

            migrationBuilder.DropColumn(
                name: "navegador",
                schema: "contabilidad",
                table: "auditoria");

            migrationBuilder.DropColumn(
                name: "sistema_operativo",
                schema: "contabilidad",
                table: "auditoria");
        }
    }
}
