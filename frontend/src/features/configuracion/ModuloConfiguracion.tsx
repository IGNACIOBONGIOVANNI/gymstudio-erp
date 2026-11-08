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
import { compararConfiguracion, leerArchivoDeConfiguracion, type Comparacion, type ValorInvalido } from './compararConfiguracion';
import { VistaPreviaDeConfiguracion } from './VistaPreviaDeConfiguracion';
import { VistaPreviaDeCopia } from './VistaPreviaDeCopia';
import { CopiasAutomaticas } from './CopiasAutomaticas';
import { compararCopia, descargarCopia, guardarArchivo } from '../../api/copiasApi';
import { nombreDeArchivoDeCopia, type ComparacionDeCopia } from './copiasUtil';
import { abrirManual } from '../../acercaDe';

// Lo que se muestra antes de cargar un archivo de configuración: qué cambiaría, sin haber cambiado nada todavía.
interface VistaPrevia extends Comparacion {
  nombreArchivo: string;
  guardadoEl?: string;
  ignorados: string[];
  invalidos: ValorInvalido[];
  nueva: Partial<ConfiguracionERP>;
  actual: ConfiguracionERP;
  hayCambiosSinGuardar: boolean;
}

export default function ModuloConfiguracion() {
  const [config, setConfig] = useState<ConfiguracionERP>(obtenerConfiguracion());
  const [tabActiva, setTabActiva] = useState<'empresa' | 'contable' | 'seguridad' | 'backup'>('empresa');
  const [guardadoReciente, setGuardadoReciente] = useState(false);
  const [vistaPrevia, setVistaPrevia] = useState<VistaPrevia | null>(null);
  const [copia, setCopia] = useState<{ archivo: File; comparacion: ComparacionDeCopia } | null>(null);
  const [descargandoCopia, setDescargandoCopia] = useState(false);
  const [comparandoCopia, setComparandoCopia] = useState(false);

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
    toast.success('Configuración descargada (los datos contables se copian con scripts\\backup.ps1)');
  };

  // Copia completa del sistema (todas las tablas): se descarga, o se elige una para compararla con el sistema actual.
  const handleDescargarCopia = async () => {
    setDescargandoCopia(true);
    try {
      guardarArchivo(await descargarCopia(), nombreDeArchivoDeCopia(new Date()));
      toast.success('Copia completa descargada. Guardala en un lugar privado: incluye las contraseñas (con hash).');
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'No se pudo generar la copia de seguridad.');
    } finally {
      setDescargandoCopia(false);
    }
  };

  const handleElegirCopia = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const input = event.target;
    const archivo = input.files?.[0];
    input.value = '';
    if (!archivo) return;
    if (archivo.size > 50 * 1024 * 1024) {
      toast.error('La copia supera los 50 MB permitidos.');
      return;
    }
    setComparandoCopia(true);
    try {
      setCopia({ archivo, comparacion: await compararCopia(archivo) });
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'No se pudo comparar la copia.');
    } finally {
      setComparandoCopia(false);
    }
  };

  // Elegir un archivo NO lo carga: primero se lee, se valida y se muestra qué cambiaría contra lo que está en uso.
  const handleImportarJSON = (event: React.ChangeEvent<HTMLInputElement>) => {
    const input = event.target;
    const file = input.files?.[0];
    input.value = ''; // para poder elegir el mismo archivo otra vez
    if (!file) return;
    if (file.size > 100_000) {
      toast.error('El archivo es demasiado grande para ser una configuración.');
      return;
    }

    const reader = new FileReader();
    reader.onerror = () => toast.error('No se pudo leer el archivo.');
    reader.onload = () => {
      const lectura = leerArchivoDeConfiguracion(String(reader.result ?? ''));
      if (!lectura.ok) {
        toast.error(lectura.error);
        return;
      }
      const enUso = obtenerConfiguracion();
      setVistaPrevia({
        ...compararConfiguracion(enUso, lectura.config, lectura.invalidos),
        nombreArchivo: file.name,
        guardadoEl: lectura.guardadoEl,
        ignorados: lectura.ignorados,
        invalidos: lectura.invalidos,
        nueva: lectura.config,
        actual: enUso,
        hayCambiosSinGuardar: compararConfiguracion(enUso, config).cambios.length > 0,
      });
    };
    reader.readAsText(file);
  };

  const handleCargarArchivo = () => {
    if (!vistaPrevia) return;
    try {
      const actualizada = guardarConfiguracion({ ...vistaPrevia.actual, ...vistaPrevia.nueva });
      setConfig(actualizada);
      toast.success(`Configuración cargada: ${vistaPrevia.cambios.length} ${vistaPrevia.cambios.length === 1 ? 'dato actualizado' : 'datos actualizados'}`);
    } catch {
      toast.error('Error al guardar la configuración');
    }
    setVistaPrevia(null);
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
                    Siempre activa: al crear o cambiar la clave de un usuario, el servidor exige al menos 8 caracteres, una mayúscula, un número,
                    un carácter especial y que no haya números consecutivos (como 12 o 98).
                  </p>
                </div>
                <label className="relative inline-flex items-center cursor-not-allowed shrink-0 mt-1 opacity-70" title="No se puede desactivar: la exige el servidor">
                  <input
                    type="checkbox"
                    checked
                    disabled
                    readOnly
                    aria-label="Contraseñas seguras: siempre activa"
                    className="sr-only peer"
                  />
                  <div className="w-11 h-6 bg-slate-300 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-indigo-600"></div>
                </label>
              </div>

              <div className="flex items-start justify-between gap-4 p-4 rounded-xl bg-slate-50 border border-slate-200/80">
                <div>
                  <h4 className="text-sm font-bold text-slate-900">
                    Política de seguridad
                  </h4>
                  <p className="text-xs text-slate-500 mt-0.5">
                    Las reglas con las que se protege la información: qué hace el sistema solo y qué le toca a cada persona.
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => abrirManual('politica')}
                  className="shrink-0 inline-flex items-center gap-2 px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 shadow-sm transition-all active:scale-95"
                >
                  <ShieldCheck className="h-4 w-4" />
                  <span>Ver política de seguridad</span>
                </button>
              </div>

            </div>

          </div>
        )}

        {/* PESTAÑA 4: COPIA DE SEGURIDAD (BACKUP) */}
        {tabActiva === 'backup' && (
          <div className="bg-white rounded-xl border border-slate-200/80 p-6 shadow-sm space-y-6">
            <div>
              <h3 className="text-base font-bold text-slate-900 mb-1">
                Copia de seguridad
              </h3>
              <p className="text-xs text-slate-500">
                Hay dos cosas para resguardar: los datos contables (facturas, asientos, caja, banco, stock y usuarios) y la configuración de esta pantalla.
              </p>
            </div>

            {/* Los datos contables viven en PostgreSQL: se copian con los scripts del proyecto. */}
            <div className="p-5 rounded-xl border border-amber-300 bg-amber-50/70 space-y-3">
              <div className="flex items-start gap-3">
                <AlertCircle className="h-5 w-5 text-amber-600 shrink-0 mt-0.5" />
                <div>
                  <h4 className="font-bold text-slate-900 text-sm">Datos contables (lo más importante)</h4>
                  <p className="text-xs text-slate-700 mt-1">
                    Las facturas, los asientos y el resto de los datos están en la base de datos. Se copian desde esta pantalla
                    (<strong>Copia completa del sistema</strong>, y <strong>Copia automática</strong> para que se haga sola, más abajo) o, también, desde la carpeta del proyecto, en una terminal de PowerShell:
                  </p>
                </div>
              </div>
              <pre className="text-xs bg-white border border-amber-200 rounded-lg p-3 overflow-x-auto text-slate-800">{[
                '.\\scripts\\backup.ps1                  # hace la copia en la carpeta "backups"',
                '.\\scripts\\programar-backup.ps1        # la hace sola todos los días a las 22:00',
                '.\\scripts\\comparar-backup.ps1 -Archivo .\\backups\\gymstudio_AAAAMMDD_HHMMSS.dump   # ¿coincide con el sistema actual?',
                '.\\scripts\\restaurar.ps1 -Archivo .\\backups\\gymstudio_AAAAMMDD_HHMMSS.dump -Confirmar',
              ].join('\n')}</pre>
              <p className="text-xs text-slate-600">
                Cada copia se verifica y trae su huella SHA-256 y un manifiesto con la versión del sistema y la huella de los datos de cada tabla: con <strong>comparar-backup.ps1</strong> ves si es idéntica al sistema actual o qué cambió. Al restaurar se hace la misma comparación antes de reemplazar nada. Guardá también la carpeta <strong>backups</strong> en un pendrive o en la nube: si se rompe la PC, las copias que están en ella se pierden también.
              </p>
            </div>

            <h4 className="font-bold text-slate-900 text-sm pt-2">Copia completa del sistema</h4>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="p-5 rounded-xl border border-slate-200 bg-slate-50/50 flex flex-col justify-between">
                <div>
                  <div className="p-2.5 bg-indigo-50 text-indigo-600 rounded-lg w-fit mb-3">
                    <Database className="h-5 w-5" />
                  </div>
                  <h4 className="font-bold text-slate-900 text-sm">Descargar copia completa</h4>
                  <p className="text-xs text-slate-500 mt-1">
                    Descarga un archivo .zip con <strong>todos los datos</strong>: facturas, asientos, caja, banco, stock, socios, usuarios y auditoría. Incluye las
                    contraseñas (con hash): guardalo en un lugar privado.
                  </p>
                </div>
                <div className="mt-5">
                  <button
                    type="button"
                    onClick={handleDescargarCopia}
                    disabled={descargandoCopia}
                    className="inline-flex items-center gap-2 px-4 py-2 text-xs font-semibold text-white bg-marca-600 rounded-lg hover:bg-marca-700 shadow-sm transition-all active:scale-95 disabled:opacity-60"
                  >
                    <Download className="h-4 w-4" />
                    <span>{descargandoCopia ? 'Generando la copia…' : 'Descargar copia completa (.zip)'}</span>
                  </button>
                </div>
              </div>

              <div className="p-5 rounded-xl border border-slate-200 bg-slate-50/50 flex flex-col justify-between">
                <div>
                  <div className="p-2.5 bg-emerald-50 text-emerald-600 rounded-lg w-fit mb-3">
                    <Upload className="h-5 w-5" />
                  </div>
                  <h4 className="font-bold text-slate-900 text-sm">Comparar y cargar una copia</h4>
                  <p className="text-xs text-slate-500 mt-1">
                    Seleccioná una copia descargada antes. Primero ves una vista previa que la compara con el sistema actual, tabla por tabla (facturas, asientos,
                    socios…). Recién después podés decidir si restaurarla.
                  </p>
                </div>
                <div className="mt-5">
                  <label className={`inline-flex items-center gap-2 px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 shadow-sm transition-all ${comparandoCopia ? 'opacity-60' : 'cursor-pointer'}`}>
                    <Upload className="h-4 w-4" />
                    <span>{comparandoCopia ? 'Comparando…' : 'Seleccionar copia (.zip)'}</span>
                    <input type="file" accept=".zip" onChange={handleElegirCopia} disabled={comparandoCopia} className="sr-only" />
                  </label>
                </div>
              </div>
            </div>

            <CopiasAutomaticas />

            <h4 className="font-bold text-slate-900 text-sm pt-2">Configuración de la empresa</h4>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              
              {/* Tarjeta Descargar */}
              <div className="p-5 rounded-xl border border-slate-200 bg-slate-50/50 flex flex-col justify-between">
                <div>
                  <div className="p-2.5 bg-indigo-50 text-indigo-600 rounded-lg w-fit mb-3">
                    <Download className="h-5 w-5" />
                  </div>
                  <h4 className="font-bold text-slate-900 text-sm">
                    Descargar configuración
                  </h4>
                  <p className="text-xs text-slate-500 mt-1">
                    Descarga un archivo con los datos de la empresa y los parámetros de esta pantalla (no incluye datos contables).
                  </p>
                </div>
                <div className="mt-5">
                  <button
                    type="button"
                    onClick={handleExportarJSON}
                    className="inline-flex items-center gap-2 px-4 py-2 text-xs font-semibold text-white bg-marca-600 rounded-lg hover:bg-marca-700 shadow-sm transition-all active:scale-95"
                  >
                    <Download className="h-4 w-4" />
                    <span>Descargar configuración (JSON)</span>
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
                    Cargar configuración
                  </h4>
                  <p className="text-xs text-slate-500 mt-1">
                    Seleccioná un archivo descargado antes para volver a esos datos de la empresa. Antes de cargarlo vas a ver una vista previa que compara la configuración actual con la del archivo. No modifica facturas ni asientos.
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
                La configuración se guarda en este navegador. Si abrís el sistema desde otro navegador u otra PC vas a ver los valores iniciales: descargá el archivo y cargalo ahí.
              </p>
            </div>

          </div>
        )}

      </form>

      {copia && <VistaPreviaDeCopia archivo={copia.archivo} comparacion={copia.comparacion} onCerrar={() => setCopia(null)} />}

      {vistaPrevia && (
        <VistaPreviaDeConfiguracion
          nombreArchivo={vistaPrevia.nombreArchivo}
          guardadoEl={vistaPrevia.guardadoEl}
          actualizadaEl={vistaPrevia.actual.ultimaActualizacion}
          filas={vistaPrevia.filas}
          ignorados={vistaPrevia.ignorados}
          invalidos={vistaPrevia.invalidos}
          hayCambiosSinGuardar={vistaPrevia.hayCambiosSinGuardar}
          onCancelar={() => setVistaPrevia(null)}
          onCargar={handleCargarArchivo}
        />
      )}

    </div>
  );
}
