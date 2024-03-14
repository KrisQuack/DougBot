using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace DougBot.Shared.Database;

public partial class DougBotContext : DbContext
{
    public DougBotContext()
    {
    }

    public DougBotContext(DbContextOptions<DougBotContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Botsetting> Botsettings { get; set; }

    public virtual DbSet<Member> Members { get; set; }

    public virtual DbSet<MemberUpdate> MemberUpdates { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<MessageUpdate> MessageUpdates { get; set; }

    public virtual DbSet<Serilog> Serilogs { get; set; }

    public virtual DbSet<YoutubeSetting> YoutubeSettings { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql(Environment.GetEnvironmentVariable("CONNECTION_STRING"));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Botsetting>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("botsettings_pkey");

            entity.ToTable("botsetting");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("nextval('botsettings_id_seq'::regclass)")
                .HasColumnName("id");
            entity.Property(e => e.AiApiKey).HasColumnName("ai_api_key");
            entity.Property(e => e.AiApiVersion).HasColumnName("ai_api_version");
            entity.Property(e => e.AiAzureEndpoint).HasColumnName("ai_azure_endpoint");
            entity.Property(e => e.DiscordToken).HasColumnName("discord_token");
            entity.Property(e => e.DmReceiptChannelId)
                .HasPrecision(20)
                .HasColumnName("dm_receipt_channel_id");
            entity.Property(e => e.FullMemberRoleId)
                .HasPrecision(20)
                .HasColumnName("full_member_role_id");
            entity.Property(e => e.GuildId)
                .HasPrecision(20)
                .HasColumnName("guild_id");
            entity.Property(e => e.LogBlacklistChannels)
                .HasColumnType("numeric(20,0)[]")
                .HasColumnName("log_blacklist_channels");
            entity.Property(e => e.LogChannelId)
                .HasPrecision(20)
                .HasColumnName("log_channel_id");
            entity.Property(e => e.ModChannelId)
                .HasPrecision(20)
                .HasColumnName("mod_channel_id");
            entity.Property(e => e.ModRoleId)
                .HasPrecision(20)
                .HasColumnName("mod_role_id");
            entity.Property(e => e.NewMemberRoleId)
                .HasPrecision(20)
                .HasColumnName("new_member_role_id");
            entity.Property(e => e.ReactionFilterChannels)
                .HasColumnType("numeric(20,0)[]")
                .HasColumnName("reaction_filter_channels");
            entity.Property(e => e.ReactionFilterEmotes).HasColumnName("reaction_filter_emotes");
            entity.Property(e => e.ReportChannelId)
                .HasPrecision(20)
                .HasColumnName("report_channel_id");
            entity.Property(e => e.TwitchBotName).HasColumnName("twitch_bot_name");
            entity.Property(e => e.TwitchBotRefreshToken).HasColumnName("twitch_bot_refresh_token");
            entity.Property(e => e.TwitchChannelId)
                .HasPrecision(20)
                .HasColumnName("twitch_channel_id");
            entity.Property(e => e.TwitchChannelName).HasColumnName("twitch_channel_name");
            entity.Property(e => e.TwitchClientId).HasColumnName("twitch_client_id");
            entity.Property(e => e.TwitchClientSecret).HasColumnName("twitch_client_secret");
            entity.Property(e => e.TwitchGamblingChannelId)
                .HasPrecision(20)
                .HasColumnName("twitch_gambling_channel_id");
            entity.Property(e => e.TwitchModChannelId)
                .HasPrecision(20)
                .HasColumnName("twitch_mod_channel_id");
            entity.Property(e => e.YoutubeApi).HasColumnName("youtube_api");
        });

        modelBuilder.Entity<Member>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("member_pkey");

            entity.ToTable("member");

            entity.Property(e => e.Id)
                .HasPrecision(20)
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.GlobalName).HasColumnName("global_name");
            entity.Property(e => e.JoinedAt).HasColumnName("joined_at");
            entity.Property(e => e.McRedeem).HasColumnName("mc_redeem");
            entity.Property(e => e.Nickname).HasColumnName("nickname");
            entity.Property(e => e.Roles)
                .HasColumnType("numeric(20,0)[]")
                .HasColumnName("roles");
            entity.Property(e => e.Username).HasColumnName("username");
            entity.Property(e => e.Verification).HasColumnName("verification");
        });

        modelBuilder.Entity<MemberUpdate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("member_update_pkey");

            entity.ToTable("member_update");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ColumnName).HasColumnName("column_name");
            entity.Property(e => e.MemberId)
                .HasPrecision(20)
                .HasColumnName("member_id");
            entity.Property(e => e.NewValue).HasColumnName("new_value");
            entity.Property(e => e.PreviousValue).HasColumnName("previous_value");
            entity.Property(e => e.UpdateTimestamp)
                .HasDefaultValueSql("now()")
                .HasColumnName("update_timestamp");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("message_pkey");

            entity.ToTable("message");

            entity.Property(e => e.Id)
                .HasPrecision(20)
                .HasColumnName("id");
            entity.Property(e => e.Attachments).HasColumnName("attachments");
            entity.Property(e => e.ChannelId)
                .HasPrecision(20)
                .HasColumnName("channel_id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.MemberId)
                .HasPrecision(20)
                .HasColumnName("member_id");
        });

        modelBuilder.Entity<MessageUpdate>(entity =>
        {
            entity.HasKey(e => e.UpdateId).HasName("message_update_pkey");

            entity.ToTable("message_update");

            entity.Property(e => e.UpdateId).HasColumnName("update_id");
            entity.Property(e => e.ColumnName).HasColumnName("column_name");
            entity.Property(e => e.MessageId)
                .HasPrecision(20)
                .HasColumnName("message_id");
            entity.Property(e => e.NewValue).HasColumnName("new_value");
            entity.Property(e => e.PreviousValue).HasColumnName("previous_value");
            entity.Property(e => e.UpdateTimestamp)
                .HasDefaultValueSql("now()")
                .HasColumnName("update_timestamp");

            entity.HasOne(d => d.Message).WithMany(p => p.MessageUpdates)
                .HasForeignKey(d => d.MessageId)
                .HasConstraintName("message_update_message_id_fkey");
        });

        modelBuilder.Entity<Serilog>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("serilog");

            entity.Property(e => e.Exception).HasColumnName("exception");
            entity.Property(e => e.Level).HasColumnName("level");
            entity.Property(e => e.MachineName).HasColumnName("machine_name");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.MessageTemplate).HasColumnName("message_template");
            entity.Property(e => e.Properties)
                .HasColumnType("jsonb")
                .HasColumnName("properties");
            entity.Property(e => e.RaiseDate).HasColumnName("raise_date");
        });

        modelBuilder.Entity<YoutubeSetting>(entity =>
        {
            entity.HasKey(e => e.SettingId).HasName("youtube_settings_pkey");

            entity.ToTable("youtube_setting");

            entity.Property(e => e.SettingId)
                .HasDefaultValueSql("nextval('youtube_settings_setting_id_seq'::regclass)")
                .HasColumnName("setting_id");
            entity.Property(e => e.LastVideoId).HasColumnName("last_video_id");
            entity.Property(e => e.MentionRoleId)
                .HasPrecision(20)
                .HasColumnName("mention_role_id");
            entity.Property(e => e.PostChannelId)
                .HasPrecision(20)
                .HasColumnName("post_channel_id");
            entity.Property(e => e.UploadPlaylistId).HasColumnName("upload_playlist_id");
            entity.Property(e => e.YoutubeId).HasColumnName("youtube_id");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
