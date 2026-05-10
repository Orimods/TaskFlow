import json
import os
import sys
from collections import Counter
from datetime import date, datetime, timedelta
from typing import Any

import requests
from dotenv import load_dotenv


class TaskFlowClient:
    def __init__(self, base_url: str, user_id: int) -> None:
        self.base_url = base_url.rstrip("/")
        self.user_id = user_id
        self.session = requests.Session()

    def login(self) -> dict[str, Any]:
        response = self._request("POST", "/api/auth/login", json_data={"userId": self.user_id}, auth=False)
        token = response["data"]["token"]
        self.session.headers.update({"Authorization": f"Bearer {token}"})
        return response["data"]["user"]

    def get_tasks(self) -> list[dict[str, Any]]:
        response = self._request("GET", "/api/tasks?sort=deadline")
        return response["data"]

    def create_task(self, title: str, description: str) -> dict[str, Any]:
        payload = {
            "title": title,
            "description": description,
            "deadline": (date.today() + timedelta(days=1)).isoformat(),
            "status": "New",
            "categoryId": None,
            "userId": self.user_id,
        }
        response = self._request("POST", "/api/tasks", json_data=payload)
        return response["data"]

    def update_task_status(self, task: dict[str, Any], status: str) -> dict[str, Any]:
        payload = {
            "title": task["title"],
            "description": task.get("description"),
            "deadline": task["deadline"],
            "status": status,
            "categoryId": task.get("categoryId"),
            "userId": task["userId"],
        }
        response = self._request("PUT", f"/api/tasks/{task['id']}", json_data=payload)
        return response["data"]

    def delete_task(self, task_id: int) -> dict[str, Any]:
        response = self._request("DELETE", f"/api/tasks/{task_id}")
        return response["data"]

    def _request(self, method: str, path: str, json_data: dict[str, Any] | None = None, auth: bool = True) -> dict[str, Any]:
        url = f"{self.base_url}{path}"
        try:
            response = self.session.request(method, url, json=json_data, timeout=10)
        except requests.RequestException as error:
            raise RuntimeError(f"API is unavailable: {error}") from error

        if response.status_code in (401, 403):
            message = _extract_error(response)
            raise RuntimeError(f"Access denied ({response.status_code}): {message}")

        if response.status_code >= 400:
            message = _extract_error(response)
            raise RuntimeError(f"API request failed ({response.status_code}): {message}")

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


def build_report(tasks: list[dict[str, Any]]) -> str:
    statuses = Counter(task["status"] for task in tasks)
    nearest = sorted(tasks, key=lambda item: item["deadline"])[:5]

    lines = [
        "TaskFlow Task Auditor report",
        f"Total tasks: {len(tasks)}",
        "Tasks by status:",
    ]

    for status, count in statuses.items():
        lines.append(f"  - {status}: {count}")

    lines.append("Nearest deadlines:")
    for task in nearest:
        lines.append(f"  - #{task['id']} {task['title']} | {task['deadline']} | {task['status']} | {task['userFullName']}")

    return "\n".join(lines)


def run_demo_writes(client: TaskFlowClient) -> None:
    print("\nCRUD check:")
    created = client.create_task(
        "Python service healthcheck",
        "Temporary task created by external Python service and removed after API verification.",
    )
    print(f"  POST /api/tasks -> created task #{created['id']}")

    updated = client.update_task_status(created, "Done")
    print(f"  PUT /api/tasks/{created['id']} -> status {updated['status']}")

    deleted = client.delete_task(created["id"])
    print(f"  DELETE /api/tasks/{created['id']} -> deleted id {deleted['deletedId']}")


def main() -> int:
    load_dotenv()

    base_url = os.getenv("TASKFLOW_API_BASE_URL", "http://localhost:5258")
    user_id = int(os.getenv("TASKFLOW_API_USER_ID", "1"))
    demo_writes = os.getenv("TASKFLOW_DEMO_WRITES", "true").lower() == "true"

    client = TaskFlowClient(base_url, user_id)

    try:
        user = client.login()
        print(f"Authenticated as {user['fullName']} ({user['role']})")

        tasks = client.get_tasks()
        print()
        print(build_report(tasks))

        if demo_writes:
            run_demo_writes(client)

        print("\nService finished successfully.")
        return 0
    except RuntimeError as error:
        print(f"Task Auditor error: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
