using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hongdal.Infrastructure.Persistence.AgriculturalFisheries.Migrations
{
    /// <inheritdoc />
    public partial class AddKamisPriceFrequency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_agri_kamis_price_observations_SurveyDate_ProductClassCode_Ca~",
                table: "agri_kamis_price_observations");

            migrationBuilder.AddColumn<string>(
                name: "FrequencyCode",
                table: "agri_kamis_price_observations",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Daily")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_agri_kamis_price_observations_SurveyDate_FrequencyCode_Produ~",
                table: "agri_kamis_price_observations",
                columns: new[] { "SurveyDate", "FrequencyCode", "ProductClassCode", "CategoryCode", "ItemCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_agri_kamis_price_observations_SurveyDate_FrequencyCode_Produ~",
                table: "agri_kamis_price_observations");

            migrationBuilder.DropColumn(
                name: "FrequencyCode",
                table: "agri_kamis_price_observations");

            migrationBuilder.CreateIndex(
                name: "IX_agri_kamis_price_observations_SurveyDate_ProductClassCode_Ca~",
                table: "agri_kamis_price_observations",
                columns: new[] { "SurveyDate", "ProductClassCode", "CategoryCode", "ItemCode" });
        }
    }
}
