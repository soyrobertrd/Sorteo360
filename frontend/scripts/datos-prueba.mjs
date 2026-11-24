// Genera los datos del ensayo en frontend/datos-prueba/ (fuera de git):
//   padron-25000.xlsx  25.000 filas, 50 con problemas a propósito
//   premios-10000.csv  20 rondas, 10.000 unidades, filas repetidas para el agrupado
//   padron-25.csv y premios-10.csv  para los topes del plan Gratis
// Uso (desde frontend/): node scripts/datos-prueba.mjs
// La semilla es fija: cada corrida da los mismos archivos.
import { mkdirSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import assert from 'node:assert/strict'
import * as XLSX from 'xlsx'

const OUT = join(dirname(fileURLToPath(import.meta.url)), '..', 'datos-prueba')
mkdirSync(OUT, { recursive: true })

// mulberry32: pequeño y determinista.
let seed = 360
const rand = () => {
  seed = (seed + 0x6d2b79f5) | 0
  let t = Math.imul(seed ^ (seed >>> 15), 1 | seed)
  t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296
}
const pick = (arr) => arr[Math.floor(rand() * arr.length)]

const NOMBRES = ['Ana', 'Luis', 'María', 'José', 'Carmen', 'Pedro', 'Rosa', 'Juan', 'Lucía', 'Carlos', 'Yesenia', 'Rafael', 'Altagracia', 'Miguel', 'Daniela', 'Ramón', 'Sofía', 'Héctor', 'Patricia', 'Wilson']
const APELLIDOS = ['Pérez', 'Gómez', 'Rodríguez', 'Martínez', 'Santana', 'Peña', 'Núñez', 'Reyes', 'Castillo', 'Almonte', 'Báez', 'Jiménez', 'Ureña', 'De la Cruz', 'Mejía', 'Tavárez', 'Polanco', 'Encarnación', 'Vargas', 'Féliz']
const DEPARTAMENTOS = ['Finanzas', 'Tecnología', 'Riesgo', 'Operaciones', 'Negocios', 'Recursos Humanos', 'Auditoría', 'Legal', 'Mercadeo', 'Tesorería']
const UBICACIONES = ['Torre Banreservas', 'Santiago', 'La Romana', 'San Pedro de Macorís', 'Puerto Plata', 'Higüey', 'San Francisco de Macorís', 'Baní']

const sinTildes = (s) => s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/\s+/g, '')

function padron(total, malas) {
  const filas = [['Nombre', 'Apellido', 'ID de empleado', 'Correo', 'Departamento', 'Ubicación', 'Teléfono']]
  for (let i = 1; i <= total; i++) {
    const nombre = pick(NOMBRES)
    const apellido = pick(APELLIDOS)
    const codigo = `BR-${String(i).padStart(6, '0')}`
    const correo = `${sinTildes(nombre)}.${sinTildes(apellido)}.${i}@banreservas.com.do`
    const tel = `809-${String(200 + Math.floor(rand() * 800))}-${String(Math.floor(rand() * 10000)).padStart(4, '0')}`
    filas.push([nombre, apellido, codigo, correo, pick(DEPARTAMENTOS), pick(UBICACIONES), tel])
  }
  // Filas malas repartidas por el archivo (índice de datos = fila del Excel - 1).
  // Intercaladas, para que las tres clases salgan entre las 20 que muestra el informe.
  const tipos = []
  const quedan = { ...malas }
  while (quedan.sinNombre + quedan.correo + quedan.repetido > 0)
    for (const t of ['sinNombre', 'correo', 'repetido'])
      if (quedan[t] > 0) {
        quedan[t]--
        tipos.push(t)
      }
  const paso = Math.floor(total / (tipos.length + 1))
  tipos.forEach((t, k) => {
    const i = paso * (k + 1)
    if (t === 'sinNombre') filas[i][0] = filas[i][1] = ''
    else if (t === 'correo') filas[i][3] = `correo-invalido-${k}`
    else filas[i][2] = filas[i - 1][2]
  })
  return filas
}

// 25.000 filas: 20 sin nombre y 10 con código repetido no entran; 20 entran sin correo.
const MALAS = { sinNombre: 20, correo: 20, repetido: 10 }
const grande = padron(25_000, MALAS)
const wb = XLSX.utils.book_new()
XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(grande), 'Padrón')
// El build ESM de xlsx no trae fs: se escribe el buffer.
writeFileSync(join(OUT, 'padron-25000.xlsx'), XLSX.write(wb, { type: 'buffer', bookType: 'xlsx' }))

const csv = (filas) => '﻿' + filas.map((f) => f.join(';')).join('\r\n') + '\r\n'
writeFileSync(join(OUT, 'padron-25.csv'), csv(padron(25, { sinNombre: 0, correo: 0, repetido: 0 })))

// Premios: 20 rondas de 500 unidades. Cada premio sale en varias filas seguidas iguales (el
// importador las suma) y algunos montos van en formato 1.250,75 o RD$5,000.00.
const CATALOGO = [
  ['Bono de compra', '1.250,75'],
  ['Tarjeta de regalo', 'RD$5,000.00'],
  ['Audífonos inalámbricos', '3500'],
  ['Cena para dos', '4.800,00'],
  ['Smart TV 55"', 'RD$38,000.00'],
  ['Fin de semana en Punta Cana', '25000'],
]
const premios = [['Ronda', 'Premio', 'Valor', 'Cantidad']]
for (let ronda = 1; ronda <= 20; ronda++) {
  let restante = 500
  for (let p = 0; restante > 0; p++) {
    const [nombre, valor] = CATALOGO[(ronda + p) % CATALOGO.length]
    const unidades = p === CATALOGO.length - 1 ? restante : Math.min(restante, 20 + Math.floor(rand() * 120))
    // Partido en filas de hasta 25: prueba el agrupado.
    for (let q = unidades; q > 0; q -= 25) premios.push([ronda, `"${nombre.replace(/"/g, '""')}"`, valor, Math.min(q, 25)])
    restante -= unidades
  }
}
writeFileSync(join(OUT, 'premios-10000.csv'), csv(premios))
writeFileSync(join(OUT, 'premios-10.csv'), csv([['Ronda', 'Premio', 'Valor', 'Cantidad'], [1, 'Bono de compra', '1.250,75', 5], [2, 'Tarjeta de regalo', 'RD$5,000.00', 5]]))

// Autocomprobación de lo que el ensayo da por hecho.
assert.equal(grande.length - 1, 25_000)
assert.equal(grande.filter((f) => f[0] === '').length, MALAS.sinNombre)
assert.equal(grande.filter((f) => f[3].startsWith('correo-invalido')).length, MALAS.correo)
assert.equal(grande.length - new Set(grande.map((f) => f[2])).size, MALAS.repetido)
assert.equal(premios.slice(1).reduce((s, f) => s + f[3], 0), 10_000)
assert.equal(new Set(premios.slice(1).map((f) => f[0])).size, 20)
console.log(`Listo en ${OUT}: padron-25000.xlsx, premios-10000.csv (${premios.length - 1} filas), padron-25.csv, premios-10.csv`)
