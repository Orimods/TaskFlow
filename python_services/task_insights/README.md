# TaskFlow Task Insights

Second external Python service for Lab 6. The service authenticates in TaskFlow REST API, reads tasks, classifies them by status and deadline, and writes analytical reports to JSON and CSV files.

## Local run

```powershell
cd C:\Users\kofto\source\repos\TaskFlow\python_services\task_insights
python -m pip install -r requirements.txt
$env:TASKFLOW_API_BASE_URL="https://localhost:32773"
$env:TASKFLOW_VERIFY_SSL="false"
python task_insights.py
```

## Docker run

The service is included in the root `docker-compose.yml` and uses `http://api:5000` inside Docker network.
