import { useRef, useState } from 'react';
import { PAIS_POR_DEFECTO, paisPorCodigo } from '../utils/paises';
import { armarTelefono, interpretarEscrito, interpretarPegado, leerTelefono, maximoDeDigitos } from '../utils/telefono';
import { SelectorDePais } from './SelectorDePais';

interface Props {
  /** Id del campo del número: el `<label htmlFor>` del formulario apunta acá. */
  id: string;
  /** Lo guardado: «+54 1155551234», o vacío si no hay teléfono. */
  value: string;
  onChange: (valor: string) => void;
  /** Cómo se llama el dato, para el lector de pantalla: «teléfono», «WhatsApp». */
  dato: string;
  invalido?: boolean;
  describedBy?: string;
  disabled?: boolean;
  placeholder?: string;
}

/**
 * Teléfono con el país a la izquierda (bandera y código) y el número a la derecha, que solo acepta dígitos. Lo que sale es el teléfono
 * como se guarda («+54 1155551234») o vacío si no hay número. Pegar un número internacional («+54 9 11 5555-1234») elige también el país.
 */
export function CampoDeTelefono({ id, value, onChange, dato, invalido, describedBy, disabled, placeholder = 'Solo números' }: Props) {
  // El país que eligió la persona: se recuerda aunque todavía no haya número (que es cuando `value` no sabe de qué país es) y también
  // para los códigos que comparten varios países (+1 es de Estados Unidos y de Canadá).
  const [elegido, setElegido] = useState<string | null>(null);
  const numeroRef = useRef<HTMLInputElement>(null);

  const leido = leerTelefono(value, elegido ?? PAIS_POR_DEFECTO);
  const pais = elegido && (value === '' || paisPorCodigo(elegido)?.prefijo === paisPorCodigo(leido.pais)?.prefijo) ? elegido : leido.pais;

  const cambiar = (nuevoPais: string, numero: string) => {
    setElegido(nuevoPais);
    onChange(armarTelefono(nuevoPais, numero));
  };

  return (
    <div className="flex gap-2">
      <SelectorDePais pais={pais} dato={dato} disabled={disabled}
        onElegir={(codigo) => { cambiar(codigo, leido.numero); numeroRef.current?.focus(); }} />
      <input ref={numeroRef} id={id} type="text" inputMode="numeric" autoComplete="off" value={leido.numero} disabled={disabled}
        maxLength={maximoDeDigitos(pais) + 20}
        onChange={(e) => { const partes = interpretarEscrito(e.target.value, pais); cambiar(partes.pais, partes.numero); }}
        onPaste={(e) => {
          const pegado = e.clipboardData.getData('text');
          if (!pegado.trim().startsWith('+')) return;   // un número sin país entra por el camino de siempre
          e.preventDefault();
          const partes = interpretarPegado(pegado, pais);
          cambiar(partes.pais, partes.numero);
        }}
        aria-invalid={invalido} aria-describedby={describedBy} placeholder={placeholder}
        className={`min-w-0 flex-1 rounded-lg border px-3 py-2 text-sm tabular-nums focus:outline-none focus:ring-2 disabled:cursor-not-allowed disabled:opacity-60 ${
          invalido ? 'border-red-400 focus:border-red-400 focus:ring-red-400' : 'border-slate-300 focus:border-marca-500 focus:ring-marca-500'}`} />
    </div>
  );
}
