using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dsw2026Tpi.Data.Migrations.Business
{
    /// <inheritdoc />
    public partial class AddSoftDeleteToSpeciality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Specialities",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Appointments",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Specialities");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Appointments");
        }
    }
}
