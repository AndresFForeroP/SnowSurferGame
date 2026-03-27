---
description: Lista y activa agentes (personas) definidos en el directorio .agents/agents/.
---

# Workflow: Desarrollo de Sistema de Juego

## Descripción
Este workflow ejecuta un flujo completo de desarrollo usando múltiples agentes especializados en Unity.

Convierte una idea del usuario en:
- Diseño de gameplay
- Arquitectura
- Código funcional
- Integración en Unity
- Pruebas y mejoras

---

## Flujo

### Paso 1 - Diseño
@designer_agent

Toma la idea del usuario y diseña el sistema.

IMPORTANTE:
- Define mecánicas claras
- Divide en partes simples
- NO escribas código

Guarda el resultado como: DESIGN_OUTPUT

---

### Paso 2 - Arquitectura
@architect_agent

Usa el siguiente diseño como base:

{DESIGN_OUTPUT}

Define:
- Estructura del sistema
- Organización de scripts
- Patrones a usar

Guarda el resultado como: ARCH_OUTPUT

---

### Paso 3 - Programación
@programmer_agent

Usa:

Diseño:
{DESIGN_OUTPUT}

Arquitectura:
{ARCH_OUTPUT}

Implementa:
- Código en C#
- Scripts listos para Unity

Guarda como: CODE_OUTPUT

---

### Paso 4 - Integración
@integrator_agent

Usa este código:

{CODE_OUTPUT}

Explica:
- Cómo implementarlo en Unity
- Qué GameObjects crear
- Cómo conectar componentes

Guarda como: INTEGRATION_OUTPUT

---

### Paso 5 - QA
@qa_agent

Analiza todo el sistema:

Diseño:
{DESIGN_OUTPUT}

Código:
{CODE_OUTPUT}

Detecta:
- Bugs
- Edge cases
- Mejoras

---

## Instrucción de uso

Cuando el usuario escriba una idea, ejecuta todos los pasos en orden.
Cada paso debe usar el resultado del anterior.