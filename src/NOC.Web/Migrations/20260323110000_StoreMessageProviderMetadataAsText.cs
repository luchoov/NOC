using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOC.Web.Migrations
{
    public partial class StoreMessageProviderMetadataAsText : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
ALTER TABLE messages
ALTER COLUMN provider_metadata TYPE text
USING provider_metadata::text;
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
ALTER TABLE messages
ALTER COLUMN provider_metadata TYPE jsonb
USING provider_metadata::jsonb;
""");
        }
    }
}
