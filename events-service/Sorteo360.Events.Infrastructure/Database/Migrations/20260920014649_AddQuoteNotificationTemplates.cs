using Microsoft.EntityFrameworkCore.Migrations;
using Sorteo360.Events.Core.Templates;

#nullable disable

namespace Sorteo360.Events.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteNotificationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "template",
                schema: "events",
                columns: table => new
                {
                    template_key = table.Column<string>(type: "text", nullable: false),
                    language = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    body_html = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_template", x => new { x.template_key, x.language });
                    table.CheckConstraint("ck_template_language", "language IN ('de', 'en', 'es', 'fr', 'it', 'pt')");
                });

            // Se siembra con InsertData y nunca con HasData. HasData lo gestiona el modelo y vuelve
            // a afirmar sus valores en cada migración posterior, así que revertiría en silencio una
            // plantilla que alguien editara por los endpoints de administración que el cliente ya
            // pidió. Nada en este repositorio los tiene todavía, que es justo lo que deja el error
            // disponible para cometerse.
            migrationBuilder.InsertData(
                schema: "events",
                table: "template",
                columns: ["template_key", "language", "subject", "body_html"],
                values: SeedValues());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "template",
                schema: "events");
        }

        /// <summary>
        /// Las filas de <see cref="QuoteTemplateSeed"/> en el orden de columnas de la tabla. La
        /// copia vive con el dominio porque es dominio, y el test de archivo dorado la recorre
        /// desde ahí; esta migración solo la inserta.
        /// <para>
        /// <see cref="QuoteTemplateSeed"/> está congelada por eso mismo: editarla cambiaría lo que
        /// recibe una base nueva sin tocar lo que ya tiene una base existente.
        /// </para>
        /// </summary>
        private static object[,] SeedValues()
        {
            var rows = QuoteTemplateSeed.Rows;
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
