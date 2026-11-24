namespace Sorteo360.Convocatorias.Core.Uploads;

/// <summary>
/// Cuánto admite una carga de destinatarios. Ninguno de los tres números sale de la memoria —una
/// hoja de 200k filas pica en 3 MB— así que cada uno protege otra cosa (spec <c>05</c> §7).
/// <para>
/// <b>No son configuración, y por eso no llevan anotaciones de validación.</b> Van con valores
/// por omisión y sin sección en el entorno, porque no son una palanca de despliegue: <see cref="MaxDataRows"/> es un marcador provisional con
/// dueño —el cliente todavía nos debe su número real de filas— y los otros dos son decisiones de
/// este servicio. Un entorno que pudiera subirlos escondería el trato que hay detrás.
/// </para>
/// </summary>
public sealed record SpreadsheetUploadLimits
{
    private const long BytesPerMegabyte = 1024 * 1024;

    /// <summary>
    /// Tamaño máximo del archivo, 5 MB. Protege poco: es generoso, es barato, y se comprueba
    /// antes de leer un solo byte. Queda holgadamente dentro del <c>client_max_body_size 32m</c>
    /// del nginx.
    /// </summary>
    public long MaxSizeBytes { get; init; } = 5L * BytesPerMegabyte;

    /// <summary>
    /// Filas de datos, 500. <b>Lo que protege es la escritura de la confirmación.</b>
    /// <c>frontend/nginx.conf</c> no declara <c>proxy_read_timeout</c>, así que manda el de
    /// fábrica, 60 s; a 50 ms por POST interno eso da unas 1.200 filas antes de que el navegador
    /// reciba un <b>504 con la escritura todavía corriendo</b>, que es el peor fallo disponible
    /// porque el trabajo sigue y quien subió no puede ver qué se completó. El tope y ese tiempo
    /// de espera son una sola decisión.
    /// <para>
    /// <b>Subirlo no es solo poner otro número</b>: es declarar un <c>proxy_read_timeout</c>
    /// explícito en la ruta de confirmación, en <c>nginx.conf</c> <b>y</b> en su gemelo sin
    /// comparar de <c>frontend/vite.config.ts</c>, o paginar la confirmación.
    /// </para>
    /// </summary>
    public int MaxDataRows { get; init; } = 500;

    /// <summary>
    /// Bytes descomprimidos de un xlsx, 100 MB. <b>Caza la bomba zip y nada más</b>: son 20x el
    /// tope de tamaño y quedan muy por encima de cualquier hoja real, porque lo que se busca es
    /// una expansión de 100x para arriba —la bomba medida iba de 497 KB a 500 MB— y no un
    /// archivo grande, que ya lo acotan el tamaño y las filas. Apretarlo daría falsos positivos
    /// sobre una hoja ancha legítima, con un error sobre el que nadie puede actuar.
    /// <para>
    /// <b>La aplicación se reparte</b>: el número es de esta política y la cuenta es de quien
    /// lee, porque el tamaño expandido no se sabe de antemano.
    /// </para>
    /// </summary>
    public long MaxDecompressedBytes { get; init; } = 100L * BytesPerMegabyte;

    /// <summary>
    /// Columnas de una hoja xlsx, 50. <b>Protege el tiempo del parser</b>: MiniExcel arma cada fila
    /// con todas las columnas hasta la última, y un libro de 1,6 KB con una celda en <c>XFD</c> lo
    /// ocupó más de 20 s (auditoría run-1). Una lista real trae cinco.
    /// </summary>
    public int MaxColumns { get; init; } = 50;

    /// <summary>El tope de tamaño en megabytes, para el texto de un rechazo.</summary>
    public long MaxSizeMegabytes => MaxSizeBytes / BytesPerMegabyte;

    /// <summary>El presupuesto de descompresión en megabytes, para el texto de un rechazo.</summary>
    public long MaxDecompressedMegabytes => MaxDecompressedBytes / BytesPerMegabyte;
}
