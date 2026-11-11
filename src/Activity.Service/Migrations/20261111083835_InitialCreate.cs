using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayLite.ActivityService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No tables yet (Owner B adds them). Creating the schema now means the database and
            // the "activity" schema exist from day one, so db/init-app-logins.sql can grant on it.
            migrationBuilder.EnsureSchema(name: "activity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS activity;");
        }
    }
}
