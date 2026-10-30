import { useEffect, useRef } from 'react';
import { ArrowRight, Info, TriangleAlert, Upload, X } from 'lucide-react';
import type { Cambio, ValorInvalido } from './compararConfiguracion';
import { CAMPOS } from './compararConfiguracion';

interface Props {
  nombreArchivo: string;
  /** Cuándo se guardó el archivo, si lo dice. */
  guardadoEl?: string;
  /** Cuándo se guardó la configuración que está en uso. */
  actualizadaEl?: string;
  cambios: Cambio[];
  iguales: number;
  sinDato: number;
  ignorados: string[];
  invalidos: ValorInvalido[];
  /** Hay cambios hechos en la pantalla que todavía no se guardaron: al cargar el archivo se pierden. */
  hayCambiosSinGuardar: boolean;
  onCancelar: () => void;
  onCargar: () => void;
}

const plural = (n: number, uno: string, varios: string) => `${n} ${n === 1 ? uno : varios}`;

export function VistaPreviaDeConfiguracion({
  nombreArchivo, guardadoEl, actualizadaEl, cambios, iguales, sinDato, ignorados, invalidos, hayCambiosSinGuardar, onCancelar, onCargar,
}: Props) {
  const principal = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    principal.current?.focus();
    const tecla = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onCancelar();
    };
    document.addEventListener('keydown', tecla);
    return () => document.removeEventListener('keydown', tecla);
  }, [onCancelar]);

  const secciones = [...new Set(cambios.map((c) => c.seccion))];
  // Un dato inválido sí está en el archivo (se informa aparte): no cuenta como «el archivo no lo trae».
  const faltantes = Math.max(0, sinDato - invalidos.length);
  const sinCambios = cambios.length === 0;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget) onCancelar();
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="vista-previa-titulo"
        className="flex max-h-[calc(100vh-2rem)] w-[95%] max-w-3xl flex-col overflow-hidden rounded-xl bg-white shadow-2xl"
      >
        <div className="flex items-start justify-between gap-4 bg-slate-900 px-6 py-4">
          <div className="min-w-0">
            <h2 id="vista-previa-titulo" className="text-lg font-semibold leading-tight text-white">
              Vista previa de la configuración
            </h2>
            <p className="mt-0.5 break-all text-sm text-slate-400">
              Archivo: {nombreArchivo}
              {guardadoEl ? `, guardado el ${guardadoEl}` : ''}. Todavía no se cambió nada.
            </p>
          </div>
          <button type="button" onClick={onCancelar} aria-label="Cerrar" className="shrink-0 text-slate-400 transition-colors hover:text-white">
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="space-y-4 overflow-y-auto p-6">
          <p className="text-[15px] leading-6 text-slate-800">
            {sinCambios ? (
              <strong>El archivo es igual a la configuración actual: no hay nada que cargar.</strong>
            ) : (
              <>
                <strong>{plural(cambios.length, 'dato cambia', 'datos cambian')}</strong> y {plural(iguales, 'queda igual', 'quedan iguales')}.
                {faltantes > 0 && ` El archivo no trae ${plural(faltantes, 'dato', 'datos')}: quedan como están.`}
              </>
            )}
            {actualizadaEl && <span className="text-slate-500"> La configuración de ahora se guardó el {actualizadaEl}.</span>}
          </p>

          {!sinCambios && (
            <div className="overflow-x-auto rounded-lg ring-1 ring-slate-200">
              <table className="w-full min-w-[34rem] border-collapse text-left text-sm">
                <thead>
                  <tr className="bg-slate-100 text-slate-700">
                    <th className="px-3 py-2 font-semibold">Dato</th>
                    <th className="px-3 py-2 font-semibold">Ahora</th>
                    <th className="w-6 px-0 py-2" aria-hidden="true" />
                    <th className="px-3 py-2 font-semibold">En el archivo</th>
                  </tr>
                </thead>
                <tbody>
                  {secciones.map((seccion) => (
                    <SeccionDeCambios key={seccion} seccion={seccion} cambios={cambios.filter((c) => c.seccion === seccion)} />
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {(hayCambiosSinGuardar || invalidos.length > 0 || ignorados.length > 0) && (
            <ul className="space-y-2 text-[13px] leading-5">
              {hayCambiosSinGuardar && !sinCambios && (
                <Aviso ojo>Hay cambios hechos en la pantalla que todavía no guardaste: al cargar el archivo se pierden.</Aviso>
              )}
              {invalidos.length > 0 && (
                <Aviso ojo>
                  {invalidos.length === 1
                    ? '1 dato del archivo no se va a cargar porque no tiene un valor válido:'
                    : `${invalidos.length} datos del archivo no se van a cargar porque no tienen un valor válido:`}{' '}
                  {invalidos.map((v) => `${CAMPOS[v.campo as keyof typeof CAMPOS]?.etiqueta ?? v.campo} (${v.motivo})`).join('; ')}.
                </Aviso>
              )}
              {ignorados.length > 0 && (
                <Aviso>
                  {ignorados.length === 1
                    ? 'Se ignora 1 dato del archivo que no es de la configuración: '
                    : `Se ignoran ${ignorados.length} datos del archivo que no son de la configuración: `}
                  {ignorados.slice(0, 8).join(', ')}
                  {ignorados.length > 8 ? '…' : ''}.
                </Aviso>
              )}
            </ul>
          )}

          <p className="text-[13px] text-slate-500">
            La configuración no incluye datos contables: cargar el archivo no modifica facturas, asientos ni socios.
          </p>
        </div>

        <div className="flex flex-wrap justify-end gap-3 border-t border-slate-100 bg-white px-6 py-4">
          <button
            type="button"
            onClick={onCancelar}
            ref={sinCambios ? principal : undefined}
            className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-50"
          >
            {sinCambios ? 'Cerrar' : 'Cancelar'}
          </button>
          {!sinCambios && (
            <button
              type="button"
              onClick={onCargar}
              ref={principal}
              className="inline-flex items-center gap-2 rounded-lg bg-marca-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-marca-700"
            >
              <Upload className="h-4 w-4" aria-hidden="true" />
              Cargar {plural(cambios.length, 'cambio', 'cambios')}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

function SeccionDeCambios({ seccion, cambios }: { seccion: string; cambios: Cambio[] }) {
  return (
    <>
      <tr>
        <th colSpan={4} className="bg-slate-50 px-3 py-1.5 text-xs font-semibold text-slate-500">
          {seccion}
        </th>
      </tr>
      {cambios.map((c) => (
        <tr key={c.campo} className="border-t border-slate-100 align-top">
          <td className="px-3 py-2 font-medium text-slate-800">{c.etiqueta}</td>
          <td className="break-words px-3 py-2 text-red-700 line-through decoration-red-300">{c.actual}</td>
          <td className="px-0 py-2 text-slate-400" aria-hidden="true">
            <ArrowRight className="h-4 w-4" />
          </td>
          <td className="break-words px-3 py-2 font-medium text-emerald-700">{c.nuevo}</td>
        </tr>
      ))}
    </>
  );
}

function Aviso({ ojo, children }: { ojo?: boolean; children: React.ReactNode }) {
  const Icono = ojo ? TriangleAlert : Info;
  return (
    <li className={`flex gap-2 rounded-lg px-3 py-2 ${ojo ? 'bg-amber-50 text-amber-900' : 'bg-slate-50 text-slate-600'}`}>
      <Icono className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
      <span>{children}</span>
    </li>
  );
}
