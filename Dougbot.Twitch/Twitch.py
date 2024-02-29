import logging
from datetime import datetime

import discord
from discord.ext import commands, tasks
from twitchAPI.eventsub.websocket import EventSubWebsocket
from twitchAPI.helper import first
from twitchAPI.oauth import refresh_access_token
from twitchAPI.object.eventsub import ChannelChatMessageEvent
from twitchAPI.twitch import Twitch
from twitchAPI.type import AuthScope

from Members import get_member_by_mc_redeem, update_member


class TwitchBot(commands.Cog):
    def __init__(self, client):
        self.discordBot = client
        self.BOT_TARGET_SCOPES = [AuthScope.CHAT_READ]
        self.twitch_bot = None
        self.channel_user = None
        self.bot_user = None

    async def on_chat_message(self, data: ChannelChatMessageEvent):
        msg = data.event.message
        if msg.text.startswith('DMC-') and data.event.channel_points_custom_reward_id == 'a5b9d1c7-44f9-4964-b0f7-42c39cb04f98':
            try:
                print(f"Redeeming code: {msg.text} by {data.event.chatter_user_name}")
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
                            print(f"Error redeeming code: {e}")
                    # Remove the code from the database
                    dbUser['mc_redeem'] = None
                    await update_member(dbUser, self.discordBot.database)
                else:
                    raise Exception("Invalid code")
            except Exception as e:
                print(f"Error redeeming code: {e}")

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
        print(f'Twitch Bot ID: {self.bot_user.id}')
        # Set up EventSub
        self.eventsub = EventSubWebsocket(self.twitch_bot, callback_loop=self.discordBot.loop)
        self.eventsub.reconnect_delay_steps = [10, 10, 10, 10, 10, 10, 10]
        self.eventsub.start()
        await self.eventsub.listen_channel_chat_message(self.channel_user.id, self.bot_user.id, self.on_chat_message)
        print('Twitch EventSub listening')
