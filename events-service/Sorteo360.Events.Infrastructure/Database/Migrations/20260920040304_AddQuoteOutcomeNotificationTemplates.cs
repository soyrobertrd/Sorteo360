using Microsoft.EntityFrameworkCore.Migrations;
using Sorteo360.Events.Core.Templates;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteOutcomeNotificationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Solo datos: la tabla la creó AddQuoteNotificationTemplates. Se siembra con InsertData
            // y nunca con HasData, por lo mismo que aquella: HasData lo gestiona el modelo y vuelve
            // a afirmar sus valores en cada migración posterior, así que revertiría en silencio una
            // plantilla que alguien editara por los endpoints de administración que el cliente ya
            // pidió.
            migrationBuilder.InsertData(
                schema: "events",
                table: "template",
                columns: ["template_key", "language", "subject", "body_html"],
                values: SeedValues());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Se borran solo estas dos filas y no la tabla: la tabla es de la migración anterior, y
            // tirarla acá se llevaría por delante la plantilla del sometimiento.
            foreach (var row in QuoteOutcomeTemplateSeed.Rows)
            {
                migrationBuilder.DeleteData(
                    schema: "events",
                    table: "template",
                    keyColumns: ["template_key", "language"],
                    keyValues: [row.TemplateKey, row.Language]);
            }
        }

        /// <summary>
        /// Las filas de <see cref="QuoteOutcomeTemplateSeed"/> en el orden de columnas de la tabla.
        /// La copia vive con el dominio porque es dominio, y el test de archivo dorado la recorre
        /// desde ahí; esta migración solo la inserta.
        /// <para>
        /// <see cref="QuoteOutcomeTemplateSeed"/> queda congelada en cuanto esto se despliegue:
        /// editarla cambiaría lo que recibe una base nueva sin tocar lo que ya tiene una existente.
        /// </para>
        /// </summary>
        private static object[,] SeedValues()
        {
            var rows = QuoteOutcomeTemplateSeed.Rows;
            var values = new object[rows.Count, 4];

            for (var i = 0; i < rows.Count; i++)
            {
                values[i, 0] = rows[i].TemplateKey;
                values[i, 1] = rows[i].Language;
                values[i, 2] = rows[i].Subject;
                values[i, 3] = rows[i].BodyHtml;
            }

            return values;
        }
    }
}
