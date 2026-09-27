# Formularios de la Play Console — respuestas para piRadar

Basadas en cómo está construida la app (septiembre de 2026): sin permiso de Internet en Release, BD cifrada en el dispositivo y OCR local.

## Crear la app
- Nombre: piRadar · Idioma predeterminado: Español (Latinoamérica)
- App o juego: **App** · Gratuita o de pago: **Gratuita** (no se puede cambiar a pago después)
- Paquete: se fija con el primer `.aab` subido → `com.jhbelalc.piradar`

## Firma de apps (Play App Signing)
Elegir **usar tu propia clave de firma** (exportar `piradar.keystore` con la herramienta PEPK que indica la consola), **no** la que genera Google. Así la versión de Play se instala encima del APK que ya está en el teléfono y no hay que desinstalar.

## Acceso a la app
- **Toda la funcionalidad está disponible sin restricciones especiales** (no hay inicio de sesión).

## Anuncios
- **No, la app no contiene anuncios.**

## Clasificación de contenido
- Categoría: **Utilidad, productividad, comunicación u otra**.
- Violencia, sexualidad, lenguaje, drogas, apuestas: **No** en todo.
- ¿Los usuarios interactúan o comparten contenido entre sí? **No**. ¿Comparte la ubicación? **No**. ¿Compras digitales? **No**.
- Resultado esperado: apta para todos (PEGI 3 / Everyone).

## Público objetivo
- Edades: **18 años o más** (finanzas personales; evita los requisitos de la política de familias).
- ¿Atrae a niños sin querer? **No**.

## Seguridad de los datos (Data safety)
- ¿La app recopila o comparte alguno de los tipos de datos requeridos? **No.**
  - Justificación: los datos se procesan y guardan solo en el dispositivo; Google no los considera "recopilados" si nunca salen de él. La app no tiene permiso `INTERNET` en Release (verificado con `aapt dump permissions`), y eso incluye a las librerías: ML Kit no puede enviar su telemetría.
  - El respaldo lo exporta el usuario a donde él elige con el menú de compartir: es una acción del usuario, no una transmisión al desarrollador.
- Con "No" la consola no pregunta por cifrado en tránsito ni por borrado de datos.

## Funciones financieras (declaración)
- piRadar solo registra movimientos que el usuario ingresa. No da préstamos, no mueve dinero, no se conecta a bancos ni maneja criptomonedas.
- Marcar la opción de que la app **no ofrece servicios financieros** de la lista; si aparece una opción de gestión de finanzas personales o presupuesto sin transacciones, usar esa. Revisar las opciones exactas en la consola.

## Salud, apps gubernamentales, noticias, COVID-19
- **No** en todas.

## Política de privacidad (URL obligatoria)
El repositorio es privado, así que la política tiene que publicarse aparte, en una URL pública y estable:
- **Google Sites** (gratis, con la misma cuenta de Google): pegar el texto de `politica-privacidad.md`.
- Un repositorio público aparte con GitHub Pages (p. ej. `jhbelalc.github.io/piradar-privacidad`).

## Pruebas antes de producción
- Cuenta **personal** creada después del 13-nov-2023: hace falta una **prueba cerrada con ≥ 12 testers durante 14 días seguidos** antes de pedir acceso a producción.
- Para uso propio basta con la **prueba interna** (hasta 100 testers por correo, disponible en minutos, sin ese requisito).
