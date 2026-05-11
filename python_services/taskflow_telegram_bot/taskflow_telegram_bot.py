import json
import os
import sys
import time
from collections import Counter
from dataclasses import dataclass
from datetime import date
from html import escape
from typing import Any

import requests
import urllib3
from dotenv import load_dotenv

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)


@dataclass
class BotResponse:
    text: str
    reply_markup: dict[str, Any] | None = None


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

        if response.status_code == 401 and path != "/api/auth/login":
            self.login()
            response = self.session.request(method, url, json=json_data, timeout=15, verify=self.verify_ssl)

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
        self._set_commands()
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
        if "callback_query" in update:
            self._handle_callback(update["callback_query"])
            return

        message = update.get("message") or {}
        chat = message.get("chat") or {}
        chat_id = chat.get("id")
        text = (message.get("text") or "").strip()

        if not chat_id or not text:
            return

        if self.allowed_chat_ids and chat_id not in self.allowed_chat_ids:
            self._send_message(chat_id, BotResponse("Access denied for this chat."))
            return

        try:
            response = handle_command(self.taskflow, text)
        except RuntimeError as error:
            response = BotResponse(f"<b>Error</b>\n{escape(str(error))}")
        except ValueError:
            response = BotResponse("<b>Error</b>\nCheck command format and try again.", main_menu())

        self._send_message(chat_id, response)

    def _handle_callback(self, callback_query: dict[str, Any]) -> None:
        chat_id = callback_query["message"]["chat"]["id"]
        callback_id = callback_query["id"]
        command = callback_query.get("data") or "/help"

        if self.allowed_chat_ids and chat_id not in self.allowed_chat_ids:
            self._answer_callback(callback_id)
            self._send_message(chat_id, BotResponse("Access denied for this chat."))
            return

        try:
            response = handle_command(self.taskflow, command)
        except RuntimeError as error:
            response = BotResponse(f"<b>Error</b>\n{escape(str(error))}", main_menu())
        except ValueError:
            response = BotResponse("<b>Error</b>\nCheck command format and try again.", main_menu())

        self._answer_callback(callback_id)
        self._send_message(chat_id, response)

    def _send_message(self, chat_id: int, response: BotResponse) -> None:
        payload: dict[str, Any] = {
            "chat_id": chat_id,
            "text": response.text[:3900],
            "parse_mode": "HTML",
            "disable_web_page_preview": True,
        }
        if response.reply_markup:
            payload["reply_markup"] = response.reply_markup

        try:
            requests.post(
                f"{self.base_url}/sendMessage",
                json=payload,
                timeout=10,
            ).raise_for_status()
        except requests.RequestException as error:
            print(f"Telegram send error: {self._hide_token(error)}", file=sys.stderr)

    def _answer_callback(self, callback_id: str) -> None:
        try:
            requests.post(
                f"{self.base_url}/answerCallbackQuery",
                json={"callback_query_id": callback_id},
                timeout=10,
            ).raise_for_status()
        except requests.RequestException as error:
            print(f"Telegram callback error: {self._hide_token(error)}", file=sys.stderr)

    def _set_commands(self) -> None:
        commands = [
            {"command": "start", "description": "open TaskFlow menu"},
            {"command": "tasks", "description": "show nearest tasks"},
            {"command": "stats", "description": "show task statistics"},
            {"command": "urgent", "description": "show urgent tasks"},
            {"command": "help", "description": "show command help"},
        ]
        try:
            requests.post(f"{self.base_url}/setMyCommands", json={"commands": commands}, timeout=10).raise_for_status()
        except requests.RequestException as error:
            print(f"Telegram menu setup error: {self._hide_token(error)}", file=sys.stderr)

    def _hide_token(self, error: requests.RequestException) -> str:
        return str(error).replace(self.base_url, self.safe_base_url)


def _extract_error(response: requests.Response) -> str:
    try:
        payload = response.json()
    except ValueError:
        return response.text or "No error details."
    return payload.get("error") or json.dumps(payload, ensure_ascii=False)


def handle_command(client: TaskFlowClient, command: str) -> BotResponse:
    if command == "/start" or command == "/help":
        return BotResponse(
            "<b>TaskFlow Bot</b>\n"
            "Управление задачами прямо из Telegram.\n\n"
            "<b>Команды</b>\n"
            "/tasks - ближайшие задачи\n"
            "/stats - статистика\n"
            "/urgent - срочные задачи\n"
            "/done 3 - отметить задачу выполненной\n"
            "/create Название | Описание | 2026-06-20 - создать задачу",
            main_menu(),
        )

    if command == "/tasks":
        return BotResponse(format_tasks(client.get_tasks()[:8]), main_menu())

    if command == "/stats":
        tasks = client.get_tasks()
        statuses = Counter(task["status"] for task in tasks)
        lines = [
            "<b>TaskFlow Statistics</b>",
            f"Всего задач: <b>{len(tasks)}</b>",
            "",
            f"New: <b>{statuses.get('New', 0)}</b>",
            f"In Progress: <b>{statuses.get('In Progress', 0)}</b>",
            f"Done: <b>{statuses.get('Done', 0)}</b>",
        ]
        return BotResponse("\n".join(lines), main_menu())

    if command == "/urgent":
        today = date.today()
        urgent = [
            task for task in client.get_tasks()
            if task["status"] != "Done" and 0 <= (date.fromisoformat(task["deadline"][:10]) - today).days <= 3
        ]
        text = format_tasks(urgent) if urgent else "<b>Urgent tasks</b>\nСрочных задач на ближайшие 3 дня нет."
        return BotResponse(text, main_menu())

    if command.startswith("/done "):
        task_id = int(command.split(maxsplit=1)[1])
        task = client.mark_done(task_id)
        return BotResponse(
            f"<b>Готово</b>\nЗадача #{task['id']} отмечена как Done:\n{escape(task['title'])}",
            main_menu(),
        )

    if command.startswith("/create "):
        parts = [part.strip() for part in command.removeprefix("/create ").split("|")]
        if len(parts) != 3:
            return BotResponse(
                "<b>Формат создания задачи</b>\n"
                "/create Название | Описание | 2026-06-20",
                main_menu(),
            )
        task = client.create_task(parts[0], parts[1], parts[2])
        return BotResponse(
            f"<b>Задача создана</b>\n#{task['id']} {escape(task['title'])}\nСрок: {escape(task['deadline'][:10])}",
            main_menu(),
        )

    return BotResponse("Неизвестная команда. Нажми /help или выбери кнопку ниже.", main_menu())


def format_tasks(tasks: list[dict[str, Any]]) -> str:
    if not tasks:
        return "<b>TaskFlow Tasks</b>\nЗадачи не найдены."

    lines = ["<b>TaskFlow Tasks</b>"]
    for task in tasks:
        lines.extend(
            [
                "",
                f"<b>#{task['id']} {escape(task['title'])}</b>",
                f"Статус: {escape(task['status'])}",
                f"Срок: {escape(task['deadline'][:10])}",
                f"Ответственный: {escape(task['userFullName'])}",
            ]
        )
    return "\n".join(lines)


def main_menu() -> dict[str, Any]:
    return {
        "inline_keyboard": [
            [
                {"text": "Задачи", "callback_data": "/tasks"},
                {"text": "Статистика", "callback_data": "/stats"},
            ],
            [
                {"text": "Срочные", "callback_data": "/urgent"},
                {"text": "Справка", "callback_data": "/help"},
            ],
        ]
    }


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
        print(handle_command(client, command).text)
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
