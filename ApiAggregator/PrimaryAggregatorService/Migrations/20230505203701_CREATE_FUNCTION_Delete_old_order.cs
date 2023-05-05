using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrimaryAggregatorService.Migrations
{
    /// <inheritdoc />
    public partial class CREATE_FUNCTION_Delete_old_order : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            CREATE OR REPLACE FUNCTION deleteOrdersWithExcessiveDate() RETURNS void AS $$
            BEGIN
                DELETE FROM ""Orders""
                WHERE ""PackageDate"" > NOW() - INTERVAL '13 hours';
            END;
            $$ LANGUAGE plpgsql;
        ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP FUNCTION ""deleteOrdersWithExcessiveDate""");
        }
    }
}
