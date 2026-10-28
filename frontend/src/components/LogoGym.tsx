// Marca de GymStudio: una "G" armada con un disco de pesas y una barra cuyo extremo es otro disco.
// La misma geometría (100 x 100) se usa en utils/logoPDF.ts y en public/favicon.svg.
interface Props {
  className?: string
  /** Dibuja el cuadrado oscuro de fondo (para usarlo sobre blanco). */
  conFondo?: boolean
}

export function LogoGym({ className = 'h-8 w-8', conFondo = false }: Props) {
  return (
    <svg viewBox="0 0 100 100" className={className} role="img" aria-label="GymStudio" xmlns="http://www.w3.org/2000/svg">
      {conFondo && <rect width="100" height="100" rx="22" fill="#1e2422" />}
      <g transform={conFondo ? 'translate(11 11) scale(.78)' : undefined}>
        <path d="M80.64 24.29A40 40 0 1 0 90 50L74 50A24 24 0 1 1 68.38 34.57Z" fill="#ffffff" />
        <rect x="52" y="42" width="38" height="12" fill="#ffffff" />
        <rect x="84" y="33" width="9" height="30" rx="2" fill="#47a27c" />
      </g>
    </svg>
  )
}
