// Lee el motivo de un error devuelto por la API para mostrarlo al usuario.
// El backend responde ProblemDetails ({ title, detail }); algunos endpoints viejos
// devuelven { mensaje }. Si no hay nada legible se usa el texto por defecto.
export async function mensajeDeError(res: Response, porDefecto: string): Promise<string> {
  try {
    const texto = await res.text();
    if (!texto) return porDefecto;
    try {
      const cuerpo = JSON.parse(texto);
      return cuerpo?.detail || cuerpo?.mensaje || cuerpo?.Mensaje || cuerpo?.title || porDefecto;
    } catch {
      return texto.length < 200 ? texto : porDefecto;
    }
  } catch {
    return porDefecto;
  }
}

export const ERROR_DE_CONEXION = 'No se pudo conectar con el servidor. Verificá que el sistema esté levantado.';
