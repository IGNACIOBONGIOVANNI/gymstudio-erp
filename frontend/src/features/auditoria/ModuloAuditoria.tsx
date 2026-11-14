import { useEffect, useState, Fragment } from 'react';
import { History, Loader2, RefreshCw, ChevronDown, ChevronRight, ChevronLeft, ChevronsLeft, ChevronsRight } from 'lucide-react';
import toast from 'react-hot-toast';

// Registro generado por el trigger contabilidad.fn_auditar() en PostgreSQL.
interface RegistroAuditoria {
  id: number;
  fecha: string;
  usuario: string;
  tabla: string;
  // INSERT, UPDATE y DELETE los escribe el trigger; INGRESO, FALLIDO y BLOQUEADO, el login (tabla "acceso").
  operacion: string;
  registroId: string | null;
  camposModificados: string[];
  // Equipo desde el que se hizo el cambio. Vacío en los registros anteriores a que se guardaran.
  ip: string | null;
  sistemaOperativo: string | null;
  navegador: string | null;
  datosAnteriores: Record<string, unknown> | null;
  datosNuevos: Record<string, unknown> | null;
}

const OPERACIONES: Record<string, { texto: string; clase: string }> = {
  INSERT: { texto: 'Alta', clase: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  UPDATE: { texto: 'Modificación', clase: 'bg-amber-50 text-amber-700 border-amber-200' },
  DELETE: { texto: 'Baja', clase: 'bg-red-50 text-red-700 border-red-200' },
  INGRESO: { texto: 'Inicio de sesión', clase: 'bg-sky-50 text-sky-700 border-sky-200' },
  FALLIDO: { texto: 'Intento fallido', clase: 'bg-red-50 text-red-700 border-red-200' },
  BLOQUEADO: { texto: 'Cuenta bloqueada', clase: 'bg-red-100 text-red-800 border-red-300' },
  DESCARGA: { texto: 'Copia descargada', clase: 'bg-sky-50 text-sky-700 border-sky-200' },
  RESTAURADA: { texto: 'Copia restaurada', clase: 'bg-red-100 text-red-800 border-red-300' },
  CAMBIO_CLAVE: { texto: 'Cambió su contraseña', clase: 'bg-sky-50 text-sky-700 border-sky-200' },
  CLAVE_RECHAZADA: { texto: 'Contraseña actual incorrecta', clase: 'bg-red-50 text-red-700 border-red-200' },
};

// Una operación que esta pantalla no conoce se muestra tal cual, sin romperla.
const operacion = (codigo: string) =>
  OPERACIONES[codigo] ?? { texto: codigo, clase: 'bg-slate-50 text-slate-700 border-slate-200' };

const DESCRIPCION_ACCESO: Record<string, string> = {
  DESCARGA: 'Descargó una copia de seguridad completa del sistema.',
  RESTAURADA: 'Restauró una copia de seguridad: se reemplazaron todos los datos del sistema.',
  INGRESO: 'Inició sesión correctamente.',
  FALLIDO: 'Intento de ingreso con una clave incorrecta, una cuenta inexistente o una cuenta inactiva.',
  BLOQUEADO: 'Intento de ingreso sobre una cuenta bloqueada por demasiados intentos fallidos.',
  CAMBIO_CLAVE: 'Cambió su propia contraseña, dando la actual.',
  CLAVE_RECHAZADA: 'Intentó cambiar su contraseña con una contraseña actual incorrecta.',
};

// "contabilidad.Facturas" -> "Facturas"; los inicios de sesión se guardan con la tabla "acceso".
const nombreTabla = (tabla: string) => (tabla === 'acceso' ? 'Accesos' : tabla === 'copia' ? 'Copias de seguridad' : tabla.split('.').pop() ?? tabla);

const equipo = (r: RegistroAuditoria) => [r.sistemaOperativo, r.navegador].filter(Boolean).join(', ') || '—';

function mostrarValor(valor: unknown): string {
  if (valor === null || valor === undefined || valor === '') return '—';
  if (typeof valor === 'object') return JSON.stringify(valor);
  return String(valor);
}

const TAMANOS = [25, 50, 100] as const;

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
  // De a una página: el servidor manda el total en X-Total-Count.
  const [pagina, setPagina] = useState(1);
  const [tamano, setTamano] = useState<number>(25);
  const [total, setTotal] = useState(0);
  const paginas = Math.max(1, Math.ceil(total / tamano));

  const cargar = async (numero = pagina) => {
    setCargando(true);
    try {
      const params = new URLSearchParams({ pagina: String(numero), tamano: String(tamano) });
      if (tabla) params.set('tabla', tabla);
      if (usuario.trim()) params.set('usuario', usuario.trim());
      const res = await fetch(`/api/auditoria?${params}`);
      const datos = await leerJson<RegistroAuditoria[]>(res);
      const cantidad = Number(res.headers.get('X-Total-Count') ?? datos.length);
      setTotal(Number.isFinite(cantidad) ? cantidad : datos.length);
      setRegistros(datos);
      setPagina(numero);
      setExpandido(null);
    } catch (error: any) {
      toast.error(error?.message);
    } finally {
      setCargando(false);
    }
  };

  const irAPagina = (numero: number) => {
    const destino = Math.min(Math.max(numero, 1), paginas);
    if (destino !== pagina) cargar(destino);
  };

  useEffect(() => {
    fetch('/api/auditoria/tablas').then((r) => leerJson<string[]>(r)).then(setTablas).catch(() => setTablas([]));
  }, []);

  // Al cambiar el filtro de tabla o el tamaño de página se vuelve a la primera.
  useEffect(() => {
    cargar(1);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tabla, tamano]);

  const detalleCambios = (r: RegistroAuditoria) => {
    if (r.tabla === 'acceso' || r.tabla === 'copia') {
      return [
        <div key="acceso" className="text-slate-800">{DESCRIPCION_ACCESO[r.operacion] ?? r.operacion}</div>,
        <div key="equipo" className="text-slate-600">
          Equipo: IP {r.ip ?? '—'}, {r.sistemaOperativo ?? 'sistema desconocido'}, {r.navegador ?? 'navegador desconocido'}.
        </div>,
      ];
    }
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
            onKeyDown={(e) => e.key === 'Enter' && cargar(1)}
            placeholder="Filtrar por usuario"
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          />
          <button
            onClick={() => cargar(1)}
            className="inline-flex items-center gap-1.5 rounded-md bg-marca-600 px-3 py-2 text-sm font-medium text-white hover:bg-marca-700"
          >
            {cargando ? <Loader2 className="h-4 w-4 animate-spin" /> : <RefreshCw className="h-4 w-4" />}
            Actualizar
          </button>
        </div>
      </div>

      <div className="rounded-xl border border-slate-200/80 bg-white shadow-sm overflow-hidden">
        <Paginador pagina={pagina} paginas={paginas} total={total} tamano={tamano} cantidad={registros.length}
          cargando={cargando} onIr={irAPagina} onTamano={setTamano} />
        {/* Alto limitado y encabezado fijo: la barra horizontal queda siempre a la vista, sin bajar hasta el final. */}
        <div className="max-h-[65vh] overflow-auto border-t border-slate-200">
          <table className="w-full text-sm">
            <thead className="sticky top-0 z-10 bg-slate-50">
              <tr className="border-b border-slate-200 text-left text-slate-500">
                <th className="h-11 px-4 font-medium w-8"></th>
                <th className="h-11 px-4 font-medium">Fecha</th>
                <th className="h-11 px-4 font-medium">Usuario</th>
                <th className="h-11 px-4 font-medium">Tabla</th>
                <th className="h-11 px-4 font-medium">Operación</th>
                <th className="h-11 px-4 font-medium">Registro</th>
                <th className="h-11 px-4 font-medium">Campos modificados</th>
                <th className="h-11 px-4 font-medium">IP</th>
                <th className="h-11 px-4 font-medium">Equipo</th>
              </tr>
            </thead>
            <tbody>
              {registros.length === 0 && (
                <tr>
                  <td colSpan={9} className="h-24 text-center text-slate-500">
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
                      <span className={`inline-flex px-2.5 py-0.5 rounded-full text-xs font-medium border whitespace-nowrap ${operacion(r.operacion).clase}`}>
                        {operacion(r.operacion).texto}
                      </span>
                    </td>
                    <td className="px-4 py-3 tabular-nums text-slate-600">{r.registroId ?? '—'}</td>
                    <td className="px-4 py-3 text-slate-600">{r.camposModificados.join(', ') || '—'}</td>
                    <td className="px-4 py-3 tabular-nums text-slate-600 whitespace-nowrap">{r.ip ?? '—'}</td>
                    <td className="px-4 py-3 text-slate-600 whitespace-nowrap">{equipo(r)}</td>
                  </tr>
                  {expandido === r.id && (
                    <tr className="bg-slate-50/60 border-b border-slate-100">
                      <td></td>
                      <td colSpan={8} className="px-4 py-3 space-y-1 text-xs">
                        {detalleCambios(r)}
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
        {paginas > 1 && (
          <div className="border-t border-slate-200">
            <Paginador pagina={pagina} paginas={paginas} total={total} tamano={tamano} cantidad={registros.length}
              cargando={cargando} onIr={irAPagina} onTamano={setTamano} />
          </div>
        )}
      </div>
    </div>
  );
}

interface PaginadorProps {
  pagina: number;
  paginas: number;
  total: number;
  tamano: number;
  cantidad: number;
  cargando: boolean;
  onIr: (pagina: number) => void;
  onTamano: (tamano: number) => void;
}

/** «Registros 1 a 25 de 163», el tamaño de página y los botones para moverse. */
function Paginador({ pagina, paginas, total, tamano, cantidad, cargando, onIr, onTamano }: PaginadorProps) {
  const desde = total === 0 ? 0 : (pagina - 1) * tamano + 1;
  const hasta = total === 0 ? 0 : desde + cantidad - 1;
  const boton = 'inline-flex h-8 w-8 items-center justify-center rounded-md border border-slate-300 text-slate-600 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40';
  return (
    <nav aria-label="Páginas de la auditoría" className="flex flex-wrap items-center justify-between gap-3 px-4 py-2.5 text-sm text-slate-600">
      <p aria-live="polite">
        {total === 0 ? 'Sin registros' : <>Registros <b className="tabular-nums">{desde}</b> a <b className="tabular-nums">{hasta}</b> de <b className="tabular-nums">{total}</b></>}
      </p>
      <div className="flex flex-wrap items-center gap-3">
        <label className="flex items-center gap-2">
          Por página
          <select value={tamano} onChange={(e) => onTamano(Number(e.target.value))} disabled={cargando}
            className="rounded-md border border-slate-300 px-2 py-1 text-sm">
            {TAMANOS.map((t) => <option key={t} value={t}>{t}</option>)}
          </select>
        </label>
        <div className="flex items-center gap-1">
          <button type="button" className={boton} onClick={() => onIr(1)} disabled={cargando || pagina <= 1} aria-label="Primera página" title="Primera página">
            <ChevronsLeft className="h-4 w-4" />
          </button>
          <button type="button" className={boton} onClick={() => onIr(pagina - 1)} disabled={cargando || pagina <= 1} aria-label="Página anterior" title="Página anterior">
            <ChevronLeft className="h-4 w-4" />
          </button>
          <span className="px-2 tabular-nums">Página {pagina} de {paginas}</span>
          <button type="button" className={boton} onClick={() => onIr(pagina + 1)} disabled={cargando || pagina >= paginas} aria-label="Página siguiente" title="Página siguiente">
            <ChevronRight className="h-4 w-4" />
          </button>
          <button type="button" className={boton} onClick={() => onIr(paginas)} disabled={cargando || pagina >= paginas} aria-label="Última página" title="Última página">
            <ChevronsRight className="h-4 w-4" />
          </button>
        </div>
      </div>
    </nav>
  );
}
