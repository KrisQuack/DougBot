import asyncio
import logging
from datetime import datetime, timedelta, timezone
from uuid import UUID
import aiohttp

import discord
import pytz
from discord import Embed, Color, Guild, ScheduledEvent, EventStatus, EntityType, PrivacyLevel, Message
from discord.ext import commands, tasks
from twitchAPI.eventsub.websocket import EventSubWebsocket
from twitchAPI.helper import first
from twitchAPI.oauth import refresh_access_token
from twitchAPI.object.api import Stream
from twitchAPI.object.eventsub import ChannelUpdateEvent, StreamOnlineEvent, StreamOfflineEvent, ChannelChatMessageEvent
from twitchAPI.pubsub import PubSub
from twitchAPI.twitch import Twitch
from twitchAPI.type import AuthScope

from Members import get_member_by_mc_redeem, update_member


class TwitchBot(commands.Cog):
    def __init__(self, client):
        self.discordBot = client
        self.BOT_TARGET_SCOPES = [AuthScope.CHAT_READ, AuthScope.CHAT_EDIT, AuthScope.MODERATION_READ]
        self.twitch_bot = None
        self.channel_user = None
        self.bot_user = None
        self.chat = None
        self.current_gamble_embed: Message = None
        self.current_gamble_last_update: datetime = datetime.utcnow()

    async def on_prediction_event(self, uuid: UUID, data: dict):
        # Extracting event type and prediction data
        event_type = data["type"]
        prediction = data["data"]
        embed = None
        # Dict for color mapping
        color_emotes = {
            "BLUE": "🟦",
            "PINK": "🟥",
        }

        # Helper function to create an embed with a given title and color
        def create_embed(title, color=Color.green()):
            return Embed(title=title, color=color)

        # Helper function to format the ISO string
        def format_iso_str(iso_str):
            iso_str = iso_str.rstrip("Z")
            if "." in iso_str:
                date_str, time_str = iso_str.split("T")
                time_str, fractional = time_str.split(".")
                fractional = fractional[:6]  # Truncate to 6 digits
                return f"{date_str}T{time_str}.{fractional}"
            return iso_str

        # Handling event-created type
        if event_type == "event-created":
            embed = create_embed(f"Prediction Created: {prediction['event']['title']}")
            # Convert ISO string to datetime object
            created_at = datetime.fromisoformat(format_iso_str(prediction["event"]["created_at"])).replace(
                tzinfo=timezone.utc)
            # Calculate the lock timestamp
            lock_timestamp = int(
                (created_at + timedelta(seconds=prediction["event"]["prediction_window_seconds"])).timestamp())
            # Set the embed message
            embed.description = f"Prediction will be locked <t:{lock_timestamp}:R>"
            embed.add_field(name="Outcomes", value="\n".join(
                [f"{color_emotes[outcome['color']]} {outcome['title']}" for outcome in
                 prediction["event"]["outcomes"]]),
                            inline=False)

        # Handling event-updated type
        elif event_type == "event-updated":
            predictionTitle = prediction["event"]["title"]
            # Calculate total points and users for the event
            total_points = sum(outcome["total_points"] for outcome in prediction["event"]["outcomes"])
            total_users = sum(outcome["total_users"] for outcome in prediction["event"]["outcomes"])

            # If the event status is ONGOING
            if prediction["event"]["status"] != "RESOLVED":
                embed = create_embed(f"Prediction: {predictionTitle}")
                for outcome in prediction["event"]["outcomes"]:
                    if outcome["total_points"] == 0 or outcome[
                        "total_users"] == 0 or total_points == 0 or total_users == 0:
                        continue
                    user_percentage = (outcome["total_users"] / total_users) * 100
                    points_percentage = (outcome["total_points"] / total_points) * 100
                    ratio = total_points / outcome["total_points"]
                    top_predictors_str = "\n".join(
                        [f"{predictor['user_display_name']} bet {predictor['points']} points" for predictor in
                         outcome["top_predictors"][:5]]) or "None"
                    embed.add_field(name=f"{color_emotes[outcome['color']]} Outcome: {outcome['title']}",
                                    value=f"Points: {outcome['total_points']} ({points_percentage:.2f}%)\nUsers: {outcome['total_users']} ({user_percentage:.2f}%)\nRatio: {ratio:.2f}\n**Top Predictors:**\n{top_predictors_str}",
                                    inline=False)

            # If the event status is RESOLVED
            else:
                embed = create_embed(f"Prediction Resolved: {predictionTitle}")
                winning_outcome_id = prediction["event"]["winning_outcome_id"]
                total_points = sum(outcome["total_points"] for outcome in prediction["event"]["outcomes"])
                total_users = sum(outcome["total_users"] for outcome in prediction["event"]["outcomes"])

                for outcome in prediction["event"]["outcomes"]:
                    is_winner = "✅" if outcome["id"] == winning_outcome_id else "❌"
                    user_percentage = (outcome["total_users"] / total_users) * 100
                    points_percentage = (outcome["total_points"] / total_points) * 100
                    ratio = total_points / outcome["total_points"]
                    top_predictors_str = "\n".join([
                        f"{predictor['user_display_name']} {'won' if predictor['result']['type'] == 'WIN' else 'lost'} {predictor['result']['points_won'] if predictor['result']['points_won'] is not None else predictor['points']} points"
                        for predictor in outcome["top_predictors"][:5]]) or "None"
                    embed.add_field(name=f"{color_emotes[outcome['color']]} Outcome: {outcome['title']} {is_winner}",
                                    value=f"Points: {outcome['total_points']} ({points_percentage:.2f}%)\nUsers: {outcome['total_users']} ({user_percentage:.2f}%)\nRatio: {ratio:.2f}\n**Top Predictors:**\n{top_predictors_str}",
                                    inline=False)

        # If an embed was created, send it to chat
        if embed:
            if self.current_gamble_embed:
                # If the gamble is not locked, update only every 10 seconds
                if prediction["event"]["status"] != "RESOLVED" and prediction["event"]["status"] != "LOCKED" and (
                        datetime.utcnow() - self.current_gamble_last_update).seconds < 2:
                    return
                await self.current_gamble_embed.edit(embed=embed)
                self.current_gamble_last_update = datetime.utcnow()
            else:
                self.current_gamble_embed = await self.discordBot.statics.twitch_gambling_channel.send(
                    '<@&1080237787174948936>', embed=embed)
                await self.current_gamble_embed.pin()

        if prediction["event"]["status"] == "RESOLVED":
            await self.discordBot.statics.twitch_gambling_channel.send(
                f'The gamble has been resolved {self.current_gamble_embed.jump_url}')
            await self.current_gamble_embed.unpin()
            self.current_gamble_embed = None

        if prediction["event"]["status"] == "CANCELED":
            await self.current_gamble_embed.delete()
            self.current_gamble_embed = None

    async def on_channel_update(self, data: ChannelUpdateEvent):
        # Create and embed of the channel update
        embed = Embed(title=f"Channel Update: {data.event.broadcaster_user_name}", color=Color.green())
        embed.add_field(name="Title", value=data.event.title, inline=True)
        embed.add_field(name="Category", value=data.event.category_name, inline=True)
        # Send the embed to the mod channel
        await self.discordBot.statics.twitch_mod_channel.send(embed=embed)
        # Check if there are any active events
        guild: Guild = self.discordBot.statics.guild
        events = await guild.fetch_scheduled_events()
        # Now iterate over the events
        for event in events:
            event: ScheduledEvent = event
            if event.status == EventStatus.active and event.location == 'https://twitch.tv/dougdoug' and event.creator.bot:
                await event.edit(
                    name=data.event.title,
                )

    async def download_image_as_bytes(url):
            async with aiohttp.ClientSession() as session:
                async with session.get(url) as resp:
                    return await resp.read()
    async def on_stream_online(self, data: StreamOnlineEvent):
        # Get stream details
        await asyncio.sleep(5)
        streams = [stream async for stream in self.twitch_bot.get_streams(user_id=self.channel_user.id, first=1)]
        stream: Stream = streams[0]
        # Create and embed of the channel update
        embed = Embed(title=f"Stream Online: {data.event.broadcaster_user_name}", color=Color.green())
        # Send the embed to the mod channel
        await self.discordBot.statics.twitch_mod_channel.send(embed=embed)
        # Check if there are any pending events
        guild: Guild = self.discordBot.statics.guild
        # Download the image
        thumbnail_bytes = await self.download_image_as_bytes(stream.thumbnail_url.replace('{width}', '1280').replace('{height}', '720'))
        # Create an event for the stream
        event = await guild.create_scheduled_event(
            name=stream.title,
            start_time=datetime.now(tz=pytz.UTC) + timedelta(minutes=5),
            entity_type=EntityType.external,
            privacy_level=PrivacyLevel.guild_only,
            location='https://twitch.tv/dougdoug',
            end_time=datetime.now(tz=pytz.UTC) + timedelta(hours=24),
            description=f'Twitch Stream: {stream.title}\nGame: {stream.game_name}\nhttps://twitch.tv/dougdoug',
            image=thumbnail_bytes
        )
        await event.start()

    async def on_stream_offline(self, data: StreamOfflineEvent):
        # Create and embed of the channel update
        embed = Embed(title=f"Stream Offline: {data.event.broadcaster_user_name}", color=Color.red())
        # Send the embed to the mod channel
        await self.discordBot.statics.twitch_mod_channel.send(embed=embed)
        # Check if there are any active events
        guild: Guild = self.discordBot.statics.guild
        events = await guild.fetch_scheduled_events()
        # Now iterate over the events
        for event in events:
            event: ScheduledEvent = event
            if event.status == EventStatus.active and event.location == 'https://twitch.tv/dougdoug' and event.creator.bot:
                await event.end()
        # Clear the current gamble pins
        pins = await self.discordBot.statics.twitch_gambling_channel.pins()
        for pin in pins:
            if pin.author.id == self.discordBot.user.id:
                await pin.unpin()

    async def on_chat_message(self, data: ChannelChatMessageEvent):
        msg = data.event.message
        if msg.text.startswith('DMC-') and data.event.channel_points_custom_reward_id == 'a5b9d1c7-44f9-4964-b0f7-42c39cb04f98':
            try:
                dbUser = await get_member_by_mc_redeem(msg.text, self.discordBot.database)
                dbUserID = dbUser['_id']
                if dbUser:
                    # respond to the user
                    guild: discord.Guild = self.discordBot.statics.guild
                    discordUser: discord.Member = guild.get_member(int(dbUserID))
                    if discordUser:
                        try:
                            mcRole = guild.get_role(698681714616303646)
                            pesosRole = guild.get_role(954017881652342786)
                            await discordUser.add_roles(mcRole, pesosRole)
                            await self.discordBot.statics.guild.get_channel(698679698699583529).send(f'{discordUser.mention} Redemption succesful, Please link your minecraft account using the instructions in <#743938486888824923>')
                            await self.discordBot.statics.twitch_mod_channel.send(f'Minecraft redemption succesful, Please approve in the redemption queue\nTwitch: {data.event.chatter_user_name}\nDiscord: {discordUser.mention}')
                        except Exception as e:
                            await self.discordBot.statics.twitch_mod_channel.send(f'{discordUser.mention} Redemption failed, Please verify their redemption')
                            logging.getLogger("Twitch").exception(f"Error redeeming code: {e}")
                    # Remove the code from the database
                    dbUser['mc_redeem'] = None
                    await update_member(dbUser, self.discordBot.database)
                else:
                    raise Exception("Invalid code")
            except Exception as e:
                logging.getLogger("Twitch").exception(f"Error redeeming code: {e}")

    async def load(self):
        self.twitch_client_id = self.discordBot.settings["twitch_client_id"]
        self.twitch_client_secret = self.discordBot.settings["twitch_client_secret"]
        self.twitch_bot_name = self.discordBot.settings["twitch_bot_name"]
        self.twitch_bot_refresh_token = self.discordBot.settings["twitch_bot_refresh_token"]
        self.twitch_channel_name = self.discordBot.settings["twitch_channel_name"]
        # Set the current gamble message if any
        pinned_messages = await self.discordBot.statics.twitch_gambling_channel.pins()
        bot_message = None
        for message in pinned_messages:
            if message.author.id == self.discordBot.user.id:
                bot_message = message
                break
        if bot_message:
            self.current_gamble_embed = bot_message
            self.current_gamble_last_update = datetime.utcnow()
        # Set up the Twitch instance for the bot
        self.twitch_bot = await Twitch(self.twitch_client_id,
                                       self.twitch_client_secret)
        self.bot_user = await first(self.twitch_bot.get_users(logins=[self.twitch_bot_name]))
        self.channel_user = await first(
            self.twitch_bot.get_users(logins=self.twitch_channel_name))
        bot_tokens = await refresh_access_token(self.twitch_bot_refresh_token,
                                                self.twitch_client_id,
                                                self.twitch_client_secret)
        await self.twitch_bot.set_user_authentication(bot_tokens[0], self.BOT_TARGET_SCOPES,
                                                      refresh_token=bot_tokens[1])
        logging.getLogger("Twitch").info(f'Twitch Bot ID: {self.bot_user.id}')
        # Set up the pubsub
        self.pubsub = PubSub(self.twitch_bot, self.discordBot.loop)
        self.pubsub.start()
        await self.pubsub.listen_undocumented_topic(f"predictions-channel-v1.{self.channel_user.id}",
                                                    self.on_prediction_event)
        logging.getLogger("Twitch").info('Twitch PubSub listening')
        # Set up EventSub
        self.eventsub = EventSubWebsocket(self.twitch_bot, callback_loop=self.discordBot.loop)
        self.eventsub.reconnect_delay_steps = [10, 10, 10, 10, 10, 10, 10]
        self.eventsub.start()
        await self.eventsub.listen_channel_update_v2(self.channel_user.id, self.on_channel_update)
        await self.eventsub.listen_stream_online(self.channel_user.id, self.on_stream_online)
        await self.eventsub.listen_stream_offline(self.channel_user.id, self.on_stream_offline)
        await self.eventsub.listen_channel_chat_message(self.channel_user.id, self.bot_user.id, self.on_chat_message)
        logging.getLogger("Twitch").info('Twitch EventSub listening')
