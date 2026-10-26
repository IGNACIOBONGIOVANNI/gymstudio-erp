import { useEffect, useState, Fragment } from 'react';
import { History, Loader2, RefreshCw, ChevronDown, ChevronRight } from 'lucide-react';
import toast from 'react-hot-toast';

// Registro generado por el trigger contabilidad.fn_auditar() en PostgreSQL.
interface RegistroAuditoria {
  id: number;
  fecha: string;
  usuario: string;
  tabla: string;
  operacion: 'INSERT' | 'UPDATE' | 'DELETE';
  registroId: string | null;
  camposModificados: string[];
  datosAnteriores: Record<string, unknown> | null;
  datosNuevos: Record<string, unknown> | null;
}

const OPERACIONES: Record<RegistroAuditoria['operacion'], { texto: string; clase: string }> = {
  INSERT: { texto: 'Alta', clase: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  UPDATE: { texto: 'Modificación', clase: 'bg-amber-50 text-amber-700 border-amber-200' },
  DELETE: { texto: 'Baja', clase: 'bg-red-50 text-red-700 border-red-200' },
};

// "contabilidad.Facturas" -> "Facturas"
const nombreTabla = (tabla: string) => tabla.split('.').pop() ?? tabla;

function mostrarValor(valor: unknown): string {
  if (valor === null || valor === undefined || valor === '') return '—';
  if (typeof valor === 'object') return JSON.stringify(valor);
  return String(valor);
}

async function leerJson<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const problem = await res.json().catch(() => null);
    throw new Error(problem?.detail ?? 'Error al consultar la auditoría');
  }
  return res.json();
}

export default function ModuloAuditoria() {
  const [registros, setRegistros] = useState<RegistroAuditoria[]>([]);
  const [tablas, setTablas] = useState<string[]>([]);
  const [tabla, setTabla] = useState('');
  const [usuario, setUsuario] = useState('');
  const [cargando, setCargando] = useState(false);
  const [expandido, setExpandido] = useState<number | null>(null);

  const cargar = async () => {
    setCargando(true);
    try {
      const params = new URLSearchParams({ cantidad: '200' });
      if (tabla) params.set('tabla', tabla);
      if (usuario.trim()) params.set('usuario', usuario.trim());
      setRegistros(await leerJson<RegistroAuditoria[]>(await fetch(`/api/auditoria?${params}`)));
    } catch (error: any) {
      toast.error(error?.message);
    } finally {
      setCargando(false);
    }
  };

  useEffect(() => {
    fetch('/api/auditoria/tablas').then((r) => leerJson<string[]>(r)).then(setTablas).catch(() => setTablas([]));
  }, []);

  useEffect(() => {
    cargar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tabla]);

  const detalleCambios = (r: RegistroAuditoria) => {
    if (r.operacion !== 'UPDATE') {
      const datos = r.datosNuevos ?? r.datosAnteriores ?? {};
      return Object.entries(datos).map(([campo, valor]) => (
        <div key={campo} className="flex gap-2">
          <span className="font-medium text-slate-600 min-w-[160px]">{campo}</span>
          <span className="text-slate-800 break-all">{mostrarValor(valor)}</span>
        </div>
      ));
    }
    return r.camposModificados.map((campo) => (
      <div key={campo} className="flex gap-2 flex-wrap">
        <span className="font-medium text-slate-600 min-w-[160px]">{campo}</span>
        {campo === 'Contraseña' ? (
          <span className="text-slate-800">(cambiada)</span>
        ) : (
          <>
            <span className="text-red-600 line-through break-all">{mostrarValor(r.datosAnteriores?.[campo])}</span>
            <span className="text-slate-400">→</span>
            <span className="text-emerald-700 break-all">{mostrarValor(r.datosNuevos?.[campo])}</span>
          </>
        )}
      </div>
    ));
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 bg-white p-4 sm:p-5 rounded-xl border border-slate-200/80 shadow-sm">
        <div className="flex items-center gap-3">
          <div className="p-2 bg-slate-100 rounded-lg text-slate-500">
            <History className="h-5 w-5" />
          </div>
          <div>
            <h2 className="text-lg font-bold text-slate-900">Auditoría</h2>
            <p className="text-xs sm:text-sm text-slate-500">
              Quién modificó qué y cuándo. Lo registra la base de datos con un trigger, sea cual sea el origen del cambio.
            </p>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <select
            value={tabla}
            onChange={(e) => setTabla(e.target.value)}
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          >
            <option value="">Todas las tablas</option>
            {tablas.map((t) => (
              <option key={t} value={t}>{nombreTabla(t)}</option>
            ))}
          </select>
          <input
            value={usuario}
            onChange={(e) => setUsuario(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && cargar()}
            placeholder="Filtrar por usuario"
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          />
          <button
            onClick={cargar}
            className="inline-flex items-center gap-1.5 rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white hover:bg-slate-800"
          >
            {cargando ? <Loader2 className="h-4 w-4 animate-spin" /> : <RefreshCw className="h-4 w-4" />}
            Actualizar
          </button>
        </div>
      </div>

      <div className="rounded-xl border border-slate-200/80 bg-white shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-slate-50/50">
              <tr className="border-b border-slate-200 text-left text-slate-500">
                <th className="h-11 px-4 font-medium w-8"></th>
                <th className="h-11 px-4 font-medium">Fecha</th>
                <th className="h-11 px-4 font-medium">Usuario</th>
                <th className="h-11 px-4 font-medium">Tabla</th>
                <th className="h-11 px-4 font-medium">Operación</th>
                <th className="h-11 px-4 font-medium">Registro</th>
                <th className="h-11 px-4 font-medium">Campos modificados</th>
              </tr>
            </thead>
            <tbody>
              {registros.length === 0 && (
                <tr>
                  <td colSpan={7} className="h-24 text-center text-slate-500">
                    {cargando ? 'Cargando…' : 'No hay registros de auditoría.'}
                  </td>
                </tr>
              )}
              {registros.map((r) => (
                <Fragment key={r.id}>
                  <tr
                    onClick={() => setExpandido(expandido === r.id ? null : r.id)}
                    className="border-b border-slate-100 hover:bg-slate-50/80 cursor-pointer"
                  >
                    <td className="px-4 py-3 text-slate-400">
                      {expandido === r.id ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
                    </td>
                    <td className="px-4 py-3 tabular-nums text-slate-700 whitespace-nowrap">
                      {new Date(r.fecha).toLocaleString('es-AR', { dateStyle: 'short', timeStyle: 'medium' })}
                    </td>
                    <td className="px-4 py-3 text-slate-700">{r.usuario}</td>
                    <td className="px-4 py-3 font-medium text-slate-900">{nombreTabla(r.tabla)}</td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex px-2.5 py-0.5 rounded-full text-xs font-medium border ${OPERACIONES[r.operacion].clase}`}>
                        {OPERACIONES[r.operacion].texto}
                      </span>
                    </td>
                    <td className="px-4 py-3 tabular-nums text-slate-600">{r.registroId ?? '—'}</td>
                    <td className="px-4 py-3 text-slate-600">{r.camposModificados.join(', ') || '—'}</td>
                  </tr>
                  {expandido === r.id && (
                    <tr className="bg-slate-50/60 border-b border-slate-100">
                      <td></td>
                      <td colSpan={6} className="px-4 py-3 space-y-1 text-xs">
                        {detalleCambios(r)}
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
