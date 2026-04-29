using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaskFlow.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthorizationAndOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "Users",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "CategoryId",
                table: "Tasks",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Учёба" },
                    { 2, "Работа" },
                    { 3, "Личное" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "FullName", "RegisteredAt", "Role", "UserName" },
                values: new object[,]
                {
                    { 1, "Администратор системы", new DateTime(2026, 1, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Admin", "admin" },
                    { 2, "Менеджер проектов", new DateTime(2026, 1, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Manager", "manager" },
                    { 3, "Иван Петров", new DateTime(2026, 1, 24, 0, 0, 0, 0, DateTimeKind.Utc), "User", "ivan" },
                    { 4, "Мария Соколова", new DateTime(2026, 1, 24, 0, 0, 0, 0, DateTimeKind.Utc), "User", "maria" }
                });

            migrationBuilder.InsertData(
                table: "Tasks",
                columns: new[] { "Id", "CategoryId", "Deadline", "Description", "Status", "Title", "UserId" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(2026, 4, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), "Собрать результаты и оформить пояснительную записку.", "In Progress", "Подготовить отчёт по лабораторной", 3 },
                    { 2, 2, new DateTime(2026, 4, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), "Провести ревью задач и уточнить сроки выполнения.", "New", "Проверить задачи команды", 2 },
                    { 3, 2, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), "Подготовить базовый регламент резервного копирования базы данных.", "Done", "Настроить резервное копирование", 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_UserName",
                table: "Users");

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UserName",
                table: "Users");

            migrationBuilder.AlterColumn<int>(
                name: "CategoryId",
                table: "Tasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
