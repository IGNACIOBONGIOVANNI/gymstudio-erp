import { createContext, useContext, useState, useEffect } from 'react';
import type { ReactNode } from 'react';
import toast from 'react-hot-toast';
import { SESION_EXPIRADA, STORAGE_KEY_SESION, tokenVencido } from '../api/authFetch';

interface User {
  nombre: string;
  rol: string;
  token: string;
  email?: string;
}

interface AuthContextType {
  user: User | null;
  login: (token: string, rol: string, nombre: string, email?: string) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

function leerSesionGuardada(): User | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY_SESION);
    if (!raw) return null;
    const user = JSON.parse(raw) as User;
    // Una sesión con token vencido (o de la versión anterior, sin JWT) se descarta.
    if (!user.token || tokenVencido(user.token)) {
      localStorage.removeItem(STORAGE_KEY_SESION);
      return null;
    }
    return user;
  } catch {
    return null;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(leerSesionGuardada);

  const login = (token: string, rol: string, nombre: string, email?: string) => {
    const newUser: User = { token, rol, nombre, email };
    setUser(newUser);
    localStorage.setItem(STORAGE_KEY_SESION, JSON.stringify(newUser));
  };

  const logout = () => {
    setUser(null);
    localStorage.removeItem(STORAGE_KEY_SESION);
  };

  // El interceptor de fetch avisa cuando la API responde 401.
  useEffect(() => {
    const alExpirar = () => {
      if (!localStorage.getItem(STORAGE_KEY_SESION)) return;
      logout();
      toast.error('Tu sesión expiró. Iniciá sesión nuevamente.');
    };
    window.addEventListener(SESION_EXPIRADA, alExpirar);
    return () => window.removeEventListener(SESION_EXPIRADA, alExpirar);
  }, []);

  return (
    <AuthContext.Provider value={{ user, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
