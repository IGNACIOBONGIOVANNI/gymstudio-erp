import { useEffect, useState } from 'react';
import { 
  PieChart, Pie, Cell, 
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip, Legend, ResponsiveContainer 
} from 'recharts';
import { TrendingUp, Wallet, Activity } from 'lucide-react';

interface BalanceItem {
  name: string;
  value: number;
}

interface BalanceData {
  totalActivo: number;
  totalPasivo: number;
  totalPatrimonioNeto: number;
  resultadoDelEjercicio: number;
  composicionActivos: BalanceItem[];
  composicionPasivos: BalanceItem[];
  composicionPatrimonioNeto: BalanceItem[];
  detalleResultados: BalanceItem[];
}

export function BalanceGeneralTab() {
  const [data, setData] = useState<BalanceData | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [periodo, setPeriodo] = useState('historico');

  useEffect(() => {
    const fetchBalance = async () => {
      try {
        setIsLoading(true);
        let url = '/api/balance';
        
        if (periodo !== 'historico') {
          const hoy = new Date();
          let inicio = new Date();
          let fin = new Date();
          
          if (periodo === 'este_mes') {
            inicio = new Date(hoy.getFullYear(), hoy.getMonth(), 1);
            fin = new Date(hoy.getFullYear(), hoy.getMonth() + 1, 0);
          } else if (periodo === 'mes_anterior') {
            inicio = new Date(hoy.getFullYear(), hoy.getMonth() - 1, 1);
            fin = new Date(hoy.getFullYear(), hoy.getMonth(), 0);
          } else if (periodo === 'este_ano') {
            inicio = new Date(hoy.getFullYear(), 0, 1);
            fin = new Date(hoy.getFullYear(), 11, 31);
          }

          const formatDate = (d: Date) => {
            const yyyy = d.getFullYear();
            const mm = String(d.getMonth() + 1).padStart(2, '0');
            const dd = String(d.getDate()).padStart(2, '0');
            return `${yyyy}-${mm}-${dd}`;
          };

          url += `?fechaInicio=${formatDate(inicio)}&fechaFin=${formatDate(fin)}`;
        }

        const res = await fetch(url);
        if (res.ok) {
          const json = await res.json();
          setData(json);
        }
      } catch (error) {
        console.error('Error fetching balance:', error);
      } finally {
        setIsLoading(false);
      }
    };
    fetchBalance();
  }, [periodo]);

  if (isLoading) {
    return (
      <div className="flex justify-center items-center h-64 text-slate-500 font-medium">
        Cargando Balance General...
      </div>
    );
  }

  if (!data) {
    return (
      <div className="flex justify-center items-center h-64 text-red-500 font-medium">
        Error al cargar el balance. Verificá la conexión con el servidor.
      </div>
    );
  }

  const formatCurrency = (value: number) => 
    new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' }).format(value);

  // Paleta moderna de colores para el gráfico de Dona (Tonos Verde/Esmeralda para Activos)
  const COLORS_ACTIVOS = ['#10b981', '#3b82f6', '#14b8a6', '#f59e0b', '#8b5cf6', '#ec4899'];
  
  // Datos formateados para el gráfico de barras (Comparativa Activo vs Pasivo)
  const barData = [
    { name: 'Activo', monto: data.totalActivo, fill: '#10b981' }, // Verde Esmeralda
    { name: 'Pasivo', monto: data.totalPasivo, fill: '#ef4444' }  // Rojo Fuerte
  ];

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 mb-2">
        <h2 className="text-xl font-bold text-slate-800">Balance General</h2>
        <select
          value={periodo}
          onChange={(e) => setPeriodo(e.target.value)}
          className="border border-slate-300 rounded-lg text-sm py-2 px-3.5 focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white shadow-sm font-medium text-slate-700 transition-colors"
        >
          <option value="historico">Histórico (Todo)</option>
          <option value="este_mes">Este Mes</option>
          <option value="mes_anterior">Mes Anterior</option>
          <option value="este_ano">Este Año</option>
        </select>
      </div>

      {/* --- Fila 1: Tarjetas Resumen (Kpis) --- */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {/* KPI Activo */}
        <div className="bg-white rounded-xl shadow-sm p-6 border-l-4 border-emerald-500 flex items-center justify-between border-slate-200/80 border">
          <div>
            <p className="text-sm font-medium text-slate-500 mb-1">Total Activo</p>
            <h4 className="text-2xl font-bold text-slate-900 tabular-nums">{formatCurrency(data.totalActivo)}</h4>
          </div>
          <div className="p-3 bg-emerald-50 rounded-full text-emerald-600">
            <Wallet className="h-6 w-6" />
          </div>
        </div>
        
        {/* KPI Pasivo */}
        <div className="bg-white rounded-xl shadow-sm p-6 border-l-4 border-red-500 flex items-center justify-between border-slate-200/80 border">
          <div>
            <p className="text-sm font-medium text-slate-500 mb-1">Total Pasivo</p>
            <h4 className="text-2xl font-bold text-slate-900 tabular-nums">{formatCurrency(data.totalPasivo)}</h4>
          </div>
          <div className="p-3 bg-red-50 rounded-full text-red-600">
            <Activity className="h-6 w-6" />
          </div>
        </div>

        {/* KPI Resultado (Dinámico según ganancia o pérdida) */}
        <div className={`bg-white rounded-xl shadow-sm p-6 border-l-4 flex items-center justify-between border-slate-200/80 border ${data.resultadoDelEjercicio >= 0 ? 'border-l-blue-500' : 'border-l-orange-500'}`}>
          <div>
            <p className="text-sm font-medium text-slate-500 mb-1">Resultado del Ejercicio</p>
            <h4 className="text-2xl font-bold text-slate-900 tabular-nums">{formatCurrency(data.resultadoDelEjercicio)}</h4>
          </div>
          <div className={`p-3 rounded-full ${data.resultadoDelEjercicio >= 0 ? 'bg-blue-50 text-blue-600' : 'bg-orange-50 text-orange-600'}`}>
            <TrendingUp className="h-6 w-6" />
          </div>
        </div>
      </div>

      {/* --- Fila 2: Gráficos Interactivos (Recharts) --- */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        
        {/* Gráfico de Dona: Composición de Activos */}
        <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-200/80">
          <h3 className="text-lg font-semibold text-slate-800 mb-4">Distribución del Activo</h3>
          <div className="h-72">
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie
                  data={data.composicionActivos.filter(a => a.value > 0)}
                  cx="50%"
                  cy="50%"
                  innerRadius={65}
                  outerRadius={95}
                  paddingAngle={5}
                  dataKey="value"
                >
                  {data.composicionActivos.map((_, index) => (
                    <Cell key={`cell-${index}`} fill={COLORS_ACTIVOS[index % COLORS_ACTIVOS.length]} />
                  ))}
                </Pie>
                <RechartsTooltip formatter={(val: any) => formatCurrency(Number(val) || 0)} />
                <Legend verticalAlign="bottom" height={36} />
              </PieChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Gráfico de Barras: Activo vs Pasivo */}
        <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-200/80">
          <h3 className="text-lg font-semibold text-slate-800 mb-4">Estructura Financiera</h3>
          <div className="h-72">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={barData} margin={{ top: 20, right: 30, left: 20, bottom: 5 }}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e2e8f0" />
                <XAxis dataKey="name" axisLine={false} tickLine={false} />
                <YAxis 
                   tickFormatter={(val) => `$${val/1000}k`} 
                   axisLine={false} 
                   tickLine={false} 
                   width={80} 
                />
                <RechartsTooltip 
                  formatter={(val: any) => formatCurrency(Number(val) || 0)} 
                  cursor={{fill: '#f8fafc'}} 
                />
                <Bar dataKey="monto" radius={[6, 6, 0, 0]} maxBarSize={80}>
                  {barData.map((entry, index) => (
                    <Cell key={`cell-${index}`} fill={entry.fill} />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>
      </div>

      {/* --- Fila 3: Tabla Contable (Formato "T") --- */}
      <div className="bg-white rounded-xl shadow-sm overflow-hidden border border-slate-200/80">
        <div className="px-6 py-4 border-b border-slate-200/80 bg-slate-50/50">
          <h3 className="text-lg font-semibold text-slate-800">Estado de Situación Patrimonial</h3>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 divide-y md:divide-y-0 md:divide-x divide-slate-200/80">
          
          {/* Columna Izquierda: Activos */}
          <div className="p-0 flex flex-col h-full">
            <div className="bg-emerald-50/40 px-6 py-2.5 border-b border-slate-200/80 font-bold text-xs uppercase tracking-wider text-emerald-800">
              ACTIVO
            </div>
            <ul className="divide-y divide-slate-100 flex-grow">
              {data.composicionActivos.map((item, idx) => (
                <li key={idx} className="flex justify-between px-6 py-3 text-sm hover:bg-slate-50/50 transition-colors">
                  <span className="text-slate-600">{item.name}</span>
                  <span className="font-medium text-slate-800 tabular-nums">{formatCurrency(item.value)}</span>
                </li>
              ))}
            </ul>
            <div className="flex justify-between px-6 py-4 bg-emerald-50 font-bold border-t border-emerald-200 text-emerald-900 mt-auto">
              <span>TOTAL ACTIVO</span>
              <span className="tabular-nums">{formatCurrency(data.totalActivo)}</span>
            </div>
          </div>

          {/* Columna Derecha: Pasivo y Patrimonio Neto */}
          <div className="p-0 flex flex-col h-full">
            <div className="bg-red-50/40 px-6 py-2.5 border-b border-slate-200/80 font-bold text-xs uppercase tracking-wider text-red-800">
              PASIVO
            </div>
            <ul className="divide-y divide-slate-100">
              {data.composicionPasivos.map((item, idx) => (
                <li key={idx} className="flex justify-between px-6 py-3 text-sm hover:bg-slate-50/50 transition-colors">
                  <span className="text-slate-600">{item.name}</span>
                  <span className="font-medium text-slate-800 tabular-nums">{formatCurrency(item.value)}</span>
                </li>
              ))}
              <li className="flex justify-between px-6 py-3 bg-red-50/50 font-bold border-t border-red-100 text-red-900">
                <span>TOTAL PASIVO</span>
                <span className="tabular-nums">{formatCurrency(data.totalPasivo)}</span>
              </li>
            </ul>

            <div className="bg-blue-50/40 px-6 py-2.5 border-y border-slate-200/80 font-bold text-xs uppercase tracking-wider text-blue-800 mt-4">
              PATRIMONIO NETO
            </div>
            <ul className="divide-y divide-slate-100 flex-grow">
              {data.composicionPatrimonioNeto.map((item, idx) => (
                <li key={idx} className="flex justify-between px-6 py-3 text-sm hover:bg-slate-50/50 transition-colors">
                  <span className="text-slate-600">{item.name}</span>
                  <span className="font-medium text-slate-800 tabular-nums">{formatCurrency(item.value)}</span>
                </li>
              ))}
              <li className="flex justify-between px-6 py-3 text-sm hover:bg-slate-50/50 transition-colors">
                <span className="text-slate-600">Resultado del Ejercicio</span>
                <span className={`font-medium tabular-nums ${data.resultadoDelEjercicio >= 0 ? 'text-blue-600' : 'text-orange-600'}`}>
                  {formatCurrency(data.resultadoDelEjercicio)}
                </span>
              </li>
            </ul>
            {/* Principio de Equidad: Pasivo + Patrimonio Neto (incluye el resultado) */}
            <div className="flex justify-between px-6 py-4 bg-slate-100 font-bold border-t border-slate-200 text-slate-900 mt-auto">
              <span>TOTAL PASIVO + P.N.</span>
              <span className="tabular-nums">{formatCurrency(data.totalPasivo + data.totalPatrimonioNeto + data.resultadoDelEjercicio)}</span>
            </div>
          </div>
          
        </div>
      </div>
    </div>
  );
}