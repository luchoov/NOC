using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace NOC.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddContactLocality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "locality",
                table: "contacts",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "contacts",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "to_tsvector('spanish', coalesce(name, '') || ' ' || phone || ' ' || coalesce(email, '') || ' ' || coalesce(locality, ''))",
                stored: true,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector",
                oldNullable: true,
                oldComputedColumnSql: "to_tsvector('spanish', coalesce(name, '') || ' ' || phone || ' ' || coalesce(email, ''))",
                oldStored: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "locality",
                table: "contacts");

            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "contacts",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "to_tsvector('spanish', coalesce(name, '') || ' ' || phone || ' ' || coalesce(email, ''))",
                stored: true,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector",
                oldNullable: true,
                oldComputedColumnSql: "to_tsvector('spanish', coalesce(name, '') || ' ' || phone || ' ' || coalesce(email, '') || ' ' || coalesce(locality, ''))",
                oldStored: true);
        }
    }
}
