import type { ConfiguracionERP } from './types';

const STORAGE_KEY = 'gymstudio_configuracion_v1';

export const CONFIGURACION_INICIAL: ConfiguracionERP = {
  // Datos Fiscales y de la Empresa
  razonSocial: 'GYM STUDIO S.R.L.',
  nombreFantasia: 'Gym Studio Fitness & Health',
  cuit: '30-71234567-9',
  condicionIva: 'Responsable Inscripto',
  puntoVenta: '0001',
  direccion: 'Av. Corrientes 1234, Ciudad Autónoma de Buenos Aires',
  telefono: '+54 11 4567-8900',
  email: 'administracion@gymstudio.com',
  inicioActividades: '2020-01-01',
  ingresosBrutos: '30-71234567-9 (Convenio Multilateral 901)',

  // Parámetros Contables y Facturación
  monedaPrincipal: 'ARS - Peso Argentino ($)',
  alicuotaIvaDefecto: 21,
  mesCierreEjercicio: 'Diciembre',
  numeracionAutomaticaAsientos: true,
  asientosAutomaticosFacturacion: true,
  metodoCosteoInventario: 'PPP',

  // Seguridad y Sistema
  tiempoInactividadMinutos: 60,
  exigirPasswordSegura: true,
  modoDemostracion: true,
  ultimaActualizacion: new Date().toLocaleDateString('es-AR') + ' 12:00',
};

export function obtenerConfiguracion(): ConfiguracionERP {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(CONFIGURACION_INICIAL));
      return CONFIGURACION_INICIAL;
    }
    const parsed = JSON.parse(raw);
    return { ...CONFIGURACION_INICIAL, ...parsed };
  } catch (error) {
    console.error('Error al leer configuración de localStorage:', error);
    return CONFIGURACION_INICIAL;
  }
}

export function guardarConfiguracion(nueva: ConfiguracionERP): ConfiguracionERP {
  const ahora = new Date();
  const fechaStr = `${ahora.toLocaleDateString('es-AR')} ${ahora.toLocaleTimeString('es-AR', {
    hour: '2-digit',
    minute: '2-digit',
  })}`;

  const configActualizada: ConfiguracionERP = {
    ...nueva,
    ultimaActualizacion: fechaStr,
  };

  localStorage.setItem(STORAGE_KEY, JSON.stringify(configActualizada));
  return configActualizada;
}

export function restablecerConfiguracionPorDefecto(): ConfiguracionERP {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(CONFIGURACION_INICIAL));
  return CONFIGURACION_INICIAL;
}
