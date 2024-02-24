using DougBot.Shared;
using DougBot.Discord.Notifications;
using MediatR;
using MongoDB.Bson;
using Serilog;
using Google.Apis.YouTube.v3;
using Google.Apis.Services;
using System.Xml;
using Discord;

namespace DougBot.Discord.Modules
{
    public class Youtube_ReadyHandler : INotificationHandler<ReadyNotification>
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
                        var settings = await new Mongo().GetBotSettings();
                        var api_key = settings["youtube_api"].AsString;
                        var youtube_settings = (BsonArray)settings["youtube_settings"];
                        var youtube_service = new YouTubeService(new BaseClientService.Initializer()
                        {
                            ApiKey = api_key
                        });
                        // For each youtube channel, get the most recent video
                        foreach (var channel in youtube_settings)
                        {
                            // Get the upload playlist ID
                            var uploads_playlist_id = channel["upload_playlist_id"].AsString;
                            if(uploads_playlist_id == null)
                            {
                                // Get the channel by ID
                                var channelsListRequest = youtube_service.Channels.List("contentDetails");
                                channelsListRequest.Id = channel["youtube_id"].AsString;
                                var channelsListResponse = await channelsListRequest.ExecuteAsync();
                                uploads_playlist_id = channelsListResponse.Items[0].ContentDetails.RelatedPlaylists.Uploads;
                            }
                            // Get the most recent video
                            var playlistItemsListRequest = youtube_service.PlaylistItems.List("snippet");
                            playlistItemsListRequest.PlaylistId = uploads_playlist_id;
                            playlistItemsListRequest.MaxResults = 1;
                            var playlistItemsListResponse = await playlistItemsListRequest.ExecuteAsync();
                            var video = playlistItemsListResponse.Items[0];
                            // Get the video ID
                            var last_video_id = channel["last_video_id"].AsString;
                            var video_id = video.Snippet.ResourceId.VideoId;
                            // If the ID is the same, continue
                            if (last_video_id == video_id)
                            {
                                continue;
                            }
                            // Get the full video details
                            var videoListRequest = youtube_service.Videos.List("snippet,contentDetails");
                            videoListRequest.Id = video_id;
                            var videoListResponse = await videoListRequest.ExecuteAsync();
                            var video_details = videoListResponse.Items[0];
                            var channel_name = video_details.Snippet.ChannelTitle;
                            var channel_url = $"https://www.youtube.com/channel/{channel["youtube_id"].AsString}";
                            var video_title = video_details.Snippet.Title;
                            var video_url = $"https://www.youtube.com/watch?v={video_id}";
                            var video_thumbnail = video_details.Snippet.Thumbnails.Maxres.Url;
                            var duration = XmlConvert.ToTimeSpan(video_details.ContentDetails.Duration).TotalSeconds;
                            // Skip if it is a live video
                            var live_status = video_details.Snippet.LiveBroadcastContent;
                            if(live_status == "upcoming" || live_status == "live")
                            {
                                continue;
                            }
                            // Create the embed to send
                            var embed = new EmbedBuilder()
                                .WithTitle(video_title)
                                .WithUrl(video_url)
                                .WithAuthor(channel_name, url: channel_url)
                                .WithImageUrl(video_thumbnail)
                                .WithColor(Color.Orange)
                                .WithFooter($"Duration: {TimeSpan.FromSeconds(duration).ToString("hh\\:mm\\:ss")}");
                            var post_channel = await notification.Client.GetChannelAsync(Convert.ToUInt64(channel["post_channel_id"].AsString));
                            var mention = $"<@&{channel["mention_role_id"].AsString}>";
                            // If it is a short
                            // Or if it is the VOD channel and the title does not contain "VOD"
                            if (duration < 120|| (channel["youtube_id"].AsString == "UCzL0SBEypNk4slpzSbxo01g" && !video_title.ToLower().Contains("vod")))
                            {
                                mention = "<@&812501073289805884>";
                            }
                            // Send the message
                            await ((ITextChannel)post_channel).SendMessageAsync(mention,embed: embed.Build());
                            // Update the last video ID
                            channel["last_video_id"] = video_id;
                        }
                        // Update the settings
                        await new Mongo().UpdateBotSettings(settings);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Error in Youtube_ReadyHandler");
                    }
                    // Sleep 5 minutes
                    await Task.Delay(300000);
                }
            });
        }
    }
}
