using Microsoft.EntityFrameworkCore.Migrations;
using Sorteo360.Events.Core.Templates;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPrizeWonNotificationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Solo datos, con InsertData y nunca con HasData, por lo mismo que las plantillas de la
            // cotización: HasData lo gestiona el modelo y revertiría en silencio una plantilla
            // editada por los endpoints de administración que el cliente ya pidió.
            migrationBuilder.InsertData(
                schema: "events",
                table: "template",
                columns: ["template_key", "language", "subject", "body_html"],
                values: SeedValues());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Se borran solo estas dos filas: la tabla es de una migración anterior.
            foreach (var row in PrizeWonTemplateSeed.Rows)
            {
                migrationBuilder.DeleteData(
                    schema: "events",
                    table: "template",
                    keyColumns: ["template_key", "language"],
                    keyValues: [row.TemplateKey, row.Language]);
            }
        }

        /// <summary>
        /// Las filas de <see cref="PrizeWonTemplateSeed"/> en el orden de columnas de la tabla.
        /// <see cref="PrizeWonTemplateSeed"/> queda congelada en cuanto esto se despliegue.
        /// </summary>
        private static object[,] SeedValues()
        {
            var rows = PrizeWonTemplateSeed.Rows;
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
