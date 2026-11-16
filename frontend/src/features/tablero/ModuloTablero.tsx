import { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { hoyLocal } from '../../utils/formatters';
import { PestanaAnalisis } from './PestanaAnalisis';
import { PestanaComando } from './PestanaComando';
import { PestanaDecisiones } from './PestanaDecisiones';
import { PestanaExportar } from './PestanaExportar';

type Pestana = 'comando' | 'analisis' | 'decisiones' | 'exportar';
const PESTANAS: readonly { id: Pestana; titulo: string; ayuda: string }[] = [
  { id: 'comando', titulo: 'Comando', ayuda: 'Tablero de comando: indicadores con meta y semáforo (nivel estratégico).' },
  { id: 'analisis', titulo: 'Análisis', ayuda: 'Cruzar los datos con filtros, como en Power BI (nivel táctico).' },
  { id: 'decisiones', titulo: 'Decisiones', ayuda: 'A quién contactar hoy y qué pasaría si cambian los precios.' },
  { id: 'exportar', titulo: 'Exportar', ayuda: 'El tablero en PDF y los datos en Excel.' },
];

/**
 * Tablero de Gestión: la parte del sistema que convierte los datos de todos los días en información para decidir.
 * Solo lo ven el Administrador y el Contador; las metas las fija el Administrador.
 */
export function ModuloTablero() {
  const { user } = useAuth();
  const esAdministrador = user?.rol === 'Administrador';
  const hoy = hoyLocal();
  const [pestana, setPestana] = useState<Pestana>('comando');
  const [mes, setMes] = useState(hoy.slice(0, 7));

  const moverse = (e: React.KeyboardEvent<HTMLButtonElement>) => {
    const i = PESTANAS.findIndex((p) => p.id === pestana);
    if (e.key === 'ArrowRight') setPestana(PESTANAS[(i + 1) % PESTANAS.length].id);
    else if (e.key === 'ArrowLeft') setPestana(PESTANAS[(i + PESTANAS.length - 1) % PESTANAS.length].id);
    else return;
    e.preventDefault();
  };

  const actual = PESTANAS.find((p) => p.id === pestana)!;
  return (
    <div className="space-y-4">
      <p className="text-sm text-slate-600">
        Los datos que se cargan todos los días (facturas, cuotas, ingresos, horas, stock) se convierten acá en información para decidir:
        se mide, se compara con la meta y con el mes anterior, y se ve qué corregir.
      </p>
      <div role="tablist" aria-label="Secciones del Tablero de Gestión" className="flex flex-wrap gap-1 border-b border-slate-200">
        {PESTANAS.map(({ id, titulo }) => (
          <button key={id} type="button" role="tab" id={`tablero-${id}`} aria-selected={pestana === id} aria-controls={`panel-tablero-${id}`}
            tabIndex={pestana === id ? 0 : -1} onClick={() => setPestana(id)} onKeyDown={moverse}
            className={`-mb-px border-b-2 px-4 py-2 text-sm font-medium transition-colors ${
              pestana === id ? 'border-marca-600 text-marca-700' : 'border-transparent text-slate-500 hover:text-slate-800'}`}>
            {titulo}
          </button>
        ))}
      </div>
      <div role="tabpanel" id={`panel-tablero-${pestana}`} aria-labelledby={`tablero-${pestana}`} className="space-y-4">
        <p className="text-xs text-slate-500">{actual.ayuda}</p>
        {pestana === 'comando' && <PestanaComando mes={mes} onMes={setMes} hoy={hoy} puedeEditarMetas={esAdministrador} />}
        {pestana === 'analisis' && <PestanaAnalisis mesFinal={mes} />}
        {pestana === 'decisiones' && <PestanaDecisiones hoy={hoy} />}
        {pestana === 'exportar' && <PestanaExportar mes={mes} onMes={setMes} hoy={hoy} />}
      </div>
    </div>
  );
}
