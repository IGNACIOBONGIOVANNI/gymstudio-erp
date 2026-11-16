// Interceptor de autenticación para todas las llamadas a la API.
//
// En lugar de modificar cada fetch del proyecto, se envuelve window.fetch una sola
// vez al iniciar la app (igual que un interceptor de axios):
// - agrega "Authorization: Bearer <token>" a toda request dirigida a /api/
// - si la API responde 401 (token vencido o inválido), avisa con el evento
//   SESION_EXPIRADA para que AuthContext cierre la sesión.

import { pedidosALaApi } from '../utils/cursorDelGimnasio';

export const STORAGE_KEY_SESION = 'user';
export const SESION_EXPIRADA = 'sesion-expirada';

export function obtenerToken(): string | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY_SESION);
    return raw ? (JSON.parse(raw).token as string) ?? null : null;
  } catch {
    return null;
  }
}

function esLlamadaALaApi(url: string): boolean {
  return url.includes('/api/') && !url.includes('/api/auth/login');
}

let instalado = false;

export function instalarInterceptorAuth(): void {
  if (instalado) return;
  instalado = true;

  const fetchOriginal = window.fetch.bind(window);

  window.fetch = async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    if (!url.includes('/api/')) return fetchOriginal(input, init);
    // Mientras haya pedidos a la API en curso, el cursor muestra el disco girando.
    pedidosALaApi.empezar();
    try {
      return await pedirALaApi(url, input, init);
    } finally {
      pedidosALaApi.terminar();
    }
  };

  const pedirALaApi = async (url: string, input: RequestInfo | URL, init?: RequestInit) => {
    if (!esLlamadaALaApi(url)) {
      return fetchOriginal(input, init);
    }

    const headers = new Headers(init?.headers ?? (input instanceof Request ? input.headers : undefined));
    const token = obtenerToken();
    if (token && !headers.has('Authorization')) {
      headers.set('Authorization', `Bearer ${token}`);
    }

    const respuesta = await fetchOriginal(input, { ...init, headers });
    if (respuesta.status === 401 && token) {
      window.dispatchEvent(new Event(SESION_EXPIRADA));
    }
    return respuesta;
  };
}

/** Devuelve true si el JWT ya venció (lee el claim "exp" sin validar la firma). */
export function tokenVencido(token: string): boolean {
  try {
    const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const { exp } = JSON.parse(atob(payload));
    return typeof exp !== 'number' || exp * 1000 <= Date.now();
  } catch {
    return true;
  }
}
