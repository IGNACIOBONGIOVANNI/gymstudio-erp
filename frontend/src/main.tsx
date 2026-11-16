import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { Manual } from './features/manual/Manual'
import { RUTA_MANUAL } from './acercaDe'
import { instalarInterceptorAuth } from './api/authFetch'
import { CursorDelGimnasio } from './components/CursorDelGimnasio'

// Agrega el token JWT a todas las llamadas a /api/ antes de montar la app.
instalarInterceptorAuth()

// El manual de usuario se abre en su propia ventana, en /manual, y no pide iniciar sesión.
const esManual = window.location.pathname.replace(/\/+$/, '') === RUTA_MANUAL

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    {esManual ? <Manual /> : <App />}
    <CursorDelGimnasio />
  </StrictMode>,
)
