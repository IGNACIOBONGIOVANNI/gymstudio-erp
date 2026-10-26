export interface ConfiguracionERP {
  // Datos Fiscales y de la Empresa
  razonSocial: string;
  nombreFantasia: string;
  cuit: string;
  condicionIva: 'Responsable Inscripto' | 'Monotributo' | 'Exento';
  puntoVenta: string;
  direccion: string;
  telefono: string;
  email: string;
  inicioActividades: string;
  ingresosBrutos: string;

  // Parámetros Contables y Facturación
  monedaPrincipal: string;
  alicuotaIvaDefecto: number;
  mesCierreEjercicio: string;
  numeracionAutomaticaAsientos: boolean;
  asientosAutomaticosFacturacion: boolean;
  metodoCosteoInventario: 'PPP' | 'FIFO';

  // Seguridad y Sistema
  tiempoInactividadMinutos: number;
  exigirPasswordSegura: boolean;
  modoDemostracion: boolean;
  ultimaActualizacion?: string;
}
