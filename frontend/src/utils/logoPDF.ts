import type jsPDF from 'jspdf';

// Misma marca que components/LogoGym.tsx, en vectores y en negro para que fotocopie bien.
// (x, y) es la esquina superior izquierda y `lado` el tamaño del cuadrado en mm.
export function dibujarLogo(doc: jsPDF, x: number, y: number, lado: number) {
  const k = lado / 100;
  const punto = (grados: number, radio: number): [number, number] => {
    const a = (grados * Math.PI) / 180;
    return [x + (50 + radio * Math.cos(a)) * k, y + (50 + radio * Math.sin(a)) * k];
  };
  // Sector de corona: de -40° a -360° (322° pasando por arriba, la izquierda y abajo).
  const pasos = 72;
  const contorno: [number, number][] = [];
  for (let i = 0; i <= pasos; i++) contorno.push(punto(-40 - (320 * i) / pasos, 40));
  for (let i = pasos; i >= 0; i--) contorno.push(punto(-40 - (320 * i) / pasos, 24));
  const [x0, y0] = contorno[0];
  const tramos = contorno.slice(1).map(([px, py], i) => [px - contorno[i][0], py - contorno[i][1]]);

  doc.setFillColor(0, 0, 0);
  doc.lines(tramos, x0, y0, [1, 1], 'F', true);
  doc.rect(x + 52 * k, y + 42 * k, 38 * k, 12 * k, 'F');
  doc.roundedRect(x + 84 * k, y + 33 * k, 9 * k, 30 * k, 2 * k, 2 * k, 'F');
}
