# ADR 0002: Microsoft SQL Server

- Estado: Aceptado.
- Contexto: Se desea practicar un motor relacional distinto de PostgreSQL y profundizar en el ecosistema Microsoft/.NET.
- Decisión: SQL Server con Entity Framework Core.
- Consecuencias: aprendizaje de T-SQL y operación de SQL Server; compatibilidad natural con EF Core.
- Nota: bases lógicas separadas por servicio; en desarrollo pueden compartir una instancia. No compartir tablas ni acceso entre servicios.
