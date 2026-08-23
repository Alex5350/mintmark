using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace Mintmark.Infrastructure.Persistence.Migrations;
/// <inheritdoc />
public partial class Initial : Migration
{

    private static readonly string[] Columns0 = ["gin_trgm_ops"];
    private static readonly string[] Columns6 = ["vector_cosine_ops"];
    private static readonly string[] Columns3 = ["series_id", "year"];
    private static readonly string[] Columns5 = ["user_id", "is_deleted"];
    private static readonly string[] Columns4 = ["user_id", "idempotency_key"];
    private static readonly string[] Columns2 = ["metal", "currency", "source_timestamp_utc"];
    private static readonly string[] Columns1 = ["holding_id", "computed_at_utc"];
    private static readonly bool[] Descending7 = [false, false, true];


    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
            .Annotation("Npgsql:PostgresExtension:vector", ",,");

        migrationBuilder.CreateTable(
            name: "asp_net_roles",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                concurrency_stamp = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_asp_net_roles", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "asp_net_users",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                display_name = table.Column<string>(type: "text", nullable: true),
                user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                password_hash = table.Column<string>(type: "text", nullable: true),
                security_stamp = table.Column<string>(type: "text", nullable: true),
                concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                phone_number = table.Column<string>(type: "text", nullable: true),
                phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                access_failed_count = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_asp_net_users", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "idempotency_records",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                user_id = table.Column<long>(type: "bigint", nullable: false),
                idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                endpoint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                response_body = table.Column<string>(type: "text", nullable: false),
                status_code = table.Column<int>(type: "integer", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_idempotency_records", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "identification_runs",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                user_id = table.Column<long>(type: "bigint", nullable: false),
                obverse_image_id = table.Column<long>(type: "bigint", nullable: true),
                reverse_image_id = table.Column<long>(type: "bigint", nullable: true),
                edge_image_id = table.Column<long>(type: "bigint", nullable: true),
                obverse_perceptual_hash = table.Column<long>(type: "bigint", nullable: true),
                model_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                prompt_template_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                raw_response = table.Column<string>(type: "text", nullable: false),
                field_confidences = table.Column<string>(type: "jsonb", nullable: false),
                candidates = table.Column<string>(type: "jsonb", nullable: false),
                confirmed_coin_type_id = table.Column<long>(type: "bigint", nullable: true),
                confirmed_by = table.Column<string>(type: "text", nullable: true),
                confirmed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_identification_runs", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "mints",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                mint_marks = table.Column<string[]>(type: "text[]", nullable: false),
                founded_year = table.Column<int>(type: "integer", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                notes = table.Column<string>(type: "text", nullable: true),
                logo_asset_key = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_mints", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "refresh_tokens",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                user_id = table.Column<long>(type: "bigint", nullable: false),
                family_id = table.Column<Guid>(type: "uuid", nullable: false),
                token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                consumed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                device_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_refresh_tokens", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "series_demand_tiers",
            columns: table => new
            {
                series_id = table.Column<long>(type: "bigint", nullable: false),
                tier = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_series_demand_tiers", x => x.series_id);
            });

        migrationBuilder.CreateTable(
            name: "spot_price_daily",
            columns: table => new
            {
                metal = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                date = table.Column<DateOnly>(type: "date", nullable: false),
                currency = table.Column<string>(type: "char(3)", nullable: false),
                provider_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ingested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                close_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                close_currency = table.Column<string>(type: "char(3)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_spot_price_daily", x => new { x.metal, x.currency, x.date });
            });

        migrationBuilder.CreateTable(
            name: "spot_prices",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                metal = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                currency = table.Column<string>(type: "char(3)", nullable: false),
                provider_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                source_timestamp_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ingested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ask_per_troy_ounce_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                ask_per_troy_ounce_currency = table.Column<string>(type: "char(3)", nullable: false),
                bid_per_troy_ounce_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                bid_per_troy_ounce_currency = table.Column<string>(type: "char(3)", nullable: false),
                price_per_troy_ounce_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                price_per_troy_ounce_currency = table.Column<string>(type: "char(3)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_spot_prices", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "asp_net_role_claims",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                role_id = table.Column<long>(type: "bigint", nullable: false),
                claim_type = table.Column<string>(type: "text", nullable: true),
                claim_value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_asp_net_role_claims", x => x.id);
                table.ForeignKey(
                    name: "fk_asp_net_role_claims_asp_net_roles_role_id",
                    column: x => x.role_id,
                    principalTable: "asp_net_roles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "asp_net_user_claims",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                user_id = table.Column<long>(type: "bigint", nullable: false),
                claim_type = table.Column<string>(type: "text", nullable: true),
                claim_value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_asp_net_user_claims", x => x.id);
                table.ForeignKey(
                    name: "fk_asp_net_user_claims_asp_net_users_user_id",
                    column: x => x.user_id,
                    principalTable: "asp_net_users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "asp_net_user_logins",
            columns: table => new
            {
                login_provider = table.Column<string>(type: "text", nullable: false),
                provider_key = table.Column<string>(type: "text", nullable: false),
                provider_display_name = table.Column<string>(type: "text", nullable: true),
                user_id = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_asp_net_user_logins", x => new { x.login_provider, x.provider_key });
                table.ForeignKey(
                    name: "fk_asp_net_user_logins_asp_net_users_user_id",
                    column: x => x.user_id,
                    principalTable: "asp_net_users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "asp_net_user_roles",
            columns: table => new
            {
                user_id = table.Column<long>(type: "bigint", nullable: false),
                role_id = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_asp_net_user_roles", x => new { x.user_id, x.role_id });
                table.ForeignKey(
                    name: "fk_asp_net_user_roles_asp_net_roles_role_id",
                    column: x => x.role_id,
                    principalTable: "asp_net_roles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_asp_net_user_roles_asp_net_users_user_id",
                    column: x => x.user_id,
                    principalTable: "asp_net_users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "asp_net_user_tokens",
            columns: table => new
            {
                user_id = table.Column<long>(type: "bigint", nullable: false),
                login_provider = table.Column<string>(type: "text", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_asp_net_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                table.ForeignKey(
                    name: "fk_asp_net_user_tokens_asp_net_users_user_id",
                    column: x => x.user_id,
                    principalTable: "asp_net_users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "series",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                mint_id = table.Column<long>(type: "bigint", nullable: false),
                metal = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                start_year = table.Column<int>(type: "integer", nullable: true),
                end_year = table.Column<int>(type: "integer", nullable: true),
                notes = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_series", x => x.id);
                table.ForeignKey(
                    name: "fk_series_mints_mint_id",
                    column: x => x.mint_id,
                    principalTable: "mints",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "coin_types",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                series_id = table.Column<long>(type: "bigint", nullable: false),
                mint_id = table.Column<long>(type: "bigint", nullable: false),
                name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                year = table.Column<int>(type: "integer", nullable: false),
                finish = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                finish_attributes = table.Column<int>(type: "integer", nullable: false),
                fineness = table.Column<decimal>(type: "numeric(6,4)", nullable: false),
                gross_weight_grams = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                actual_metal_weight_troy_oz = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                diameter_millimeters = table.Column<decimal>(type: "numeric(8,3)", nullable: true),
                thickness_millimeters = table.Column<decimal>(type: "numeric(8,3)", nullable: true),
                edge = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                mintage = table.Column<long>(type: "bigint", nullable: true),
                source_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                km_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                red_book_reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                obverse_image_key = table.Column<string>(type: "text", nullable: true),
                reverse_image_key = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_coin_types", x => x.id);
                table.ForeignKey(
                    name: "fk_coin_types_mints_mint_id",
                    column: x => x.mint_id,
                    principalTable: "mints",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_coin_types_series_series_id",
                    column: x => x.series_id,
                    principalTable: "series",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "holdings",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                user_id = table.Column<long>(type: "bigint", nullable: false),
                coin_type_id = table.Column<long>(type: "bigint", nullable: true),
                form = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                quantity = table.Column<int>(type: "integer", nullable: false),
                purchased_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                dealer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                storage_location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                packaging_state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                purchase_price_per_unit_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                purchase_price_per_unit_currency = table.Column<string>(type: "char(3)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_holdings", x => x.id);
                table.ForeignKey(
                    name: "fk_holdings_coin_types_coin_type_id",
                    column: x => x.coin_type_id,
                    principalTable: "coin_types",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "reference_images",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                coin_type_id = table.Column<long>(type: "bigint", nullable: false),
                side = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                storage_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                perceptual_hash = table.Column<long>(type: "bigint", nullable: false),
                embedding = table.Column<Vector>(type: "vector(768)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_reference_images", x => x.id);
                table.ForeignKey(
                    name: "fk_reference_images_coin_types_coin_type_id",
                    column: x => x.coin_type_id,
                    principalTable: "coin_types",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "coin_images",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                holding_id = table.Column<long>(type: "bigint", nullable: false),
                side = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                storage_key = table.Column<string>(type: "text", nullable: false),
                perceptual_hash = table.Column<long>(type: "bigint", nullable: false),
                captured_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                content_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_coin_images", x => x.id);
                table.ForeignKey(
                    name: "fk_coin_images_holdings_holding_id",
                    column: x => x.holding_id,
                    principalTable: "holdings",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "gradings",
            columns: table => new
            {
                holding_id = table.Column<long>(type: "bigint", nullable: false),
                service = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                numeric_grade = table.Column<int>(type: "integer", nullable: true),
                designations = table.Column<int>(type: "integer", nullable: false),
                certification_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                label_pedigree = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                verification_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_gradings", x => x.holding_id);
                table.ForeignKey(
                    name: "fk_gradings_holdings_holding_id",
                    column: x => x.holding_id,
                    principalTable: "holdings",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "holding_revisions",
            columns: table => new
            {
                revision_number = table.Column<int>(type: "integer", nullable: false),
                holding_id = table.Column<long>(type: "bigint", nullable: false),
                quantity = table.Column<int>(type: "integer", nullable: false),
                reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                revised_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                purchase_price_per_unit_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                purchase_price_per_unit_currency = table.Column<string>(type: "char(3)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_holding_revisions", x => new { x.holding_id, x.revision_number });
                table.ForeignKey(
                    name: "fk_holding_revisions_holdings_holding_id",
                    column: x => x.holding_id,
                    principalTable: "holdings",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "valuations",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                holding_id = table.Column<long>(type: "bigint", nullable: false),
                type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                derived_from_spot_price_id = table.Column<long>(type: "bigint", nullable: true),
                spot_provider_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                spot_source_timestamp_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                method = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                method_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                confidence_band_low = table.Column<decimal>(type: "numeric(6,4)", nullable: false),
                confidence_band_high = table.Column<decimal>(type: "numeric(6,4)", nullable: false),
                computed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                value_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                value_currency = table.Column<string>(type: "char(3)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_valuations", x => x.id);
                table.ForeignKey(
                    name: "fk_valuations_holdings_holding_id",
                    column: x => x.holding_id,
                    principalTable: "holdings",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_valuations_spot_prices_derived_from_spot_price_id",
                    column: x => x.derived_from_spot_price_id,
                    principalTable: "spot_prices",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "ix_asp_net_role_claims_role_id",
            table: "asp_net_role_claims",
            column: "role_id");

        migrationBuilder.CreateIndex(
            name: "role_name_index",
            table: "asp_net_roles",
            column: "normalized_name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_asp_net_user_claims_user_id",
            table: "asp_net_user_claims",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_asp_net_user_logins_user_id",
            table: "asp_net_user_logins",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_asp_net_user_roles_role_id",
            table: "asp_net_user_roles",
            column: "role_id");

        migrationBuilder.CreateIndex(
            name: "email_index",
            table: "asp_net_users",
            column: "normalized_email");

        migrationBuilder.CreateIndex(
            name: "user_name_index",
            table: "asp_net_users",
            column: "normalized_user_name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_coin_images_holding_id",
            table: "coin_images",
            column: "holding_id");

        migrationBuilder.CreateIndex(
            name: "ix_coin_types_mint_id",
            table: "coin_types",
            column: "mint_id");

        migrationBuilder.CreateIndex(
            name: "ix_coin_types_name",
            table: "coin_types",
            column: "name")
            .Annotation("Npgsql:IndexMethod", "gin")
            .Annotation("Npgsql:IndexOperators", Columns0);

        migrationBuilder.CreateIndex(
            name: "ix_coin_types_series_id_year",
            table: "coin_types",
            columns: Columns3);

        migrationBuilder.CreateIndex(
            name: "ix_holdings_coin_type_id",
            table: "holdings",
            column: "coin_type_id");

        migrationBuilder.CreateIndex(
            name: "ix_holdings_user_id_is_deleted",
            table: "holdings",
            columns: Columns5);

        migrationBuilder.CreateIndex(
            name: "ix_idempotency_records_user_id_idempotency_key",
            table: "idempotency_records",
            columns: Columns4,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_identification_runs_obverse_perceptual_hash",
            table: "identification_runs",
            column: "obverse_perceptual_hash");

        migrationBuilder.CreateIndex(
            name: "ix_identification_runs_user_id",
            table: "identification_runs",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_mints_name",
            table: "mints",
            column: "name");

        migrationBuilder.CreateIndex(
            name: "ix_reference_images_coin_type_id",
            table: "reference_images",
            column: "coin_type_id");

        migrationBuilder.CreateIndex(
            name: "ix_reference_images_embedding",
            table: "reference_images",
            column: "embedding")
            .Annotation("Npgsql:IndexMethod", "hnsw")
            .Annotation("Npgsql:IndexOperators", Columns6);

        migrationBuilder.CreateIndex(
            name: "ix_refresh_tokens_family_id",
            table: "refresh_tokens",
            column: "family_id");

        migrationBuilder.CreateIndex(
            name: "ix_refresh_tokens_token_hash",
            table: "refresh_tokens",
            column: "token_hash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_series_mint_id",
            table: "series",
            column: "mint_id");

        migrationBuilder.CreateIndex(
            name: "ix_series_name",
            table: "series",
            column: "name");

        migrationBuilder.CreateIndex(
            name: "ix_spot_prices_metal_currency_source_timestamp_utc",
            table: "spot_prices",
            columns: Columns2,
            descending: Descending7);

        migrationBuilder.CreateIndex(
            name: "ix_valuations_derived_from_spot_price_id",
            table: "valuations",
            column: "derived_from_spot_price_id");

        migrationBuilder.CreateIndex(
            name: "ix_valuations_holding_id_computed_at_utc",
            table: "valuations",
            columns: Columns1);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "asp_net_role_claims");

        migrationBuilder.DropTable(
            name: "asp_net_user_claims");

        migrationBuilder.DropTable(
            name: "asp_net_user_logins");

        migrationBuilder.DropTable(
            name: "asp_net_user_roles");

        migrationBuilder.DropTable(
            name: "asp_net_user_tokens");

        migrationBuilder.DropTable(
            name: "coin_images");

        migrationBuilder.DropTable(
            name: "gradings");

        migrationBuilder.DropTable(
            name: "holding_revisions");

        migrationBuilder.DropTable(
            name: "idempotency_records");

        migrationBuilder.DropTable(
            name: "identification_runs");

        migrationBuilder.DropTable(
            name: "reference_images");

        migrationBuilder.DropTable(
            name: "refresh_tokens");

        migrationBuilder.DropTable(
            name: "series_demand_tiers");

        migrationBuilder.DropTable(
            name: "spot_price_daily");

        migrationBuilder.DropTable(
            name: "valuations");

        migrationBuilder.DropTable(
            name: "asp_net_roles");

        migrationBuilder.DropTable(
            name: "asp_net_users");

        migrationBuilder.DropTable(
            name: "holdings");

        migrationBuilder.DropTable(
            name: "spot_prices");

        migrationBuilder.DropTable(
            name: "coin_types");

        migrationBuilder.DropTable(
            name: "series");

        migrationBuilder.DropTable(
            name: "mints");
    }
}
