using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThirdOpinion.Common.IA.Pipelines.TestHarness.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pipeline_runs",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RunType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Configuration = table.Column<string>(type: "jsonb", nullable: true),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: true),
                    TotalResources = table.Column<int>(type: "integer", nullable: false),
                    CompletedResources = table.Column<int>(type: "integer", nullable: false),
                    FailedResources = table.Column<int>(type: "integer", nullable: false),
                    SkippedResources = table.Column<int>(type: "integer", nullable: false),
                    ParentRunId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pipeline_runs", x => x.RunId);
                    table.ForeignKey(
                        name: "FK_pipeline_runs_pipeline_runs_ParentRunId",
                        column: x => x.ParentRunId,
                        principalTable: "pipeline_runs",
                        principalColumn: "RunId");
                });

            migrationBuilder.CreateTable(
                name: "resource_runs",
                columns: table => new
                {
                    ResourceRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    PipelineRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessingTimeMs = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ErrorStep = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_runs", x => x.ResourceRunId);
                    table.ForeignKey(
                        name: "FK_resource_runs_pipeline_runs_PipelineRunId",
                        column: x => x.PipelineRunId,
                        principalTable: "pipeline_runs",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "artifacts",
                columns: table => new
                {
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ArtifactName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    StorageType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StoragePath = table.Column<string>(type: "text", nullable: true),
                    DataJson = table.Column<string>(type: "jsonb", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artifacts", x => x.ArtifactId);
                    table.ForeignKey(
                        name: "FK_artifacts_resource_runs_ResourceRunId",
                        column: x => x.ResourceRunId,
                        principalTable: "resource_runs",
                        principalColumn: "ResourceRunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "step_progress",
                columns: table => new
                {
                    StepProgressId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_step_progress", x => x.StepProgressId);
                    table.ForeignKey(
                        name: "FK_step_progress_resource_runs_ResourceRunId",
                        column: x => x.ResourceRunId,
                        principalTable: "resource_runs",
                        principalColumn: "ResourceRunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_artifacts_ResourceRunId_StepName_ArtifactName",
                table: "artifacts",
                columns: new[] { "ResourceRunId", "StepName", "ArtifactName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pipeline_runs_Category_Name",
                table: "pipeline_runs",
                columns: new[] { "Category", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_pipeline_runs_ParentRunId",
                table: "pipeline_runs",
                column: "ParentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_resource_runs_PipelineRunId_ResourceId",
                table: "resource_runs",
                columns: new[] { "PipelineRunId", "ResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_resource_runs_Status",
                table: "resource_runs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_step_progress_ResourceRunId_Sequence",
                table: "step_progress",
                columns: new[] { "ResourceRunId", "Sequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artifacts");

            migrationBuilder.DropTable(
                name: "step_progress");

            migrationBuilder.DropTable(
                name: "resource_runs");

            migrationBuilder.DropTable(
                name: "pipeline_runs");
        }
    }
}
