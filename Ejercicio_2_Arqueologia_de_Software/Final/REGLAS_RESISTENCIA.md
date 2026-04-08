# Reglas de Resistencia Estructural - Análisis de Proc_M_Check

## Introducción

Este documento detalla el análisis de ingeniería inversa del método legacy `Proc_M_Check`, un componente crítico para el cálculo de coeficientes de resistencia estructural. El método valida resistencias para materiales de construcción específicos, aplicando factores de corrección y protocolos de seguridad. Este análisis permite a los ingenieros de caminos validar las fórmulas sin necesidad de interpretar código fuente.

## Mapeo de Dominio

### Variables de Entrada

- **l (longitud/dimensión principal)**: Representa una dimensión lineal primaria en metros. En contextos estructurales, típicamente corresponde a la longitud o altura de un elemento estructural (ej. viga, columna).
- **w (ancho/dimensión secundaria)**: Representa una dimensión lineal secundaria en metros. Generalmente el ancho o espesor transversal del elemento estructural.
- **t (tipo/factor de corrección)**: Parámetro entero que indica el tipo de carga o condición estructural:
  - `t = 1`: Condiciones estándar o carga normal
  - `t = 2`: Condiciones de carga elevada o factores de seguridad reducidos
- **m (material)**: Código del material estructural:
  - `"H400"`: Hormigón de alta resistencia (Hormigón)
  - `"A500"`: Acero estructural de alta resistencia (Acero)

### Interpretación Técnica

Las operaciones matemáticas sugieren que:
- Para hormigón: Se calcula un área efectiva (l × w) multiplicada por un factor de reducción de resistencia
- Para acero: Se calcula una dimensión compuesta (l + w) multiplicada por un factor de amplificación de resistencia

## Lógica de Ingeniería

### Material H400 (Hormigón)

El cálculo de resistencia se basa en el área transversal efectiva con factores de corrección por tipo de carga:

#### Fórmula General
```
Resistencia = Área_Efectiva × Factor_Corrección
```

Donde:
- Área_Efectiva = l × w (en m²)
- Factor_Corrección depende del parámetro t

#### Casos Específicos

**Condición t = 1 (Carga Estándar):**
```
Resistencia = (l × w) × 0.95
```
- Factor de corrección: 0.95 (95% de la resistencia nominal)
- Aplicable a cargas normales de servicio

**Condición t = 2 (Carga Elevada):**
```
Resistencia = (l × w) × 0.88
```
- Factor de corrección: 0.88 (88% de la resistencia nominal)
- Aplicable a sobrecargas o condiciones extremas

### Material A500 (Acero)

El cálculo considera dimensiones lineales compuestas con factores de amplificación:

#### Fórmula General
```
Resistencia = Dimensión_Compuesa × Factor_Amplificación
```

Donde:
- Dimensión_Compuesa = l + w (en metros)
- Factor_Amplificación depende del parámetro t

#### Casos Específicos

**Condición t = 1 (Carga Estándar):**
```
Resistencia = (l + w) × 1.45
```
- Factor de amplificación: 1.45 (145% de la resistencia base)
- Considera efectos de ductilidad y reserva plástica

**Condición t = 2 (Carga Elevada):**
```
Resistencia = (l + w) × 1.10
```
- Factor de amplificación: 1.10 (110% de la resistencia base)
- Reducción conservadora para condiciones críticas

## Protocolo de Seguridad

### Umbral Crítico y Validación

El sistema implementa un protocolo de seguridad de dos niveles:

#### Nivel 1: Umbral de Alerta (5000 unidades)
- Activado cuando la resistencia calculada supera 5000
- Solo aplicable a material H400 (Hormigón)
- Inicia validación de seguridad externa

#### Nivel 2: Validación de Seguridad Externa
- Función `Check_Legacy_Security_V2`
- Verifica que la resistencia no exceda 20000 unidades
- Si la validación externa falla, el cálculo es rechazado

#### Comportamiento por Material

**Hormigón (H400):**
- Si Resistencia ≤ 5000: Sin decisión explícita (retorna null)
- Si 5000 < Resistencia ≤ 20000: Aprobado tras validación externa
- Si Resistencia > 20000: Rechazado

**Acero (A500):**
- Umbral mínimo de resistencia: 150 unidades
- Si Resistencia < 150: Rechazado (falla por resistencia insuficiente)
- Si Resistencia ≥ 150: Aprobado

### Códigos de Retorno
- `1`: Cálculo aprobado
- `0`: Cálculo rechazado
- `-1`: Error de entrada (dimensiones inválidas)
- `null`: Sin decisión (hormigón con resistencia ≤ 5000) o material no soportado

## Escenarios de Comportamiento (BDD)

### Escenario 1: Cálculo Estándar de Hormigón
**Given** un elemento de hormigón H400 con dimensiones l=5.0m, w=0.3m y condición estándar (t=1)  
**When** se ejecuta el cálculo de resistencia  
**Then** la resistencia calculada debe ser (5.0 × 0.3) × 0.95 = 1.425 unidades, y el resultado debe ser null (sin decisión explícita)

### Escenario 2: Fallo por Resistencia Mínima en Acero
**Given** un elemento de acero A500 con dimensiones l=2.0m, w=1.5m y condición elevada (t=2)  
**When** se ejecuta el cálculo de resistencia  
**Then** la resistencia calculada debe ser (2.0 + 1.5) × 1.10 = 3.85 unidades, que es menor que 150, por lo que debe ser rechazado (código 0)

### Escenario 3: Activación del Protocolo de Seguridad
**Given** un elemento de hormigón H400 con dimensiones l=100.0m, w=0.5m y condición estándar (t=1)  
**When** se ejecuta el cálculo de resistencia  
**Then** la resistencia calculada debe ser (100.0 × 0.5) × 0.95 = 47.5 unidades, que supera 5000, activando la validación externa; dado que 47.5 ≤ 20000, debe ser aprobado (código 1)

---

**Nota Técnica:** Este análisis se basa en la ingeniería inversa del código legacy. Se recomienda validar experimentalmente las fórmulas con datos reales de laboratorio antes de su implementación en producción.
