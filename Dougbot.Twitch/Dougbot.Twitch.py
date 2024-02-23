
import logging
import os
import sys
import traceback

import discord
from discord import Embed, Color, Guild, ScheduledEvent, EventStatus, EntityType, PrivacyLevel, Message
from discord.app_commands import AppCommandError
from discord.ext import commands
from motor.motor_asyncio import AsyncIOMotorClient
from Twitch import TwitchBot


class Client(commands.Bot):
    def __init__(self):
        intents = discord.Intents.none()
        intents.guilds = True
        super().__init__(command_prefix='✵', intents=intents, help_command=None)
        # Define first run
        self.first_run = True

    async def on_ready(self):
        logging.getLogger("Main").info(f'Logged on as {self.user}!')

    async def on_guild_available(self, guild: discord.Guild):
        if self.first_run:
            try:
                self.first_run = False
                self.mongo = AsyncIOMotorClient(os.environ.get('MONGO_URI'))
                self.database = self.mongo.DougBot
                await self.load_settings()
                # Log Python version as error to cause ping
                logging.warning(f'Python version: {sys.version}')
                # Load twitch bot
                twitch_bot = TwitchBot(self)
                await twitch_bot.load()
                
            except Exception as e:
                logging.getLogger("Main").error(f'Failed to initialize: {e}\n{traceback.format_exc()}')
                os._exit(1)
        logging.getLogger("Main").info(f'Guild available: {guild.name} ({guild.id})')

    async def load_settings(self):
        self.settings = await self.database.BotSettings.find_one()
        self.statics = type('', (), {})()
        self.statics.guild = client.get_guild(int(self.settings['guild_id']))
        self.statics.twitch_gambling_channel = client.get_channel(int(self.settings['twitch_gambling_channel_id']))
        self.statics.twitch_mod_channel = client.get_channel(int(self.settings['twitch_mod_channel_id']))


# Start the client
client = Client()
client.run(os.environ.get('TOKEN'))
