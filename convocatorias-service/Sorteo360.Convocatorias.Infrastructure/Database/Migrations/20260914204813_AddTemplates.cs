using Microsoft.EntityFrameworkCore.Migrations;
using Sorteo360.Convocatorias.Core.Templates;

#nullable disable

namespace Sorteo360.Convocatorias.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "convocatorias");

            migrationBuilder.CreateTable(
                name: "template",
                schema: "convocatorias",
                columns: table => new
                {
                    template_key = table.Column<string>(type: "text", nullable: false),
                    language = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    body_html = table.Column<string>(type: "text", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    is_campaign_selectable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_template", x => new { x.template_key, x.language });
                    table.CheckConstraint("ck_template_language", "language IN ('de', 'en', 'es', 'fr', 'it', 'pt')");
                    table.CheckConstraint("ck_template_purpose", "purpose IN ('campaign', 'transactional')");
                    table.CheckConstraint("ck_template_selectable_is_campaign", "is_campaign_selectable = false OR purpose = 'campaign'");
                });

            migrationBuilder.CreateIndex(
                name: "ix_template_is_campaign_selectable",
                schema: "convocatorias",
                table: "template",
                column: "is_campaign_selectable");

            // Siembra con InsertData y nunca con HasData. HasData lo gestiona el modelo y vuelve
            // a afirmar sus valores en cada migración posterior, así que revertiría en silencio
            // una plantilla que alguien editara por los endpoints de administración que el
            // cliente ya pidió. Nada en este repositorio lo hace todavía, que es justo lo que
            // deja el error disponible para cometerse.
            migrationBuilder.InsertData(
                schema: "convocatorias",
                table: "template",
                columns: ["template_key", "language", "subject", "body_html", "purpose", "is_campaign_selectable"],
                values: SeedValues());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "template",
                schema: "convocatorias");
        }

        /// <summary>
        /// Las filas de <see cref="TemplateSeed"/> en el orden de columnas de la tabla. La copia
        /// vive con el dominio porque es dominio, y los tests de marcadores derivados la recorren
        /// desde ahí; esta migración solo la inserta.
        /// <para>
        /// <see cref="TemplateSeed"/> está congelada por eso mismo: editarla cambiaría lo que
        /// recibe una base nueva sin tocar una base existente. Un arreglo de copia se despacha
        /// como una migración nueva con sus propios valores.
        /// </para>
        /// </summary>
        private static object[,] SeedValues()
        {
            var rows = TemplateSeed.Rows;
            var values = new object[rows.Count, 6];

            for (var row = 0; row < rows.Count; row++)
            {
                values[row, 0] = rows[row].TemplateKey;
                values[row, 1] = rows[row].Language;
                values[row, 2] = rows[row].Subject;
                values[row, 3] = rows[row].BodyHtml;
                values[row, 4] = rows[row].Purpose;
                values[row, 5] = rows[row].IsCampaignSelectable;
            }

            return values;
        }
    }
}
