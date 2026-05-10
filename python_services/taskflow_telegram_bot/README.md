# TaskFlow Telegram Bot

External Python service for Lab 6. The bot connects Telegram users with the protected TaskFlow REST API.

## Commands

- `/start` - show help
- `/tasks` - show nearest tasks
- `/stats` - show status statistics
- `/urgent` - show urgent tasks
- `/done 3` - mark task with id 3 as done
- `/create Title | Description | 2026-06-20` - create a new task

## Local run

```powershell
cd C:\Users\kofto\source\repos\TaskFlow\python_services\taskflow_telegram_bot
python -m pip install -r requirements.txt
$env:TELEGRAM_BOT_TOKEN="token from BotFather"
$env:TASKFLOW_API_BASE_URL="https://localhost:32773"
$env:TASKFLOW_VERIFY_SSL="false"
python taskflow_telegram_bot.py
```

If there is no Telegram token, set `TASKFLOW_BOT_DRY_RUN=true` to test REST API integration without Telegram.
