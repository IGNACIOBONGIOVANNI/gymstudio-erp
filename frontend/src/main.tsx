import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { instalarInterceptorAuth } from './api/authFetch'

// Agrega el token JWT a todas las llamadas a /api/ antes de montar la app.
instalarInterceptorAuth()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
