import type { ReactNode } from 'react'
import type { IdSeccion } from './indice'
import { B, Lista, Nota, Pasos, Quien, Sub, Tabla } from './piezas'

const SI = 'Sí'
const NO = 'No'

/** Texto de cada sección del manual. TypeScript exige una entrada por cada id de indice.ts. */
export const CONTENIDO: Record<IdSeccion, ReactNode> = {
  empezar: (
    <>
      <p>
        GymStudio ERP junta en un solo sistema la gestión del gimnasio (socios, planes, cuotas, rutinas e ingresos) y la
        contabilidad del negocio (facturación, caja, banco, stock, libros de IVA, Diario, Mayor y Balance). Lo que cargás en
        una parte aparece en las demás: una venta mueve el stock, la caja y los libros sin que tengas que escribirla de nuevo.
      </p>
      <Sub>Ingresar y salir</Sub>
      <Pasos>
        <li>Abrí el navegador y entrá a la dirección del sistema (en la PC donde está instalado: <B>http://localhost:5173</B>).</li>
        <li>Escribí tu email y tu contraseña y apretá <B>Ingresar</B>.</li>
        <li>Para salir, usá el ícono de salida que está al pie del menú, al lado de tu nombre.</li>
      </Pasos>
      <Nota tipo="ojo">
        Si te equivocás cinco veces seguidas con la contraseña, la cuenta se bloquea 15 minutos. Además, la sesión se cierra sola
        después de un rato sin usar el sistema (60 minutos por defecto; se cambia en <B>Configuración</B>, pestaña Seguridad y Sistema).
      </Nota>
      <Sub>Cómo moverse</Sub>
      <Lista>
        <li>A la <strong>izquierda</strong> está el menú, ordenado por grupos: Recepción, Ventas y compras, Tesorería, Stock, Contabilidad y Administración. Cada usuario ve solo lo que le corresponde.</li>
        <li>Arriba a la <strong>derecha</strong> está la campanita de avisos, y abajo a la derecha el botón <B>Asistente</B>.</li>
        <li>Al pie del menú, <B>Manual</B> abre esta guía en otra ventana y <B>Acerca de</B> muestra los datos del sistema.</li>
        <li>En el celular o la tablet, el menú se abre con el botón de las tres rayas, arriba a la izquierda.</li>
      </Lista>
      <p>Los importes están en pesos argentinos (por ejemplo, $ 1.234,50) y las fechas se escriben día, mes y año.</p>
    </>
  ),

  roles: (
    <>
      <p>
        Hay tres tipos de usuario. Lo que puede hacer cada uno lo controla el servidor: aunque alguien intente entrar a una
        pantalla por otro camino, el sistema se lo impide.
      </p>
      <Tabla
        columnas={['Acción', 'Administrador', 'Contador', 'Recepción']}
        filas={[
          ['Consultar libros, facturar, ver socios, planes y stock', SI, SI, SI],
          ['Ver los importes del resumen de inicio (ventas, caja, IVA)', SI, SI, NO],
          ['Anular o editar facturas', SI, SI, NO],
          ['Ver cuentas a cobrar y a pagar y estados de cuenta', SI, SI, NO],
          ['Mover dinero en Caja y Banco, y transferir entre ambos', SI, SI, NO],
          ['Asignar, cambiar y finalizar la rutina de un socio', SI, SI, SI],
          ['Crear, editar y eliminar rutinas modelo y ejercicios', SI, SI, NO],
          ['Registrar ingresos de socios y ver los socios en riesgo', SI, SI, SI],
          ['Ver e imprimir el calendario de encargados', SI, SI, SI],
          ['Modificar el calendario de encargados', SI, SI, NO],
          ['Deshacer un ingreso cargado por error', SI, SI, NO],
          ['Registrar y corregir mediciones de peso y medidas', SI, SI, SI],
          ['Crear, confirmar o anular asientos manuales; crear cuentas', SI, SI, NO],
          ['Administrar usuarios y consultar la auditoría', SI, NO, NO],
          ['Descargar, comparar y restaurar copias de seguridad', SI, NO, NO],
        ]}
      />
      <Nota>
        El administrador principal no puede eliminarse, desactivarse ni perder su rol: así el sistema nunca se queda sin nadie que lo administre.
      </Nota>
    </>
  ),

  resumen: (
    <>
      <Quien quien="todos" extra="Recepción ve solo lo del gimnasio" />
      <p>
        Es la primera pantalla. Arriba a la derecha elegís el <B>Mes</B> y ves, de un vistazo, cómo viene el negocio:
      </p>
      <Lista>
        <li><strong>Novedades:</strong> frases armadas con tus propios datos (ventas contra el mes anterior, deudas de más de 90 días, productos sin stock, cuotas por vencer...). Primero va lo que requiere atención.</li>
        <li><strong>Números del mes:</strong> ventas y compras netas, resultado, IVA a pagar, saldo de Caja y de Banco, lo que hay para cobrar y para pagar, y las facturas anuladas.</li>
        <li><strong>Gráficos:</strong> ventas contra compras de los últimos 6 meses, ingresos por origen (cuotas o productos) y productos más vendidos.</li>
        <li><strong>Gimnasio:</strong> estado de las cuotas, socios sin rutina, rutinas vencidas y productos con poco stock (5 unidades o menos).</li>
      </Lista>
      <p>
        Los totales salen de las mismas tablas que los libros, así que coinciden con el Diario, la Caja y el Banco. Las facturas
        anuladas no suman. El botón <B>Imprimir resumen</B> lo saca en PDF.
      </p>
    </>
  ),

  recepcion: (
    <>
      <Quien quien="todos" extra="Deshacer un ingreso lo hace el Administrador o el Contador" />
      <p>La pantalla de Recepción es la del mostrador: registrás quién entra, cobrás cuotas y avisás a quien se está yendo.</p>
      <Sub>Registrar un ingreso</Sub>
      <Pasos>
        <li>Escribí el DNI en <B>DNI del socio</B> (con o sin puntos).</li>
        <li>Apretá <B>Registrar ingreso</B>.</li>
        <li>Aparece, en grande, el nombre del socio, cómo está su cuota (al día, por vencer, vencida o sin plan) y <strong>qué día de su rutina le toca hoy</strong>, con los ejercicios.</li>
      </Pasos>
      <Lista>
        <li>Los días de la rutina se rotan en orden: uno por cada visita desde que empezó.</li>
        <li>Hay <strong>una visita por socio por día</strong>: si pasa dos veces, el sistema muestra lo mismo y no la duplica.</li>
        <li>Si la cuota no está al día, aparece el botón para cobrarla ahí mismo.</li>
        <li>Debajo, <B>Ingresos de hoy</B> lista quién entró, a qué hora, cómo estaba su cuota y quién lo registró.</li>
      </Lista>
      <Sub>Socios en riesgo de abandono</Sub>
      <p>
        Son los socios con la cuota al día que hace tiempo no vienen. El plazo se elige en <B>Sin venir hace</B> (14 días por
        defecto; también 7, 21 o 30). Cada uno tiene un botón que abre WhatsApp con el mensaje ya escrito. Un socio que nunca
        vino cuenta desde el día en que se anotó. Los mismos aparecen en las novedades del resumen y en la campanita.
      </p>
      <Sub>Cobrar una cuota</Sub>
      <Pasos>
        <li>En la tabla de socios de Recepción, apretá <B>Cobrar Cuota</B> en la fila del socio.</li>
        <li>Elegí el plan y cómo se cobra: <B>Contado Caja</B> o <B>Transferencia Banco</B>.</li>
        <li>Revisá el monto a cobrar y confirmá.</li>
      </Pasos>
      <p>
        El sistema renueva la membresía (el nuevo vencimiento depende de la duración del plan) y emite la factura B de la cuota,
        con su asiento contable y el ingreso en Caja o en Banco. La factura la ves en el Libro de Ventas.
      </p>
      <Sub>Avisos de cuotas por WhatsApp</Sub>
      <p>
        La tabla de socios tiene los filtros <B>Todos</B>, <B>Por vencer en 7 días</B> y <B>Vencidos</B>. El botón de cada fila abre
        WhatsApp con el mensaje de la cuota escrito. Lo envía la persona desde su propio WhatsApp: el sistema no manda mensajes solo.
      </p>
    </>
  ),

  socios: (
    <>
      <Quien quien="todos" extra="Eliminar rutinas modelo y ejercicios es de Administrador y Contador" />
      <Sub>Socios</Sub>
      <p>
        En <B>Socios</B> cargás a cada persona con <B>Nuevo Socio</B>: nombre, apellido, DNI y teléfono. En cada fila hay botones para
        <B>Editar</B>, <B>Eliminar</B>, ver la <B>Rutina del socio</B> y llevar el <B>Seguimiento de peso y medidas</B>.
      </p>
      <Nota tipo="ojo">
        Un socio que ya tiene ingresos registrados no se puede eliminar, para conservar su historial.
      </Nota>
      <Sub>Planes</Sub>
      <p>
        En <B>Planes</B> definís lo que vendés con <B>Nuevo Plan</B>: <B>Nombre del Plan</B> (por ejemplo, Pase Libre), <B>Precio</B>
        mensual y <B>Duración (Meses)</B>. Al cobrar una cuota, el vencimiento del socio se calcula con esa duración.
      </p>
      <Sub>Rutinas</Sub>
      <Lista>
        <li><strong>Ejercicios:</strong> el catálogo, con su grupo muscular. Si un ejercicio ya está en alguna rutina no se borra: queda desactivado (se ve con <B>Ver desactivados</B>).</li>
        <li><strong>Rutinas modelo:</strong> de 1 a 7 días, cada uno con sus ejercicios, series, repeticiones, descanso e indicaciones. La primera vez se cargan dos de ejemplo.</li>
        <li><strong>Rutina de un socio:</strong> se asigna con fecha de inicio y, si querés, de vencimiento. Un socio tiene una sola rutina activa: al asignarle otra, la anterior se cierra el día que empieza la nueva y queda en el historial.</li>
        <li><strong>Imprimir PDF:</strong> la rutina del socio sale en blanco y negro, con una columna para anotar el peso.</li>
      </Lista>
      <Sub>Seguimiento de peso y medidas</Sub>
      <p>
        Se anotan el peso y, si se midieron, la grasa corporal y los perímetros (cintura, cadera, pecho, brazo y muslo). Hay una
        medición por socio y por día. La pantalla muestra un gráfico de evolución y la variación desde la primera medición, y
        se puede imprimir en PDF.
      </p>
    </>
  ),

  calendario: (
    <>
      <Quien quien="todos" extra="Lo modifican el Administrador y el Contador; Recepción lo ve y lo imprime" />
      <p>
        El <B>Calendario</B> dice quién está de encargado en el gimnasio, de <strong>lunes a viernes</strong> y de <strong>06:00 a 22:00</strong>,
        en turnos de una hora. Es una semana tipo: se repite todas las semanas, no tiene fechas.
      </p>
      <Sub>Cargar o cambiar los encargados</Sub>
      <Pasos>
        <li>Apretá <B>Editar calendario</B>.</li>
        <li>
          Escribí el nombre en cada hora. Con <strong>Enter</strong> o las flechas pasás a la hora de abajo o de arriba. Para dejar una
          hora libre, borrá el nombre.
        </li>
        <li>
          Para cargar a una persona en varias horas y días de una vez, apretá <B>Asignar turno</B>: escribí el nombre, tildá los días
          y elegí <B>Desde las</B> y <B>Hasta las</B> (por ejemplo, Ana de lunes a viernes, de 06:00 a 14:00). El cuadro avisa a cuántos
          turnos ya cargados reemplaza. <B>Dejar libres esas horas</B> vacía ese rango.
        </li>
        <li>Apretá <B>Guardar cambios</B>. Hasta entonces nada queda guardado; <B>Cancelar</B> pide confirmación si hay cambios sin guardar.</li>
      </Pasos>
      <Lista>
        <li>La misma persona en horas seguidas se ve como un solo bloque, con un color suave por persona. Abajo figuran las horas semanales de cada una.</li>
        <li><B>Vaciar todo</B> deja el calendario sin nadie (recién se aplica al guardar).</li>
        <li>Cada cambio queda en la <B>Auditoría</B>: quién lo hizo y desde qué equipo.</li>
      </Lista>
      <Sub>Pasarlo a PDF</Sub>
      <p>
        <B>Imprimir (PDF)</B> abre el calendario en una hoja A4 apaisada, en blanco y negro y con el logo y los datos del negocio, lista para
        fotocopiar. Las horas libres quedan en blanco por si querés anotar a mano. El PDF sale con lo que está guardado: si tenés cambios
        sin guardar, el botón se desactiva hasta que los guardes o los canceles.
      </p>
      <Nota>
        Si la hoja se imprime sin nadie cargado, sale la grilla vacía: sirve para completarla a mano.
      </Nota>
    </>
  ),

  facturar: (
    <>
      <Quien quien="todos" />
      <p>
        <B>Facturar</B> sirve para vender y también para registrar compras: es la misma pantalla y lo primero que elegís es el
        <B>Tipo de Operación</B>.
      </p>
      <Pasos>
        <li>Elegí <B>Venta</B> (le vendés a un cliente) o <B>Compra</B> (le comprás a un proveedor).</li>
        <li>Controlá la <B>Fecha</B> y elegí el <B>Tipo</B> de comprobante: Factura A, B o C. La C no discrimina IVA.</li>
        <li>Elegí el <B>Cliente</B> (o el proveedor, en una compra). Si todavía no existe, cargalo antes en Clientes o en Proveedores.</li>
        <li>Elegí la <B>Condición de Pago</B>: <B>Contado (Caja)</B>, <B>Transferencia (Banco)</B> o <B>Cuenta Corriente</B> (queda como deuda del cliente con vos, o tuya con el proveedor).</li>
        <li>Cargá los ítems: <B>Cant.</B>, <B>Descripción</B> y <B>P. Unitario</B>. Con <B>Agregar ítem</B> sumás renglones y con <B>Quitar ítem</B> sacás uno.</li>
        <li>Elegí la alícuota de IVA (0 %, 10,5 %, 21 % o 27 %), revisá los totales y la <strong>Vista previa del asiento contable</strong>.</li>
        <li>Apretá <B>Emitir Factura</B>. <B>Limpiar</B> vacía el formulario si querés empezar de nuevo.</li>
      </Pasos>
      <Sub>Productos y conceptos</Sub>
      <p>
        En <B>Descripción</B> escribí el nombre de un producto y elegilo de la lista: el sistema propone su precio y mueve el
        stock. Si lo que escribís no es un producto (un servicio, un gasto), se factura solo como concepto y no toca el stock.
      </p>
      <Lista>
        <li>Al <strong>vender</strong> se propone el <strong>precio de venta</strong> del producto; al <strong>comprar</strong>, su precio de compra (el costo).</li>
        <li>Si cambiás entre Venta y Compra, los productos ya elegidos pasan al precio de la nueva operación. Un precio que escribiste a mano no se pisa.</li>
        <li>Debajo de cada producto se aclara de dónde salió el precio.</li>
        <li>Un producto que nació de una compra todavía no tiene precio de venta: escribilo en la primera venta y queda guardado.</li>
        <li>En una compra, si el renglón no es un producto del inventario, aparece <B>Producto nuevo: agregarlo al stock</B>. Tildalo para crearlo.</li>
      </Lista>
      <Sub>Numeración</Sub>
      <Lista>
        <li><strong>Ventas:</strong> el sistema las numera solo, en orden, por tipo de comprobante y punto de venta (por ejemplo, 0001-00000007).</li>
        <li><strong>Compras:</strong> el número de la factura del proveedor es opcional. Si lo dejás vacío, el sistema le pone un número interno con el punto de venta 9999 (por ejemplo, 9999-00000001), pensado para ejercicios y demostraciones. Si escribís el número real, no se puede cargar dos veces la misma factura del mismo proveedor.</li>
      </Lista>
      <Sub>Qué pasa al emitir</Sub>
      <p>
        El sistema registra el asiento en el Libro Diario, asienta la factura en el libro de IVA que corresponda, mueve Caja o
        Banco si fue al contado o por transferencia, y ajusta el stock (una venta lo resta; una compra lo suma). Si fue a cuenta
        corriente, queda en <B>Cuentas a cobrar y pagar</B>.
      </p>
      <Nota tipo="ojo">
        El sistema no deja vender más de lo que hay en stock. Si necesitás vender un producto que todavía no cargaste, primero
        registrá su compra.
      </Nota>
    </>
  ),

  'libros-iva': (
    <>
      <Quien quien="todos" extra="Editar y anular facturas es de Administrador y Contador" />
      <p>
        <B>Libro de Ventas</B> y <B>Libro de Compras</B> listan todas las facturas con su fecha, tipo y número, nombre, CUIT, neto,
        IVA, total y condición de pago. Con <B>Filtrar por fecha:</B> mostrás un día puntual.
      </p>
      <p>En cada factura tenés tres acciones:</p>
      <Lista>
        <li><B>Ver o imprimir la factura en PDF</B>.</li>
        <li><B>Editar factura</B>: el sistema revierte los efectos de la versión anterior (asiento, caja o banco y stock) y aplica los de la nueva.</li>
        <li><B>Anular factura</B>: la factura queda marcada como anulada, su asiento pasa a Anulado, se revierte el movimiento de Caja o Banco y se devuelve el stock.</li>
      </Lista>
      <Nota>
        Las facturas y los asientos confirmados <strong>nunca se borran</strong>: una factura anulada sigue en la base para que
        quede el rastro, pero no suma en el Diario, el Mayor, el Balance ni en el resumen de inicio.
      </Nota>
      <p>
        Arriba de cada libro están <B>Imprimir PDF</B> y <B>Exportar Excel</B>, con el mismo período y los mismos totales.
      </p>
    </>
  ),

  cuentas: (
    <>
      <Quien quien="gestion" />
      <p>
        Muestra, para cada cliente (pestaña <B>A cobrar (clientes)</B>) o proveedor (<B>A pagar (proveedores)</B>), lo facturado
        en cuenta corriente, lo cobrado o pagado, el saldo y la antigüedad de la deuda: de 0 a 30 días, de 31 a 60, de 61 a 90 y
        más de 90. Cada uno tiene su estado de cuenta en pantalla y en PDF.
      </p>
      <Sub>Registrar un cobro o un pago</Sub>
      <Pasos>
        <li>Andá a <B>Caja</B> (o a <B>Banco</B>) y apretá <B>Nuevo Movimiento</B>.</li>
        <li>Para un cobro, elegí <B>Ingreso</B> y la contrapartida <B>Cobro a clientes (Deudores por Ventas)</B>. Para un pago, elegí <B>Egreso</B> y <B>Pago a proveedores</B>.</li>
        <li>Elegí el cliente o el proveedor. Sin elegirlo, el sistema no deja guardar.</li>
      </Pasos>
      <Lista>
        <li>Los pagos no se asignan a una factura puntual: se aplican a las más antiguas primero, y lo que sobra queda como saldo a favor.</li>
        <li>Las facturas anuladas no cuentan. Si eliminás o corregís un cobro, la deuda se actualiza.</li>
        <li>El total debe ser igual al saldo de <em>Deudores por Ventas</em> (o <em>Proveedores</em>) del Libro Mayor. Si no coincide, la pantalla avisa y explica la causa probable.</li>
      </Lista>
    </>
  ),

  tesoreria: (
    <>
      <Quien quien="todos" extra="Cargar, editar y eliminar movimientos es de Administrador y Contador" />
      <p>
        <B>Caja</B> es el efectivo y <B>Banco</B> son los depósitos y las transferencias. Cada uno es un libro con sus movimientos,
        un resumen y el saldo. En Caja se separan los <strong>Ingresos (Ventas)</strong> de los <strong>Egresos (Compras)</strong>;
        en Banco, una sola tabla con <B>Depósito</B>, <B>Cheque / Transf.</B> y el <B>Saldo</B> después de cada movimiento.
      </p>
      <Lista>
        <li><B>Nuevo Movimiento:</B> elegís <B>Ingreso</B> o <B>Egreso</B> y la cuenta contra la que se asienta (Otros Ingresos, Gastos Generales, Cobro a clientes o Pago a proveedores). Cada movimiento genera su asiento automáticamente. Si lo editás o lo eliminás, el asiento anterior se anula.</li>
        <li><B>Transferir a Banco</B> / <B>Transferir a Caja:</B> es un egreso en un libro, un ingreso en el otro y un solo asiento. Una transferencia no se edita: si te equivocaste, eliminala (se borran los dos movimientos) y cargala de nuevo.</li>
        <li><B>Filtrar por fecha:</B>, <B>Imprimir PDF</B> y <B>Exportar Excel</B> funcionan igual que en los demás libros.</li>
      </Lista>
      <Nota tipo="ojo">
        La Caja puede quedar en negativo: el sistema avisa pero deja cargar el movimiento (por ejemplo, si registrás las compras
        de contado antes de los cobros). Revisá el orden de carga si no es lo que esperabas.
      </Nota>
      <Nota>
        Caja y Banco no se usan en los asientos manuales: son cuentas de control cuyo saldo sale solo de sus libros y de las
        facturas. Por eso el Libro Caja siempre coincide con el Mayor.
      </Nota>
    </>
  ),

  stock: (
    <>
      <Quien quien="todos" extra="Eliminar productos es de Administrador y Contador" />
      <Sub>Productos</Sub>
      <p>
        <B>Productos</B> es el catálogo con su costo, precio de venta y stock. Con <B>Nuevo Producto</B> cargás el <B>Nombre</B>,
        la <B>Marca</B> o el sabor, el <B>Precio Unitario (Costo)</B>, el <B>Precio Venta</B> y el <B>Stock Inicial</B>; la
        información nutricional es opcional.
      </p>
      <Nota tipo="ojo">
        Un producto que ya tiene ventas o compras registradas no se puede eliminar.
      </Nota>
      <Sub>Libro Stock</Sub>
      <p>
        Muestra, producto por producto, cada movimiento con su <B>Ingreso</B>, <B>Salida</B> y el <B>Saldo</B> resultante, más el
        precio unitario y el precio de venta. Con <B>Buscar producto:</B> encontrás uno puntual, y podés imprimirlo en PDF o
        exportarlo a Excel. El resumen de inicio avisa cuando un producto queda con 5 unidades o menos.
      </p>
    </>
  ),

  terceros: (
    <>
      <Quien quien="todos" extra="Dar de baja es de Administrador y Contador" />
      <p>
        En <B>Clientes</B> y <B>Proveedores</B> cargás a quienes les vendés o les comprás con <B>Nuevo Cliente</B> o
        <B>Nuevo proveedor</B>: <B>Nombre / Razón Social</B>, <B>CUIT / DNI</B> (el sistema controla que sea válido),
        <B>Dirección</B>, <B>Localidad</B> y la <B>Condición frente al IVA</B> (Responsable Inscripto, Monotributo, Exento o
        Consumidor Final).
      </p>
      <p>
        <B>Dar de baja</B> saca a la persona de las listas, pero sus facturas se conservan en los libros. Si más adelante la volvés
        a cargar con el mismo CUIT, se reactiva en lugar de duplicarse.
      </p>
    </>
  ),

  contabilidad: (
    <>
      <Quien quien="todos" extra="Control de integridad y Nuevo asiento son de Administrador y Contador" />
      <p>
        Todas las operaciones del sistema generan sus asientos solas. Estas pantallas te dejan verlos desde distintos ángulos.
      </p>
      <Lista>
        <li><strong>Libro Diario:</strong> todos los asientos en orden de fecha. Con <B>Formato planilla</B> se ven como una hoja corrida; con <B>Por asiento</B>, uno por uno con su detalle.</li>
        <li><strong>Libro Mayor:</strong> el movimiento de cada cuenta en forma de «T» (Crédito Fiscal, Débito Fiscal, Compras, Ventas, Caja y Banco), con su Debe, su Haber y el saldo.</li>
        <li><strong>Balance General:</strong> el Estado de Situación Patrimonial, con la distribución del Activo y la estructura financiera.</li>
        <li><strong>Nuevo asiento:</strong> para registrar un asiento a mano. Completá la <B>Fecha</B> y la <B>Descripción</B>, agregá las líneas con <B>Agregar línea</B> (cuenta, Debe y Haber) y apretá <B>Confirmar asiento</B>. El Debe tiene que ser igual al Haber: si no, el sistema no lo acepta.</li>
        <li><strong>Control de integridad:</strong> el sistema se audita a sí mismo con 16 controles de solo lectura (Debe igual a Haber, Caja y Banco contra el Mayor, IVA de las facturas contra el Mayor, cuentas a cobrar y a pagar, stock sin negativos, etc.). <B>Verificar de nuevo</B> lo repite. Si algo no cierra, indica la diferencia exacta.</li>
      </Lista>
      <Nota>
        Un asiento confirmado no se modifica. Si hay que corregirlo, se anula y queda como «Anulado» (sigue en la base, pero no suma).
        Los asientos de facturas y de movimientos se corrigen editando o anulando la operación que los originó.
      </Nota>
    </>
  ),

  reportes: (
    <>
      <Quien quien="todos" />
      <p>
        Hay dos formas de sacar los libros, y las dos respetan el período y los totales de lo que ves en pantalla.
      </p>
      <Lista>
        <li><strong>Desde cada libro:</strong> los botones <B>Imprimir PDF</B> y <B>Exportar Excel</B> están arriba de cada uno (Diario, Mayor, Balance, IVA Ventas e IVA Compras, Caja, Banco y Stock).</li>
        <li><strong>Reportes en PDF</strong> (en el grupo Contabilidad del menú): elegís el <B>Tipo de Libro</B>, o <B>Todos los libros (un solo PDF)</B>, y opcionalmente una <B>Fecha Desde</B> y una <B>Fecha Hasta</B>. Apretá <B>Generar</B>.</li>
      </Lista>
      <Nota>
        Los PDF están pensados para imprimirse o fotocopiarse: salen en blanco y negro, con los datos del negocio, el período,
        los totales y el número de página. Los datos del negocio se cargan en <B>Configuración</B>.
      </Nota>
    </>
  ),

  administracion: (
    <>
      <Quien quien="admin" />
      <Sub>Usuarios</Sub>
      <p>
        Con <B>Nuevo Usuario</B> das de alta a quien va a usar el sistema: nombre, email, teléfono y rol (<B>Jefe / Administrador</B>,
        <B>Contador</B> u <B>Operador / Recepción</B>). Desde la lista podés editarlo, marcarlo como activo o inactivo y
        ver su último acceso. Una baja o un cambio de rol rigen al instante, aunque la persona tenga la sesión abierta.
      </p>
      <Nota>
        Las contraseñas nuevas necesitan al menos 8 caracteres, una mayúscula, un número, un carácter especial (como ! ? # $ % &amp; * - _ .) y
        que no haya números consecutivos (como 12, 45 o 98). Mientras escribís, el formulario marca en verde lo que ya cumple. Cada
        persona debería cambiar la que le dieron al principio.
      </Nota>
      <Sub>Configuración</Sub>
      <Lista>
        <li><strong>Datos Fiscales y Empresa:</strong> razón social, nombre de fantasía, CUIT, condición frente al IVA, punto de venta, domicilio, teléfono y email. Estos datos salen en los PDF y en el menú.</li>
        <li><strong>Parámetros Contables y Facturación:</strong> valores por defecto como la alícuota de IVA.</li>
        <li><strong>Seguridad y Sistema:</strong> el tiempo de inactividad antes de cerrar la sesión y la política de contraseñas (que siempre está activa).</li>
        <li><strong>Copia de Seguridad:</strong> la <strong>copia completa del sistema</strong> (facturas, asientos y todos los datos: se descarga, se compara y se restaura desde acá; ver <B>Copias de seguridad</B>) y la descarga y carga de la configuración de la empresa. Al cargar cualquier archivo, antes de aplicar nada ves una <strong>vista previa</strong> que lo compara con lo que está en uso, y elegís si seguir o cancelar.</li>
        <li><strong>Política de seguridad:</strong> el botón <B>Ver política de seguridad</B>, en la pestaña Seguridad y Sistema, abre las reglas del sistema en esta guía.</li>
      </Lista>
      <p>Apretá <B>Guardar Configuración</B> para conservar los cambios; <B>Valores por Defecto</B> vuelve a los iniciales.</p>
      <Nota tipo="ojo">
        Los datos de Configuración se guardan en el navegador de la PC donde los cargás. Si abrís el sistema desde otro
        navegador u otra computadora, hay que cargarlos de nuevo. Los datos contables, en cambio, viven en la base de datos.
      </Nota>
      <Sub>Auditoría</Sub>
      <p>
        Registra automáticamente <strong>quién cambió qué y cuándo</strong>: fecha, usuario, tabla, operación, registro y los campos
        modificados, y además <strong>desde qué equipo</strong> se hizo: la <B>IP</B>, el sistema operativo y el navegador (columna
        <B>Equipo</B>). Es de solo lectura: nadie, ni el administrador, puede editarla desde el sistema. <B>Actualizar</B> trae lo último.
      </p>
      <p>
        También quedan registrados los <strong>inicios de sesión</strong> (elegí <B>Accesos</B> en el filtro de tablas): los correctos,
        los intentos fallidos (con el email que se probó) y los intentos sobre una cuenta bloqueada. La contraseña nunca se guarda.
        Lo mismo pasa con las <strong>copias de seguridad</strong> que se descargan o se restauran (filtro <B>Copias de seguridad</B>).
      </p>
      <Nota>
        El sistema operativo y el navegador los informa el propio equipo, así que sirven para saber desde dónde se trabajó, pero no como
        prueba. Si el sistema corre en una sola PC con Docker, la IP que se ve es la de Docker (algo como 172.18.0.1), no la de internet.
      </Nota>
    </>
  ),

  ayudas: (
    <>
      <Sub>Asistente</Sub>
      <p>
        El botón <B>Asistente</B> (abajo a la derecha, en todas las pantallas) contesta preguntas escritas con tus palabras, usando
        los datos del sistema. Funciona por reglas: reconoce palabras clave y períodos, consulta la base y arma la respuesta. No
        usa internet ni inteligencia artificial externa, así que anda igual sin conexión, y es de solo lectura (no cambia nada).
      </p>
      <Lista>
        <li><strong>Ventas y compras:</strong> «¿cuánto vendí este mes?», «ventas de marzo de 2026», «¿cuánto gasté el mes pasado?», «resultado de este año».</li>
        <li><strong>Impuestos y plata:</strong> «¿cuánto IVA tengo que pagar?», «¿cuánto hay en caja?», «¿quién me debe?», «¿a quién le debo?», «¿cuánto me debe Ferretería Sur?».</li>
        <li><strong>Stock:</strong> «¿cuál es el producto más vendido?», «¿cuánto stock hay de proteína?», «¿qué productos están sin stock?».</li>
        <li><strong>Gimnasio:</strong> «¿quién vino hoy?», «¿quiénes no vienen hace 30 días?», «¿cuántas cuotas vencen esta semana?», «¿quiénes no tienen rutina?», «ficha del DNI 23232323».</li>
        <li><strong>Comparaciones:</strong> «¿vendí más que el mes pasado?». Los períodos que entiende son hoy, ayer, esta semana, la semana pasada, este mes, el mes pasado, un mes con su nombre y este año.</li>
        <li><strong>Control:</strong> «¿está todo bien con la contabilidad?» y «dame un resumen».</li>
      </Lista>
      <p>
        Ignora tildes, mayúsculas y signos. Si no entiende la pregunta, lo dice y te sugiere otras. Lo financiero (ventas, caja,
        deudas, IVA, contabilidad y comparaciones) solo se le contesta al Administrador y al Contador; Recepción recibe ayuda con
        socios, cuotas, rutinas, ingresos, planes y stock.
      </p>
      <Sub>Campanita de avisos</Sub>
      <p>
        Arriba a la derecha, en todas las pantallas, muestra las mismas novedades del resumen y un número con las que requieren
        atención. Al entrar avisa una vez cuántas hay y, mientras trabajás, solo muestra un cartel cuando aparece un aviso nuevo.
        Se actualiza sola cada minuto y al cambiar de pantalla.
      </p>
    </>
  ),

  respaldo: (
    <>
      <Quien quien="admin" />
      <p>
        Los datos viven en un volumen de Docker: se pierden si se borra con <B>docker compose down -v</B> o si se rompe la PC.
        Por eso conviene hacer copias de seguridad. Hay dos caminos y los dos sirven.
      </p>
      <Sub>Desde el sistema</Sub>
      <p>
        En <B>Configuración</B>, pestaña <B>Copia de Seguridad</B>, sección <B>Copia completa del sistema</B>:
      </p>
      <Tabla
        columnas={['Para...', 'Qué hacer']}
        filas={[
          [
            'Guardar todo el sistema',
            <>Apretá <B>Descargar copia completa (.zip)</B>. El archivo incluye facturas, asientos, caja, banco, stock, socios, usuarios y auditoría.</>,
          ],
          [
            'Comparar una copia con el sistema actual',
            <>Con <B>Seleccionar copia (.zip)</B> elegís el archivo y ves una vista previa que compara, tabla por tabla, lo que hay en la copia con lo que hay ahora (cuántas filas y si cambian). No se modifica nada.</>,
          ],
          [
            'Volver a una copia',
            <>Desde esa vista previa, <B>Restaurar esta copia…</B>, escribí <B>RESTAURAR</B> para confirmar y <B>Restaurar ahora</B>. Antes de reemplazar nada, el sistema te descarga una copia de lo que hay.</>,
          ],
        ]}
      />
      <p>
        La copia se verifica antes de usarla: va firmada por el sistema, y se rechaza si está dañada, si fue modificada, si no tiene
        firma o si es de una versión más nueva que el sistema (la vista previa muestra el estado de la <B>Firma de la copia</B>). Restaurar es todo o nada: si algo falla, los datos quedan como estaban. Descargar y restaurar quedan en la
        <B>Auditoría</B>.
      </p>
      <Nota tipo="ojo">
        La copia incluye los datos de los socios y las contraseñas (en forma de hash), y no está cifrada: guardala en un lugar
        privado. Restaurar reemplaza <strong>todos</strong> los datos actuales.
      </Nota>
      <Sub>Desde PowerShell</Sub>
      <p>
        También se pueden pedir desde PowerShell, en la carpeta del sistema y con el sistema encendido (lo que usa la copia programada de las 22:00):
      </p>
      <Tabla
        columnas={['Para...', 'Comando']}
        filas={[
          ['Hacer una copia ahora (queda en la carpeta backups)', <code key="a" className="text-[13px]">.\scripts\backup.ps1</code>],
          ['Que se haga sola todos los días a las 22:00', <code key="b" className="text-[13px]">.\scripts\programar-backup.ps1</code>],
          [
            'Ver si una copia coincide con el sistema actual (no modifica nada)',
            <code key="d" className="break-all text-[13px]">.\scripts\comparar-backup.ps1 -Archivo .\backups\gymstudio_AAAAMMDD_HHMMSS.dump</code>,
          ],
          [
            'Restaurar una copia',
            <code key="c" className="break-all text-[13px]">.\scripts\restaurar.ps1 -Archivo .\backups\gymstudio_AAAAMMDD_HHMMSS.dump -Confirmar</code>,
          ],
        ]}
      />
      <p>
        Cada copia se verifica y trae su huella SHA-256 y un manifiesto con la versión del sistema y la huella de los datos de cada
        tabla. Con <B>comparar-backup.ps1</B> ves si el archivo está sano, de qué versión es y qué cambió desde que se hizo (o que es
        idéntica al sistema actual). Al restaurar se hace la misma comparación antes de reemplazar nada, y no se restaura una copia
        de una versión más nueva que el sistema instalado.
      </p>
      <Nota tipo="ojo">
        Guardá también la carpeta <strong>backups</strong> en un pendrive o en la nube: si se rompe la PC, las copias que están en
        ella se pierden también. Restaurar reemplaza los datos actuales (antes se hace una copia de lo que hay, por si te arrepentís).
      </Nota>
      <p>El detalle completo está en la pestaña <B>Copia de Seguridad</B> de Configuración.</p>
    </>
  ),

  politica: (
    <>
      <p>
        Es el conjunto de reglas con las que se protege la información del negocio: que cada persona vea solo lo que necesita
        (<strong>confidencialidad</strong>), que los datos no se alteren sin dejar rastro (<strong>integridad</strong>) y que se
        puedan recuperar si algo sale mal (<strong>disponibilidad</strong>). Rige para todas las personas que usan el sistema.
      </p>
      <Sub>Lo que el sistema hace solo</Sub>
      <Tabla
        columnas={['Tema', 'Regla']}
        filas={[
          ['Contraseñas', 'Mínimo 8 caracteres, con una mayúscula, un número, un carácter especial y sin números consecutivos (12, 98). Se guardan con hash, nunca en texto.'],
          ['Bloqueo', 'Cinco claves incorrectas seguidas bloquean la cuenta 15 minutos. El mensaje no revela si el email existe.'],
          ['Sesión', 'Vence a las 8 horas y se cierra sola tras un rato sin actividad (60 minutos por defecto).'],
          ['Permisos', 'Cada rol hace solo lo suyo, y lo controla el servidor, no solo la pantalla. Una baja o un cambio de rol rigen al instante.'],
          ['Datos', 'Todo lo que se escribe se valida (forma, largo y rango). Los asientos confirmados no se editan: se anulan.'],
          ['Auditoría', 'Cada cambio y cada ingreso registran quién, cuándo y desde qué equipo (IP, sistema y navegador). No se puede editar.'],
          ['Copias', 'Se verifican con una huella SHA-256 y un manifiesto. Restaurar pide confirmación escrita y todo se hace o no se hace.'],
          ['Red', 'La base, la API y el sistema solo se alcanzan desde la propia PC, salvo que el Administrador los abra a propósito.'],
        ]}
      />
      <Sub>Lo que te toca a vos</Sub>
      <Lista>
        <li>Usá solo tu cuenta y no compartas tu contraseña; cambiá la que te dieron al principio.</li>
        <li>Cerrá la sesión cuando te vayas de la PC, sobre todo si la usa más gente.</li>
        <li>Avisale al Administrador si ves un acceso que no hiciste o un dato que cambió solo.</li>
        <li>Los datos de los socios (nombre, DNI, teléfono, email) son personales: usalos solo para gestionar el gimnasio.</li>
      </Lista>
      <Sub>Si sos Administrador</Sub>
      <Lista>
        <li>Antes de cargar datos reales, reemplazá la cuenta y la clave de ejemplo del sistema.</li>
        <li>Dá de baja las cuentas que ya no se usan y asigná a cada persona el rol mínimo que necesita.</li>
        <li>Revisá la <B>Auditoría</B> al menos una vez por semana, en especial los <B>Accesos</B> y las <B>Copias de seguridad</B>.</li>
        <li>Hacé una copia todos los días y guardá al menos una <strong>fuera de la PC</strong> (pendrive o nube privada).</li>
        <li>Una vez por mes, probá una copia: compará el archivo con el sistema. Una copia que nunca se probó no es una copia.</li>
        <li>Con datos reales, ejecutá el sistema en modo <em>Production</em>, cambiá las claves de ejemplo y publicalo solo por HTTPS.</li>
      </Lista>
      <Sub>Si pasa algo raro</Sub>
      <Pasos>
        <li><strong>Frená</strong> el problema: desactivá la cuenta involucrada en <B>Usuarios</B> y cambiale la contraseña.</li>
        <li><strong>Investigá</strong> en la <B>Auditoría</B> qué se hizo y desde qué equipo, y corré el <B>Control de integridad</B>.</li>
        <li><strong>Recuperá</strong>: compará la última copia sana con el sistema y, si hace falta, restaurala.</li>
        <li><strong>Avisá</strong> a quien corresponda y anotá qué pasó para que no se repita.</li>
      </Pasos>
      <Nota tipo="ojo">
        Límites que conviene conocer: el sistema no tiene doble factor de autenticación y las copias no están cifradas. Por eso la
        custodia de las contraseñas y de los archivos de copia depende de las personas. El texto completo de la política está en
        el archivo <strong>docs/POLITICA_DE_SEGURIDAD.md</strong> del proyecto.
      </Nota>
    </>
  ),

  glosario: (
    <Tabla
      columnas={['Término', 'Qué significa']}
      filas={[
        ['Asiento', 'El registro contable de una operación: dice qué cuentas se debitan y cuáles se acreditan.'],
        ['Partida doble', 'Regla por la que en todo asiento el Debe es igual al Haber.'],
        ['Debe y Haber', 'Los dos lados de un asiento. Un ingreso de plata a la caja va al Debe de Caja; una venta va al Haber de Ventas.'],
        ['Libro Diario', 'Lista de todos los asientos en orden de fecha.'],
        ['Libro Mayor', 'El movimiento de cada cuenta por separado, con su saldo.'],
        ['Balance General', 'Foto del patrimonio: lo que tiene el negocio (Activo), lo que debe (Pasivo) y lo que le queda (Patrimonio).'],
        ['Neto', 'El importe sin IVA.'],
        ['IVA Crédito Fiscal', 'El IVA de las compras: se puede descontar.'],
        ['IVA Débito Fiscal', 'El IVA de las ventas: se le debe al fisco. La diferencia entre ambos es el IVA a pagar.'],
        ['Cuenta corriente', 'Venta o compra que se paga después: queda como deuda hasta que se cobre o se pague.'],
        ['Anulado', 'Estado de una factura o un asiento que ya no suma, pero que se conserva para que quede el rastro.'],
        ['Membresía', 'Período en que un socio puede usar el gimnasio, con su fecha de inicio y de vencimiento.'],
        ['Cuota por vencer', 'La que vence en los próximos 7 días. Con el vencimiento pasado se llama vencida.'],
        ['Auditoría', 'Registro automático de quién cambió qué y cuándo.'],
      ]}
    />
  ),

  problemas: (
    <>
      <Tabla
        columnas={['Si pasa esto...', 'Qué hacer']}
        filas={[
          ['No me deja vender un producto', 'Falta stock. Mirá el saldo en Libro Stock y registrá primero la compra del producto.'],
          ['No me deja guardar un cobro o un pago', 'Elegí el cliente o el proveedor: sin eso el sistema no guarda.'],
          ['No puedo eliminar un socio o un producto', 'Tienen movimientos registrados (ingresos de un socio, ventas o compras de un producto). Se conservan para que el historial quede completo.'],
          ['Se me bloqueó la cuenta', 'Fueron cinco claves incorrectas seguidas. Esperá 15 minutos o pedile al Administrador que te ayude.'],
          ['Me dice «Demasiados intentos de inicio de sesión»', 'El ingreso permite 10 intentos por minuto desde la misma PC. Esperá un minuto y probá de nuevo.'],
          ['Me sacó del sistema', 'La sesión se cierra tras un rato sin actividad (60 minutos por defecto, y se avisa con un cartel) o a las 8 horas. Volvé a ingresar.'],
          ['La Caja me quedó en negativo', 'Es un aviso, no un error: revisá que los cobros estén cargados antes de las compras de contado.'],
          ['Un total no coincide con otro', 'Entrá a Control de integridad y apretá Verificar de nuevo: señala qué control no cierra y por cuánto.'],
          ['Una pantalla muestra un error al cargar', 'Revisá que Docker esté abierto y el sistema encendido. Al volver a levantarlo, los datos siguen ahí.'],
          ['Veo intentos fallidos de ingreso en Auditoría que no hice yo', 'Elegí Accesos en el filtro de Auditoría: ahí figura cada intento con el email que se probó y el equipo. Si alguien probó con tu cuenta, cambiá la contraseña y avisale al administrador.'],
          ['Cargué algo por error', 'Una factura se edita o se anula desde su libro; un movimiento de Caja o Banco se edita o se elimina; un ingreso de un socio lo deshace el Administrador o el Contador.'],
        ]}
      />
      <Nota>
        ¿No encontrás lo que buscás? Probá con el botón <B>Asistente</B>: contesta en lenguaje común con los datos del sistema.
      </Nota>
    </>
  ),
}
