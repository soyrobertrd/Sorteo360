import { describe, expect, it } from 'vitest'
import {
  mapHeaders,
  parseParticipantMatrix,
  parseParticipantText,
  problemsToCsv,
  splitDelimitedLine,
} from './participantFile'

describe('parseParticipantMatrix', () => {
  it('une Nombre y Apellido y no confunde "Apellido" con el código', () => {
    const { rows, problems } = parseParticipantMatrix([
      ['Código', 'Nombre', 'Apellido', 'Correo', 'Departamento'],
      ['E-001', 'Ana', 'Pérez', 'ana@banco.com.do', 'Finanzas'],
    ])
    expect(problems).toEqual([])
    expect(rows).toEqual([
      {
        fullName: 'Ana Pérez',
        firstName: 'Ana',
        lastName: 'Pérez',
        email: 'ana@banco.com.do',
        company: undefined,
        department: 'Finanzas',
        location: undefined,
        employeeCode: 'E-001',
        phone: undefined,
        qrCode: undefined,
        checkedIn: undefined,
      },
    ])
  })

  it('lee "ID de empleado" como código y "Ubicación" como ubicación, que hace de área sin departamento', () => {
    const { rows, problems } = parseParticipantMatrix([
      ['Nombre', 'Apellido', 'ID de empleado', 'Correo', 'Ubicación'],
      ['Ana', 'Pérez', 'BR-000001', 'ana@banco.com.do', 'Santiago'],
    ])
    expect(problems).toEqual([])
    expect(rows[0]).toMatchObject({
      employeeCode: 'BR-000001',
      location: 'Santiago',
      department: 'Santiago',
      firstName: 'Ana',
      lastName: 'Pérez',
    })
  })

  it('con departamento y ubicación, cada uno va a su campo', () => {
    const { rows } = parseParticipantMatrix([
      ['Nombres', 'Apellidos', 'Departamento', 'Sucursal'],
      ['Luis', 'Gómez', 'Tesorería', 'Torre Banreservas'],
    ])
    expect(rows[0]).toMatchObject({
      fullName: 'Luis Gómez',
      department: 'Tesorería',
      company: undefined,
      location: 'Torre Banreservas',
    })
  })

  it('pone a todos la empresa de la carga cuando el archivo no la trae', () => {
    const { rows } = parseParticipantMatrix(
      [
        ['Nombre', 'Ubicación'],
        ['Luis Gómez', 'Oficina Pantoja'],
      ],
      { empresa: 'Banreservas' },
    )
    expect(rows[0]).toMatchObject({
      company: 'Banreservas',
      department: 'Oficina Pantoja',
      location: 'Oficina Pantoja',
    })
  })

  it('informa filas sin nombre, correos inválidos y códigos repetidos con su número de fila', () => {
    const { rows, problems } = parseParticipantMatrix([
      ['Nombre completo', 'Correo', 'Código'],
      ['Ana Pérez', 'ana@x.com', 'A1'],
      ['', 'nadie@x.com', 'A2'],
      ['Luis Gómez', 'no-es-correo', 'A3'],
      ['Ana Otra', 'otra@x.com', 'a1'],
    ])
    expect(rows.map((r) => r.fullName)).toEqual(['Ana Pérez', 'Luis Gómez'])
    expect(rows[1].email).toBeUndefined()
    expect(problems.map((p) => p.row)).toEqual([3, 4, 5])
  })

  it('importa a dos empleados con correo compartido; el segundo, sin correo', () => {
    const { rows, problems } = parseParticipantMatrix([
      ['Nombre completo', 'Correo', 'Código'],
      ['Ana Pérez', 'rrhh@x.com', 'A1'],
      ['Luis Gómez', 'RRHH@x.com', 'A2'],
    ])
    expect(rows.map((r) => [r.employeeCode, r.email])).toEqual([
      ['A1', 'rrhh@x.com'],
      ['A2', undefined],
    ])
    expect(problems).toEqual([
      { row: 3, message: expect.stringMatching(/compartido con la fila 2/) },
    ])
  })

  it('rechaza un encabezado sin columna de nombre', () => {
    const { rows, problems } = parseParticipantMatrix([
      ['Correo', 'Código'],
      ['a@x.com', '1'],
    ])
    expect(rows).toEqual([])
    expect(problems[0].message).toMatch(/Nombre/)
  })

  it('lee por posición cuando no hay encabezado', () => {
    const { rows } = parseParticipantMatrix([['Ana Pérez', 'ana@x.com', 'Finanzas', 'E1']])
    expect(rows[0]).toMatchObject({
      fullName: 'Ana Pérez',
      email: 'ana@x.com',
      department: 'Finanzas',
      employeeCode: 'E1',
    })
  })

  it('lee la asistencia como sí/no', () => {
    const { rows } = parseParticipantMatrix([
      ['Nombre', 'Asistencia'],
      ['Ana', 'Sí'],
      ['Luis', 'no'],
    ])
    expect(rows.map((r) => r.checkedIn)).toEqual([true, false])
  })
})

describe('parseParticipantText', () => {
  it('lee CSV con comillas y BOM', () => {
    const { rows } = parseParticipantText(
      '\uFEFFNombre completo;Departamento\n"Pérez; Ana";Finanzas\n',
    )
    expect(rows[0]).toMatchObject({ fullName: 'Pérez; Ana', department: 'Finanzas' })
  })

  it('lee un TXT con una persona por línea y "Nombre - Departamento"', () => {
    const { rows } = parseParticipantText('Nombre\nAna Pérez - Finanzas\nLuis Gómez\n')
    expect(rows.map((r) => [r.fullName, r.department])).toEqual([
      ['Ana Pérez', 'Finanzas'],
      ['Luis Gómez', undefined],
    ])
  })
})

it('splitDelimitedLine respeta comillas dobles escapadas', () => {
  expect(splitDelimitedLine('"a ""b""",c', ',')).toEqual(['a "b"', 'c'])
})

it('problemsToCsv escapa comillas', () => {
  expect(problemsToCsv([{ row: 2, message: 'Correo "x"' }])).toBe(
    'Fila;Problema\r\n2;"Correo ""x"""',
  )
})

describe('columnas elegidas a mano y ID automático', () => {
  const matriz = [
    ['Cédula', 'Colaborador', 'Sede'],
    ['001-1', 'Ana Pérez', 'Santiago'],
    ['', 'Luis Gómez', 'Santo Domingo'],
  ]

  it('lee cada dato de la columna elegida', () => {
    const columnas = { ...mapHeaders(matriz[0] as string[]), department: 2, location: -1 }
    const { rows } = parseParticipantMatrix(matriz, { columnas })
    expect(rows[0]).toMatchObject({
      employeeCode: '001-1',
      fullName: 'Ana Pérez',
      department: 'Santiago',
    })
  })

  it('da un ID correlativo a quien no lo trae', () => {
    const { rows } = parseParticipantMatrix(matriz, { generarCodigo: true })
    expect(rows.map((r) => r.employeeCode)).toEqual(['001-1', 'AUTO-00001'])
  })
})
