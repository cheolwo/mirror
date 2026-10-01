using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 홍달.Data;

#nullable disable

namespace Hongdal.Migrations;

[DbContext(typeof(HongdalContext))]
[Migration("20260718123000_AddYouTubeKnowledgeReflectionChannels")]
public sealed class AddYouTubeKnowledgeReflectionChannels : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_knowledge_reflection_channel",
            table: "youtube_watched_channels",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "knowledge_reflection_category_codes",
            table: "youtube_watched_channels",
            type: "varchar(300)",
            maxLength: 300,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "perspective_label",
            table: "youtube_watched_channels",
            type: "varchar(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "official_source_url",
            table: "youtube_watched_channels",
            type: "varchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "source_verified_at_utc",
            table: "youtube_watched_channels",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "is_prajna_publication_allowed",
            table: "youtube_watched_channels",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_youtube_watched_channels_knowledge_prajna_active",
            table: "youtube_watched_channels",
            columns: ["is_knowledge_reflection_channel", "is_prajna_publication_allowed", "is_active"]);

        migrationBuilder.Sql(
            """
            UPDATE youtube_watched_channels
            SET is_knowledge_reflection_channel = 1,
                knowledge_reflection_category_codes = 'philosophy,ethics,self-development',
                perspective_label = '홍익·양심 공부',
                official_source_url = 'https://www.youtube.com/channel/UCI8HW08rOSlvweOjJ9Gp2Ng',
                source_verified_at_utc = '2026-07-18 00:00:00'
            WHERE channel_id = 'UCI8HW08rOSlvweOjJ9Gp2Ng';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_youtube_watched_channels_knowledge_prajna_active",
            table: "youtube_watched_channels");

        migrationBuilder.DropColumn(name: "is_knowledge_reflection_channel", table: "youtube_watched_channels");
        migrationBuilder.DropColumn(name: "knowledge_reflection_category_codes", table: "youtube_watched_channels");
        migrationBuilder.DropColumn(name: "perspective_label", table: "youtube_watched_channels");
        migrationBuilder.DropColumn(name: "official_source_url", table: "youtube_watched_channels");
        migrationBuilder.DropColumn(name: "source_verified_at_utc", table: "youtube_watched_channels");
        migrationBuilder.DropColumn(name: "is_prajna_publication_allowed", table: "youtube_watched_channels");
    }
}
