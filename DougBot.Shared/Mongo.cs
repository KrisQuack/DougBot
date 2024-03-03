using MongoDB.Bson;
using MongoDB.Driver;

namespace DougBot.Shared;

public class Mongo
{
    private readonly IMongoCollection<BsonDocument> _botSettings;
    private readonly IMongoCollection<BsonDocument> _members;
    private readonly IMongoCollection<BsonDocument> _messages;

    public Mongo()
    {
        var connectionString = Environment.GetEnvironmentVariable("MONGO_URI");
        var client = new MongoClient(connectionString);
        var database = client.GetDatabase("DougBot");
        _botSettings = database.GetCollection<BsonDocument>("BotSettings");
        _members = database.GetCollection<BsonDocument>("Members");
        _messages = database.GetCollection<BsonDocument>("Messages");
    }

    public async Task<BsonDocument> GetBotSettings()
    {
        var settings = await _botSettings.Find(new BsonDocument()).FirstOrDefaultAsync();
        return settings;
    }

    public async Task UpdateBotSettings(BsonDocument settings)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", settings["_id"]);
        await _botSettings.ReplaceOneAsync(filter, settings);
    }

    public async Task<BsonDocument> GetMember(ulong id)
    {
        var member = await _members.Find(new BsonDocument("_id", id.ToString())).FirstOrDefaultAsync();
        return member;
    }

    public async Task<BsonDocument> GetMemberByMcRedeem(string code)
    {
        var member = await _members.Find(new BsonDocument("mc_redeem", code)).FirstOrDefaultAsync();
        return member;
    }

    public async Task<List<BsonDocument>> GetAllMembers()
    {
        return await _members.Find(new BsonDocument()).ToListAsync();
    }

    public async Task InsertMember(BsonDocument member)
    {
        await _members.InsertOneAsync(member);
    }

    public async Task UpdateMember(BsonDocument member)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", member["_id"]);
        await _members.ReplaceOneAsync(filter, member);
    }

    public async Task<BsonDocument> GetMessage(ulong id)
    {
        var message = await _messages.Find(new BsonDocument("_id", id.ToString())).FirstOrDefaultAsync();
        return message;
    }

    public async Task<List<BsonDocument>> GetMessagesByQuery(string? channelId, DateTime? after = null,
        DateTime? before = null)
    {
        var filter = Builders<BsonDocument>.Filter;
        var filters = new List<FilterDefinition<BsonDocument>>();

        if (channelId != null) filters.Add(filter.Eq("channel_id", channelId));
        if (after != null) filters.Add(filter.Gte("created_at", after));
        if (before != null) filters.Add(filter.Lte("created_at", before));

        return filters.Count > 0
            ? await _messages.Find(filter.And(filters)).ToListAsync()
            : await _messages.Find(new BsonDocument()).ToListAsync();
    }


    public async Task InsertMessage(BsonDocument message)
    {
        await _messages.InsertOneAsync(message);
    }

    public async Task UpdateMessage(BsonDocument message)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", message["_id"]);
        await _messages.ReplaceOneAsync(filter, message);
    }
}