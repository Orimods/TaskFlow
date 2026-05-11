import json
import os
import sys
import time
from collections import Counter
from datetime import date
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
        payload = self._request("POST", "/api/auth/login", json_data={"userId": self.user_id})
        token = payload["data"]["token"]
        self.session.headers.update({"Authorization": f"Bearer {token}"})
        return payload["data"]["user"]

    def get_tasks(self) -> list[dict[str, Any]]:
        payload = self._request("GET", "/api/tasks?sort=deadline")
        return payload["data"]

    def get_task(self, task_id: int) -> dict[str, Any]:
        payload = self._request("GET", f"/api/tasks/{task_id}")
        return payload["data"]

    def create_task(self, title: str, description: str, deadline: str) -> dict[str, Any]:
        payload = {
            "title": title,
            "description": description,
            "deadline": deadline,
            "status": "New",
            "categoryId": None,
            "userId": self.user_id,
        }
        response = self._request("POST", "/api/tasks", json_data=payload)
        return response["data"]

    def mark_done(self, task_id: int) -> dict[str, Any]:
        task = self.get_task(task_id)
        payload = {
            "title": task["title"],
            "description": task.get("description"),
            "deadline": task["deadline"],
            "status": "Done",
            "categoryId": task.get("categoryId"),
            "userId": task["userId"],
        }
        response = self._request("PUT", f"/api/tasks/{task_id}", json_data=payload)
        return response["data"]

    def _request(self, method: str, path: str, json_data: dict[str, Any] | None = None) -> dict[str, Any]:
        url = f"{self.base_url}{path}"
        try:
            response = self.session.request(method, url, json=json_data, timeout=15, verify=self.verify_ssl)
        except requests.RequestException as error:
            raise RuntimeError(f"TaskFlow API is unavailable: {error}") from error

        if response.status_code in (401, 403):
            raise RuntimeError(f"TaskFlow API access denied ({response.status_code}): {_extract_error(response)}")
        if response.status_code >= 400:
            raise RuntimeError(f"TaskFlow API request failed ({response.status_code}): {_extract_error(response)}")

        try:
            payload = response.json()
        except ValueError as error:
            raise RuntimeError("TaskFlow API returned invalid JSON.") from error

        if not payload.get("success", False):
            raise RuntimeError(payload.get("error") or "TaskFlow API returned unsuccessful response.")

        return payload


class TelegramBot:
    def __init__(self, token: str, taskflow: TaskFlowClient, allowed_chat_ids: set[int]) -> None:
        self.base_url = f"https://api.telegram.org/bot{token}"
        self.safe_base_url = "https://api.telegram.org/bot<hidden>"
        self.taskflow = taskflow
        self.allowed_chat_ids = allowed_chat_ids
        self.offset = 0

    def run(self) -> None:
        user = self.taskflow.login()
        print(f"TaskFlow Telegram bot started as {user['fullName']} ({user['role']})")
        print("Waiting for Telegram commands...")

        while True:
            for update in self._get_updates():
                self._handle_update(update)
            time.sleep(1)

    def _get_updates(self) -> list[dict[str, Any]]:
        try:
            response = requests.get(
                f"{self.base_url}/getUpdates",
                params={"offset": self.offset, "timeout": 25},
                timeout=30,
            )
            response.raise_for_status()
            payload = response.json()
        except requests.RequestException as error:
            print(f"Telegram polling error: {self._hide_token(error)}", file=sys.stderr)
            return []

        if not payload.get("ok", False):
            print(f"Telegram API error: {payload}", file=sys.stderr)
            return []

        updates = payload.get("result", [])
        if updates:
            self.offset = updates[-1]["update_id"] + 1
        return updates

    def _handle_update(self, update: dict[str, Any]) -> None:
        message = update.get("message") or {}
        chat = message.get("chat") or {}
        chat_id = chat.get("id")
        text = (message.get("text") or "").strip()

        if not chat_id or not text:
            return

        if self.allowed_chat_ids and chat_id not in self.allowed_chat_ids:
            self._send_message(chat_id, "Access denied for this chat.")
            return

        try:
            answer = handle_command(self.taskflow, text)
        except RuntimeError as error:
            answer = f"Error: {error}"

        self._send_message(chat_id, answer)

    def _send_message(self, chat_id: int, text: str) -> None:
        try:
            requests.post(
                f"{self.base_url}/sendMessage",
                json={"chat_id": chat_id, "text": text[:3900]},
                timeout=10,
            ).raise_for_status()
        except requests.RequestException as error:
            print(f"Telegram send error: {self._hide_token(error)}", file=sys.stderr)

    def _hide_token(self, error: requests.RequestException) -> str:
        return str(error).replace(self.base_url, self.safe_base_url)


def _extract_error(response: requests.Response) -> str:
    try:
        payload = response.json()
    except ValueError:
        return response.text or "No error details."
    return payload.get("error") or json.dumps(payload, ensure_ascii=False)


def handle_command(client: TaskFlowClient, command: str) -> str:
    if command == "/start" or command == "/help":
        return (
            "TaskFlow bot commands:\n"
            "/tasks - nearest tasks\n"
            "/stats - task statistics\n"
            "/urgent - urgent tasks\n"
            "/done 3 - mark task #3 as done\n"
            "/create Title | Description | 2026-06-20 - create task"
        )

    if command == "/tasks":
        return format_tasks(client.get_tasks()[:8])

    if command == "/stats":
        tasks = client.get_tasks()
        statuses = Counter(task["status"] for task in tasks)
        lines = [f"Total tasks: {len(tasks)}", "By status:"]
        lines.extend(f"- {status}: {count}" for status, count in statuses.items())
        return "\n".join(lines)

    if command == "/urgent":
        today = date.today()
        urgent = [
            task for task in client.get_tasks()
            if task["status"] != "Done" and 0 <= (date.fromisoformat(task["deadline"][:10]) - today).days <= 3
        ]
        return format_tasks(urgent) if urgent else "No urgent tasks for the next 3 days."

    if command.startswith("/done "):
        task_id = int(command.split(maxsplit=1)[1])
        task = client.mark_done(task_id)
        return f"Task #{task['id']} marked as Done: {task['title']}"

    if command.startswith("/create "):
        parts = [part.strip() for part in command.removeprefix("/create ").split("|")]
        if len(parts) != 3:
            return "Use: /create Title | Description | 2026-06-20"
        task = client.create_task(parts[0], parts[1], parts[2])
        return f"Created task #{task['id']}: {task['title']}"

    return "Unknown command. Send /help."


def format_tasks(tasks: list[dict[str, Any]]) -> str:
    if not tasks:
        return "No tasks found."

    lines = ["TaskFlow tasks:"]
    for task in tasks:
        lines.append(
            f"#{task['id']} {task['title']} | {task['deadline'][:10]} | "
            f"{task['status']} | {task['userFullName']}"
        )
    return "\n".join(lines)


def parse_allowed_chat_ids(value: str) -> set[int]:
    if not value.strip():
        return set()
    return {int(item.strip()) for item in value.split(",") if item.strip()}


def run_dry_check(client: TaskFlowClient) -> None:
    user = client.login()
    print(f"TaskFlow Telegram bot dry run as {user['fullName']} ({user['role']})")
    print()
    for command in ("/help", "/stats", "/tasks", "/urgent"):
        print(f"> {command}")
        print(handle_command(client, command))
        print()


def main() -> int:
    load_dotenv()

    token = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
    base_url = os.getenv("TASKFLOW_API_BASE_URL", "http://localhost:5258")
    user_id = int(os.getenv("TASKFLOW_API_USER_ID", "1"))
    verify_ssl = os.getenv("TASKFLOW_VERIFY_SSL", "false").lower() == "true"
    dry_run = os.getenv("TASKFLOW_BOT_DRY_RUN", "false").lower() == "true" or not token
    allowed_chat_ids = parse_allowed_chat_ids(os.getenv("TASKFLOW_ALLOWED_CHAT_IDS", ""))

    client = TaskFlowClient(base_url, user_id, verify_ssl)

    try:
        if dry_run:
            run_dry_check(client)
            return 0

        TelegramBot(token, client, allowed_chat_ids).run()
        return 0
    except RuntimeError as error:
        print(f"TaskFlow Telegram bot error: {error}", file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print("TaskFlow Telegram bot stopped.")
        return 0


if __name__ == "__main__":
    raise SystemExit(main())
