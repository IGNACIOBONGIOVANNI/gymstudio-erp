# Cursor del gimnasio — diseño corto

Fecha: 16/11/2026 · Rama: `cursor-gimnasio` · Aprobado en la conversación (vista previa animada mostrada al usuario)

## Por qué
El usuario quiere que el cursor sea propio del sistema y «acorde al gym»: mancuernas, un brazo haciendo curl de bíceps
cuando se puede hacer una acción (animado, «tipo gif») y un disco de pesas girando, que se note, mientras carga.

## Qué se ve
- **Normal:** flecha verde de la marca (`#1f6f54`, borde blanco) con una mancuerna chiquita. Cursor CSS estático
  (`/cursores/flecha.svg`, punto activo 3 2), con `auto` de respaldo.
- **Sobre algo que se puede tocar** (enlaces, botones habilitados, pestañas, `role=button`, `select`, checkbox/radio,
  `label[for]`, `summary`, `.cursor-pointer`, elementos SVG con `cursor="pointer"`): la flecha sigue marcando el clic y al
  lado aparece un **brazo haciendo curl** animado (sube y baja la mancuerna, el bíceps se infla; ciclo de 1,6 s).
- **Mientras carga** (hay un pedido a `/api/` en curso hace más de 200 ms): al lado de la flecha, un **disco de pesas
  grande girando** (0,8 s por vuelta, con una franja clara que hace notar el giro). Tiene prioridad sobre el brazo.
- **Sin cambios:** barrita de texto en campos para escribir y cartel de prohibido en lo deshabilitado.

## Cómo
Los navegadores no animan cursores, así que la animación es un «acompañante»: un elemento `position: fixed`,
`pointer-events: none`, encima de todo, que sigue al mouse (se mueve con `transform` en `requestAnimationFrame`, sin
volver a dibujar React en cada movimiento). El componente se monta una vez en `main.tsx` (sirve también para el inicio de
sesión y el Manual). La carga se detecta en el interceptor de `fetch` que ya existe (`authFetch.ts`), que lleva la cuenta
de pedidos a `/api/` en curso.

## Reglas
- Solo con mouse (`pointer: fine`); en pantallas táctiles no aparece.
- Con «reducir movimiento» del sistema: el brazo queda quieto con el bíceps arriba y el disco no gira.
- Se oculta al salir el mouse de la ventana.
- Si el JavaScript fallara, queda la flecha estática.

## Pruebas
Lógica pura en `cursorDelGimnasio.ts` con pruebas de Node: qué modo corresponde (normal, acción o carga), el retraso de
200 ms de la carga, qué elementos cuentan como acción (deshabilitados no, campos de texto no) y el contador de pedidos.
Revisión visual en una copia del sistema.

## Enfoques descartados
- Cursor animado (GIF o .ani): los navegadores muestran solo el primer cuadro.
- Reemplazar la flecha por el brazo animado: el punto del clic se movería con la animación y se perdería precisión.
