import { useState, useEffect } from 'react';
import { 
  Settings, Building2, Calculator, ShieldCheck, Database, 
  Save, RotateCcw, CheckCircle2, Download, Upload, AlertCircle
} from 'lucide-react';
import type { ConfiguracionERP } from './types';
import { 
  obtenerConfiguracion, 
  guardarConfiguracion, 
  restablecerConfiguracionPorDefecto 
} from './configuracionStorage';
import toast from 'react-hot-toast';

export default function ModuloConfiguracion() {
  const [config, setConfig] = useState<ConfiguracionERP>(obtenerConfiguracion());
  const [tabActiva, setTabActiva] = useState<'empresa' | 'contable' | 'seguridad' | 'backup'>('empresa');
  const [guardadoReciente, setGuardadoReciente] = useState(false);

  useEffect(() => {
    setConfig(obtenerConfiguracion());
  }, []);

  const handleChange = (campo: keyof ConfiguracionERP, valor: any) => {
    setConfig((prev) => ({
      ...prev,
      [campo]: valor,
    }));
  };

  const handleGuardar = (e?: React.FormEvent) => {
    if (e) e.preventDefault();

    if (!config.razonSocial.trim()) {
      toast.error('La Razón Social es obligatoria');
      return;
    }
    if (!config.cuit.trim()) {
      toast.error('El CUIT es obligatorio');
      return;
    }

    try {
      const actualizada = guardarConfiguracion(config);
      setConfig(actualizada);
      setGuardadoReciente(true);
      toast.success('¡Configuración guardada exitosamente!');
      setTimeout(() => setGuardadoReciente(false), 4000);
    } catch (error) {
      toast.error('Error al guardar la configuración');
    }
  };

  const handleRestablecer = () => {
    if (window.confirm('¿Deseás restablecer los parámetros a los valores iniciales de fábrica?')) {
      const def = restablecerConfiguracionPorDefecto();
      setConfig(def);
      toast.success('Parámetros restablecidos por defecto');
    }
  };

  const handleExportarJSON = () => {
    const dataStr = 'data:text/json;charset=utf-8,' + encodeURIComponent(JSON.stringify(config, null, 2));
    const downloadAnchor = document.createElement('a');
    downloadAnchor.setAttribute('href', dataStr);
    downloadAnchor.setAttribute('download', `gymstudio_config_${new Date().toISOString().slice(0, 10)}.json`);
    document.body.appendChild(downloadAnchor);
    downloadAnchor.click();
    downloadAnchor.remove();
    toast.success('Archivo de configuración descargado');
  };

  const handleImportarJSON = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = (e) => {
      try {
        const contenido = e.target?.result as string;
        const parsed = JSON.parse(contenido);
        if (parsed.razonSocial && parsed.cuit) {
          const actualizada = guardarConfiguracion({ ...config, ...parsed });
          setConfig(actualizada);
          toast.success('Configuración importada exitosamente');
        } else {
          toast.error('El archivo no contiene un formato de configuración válido');
        }
      } catch (err) {
        toast.error('Error al leer el archivo JSON');
      }
    };
    reader.readAsText(file);
  };

  return (
    <div className="space-y-6">
      
      {/* Encabezado */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-200 pb-5">
        <div>
          <div className="flex items-center gap-2.5">
            <div className="p-2 bg-indigo-50 text-indigo-600 rounded-lg">
              <Settings className="h-6 w-6" />
            </div>
            <div>
              <h2 className="text-xl sm:text-2xl font-bold text-slate-900">
                Configuración del Sistema
              </h2>
              <p className="text-xs sm:text-sm text-slate-500">
                Parámetros fiscales, contables, reglas de facturación y preferencias del ERP
              </p>
            </div>
          </div>
        </div>

        <div className="flex items-center gap-2.5 shrink-0">
          <button
            type="button"
            onClick={handleRestablecer}
            title="Restablecer valores predeterminados"
            className="inline-flex items-center gap-1.5 px-3 py-2 text-xs sm:text-sm font-medium text-slate-600 bg-white border border-slate-200 rounded-lg hover:bg-slate-50 hover:text-slate-900 shadow-sm transition-colors"
          >
            <RotateCcw className="h-4 w-4" />
            <span className="hidden sm:inline">Valores por Defecto</span>
          </button>

          <button
            type="button"
            onClick={() => handleGuardar()}
            className="inline-flex items-center gap-2 rounded-lg bg-marca-600 px-4 py-2 min-h-[40px] text-sm font-semibold text-white shadow-sm transition-all hover:bg-marca-700 active:scale-95 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2"
          >
            <Save className="h-4 w-4" />
            <span>Guardar Configuración</span>
          </button>
        </div>
      </div>

      {/* Banner de Estado Guardado */}
      {guardadoReciente && (
        <div className="flex items-center gap-3 p-4 bg-emerald-50 border border-emerald-200 rounded-xl text-emerald-800 text-sm animate-in fade-in duration-200">
          <CheckCircle2 className="h-5 w-5 text-emerald-600 shrink-0" />
          <div className="flex-1">
            <span className="font-bold">¡Cambios guardados con éxito!</span> Los nuevos parámetros fiscales y contables ya están vigentes y sincronizados en el navegador.
          </div>
        </div>
      )}

      {/* Resumen Superior de la Empresa */}
      <div className="bg-white p-4 sm:p-5 rounded-xl border border-slate-200/80 shadow-sm flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className="h-12 w-12 rounded-xl bg-slate-900 text-white font-bold flex items-center justify-center text-lg shadow-sm">
            GS
          </div>
          <div>
            <h3 className="font-bold text-slate-900 text-base sm:text-lg">
              {config.razonSocial || 'GYM STUDIO S.R.L.'}
            </h3>
            <div className="flex items-center gap-2 text-xs text-slate-500 flex-wrap">
              <span className="font-mono bg-slate-100 px-2 py-0.5 rounded text-slate-700 font-semibold">
                CUIT: {config.cuit}
              </span>
              <span>•</span>
              <span className="text-emerald-700 font-medium">
                {config.condicionIva}
              </span>
              <span>•</span>
              <span>Pto. Venta: {config.puntoVenta}</span>
            </div>
          </div>
        </div>

        <div className="text-left sm:text-right text-xs text-slate-400">
          <span>Última modificación: </span>
          <span className="font-semibold text-slate-600 tabular-nums">
            {config.ultimaActualizacion || 'Hoy'}
          </span>
        </div>
      </div>

      {/* Barra de Pestañas */}
      <div className="border-b border-slate-200 overflow-x-auto whitespace-nowrap scrollbar-none">
        <nav className="flex space-x-6">
          <button
            onClick={() => setTabActiva('empresa')}
            className={`pb-3 text-sm font-medium border-b-2 transition-colors flex items-center gap-2 ${
              tabActiva === 'empresa'
                ? 'border-indigo-600 text-indigo-600 font-bold'
                : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700'
            }`}
          >
            <Building2 className="h-4 w-4" />
            <span>Datos Fiscales y Empresa</span>
          </button>

          <button
            onClick={() => setTabActiva('contable')}
            className={`pb-3 text-sm font-medium border-b-2 transition-colors flex items-center gap-2 ${
              tabActiva === 'contable'
                ? 'border-indigo-600 text-indigo-600 font-bold'
                : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700'
            }`}
          >
            <Calculator className="h-4 w-4" />
            <span>Parámetros Contables y Facturación</span>
          </button>

          <button
            onClick={() => setTabActiva('seguridad')}
            className={`pb-3 text-sm font-medium border-b-2 transition-colors flex items-center gap-2 ${
              tabActiva === 'seguridad'
                ? 'border-indigo-600 text-indigo-600 font-bold'
                : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700'
            }`}
          >
            <ShieldCheck className="h-4 w-4" />
            <span>Seguridad y Sistema</span>
          </button>

          <button
            onClick={() => setTabActiva('backup')}
            className={`pb-3 text-sm font-medium border-b-2 transition-colors flex items-center gap-2 ${
              tabActiva === 'backup'
                ? 'border-indigo-600 text-indigo-600 font-bold'
                : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700'
            }`}
          >
            <Database className="h-4 w-4" />
            <span>Copia de Seguridad</span>
          </button>
        </nav>
      </div>

      {/* Formulario Principal */}
      <form onSubmit={handleGuardar}>
        
        {/* PESTAÑA 1: DATOS FISCALES Y EMPRESA */}
        {tabActiva === 'empresa' && (
          <div className="bg-white rounded-xl border border-slate-200/80 p-6 shadow-sm space-y-6">
            <div>
              <h3 className="text-base font-bold text-slate-900 mb-1">
                Identificación Tributaria y Comercial
              </h3>
              <p className="text-xs text-slate-500">
                Estos datos se imprimen en los encabezados de las facturas (A, B, C) y en los libros contables reglamentarios.
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
              
              <div>
                <label className="block text-xs font-semibold text-slate-600 mb-1">
                  Razón Social *
                </label>
                <input
                  type="text"
                  required
                  value={config.razonSocial}
                  onChange={(e) => handleChange('razonSocial', e.target.value)}
                  className="w-full px-3 py-2 text-sm border border-slate-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-all"
                  placeholder="GYM STUDIO S.R.L."
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-600 mb-1">
                  Nombre de Fantasía
                </label>
                <input
                  type="text"
                  value={config.nombreFantasia}
                  onChange={(e) => handleChange('nombreFantasia', e.target.value)}
                  className="w-full px-3 py-2 text-sm border border-slate-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-all"
                  placeholder="Gym Studio Fitness & Health"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-600 mb-1">
                  CUIT (con guiones) *
                </label>
                <input
                  type="text"
                  required
                  value={config.cuit}
                  onChange={(e) => handleChange('cuit', e.target.value)}
                  className="w-full px-3 py-2 text-sm border border-slate-300 rounded-lg font-mono focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-all"
                  placeholder="30-71234567-9"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-600 mb-1">
                  Condición Frente al IVA *
                </label>
                <select
                  value={config.condicionIva}
                  onChange={(e) => handleChange('condicionIva', e.target.value)}
                  className="w-full px-3 py-2 text-sm border border-slate-300 rounded-lg bg-white focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-all"
                >
                  <option value="Responsable Inscripto">Responsable Inscripto</option>
                  <option value="Monotributo">Monotributo</option>
                  <option value="Exento">Exento</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-600 mb-1">
                  Punto de Venta Predeterminado *
                </label>
                <input
                  type="text"
                  maxLength={5}
                  value={config.puntoVenta}
                  onChange={(e) => handleChange('puntoVenta', e.target.value)}
                  className="w-full px-3 py-2 text-sm border border-slate-300 rounded-lg font-mono focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-all"
                  placeholder="0001"
                />
                <span className="text-[11px] text-slate-400 mt-0.5 block">
                  Número de 4 dígitos para comprobantes fiscales electrónicos.
                </span>
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-600 mb-1">
                  Ingresos Brutos (IIBB)
                </label>
                <input
                  type="text"
                  value={config.ingresosBrutos}
                  onChange={(e) => handleChange('ingresosBrutos', e.target.value)}
                  className="w-full px-3 py-2 text-sm border border-slate-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-all"
                  placeholder="30-71234567-9 (Convenio Multilateral)"
                />
              </div>

              <div className="md:col-span-2">
                <label className="block text-xs font-semibold text-slate-600 mb-1">
                  Domicilio Comercial
                </label>
                <input
                  type="text"
                  value={config.direccion}
                  onChange={(e) => handleChange('direccion', e.target.value)}
                  className="w-full px-3 py-2 text-sm border border-slate-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-all"
                  placeholder="Av. Corrientes 1234, Ciudad Autónoma de Buenos Aires"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-600 mb-1">
                  Teléfono de Contacto
                </label>
                <input
                  type="text"
                  value={config.telefono}
                  onChange={(e) => handleChange('telefono', e.target.value)}
                  className="w-full px-3 py-2 text-sm border border-slate-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-all"
                  placeholder="+54 11 4567-8900"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-600 mb-1">
                  Email de Facturación / Administración
                </label>
                <input
                  type="email"
                  value={config.email}
                  onChange={(e) => handleChange('email', e.target.value)}
                  className="w-full px-3 py-2 text-sm border border-slate-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-all"
                  placeholder="administracion@gymstudio.com"
                />
              </div>

            </div>
          </div>
        )}

        {/* PESTAÑA 2: CONTABILIDAD Y FACTURACIÓN */}
        {tabActiva === 'contable' && (
          <div className="bg-white rounded-xl border border-slate-200/80 p-6 shadow-sm space-y-6">
            <div>
              <h3 className="text-base font-bold text-slate-900 mb-1">
                Reglas Contables y Facturación Automatizada
              </h3>
              <p className="text-xs text-slate-500">
                Alícuota que propone el formulario de facturación y reglas contables del sistema.
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
              
              <div>
                <label className="block text-xs font-semibold text-slate-600 mb-1">
                  Alícuota General de IVA Predeterminada
                </label>
                <select
                  value={config.alicuotaIvaDefecto}
                  onChange={(e) => handleChange('alicuotaIvaDefecto', Number(e.target.value))}
                  className="w-full px-3 py-2 text-sm border border-slate-300 rounded-lg bg-white focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-all"
                >
                  <option value={21}>21% (Tasa General AFIP)</option>
                  <option value={10.5}>10.5% (Tasa Reducida)</option>
                  <option value={27}>27% (Servicios y Telecomunicaciones)</option>
                  <option value={0}>0% (Exento)</option>
                </select>
              </div>

            </div>

            {/* Reglas que el sistema aplica siempre (no se configuran). */}
            <div className="pt-4 border-t border-slate-100">
              <h4 className="text-sm font-bold text-slate-900 mb-2">Cómo registra el sistema</h4>
              <ul className="space-y-1.5 text-xs text-slate-600 list-disc pl-5">
                <li>Moneda: pesos argentinos (ARS).</li>
                <li>Los asientos se numeran solos, en orden correlativo, en el Libro Diario.</li>
                <li>Cada factura genera su asiento al emitirse: Compras + IVA Crédito Fiscal a Caja/Banco/Proveedores, y Caja/Banco/Deudores a Ventas + IVA Débito Fiscal.</li>
                <li>Las ventas se numeran por tipo de comprobante y punto de venta; las compras llevan el número del proveedor.</li>
                <li>Anular o editar una factura deja su asiento como Anulado: el IVA Crédito Fiscal nunca va al Haber ni el Débito Fiscal al Debe.</li>
                <li>Las compras de productos nuevos los dan de alta en el Libro Stock; las ventas descuentan stock.</li>
              </ul>
            </div>

          </div>
        )}

        {/* PESTAÑA 3: SEGURIDAD Y SISTEMA */}
        {tabActiva === 'seguridad' && (
          <div className="bg-white rounded-xl border border-slate-200/80 p-6 shadow-sm space-y-6">
            <div>
              <h3 className="text-base font-bold text-slate-900 mb-1">
                Políticas de Sesión y Contraseñas
              </h3>
              <p className="text-xs text-slate-500">
                Cierre automático de la sesión por inactividad y exigencia de contraseñas seguras al crear usuarios.
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
              <div>
                <label className="block text-xs font-semibold text-slate-600 mb-1">
                  Tiempo de Inactividad para Cierre de Sesión
                </label>
                <select
                  value={config.tiempoInactividadMinutos}
                  onChange={(e) => handleChange('tiempoInactividadMinutos', Number(e.target.value))}
                  className="w-full px-3 py-2 text-sm border border-slate-300 rounded-lg bg-white focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-all"
                >
                  <option value={15}>15 Minutos (Máxima seguridad)</option>
                  <option value={30}>30 Minutos (Estándar)</option>
                  <option value={60}>60 Minutos (Recomendado)</option>
                  <option value={0}>Sin límite (Ideal para demos en vivo)</option>
                </select>
              </div>
            </div>

            <div className="pt-4 border-t border-slate-100 space-y-4">
              <div className="flex items-start justify-between gap-4 p-4 rounded-xl bg-slate-50 border border-slate-200/80">
                <div>
                  <h4 className="text-sm font-bold text-slate-900">
                    Exigir Contraseñas Seguras
                  </h4>
                  <p className="text-xs text-slate-500 mt-0.5">
                    Al crear o cambiar la clave de un usuario, exige al menos 8 caracteres con letras y números.
                  </p>
                </div>
                <label className="relative inline-flex items-center cursor-pointer shrink-0 mt-1">
                  <input
                    type="checkbox"
                    checked={config.exigirPasswordSegura}
                    onChange={(e) => handleChange('exigirPasswordSegura', e.target.checked)}
                    className="sr-only peer"
                  />
                  <div className="w-11 h-6 bg-slate-300 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-indigo-600"></div>
                </label>
              </div>

            </div>

          </div>
        )}

        {/* PESTAÑA 4: COPIA DE SEGURIDAD (BACKUP) */}
        {tabActiva === 'backup' && (
          <div className="bg-white rounded-xl border border-slate-200/80 p-6 shadow-sm space-y-6">
            <div>
              <h3 className="text-base font-bold text-slate-900 mb-1">
                Respaldo Local y Portabilidad de Parámetros
              </h3>
              <p className="text-xs text-slate-500">
                Exportá la configuración actual en un archivo seguro JSON o restauralo en caso de requerir volver a un punto previo.
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              
              {/* Tarjeta Descargar */}
              <div className="p-5 rounded-xl border border-slate-200 bg-slate-50/50 flex flex-col justify-between">
                <div>
                  <div className="p-2.5 bg-indigo-50 text-indigo-600 rounded-lg w-fit mb-3">
                    <Download className="h-5 w-5" />
                  </div>
                  <h4 className="font-bold text-slate-900 text-sm">
                    Descargar Copia de Seguridad
                  </h4>
                  <p className="text-xs text-slate-500 mt-1">
                    Crea un archivo snapshot descargable con todos los datos fiscales, alícuotas y reglas configuradas.
                  </p>
                </div>
                <div className="mt-5">
                  <button
                    type="button"
                    onClick={handleExportarJSON}
                    className="inline-flex items-center gap-2 px-4 py-2 text-xs font-semibold text-white bg-marca-600 rounded-lg hover:bg-marca-700 shadow-sm transition-all active:scale-95"
                  >
                    <Download className="h-4 w-4" />
                    <span>Descargar Backup JSON</span>
                  </button>
                </div>
              </div>

              {/* Tarjeta Importar */}
              <div className="p-5 rounded-xl border border-slate-200 bg-slate-50/50 flex flex-col justify-between">
                <div>
                  <div className="p-2.5 bg-emerald-50 text-emerald-600 rounded-lg w-fit mb-3">
                    <Upload className="h-5 w-5" />
                  </div>
                  <h4 className="font-bold text-slate-900 text-sm">
                    Restaurar desde Archivo
                  </h4>
                  <p className="text-xs text-slate-500 mt-1">
                    Seleccioná un archivo JSON previamente exportado para restaurar los parámetros de la empresa al instante.
                  </p>
                </div>
                <div className="mt-5">
                  <label className="inline-flex items-center gap-2 px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 shadow-sm transition-all cursor-pointer">
                    <Upload className="h-4 w-4" />
                    <span>Seleccionar Archivo JSON</span>
                    <input
                      type="file"
                      accept=".json"
                      onChange={handleImportarJSON}
                      className="sr-only"
                    />
                  </label>
                </div>
              </div>

            </div>

            <div className="p-4 rounded-xl bg-blue-50/60 border border-blue-200 flex items-start gap-3">
              <AlertCircle className="h-5 w-5 text-blue-600 shrink-0 mt-0.5" />
              <p className="text-xs text-blue-800">
                La configuración se almacena localmente en tu navegador de forma persistente. Incluso al refrescar la página con F5 o reiniciar la aplicación, todos los parámetros se mantendrán intactos.
              </p>
            </div>

          </div>
        )}

      </form>

    </div>
  );
}
