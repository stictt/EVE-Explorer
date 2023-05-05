using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PrimaryAggregatorService.Migrations
{
    /// <inheritdoc />
    public partial class add_dataBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Duration = table.Column<short>(type: "smallint", nullable: false),
                    IsBuyOrder = table.Column<bool>(type: "boolean", nullable: false),
                    Issued = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LocationId = table.Column<long>(type: "bigint", nullable: false),
                    MinVolume = table.Column<int>(type: "integer", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    Price = table.Column<double>(type: "double precision", nullable: false),
                    Range = table.Column<string>(type: "text", nullable: false),
                    SystemId = table.Column<int>(type: "integer", nullable: false),
                    TypeId = table.Column<int>(type: "integer", nullable: false),
                    VolumeRemain = table.Column<long>(type: "bigint", nullable: false),
                    VolumeTotal = table.Column<long>(type: "bigint", nullable: false),
                    PackageDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TypeId_PackageDate",
                table: "Orders",
                columns: new[] { "TypeId", "PackageDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Orders");
        }
    }
}
