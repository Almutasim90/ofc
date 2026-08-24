# POS System — بيت الشاي

Online-first point-of-sale and recipe-based inventory system for a multi-branch tea shop.

## Architecture

- `backend/src/POS.Domain` — entities, constants, and domain events.
- `backend/src/POS.Application` — use cases, DTOs, validation, and abstractions.
- `backend/src/POS.Infrastructure` — EF Core/PostgreSQL, JWT, hashing, migrations, and background services.
- `backend/src/POS.API` — controllers, permission policies, middleware, and Swagger.
- `frontend` — React, TypeScript, Tailwind CSS, RTL/LTR i18n, and light/dark themes.

Supabase is used only as hosted PostgreSQL through the EF Core connection string. Authentication and RBAC are implemented by the ASP.NET Core API.

## Local development

Create the root `.env` from `.env.example`, then run:

```powershell
cd backend
dotnet run --project src/POS.API/POS.API.csproj --launch-profile http
```

```powershell
cd frontend
npm install
npm run dev
```

- Frontend: `http://localhost:5173`
- API: `http://localhost:5246`
- Swagger: `http://localhost:5246/swagger`

## API permissions

- `/api/sales` — `sales.create`
- `/api/voids` — `sales.void`
- `/api/inventory` — `inventory.adjust`
- `/api/reports` — `reports.branch.view` or `reports.global.view`
- `/api/users` — `users.manage`
- `/api/branches` — `branches.manage`
- `/api/products`, `/api/raw-materials` — `products.manage`
- `/api/closing-schedule` — `closing.configure`

Effective permissions are role permissions with individual user grants/denials applied on top. Branch-scoped entities use EF Core global query filters; General Managers bypass the branch filter.

## Shift workflow

Each branch defines `DefaultOpeningFloat`. A cashier normally opens a shift with one action using that value, but may submit an optional override. Closing requires denomination counts; the API calculates actual cash and variance and stores the individual count lines for audit.

## Extension points

Completed sales publish `SaleCompletedEvent`. Printing, KDS, and other integrations should be added as new domain-event handlers without modifying the sale transaction workflow.
