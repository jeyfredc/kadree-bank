# Kadree Bank: manual de usuario

Este manual explica cómo usar la banca en línea de Kadree Bank: crear tu cuenta, ingresar, consultar tus saldos y movimientos, consignar, retirar y descargar extractos. Al final hay una sección para los funcionarios del banco (rol **Banco**) sobre los reportes.

> Dirección de la aplicación en el ambiente local: **http://localhost:4200**

---

## 1. Conceptos básicos

| Término | Qué significa |
|---|---|
| **Cuenta de ahorros** | La que se abre a una **persona natural**. Su número empieza por `AH-` |
| **Cuenta corriente** | La que se abre a una **empresa**. Su número empieza por `CC-` |
| **Ciudad de origen** | La ciudad donde se abrió la cuenta. No se puede cambiar |
| **Consignación** | Dinero que entra a tu cuenta |
| **Retiro** | Dinero que sale de tu cuenta. Nunca puedes retirar más de tu saldo |
| **Usuario** | Nombre con el que ingresas, por ejemplo `ana.gomez` |
| **Contraseña** | Clave de **4 dígitos** que eliges al registrarte. Nadie de Kadree Bank te la pedirá |

Los montos se muestran en **pesos colombianos**, con punto para miles y coma para centavos (por ejemplo, `$ 1.250.000,50`). Las fechas y horas están en **hora de Colombia**.

---

## 2. Crear tu cuenta (registro)

1. En la pantalla de ingreso, pulsa **Crear cuenta**.
2. **Paso 1, tus datos:**
   - Elige **Persona natural** o **Empresa**. A una persona natural se le abre una cuenta de ahorros y a una empresa, una cuenta corriente.
   - Tipo y número de documento:
     - **Cédula de ciudadanía o de extranjería**: de 6 a 10 dígitos, sin puntos.
     - **Pasaporte**: de 6 a 12 letras o números.
     - **NIT** (empresas): 9 dígitos, guion y dígito de verificación, por ejemplo `900123456-1`.
   - Nombre completo o razón social.
   - Correo electrónico.
   - Celular: 10 dígitos, empieza por 3 y va sin espacios.
   - **Ciudad**: será la ciudad de origen de tu cuenta.
3. **Paso 2, tu acceso:**
   - **Nombre de usuario**: de 6 a 20 caracteres, solo letras minúsculas, números y punto, con al menos una letra. Al salir del campo verás si está disponible.
   - **Contraseña de 4 dígitos**. Mientras escribes, se marcan en verde las reglas que cumples:
     - Tiene 4 dígitos.
     - No repite el mismo número (como `1111`).
     - No es una secuencia (como `1234` o `4321`).
     - Es distinta de los últimos 4 dígitos de tu documento.
   - Confirma la contraseña y **acepta los términos y condiciones**.
4. **Paso 3, confirmación:** revisa tus datos. Con **Editar** vuelves al paso correspondiente. Pulsa **Crear cuenta**.
5. Verás **«Tu cuenta está lista»** con tu número de cuenta, el tipo, la ciudad de origen y tu usuario. Guarda estos datos y pulsa **Ingresar a mi banca**.

Tu cuenta nueva empieza con saldo **$ 0**.

**Si algo falla:**

| Mensaje | Qué hacer |
|---|---|
| El documento ya está registrado | Ya tienes una cuenta: ingresa con tu documento o tu usuario |
| El usuario no está disponible | Elige otro nombre de usuario |
| Las contraseñas no coinciden | Escribe la misma contraseña en los dos campos |
| Un campo marcado en rojo | Lee el mensaje debajo del campo y corrígelo |

---

## 3. Ingresar

1. Escribe tu **usuario** o tu **número de documento**, sin puntos.
2. Escribe tu **contraseña de 4 dígitos**. El ícono del ojo la muestra u oculta.
3. Si quieres, marca **Recordar mi usuario**. Así, este navegador lo escribirá por ti la próxima vez; la contraseña nunca se guarda.
4. Pulsa **Ingresar**.

| Situación | Qué pasa |
|---|---|
| Usuario o contraseña incorrectos | Verás «Usuario o contraseña incorrectos». Por seguridad no te decimos cuál de los dos falló |
| **3 intentos fallidos seguidos** | El acceso se **bloquea 15 minutos** y se desbloquea solo. El mensaje te dice cuántos minutos faltan |
| Muchos intentos en poco tiempo | «Hiciste demasiados intentos seguidos»: espera un minuto |
| Tu sesión terminó | Después de un tiempo sin actividad, o si cerraste sesión en otra pestaña, vuelves a la pantalla de ingreso con un aviso. Ingresa de nuevo |

Tu sesión dura hasta **7 días** en el mismo navegador mientras la uses. Pulsa **Salir** (arriba a la derecha) al terminar, sobre todo en computadores compartidos.

---

## 4. Inicio

Al ingresar verás:

- **Saldo total** de todas tus cuentas. El botón del ojo **oculta o muestra los saldos**; el navegador recuerda tu preferencia.
- **Accesos rápidos**: Consignar, Retirar y Extractos.
- **Tus productos**: una tarjeta por cuenta, con tipo, número, ciudad de origen y saldo disponible. Pulsa una tarjeta para ver su detalle.
- **Movimientos recientes** de tu primera cuenta y el enlace **Ver todos los movimientos**.

En pantallas pequeñas, el menú se abre con el ícono ☰.

---

## 5. Detalle de una cuenta

Muestra:

- El saldo disponible, el titular, el documento y la ciudad de origen.
- Botones para **Consignar**, **Retirar** y **Ver extracto** de esa cuenta.
- Los **últimos 50 movimientos**, agrupados por día («HOY» para los de hoy). Los botones **Todos**, **Consignaciones** y **Retiros** filtran la lista.

Cada movimiento muestra la descripción, la ciudad, la hora, el monto y el saldo que quedó después. Las consignaciones aparecen en verde con «+» y los retiros, con «−».

---

## 6. Consignar y retirar

1. Elige **Consignar** o **Retirar** (desde el inicio o desde el detalle de la cuenta).
2. Completa el formulario:
   - **Cuenta**: debajo se muestra su saldo disponible.
   - **Monto**: en pesos. Puedes escribir `50000`, `50.000` o `50.000,50`. Admite máximo dos decimales.
   - **Ciudad donde haces la operación**: por defecto, la ciudad de origen de la cuenta.
   - **Descripción** (opcional): por ejemplo «Nómina» o «Cajero». Máximo 200 caracteres.
3. Pulsa **Continuar**. Verás un **resumen** con el monto, la cuenta, la ciudad y el **saldo después** de la operación.
4. Pulsa **Confirmar consignación** o **Confirmar retiro**. Con **Editar** vuelves al formulario.
5. Verás el **comprobante**: monto, cuenta, ciudad, fecha y hora, número de comprobante y nuevo saldo.

**Reglas:**

- **No puedes retirar más de tu saldo.** Si lo intentas, verás «El monto supera tu saldo disponible» y no se hace ningún cargo.
- Puedes retirar en una ciudad distinta a la de origen de la cuenta. La pantalla te lo indica, y el banco usa esa información en sus reportes.
- Si dos operaciones sobre la misma cuenta ocurren al mismo tiempo, se procesan una después de la otra, así que el saldo siempre queda correcto. Si ves «La cuenta está siendo usada por otra operación», intenta de nuevo en unos segundos.

---

## 7. Extractos

1. Entra a **Extractos** (menú superior).
2. Elige la **cuenta** y el **mes**, y pulsa **Consultar**.
3. Verás:
   - El período (del primer al último día del mes).
   - El **saldo inicial**, el **total consignado**, el **total retirado** y el **saldo final**.
   - Los **movimientos del mes**, con filtros.
4. **Imprimir** abre el diálogo de impresión del navegador. Desde ahí también puedes guardar el extracto como PDF.

El extracto usa fondo claro para que sea fácil de leer e imprimir. No se pueden consultar meses futuros.

---

## 8. Administrador del banco

El banco tiene **un único administrador**: el usuario `admin.banco`, con rol **Banco**. Para ingresar, escribe `admin.banco` y su contraseña de 4 dígitos en la pantalla de ingreso (en el ambiente de pruebas es `2580`). Entrarás directamente a **Administración**.

El administrador **consulta** pero **no opera**: no ve los botones de consignar ni retirar, y la API rechaza esas operaciones.

### Cuentas

En **Cuentas** se ven **todas las cuentas** del banco, con titular, documento, ciudad de origen y saldo. Pulsando una se ve su detalle y sus movimientos, y desde **Extractos** se consulta el extracto de cualquier cuenta.

### Administración

La sección **Administración** (menú superior) tiene tres consultas. Cada una se puede **descargar en Excel o PDF** con los botones **Excel** y **PDF**: el archivo incluye el título, los filtros aplicados, la fecha de generación y la misma tabla que ves en pantalla.

1. **Transacciones por sede**: clientes con su número de transacciones en un mes, de mayor a menor.
   - **Mes**: el mes a consultar (no puede ser futuro).
   - **Sede**: **Todas las sedes**, o una ciudad (Bogotá, Medellín, Cali, Barranquilla, Cartagena, Bucaramanga). La sede es la **ciudad donde se hizo la transacción**.
   - **Tipo de movimiento**: consignaciones y retiros, solo consignaciones o solo retiros.
   - Cada cliente forma un **bloque**. Sede, cliente, documento y **total del cliente** aparecen una sola vez, y debajo hay **una fila por cada transacción**, en orden cronológico: **fecha y hora**, **motivo** (**Consignación** con etiqueta verde ↙, o **Retiro** con etiqueta naranja ↗), **monto** (+ lo que entra, − lo que sale), **cuenta** y **descripción**. Así se ve cuánto fue cada consignación y cada retiro, y cuándo se hizo.
   - Con **Todas las sedes**, un cliente que hizo transacciones en varias ciudades tiene un bloque por cada sede.
   - Solo aparecen los clientes con al menos una transacción que cumpla los filtros. Encima de la tabla verás los filtros usados, el número de filas y el total de transacciones.
2. **Transacciones por cliente**: elige un mes y pulsa **Consultar**. Muestra cada cliente con su número de consignaciones y retiros en ese mes (todas las sedes), de mayor a menor. Los clientes sin movimientos aparecen con 0.
3. **Retiros fuera de la ciudad de origen**: muestra los clientes cuyo total retirado en ciudades **distintas** a la de origen de la cuenta **supera** el umbral.
   - **Umbral**: por defecto $ 1.000.000. Un cliente que retiró exactamente el umbral no aparece.
   - **Desde y Hasta** (opcionales): limitan el período. Sin fechas, se considera todo el histórico.
   - Para cada cliente se muestra el número de retiros y el total retirado.

Todas las tablas tienen dos columnas de fecha: **Primera transacción** y **Última transacción** de esa fila dentro del período consultado. En el reporte de retiros se llaman **Primer retiro** y **Último retiro**. Si un cliente no tuvo transacciones, aparece «—». Las fechas están en hora de Colombia y también salen en las descargas de Excel y PDF.

**Paginación.** Las tres tablas muestran los resultados por páginas. Debajo de cada tabla verás «Mostrando 1–10 de 135 clientes», las flechas ‹ ›, los números de página y **Por página**, para elegir cuántos clientes ver a la vez. En «Transacciones por sede» se pagina por cliente: un cliente siempre aparece completo, con todas sus transacciones, en la misma página. Al pulsar **Consultar** o **Limpiar** vuelves a la página 1. Las descargas en **Excel** y **PDF** incluyen **todas las páginas** del filtro, no solo la que estás viendo.

Las consultas se calculan en el momento con los datos actuales.

---

## 9. Seguridad: recomendaciones

- No compartas tu contraseña. Kadree Bank **nunca** te la pedirá por teléfono, correo o mensaje.
- Evita contraseñas fáciles: fechas de cumpleaños, el final de tu documento o tu celular.
- Pulsa **Salir** al terminar, sobre todo en equipos compartidos, y no marques «Recordar mi usuario» en ellos.
- Verifica que la dirección del sitio sea la correcta antes de escribir tus datos.

---

## 10. Preguntas frecuentes

**¿Puedo tener más de una cuenta?**
Sí; las cuentas adicionales las abre el banco. El registro en línea abre una cuenta por cliente.

**¿Puedo cambiar la ciudad de origen?**
No. Es la ciudad donde se abrió la cuenta.

**Olvidé mi contraseña.**
La recuperación en línea todavía no está disponible. Comunícate con el banco.

**Mi usuario quedó bloqueado.**
Espera 15 minutos: se desbloquea automáticamente.

**¿Por qué el inicio me pide ingresar otra vez?**
Tu sesión expiró o la cerraste en otro lugar. Es una medida de seguridad.
