using PiRadar.Core.Domain;

namespace PiRadar.Core.Help;

/// <summary>Texto en los dos idiomas. Los nombres de botones y menús van tal como se ven en la app.</summary>
public sealed record HelpText(string Es, string En)
{
    public string In(AppLanguage language) => language == AppLanguage.English ? En : Es;
}

/// <summary>Una pregunta con su respuesta en pasos cortos y, opcionalmente, un consejo.</summary>
public sealed record HelpArticle(string Id, HelpText Question, IReadOnlyList<HelpText> Steps, HelpText? Tip = null);

public sealed record HelpSection(string Id, HelpText Title, IReadOnlyList<HelpArticle> Articles);

/// <summary>
/// Contenido de la ayuda. Única fuente para la pantalla Ayuda de la app y para las páginas web
/// (<c>tools/help/build-help.cs</c> → <c>store/help/</c>). No nombrar otras plataformas ni tiendas: la web de ayuda se enlaza desde la ficha.
/// </summary>
public static class HelpContent
{
    public const string ContactEmail = "belvictodeveloper@gmail.com";

    /// <summary>Busca un artículo por id (los de <see cref="HelpTopics"/>); <c>null</c> si no existe.</summary>
    public static HelpArticle? FindArticle(string id) =>
        Sections.SelectMany(s => s.Articles).FirstOrDefault(a => a.Id == id);

    public static IReadOnlyList<HelpSection> Sections { get; } =
    [
        Section("inicio-rapido", "Inicio rápido", "Quick start",
        [
            Article("primeros-pasos", "¿Cómo empiezo a usar piRadar?", "How do I start using piRadar?",
            [
                T("Toca **Registrar** en Inicio (o el botón verde **+**) y anota tu primer gasto o ingreso.",
                  "Tap **Add** on Home (or the green **+** button) and enter your first expense or income."),
                T("O toca **Escanear** y toma una foto de un recibo: piRadar propone el total, la fecha y el comercio, y tú confirmas.",
                  "Or tap **Scan** and take a photo of a receipt: piRadar suggests the total, date and merchant, and you confirm."),
                T("Mira cómo va tu mes en **Inicio** y en **Análisis**.",
                  "See how your month is going on **Home** and **Analytics**."),
                T("En **Más > Ajustes** elige el idioma, el tema y tu moneda base.",
                  "In **More > Settings** choose the language, the theme and your base currency."),
                T("En **Más > Respaldo** crea tu primer respaldo cifrado y guárdalo en un lugar seguro.",
                  "In **More > Backup** create your first encrypted backup and keep it somewhere safe."),
            ],
            T("No necesitas cuenta ni internet: todo se guarda cifrado en tu teléfono.",
              "You do not need an account or internet: everything is stored encrypted on your phone.")),
        ]),

        Section("registrar", "Registrar movimientos", "Adding transactions",
        [
            Article("registrar-movimiento", "¿Cómo registro un gasto o un ingreso?", "How do I add an expense or an income?",
            [
                T("Toca **Registrar** en Inicio, o el botón verde **+** de la barra inferior.",
                  "Tap **Add** on Home, or the green **+** button in the bottom bar."),
                T("Elige **Gasto** o **Ingreso**.", "Choose **Expense** or **Income**."),
                T("Escribe el **Monto** y elige la moneda (COP o USD).",
                  "Enter the **Amount** and choose the currency (COP or USD)."),
                T("Elige la **Categoría** y, si quieres, el **Medio de pago**, la **Fecha** y la **Hora**.",
                  "Choose the **Category** and, if you like, the **Payment method**, the **Date** and the **Time**."),
                T("Opcional: **Comercio**, **Descripción** y **Notas** para encontrar el movimiento después.",
                  "Optional: **Merchant**, **Description** and **Notes** to find the transaction later."),
                T("Toca **Guardar**.", "Tap **Save**."),
            ]),
            Article("editar-borrar", "¿Cómo edito o borro un movimiento?", "How do I edit or delete a transaction?",
            [
                T("Abre **Movimientos** (o los últimos movimientos de Inicio) y toca el movimiento.",
                  "Open **Transactions** (or the recent transactions on Home) and tap the transaction."),
                T("Cambia lo que necesites y toca **Guardar**.", "Change what you need and tap **Save**."),
                T("Para borrarlo, toca **Eliminar** y confirma. Si tenía foto de recibo, también se borra.",
                  "To remove it, tap **Delete** and confirm. If it had a receipt photo, the photo is deleted too."),
            ],
            T("Borrar no se puede deshacer. Si dudas, haz antes un respaldo.",
              "Deleting cannot be undone. If in doubt, make a backup first.")),
            Article("filtrar-movimientos", "¿Cómo busco o filtro mis movimientos?", "How do I filter my transactions?",
            [
                T("En **Movimientos**, usa las flechas del selector de periodo para ir al día, semana, mes o año que quieras.",
                  "In **Transactions**, use the arrows of the period selector to go to the day, week, month or year you want."),
                T("Usa los filtros **Todos**, **Gastos** e **Ingresos** para ver solo un tipo.",
                  "Use the **All**, **Expenses** and **Income** filters to see only one type."),
                T("Para saber si ya registraste un gasto, toca la lupa de **Movimientos** y escribe una palabra del comercio, la descripción o las notas, o un monto. Busca en todo tu historial, sin importar mayúsculas ni tildes.",
                  "To check whether you already recorded an expense, tap the magnifier in **Transactions** and type a word from the merchant, description or notes, or an amount. It searches your whole history, ignoring capitals and accents."),
                T("Elige una **Categoría** para ver solo esa (incluye sus subcategorías); puedes combinarla con el texto. Borra la búsqueda o toca la lupa otra vez para volver al periodo.",
                  "Choose a **Category** to see only that one (subcategories included); you can combine it with text. Clear the search or tap the magnifier again to go back to the period."),
            ]),
        ]),

        Section("escanear-recibos", "Escanear recibos", "Scanning receipts",
        [
            Article("escanear", "¿Cómo escaneo un recibo?", "How do I scan a receipt?",
            [
                T("Toca **Escanear** en Inicio.", "Tap **Scan** on Home."),
                T("Toca **Tomar foto** o **Elegir de la galería**.", "Tap **Take photo** or **Choose from gallery**."),
                T("Espera unos segundos: el recibo se lee en el teléfono, sin internet.",
                  "Wait a few seconds: the receipt is read on the phone, without internet."),
                T("Revisa el total, la fecha, el comercio y la categoría que propone piRadar.",
                  "Check the total, date, merchant and category that piRadar suggests."),
                T("Corrige lo que haga falta y toca **Guardar**: se guardan el movimiento y la foto del recibo.",
                  "Fix anything that is off and tap **Save**: the transaction and the receipt photo are saved."),
            ],
            T("piRadar solo propone: nada se guarda hasta que tú confirmes.",
              "piRadar only suggests: nothing is saved until you confirm.")),
            Article("total-incorrecto", "El total salió mal, ¿qué hago?", "The total is wrong, what do I do?",
            [
                T("Mira los botones **¿Otro total?** debajo del monto: toca el que sea correcto.",
                  "Look at the **Another total?** chips under the amount: tap the right one."),
                T("Si ninguno sirve, escribe el monto a mano.", "If none is right, type the amount by hand."),
                T("Toca **Ver texto leído** para ver exactamente lo que leyó el teléfono.",
                  "Tap **View text read** to see exactly what the phone read."),
                T("Los avisos sobre el total, la fecha o la categoría te dicen qué revisar antes de guardar.",
                  "The warnings about the total, the date or the category tell you what to check before saving."),
            ]),
            Article("mejor-foto", "¿Cómo tomo una foto que se lea bien?", "How do I take a photo that reads well?",
            [
                T("Pon el recibo completo en la foto, derecho y sobre una superficie plana.",
                  "Put the whole receipt in the photo, straight and on a flat surface."),
                T("Usa buena luz y evita sombras y reflejos.", "Use good light and avoid shadows and glare."),
                T("Si el papel está arrugado, alísalo antes.", "If the paper is wrinkled, smooth it out first."),
            ]),
            Article("permiso-camara", "No se abre la cámara o la galería", "The camera or gallery does not open",
            [
                T("Abre los ajustes del teléfono, busca piRadar y activa el permiso de la cámara y de las fotos.",
                  "Open the phone settings, find piRadar and turn on the camera and photos permission."),
                T("Mientras tanto puedes usar **Registrar a mano**.", "Meanwhile you can use **Enter manually**."),
            ]),
        ]),

        Section("categorias-medios", "Categorías y medios de pago", "Categories and payment methods",
        [
            Article("crear-categoria", "¿Cómo creo una categoría o una subcategoría?", "How do I create a category or a subcategory?",
            [
                T("Ve a **Más > Categorías** y elige **Gastos** o **Ingresos**.",
                  "Go to **More > Categories** and choose **Expenses** or **Income**."),
                T("Toca **+**, escribe el **Nombre** y elige el ícono y el color.",
                  "Tap **+**, enter the **Name** and choose the icon and color."),
                T("Para una subcategoría, en **Dentro de** elige la categoría principal.",
                  "For a subcategory, choose the main category in **Inside**."),
                T("Toca **Guardar**.", "Tap **Save**."),
            ]),
            Article("archivar", "¿Qué pasa si borro una categoría o un medio de pago?", "What happens if I delete a category or payment method?",
            [
                T("Si no tiene movimientos, se elimina.", "If it has no transactions, it is deleted."),
                T("Si tiene movimientos o subcategorías, se **archiva**: ya no aparece al registrar movimientos nuevos, pero los viejos la conservan.",
                  "If it has transactions or subcategories, it is **archived**: it no longer appears when adding new transactions, but old ones keep it."),
                T("Activa **Mostrar archivadas** para verla y **Restaurar** para volver a usarla.",
                  "Turn on **Show archived** to see it and **Restore** to use it again."),
            ]),
            Article("medios-de-pago", "¿Cómo agrego un medio de pago?", "How do I add a payment method?",
            [
                T("Ve a **Más > Medios de pago** y toca **+**.", "Go to **More > Payment methods** and tap **+**."),
                T("Escribe el **Nombre**, elige el **Tipo** y el ícono.",
                  "Enter the **Name**, choose the **Type** and the icon."),
                T("Si quieres distinguir dos tarjetas, escribe los **Últimos 4 dígitos**: el número completo nunca se guarda.",
                  "To tell two cards apart, enter the **Last 4 digits**: the full number is never stored."),
            ]),
        ]),

        Section("analisis", "Análisis y saldo", "Analytics and balance",
        [
            Article("ver-analisis", "¿Cómo veo en qué se va mi dinero?", "How do I see where my money goes?",
            [
                T("Abre **Análisis**.", "Open **Analytics**."),
                T("Elige el periodo: **Día**, **Semana**, **Mes** o **Año**, y muévete con las flechas.",
                  "Choose the period: **Day**, **Week**, **Month** or **Year**, and move with the arrows."),
                T("Elige la moneda y alterna entre **Gastos** e **Ingresos**.",
                  "Choose the currency and switch between **Expenses** and **Income**."),
                T("Mira las barras en el tiempo, **Por categoría**, **Por medio de pago** y el gasto promedio por día.",
                  "See the bars over time, **By category**, **By payment method** and the average spending per day."),
            ]),
            Article("saldo-inicial", "¿Qué es el saldo inicial?", "What is the opening balance?",
            [
                T("Es lo que te quedó de todos los periodos anteriores (ingresos menos gastos), por moneda.",
                  "It is what was left from all previous periods (income minus expenses), per currency."),
                T("Por eso un mes nuevo no empieza en cero: arranca con el saldo del anterior.",
                  "That is why a new month does not start at zero: it starts with the previous balance."),
                T("Se calcula solo: si corriges o borras un movimiento viejo, se actualiza. No cuenta como ingreso ni como gasto.",
                  "It is calculated automatically: if you fix or delete an old transaction, it updates. It does not count as income or expense."),
            ]),
            Article("todo-en-moneda-base", "¿Cómo veo todo en una sola moneda?", "How do I see everything in one currency?",
            [
                T("En **Análisis**, si tienes movimientos en dos monedas, elige **Todo en** y tu moneda base.",
                  "In **Analytics**, if you have transactions in two currencies, choose **All in** and your base currency."),
                T("Para convertir, piRadar usa las tasas que tú ingresaste en **Más > Tasas de cambio**.",
                  "To convert, piRadar uses the rates you entered in **More > Exchange rates**."),
                T("Los movimientos sin tasa para su fecha quedan fuera del total y se avisa; toca **Agregar tasa** para completarla.",
                  "Transactions with no rate for their date are left out of the total with a warning; tap **Add rate** to fill it in."),
            ]),
            Article("tasas", "¿Cómo ingreso una tasa de cambio?", "How do I enter an exchange rate?",
            [
                T("Ve a **Más > Tasas de cambio** y toca **+**.", "Go to **More > Exchange rates** and tap **+**."),
                T("Elige las monedas **De** y **A**, escribe la tasa y la fecha en **Aplica desde**.",
                  "Choose the **From** and **To** currencies, enter the rate and the date in **Applies from**."),
                T("Toca **Guardar**. Cada movimiento usa la última tasa con fecha igual o anterior a la suya.",
                  "Tap **Save**. Each transaction uses the latest rate dated on or before it."),
            ],
            T("Las tasas solo se usan en la vista consolidada; los totales por moneda no cambian.",
              "Rates are only used in the combined view; the totals per currency do not change.")),
        ]),

        Section("respaldo", "Respaldo", "Backup",
        [
            Article("crear-respaldo", "¿Cómo hago un respaldo?", "How do I make a backup?",
            [
                T("Ve a **Más > Respaldo** y toca **Crear respaldo**.", "Go to **More > Backup** and tap **Create backup**."),
                T("Elige una contraseña y confírmala.", "Choose a password and confirm it."),
                T("Guarda el archivo `.piradar` donde quieras (correo, nube, archivos) con el menú de compartir.",
                  "Save the `.piradar` file wherever you like (email, cloud, files) using the share menu."),
            ],
            T("La contraseña no se guarda en ningún sitio: si la olvidas, el respaldo no se puede abrir. Anótala en un lugar seguro.",
              "The password is not stored anywhere: if you forget it, the backup cannot be opened. Write it down somewhere safe.")),
            Article("restaurar-respaldo", "¿Cómo restauro un respaldo o paso mis datos a otro teléfono?", "How do I restore a backup or move my data to another phone?",
            [
                T("Instala piRadar en el teléfono nuevo y pasa el archivo `.piradar` a él.",
                  "Install piRadar on the new phone and move the `.piradar` file to it."),
                T("Ve a **Más > Respaldo** y toca **Restaurar un respaldo**.",
                  "Go to **More > Backup** and tap **Restore a backup**."),
                T("Elige el archivo y escribe la contraseña con la que lo creaste.",
                  "Choose the file and enter the password you used to create it."),
                T("Revisa el resumen y confirma con **Reemplazar**. Esto sustituye todos los datos que tengas en ese teléfono.",
                  "Review the summary and confirm with **Replace**. This replaces all the data on that phone."),
            ]),
            Article("recordatorio-respaldo", "¿Por qué me aparece un recordatorio de respaldo?", "Why do I see a backup reminder?",
            [
                T("Inicio te avisa cuando nunca hiciste un respaldo o pasaron más de 30 días desde el último.",
                  "Home reminds you when you have never made a backup or more than 30 days have passed since the last one."),
                T("Toca **Hacer respaldo** para crearlo en el momento.", "Tap **Back up now** to create it right away."),
            ]),
        ]),

        Section("ajustes", "Ajustes", "Settings",
        [
            Article("idioma-tema", "¿Cómo cambio el idioma, el tema o la moneda base?", "How do I change the language, theme or base currency?",
            [
                T("Ve a **Más > Ajustes**.", "Go to **More > Settings**."),
                T("En **Idioma** elige español, inglés o **Igual que el sistema**.",
                  "In **Language** choose Spanish, English or **Same as system**."),
                T("En **Tema** elige **Claro**, **Oscuro** o el del sistema.",
                  "In **Theme** choose **Light**, **Dark** or the system one."),
                T("En **Moneda base** elige la moneda en la que quieres ver los totales consolidados.",
                  "In **Base currency** choose the currency in which you want to see combined totals."),
                T("En **Título de inicio** puedes cambiar el título de la pantalla de Inicio, y **Restablecer** lo devuelve al original.",
                  "In **Home title** you can change the title of the Home screen, and **Reset** puts the original back."),
            ]),
        ]),

        Section("privacidad", "Privacidad y datos", "Privacy and data",
        [
            Article("donde-estan-mis-datos", "¿Dónde se guardan mis datos?", "Where is my data stored?",
            [
                T("Solo en tu teléfono, en una base de datos cifrada. No hay cuenta, nube, anuncios ni rastreo.",
                  "Only on your phone, in an encrypted database. There is no account, cloud, ads or tracking."),
                T("La app no usa internet: el texto de los recibos se lee en el propio teléfono.",
                  "The app does not use the internet: receipt text is read on the phone itself."),
                T("Por eso no podemos recuperar tus datos si pierdes el teléfono: haz respaldos.",
                  "That is why we cannot recover your data if you lose your phone: make backups."),
            ]),
        ]),

        Section("problemas", "Problemas frecuentes", "Troubleshooting",
        [
            Article("olvide-contrasena", "Olvidé la contraseña de mi respaldo", "I forgot my backup password",
            [
                T("No se puede recuperar: el respaldo está cifrado con esa contraseña y piRadar no la guarda.",
                  "It cannot be recovered: the backup is encrypted with that password and piRadar does not keep it."),
                T("Prueba las contraseñas que sueles usar, con y sin tildes y mayúsculas.",
                  "Try the passwords you usually use, with and without accents and capital letters."),
                T("Si todavía tienes la app con tus datos, crea un respaldo nuevo con una contraseña que anotes.",
                  "If you still have the app with your data, create a new backup with a password you write down."),
            ]),
            Article("pantalla-error-datos", "Al abrir la app dice que no puede leer mis datos", "The app says it cannot read my data when I open it",
            [
                T("Esto pasa si los datos cifrados ya no se pueden abrir con la clave del teléfono, por ejemplo tras restaurar el teléfono desde una copia.",
                  "This happens when the encrypted data can no longer be opened with the phone's key, for example after restoring the phone from a copy."),
                T("Si tienes un respaldo `.piradar`, toca **Empezar de cero…** en esa pantalla y luego usa **Restaurar un respaldo**.",
                  "If you have a `.piradar` backup, tap **Start over…** on that screen and then use **Restore a backup**."),
                T("**Borrar y empezar de cero** elimina lo que haya en la app: la pantalla te pide confirmar antes.",
                  "**Erase and start over** removes what is in the app: the screen asks you to confirm first."),
            ]),
            Article("contacto", "Mi problema no está aquí", "My problem is not here",
            [
                T($"Escríbenos a {ContactEmail} contando qué pantalla estabas viendo y qué esperabas que pasara.",
                  $"Write to {ContactEmail} telling us which screen you were on and what you expected to happen."),
                T("No envíes tu contraseña ni tus archivos de respaldo.",
                  "Do not send your password or your backup files."),
            ]),
        ]),
    ];

    private static HelpText T(string es, string en) => new(es, en);

    private static HelpSection Section(string id, string es, string en, HelpArticle[] articles) =>
        new(id, T(es, en), articles);

    private static HelpArticle Article(string id, string es, string en, HelpText[] steps, HelpText? tip = null) =>
        new(id, T(es, en), steps, tip);
}
