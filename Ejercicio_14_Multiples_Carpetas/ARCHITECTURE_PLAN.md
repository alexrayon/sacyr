# ARCHITECTURE_PLAN - Refactor con Inyeccion de Dependencias

## 1. Objetivo de arquitectura
Desacoplar el servicio de costes de la fuente de datos y de las politicas fiscales/energeticas para permitir:
- Test unitario aislado con mocks/fakes.
- Configuracion multi-pais y multi-proyecto.
- Sustitucion de infraestructura (archivo -> SQL) sin modificar logica de negocio.

## 2. Diseno propuesto (alto nivel)

### 2.1 Componentes
- CostAnalysisService (Application): orquesta calculos.
- FlotaRepositoryPort (Port): contrato para obtener maquinas.
- FiscalPolicyPort (Port): contrato para impuesto/amortizacion por contexto.
- FuelPricingPort (Port): contrato para precio de combustible.
- ClockPort (Port): contrato para obtener ano actual.
- Infrastructure adapters:
  - InMemoryFlotaRepository o CsvFlotaRepository
  - CountryFiscalPolicyProvider
  - StaticFuelPricingProvider o ApiFuelPricingProvider
  - SystemClock

### 2.2 Constructor por DI (conceptual)
CostAnalysisService debe recibir por constructor:
- repository: FlotaRepositoryPort
- fiscal_policy: FiscalPolicyPort
- fuel_pricing: FuelPricingPort
- clock: ClockPort
- inefficiency_rules: objeto de configuracion

Resultado:
- Ningun import directo desde data/ dentro del servicio.
- Todas las variaciones externas quedan inyectadas desde main.py.

## 3. Estrategia de resiliencia para servicios externos
Si fiscalidad o combustible vienen de APIs:
- Timeout estricto por llamada.
- Retry con backoff exponencial para fallos transitorios.
- Circuit breaker para evitar tormenta de reintentos.
- Fallback controlado (ultima configuracion valida cacheada) con evento de auditoria.

## 4. Por que DI habilita tests unitarios con mocks
Con el diseno actual, el servicio consume inventario real global, por lo que:
- No hay aislamiento.
- Cada test depende de datos compartidos.
- Cambios en datos rompen tests no relacionados.

Con DI:
- El test inyecta un FakeFlotaRepository con dataset minimo y determinista.
- El test inyecta FakeFiscalPolicy y FakeFuelPricing con valores controlados.
- El test inyecta FakeClock para controlar antiguedad.

Beneficios:
- Tests rapidos, repetibles y sin I/O.
- Cobertura de escenarios borde (impuesto 0, combustible alto, maquinaria alquilada, etc.).
- Menor costo de mantenimiento y mayor confianza en regresiones.

## 5. Plan de escalabilidad: archivo -> SQL sin tocar calculo
Patron aplicado: Ports and Adapters.

Escenario futuro:
1. Crear SqlFlotaRepository que implemente FlotaRepositoryPort.
2. Configurar cadena de conexion via variables de entorno.
3. Cambiar solo la composicion en main.py para inyectar SqlFlotaRepository.
4. Mantener CostAnalysisService sin cambios.

Impacto esperado:
- Cero cambios en formulas y reglas de negocio.
- Reduccion del riesgo funcional en migraciones de almacenamiento.

## 6. ADRs

### ADR-014-01: Inversion de dependencias en el servicio de costes
- Estado: Aprobado
- Contexto: El servicio importa datos concretos de infraestructura.
- Decision: Introducir puertos e inyeccion por constructor.
- Consecuencias:
  - Positivas: desacoplo, testabilidad, extensibilidad.
  - Coste: mayor numero de clases/interfaces inicial.

### ADR-014-02: Politica fiscal y energetica parametrizable por contexto
- Estado: Aprobado
- Contexto: IVA y combustible hardcodeados impiden operacion multi-pais.
- Decision: Extraer fiscalidad y combustible a proveedores configurables.
- Consecuencias:
  - Positivas: cumplimiento normativo por pais y proyecto.
  - Riesgo: necesidad de gobernanza de configuracion.

### ADR-014-03: Composition Root en main.py
- Estado: Aprobado
- Contexto: Instanciacion dispersa favorece acoplamiento accidental.
- Decision: Centralizar armado de dependencias en main.py.
- Consecuencias:
  - Positivas: visibilidad total del wiring.
  - Riesgo: main.py crece; se mitiga con factory/bootstrap module.

### ADR-014-04: Preparacion para infraestructura SQL
- Estado: Aprobado
- Contexto: Se anticipa crecimiento de volumen y concurrencia.
- Decision: Definir contrato de repositorio estable y adapter SQL futuro.
- Consecuencias:
  - Positivas: migracion controlada y sin impacto en dominio.
  - Riesgo: diseno prematuro si no se controla alcance; se limita al contrato minimo.
