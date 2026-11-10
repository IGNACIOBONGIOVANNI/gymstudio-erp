// Los países del selector de teléfono: código ISO (de ahí sale la bandera), código telefónico y nombre en español.
// Sin dependencias de React ni del navegador a propósito: así se prueba con el ejecutor de pruebas de Node.

export interface Pais {
  /** Código ISO de dos letras: «AR». La bandera se busca con él. */
  codigo: string
  /** Código telefónico sin el «+»: «54». */
  prefijo: string
  /** Nombre en español: «Argentina». */
  nombre: string
}

/** El país que aparece elegido cuando todavía no hay ningún número. */
export const PAIS_POR_DEFECTO = 'AR'

// ISO + código telefónico. Hay códigos que comparten varios países (+1 es de Estados Unidos, Canadá y el Caribe): el número que se
// guarda lleva solo el código, así que al volver a leerlo se muestra el país principal de ese código (ver PRINCIPAL_DEL_PREFIJO).
const TABLA: ReadonlyArray<readonly [string, string]> = [
  ['AF', '93'], ['AL', '355'], ['DZ', '213'], ['AS', '1'], ['AD', '376'], ['AO', '244'], ['AI', '1'], ['AG', '1'], ['AR', '54'],
  ['AM', '374'], ['AW', '297'], ['AU', '61'], ['AT', '43'], ['AZ', '994'], ['BS', '1'], ['BH', '973'], ['BD', '880'], ['BB', '1'],
  ['BY', '375'], ['BE', '32'], ['BZ', '501'], ['BJ', '229'], ['BM', '1'], ['BT', '975'], ['BO', '591'], ['BQ', '599'], ['BA', '387'],
  ['BW', '267'], ['BR', '55'], ['IO', '246'], ['VG', '1'], ['BN', '673'], ['BG', '359'], ['BF', '226'], ['BI', '257'], ['KH', '855'],
  ['CM', '237'], ['CA', '1'], ['CV', '238'], ['KY', '1'], ['CF', '236'], ['TD', '235'], ['CL', '56'], ['CN', '86'], ['CO', '57'],
  ['KM', '269'], ['CG', '242'], ['CD', '243'], ['CK', '682'], ['CR', '506'], ['CI', '225'], ['HR', '385'], ['CU', '53'], ['CW', '599'],
  ['CY', '357'], ['CZ', '420'], ['DK', '45'], ['DJ', '253'], ['DM', '1'], ['DO', '1'], ['EC', '593'], ['EG', '20'], ['SV', '503'],
  ['GQ', '240'], ['ER', '291'], ['EE', '372'], ['SZ', '268'], ['ET', '251'], ['FK', '500'], ['FO', '298'], ['FJ', '679'], ['FI', '358'],
  ['FR', '33'], ['GF', '594'], ['PF', '689'], ['GA', '241'], ['GM', '220'], ['GE', '995'], ['DE', '49'], ['GH', '233'], ['GI', '350'],
  ['GR', '30'], ['GL', '299'], ['GD', '1'], ['GP', '590'], ['GU', '1'], ['GT', '502'], ['GG', '44'], ['GN', '224'], ['GW', '245'],
  ['GY', '592'], ['HT', '509'], ['HN', '504'], ['HK', '852'], ['HU', '36'], ['IS', '354'], ['IN', '91'], ['ID', '62'], ['IR', '98'],
  ['IQ', '964'], ['IE', '353'], ['IM', '44'], ['IL', '972'], ['IT', '39'], ['JM', '1'], ['JP', '81'], ['JE', '44'], ['JO', '962'],
  ['KZ', '7'], ['KE', '254'], ['KI', '686'], ['XK', '383'], ['KW', '965'], ['KG', '996'], ['LA', '856'], ['LV', '371'], ['LB', '961'],
  ['LS', '266'], ['LR', '231'], ['LY', '218'], ['LI', '423'], ['LT', '370'], ['LU', '352'], ['MO', '853'], ['MG', '261'], ['MW', '265'],
  ['MY', '60'], ['MV', '960'], ['ML', '223'], ['MT', '356'], ['MH', '692'], ['MQ', '596'], ['MR', '222'], ['MU', '230'], ['YT', '262'],
  ['MX', '52'], ['FM', '691'], ['MD', '373'], ['MC', '377'], ['MN', '976'], ['ME', '382'], ['MS', '1'], ['MA', '212'], ['MZ', '258'],
  ['MM', '95'], ['NA', '264'], ['NR', '674'], ['NP', '977'], ['NL', '31'], ['NC', '687'], ['NZ', '64'], ['NI', '505'], ['NE', '227'],
  ['NG', '234'], ['NU', '683'], ['NF', '672'], ['KP', '850'], ['MK', '389'], ['MP', '1'], ['NO', '47'], ['OM', '968'], ['PK', '92'],
  ['PW', '680'], ['PS', '970'], ['PA', '507'], ['PG', '675'], ['PY', '595'], ['PE', '51'], ['PH', '63'], ['PL', '48'], ['PT', '351'],
  ['PR', '1'], ['QA', '974'], ['RE', '262'], ['RO', '40'], ['RU', '7'], ['RW', '250'], ['BL', '590'], ['SH', '290'], ['KN', '1'],
  ['LC', '1'], ['MF', '590'], ['PM', '508'], ['VC', '1'], ['WS', '685'], ['SM', '378'], ['ST', '239'], ['SA', '966'], ['SN', '221'],
  ['RS', '381'], ['SC', '248'], ['SL', '232'], ['SG', '65'], ['SX', '1'], ['SK', '421'], ['SI', '386'], ['SB', '677'], ['SO', '252'],
  ['ZA', '27'], ['KR', '82'], ['SS', '211'], ['ES', '34'], ['LK', '94'], ['SD', '249'], ['SR', '597'], ['SE', '46'], ['CH', '41'],
  ['SY', '963'], ['TW', '886'], ['TJ', '992'], ['TZ', '255'], ['TH', '66'], ['TL', '670'], ['TG', '228'], ['TK', '690'], ['TO', '676'],
  ['TT', '1'], ['TN', '216'], ['TR', '90'], ['TM', '993'], ['TC', '1'], ['TV', '688'], ['UG', '256'], ['UA', '380'], ['AE', '971'],
  ['GB', '44'], ['US', '1'], ['UY', '598'], ['UZ', '998'], ['VU', '678'], ['VA', '39'], ['VE', '58'], ['VN', '84'], ['VI', '1'],
  ['WF', '681'], ['EH', '212'], ['YE', '967'], ['ZM', '260'], ['ZW', '263'], ['AX', '358'],
]

// De los códigos que comparten varios países, el que se muestra al leer un número guardado.
const PRINCIPAL_DEL_PREFIJO: Readonly<Record<string, string>> = {
  '1': 'US', '7': 'RU', '39': 'IT', '44': 'GB', '212': 'MA', '262': 'RE', '358': 'FI', '590': 'GP', '599': 'CW',
}

/** Estos van primero en la lista: el país del gimnasio, sus vecinos y los que más se usan. */
export const CODIGOS_DESTACADOS: readonly string[] = ['AR', 'UY', 'CL', 'PY', 'BO', 'BR', 'PE', 'CO', 'VE', 'EC', 'MX', 'US', 'ES']

/** Minúsculas y sin tildes, para buscar «peru» y encontrar «Perú». */
export function sinTildes(texto: string): string {
  return texto.normalize('NFD').replace(/\p{M}/gu, '').toLocaleLowerCase('es')
}

const nombres = new Intl.DisplayNames(['es'], { type: 'region' })
const alfabetico = new Intl.Collator('es', { sensitivity: 'base' })

/** Todos los países, por orden alfabético del nombre en español. */
export const PAISES: readonly Pais[] = TABLA
  .map(([codigo, prefijo]) => ({ codigo, prefijo, nombre: nombres.of(codigo) ?? codigo }))
  .sort((a, b) => alfabetico.compare(a.nombre, b.nombre))

const PAIS_POR_CODIGO = new Map(PAISES.map((p) => [p.codigo, p]))
const PREFIJOS = new Set(PAISES.map((p) => p.prefijo))

export function paisPorCodigo(codigo: string): Pais | undefined {
  return PAIS_POR_CODIGO.get(codigo)
}

/** El país que se muestra para un código telefónico («54» → Argentina; «1» → Estados Unidos). */
export function paisDelPrefijo(prefijo: string): Pais | undefined {
  if (!PREFIJOS.has(prefijo)) return undefined
  const principal = PRINCIPAL_DEL_PREFIJO[prefijo]
  return principal ? PAIS_POR_CODIGO.get(principal) : PAISES.find((p) => p.prefijo === prefijo)
}

/**
 * El código telefónico con el que empieza una racha de dígitos («5491155551234» → «54»), o null si no empieza con ninguno.
 * Los códigos del mundo no son prefijo unos de otros, así que hay a lo sumo uno posible.
 */
export function prefijoAlComienzo(digitos: string): string | null {
  for (const largo of [1, 2, 3]) {
    const candidato = digitos.slice(0, largo)
    if (candidato.length === largo && PREFIJOS.has(candidato)) return candidato
  }
  return null
}

/** Los destacados primero y después el resto por orden alfabético: lo que se ve en la lista antes de escribir en el buscador. */
export const PAISES_ORDENADOS: readonly Pais[] = [
  ...CODIGOS_DESTACADOS.map((c) => PAIS_POR_CODIGO.get(c)).filter((p): p is Pais => p !== undefined),
  ...PAISES.filter((p) => !CODIGOS_DESTACADOS.includes(p.codigo)),
]

/** Los países que coinciden con lo escrito en el buscador: por nombre, por código telefónico («54» o «+54») o por código ISO («ar»). */
export function buscarPaises(texto: string): readonly Pais[] {
  const buscado = sinTildes(texto.trim())
  if (!buscado) return PAISES_ORDENADOS
  const numero = buscado.replace(/^\+/, '')
  if (/^[0-9]+$/.test(numero)) return PAISES.filter((p) => p.prefijo.startsWith(numero))
  const encontrados = PAISES.filter((p) => sinTildes(p.nombre).includes(buscado) || p.codigo.toLowerCase() === buscado)
  // Si lo escrito es justo un código ISO («ar»), ese país va primero y no entre todos los que llevan «ar» en el nombre.
  const exacto = encontrados.findIndex((p) => p.codigo.toLowerCase() === buscado)
  return exacto > 0 ? [encontrados[exacto], ...encontrados.slice(0, exacto), ...encontrados.slice(exacto + 1)] : encontrados
}
