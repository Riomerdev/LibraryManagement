using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeatureGate.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureFlagAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at_utc",
                table: "feature_flags",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at_utc",
                table: "feature_flags",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE feature_flags
                SET created_at_utc = CURRENT_TIMESTAMP,
                    updated_at_utc = CURRENT_TIMESTAMP;
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at_utc",
                table: "feature_flags",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at_utc",
                table: "feature_flags",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "feature_flag_audits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_flag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    flag_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    actor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    before_json = table.Column<string>(type: "jsonb", nullable: true),
                    after_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_flag_audits", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_feature_flag_audits_flag_occurred",
                table: "feature_flag_audits",
                columns: new[] { "feature_flag_id", "occurred_at_utc" });

            migrationBuilder.Sql("""
                INSERT INTO feature_flag_audits
                    (id, feature_flag_id, flag_key, action, actor, occurred_at_utc, before_json, after_json)
                SELECT
                    gen_random_uuid(),
                    id,
                    flag_key,
                    'imported',
                    'system:migration',
                    created_at_utc,
                    NULL,
                    jsonb_build_object(
                        'key', flag_key,
                        'description', description,
                        'enabled', enabled,
                        'rolloutPercentage', rollout_percentage,
                        'updatedAtUtc', updated_at_utc)
                FROM feature_flags;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feature_flag_audits");

            migrationBuilder.DropColumn(
                name: "created_at_utc",
                table: "feature_flags");

            migrationBuilder.DropColumn(
                name: "updated_at_utc",
                table: "feature_flags");
        }
    }
}
