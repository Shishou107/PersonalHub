# AI Daily Expense Backend API

Backend cho ứng dụng quản lý chi tiêu, công việc, giao dịch ngân hàng và kho tài khoản.

## Công nghệ

- ASP.NET Core 9 Web API
- Entity Framework Core SQL Server
- JWT Bearer Authentication
- BCrypt.Net-Next
- Swagger

## Chạy local

1. Vào thư mục API:

```bash
cd backend/AiDailyExpense.Api
```

2. Khôi phục thư viện:

```bash
dotnet restore
```

3. Chạy API:

```bash
dotnet run
```

Khi khởi động, API tự tạo bảng còn thiếu và seed danh mục mặc định. API không tạo tài khoản thử nghiệm.

Swagger mở tại:

```text
http://localhost:5000/swagger
```

## Publish Somee

Publish ra thư mục `public`:

```bash
dotnet publish backend/AiDailyExpense.Api/AiDailyExpense.Api.csproj -c Release -o public --no-restore
```

SQL bổ sung để chạy tay trên host nằm ở:

```text
backend/AiDailyExpense.Api/database/somee-update.sql
```
