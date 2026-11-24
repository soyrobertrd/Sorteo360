/**
 * Los datos del titular que aparecen en los términos y la privacidad. Robert los completa antes de
 * publicar (plans/sorteo360.md §9.8); mientras digan "[...]" la página avisa que es un borrador.
 */
export const TITULAR = {
  razonSocial: '[Razón social del titular de Sorteo360]',
  rnc: '[RNC]',
  domicilio: '[Dirección], Santo Domingo, República Dominicana',
  correo: '[correo de contacto]',
  correoPrivacidad: '[correo para temas de privacidad]',
}

export const VIGENTE_DESDE = '19 de noviembre de 2026'

export const ES_BORRADOR = Object.values(TITULAR).some((v) => v.includes('['))
