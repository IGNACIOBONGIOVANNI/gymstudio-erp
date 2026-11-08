// Índice del manual de usuario. Sin dependencias a propósito: se prueba con el ejecutor de pruebas de Node.
// El contenido de cada sección está en secciones.tsx, que exige una entrada por cada id de acá.

export const SECCIONES = [
  { id: 'empezar', titulo: 'Antes de empezar' },
  { id: 'roles', titulo: 'Qué puede hacer cada usuario' },
  { id: 'resumen', titulo: 'Resumen de inicio' },
  { id: 'recepcion', titulo: 'Recepción: ingreso de socios y cobro de cuotas' },
  { id: 'socios', titulo: 'Socios, planes y rutinas' },
  { id: 'calendario', titulo: 'Calendario' },
  { id: 'entrenadores', titulo: 'Entrenadores' },
  { id: 'facturar', titulo: 'Facturar ventas y compras' },
  { id: 'libros-iva', titulo: 'Libros de Ventas y de Compras' },
  { id: 'cuentas', titulo: 'Cuentas a cobrar y a pagar' },
  { id: 'tesoreria', titulo: 'Caja y Banco' },
  { id: 'stock', titulo: 'Productos y stock' },
  { id: 'terceros', titulo: 'Clientes y proveedores' },
  { id: 'contabilidad', titulo: 'Contabilidad: Diario, Mayor, Balance y asientos' },
  { id: 'reportes', titulo: 'Reportes en PDF y planillas de Excel' },
  { id: 'administracion', titulo: 'Administración: usuarios, configuración y auditoría' },
  { id: 'ayudas', titulo: 'Asistente y campanita de avisos' },
  { id: 'respaldo', titulo: 'Copias de seguridad' },
  { id: 'politica', titulo: 'Política de seguridad' },
  { id: 'glosario', titulo: 'Glosario' },
  { id: 'problemas', titulo: 'Si algo no sale' },
] as const

export type IdSeccion = (typeof SECCIONES)[number]['id']
