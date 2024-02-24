using MongoDB.Bson;
using MongoDB.Driver;

namespace DougBot.Shared
{
    public class Mongo
    {
        IMongoCollection<BsonDocument> BotSettings;
        IMongoCollection<BsonDocument> Members;
        IMongoCollection<BsonDocument> Messages;

        public Mongo()
        {
            var connectionString = Environment.GetEnvironmentVariable("MONGO_URI");
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase("DougBot");
            BotSettings = database.GetCollection<BsonDocument>("BotSettings");
            Members = database.GetCollection<BsonDocument>("Members");
            Messages = database.GetCollection<BsonDocument>("Messages");
        }

        public async Task<BsonDocument> GetBotSettings()
        {
            var settings = await BotSettings.Find(new BsonDocument()).FirstOrDefaultAsync();
            return settings;
        }

        public async Task UpdateBotSettings(BsonDocument settings)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", settings["_id"]);
            await BotSettings.ReplaceOneAsync(filter, settings);
        }

        public async Task<BsonDocument> GetMember(ulong id)
        {
            var member = await Members.Find(new BsonDocument("_id", id.ToString())).FirstOrDefaultAsync();
            return member;
        }

        public async Task<List<BsonDocument>> GetAllMembers()
        {
            return await Members.Find(new BsonDocument()).ToListAsync();
        }

        public async Task InsertMember(BsonDocument member)
        {
            await Members.InsertOneAsync(member);
        }

        public async Task UpdateMember(BsonDocument member)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", member["_id"]);
            await Members.ReplaceOneAsync(filter, member);
        }

        public async Task<BsonDocument> GetMessage(ulong id)
        {
            var message = await Messages.Find(new BsonDocument("_id", id.ToString())).FirstOrDefaultAsync();
            return message;
        }

        public async Task<List<BsonDocument>> GetMessagesByQuery(string? channel_id, DateTime? after = null, DateTime? before = null)
        {
            var filter = Builders<BsonDocument>.Filter;
            var filters = new List<FilterDefinition<BsonDocument>>();

            if (channel_id != null)
            {
                filters.Add(filter.Eq("channel_id", channel_id));
            }
            if (after != null)
            {
                filters.Add(filter.Gte("created_at", after));
            }
            if (before != null)
            {
                filters.Add(filter.Lte("created_at", before));
            }

            return filters.Count > 0
                ? await Messages.Find(filter.And(filters)).ToListAsync()
                : await Messages.Find(new BsonDocument()).ToListAsync();
        }


        public async Task InsertMessage(BsonDocument message)
        {
            await Messages.InsertOneAsync(message);
        }

        public async Task UpdateMessage(BsonDocument message)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", message["_id"]);
            await Messages.ReplaceOneAsync(filter, message);
        }
    }
}
