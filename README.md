
# Verdansk Game Bot — A Game Server Watcher Discord Bot

This is a discord bot to watch your game server by utilizing [node-gamedig](https://github.com/gamedig/node-gamedig) and [Discord.NET](https://github.com/discord-net/Discord.Net/). It will report the game server status in a text channel. This bot is written in [.NET (dotnet)](https://dotnet.microsoft.com/) for cross-platform and __multi-threaded environment__ because it will watch many game servers. Game servers are stored in an SQLite database using [Entity Framework (EF) Core](https://docs.microsoft.com/en-us/ef/core/).

- [Features](README#features)
- [Limitations](README#limitations)
- [Future](README#future)
- [Getting Started](README#getting-started)
  - [Install NodeJS and gamedig](README#install-nodejs-and-gamedig),
  - [Get a discord bot token](README#get-discord-bot-token),
  - [Invite the bot to your discord server](README#invite-the-discord-bot),
  - [Run the bot in service mode](README#run-the-bot-in-service-mode-linux-only).

## Features

### 1. Can watch multiple game servers provided it is supported by Gamedig,
### 2. Shows game server properties in a text channel,
### 3. Provides click to join game server for convenience,

## Limitations

### Many-many limits, I only tested this for Project Zomboid Steam Server.

## Future

I'm currently still in active study and can't actively improve this bot. Please contact me if you want to bring improvement to this bot.

There are some improvements currently in mind :
- Add more customizability for different games that reports different properties.
- Add more interactability for server status message.

## Getting Started

This discord bot is self-hosted solution meaning this bot is run by you usually in a Virtual Private Server. _It will support container-based system in the future._

### To use this bot, download the [latest release](https://github.com/ha-ves/VerdanskGameBot/releases) and run the bot. Upon running it, it will perform checks and tell you what needs to be done.

If you want step-by-step instruction to run this bot :
1. [Install NodeJS and gamedig](README#install-nodejs-and-gamedig),
2. [Get a discord bot token](README#get-discord-bot-token),
3. [Invite the bot to your discord server](README#invite-the-discord-bot),
4. [Run the bot in service mode](README#run-the-bot-in-service-mode-linux-only).

## Install NodeJS and Gamedig

1. [Download NodeJS for your operating system](https://nodejs.org/en/download/),
	- Use LTS or Latest versions depending on your other dependent app.

#### Install Gamedig Locally
2. Run `npm install gamedig` in terminal/command prompt in the same directory of the bot executable `VerdanskGameBot.exe`.

#### -Or- Install Gamedig Globally
2. Run `npm install gamedig -g` in terminal/command prompt to install on global directory,
3. Run `npm list -g` in terminal/command prompt to find where your global directory is,
4. Edit your environment variable `NODE_PATH` to your global directory,
	- on Linux this is usually `/usr/local/lib`
	- on Windows this is usually `%APPDATA%\npm`
	- [How to edit your environment variable](https://www.google.com/search?q=how+to+edit+environment+variable)

## Get Discord Bot Token

1. Head to [Discord Developer Portal](https://discord.com/developers/applications), login your discord account, and create your application,
2. Once your discord _application_'s created, head to **SETTINGS** > **Bot** and Add a new bot,  
3. **Reset Token** to get a new Bot Token, save this token in private,
![Get Discord Bot Token](https://www.tekat.my.id/wp-content/uploads/2022/04/get-discord-bot-token.png)
4. Run the bot if you haven't. It will ask for a `BotToken` inside a file named `BotConfig.json`,
5. Put the Bot Token enclosed in double-quote `""`

## Invite The Discord Bot

1. Head back to your discord developer application and go to **SETTINGS** > **OAuth2** > **URL Generator**. Check to use the **Scopes** of `bot` with **Bot Permissions** :
	- Send Messages

2. Copy and visit the **Generated URL** in your browser to invite the bot to your server.
![Invite Bot To Server](https://www.tekat.my.id/wp-content/uploads/2022/04/invite-bot-to-server.jpg)

## Run The Bot in Service Mode (Linux Only)

1. Create a new file in `/etc/systemd/system` named `gameserverwatcher.service` or anything you'd like, remember this as `THE_BOT_SERVICE_NAME`,
2. Add : 
```
[Unit]
Description=YOURBOT_DESCRIPTION
After=network.target

[Service]
User=YOUR_USER
WorkingDirectory=BOT_DIRECTORY
ExecStart=BOT_DIRECTORY/VerdanskGameBot
KillSignal=SIGINT

[Install]
WantedBy=multi-user.target
```
Where :
|Variable|Description|Example Value|
|--|--|--|
|`YOURBOT_DESCRIPTION`|the description of the bot|`Verdansk Game Server Watcher Bot`|
|`YOUR_USER`|is the user you want to run this bot as, you can delete this line if not needed.|`steam`|
|`BOT_DIRECTORY`|is the directory where you put the bot executable|`/home/steam/VerdanskGameBot`|
3. Save the service file and Run `sudo systemctl daemon-reload` to reload any service file changes for systemd,
4. Run `sudo systemctl enable THE_BOT_SERVICE_NAME` and `sudo systemctl start THE_BOT_SERVICE_NAME`
5. Monitor the service using any monitoring tools you like.