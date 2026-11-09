# 1. Visión del producto

## 1.1 Problema y propósito

Una empresa logística necesita conocer el estado de sus envíos y recursos operativos, coordinar vehículos y conductores, anticipar incidencias y comprender el rendimiento de sus operaciones. DROVIK presenta esos flujos desde una consola central demostrativa.

DROVIK es un proyecto de portafolio de ejecución local. Su repositorio público contiene el código y la documentación para que una persona interesada pueda clonar el proyecto y ejecutarlo en su equipo. No se contempla alojar la aplicación completa en un servidor público; el despliegue público no es requisito funcional, técnico ni criterio de finalización.

## 1.2 Usuarios y alcance

- **Administrator:** administra usuarios, catálogos y configuración de la demo; tiene acceso completo.
- **Operator:** consulta la operación diaria, gestiona envíos y asignaciones, ejecuta escenarios permitidos y atiende incidentes.
- Los conductores son registros operativos, no cuentas de acceso.

La demostración representa una sola empresa ficticia que opera en Bogotá y mueve envíos entre una red cerrada de 8 puntos (ADR 0012), con datos sintéticos reproducibles. No incluye GPS real, tráfico en vivo ni información personal real. La consola se presenta como una operación real; el README declara que posiciones y eventos son simulados.

## 1.3 Presentación del portafolio

El producto se presenta mediante ejecución local, capturas de pantalla y, opcionalmente, un video corto. El README debe describir el producto, arquitectura, tecnologías y pasos reproducibles. Las capturas se añadirán cuando existan; no se presentarán imágenes ficticias como producto implementado.

## 1.4 Criterios de éxito

- Los flujos principales se pueden demostrar localmente con datos sintéticos reproducibles.
- El mapa refleja las posiciones que calcula el servidor.
- Las incidencias afectan visiblemente el estado operativo.
- Los servicios se ejecutan con instrucciones reproducibles y sin depender de servicios externos obligatorios para los flujos principales.
- Las pruebas y decisiones de arquitectura permiten explicar y verificar los límites distribuidos.
