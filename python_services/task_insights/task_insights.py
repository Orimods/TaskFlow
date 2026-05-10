import csv
import json
import os
import sys
import time
from collections import Counter, defaultdict
from datetime import date, datetime, timedelta
from pathlib import Path
from typing import Any

import requests
import urllib3
from dotenv import load_dotenv

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)


class TaskFlowClient:
    def __init__(self, base_url: str, user_id: int, verify_ssl: bool) -> None:
        self.base_url = base_url.rstrip("/")
        self.user_id = user_id
        self.verify_ssl = verify_ssl
        self.session = requests.Session()

    def login(self) -> dict[str, Any]:
        payload = self._request("POST", "/api/auth/login", json_data={"userId": self.user_id}, auth=False)
        token = payload["data"]["token"]
        self.session.headers.update({"Authorization": f"Bearer {token}"})
        return payload["data"]["user"]

    def get_tasks(self) -> list[dict[str, Any]]:
        payload = self._request("GET", "/api/tasks?sort=deadline")
        return payload["data"]

    def create_summary_task(self, title: str, description: str, user_id: int) -> dict[str, Any]:
        payload = {
            "title": title,
            "description": description,
            "deadline": (date.today() + timedelta(days=1)).isoformat(),
            "status": "New",
            "categoryId": None,
            "userId": user_id,
        }
        response = self._request("POST", "/api/tasks", json_data=payload)
        return response["data"]

    def _request(
        self,
        method: str,
        path: str,
        json_data: dict[str, Any] | None = None,
        auth: bool = True,
    ) -> dict[str, Any]:
        url = f"{self.base_url}{path}"
        try:
            response = self.session.request(method, url, json=json_data, timeout=15, verify=self.verify_ssl)
        except requests.RequestException as error:
            raise RuntimeError(f"API is unavailable: {error}") from error

        if response.status_code in (401, 403):
            raise RuntimeError(f"Access denied ({response.status_code}): {_extract_error(response)}")

        if response.status_code >= 400:
            raise RuntimeError(f"API request failed ({response.status_code}): {_extract_error(response)}")

        try:
            payload = response.json()
        except ValueError as error:
            raise RuntimeError("API returned invalid JSON.") from error

        if not payload.get("success", False):
            raise RuntimeError(payload.get("error") or "API returned unsuccessful response.")

        return payload


def _extract_error(response: requests.Response) -> str:
    try:
        payload = response.json()
    except ValueError:
        return response.text or "No error details."
    return payload.get("error") or json.dumps(payload, ensure_ascii=False)


def parse_deadline(value: str) -> date:
    normalized = value.replace("Z", "+00:00")
    return datetime.fromisoformat(normalized).date()


def classify_task(task: dict[str, Any], today: date) -> str:
    status = task["status"]
    deadline = parse_deadline(task["deadline"])

    if status == "Done":
        return "done"

    days_left = (deadline - today).days
    if days_left < 0:
        return "overdue"
    if days_left <= 2:
        return "urgent"
    return "planned"


def build_insights(tasks: list[dict[str, Any]]) -> dict[str, Any]:
    today = date.today()
    status_counts = Counter(task["status"] for task in tasks)
    class_counts = Counter(classify_task(task, today) for task in tasks)
    per_user: dict[str, Counter[str]] = defaultdict(Counter)

    enriched_tasks: list[dict[str, Any]] = []
    for task in tasks:
        task_class = classify_task(task, today)
        user_name = task.get("userFullName") or f"user #{task['userId']}"
        per_user[user_name][task_class] += 1
        enriched_tasks.append(
            {
                "id": task["id"],
                "title": task["title"],
                "status": task["status"],
                "deadline": task["deadline"],
                "user": user_name,
                "category": task.get("categoryName"),
                "class": task_class,
            }
        )

    return {
        "generatedAt": datetime.now().isoformat(timespec="seconds"),
        "totalTasks": len(tasks),
        "byStatus": dict(status_counts),
        "byClass": {
            "overdue": class_counts.get("overdue", 0),
            "urgent": class_counts.get("urgent", 0),
            "planned": class_counts.get("planned", 0),
            "done": class_counts.get("done", 0),
        },
        "byUser": {user: dict(counts) for user, counts in sorted(per_user.items())},
        "nearestDeadlines": sorted(enriched_tasks, key=lambda item: item["deadline"])[:5],
    }


def save_reports(insights: dict[str, Any], report_dir: Path) -> tuple[Path, Path]:
    report_dir.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    json_path = report_dir / f"task_insights_{stamp}.json"
    csv_path = report_dir / f"task_insights_{stamp}.csv"

    json_path.write_text(json.dumps(insights, ensure_ascii=False, indent=2), encoding="utf-8")

    with csv_path.open("w", newline="", encoding="utf-8-sig") as file:
        writer = csv.writer(file, delimiter=";")
        writer.writerow(["metric", "value"])
        writer.writerow(["totalTasks", insights["totalTasks"]])
        for status, count in insights["byStatus"].items():
            writer.writerow([f"status:{status}", count])
        for task_class, count in insights["byClass"].items():
            writer.writerow([f"class:{task_class}", count])

    return json_path, csv_path


def print_summary(user: dict[str, Any], insights: dict[str, Any], json_path: Path, csv_path: Path) -> None:
    print(f"Authenticated as {user['fullName']} ({user['role']})")
    print()
    print("TaskFlow Task Insights report")
    print(f"Total tasks: {insights['totalTasks']}")
    print("Classes:")
    for task_class, count in insights["byClass"].items():
        print(f"  - {task_class}: {count}")
    print("Users:")
    for user_name, counts in insights["byUser"].items():
        print(f"  - {user_name}: {sum(counts.values())} tasks")
    print("Nearest deadlines:")
    for task in insights["nearestDeadlines"]:
        print(f"  - #{task['id']} {task['title']} | {task['deadline']} | {task['class']} | {task['user']}")
    print()
    print(f"JSON report: {json_path}")
    print(f"CSV report: {csv_path}")


def run_once() -> None:
    load_dotenv()

    base_url = os.getenv("TASKFLOW_API_BASE_URL", "http://localhost:5258")
    user_id = int(os.getenv("TASKFLOW_API_USER_ID", "1"))
    verify_ssl = os.getenv("TASKFLOW_VERIFY_SSL", "false").lower() == "true"
    report_dir = Path(os.getenv("TASKFLOW_REPORT_DIR", "reports"))
    create_summary = os.getenv("TASKFLOW_CREATE_SUMMARY_TASK", "false").lower() == "true"
    summary_user_id = int(os.getenv("TASKFLOW_SUMMARY_USER_ID", str(user_id)))

    client = TaskFlowClient(base_url, user_id, verify_ssl)
    user = client.login()
    tasks = client.get_tasks()
    insights = build_insights(tasks)
    json_path, csv_path = save_reports(insights, report_dir)
    print_summary(user, insights, json_path, csv_path)

    if create_summary:
        task = client.create_summary_task(
            "TaskFlow insights summary",
            f"Generated analytics: {insights['totalTasks']} tasks, {insights['byClass']['urgent']} urgent.",
            summary_user_id,
        )
        print(f"Created summary task #{task['id']}")


def main() -> int:
    load_dotenv()
    run_once_mode = os.getenv("TASKFLOW_RUN_ONCE", "true").lower() == "true"
    interval_seconds = int(os.getenv("TASKFLOW_INTERVAL_SECONDS", "3600"))

    try:
        if run_once_mode:
            run_once()
            return 0

        while True:
            run_once()
            print(f"Sleeping for {interval_seconds} seconds...")
            time.sleep(interval_seconds)
    except RuntimeError as error:
        print(f"Task Insights error: {error}", file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print("Task Insights stopped.")
        return 0


if __name__ == "__main__":
    raise SystemExit(main())
