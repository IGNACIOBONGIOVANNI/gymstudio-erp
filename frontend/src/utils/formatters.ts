export function formatearFechaLocal(fechaDb: string | Date | null | undefined): string {
  if (!fechaDb) return "";
  
  // Convertimos a string por si nos pasan un objeto Date
  const dateStr = typeof fechaDb === 'string' ? fechaDb : fechaDb.toISOString();
  
  // Extraemos la parte 'YYYY-MM-DD' descartando la hora y la zona horaria ('T' y 'Z')
  const fechaIsoDate = dateStr.split('T')[0]; // "2026-09-03"
  const parts = fechaIsoDate.split('-'); // ["2026", "09", "03"]
  
  if (parts.length === 3) {
    // Retornamos DD/MM/YYYY
    return `${parts[2]}/${parts[1]}/${parts[0]}`;
  }
  
  // Fallback de seguridad
  return new Date(dateStr).toLocaleDateString("es-AR");
}

/** Fecha de hoy en hora local como "aaaa-mm-dd" (para inputs de tipo date).
 *  toISOString() usa UTC: después de las 21 h en Argentina devolvería el día siguiente. */
export function hoyLocal(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}
