﻿using System.Xml;
using Discord;
using DougBot.Discord.Notifications;
using DougBot.Shared.Database;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DougBot.Discord.Modules;

public class YoutubeReadyHandler : INotificationHandler<ReadyNotification>
{
    public async Task Handle(ReadyNotification notification, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    // Get the settings
                    await using var db = new DougBotContext();
                    var settings = await db.Botsettings.FirstOrDefaultAsync();
                    var apiKey = settings.YoutubeApi;
                    var youtubeSettings = await db.YoutubeSettings.ToListAsync();
                    var youtubeService = new YouTubeService(new BaseClientService.Initializer
                    {
                        ApiKey = apiKey
                    });
                    // For each youtube channel, get the most recent video
                    foreach (var channel in youtubeSettings)
                    {
                        // Get the upload playlist ID
                        var uploadsPlaylistId = channel.UploadPlaylistId;
                        if (uploadsPlaylistId == null)
                        {
                            // Get the channel by ID
                            var channelsListRequest = youtubeService.Channels.List("contentDetails");
                            channelsListRequest.Id = channel.YoutubeId;
                            var channelsListResponse = await channelsListRequest.ExecuteAsync();
                            uploadsPlaylistId = channelsListResponse.Items[0].ContentDetails.RelatedPlaylists.Uploads;
                        }

                        // Get the most recent video
                        var playlistItemsListRequest = youtubeService.PlaylistItems.List("snippet");
                        playlistItemsListRequest.PlaylistId = uploadsPlaylistId;
                        playlistItemsListRequest.MaxResults = 1;
                        var playlistItemsListResponse = await playlistItemsListRequest.ExecuteAsync();
                        var video = playlistItemsListResponse.Items[0];
                        // Get the video ID
                        var lastVideoId = channel.LastVideoId;
                        var videoId = video.Snippet.ResourceId.VideoId;
                        // If the ID is the same, continue
                        if (lastVideoId == videoId) continue;
                        // Get the full video details
                        var videoListRequest = youtubeService.Videos.List("snippet,contentDetails");
                        videoListRequest.Id = videoId;
                        var videoListResponse = await videoListRequest.ExecuteAsync();
                        var videoDetails = videoListResponse.Items[0];
                        var channelName = videoDetails.Snippet.ChannelTitle;
                        var channelUrl = $"https://www.youtube.com/channel/{channel.YoutubeId}";
                        var videoTitle = videoDetails.Snippet.Title;
                        var videoUrl = $"https://www.youtube.com/watch?v={videoId}";
                        var videoThumbnail = videoDetails.Snippet.Thumbnails.Maxres.Url;
                        var duration = XmlConvert.ToTimeSpan(videoDetails.ContentDetails.Duration).TotalSeconds;
                        // Skip if it is a live video
                        var liveStatus = videoDetails.Snippet.LiveBroadcastContent;
                        if (liveStatus == "upcoming" || liveStatus == "live") continue;
                        // Create the embed to send
                        var embed = new EmbedBuilder()
                            .WithTitle(videoTitle)
                            .WithUrl(videoUrl)
                            .WithAuthor(channelName, url: channelUrl)
                            .WithImageUrl(videoThumbnail)
                            .WithColor(Color.Orange)
                            .WithFooter($"Duration: {TimeSpan.FromSeconds(duration).ToString("hh\\:mm\\:ss")}");
                        var postChannel =
                            await notification.Client.GetChannelAsync(
                                Convert.ToUInt64(channel.PostChannelId));
                        var mention = $"<@&{channel.MentionRoleId}>";
                        // If it is a short
                        // Or if it is the VOD channel and the title does not contain "VOD"
                        if (duration < 120 || (channel.YoutubeId == "UCzL0SBEypNk4slpzSbxo01g" &&
                                               !videoTitle.ToLower().Contains("vod")))
                            mention = "<@&812501073289805884>";
                        
                        // Check if the video is older than 6hr
                        if (DateTime.UtcNow.AddHours(-6) < videoDetails.Snippet.PublishedAtDateTimeOffset.Value.UtcDateTime)
                        {
                            // Send the message
                            await ((ITextChannel)postChannel).SendMessageAsync(mention, embed: embed.Build());
                        }
                        // Update the last video ID
                        channel.LastVideoId = videoId;
                        // Update the settings
                        await db.SaveChangesAsync();
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "[{Source}]", "Youtube_ReadyHandler");
                }

                // Sleep 5 minutes
                await Task.Delay(300000);
            }
        });
    }
}