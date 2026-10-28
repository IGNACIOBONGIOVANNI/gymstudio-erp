import { useCallback, useEffect, useState } from "react";
import { AlertTriangle, CheckCircle2, Printer, X } from "lucide-react";
import toast from "react-hot-toast";
import { pedir } from "../../api/rutinasApi";
import { TEXTOS, type DetalleCuenta, type ListadoCuentas, type TerceroConSaldo, type TipoCuenta } from "../../api/cuentasCorrientesApi";
import { formatearPesos } from "../../api/dashboardApi";
import { formatearFechaLocal, hoyLocal } from "../../utils/formatters";
import { generarEstadoDeCuentaPDF, generarListadoCuentasPDF } from "../../utils/generarCuentaCorrientePDF";

const tarjeta = "rounded-xl border border-slate-200 bg-white p-5";
const celda = "h-11 px-3 text-right align-middle font-medium text-slate-500";
const monto = (n: number) => (n === 0 ? "-" : formatearPesos(n));

function Indicador({ etiqueta, valor, nota }: { etiqueta: string; valor: string; nota?: string }) {
  return (
    <div className={tarjeta}>
      <p className="text-sm text-slate-500">{etiqueta}</p>
      <p className="mt-1 text-2xl font-semibold tracking-tight text-slate-900">{valor}</p>
      {nota && <p className="mt-1 text-xs text-slate-500">{nota}</p>}
    </div>
  );
}

function ModalEstadoDeCuenta({ tipo, id, onClose }: { tipo: TipoCuenta; id: number; onClose: () => void }) {
  const t = TEXTOS[tipo];
  const [detalle, setDetalle] = useState<DetalleCuenta | null>(null);

  useEffect(() => {
    pedir<DetalleCuenta>(`/api/cuentas-corrientes/${tipo}/${id}?hoy=${hoyLocal()}`, "No se pudo cargar el estado de cuenta")
      .then(setDetalle)
      .catch((e: Error) => { toast.error(e.message); onClose(); });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tipo, id]);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 backdrop-blur-sm">
      <div className="flex max-h-[92vh] w-full max-w-3xl flex-col rounded-xl bg-white shadow-xl ring-1 ring-slate-900/5">
        <div className="flex items-start justify-between border-b border-slate-100 px-6 py-4">
          <div>
            <h2 className="text-lg font-semibold text-slate-900">Estado de cuenta{detalle ? `: ${detalle.tercero.nombre}` : ""}</h2>
            {detalle && <p className="text-sm text-slate-500">CUIT / DNI {detalle.tercero.cuit || "-"} · {detalle.tercero.condicionIva}</p>}
          </div>
          <div className="flex items-center gap-1">
            {detalle && (
              <button onClick={() => generarEstadoDeCuentaPDF(detalle, tipo)} className="mr-2 inline-flex items-center rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50">
                <Printer className="mr-1.5 h-4 w-4" /> Imprimir PDF
              </button>
            )}
            <button onClick={onClose} aria-label="Cerrar" className="rounded-full p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-500"><X className="h-5 w-5" /></button>
          </div>
        </div>

        {!detalle ? (
          <div className="p-10 text-center text-slate-500">Cargando...</div>
        ) : (
          <div className="space-y-5 overflow-y-auto p-6">
            <div className="grid gap-3 sm:grid-cols-3">
              <Indicador etiqueta="Facturado en cuenta corriente" valor={formatearPesos(detalle.facturado)} />
              <Indicador etiqueta={t.pagado} valor={formatearPesos(detalle.pagado)} />
              <Indicador etiqueta={detalle.saldo < 0 ? "Saldo a favor" : "Saldo"} valor={formatearPesos(Math.abs(detalle.saldo))} />
            </div>

            {detalle.pendientes.length > 0 && (
              <section>
                <h3 className="mb-2 text-sm font-semibold text-slate-800">Facturas pendientes</h3>
                <p className="mb-2 text-xs text-slate-500">Los {t.verbo} se aplican a las facturas más antiguas primero.</p>
                <div className="overflow-x-auto rounded-lg border border-slate-200">
                  <table className="w-full text-sm">
                    <thead className="bg-slate-50 text-slate-500"><tr>
                      <th className="px-3 py-2 text-left font-medium">Fecha</th><th className="px-3 py-2 text-left font-medium">Comprobante</th>
                      <th className="px-3 py-2 text-right font-medium">Total</th><th className="px-3 py-2 text-right font-medium">Pendiente</th><th className="px-3 py-2 text-right font-medium">Días</th>
                    </tr></thead>
                    <tbody>
                      {detalle.pendientes.map((p) => (
                        <tr key={p.id} className="border-t border-slate-100">
                          <td className="px-3 py-2">{formatearFechaLocal(p.fecha)}</td><td className="px-3 py-2">{p.descripcion}</td>
                          <td className="px-3 py-2 text-right">{formatearPesos(p.total)}</td>
                          <td className="px-3 py-2 text-right font-medium text-slate-900">{formatearPesos(p.pendiente)}</td>
                          <td className="px-3 py-2 text-right text-slate-600">{p.dias}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </section>
            )}

            <section>
              <h3 className="mb-2 text-sm font-semibold text-slate-800">Movimientos</h3>
              <div className="overflow-x-auto rounded-lg border border-slate-200">
                <table className="w-full text-sm">
                  <thead className="bg-slate-50 text-slate-500"><tr>
                    <th className="px-3 py-2 text-left font-medium">Fecha</th><th className="px-3 py-2 text-left font-medium">Detalle</th>
                    <th className="px-3 py-2 text-right font-medium">Debe (facturas)</th><th className="px-3 py-2 text-right font-medium">Haber ({t.verbo})</th><th className="px-3 py-2 text-right font-medium">Saldo</th>
                  </tr></thead>
                  <tbody>
                    {detalle.estado.length === 0 ? (
                      <tr><td colSpan={5} className="p-4 text-center text-slate-500">Sin movimientos en cuenta corriente.</td></tr>
                    ) : detalle.estado.map((f, i) => (
                      <tr key={i} className="border-t border-slate-100">
                        <td className="px-3 py-2">{formatearFechaLocal(f.fecha)}</td><td className="px-3 py-2">{f.descripcion}</td>
                        <td className="px-3 py-2 text-right">{f.debe ? formatearPesos(f.debe) : ""}</td>
                        <td className="px-3 py-2 text-right">{f.haber ? formatearPesos(f.haber) : ""}</td>
                        <td className="px-3 py-2 text-right font-medium text-slate-900">{formatearPesos(f.saldo)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          </div>
        )}
      </div>
    </div>
  );
}

export function CuentasCorrientes() {
  const [tipo, setTipo] = useState<TipoCuenta>("clientes");
  const [datos, setDatos] = useState<ListadoCuentas | null>(null);
  const [cargando, setCargando] = useState(true);
  const [abierto, setAbierto] = useState<number | null>(null);
  const t = TEXTOS[tipo];

  const cargar = useCallback(async () => {
    setCargando(true);
    try {
      setDatos(await pedir<ListadoCuentas>(`/api/cuentas-corrientes/${tipo}?hoy=${hoyLocal()}`, "No se pudieron cargar las cuentas"));
    } catch (error: any) {
      toast.error(error.message);
    } finally {
      setCargando(false);
    }
  }, [tipo]);

  useEffect(() => { cargar(); }, [cargar]);

  const pestana = (v: TipoCuenta) =>
    `-mb-px border-b-2 px-4 py-2 text-sm font-medium transition-colors ${tipo === v ? "border-marca-600 text-marca-700" : "border-transparent text-slate-500 hover:text-slate-700"}`;
  const vencido = datos ? datos.total.antiguedad.de31A60 + datos.total.antiguedad.de61A90 + datos.total.antiguedad.mas90 : 0;
  const coincide = datos ? Math.abs(datos.mayor.diferencia) < 0.005 : true;

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-slate-500">
          {tipo === "clientes"
            ? "Lo que te deben los clientes por ventas en cuenta corriente, con la antigüedad de cada deuda."
            : "Lo que le debés a los proveedores por compras en cuenta corriente, con la antigüedad de cada deuda."}
          {" "}Se descuentan los {t.verbo} cargados en Caja o Banco a nombre de cada uno.
        </p>
        {datos && datos.terceros.length > 0 && (
          <button onClick={() => generarListadoCuentasPDF(datos)} className="inline-flex items-center rounded-md bg-marca-600 px-3 py-2 text-sm font-medium text-white hover:bg-marca-700">
            <Printer className="mr-1.5 h-4 w-4" /> Imprimir listado
          </button>
        )}
      </div>

      <div className="flex border-b border-slate-200">
        <button className={pestana("clientes")} onClick={() => setTipo("clientes")}>A cobrar (clientes)</button>
        <button className={pestana("proveedores")} onClick={() => setTipo("proveedores")}>A pagar (proveedores)</button>
      </div>

      {cargando && !datos ? (
        <div className={`${tarjeta} text-center text-slate-500`}>Cargando...</div>
      ) : datos ? (
        <>
          <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <Indicador etiqueta={tipo === "clientes" ? "Total a cobrar" : "Total a pagar"} valor={formatearPesos(datos.total.saldo)} nota={`${datos.terceros.filter((x) => x.saldo > 0).length} ${t.terceros} con deuda`} />
            <Indicador etiqueta="Deuda de 0 a 30 días" valor={formatearPesos(datos.total.antiguedad.corriente)} />
            <Indicador etiqueta="Deuda de más de 30 días" valor={formatearPesos(vencido)} nota="Conviene reclamarla o pagarla" />
            <Indicador etiqueta="Saldo a favor" valor={formatearPesos(datos.total.aFavor)} nota={tipo === "clientes" ? "Clientes que pagaron de más" : "Proveedores a los que se pagó de más"} />
          </section>

          <div className={`flex items-start gap-2 rounded-lg border px-4 py-3 text-sm ${coincide ? "border-emerald-200 bg-emerald-50 text-emerald-800" : "border-amber-300 bg-amber-50 text-amber-900"}`} role="status">
            {coincide ? <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" aria-hidden /> : <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />}
            <p>
              {coincide
                ? `El total coincide con la cuenta ${datos.mayor.cuenta} del Libro Mayor (${formatearPesos(datos.mayor.saldo)}).`
                : `La cuenta ${datos.mayor.cuenta} del Libro Mayor muestra ${formatearPesos(datos.mayor.saldo)}: hay una diferencia de ${formatearPesos(datos.mayor.diferencia)}. Suele ser por ${t.verbo} cargados antes sin ${t.tercero.toLowerCase()} asignado (editalos en Caja o Banco) o por asientos manuales.`}
            </p>
          </div>

          <div className="overflow-x-auto rounded-md border border-slate-200 bg-white">
            <table className="w-full text-sm">
              <thead className="bg-slate-50/50">
                <tr className="border-b border-slate-200">
                  <th className="h-11 px-3 text-left align-middle font-medium text-slate-500">{t.tercero}</th>
                  <th className={celda}>Facturado</th><th className={celda}>{t.pagado}</th><th className={celda}>Saldo</th>
                  <th className={celda}>0 a 30 días</th><th className={celda}>31 a 60</th><th className={celda}>61 a 90</th><th className={celda}>Más de 90</th>
                  <th className="h-11 px-3" />
                </tr>
              </thead>
              <tbody>
                {datos.terceros.length === 0 ? (
                  <tr><td colSpan={9} className="p-8 text-center text-slate-500">No hay {t.terceros} con operaciones en cuenta corriente. Al facturar elegí "Cuenta corriente" como condición de pago.</td></tr>
                ) : datos.terceros.map((x: TerceroConSaldo) => (
                  <tr key={x.id ?? x.nombre} className="border-b border-slate-100 hover:bg-slate-50/50">
                    <td className="px-3 py-3 font-medium text-slate-900">{x.nombre}{x.cuit && <span className="block text-xs font-normal text-slate-500">{x.cuit}</span>}</td>
                    <td className="px-3 py-3 text-right text-slate-600">{monto(x.facturado)}</td>
                    <td className="px-3 py-3 text-right text-slate-600">{monto(x.pagado)}</td>
                    <td className="px-3 py-3 text-right font-semibold text-slate-900">{x.saldo < 0 ? `${formatearPesos(-x.saldo)} a favor` : monto(x.saldo)}</td>
                    <td className="px-3 py-3 text-right text-slate-600">{monto(x.antiguedad.corriente)}</td>
                    <td className="px-3 py-3 text-right text-slate-600">{monto(x.antiguedad.de31A60)}</td>
                    <td className="px-3 py-3 text-right text-slate-600">{monto(x.antiguedad.de61A90)}</td>
                    <td className="px-3 py-3 text-right text-slate-600">{monto(x.antiguedad.mas90)}</td>
                    <td className="px-3 py-3 text-right">
                      {x.id !== null && <button onClick={() => setAbierto(x.id)} className="text-sm font-medium text-marca-700 hover:text-marca-800">Estado de cuenta</button>}
                    </td>
                  </tr>
                ))}
              </tbody>
              {datos.terceros.length > 0 && (
                <tfoot>
                  <tr className="border-t-2 border-slate-300 bg-slate-50 font-semibold text-slate-900">
                    <td className="px-3 py-3">Totales</td>
                    <td className="px-3 py-3 text-right">{monto(datos.total.facturado)}</td><td className="px-3 py-3 text-right">{monto(datos.total.pagado)}</td>
                    <td className="px-3 py-3 text-right">{monto(datos.total.saldo)}</td>
                    <td className="px-3 py-3 text-right">{monto(datos.total.antiguedad.corriente)}</td><td className="px-3 py-3 text-right">{monto(datos.total.antiguedad.de31A60)}</td>
                    <td className="px-3 py-3 text-right">{monto(datos.total.antiguedad.de61A90)}</td><td className="px-3 py-3 text-right">{monto(datos.total.antiguedad.mas90)}</td>
                    <td />
                  </tr>
                </tfoot>
              )}
            </table>
          </div>
        </>
      ) : null}

      {abierto !== null && <ModalEstadoDeCuenta tipo={tipo} id={abierto} onClose={() => setAbierto(null)} />}
    </div>
  );
}
