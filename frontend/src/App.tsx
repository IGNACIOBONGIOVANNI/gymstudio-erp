import { useState, useEffect } from 'react'
import { Toaster } from 'react-hot-toast'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { FileText, LogOut, User } from 'lucide-react'
import { AsientoForm } from './asientos/AsientoForm'
import { ListadoAsientos } from './asientos/ListadoAsientos'
import { LibroMayor } from './asientos/LibroMayor'
import { BalanceGeneralTab } from './asientos/BalanceGeneralTab'
import ModuloClientes from './features/clientes/ModuloClientes'
import ModuloProveedores from './features/proveedores/ModuloProveedores'
import ModuloUsuarios from './features/usuarios/ModuloUsuarios'
import ModuloConfiguracion from './features/configuracion/ModuloConfiguracion'
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

import { AuthProvider, useAuth } from './context/AuthContext'
import { Login } from './components/Login'
import { ModalGenerarReporte } from './components/reportes/ModalGenerarReporte'

const queryClient = new QueryClient()

import { cuentasDePrueba } from './asientos/cuentas'


function AppContent() {
  const { user, logout } = useAuth();
  
  const [modulo, setModulo] = useState<'contable' | 'gimnasio' | 'stock' | 'clientes' | 'proveedores' | 'usuarios' | 'configuracion'>('contable');
  const [tabContable, setTabContable] = useState<'nuevo' | 'listado' | 'mayor' | 'ventas' | 'compras' | 'caja' | 'banco' | 'facturacion' | 'balance'>('listado');
  const [tabGimnasio, setTabGimnasio] = useState<'planes' | 'socios' | 'recepcion'>('recepcion');
  const [tabStock, setTabStock] = useState<'libro' | 'productos'>('libro');
  const [showReporteModal, setShowReporteModal] = useState(false);

  // La API es la que realmente controla los permisos; esto sólo oculta menús.
  const esJefe = user?.rol === 'Administrador';

  useEffect(() => {
    if (!esJefe && (modulo === 'usuarios' || modulo === 'configuracion')) {
      setModulo('contable');
    }
  }, [esJefe, modulo]);

  if (!user) {
    return <Login />;
  }

  return (
    <div className="min-h-screen bg-slate-50">
      
      <div className="bg-slate-900 px-4 py-3 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-7xl flex flex-wrap items-center justify-between gap-4">
          <div className="flex flex-wrap items-center gap-8">
            <span className="text-white font-bold text-lg tracking-tight">GymStudio ERP</span>
            <nav className="flex flex-wrap gap-4">
              <button
                onClick={() => setModulo('contable')}
                className={`rounded-md px-3 py-2 text-sm font-medium transition-colors ${
                  modulo === 'contable'
                    ? 'bg-slate-800 text-white'
                    : 'text-slate-300 hover:bg-slate-700 hover:text-white'
                }`}
              >
                Módulo Contable
              </button>
              <button
                onClick={() => setModulo('gimnasio')}
                className={`rounded-md px-3 py-2 text-sm font-medium transition-colors ${
                  modulo === 'gimnasio'
                    ? 'bg-slate-800 text-white'
                    : 'text-slate-300 hover:bg-slate-700 hover:text-white'
                }`}
              >
                Módulo Gimnasio
              </button>
              <button
                onClick={() => setModulo('stock')}
                className={`rounded-md px-3 py-2 text-sm font-medium transition-colors ${
                  modulo === 'stock'
                    ? 'bg-slate-800 text-white'
                    : 'text-slate-300 hover:bg-slate-700 hover:text-white'
                }`}
              >
                Módulo Stock
              </button>
              <button
                onClick={() => setModulo('clientes')}
                className={`rounded-md px-3 py-2 text-sm font-medium transition-colors ${
                  modulo === 'clientes'
                    ? 'bg-slate-800 text-white'
                    : 'text-slate-300 hover:bg-slate-700 hover:text-white'
                }`}
              >
                Módulo Clientes
              </button>
              <button
                onClick={() => setModulo('proveedores')}
                className={`rounded-md px-3 py-2 text-sm font-medium transition-colors ${
                  modulo === 'proveedores'
                    ? 'bg-slate-800 text-white'
                    : 'text-slate-300 hover:bg-slate-700 hover:text-white'
                }`}
              >
                Módulo Proveedores
              </button>

              {esJefe && (
                <>
                  <button
                    onClick={() => setModulo('usuarios')}
                    className={`rounded-md px-3 py-2 text-sm font-medium transition-colors ${
                      modulo === 'usuarios'
                        ? 'bg-slate-800 text-white'
                        : 'text-slate-300 hover:bg-slate-700 hover:text-white'
                    }`}
                  >
                    Admin de Usuarios
                  </button>
                  <button
                    onClick={() => setModulo('configuracion')}
                    className={`rounded-md px-3 py-2 text-sm font-medium transition-colors ${
                      modulo === 'configuracion'
                        ? 'bg-slate-800 text-white'
                        : 'text-slate-300 hover:bg-slate-700 hover:text-white'
                    }`}
                  >
                    Configuración
                  </button>
                </>
              )}
            </nav>
          </div>
          
          <div className="flex items-center gap-4 border-l border-slate-700 pl-4 ml-auto">
            <div className="flex flex-col items-end">
              <span className="text-sm font-medium text-white">{user.nombre}</span>
              <span className="text-xs text-slate-400 font-medium">{user.rol}</span>
            </div>
            <div className="h-8 w-8 bg-slate-700 rounded-full flex items-center justify-center text-white text-xs font-bold shrink-0">
              {user.nombre
                ? user.nombre
                    .split(' ')
                    .map((n) => n[0])
                    .slice(0, 2)
                    .join('')
                    .toUpperCase()
                : <User className="h-4 w-4" />}
            </div>
            <button 
              onClick={logout}
              className="ml-2 text-slate-400 hover:text-red-400 transition-colors"
              title="Cerrar Sesión"
            >
              <LogOut className="h-5 w-5" />
            </button>
          </div>
        </div>
      </div>

      <div className="py-12 px-4 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-6xl">
          <h1 className="mb-8 text-3xl font-bold tracking-tight text-slate-900">
            {modulo === 'contable' && 'Módulo Contable'}
            {modulo === 'gimnasio' && 'Módulo Gimnasio'}
            {modulo === 'stock' && 'Módulo Stock'}
            {modulo === 'clientes' && 'Módulo Clientes'}
            {modulo === 'proveedores' && 'Módulo Proveedores'}
          </h1>
          
          {modulo === 'contable' && (
            <div className="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-200 pb-1">
              <div className="flex items-center gap-2 sm:gap-4 overflow-x-auto whitespace-nowrap scrollbar-none py-1">
                <button
                  onClick={() => setTabContable('listado')}
                  className={`pb-3 text-sm font-medium border-b-2 transition-colors shrink-0 whitespace-nowrap ${
                    tabContable === 'listado'
                      ? 'border-slate-900 text-slate-900'
                      : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700'
                  }`}
                >
                  Libro Diario
                </button>
                <button
                  onClick={() => setTabContable('mayor')}
                  className={`pb-3 text-sm font-medium border-b-2 transition-colors shrink-0 whitespace-nowrap ${
                    tabContable === 'mayor'
                      ? 'border-slate-900 text-slate-900'
                      : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700'
                  }`}
                >
                  Libro Mayor
                </button>
                <button
                  onClick={() => setTabContable('balance')}
                  className={`pb-3 text-sm font-medium border-b-2 transition-colors shrink-0 whitespace-nowrap ${
                    tabContable === 'balance'
                      ? 'border-slate-900 text-slate-900'
                      : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700'
                  }`}
                >
                  Balance General
                </button>
                <button
                  onClick={() => setTabContable('ventas')}
                  className={`pb-3 text-sm font-medium border-b-2 transition-colors shrink-0 whitespace-nowrap ${
                    tabContable === 'ventas'
                      ? 'border-slate-900 text-slate-900'
                      : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700'
                  }`}
                >
                  Libro Venta
                </button>
                <button
                  onClick={() => setTabContable('compras')}
                  className={`pb-3 text-sm font-medium border-b-2 transition-colors shrink-0 whitespace-nowrap ${
                    tabContable === 'compras'
                      ? 'border-slate-900 text-slate-900'
                      : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700'
                  }`}
                >
                  Libro Compra
                </button>
                <button
                  onClick={() => setTabContable('caja')}
                  className={`pb-3 text-sm font-medium border-b-2 transition-colors shrink-0 whitespace-nowrap ${
                    tabContable === 'caja'
                      ? 'border-slate-900 text-slate-900'
                      : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700'
                  }`}
                >
                  Libro Caja
                </button>
                <button
                  onClick={() => setTabContable('banco')}
                  className={`pb-3 text-sm font-medium border-b-2 transition-colors shrink-0 whitespace-nowrap ${
                    tabContable === 'banco'
                      ? 'border-slate-900 text-slate-900'
                      : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700'
                  }`}
                >
                  Libro Banco
                </button>
                <button
                  onClick={() => setTabContable('nuevo')}
                  className={`pb-3 text-sm font-medium border-b-2 transition-colors shrink-0 whitespace-nowrap ${
                    tabContable === 'nuevo'
                      ? 'border-slate-900 text-slate-900'
                      : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700'
                  }`}
                >
                  Nuevo Asiento
                </button>
                <button
                  onClick={() => setTabContable('facturacion')}
                  className={`pb-3 text-sm font-medium border-b-2 transition-colors shrink-0 whitespace-nowrap ${
                    tabContable === 'facturacion'
                      ? 'border-slate-900 text-slate-900'
                      : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700'
                  }`}
                >
                  Facturación
                </button>
              </div>
              <div className="shrink-0 pb-2 md:pb-0 flex items-center">
                <button
                  onClick={() => setShowReporteModal(true)}
                  className="w-full sm:w-auto inline-flex items-center justify-center gap-2 rounded-lg bg-indigo-600 px-4 py-2 min-h-[40px] text-sm font-semibold text-white shadow-sm transition-all hover:bg-indigo-700 active:scale-95 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2"
                >
                  <FileText className="h-4 w-4 shrink-0" />
                  <span>Generar Reporte (PDF)</span>
                </button>
              </div>
            </div>
          )}

          {showReporteModal && <ModalGenerarReporte onClose={() => setShowReporteModal(false)} />}

          {modulo === 'gimnasio' && (
            <div className="mb-6 flex flex-wrap gap-4 border-b border-slate-200">
              <button
                onClick={() => setTabGimnasio('recepcion')}
                className={`pb-3 text-sm font-medium border-b-2 transition-colors ${
                  tabGimnasio === 'recepcion'
                    ? 'border-slate-900 text-slate-900'
                    : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700'
                }`}
              >
                Recepción
              </button>
              <button
                onClick={() => setTabGimnasio('planes')}
                className={`pb-3 text-sm font-medium border-b-2 transition-colors ${
                  tabGimnasio === 'planes'
                    ? 'border-slate-900 text-slate-900'
                    : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700'
                }`}
              >
                Planes
              </button>
              <button
                onClick={() => setTabGimnasio('socios')}
                className={`pb-3 text-sm font-medium border-b-2 transition-colors ${
                  tabGimnasio === 'socios'
                    ? 'border-slate-900 text-slate-900'
                    : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700'
                }`}
              >
                Socios
              </button>
            </div>
          )}

          {modulo === 'stock' && (
            <div className="mb-6 flex flex-wrap gap-4 border-b border-slate-200">
              <button
                onClick={() => setTabStock('libro')}
                className={`pb-3 text-sm font-medium border-b-2 transition-colors ${
                  tabStock === 'libro'
                    ? 'border-slate-900 text-slate-900'
                    : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700'
                }`}
              >
                Libro Stock
              </button>
              <button
                onClick={() => setTabStock('productos')}
                className={`pb-3 text-sm font-medium border-b-2 transition-colors ${
                  tabStock === 'productos'
                    ? 'border-slate-900 text-slate-900'
                    : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700'
                }`}
              >
                Gestión Productos
              </button>
            </div>
          )}

          <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200 sm:p-8">
            {modulo === 'contable' && (
              <>
                {tabContable === 'nuevo' && (
                  <AsientoForm 
                    clienteId="1" 
                    cuentas={cuentasDePrueba} 
                    onConfirmado={() => setTabContable('listado')}
                  />
                )}
                {tabContable === 'listado' && <ListadoAsientos clienteId="1" />}
                {tabContable === 'mayor' && <LibroMayor />}
                {tabContable === 'balance' && <BalanceGeneralTab />}
                {tabContable === 'ventas' && <LibroVenta />}
                {tabContable === 'compras' && <LibroCompra />}
                {tabContable === 'caja' && <LibroCaja clienteId="1" />}
                {tabContable === 'banco' && <LibroBanco clienteId="1" />}
                {tabContable === 'facturacion' && <Facturacion />}
              </>
            )}

            {modulo === 'gimnasio' && (
              <>
                {tabGimnasio === 'recepcion' && <RecepcionGym />}
                {tabGimnasio === 'planes' && <PlanesGym />}
                {tabGimnasio === 'socios' && <SociosGym />}
              </>
            )}

            {modulo === 'stock' && (
              <>
                {tabStock === 'libro' && <LibroStock />}
                {tabStock === 'productos' && <GestionProductos />}
              </>
            )}

            {modulo === 'clientes' && (
              <ModuloClientes />
            )}
            
            {modulo === 'proveedores' && (
              <ModuloProveedores />
            )}

            {modulo === 'usuarios' && (
              <ModuloUsuarios />
            )}

            {modulo === 'configuracion' && (
              <ModuloConfiguracion />
            )}
          </div>
        </div>
      </div>
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