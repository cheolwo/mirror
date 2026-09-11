using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ssalddel.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodDeliveryCompletedWorldProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "음식배달완료_WorldSnapshot",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    source_outbox_id = table.Column<long>(type: "bigint", nullable: false),
                    snapshot_stable_id = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    area_stable_id = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    lifecycle_revision = table.Column<long>(type: "bigint", nullable: false),
                    outcome_code = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    completed_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    published_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    orderer_actor_stable_id = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    restaurant_actor_stable_id = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    driver_actor_stable_id = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    milestones_json = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_음식배달완료_WorldSnapshot", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_음식배달완료_WorldSnapshot_area_stable_id_expires_at_utc_published~",
                table: "음식배달완료_WorldSnapshot",
                columns: new[] { "area_stable_id", "expires_at_utc", "published_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_음식배달완료_WorldSnapshot_snapshot_stable_id",
                table: "음식배달완료_WorldSnapshot",
                column: "snapshot_stable_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_음식배달완료_WorldSnapshot_source_outbox_id",
                table: "음식배달완료_WorldSnapshot",
                column: "source_outbox_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "음식배달완료_WorldSnapshot");
        }
    }
}
