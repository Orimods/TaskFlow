# Task Auditor

Task Auditor is the first external Python service for TaskFlow. It works outside the ASP.NET application and communicates with the protected REST API through HTTP.

## What it does

- receives a JWT token from `POST /api/auth/login`;
- loads tasks through `GET /api/tasks`;
- calculates task statistics by status;
- prints the nearest deadlines;
- demonstrates `POST`, `PUT`, and `DELETE` on a temporary service task;
- handles connection errors and HTTP `401/403` responses without crashing.

## Run

```powershell
cd C:\Users\kofto\source\repos\TaskFlow\python_services\task_auditor
python -m pip install -r requirements.txt
copy .env.example .env
python task_auditor.py
```

The ASP.NET backend must be running first.
