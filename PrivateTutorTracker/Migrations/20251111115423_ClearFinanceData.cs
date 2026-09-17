using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrivateTutorTracker.Migrations
{
    /// <inheritdoc />
    public partial class ClearFinanceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Finans modülündeki tüm test verilerini temizle
            migrationBuilder.Sql("DELETE FROM Investments");
            migrationBuilder.Sql("DELETE FROM Debts");
            migrationBuilder.Sql("DELETE FROM Expenses");
            migrationBuilder.Sql("DELETE FROM Incomes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alma işlemi yok (veri kurtarılamaz)
        }
    }
}
