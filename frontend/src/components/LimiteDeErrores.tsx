import { Component, type ErrorInfo, type ReactNode } from "react";
import { AlertTriangle } from "lucide-react";

interface Props { children: ReactNode }
interface Estado { fallo: Error | null }

/**
 * Si una pantalla falla al dibujarse (por ejemplo, por un dato que no llegó), muestra un aviso con la forma de
 * seguir en lugar de dejar toda la aplicación en blanco. El menú sigue funcionando para ir a otra pantalla.
 */
export class LimiteDeErrores extends Component<Props, Estado> {
  state: Estado = { fallo: null };

  static getDerivedStateFromError(fallo: Error): Estado {
    return { fallo };
  }

  componentDidCatch(fallo: Error, info: ErrorInfo) {
    console.error("Error al dibujar la pantalla:", fallo, info.componentStack);
  }

  render() {
    if (!this.state.fallo) return this.props.children;
    return (
      <div role="alert" className="rounded-xl border border-amber-300 bg-amber-50 p-6 text-amber-950">
        <div className="flex items-start gap-3">
          <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0" aria-hidden />
          <div>
            <p className="font-semibold">Esta pantalla no pudo mostrarse</p>
            <p className="mt-1 text-sm">
              Probá recargar la página (Ctrl + F5). Si sigue igual, puede que el servidor todavía se esté actualizando:
              esperá un minuto y volvé a intentar. Mientras tanto podés usar el resto del menú.
            </p>
            <div className="mt-3 flex gap-2">
              <button onClick={() => window.location.reload()} className="rounded-md bg-amber-900 px-3 py-1.5 text-sm font-medium text-white hover:bg-amber-950">
                Recargar la página
              </button>
              <button onClick={() => this.setState({ fallo: null })} className="rounded-md border border-amber-400 px-3 py-1.5 text-sm font-medium hover:bg-amber-100">
                Reintentar
              </button>
            </div>
          </div>
        </div>
      </div>
    );
  }
}
