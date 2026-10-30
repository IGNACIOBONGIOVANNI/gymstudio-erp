using System;
using System.Net;
using System.Text;

namespace EstudioContable.Domain
{
    /// <summary>
    /// Qué equipo hizo un pedido, para la auditoría: dirección IP, sistema operativo y navegador.
    /// El sistema operativo y el navegador salen del encabezado User-Agent, que el propio equipo
    /// informa y puede falsearse: sirve para saber desde dónde se trabajó, no como prueba.
    /// </summary>
    public static class AgenteDeUsuario
    {
        public const string Desconocido = "Desconocido";
        public const int LargoMaximoDelAgente = 512;
        public const int LargoMaximoDeIp = 45;      // IPv6 completa
        public const int LargoMaximoDelTexto = 60;

        /// <summary>Sistema operativo y navegador a partir del User-Agent. Nunca devuelve null.</summary>
        public static (string SistemaOperativo, string Navegador) Analizar(string? agente)
        {
            var ua = Limpiar(agente);
            if (ua.Length == 0) return (Desconocido, Desconocido);
            return (Recortar(DetectarSistema(ua)), Recortar(DetectarNavegador(ua)));
        }

        /// <summary>
        /// IP en su forma canónica (una IPv4 dentro de IPv6 se escribe como IPv4), o null si el texto no es
        /// una dirección. No acepta puertos ni texto libre: lo que llega de un encabezado no se guarda tal cual.
        /// </summary>
        public static string? NormalizarIp(string? ip)
        {
            if (string.IsNullOrWhiteSpace(ip) || ip.Length > LargoMaximoDeIp + 8) return null;
            if (!IPAddress.TryParse(ip.Trim(), out var direccion)) return null;
            if (direccion.IsIPv4MappedToIPv6) direccion = direccion.MapToIPv4();
            // Sin zona de IPv6 (fe80::1%eth0): la zona es un dato del equipo local, no de la red.
            var texto = direccion.ToString();
            var porciento = texto.IndexOf('%');
            if (porciento >= 0) texto = texto[..porciento];
            return texto.Length <= LargoMaximoDeIp ? texto : null;
        }

        private static string DetectarSistema(string ua)
        {
            if (Tiene(ua, "Windows"))
            {
                if (Tiene(ua, "Windows NT 10.0")) return "Windows 10/11";
                if (Tiene(ua, "Windows NT 6.3")) return "Windows 8.1";
                if (Tiene(ua, "Windows NT 6.2")) return "Windows 8";
                if (Tiene(ua, "Windows NT 6.1")) return "Windows 7";
                return "Windows";
            }
            // Android y iOS antes que Linux y macOS: sus agentes también dicen "Linux" y "Mac OS X".
            if (Tiene(ua, "Android")) return Con("Android", Mayor(ua, "Android "));
            if (Tiene(ua, "iPhone") || Tiene(ua, "iPad") || Tiene(ua, "iPod")) return Con("iOS", Mayor(ua, "OS "));
            if (Tiene(ua, "CrOS")) return "ChromeOS";
            if (Tiene(ua, "Macintosh") || Tiene(ua, "Mac OS X")) return "macOS";
            if (Tiene(ua, "Linux") || Tiene(ua, "X11")) return "Linux";
            return Desconocido;
        }

        private static string DetectarNavegador(string ua)
        {
            // Herramientas que no son navegadores: se nombran para que en la auditoría se note.
            if (Tiene(ua, "curl/")) return Con("curl", Mayor(ua, "curl/"));
            if (Tiene(ua, "PostmanRuntime/")) return "Postman";
            if (Tiene(ua, "python-requests/")) return "python-requests";
            if (Tiene(ua, "Python-urllib")) return "Python urllib";
            if (Tiene(ua, "okhttp")) return "okhttp";

            // El orden importa: Edge, Opera y Samsung dicen también "Chrome"; Chrome dice también "Safari".
            if (Tiene(ua, "Edg/") || Tiene(ua, "EdgA/") || Tiene(ua, "EdgiOS/"))
                return Con("Edge", Mayor(ua, "Edg/") ?? Mayor(ua, "EdgA/") ?? Mayor(ua, "EdgiOS/"));
            if (Tiene(ua, "OPR/")) return Con("Opera", Mayor(ua, "OPR/"));
            if (Tiene(ua, "SamsungBrowser/")) return Con("Samsung Internet", Mayor(ua, "SamsungBrowser/"));
            if (Tiene(ua, "Firefox/") || Tiene(ua, "FxiOS/")) return Con("Firefox", Mayor(ua, "Firefox/") ?? Mayor(ua, "FxiOS/"));
            if (Tiene(ua, "Chrome/") || Tiene(ua, "CriOS/")) return Con("Chrome", Mayor(ua, "Chrome/") ?? Mayor(ua, "CriOS/"));
            if (Tiene(ua, "Safari/") && Tiene(ua, "Version/")) return Con("Safari", Mayor(ua, "Version/"));
            return Desconocido;
        }

        private static bool Tiene(string texto, string parte) => texto.Contains(parte, StringComparison.OrdinalIgnoreCase);

        private static string Con(string nombre, string? version) => string.IsNullOrEmpty(version) ? nombre : $"{nombre} {version}";

        /// <summary>El número mayor de la versión que sigue a una marca ("Chrome/141.0.0.0" → "141"), o null.</summary>
        private static string? Mayor(string ua, string marca)
        {
            var i = ua.IndexOf(marca, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;
            var inicio = i + marca.Length;
            var fin = inicio;
            while (fin < ua.Length && fin - inicio < 4 && char.IsAsciiDigit(ua[fin])) fin++;
            return fin > inicio ? ua[inicio..fin] : null;
        }

        /// <summary>Sin caracteres de control (un salto de línea no puede partir un registro) y con tope de largo.</summary>
        private static string Limpiar(string? agente)
        {
            if (string.IsNullOrWhiteSpace(agente)) return string.Empty;
            var origen = agente.Length > LargoMaximoDelAgente ? agente[..LargoMaximoDelAgente] : agente;
            var sb = new StringBuilder(origen.Length);
            foreach (var c in origen)
                sb.Append(char.IsControl(c) ? ' ' : c);
            return sb.ToString().Trim();
        }

        private static string Recortar(string texto) => texto.Length <= LargoMaximoDelTexto ? texto : texto[..LargoMaximoDelTexto];
    }
}
