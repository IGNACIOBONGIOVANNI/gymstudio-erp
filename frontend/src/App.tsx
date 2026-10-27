import { useState, useEffect, useMemo, type ReactNode } from 'react'
import toast, { Toaster } from 'react-hot-toast'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import {
  LogOut, Menu, X, FileText, DoorOpen, Users, CalendarRange, Receipt, BookOpenText,
  ShoppingCart, Building2, Truck, Wallet, Landmark, Package, History, BookText,
  Scale, BarChart3, FilePlus2, UserCog, Settings, ShieldCheck,
} from 'lucide-react'
import { AsientoForm } from './asientos/AsientoForm'
import { ListadoAsientos } from './asientos/ListadoAsientos'
import { LibroMayor } from './asientos/LibroMayor'
import { BalanceGeneralTab } from './asientos/BalanceGeneralTab'
import ModuloClientes from './features/clientes/ModuloClientes'
import ModuloProveedores from './features/proveedores/ModuloProveedores'
import ModuloUsuarios from './features/usuarios/ModuloUsuarios'
import ModuloConfiguracion from './features/configuracion/ModuloConfiguracion'
import ModuloAuditoria from './features/auditoria/ModuloAuditoria'
import { LibroVenta } from './features/ventas/components/LibroVenta'
import { LibroCompra } from './features/compras/components/LibroCompra'
import { LibroCaja } from './features/caja/components/LibroCaja'
import { LibroBanco } from './features/banco/components/LibroBanco'
import { PlanesGym } from './features/gimnasio/components/PlanesGym'
import { SociosGym } from './features/gimnasio/components/SociosGym'
import { RecepcionGym } from './features/gimnasio/components/RecepcionGym'
import { Facturacion } from './features/ventas/components/Facturacion'
import { LibroStock } from './features/inventario/components/LibroStock'
import { GestionProductos } from './features/inventario/components/GestionProductos'
import { obtenerConfiguracion } from './features/configuracion/configuracionStorage'

import { AuthProvider, useAuth } from './context/AuthContext'
import { Login } from './components/Login'
import { ModalGenerarReporte } from './components/reportes/ModalGenerarReporte'
import { useCuentas } from './asientos/hooks/useCuentas'

const queryClient = new QueryClient()

type Rol = 'Administrador' | 'Contador' | 'Recepcion'

interface Vista {
  id: string
  titulo: string
  icono: ReactNode
  /** Roles que la ven en el menú (la API controla los permisos reales). */
  roles?: Rol[]
  /** Las pantallas que son un formulario suelto se muestran dentro de una tarjeta. */
  tarjeta?: boolean
}

interface Grupo {
  titulo: string
  vistas: Vista[]
}

const icono = 'h-4 w-4 shrink-0'
const GESTION: Rol[] = ['Administrador', 'Contador']

const MENU: Grupo[] = [
  {
    titulo: 'Recepción',
    vistas: [
      { id: 'recepcion', titulo: 'Recepción', icono: <DoorOpen className={icono} /> },
      { id: 'socios', titulo: 'Socios', icono: <Users className={icono} /> },
      { id: 'planes', titulo: 'Planes', icono: <CalendarRange className={icono} /> },
    ],
  },
  {
    titulo: 'Ventas y compras',
    vistas: [
      { id: 'facturar', titulo: 'Facturar', icono: <Receipt className={icono} />, tarjeta: true },
      { id: 'ventas', titulo: 'Libro de Ventas', icono: <BookOpenText className={icono} /> },
      { id: 'compras', titulo: 'Libro de Compras', icono: <ShoppingCart className={icono} /> },
      { id: 'clientes', titulo: 'Clientes', icono: <Building2 className={icono} /> },
      { id: 'proveedores', titulo: 'Proveedores', icono: <Truck className={icono} /> },
    ],
  },
  {
    titulo: 'Tesorería',
    vistas: [
      { id: 'caja', titulo: 'Caja', icono: <Wallet className={icono} /> },
      { id: 'banco', titulo: 'Banco', icono: <Landmark className={icono} /> },
    ],
  },
  {
    titulo: 'Stock',
    vistas: [
      { id: 'productos', titulo: 'Productos', icono: <Package className={icono} /> },
      { id: 'stock', titulo: 'Libro Stock', icono: <History className={icono} /> },
    ],
  },
  {
    titulo: 'Contabilidad',
    vistas: [
      { id: 'diario', titulo: 'Libro Diario', icono: <BookText className={icono} /> },
      { id: 'mayor', titulo: 'Libro Mayor', icono: <Scale className={icono} /> },
      { id: 'balance', titulo: 'Balance General', icono: <BarChart3 className={icono} /> },
      { id: 'asiento', titulo: 'Nuevo asiento', icono: <FilePlus2 className={icono} />, roles: GESTION, tarjeta: true },
    ],
  },
  {
    titulo: 'Administración',
    vistas: [
      { id: 'usuarios', titulo: 'Usuarios', icono: <UserCog className={icono} />, roles: ['Administrador'] },
      { id: 'configuracion', titulo: 'Configuración', icono: <Settings className={icono} />, roles: ['Administrador'] },
      { id: 'auditoria', titulo: 'Auditoría', icono: <ShieldCheck className={icono} />, roles: ['Administrador'] },
    ],
  },
]

function iniciales(nombre: string) {
  return nombre.split(' ').filter(Boolean).map((n) => n[0]).slice(0, 2).join('').toUpperCase()
}

const ETIQUETA_ROL: Record<string, string> = {
  Administrador: 'Administrador',
  Contador: 'Contador',
  Recepcion: 'Recepción',
}

function AppContent() {
  const { user, logout } = useAuth()
  const { data: cuentas = [] } = useCuentas(Boolean(user))

  // Cierre de sesión por inactividad, según Configuración > Seguridad (0 = sin límite).
  // Los minutos se leen en cada reinicio: un cambio en la Configuración aplica enseguida.
  useEffect(() => {
    if (!user) return
    let temporizador: number | undefined
    const reiniciar = () => {
      window.clearTimeout(temporizador)
      const minutos = Number(obtenerConfiguracion().tiempoInactividadMinutos) || 0
      if (minutos <= 0) return
      temporizador = window.setTimeout(() => {
        toast(`Sesión cerrada por ${minutos} minutos sin actividad`, { icon: '🔒', duration: 6000 })
        logout()
      }, minutos * 60_000)
    }
    const eventos = ['mousemove', 'keydown', 'click', 'scroll', 'touchstart'] as const
    eventos.forEach((e) => window.addEventListener(e, reiniciar, { passive: true }))
    reiniciar()
    return () => {
      window.clearTimeout(temporizador)
      eventos.forEach((e) => window.removeEventListener(e, reiniciar))
    }
  }, [user, logout])
  const rol = user?.rol as Rol | undefined

  // Menú filtrado según el rol del usuario.
  const menu = useMemo(
    () =>
      MENU.map((g) => ({ ...g, vistas: g.vistas.filter((v) => !v.roles || (rol && v.roles.includes(rol))) }))
        .filter((g) => g.vistas.length > 0),
    [rol]
  )
  const vistas = menu.flatMap((g) => g.vistas)

  const [vistaId, setVistaId] = useState<string>(rol === 'Recepcion' ? 'recepcion' : 'diario')
  const [menuAbierto, setMenuAbierto] = useState(false)
  const [mostrarReportes, setMostrarReportes] = useState(false)

  // Si el rol cambia (otro usuario inicia sesión) y la vista ya no está permitida, volver al inicio.
  useEffect(() => {
    if (user && !vistas.some((v) => v.id === vistaId)) {
      setVistaId(rol === 'Recepcion' ? 'recepcion' : 'diario')
    }
  }, [user, rol, vistaId, vistas])

  if (!user) {
    return <Login />
  }

  const vista = vistas.find((v) => v.id === vistaId) ?? vistas[0]
  const negocio = obtenerConfiguracion().nombreFantasia || obtenerConfiguracion().razonSocial

  const irA = (id: string) => {
    setVistaId(id)
    setMenuAbierto(false)
    window.scrollTo({ top: 0 })
  }

  const contenido = (() => {
    switch (vista.id) {
      case 'recepcion': return <RecepcionGym />
      case 'socios': return <SociosGym />
      case 'planes': return <PlanesGym />
      case 'facturar': return <Facturacion />
      case 'ventas': return <LibroVenta />
      case 'compras': return <LibroCompra />
      case 'clientes': return <ModuloClientes />
      case 'proveedores': return <ModuloProveedores />
      case 'caja': return <LibroCaja clienteId="1" />
      case 'banco': return <LibroBanco clienteId="1" />
      case 'productos': return <GestionProductos />
      case 'stock': return <LibroStock />
      case 'diario': return <ListadoAsientos clienteId="1" />
      case 'mayor': return <LibroMayor />
      case 'balance': return <BalanceGeneralTab />
      case 'asiento':
        return (
          <AsientoForm
            clienteId="1"
            // Caja y Banco se mueven desde sus libros (son cuentas de control).
            cuentas={cuentas.filter((c) => c.id !== '101' && c.id !== '102')}
            onConfirmado={() => irA('diario')}
          />
        )
      case 'usuarios': return <ModuloUsuarios />
      case 'configuracion': return <ModuloConfiguracion />
      case 'auditoria': return <ModuloAuditoria />
      default: return null
    }
  })()

  const barraLateral = (
    <div className="flex h-full flex-col bg-slate-900 text-slate-300">
      <div className="px-5 pt-5 pb-3">
        <p className="text-xl font-semibold tracking-tight text-white">GymStudio</p>
        {negocio && <p className="mt-0.5 truncate text-sm text-slate-400">{negocio}</p>}
      </div>

      <nav className="flex-1 overflow-y-auto px-3 pb-3 [scrollbar-width:thin] [scrollbar-color:var(--color-slate-700)_transparent]" aria-label="Menú principal">
        {menu.map((grupo) => (
          <div key={grupo.titulo} className="mt-3 first:mt-0">
            <p className="px-2 pb-1 text-xs font-medium text-slate-500">{grupo.titulo}</p>
            <ul className="space-y-0.5">
              {grupo.vistas.map((v) => {
                const activa = v.id === vista.id
                return (
                  <li key={v.id}>
                    <button
                      onClick={() => irA(v.id)}
                      aria-current={activa ? 'page' : undefined}
                      className={`relative flex w-full items-center gap-2.5 rounded-md px-2 py-[5px] text-left text-sm transition-colors ${
                        activa
                          ? 'bg-slate-800 font-medium text-white before:absolute before:left-0 before:top-1 before:bottom-1 before:w-[3px] before:rounded-full before:bg-marca-400'
                          : 'hover:bg-slate-800/60 hover:text-white'
                      }`}
                    >
                      {v.icono}
                      {v.titulo}
                    </button>
                  </li>
                )
              })}
              {grupo.titulo === 'Contabilidad' && (
                <li>
                  <button
                    onClick={() => { setMostrarReportes(true); setMenuAbierto(false) }}
                    className="flex w-full items-center gap-2.5 rounded-md px-2 py-[5px] text-left text-sm transition-colors hover:bg-slate-800/60 hover:text-white"
                  >
                    <FileText className={icono} />
                    Reportes en PDF
                  </button>
                </li>
              )}
            </ul>
          </div>
        ))}
      </nav>

      <div className="flex items-center gap-3 border-t border-slate-800 px-4 py-3">
        <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-marca-700 text-xs font-semibold text-white">
          {iniciales(user.nombre || '?')}
        </div>
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-medium text-white">{user.nombre}</p>
          <p className="text-xs text-slate-400">{ETIQUETA_ROL[user.rol] ?? user.rol}</p>
        </div>
        <button
          onClick={logout}
          className="rounded-md p-1.5 text-slate-400 transition-colors hover:bg-slate-800 hover:text-white"
          title="Cerrar sesión"
          aria-label="Cerrar sesión"
        >
          <LogOut className="h-4 w-4" />
        </button>
      </div>
    </div>
  )

  return (
    <div className="min-h-screen bg-slate-50">
      {/* Barra lateral fija en pantallas grandes */}
      <aside className="fixed inset-y-0 left-0 z-30 hidden w-60 lg:block">{barraLateral}</aside>

      {/* En celular y tablet: barra superior con menú desplegable */}
      <header className="sticky top-0 z-30 flex items-center justify-between bg-slate-900 px-4 py-3 lg:hidden">
        <button
          onClick={() => setMenuAbierto(true)}
          className="rounded-md p-1.5 text-slate-300 hover:bg-slate-800 hover:text-white"
          aria-label="Abrir menú"
        >
          <Menu className="h-5 w-5" />
        </button>
        <span className="font-semibold text-white">GymStudio</span>
        <span className="w-8" />
      </header>
      {menuAbierto && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <div className="absolute inset-0 bg-slate-950/50" onClick={() => setMenuAbierto(false)} />
          <aside className="absolute inset-y-0 left-0 w-64 shadow-xl">
            {barraLateral}
            <button
              onClick={() => setMenuAbierto(false)}
              className="absolute right-3 top-5 rounded-md p-1.5 text-slate-400 hover:bg-slate-800 hover:text-white"
              aria-label="Cerrar menú"
            >
              <X className="h-5 w-5" />
            </button>
          </aside>
        </div>
      )}

      <main className="lg:pl-60">
        <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
          <h1 className="mb-6 text-2xl font-semibold tracking-tight text-slate-900">{vista.titulo}</h1>
          {vista.tarjeta ? (
            <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200 sm:p-8">{contenido}</div>
          ) : (
            contenido
          )}
        </div>
      </main>

      {mostrarReportes && <ModalGenerarReporte onClose={() => setMostrarReportes(false)} />}
    </div>
  )
}

function App() {
  return (
    <AuthProvider>
      <QueryClientProvider client={queryClient}>
        <Toaster position="top-right" />
        <AppContent />
      </QueryClientProvider>
    </AuthProvider>
  )
}

export default App
