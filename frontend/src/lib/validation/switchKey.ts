/**
 * La forma de clave de interruptor que acepta crm-service (`FeatureSwitchKey`, docs/adr/0039):
 * minúsculas, guiones entre palabras y un punto como mucho, en 100 caracteres o menos. La comparten
 * el mock, que rechaza como el servicio, y la prueba del catálogo.
 */
export function isWellFormedSwitchKey(key: string): boolean {
  return key.length <= 100 && /^[a-z]+(-[a-z]+)*(\.[a-z]+(-[a-z]+)*)?$/.test(key)
}
