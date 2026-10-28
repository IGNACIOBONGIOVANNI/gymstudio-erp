import { useCallback, useEffect, useState } from "react";
import { CheckCircle2, Loader2, RefreshCw, XCircle } from "lucide-react";
import toast from "react-hot-toast";
import { pedir } from "../../api/rutinasApi";
import { formatearPesos } from "../../api/dashboardApi";

interface Control {
  clave: string;
  titulo: string;
  explicacion: string;
  correcto: boolean;
  obtenido: number;
  esperado: number;
  etiquetaObtenido: string;
  etiquetaEsperado: string;
  esImporte: boolean;
}

interface Resultado {
  fecha: string;
  total: number;
  correctos: number;
  todoCorrecto: boolean;
  controles: Control[];
}

const valor = (c: Control, n: number) => (c.esImporte ? formatearPesos(n) : String(n));

export function ControlIntegridad() {
  const [datos, setDatos] = useState<Resultado | null>(null);
  const [verificando, setVerificando] = useState(true);

  const verificar = useCallback(async () => {
    setVerificando(true);
    try {
      setDatos(await pedir<Resultado>("/api/integridad", "No se pudo ejecutar el control de integridad"));
    } catch (error: any) {
      toast.error(error.message);
    } finally {
      setVerificando(false);
    }
  }, []);

  useEffect(() => { verificar(); }, [verificar]);

  const fallan = datos ? datos.total - datos.correctos : 0;

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <p className="max-w-2xl text-sm text-slate-500">
          El sistema se audita a sí mismo: cruza las facturas, los libros de Caja y Banco, el stock y el Libro Mayor,
          y comprueba que todo cierre. Si alguien tocara un dato por fuera de las reglas, acá se vería.
        </p>
        <button onClick={verificar} disabled={verificando}
          className="inline-flex items-center rounded-md bg-marca-600 px-4 py-2 text-sm font-medium text-white hover:bg-marca-700 disabled:opacity-60">
          {verificando ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-2 h-4 w-4" />}
          Verificar de nuevo
        </button>
      </div>

      {datos && (
        <>
          <div role="status" className={`flex items-center gap-3 rounded-xl border px-5 py-4 ${datos.todoCorrecto ? "border-emerald-200 bg-emerald-50 text-emerald-900" : "border-red-200 bg-red-50 text-red-900"}`}>
            {datos.todoCorrecto ? <CheckCircle2 className="h-7 w-7 shrink-0" aria-hidden /> : <XCircle className="h-7 w-7 shrink-0" aria-hidden />}
            <div>
              <p className="text-lg font-semibold">
                {datos.todoCorrecto ? "Todo en orden" : `Atención: ${fallan} ${fallan === 1 ? "control con diferencias" : "controles con diferencias"}`}
              </p>
              <p className="text-sm">
                {datos.correctos} de {datos.total} controles correctos · verificado a las {new Date(datos.fecha).toLocaleTimeString("es-AR", { hour: "2-digit", minute: "2-digit", second: "2-digit" })}
              </p>
            </div>
          </div>

          <ul className="divide-y divide-slate-100 overflow-hidden rounded-xl border border-slate-200 bg-white">
            {datos.controles.map((c) => (
              <li key={c.clave} className="flex items-start gap-3 px-5 py-4">
                {c.correcto
                  ? <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0 text-emerald-600" aria-label="Correcto" />
                  : <XCircle className="mt-0.5 h-5 w-5 shrink-0 text-red-600" aria-label="Con diferencias" />}
                <div className="min-w-0 flex-1">
                  <p className="font-medium text-slate-900">{c.titulo}</p>
                  <p className="text-sm text-slate-500">{c.explicacion}</p>
                  {!c.correcto && (
                    <p className="mt-1 text-sm font-medium text-red-700">
                      {c.esImporte
                        ? `Diferencia de ${formatearPesos(Math.abs(c.obtenido - c.esperado))}: ${c.etiquetaObtenido} ${formatearPesos(c.obtenido)} contra ${c.etiquetaEsperado} ${formatearPesos(c.esperado)}.`
                        : `${c.etiquetaObtenido}: ${c.obtenido} (debería ser 0).`}
                    </p>
                  )}
                </div>
                {c.correcto && (
                  <p className="shrink-0 text-right text-sm text-slate-600">
                    {c.esImporte
                      ? <><span className="block font-medium text-slate-900">{valor(c, c.obtenido)}</span><span className="block text-xs text-slate-500">{c.etiquetaObtenido} = {c.etiquetaEsperado}</span></>
                      : <><span className="block font-medium text-slate-900">0</span><span className="block text-xs text-slate-500">{c.etiquetaObtenido}</span></>}
                  </p>
                )}
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
}
