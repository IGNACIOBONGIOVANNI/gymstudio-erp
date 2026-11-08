// Prepara la foto de un entrenador en el navegador: la recorta al cuadrado central, la achica a 256 × 256 y la codifica como JPEG
// que pese hasta 150 000 bytes. El servidor vuelve a validar todo; esto es para que se suba algo liviano y del tamaño justo.
import { LADO_DE_LA_FOTO, codificarHastaQueEntre, recorteCuadrado } from './entrenadoresUtil';

/** Una foto de celular puede pesar varios MB; más de esto es casi seguro que no es una foto. */
const BYTES_MAXIMOS_DEL_ARCHIVO = 30_000_000;

export async function procesarFoto(archivo: File): Promise<Blob> {
  if (archivo.size > BYTES_MAXIMOS_DEL_ARCHIVO) {
    throw new Error(`El archivo es demasiado grande (más de ${BYTES_MAXIMOS_DEL_ARCHIVO / 1_000_000} MB). Elegí una foto más chica.`);
  }

  let imagen: ImageBitmap;
  try {
    imagen = await createImageBitmap(archivo);
  } catch {
    throw new Error('No se pudo abrir esa imagen. Elegí una foto en formato JPG, PNG o WebP.');
  }

  try {
    const { x, y, lado } = recorteCuadrado(imagen.width, imagen.height);
    if (lado === 0) throw new Error('No se pudo abrir esa imagen. Elegí otra foto.');

    const lienzo = document.createElement('canvas');
    lienzo.width = LADO_DE_LA_FOTO;
    lienzo.height = LADO_DE_LA_FOTO;
    const contexto = lienzo.getContext('2d');
    if (!contexto) throw new Error('Este navegador no puede preparar la foto.');
    // El JPEG no tiene transparencia: lo transparente de un PNG quedaría negro si no se pinta antes un fondo.
    contexto.fillStyle = '#ffffff';
    contexto.fillRect(0, 0, LADO_DE_LA_FOTO, LADO_DE_LA_FOTO);
    contexto.imageSmoothingQuality = 'high';
    contexto.drawImage(imagen, x, y, lado, lado, 0, 0, LADO_DE_LA_FOTO, LADO_DE_LA_FOTO);

    const foto = await codificarHastaQueEntre((calidad) => new Promise<Blob>((resolver, rechazar) => {
      lienzo.toBlob((blob) => (blob ? resolver(blob) : rechazar(new Error('No se pudo preparar la foto.'))), 'image/jpeg', calidad);
    }));
    // Por si el navegador no sabe codificar JPEG y devolvió otro formato (el servidor solo acepta JPEG).
    if (foto.type !== 'image/jpeg') throw new Error('Este navegador no puede preparar la foto en JPEG.');
    return foto;
  } finally {
    imagen.close();
  }
}
