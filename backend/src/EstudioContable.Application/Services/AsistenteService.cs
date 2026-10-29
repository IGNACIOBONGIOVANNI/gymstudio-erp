using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EstudioContable.Application.Common;
using EstudioContable.Application.Interfaces;
using EstudioContable.Domain;
using EstudioContable.Domain.Asistente;
using EstudioContable.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EstudioContable.Application.Services
{
    public record FilaDeRespuesta(string Etiqueta, string Valor);

    public record RespuestaDelAsistente(string Texto, IReadOnlyList<FilaDeRespuesta> Filas, IReadOnlyList<string> Sugerencias);

    public interface IAsistenteService
    {
        Task<RespuestaDelAsistente> PreguntarAsync(string pregunta, bool veFinanzas, DateOnly hoy, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Asistente por reglas: entiende las preguntas más comunes sobre el negocio (ventas, compras, caja, deudas,
    /// stock, socios...) y las contesta con los datos del sistema. No usa internet ni inteligencia artificial
    /// externa: reconoce palabras clave y períodos, consulta la base y arma la respuesta. Es de solo lectura y respeta
    /// los permisos: lo financiero solo se responde a Administrador y Contador.
    /// </summary>
    public class AsistenteService : IAsistenteService
    {
        private const int StockBajo = 5;
        private const int DiasPorVencer = 7;
        private const int MaximoDeFilas = 8;

        private readonly IAppDbContext _context;
        private readonly ICuentasCorrientesService _cuentas;
        private readonly IControlIntegridadService _integridad;
        private readonly IAsistenciasService _asistencias;

        public AsistenteService(IAppDbContext context, ICuentasCorrientesService cuentas, IControlIntegridadService integridad, IAsistenciasService asistencias)
        {
            _context = context;
            _cuentas = cuentas;
            _integridad = integridad;
            _asistencias = asistencias;
        }

        private static readonly string[] SugerenciasDeGestion =
        {
            "¿Cuánto vendí este mes?", "¿Vendí más que el mes pasado?", "¿Quién me debe?", "¿Está todo bien con la contabilidad?",
            "¿Quién vino hoy?", "¿Qué productos están sin stock?", "¿Cuál es el producto más vendido?", "¿Cuánto hay en caja?",
            "¿Quiénes no vienen hace tiempo?", "¿Cuántas cuotas vencen esta semana?", "Dame un resumen",
        };

        private static readonly string[] SugerenciasDeRecepcion =
        {
            "¿Quién vino hoy?", "¿Quiénes no vienen hace tiempo?", "¿Cuántas cuotas vencen esta semana?", "¿Qué socios tienen la cuota vencida?",
            "¿Quiénes no tienen rutina?", "¿Qué planes hay?", "¿Qué productos están sin stock?", "¿Cuántos socios hay?", "Dame un resumen",
        };

        private static string[] Sugerir(bool veFinanzas, string pregunta)
            => (veFinanzas ? SugerenciasDeGestion : SugerenciasDeRecepcion)
                .Where(s => Lenguaje.Normalizar(s) != Lenguaje.Normalizar(pregunta)).Take(4).ToArray();

        private static RespuestaDelAsistente Respuesta(string texto, bool veFinanzas, string pregunta, params FilaDeRespuesta[] filas)
            => new(texto, filas, Sugerir(veFinanzas, pregunta));

        public async Task<RespuestaDelAsistente> PreguntarAsync(string pregunta, bool veFinanzas, DateOnly hoy, CancellationToken cancellationToken)
        {
            var q = Lenguaje.Normalizar(pregunta);
            var periodo = Lenguaje.InterpretarPeriodo(q, hoy);
            bool Tiene(params string[] raices) => Lenguaje.Tiene(q, raices);
            bool Frase(params string[] frases) => Lenguaje.TieneFrase(q, frases);

            // --- Conversación ------------------------------------------------------------------------------
            if (q.Split(' ').Length <= 4 && Tiene("hola", "buenas", "buen", "hey", "saludos"))
                return Respuesta("¡Hola! Soy el asistente del sistema. Respondo con los datos que ya están cargados: preguntame lo que quieras saber o probá con alguna de estas.", veFinanzas, pregunta);
            if (Tiene("ayuda", "ayudame", "comandos") || Frase("que puedo preguntar", "que sabes", "que podes", "como funcionas", "para que servis", "que haces"))
                return Respuesta(veFinanzas
                    ? "Puedo contestar sobre ventas, compras, resultado, IVA, caja y banco, quién te debe (o cuánto te debe un cliente) y a quién le debés, comparaciones con el mes pasado, productos más vendidos, stock, facturas anuladas, y si la contabilidad está en orden. Del gimnasio: socios, cuotas, rutinas, planes, quién vino hoy, quiénes no vienen hace tiempo y la ficha de un socio por nombre o DNI. Podés agregar un período: \"hoy\", \"ayer\", \"esta semana\", \"este mes\", \"el mes pasado\", \"marzo de 2026\" o \"este año\"."
                    : "Puedo contestar sobre socios (la ficha de uno por nombre o DNI), cuotas por vencer o vencidas, rutinas, planes y precios, quién vino hoy, quiénes no vienen hace tiempo, socios nuevos y stock de productos.", veFinanzas, pregunta);
            if (Tiene("resumen", "novedad", "novedades", "alerta", "alertas") || Frase("como vamos", "como estamos", "como viene", "como anda", "como andamos", "que pasa"))
                return await ResumenAsync(veFinanzas, hoy, periodo, pregunta, cancellationToken);

            // --- Gimnasio y stock (todos los roles) ----------------------------------------------------------
            // Si la pregunta nombra a un socio (por nombre, apellido o DNI) se contesta con su ficha, salvo que hable de plata
            // ("¿cuánto me debe Pérez?" es de un cliente, no de un socio).
            if (!Tiene("debe", "deben", "debo", "debemos", "deud", "pagar", "cobrar", "factur", "vend", "compr", "gast"))
            {
                var ficha = await FichaDeSocioAsync(q, veFinanzas, hoy, pregunta, cancellationToken);
                if (ficha is not null) return ficha;
            }

            if (Frase("no vien", "no vino", "no vinieron", "sin venir", "dejaron de venir", "dejo de venir", "ausent", "abandon", "en riesgo", "riesgo de", "no asistio", "no asistieron", "inactiv"))
                return await SociosEnRiesgoAsync(Lenguaje.DiasDeLaPregunta(q, ReglasDeAsistencia.DiasSinVenirPorDefecto), veFinanzas, hoy, pregunta, cancellationToken);
            if (Tiene("nuevo", "nuevos", "nueva", "nuevas", "anotaron", "anotado", "anotados", "altas", "inscri") && Tiene("socio", "socios", "alumno", "alumnos"))
                return await SociosNuevosAsync(periodo, veFinanzas, pregunta, cancellationToken);
            if (Tiene("vino", "vinieron", "vienen", "asistenc", "asistio", "asistieron", "visita", "visitas", "concurr", "entrenaron", "ingresaron", "entraron") || (Tiene("ingres") && Tiene("socio", "socios", "gimnasio", "gym")))
            {
                // Sin un período dicho ("¿quién vino?") se entiende que es hoy.
                var sinPeriodo = periodo.Etiqueta.StartsWith("este mes", StringComparison.Ordinal) && !Frase("este mes", "en el mes", "del mes");
                return await VisitasAsync(sinPeriodo ? Lenguaje.InterpretarPeriodo("hoy", hoy) : periodo, veFinanzas, pregunta, cancellationToken);
            }
            var hablaDePlanes = Tiene("planes") || (Tiene("plan") && Tiene("cuesta", "cuestan", "precio", "precios", "valor", "cuanto", "ofrece", "ofrecen", "tenemos", "hay"));
            if (hablaDePlanes && !Tiene("socio", "socios", "vencid", "vence", "vencen", "vencer"))
                return await PlanesAsync(veFinanzas, pregunta, cancellationToken);

            if (Tiene("rutina", "rutinas") && Tiene("vencid", "vencen", "vencer", "vence"))
                return await SociosAsync(Tipo.RutinasVencidas, veFinanzas, hoy, pregunta, cancellationToken);
            if (Frase("sin rutina") || (Tiene("rutina", "rutinas") && Tiene("sin", "falta", "faltan", "no", "asignar")))
                return await SociosAsync(Tipo.SinRutina, veFinanzas, hoy, pregunta, cancellationToken);
            if (Tiene("vence", "vencen", "vencer", "vencimiento", "venciendo") && !Tiene("factura", "facturas"))
                return await SociosAsync(Tipo.PorVencer, veFinanzas, hoy, pregunta, cancellationToken);
            if (Tiene("vencid", "adeudan") && Tiene("socio", "socios", "cuota", "cuotas", "membresia", "plan", "gimnasio"))
                return await SociosAsync(Tipo.Vencidos, veFinanzas, hoy, pregunta, cancellationToken);
            if (Tiene("socio", "socios", "alumno", "alumnos") && Tiene("cuant", "total", "hay", "tenemos", "tengo", "activo"))
                return await SociosAsync(Tipo.Cantidad, veFinanzas, hoy, pregunta, cancellationToken);

            var esConsultaDeProductoVendido = Frase("mas vend", "mejor vend", "top", "producto estrella", "que se vende") || (Tiene("producto", "productos") && Tiene("vend"));
            if (!esConsultaDeProductoVendido
                && (Tiene("stock", "existencia", "existencias", "inventario", "agotad", "agotar", "agotarse") || (Tiene("producto", "productos") && Tiene("quedan", "queda", "hay", "sin", "poco"))))
                return await StockAsync(q, veFinanzas, pregunta, cancellationToken);

            // --- Lo financiero: solo Administrador y Contador ---------------------------------------------------
            var esFinanciera = esConsultaDeProductoVendido
                || Tiene("integridad", "auditor", "cuadra", "cuadran", "balancea", "verific", "anulad", "iva", "resultado", "ganancia", "gane", "ganamos",
                         "perdida", "perdi", "rentabil", "utilidad", "debo", "debemos", "pagar", "proveedor", "proveedores", "acreedor", "debe", "deben", "deuda",
                         "deudor", "deudores", "cobrar", "moroso", "morosos", "caja", "banco", "compr", "gaste", "gasto", "gastamos", "vend", "venta", "ventas",
                         "factur", "ingres", "recaud")
                || Frase("todo bien", "hay errores", "contabilidad", "en orden");
            if (!esFinanciera)
                return Respuesta("No entendí tu pregunta. Probá con alguna de estas o escribí \"ayuda\" para ver todo lo que puedo contestar.", veFinanzas, pregunta);

            if (!veFinanzas)
                return Respuesta("Los importes, las ventas y la contabilidad los puede ver solo Administración o Contaduría. Yo te puedo ayudar con socios, cuotas, rutinas, ingresos y stock.", veFinanzas, pregunta);

            if (Tiene("integridad", "auditor", "cuadra", "cuadran", "balancea", "verific") || Frase("todo bien", "hay errores", "contabilidad", "en orden"))
                return await IntegridadAsync(pregunta, cancellationToken);
            if (esConsultaDeProductoVendido)
                return await ProductosMasVendidosAsync(periodo, pregunta, cancellationToken);
            var pideComparar = Tiene("compar", "respecto", "subio", "subieron", "baj", "crecio", "crecieron", "aument") || Frase("mas que", "menos que");
            if (pideComparar && Tiene("vend", "venta", "ventas", "compr", "gaste", "gasto", "resultado", "ganancia", "gane", "perdida"))
                return await ComparacionAsync(q, hoy, pregunta, cancellationToken);
            if (Tiene("anulad"))
                return await AnuladasAsync(periodo, pregunta, cancellationToken);
            if (Tiene("iva"))
                return await IvaAsync(periodo, pregunta, cancellationToken);
            if (Tiene("resultado", "ganancia", "gane", "ganamos", "perdida", "perdi", "rentabil", "utilidad"))
                return await ResultadoAsync(periodo, pregunta, cancellationToken);
            if (Tiene("debo", "debemos", "pagar", "proveedor", "proveedores", "acreedor"))
                return await DeudasAsync(clientes: false, q, hoy, pregunta, cancellationToken);
            if (Tiene("debe", "deben", "deuda", "deudor", "deudores", "cobrar", "moroso", "morosos") || (Tiene("cliente", "clientes") && Tiene("saldo")))
                return await DeudasAsync(clientes: true, q, hoy, pregunta, cancellationToken);
            if (Tiene("caja") || Tiene("banco"))
                return await TesoreriaAsync(Tiene("caja"), Tiene("banco"), pregunta, cancellationToken);
            if (Tiene("compr", "gaste", "gasto", "gastamos"))
                return await VentasOComprasAsync("Compra", periodo, pregunta, cancellationToken);
            return await VentasOComprasAsync("Venta", periodo, pregunta, cancellationToken);
        }

        private static DateTime Utc(DateOnly d) => new(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc);

        private static string Mas(int total) => total > MaximoDeFilas ? $" y {total - MaximoDeFilas} más" : "";

        // --- Ventas, compras, resultado e IVA -------------------------------------------------------------------

        private record Totales(decimal Neto, decimal Iva, decimal Total, int Cantidad);

        private async Task<Totales> TotalesAsync(string operacion, Periodo p, CancellationToken cancellationToken)
        {
            var desde = Utc(p.Desde);
            var hasta = Utc(p.Hasta);
            var consulta = _context.Facturas.AsNoTracking().Where(f => !f.Anulada && f.TipoOperacion == operacion && f.Fecha >= desde && f.Fecha < hasta);
            return new Totales(
                await consulta.SumAsync(f => f.Subtotal, cancellationToken),
                await consulta.SumAsync(f => f.Iva, cancellationToken),
                await consulta.SumAsync(f => f.Total, cancellationToken),
                await consulta.CountAsync(cancellationToken));
        }

        private async Task<RespuestaDelAsistente> VentasOComprasAsync(string operacion, Periodo p, string pregunta, CancellationToken cancellationToken)
        {
            var t = await TotalesAsync(operacion, p, cancellationToken);
            var esVenta = operacion == "Venta";
            if (t.Cantidad == 0)
                return Respuesta($"{Cuando(p)} no hay {(esVenta ? "ventas" : "compras")} cargadas.", true, pregunta);
            return Respuesta(
                $"{Cuando(p)} {(esVenta ? "vendiste" : "compraste")} {Formato.Pesos(t.Neto)} netos ({Formato.Pesos(t.Total)} con IVA) en {t.Cantidad} {(t.Cantidad == 1 ? "comprobante" : "comprobantes")}.",
                true, pregunta,
                new("Neto", Formato.Pesos(t.Neto)), new("IVA", Formato.Pesos(t.Iva)), new("Total", Formato.Pesos(t.Total)), new("Comprobantes", t.Cantidad.ToString(CultureInfo.InvariantCulture)));
        }

        private async Task<RespuestaDelAsistente> ResultadoAsync(Periodo p, string pregunta, CancellationToken cancellationToken)
        {
            var ventas = await TotalesAsync("Venta", p, cancellationToken);
            var compras = await TotalesAsync("Compra", p, cancellationToken);
            var resultado = ventas.Neto - compras.Neto;
            var texto = ventas.Cantidad == 0 && compras.Cantidad == 0
                ? $"{Cuando(p)} no hay movimientos cargados."
                : resultado >= 0
                    ? $"{Cuando(p)} el resultado es una ganancia de {Formato.Pesos(resultado)} (ventas netas menos compras netas)."
                    : $"{Cuando(p)} el resultado es una pérdida de {Formato.Pesos(-resultado)}: se compró más de lo que se vendió.";
            return Respuesta(texto, true, pregunta,
                new("Ventas netas", Formato.Pesos(ventas.Neto)), new("Compras netas", Formato.Pesos(compras.Neto)), new("Resultado", Formato.Pesos(resultado)));
        }

        private async Task<RespuestaDelAsistente> IvaAsync(Periodo p, string pregunta, CancellationToken cancellationToken)
        {
            var ventas = await TotalesAsync("Venta", p, cancellationToken);
            var compras = await TotalesAsync("Compra", p, cancellationToken);
            var aPagar = ventas.Iva - compras.Iva;
            return Respuesta(
                aPagar >= 0
                    ? $"{Cuando(p)} te corresponde pagar {Formato.Pesos(aPagar)} de IVA (débito fiscal menos crédito fiscal)."
                    : $"{Cuando(p)} tenés {Formato.Pesos(-aPagar)} de IVA a favor (el crédito fiscal supera al débito).",
                true, pregunta,
                new("IVA de ventas (débito fiscal)", Formato.Pesos(ventas.Iva)), new("IVA de compras (crédito fiscal)", Formato.Pesos(compras.Iva)),
                new(aPagar >= 0 ? "IVA a pagar" : "IVA a favor", Formato.Pesos(Math.Abs(aPagar))));
        }

        private async Task<RespuestaDelAsistente> AnuladasAsync(Periodo p, string pregunta, CancellationToken cancellationToken)
        {
            var desde = Utc(p.Desde);
            var hasta = Utc(p.Hasta);
            var n = await _context.Facturas.AsNoTracking().CountAsync(f => f.Anulada && f.Fecha >= desde && f.Fecha < hasta, cancellationToken);
            return Respuesta(n == 0 ? $"{Cuando(p)} no hay facturas anuladas." : $"{Cuando(p)} hay {n} {(n == 1 ? "factura anulada" : "facturas anuladas")}; no suman en los totales.", true, pregunta);
        }

        private async Task<RespuestaDelAsistente> ProductosMasVendidosAsync(Periodo p, string pregunta, CancellationToken cancellationToken)
        {
            var desde = Utc(p.Desde);
            var hasta = Utc(p.Hasta);
            var top = await (from i in _context.FacturaItems.AsNoTracking()
                             join f in _context.Facturas.AsNoTracking() on i.FacturaId equals f.Id
                             where !f.Anulada && f.TipoOperacion == "Venta" && i.ProductoId != null && f.Fecha >= desde && f.Fecha < hasta
                             group i by i.ProductoId into g
                             select new { ProductoId = g.Key!.Value, Cantidad = g.Sum(x => x.Cantidad), Total = g.Sum(x => x.Subtotal) })
                .OrderByDescending(x => x.Total).ThenBy(x => x.ProductoId).Take(5).ToListAsync(cancellationToken);
            if (top.Count == 0)
                return Respuesta($"{Cuando(p)} no se vendieron productos del inventario.", true, pregunta);

            var ids = top.Select(x => x.ProductoId).ToList();
            var nombres = await _context.Productos.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Nombre, cancellationToken);
            var filas = top.Select(x => new FilaDeRespuesta(nombres.GetValueOrDefault(x.ProductoId) ?? "(producto eliminado)", $"{Formato.Pesos(x.Total)} ({x.Cantidad} u.)")).ToArray();
            return Respuesta($"El producto más vendido en {p.Etiqueta} es {filas[0].Etiqueta}, con {filas[0].Valor}.", true, pregunta, filas);
        }

        // --- Caja, banco, deudas e integridad -------------------------------------------------------------------

        private async Task<RespuestaDelAsistente> TesoreriaAsync(bool caja, bool banco, string pregunta, CancellationToken cancellationToken)
        {
            var clienteId = PlanDeCuentas.ClienteIdPorDefecto.ToString();
            var filas = new List<FilaDeRespuesta>();
            var partes = new List<string>();
            if (caja)
            {
                var saldo = await _context.MovimientosCaja.AsNoTracking().Where(m => m.ClienteId == clienteId)
                    .SumAsync(m => m.TipoMovimiento == TipoMovimientoCaja.Ingreso ? m.Importe : -m.Importe, cancellationToken);
                filas.Add(new("Saldo de Caja", Formato.Pesos(saldo)));
                partes.Add($"en la Caja hay {Formato.Pesos(saldo)}{(saldo < 0 ? " (está en negativo: revisá los movimientos)" : "")}");
            }
            if (banco)
            {
                var saldo = await _context.MovimientosBanco.AsNoTracking().Where(m => m.ClienteId == clienteId)
                    .SumAsync(m => m.TipoMovimiento == TipoMovimientoBanco.Ingreso ? m.Importe : -m.Importe, cancellationToken);
                filas.Add(new("Saldo de Banco", Formato.Pesos(saldo)));
                partes.Add($"en el Banco hay {Formato.Pesos(saldo)}{(saldo < 0 ? " (está en negativo)" : "")}");
            }
            var texto = string.Join(" y ", partes);
            return Respuesta(char.ToUpper(texto[0], CultureInfo.InvariantCulture) + texto[1..] + ".", true, pregunta, filas.ToArray());
        }

        private static readonly HashSet<string> PalabrasDeDeuda = new()
        {
            "debe", "deben", "debo", "debemos", "deuda", "deudor", "deudores", "pagar", "cobrar", "saldo", "adeuda", "adeudan", "pendiente", "pendientes",
            "cliente", "clientes", "proveedor", "proveedores", "plata", "dinero", "total", "todos", "todo", "cuanto", "cuanta", "quien", "quienes", "mas",
            "le", "les", "me", "te", "nos", "al", "a", "hoy", "ayer", "mes", "pasado", "este", "esta",
        };

        /// <summary>Si la pregunta nombra a un cliente o proveedor ("¿cuánto me debe Ferretería Sur?"), el que se parece; si no, null.</summary>
        private static List<TerceroConSaldo> TercerosNombrados(string q, IReadOnlyList<TerceroConSaldo> terceros)
        {
            var terminos = q.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 3 && !PalabrasDeDeuda.Contains(t) && !PalabrasSinValor.Contains(t)).ToList();
            if (terminos.Count == 0) return new();
            return terceros.Where(t => terminos.All(x => Lenguaje.Normalizar(t.Nombre).Contains(x, StringComparison.Ordinal))).Take(5).ToList();
        }

        private async Task<RespuestaDelAsistente> DeudasAsync(bool clientes, string q, DateOnly hoy, string pregunta, CancellationToken cancellationToken)
        {
            var l = await _cuentas.ListadoAsync(clientes, hoy, cancellationToken);

            // ¿Pregunta por uno en particular?
            var nombrados = TercerosNombrados(q, l.Terceros);
            if (nombrados.Count == 1)
            {
                var t = nombrados[0];
                var antigua = t.FacturasPendientes > 0 ? $" ({t.FacturasPendientes} {(t.FacturasPendientes == 1 ? "factura pendiente" : "facturas pendientes")}; la más vieja es de hace {t.DiasMasAntigua} días)" : string.Empty;
                var frase = t.Saldo > 0
                    ? (clientes ? $"{t.Nombre} te debe {Formato.Pesos(t.Saldo)}{antigua}." : $"Le debés {Formato.Pesos(t.Saldo)} a {t.Nombre}{antigua}.")
                    : (clientes ? $"{t.Nombre} no te debe nada" : $"No le debés nada a {t.Nombre}") + (t.AFavor > 0 ? $"; tiene {Formato.Pesos(t.AFavor)} a favor." : ".");
                return Respuesta(frase, true, pregunta,
                    new("Facturado en cuenta corriente", Formato.Pesos(t.Facturado)), new(clientes ? "Cobrado" : "Pagado", Formato.Pesos(t.Pagado)), new("Saldo", Formato.Pesos(t.Saldo)));
            }
            if (nombrados.Count > 1)
                return Respuesta($"Encontré {nombrados.Count} {(clientes ? "clientes" : "proveedores")} que se parecen; decime cuál:", true, pregunta,
                    nombrados.Select(t => new FilaDeRespuesta(t.Nombre, Formato.Pesos(t.Saldo))).ToArray());

            var conDeuda = l.Terceros.Where(t => t.Saldo > 0).ToList();
            if (conDeuda.Count == 0)
                return Respuesta(clientes ? "Ningún cliente te debe nada en cuenta corriente." : "No le debés nada a ningún proveedor.", true, pregunta);

            var viejas = clientes ? l.Antiguedad.Mas90 : l.Antiguedad.De61A90 + l.Antiguedad.Mas90;
            var texto = clientes
                ? $"Los clientes te deben {Formato.Pesos(l.Saldo)} en total ({conDeuda.Count} {(conDeuda.Count == 1 ? "cliente" : "clientes")})."
                : $"Les debés a los proveedores {Formato.Pesos(l.Saldo)} en total ({conDeuda.Count} {(conDeuda.Count == 1 ? "proveedor" : "proveedores")}).";
            if (viejas > 0)
                texto += clientes ? $" De eso, {Formato.Pesos(viejas)} tiene más de 90 días." : $" De eso, {Formato.Pesos(viejas)} tiene más de 60 días.";
            texto += $" Los que más {(clientes ? "deben" : "se les debe")}:";
            return Respuesta(texto, true, pregunta, conDeuda.Take(5).Select(t => new FilaDeRespuesta(t.Nombre, Formato.Pesos(t.Saldo))).ToArray());
        }

        private async Task<RespuestaDelAsistente> IntegridadAsync(string pregunta, CancellationToken cancellationToken)
        {
            var r = await _integridad.EjecutarAsync(cancellationToken);
            if (r.Correctos == r.Total)
                return Respuesta($"Sí, todo en orden: los {r.Total} controles de integridad dan correcto (partida doble, Caja y Banco contra el Mayor, IVA, cuentas corrientes y stock).", true, pregunta);

            var mal = r.Controles.Where(c => !c.Correcto).ToList();
            return Respuesta($"Atención: {mal.Count} de {r.Total} controles encontraron diferencias. Revisalos en Contabilidad, Control de integridad:", true, pregunta,
                mal.Take(MaximoDeFilas).Select(c => new FilaDeRespuesta(c.Titulo,
                    c.EsImporte ? $"diferencia de {Formato.Pesos(Math.Abs(c.Obtenido - c.Esperado))}" : $"{c.Obtenido.ToString("0", CultureInfo.InvariantCulture)} caso(s)")).ToArray());
        }

        // --- Socios, cuotas y rutinas ---------------------------------------------------------------------------

        private enum Tipo { PorVencer, Vencidos, SinRutina, RutinasVencidas, Cantidad }

        private async Task<RespuestaDelAsistente> SociosAsync(Tipo tipo, bool veFinanzas, DateOnly hoy, string pregunta, CancellationToken cancellationToken)
        {
            var socios = (await _context.Socios.AsNoTracking().Where(s => s.Activo)
                    .Select(s => new { s.Id, s.Nombre, s.Apellido, s.FechaVencimiento }).ToListAsync(cancellationToken))
                .Select(s => new { s.Id, Nombre = $"{s.Apellido}, {s.Nombre}", Vence = s.FechaVencimiento is DateTime f ? DateOnly.FromDateTime(f) : (DateOnly?)null })
                .ToList();
            var vigentes = socios.Where(s => s.Vence is DateOnly v && v >= hoy).ToList();

            static string Fecha(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            RespuestaDelAsistente Lista(string vacio, string encabezado, IReadOnlyList<FilaDeRespuesta> filas)
                => filas.Count == 0
                    ? Respuesta(vacio, veFinanzas, pregunta)
                    : Respuesta($"{encabezado}{Mas(filas.Count)}", veFinanzas, pregunta, filas.Take(MaximoDeFilas).ToArray());

            switch (tipo)
            {
                case Tipo.PorVencer:
                {
                    var lista = vigentes.Where(s => s.Vence!.Value <= hoy.AddDays(DiasPorVencer)).OrderBy(s => s.Vence).ToList();
                    return Lista($"No hay cuotas que venzan en los próximos {DiasPorVencer} días.",
                        $"{lista.Count} {(lista.Count == 1 ? "cuota vence" : "cuotas vencen")} en los próximos {DiasPorVencer} días (podés avisarles por WhatsApp desde Recepción):",
                        lista.Select(s => new FilaDeRespuesta(s.Nombre, $"vence el {Fecha(s.Vence!.Value)}")).ToList());
                }
                case Tipo.Vencidos:
                {
                    var lista = socios.Where(s => s.Vence is DateOnly v && v < hoy).OrderBy(s => s.Vence).ToList();
                    return Lista("No hay socios con la cuota vencida.",
                        $"{lista.Count} {(lista.Count == 1 ? "socio tiene" : "socios tienen")} la cuota vencida:",
                        lista.Select(s => new FilaDeRespuesta(s.Nombre, $"venció el {Fecha(s.Vence!.Value)}")).ToList());
                }
                case Tipo.SinRutina:
                {
                    var conRutina = (await _context.AsignacionesRutina.AsNoTracking().Where(a => a.Activa).Select(a => a.SocioId).ToListAsync(cancellationToken)).ToHashSet();
                    var lista = vigentes.Where(s => !conRutina.Contains(s.Id)).OrderBy(s => s.Nombre).ToList();
                    return Lista("Todos los socios con la cuota al día tienen una rutina asignada.",
                        $"{lista.Count} {(lista.Count == 1 ? "socio al día no tiene" : "socios al día no tienen")} rutina asignada:",
                        lista.Select(s => new FilaDeRespuesta(s.Nombre, "sin rutina")).ToList());
                }
                case Tipo.RutinasVencidas:
                {
                    var porSocio = socios.ToDictionary(s => s.Id, s => s.Nombre);
                    var vencidas = await (from a in _context.AsignacionesRutina.AsNoTracking()
                                          where a.Activa && a.FechaFin != null && a.FechaFin < hoy
                                          select new { a.SocioId, Rutina = a.Rutina!.Nombre, Fin = a.FechaFin!.Value }).ToListAsync(cancellationToken);
                    return Lista("No hay rutinas vencidas.",
                        $"{vencidas.Count} {(vencidas.Count == 1 ? "rutina vencida" : "rutinas vencidas")} (hay que renovarlas):",
                        vencidas.OrderBy(v => v.Fin).Select(v => new FilaDeRespuesta(porSocio.GetValueOrDefault(v.SocioId) ?? $"Socio {v.SocioId}", $"{v.Rutina}, venció el {Fecha(v.Fin)}")).ToList());
                }
                default:
                {
                    var vencidos = socios.Count(s => s.Vence is DateOnly v && v < hoy);
                    var sinPlan = socios.Count(s => s.Vence is null);
                    return Respuesta($"Hay {socios.Count} {(socios.Count == 1 ? "socio activo" : "socios activos")}: {vigentes.Count} con la cuota al día, {vencidos} con la cuota vencida y {sinPlan} sin plan todavía.",
                        veFinanzas, pregunta,
                        new("Con cuota al día", vigentes.Count.ToString(CultureInfo.InvariantCulture)), new("Cuota vencida", vencidos.ToString(CultureInfo.InvariantCulture)),
                        new("Sin plan", sinPlan.ToString(CultureInfo.InvariantCulture)));
                }
            }
        }

        // --- Stock ----------------------------------------------------------------------------------------------

        private static readonly HashSet<string> PalabrasSinValor = new()
        {
            "stock", "de", "del", "la", "el", "los", "las", "un", "una", "cuanto", "cuanta", "cuantos", "cuantas", "hay", "queda", "quedan", "tengo", "tenemos",
            "que", "en", "inventario", "existencia", "existencias", "producto", "productos", "unidades", "unidad", "me", "dame", "decime", "dime", "y", "tenes",
            "sin", "poco", "pocos", "bajo", "agotado", "agotados", "agotar", "agotarse", "por", "estan", "esta", "tiene", "tienen", "cual", "cuales", "con",
        };

        private async Task<RespuestaDelAsistente> StockAsync(string q, bool veFinanzas, string pregunta, CancellationToken cancellationToken)
        {
            var productos = await _context.Productos.AsNoTracking().OrderBy(p => p.Nombre).Select(p => new { p.Nombre, p.StockActual }).ToListAsync(cancellationToken);

            // ¿Pregunta por un producto en particular? Las palabras que quedan después de sacar las de relleno.
            var terminos = q.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => !PalabrasSinValor.Contains(t)).ToList();
            if (terminos.Count > 0)
            {
                var encontrados = productos.Where(p => terminos.All(t => Lenguaje.Normalizar(p.Nombre).Contains(t, StringComparison.Ordinal))).Take(5).ToList();
                if (encontrados.Count == 0)
                    return Respuesta($"No encontré productos que se parezcan a \"{string.Join(' ', terminos)}\".", veFinanzas, pregunta);
                return Respuesta(
                    encontrados.Count == 1
                        ? $"De {encontrados[0].Nombre} hay {encontrados[0].StockActual} {(encontrados[0].StockActual == 1 ? "unidad" : "unidades")} en stock."
                        : $"Encontré {encontrados.Count} productos:",
                    veFinanzas, pregunta, encontrados.Select(p => new FilaDeRespuesta(p.Nombre, $"{p.StockActual} u.")).ToArray());
            }

            var sinStock = productos.Where(p => p.StockActual <= 0).ToList();
            var pocoStock = productos.Where(p => p.StockActual > 0 && p.StockActual <= StockBajo).OrderBy(p => p.StockActual).ToList();
            if (Lenguaje.Tiene(q, "sin", "agotad") && !Lenguaje.Tiene(q, "poco", "bajo"))
                return sinStock.Count == 0
                    ? Respuesta("Ningún producto está sin stock.", veFinanzas, pregunta)
                    : Respuesta($"{sinStock.Count} {(sinStock.Count == 1 ? "producto está sin stock" : "productos están sin stock")}{Mas(sinStock.Count)}:", veFinanzas, pregunta,
                        sinStock.Take(MaximoDeFilas).Select(p => new FilaDeRespuesta(p.Nombre, "sin stock")).ToArray());

            var alertas = sinStock.Concat(pocoStock).ToList();
            if (alertas.Count == 0)
                return Respuesta($"Ningún producto tiene poco stock (el aviso es desde {StockBajo} unidades o menos).", veFinanzas, pregunta);
            return Respuesta($"{sinStock.Count} sin stock y {pocoStock.Count} con poco stock (hasta {StockBajo} unidades){Mas(alertas.Count)}:", veFinanzas, pregunta,
                alertas.Take(MaximoDeFilas).Select(p => new FilaDeRespuesta(p.Nombre, p.StockActual <= 0 ? "sin stock" : $"{p.StockActual} u.")).ToArray());
        }

        // --- Ingresos al gimnasio, ficha de un socio, planes y socios nuevos ----------------------------------------

        private static string FechaCorta(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        /// <summary>"Hoy", "Ayer", "Esta semana" y "La semana pasada" solos; para los demás períodos, "En este mes (octubre de 2026)", "En marzo de 2026"...</summary>
        private static string Cuando(Periodo p)
            => p.Etiqueta is "hoy" or "ayer" or "esta semana" or "la semana pasada"
                ? char.ToUpper(p.Etiqueta[0], CultureInfo.InvariantCulture) + p.Etiqueta[1..]
                : $"En {p.Etiqueta}";

        private async Task<RespuestaDelAsistente> SociosEnRiesgoAsync(int dias, bool veFinanzas, DateOnly hoy, string pregunta, CancellationToken cancellationToken)
        {
            var lista = await _asistencias.SociosEnRiesgoAsync(hoy, dias, cancellationToken);
            if (lista.Count == 0)
                return Respuesta($"Ningún socio con la cuota al día lleva {dias} días o más sin venir.", veFinanzas, pregunta);

            return Respuesta(
                $"{lista.Count} {(lista.Count == 1 ? "socio al día no viene" : "socios al día no vienen")} hace {dias} días o más{Mas(lista.Count)} (podés escribirles por WhatsApp desde Recepción):",
                veFinanzas, pregunta,
                lista.Take(MaximoDeFilas).Select(s => new FilaDeRespuesta($"{s.Apellido}, {s.Nombre}",
                    s.UltimaVisita is DateOnly u ? $"{s.DiasSinVenir} días sin venir (última visita {FechaCorta(u)})" : $"todavía no vino ({s.DiasSinVenir} días desde que se anotó)")).ToArray());
        }

        private async Task<RespuestaDelAsistente> VisitasAsync(Periodo p, bool veFinanzas, string pregunta, CancellationToken cancellationToken)
        {
            var visitas = await (from a in _context.AsistenciasSocio.AsNoTracking()
                                 join s in _context.Socios.AsNoTracking() on a.SocioId equals s.Id
                                 where a.Fecha >= p.Desde && a.Fecha < p.Hasta
                                 orderby a.RegistradaEn descending
                                 select new { a.SocioId, s.Apellido, s.Nombre, a.RegistradaEn }).ToListAsync(cancellationToken);
            if (visitas.Count == 0)
                return Respuesta($"{Cuando(p)} no se registró ningún ingreso.", veFinanzas, pregunta);

            var distintos = visitas.Select(v => v.SocioId).Distinct().Count();
            var resumen = $"{Cuando(p)} hubo {visitas.Count} {(visitas.Count == 1 ? "ingreso" : "ingresos")} de {distintos} {(distintos == 1 ? "socio" : "socios")}";

            // Un solo día: quién entró y a qué hora. Varios días: los que más vinieron.
            if (p.Hasta.DayNumber - p.Desde.DayNumber == 1)
                return Respuesta($"{resumen}{Mas(visitas.Count)}:", veFinanzas, pregunta,
                    visitas.Take(MaximoDeFilas).Select(v => new FilaDeRespuesta($"{v.Apellido}, {v.Nombre}", v.RegistradaEn.AddHours(-3).ToString("HH:mm", CultureInfo.InvariantCulture))).ToArray());

            var frecuentes = visitas.GroupBy(v => new { v.SocioId, v.Apellido, v.Nombre })
                .Select(g => new { Nombre = $"{g.Key.Apellido}, {g.Key.Nombre}", Veces = g.Count() })
                .OrderByDescending(x => x.Veces).ThenBy(x => x.Nombre).Take(5).ToList();
            return Respuesta($"{resumen}. Los que más vinieron:", veFinanzas, pregunta,
                frecuentes.Select(x => new FilaDeRespuesta(x.Nombre, $"{x.Veces} {(x.Veces == 1 ? "vez" : "veces")}")).ToArray());
        }

        private static readonly HashSet<string> NombreDeSocioDemasiadoComun = new() { "de", "la", "del", "los", "las", "san", "santa" };

        /// <summary>Verdadero si todas las palabras del nombre (las de 3 letras o más) están en la pregunta.</summary>
        private static bool Nombrado(HashSet<string> palabrasDeLaPregunta, string nombre)
        {
            var partes = Lenguaje.Normalizar(nombre).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length >= 3 && !NombreDeSocioDemasiadoComun.Contains(t)).ToList();
            return partes.Count > 0 && partes.All(palabrasDeLaPregunta.Contains);
        }

        /// <summary>
        /// Si la pregunta nombra a un socio por su DNI, su apellido o su nombre ("¿cuándo vence la cuota de Gimnez?")
        /// contesta con su ficha: cuota, plan, rutina y visitas. Si no nombra a nadie devuelve null y sigue el resto de las reglas.
        /// </summary>
        private async Task<RespuestaDelAsistente?> FichaDeSocioAsync(string q, bool veFinanzas, DateOnly hoy, string pregunta, CancellationToken cancellationToken)
        {
            var palabras = q.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var dni = palabras.FirstOrDefault(p => p.Length is 7 or 8 && p.All(char.IsDigit));
            var socios = await _context.Socios.AsNoTracking()
                .Select(s => new { s.Id, s.Nombre, s.Apellido, s.Dni, s.Activo, s.FechaVencimiento, s.PlanActualId })
                .ToListAsync(cancellationToken);

            var coincidencias = socios.Where(s => dni is not null && s.Dni == dni).ToList();
            if (dni is not null && coincidencias.Count == 0)
                return Respuesta($"No encontré ningún socio con el DNI {dni}.", veFinanzas, pregunta);
            if (dni is null)
            {
                coincidencias = socios.Where(s => Nombrado(palabras, s.Apellido)).ToList();
                if (coincidencias.Count > 1)
                {
                    var afinadas = coincidencias.Where(s => Nombrado(palabras, s.Nombre)).ToList();
                    if (afinadas.Count > 0) coincidencias = afinadas;
                }
                if (coincidencias.Count == 0)
                    coincidencias = socios.Where(s => Nombrado(palabras, s.Nombre)).ToList();
            }
            if (coincidencias.Count == 0) return null;

            if (coincidencias.Count > 1)
                return Respuesta($"Encontré {coincidencias.Count} socios que se parecen; escribí el DNI de uno para ver su ficha:", veFinanzas, pregunta,
                    coincidencias.Take(MaximoDeFilas).Select(s => new FilaDeRespuesta($"{s.Apellido}, {s.Nombre}", $"DNI {s.Dni}")).ToArray());

            var socio = coincidencias[0];
            var vence = socio.FechaVencimiento is DateTime f ? DateOnly.FromDateTime(f) : (DateOnly?)null;
            var dias = vence is DateOnly v ? v.DayNumber - hoy.DayNumber : 0;
            var cuota = ReglasDeAsistencia.CalcularEstado(socio.FechaVencimiento, hoy) switch
            {
                EstadoDeCuota.AlDia => $"tiene la cuota al día, vence el {FechaCorta(vence!.Value)}",
                EstadoDeCuota.PorVencer => dias == 0 ? "tiene la cuota por vencer: vence hoy" : dias == 1 ? "tiene la cuota por vencer: vence mañana" : $"tiene la cuota por vencer: vence el {FechaCorta(vence!.Value)} (en {dias} días)",
                EstadoDeCuota.Vencida => $"tiene la cuota vencida desde el {FechaCorta(vence!.Value)}",
                _ => "todavía no pagó ninguna cuota",
            };

            var plan = socio.PlanActualId is long planId ? await _context.Planes.AsNoTracking().Where(p => p.Id == planId).Select(p => p.Nombre).FirstOrDefaultAsync(cancellationToken) : null;
            var rutina = await _context.AsignacionesRutina.AsNoTracking().Where(a => a.SocioId == socio.Id && a.Activa)
                .Select(a => new { Nombre = a.Rutina!.Nombre, a.FechaFin }).FirstOrDefaultAsync(cancellationToken);
            var ultima = await _context.AsistenciasSocio.AsNoTracking().Where(a => a.SocioId == socio.Id).MaxAsync(a => (DateOnly?)a.Fecha, cancellationToken);
            var primeroDelMes = new DateOnly(hoy.Year, hoy.Month, 1);
            var delMes = await _context.AsistenciasSocio.AsNoTracking().CountAsync(a => a.SocioId == socio.Id && a.Fecha >= primeroDelMes && a.Fecha <= hoy, cancellationToken);

            var filas = new List<FilaDeRespuesta> { new("DNI", socio.Dni) };
            if (plan is not null) filas.Add(new("Plan", plan));
            filas.Add(new("Rutina", rutina is null ? "sin rutina asignada"
                : rutina.FechaFin is DateOnly fin ? (fin < hoy ? $"{rutina.Nombre}, venció el {FechaCorta(fin)}" : $"{rutina.Nombre}, hasta el {FechaCorta(fin)}") : rutina.Nombre));
            filas.Add(new("Última visita", ultima is DateOnly u
                ? (u == hoy ? "hoy" : $"{FechaCorta(u)} (hace {hoy.DayNumber - u.DayNumber} días)") : "todavía no vino"));
            filas.Add(new("Visitas este mes", delMes.ToString(CultureInfo.InvariantCulture)));

            return Respuesta($"{socio.Apellido}, {socio.Nombre}{(socio.Activo ? string.Empty : " (dado de baja)")} {cuota}.", veFinanzas, pregunta, filas.ToArray());
        }

        private async Task<RespuestaDelAsistente> PlanesAsync(bool veFinanzas, string pregunta, CancellationToken cancellationToken)
        {
            var planes = await _context.Planes.AsNoTracking().Where(p => p.Activo).OrderBy(p => p.PrecioMensual).ThenBy(p => p.Nombre).ToListAsync(cancellationToken);
            if (planes.Count == 0)
                return Respuesta("Todavía no hay planes cargados: se crean desde el menú Planes.", veFinanzas, pregunta);
            return Respuesta($"Hay {planes.Count} {(planes.Count == 1 ? "plan activo" : "planes activos")}{Mas(planes.Count)}:", veFinanzas, pregunta,
                planes.Take(MaximoDeFilas).Select(p => new FilaDeRespuesta(p.Nombre,
                    $"{Formato.Pesos(p.PrecioMensual)} por mes{(p.DuracionMeses > 1 ? $", dura {p.DuracionMeses} meses" : string.Empty)}")).ToArray());
        }

        private async Task<RespuestaDelAsistente> SociosNuevosAsync(Periodo p, bool veFinanzas, string pregunta, CancellationToken cancellationToken)
        {
            var desde = Utc(p.Desde);
            var hasta = Utc(p.Hasta);
            var nuevos = await _context.Socios.AsNoTracking().Where(s => s.FechaAlta >= desde && s.FechaAlta < hasta)
                .OrderByDescending(s => s.FechaAlta).Select(s => new { s.Apellido, s.Nombre, s.FechaAlta }).ToListAsync(cancellationToken);
            if (nuevos.Count == 0)
                return Respuesta($"{Cuando(p)} no se anotó ningún socio nuevo.", veFinanzas, pregunta);
            return Respuesta($"{Cuando(p)} {(nuevos.Count == 1 ? "se anotó 1 socio nuevo" : $"se anotaron {nuevos.Count} socios nuevos")}{Mas(nuevos.Count)}:", veFinanzas, pregunta,
                nuevos.Take(MaximoDeFilas).Select(s => new FilaDeRespuesta($"{s.Apellido}, {s.Nombre}", FechaCorta(DateOnly.FromDateTime(s.FechaAlta)))).ToArray());
        }

        // --- Comparaciones ----------------------------------------------------------------------------------------

        private async Task<RespuestaDelAsistente> ComparacionAsync(string q, DateOnly hoy, string pregunta, CancellationToken cancellationToken)
        {
            var (actual, anterior) = Lenguaje.InterpretarComparacion(q, hoy);
            var quiereResultado = Lenguaje.Tiene(q, "resultado", "ganancia", "gane", "ganamos", "perdida");
            var quiereCompras = !quiereResultado && Lenguaje.Tiene(q, "compr", "gaste", "gasto", "gastamos") && !Lenguaje.Tiene(q, "vend", "venta", "ventas");

            async Task<decimal> ValorAsync(Periodo p)
            {
                if (quiereResultado) return (await TotalesAsync("Venta", p, cancellationToken)).Neto - (await TotalesAsync("Compra", p, cancellationToken)).Neto;
                return (await TotalesAsync(quiereCompras ? "Compra" : "Venta", p, cancellationToken)).Neto;
            }

            var ahora = await ValorAsync(actual);
            var antes = await ValorAsync(anterior);
            var que = quiereResultado ? "el resultado" : quiereCompras ? "las compras netas" : "las ventas netas";
            var Que = char.ToUpper(que[0], CultureInfo.InvariantCulture) + que[1..];

            if (ahora == 0 && antes == 0)
                return Respuesta($"En {actual.Etiqueta} ni en {anterior.Etiqueta} hay movimientos para comparar.", true, pregunta);

            var diferencia = ahora - antes;
            var variacion = diferencia == 0 ? "No hubo ningún cambio."
                : antes == 0 ? $"En {anterior.Etiqueta} no había nada, así que no se puede calcular el porcentaje."
                : $"Es {Formato.Pesos(Math.Abs(diferencia))} {(diferencia > 0 ? "más" : "menos")} ({(diferencia > 0 ? "+" : "-")}{Math.Abs(Math.Round(diferencia / Math.Abs(antes) * 100m, 0)).ToString("0", CultureInfo.InvariantCulture)} %).";
            return Respuesta($"{Que} de {actual.Etiqueta}: {Formato.Pesos(ahora)}; de {anterior.Etiqueta}: {Formato.Pesos(antes)}. {variacion}", true, pregunta,
                new(char.ToUpper(actual.Etiqueta[0], CultureInfo.InvariantCulture) + actual.Etiqueta[1..], Formato.Pesos(ahora)),
                new(char.ToUpper(anterior.Etiqueta[0], CultureInfo.InvariantCulture) + anterior.Etiqueta[1..], Formato.Pesos(antes)),
                new("Diferencia", Formato.Pesos(diferencia)));
        }

        // --- Resumen general --------------------------------------------------------------------------------------

        private async Task<RespuestaDelAsistente> ResumenAsync(bool veFinanzas, DateOnly hoy, Periodo periodo, string pregunta, CancellationToken cancellationToken)
        {
            var filas = new List<FilaDeRespuesta>();
            if (veFinanzas)
            {
                var ventas = await TotalesAsync("Venta", periodo, cancellationToken);
                var compras = await TotalesAsync("Compra", periodo, cancellationToken);
                filas.Add(new($"Ventas netas ({periodo.Etiqueta})", Formato.Pesos(ventas.Neto)));
                filas.Add(new("Compras netas", Formato.Pesos(compras.Neto)));
                filas.Add(new("Resultado", Formato.Pesos(ventas.Neto - compras.Neto)));
                var clienteId = PlanDeCuentas.ClienteIdPorDefecto.ToString();
                filas.Add(new("Saldo de Caja", Formato.Pesos(await _context.MovimientosCaja.AsNoTracking().Where(m => m.ClienteId == clienteId)
                    .SumAsync(m => m.TipoMovimiento == TipoMovimientoCaja.Ingreso ? m.Importe : -m.Importe, cancellationToken))));
                filas.Add(new("Saldo de Banco", Formato.Pesos(await _context.MovimientosBanco.AsNoTracking().Where(m => m.ClienteId == clienteId)
                    .SumAsync(m => m.TipoMovimiento == TipoMovimientoBanco.Ingreso ? m.Importe : -m.Importe, cancellationToken))));
                filas.Add(new("A cobrar a clientes", Formato.Pesos((await _cuentas.ListadoAsync(true, hoy, cancellationToken)).Saldo)));
                filas.Add(new("A pagar a proveedores", Formato.Pesos((await _cuentas.ListadoAsync(false, hoy, cancellationToken)).Saldo)));
                var integridad = await _integridad.EjecutarAsync(cancellationToken);
                filas.Add(new("Contabilidad", integridad.Correctos == integridad.Total ? $"en orden ({integridad.Total} controles)" : $"{integridad.Total - integridad.Correctos} control(es) con diferencias"));
            }

            var inicio = Utc(hoy);
            var activos = _context.Socios.AsNoTracking().Where(s => s.Activo);
            filas.Add(new("Socios con cuota al día", (await activos.CountAsync(s => s.FechaVencimiento >= inicio, cancellationToken)).ToString(CultureInfo.InvariantCulture)));
            filas.Add(new($"Cuotas que vencen en {DiasPorVencer} días", (await activos.CountAsync(s => s.FechaVencimiento >= inicio && s.FechaVencimiento < inicio.AddDays(DiasPorVencer + 1), cancellationToken)).ToString(CultureInfo.InvariantCulture)));
            filas.Add(new("Productos sin stock o con poco stock", (await _context.Productos.AsNoTracking().CountAsync(p => p.StockActual <= StockBajo, cancellationToken)).ToString(CultureInfo.InvariantCulture)));

            return Respuesta(veFinanzas ? $"Así viene el negocio hoy ({Lenguaje.NombreDelMes(hoy.Month)} de {hoy.Year}):" : "Así viene el gimnasio hoy:", veFinanzas, pregunta, filas.ToArray());
        }
    }
}
