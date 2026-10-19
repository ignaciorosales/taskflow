using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using TaskFlow.Domain.Tasks;
using TaskFlow.Domain.Users;

namespace TaskFlow.Infrastructure.Persistence;

public static class MongoSetup
{
    public const string UsersCollection = "users";
    public const string TasksCollection = "tasks";
    public const string RefreshTokensCollection = "refresh_tokens";
    public const string LoginAttemptsCollection = "login_attempts";

    // Serialization is process-wide in the driver, so it's registered exactly once.
    static MongoSetup()
    {
        ConventionRegistry.Register(
            "TaskFlow",
            new ConventionPack { new CamelCaseElementNameConvention(), new IgnoreExtraElementsConvention(true) },
            type => type.Namespace?.StartsWith("TaskFlow.Domain", StringComparison.Ordinal) == true);

        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        // BSON dates (UTC, millisecond precision) so dates compare and sort as instants inside Mongo.
        BsonSerializer.RegisterSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
        BsonSerializer.RegisterSerializer(new EmailSerializer());

        BsonClassMap.RegisterClassMap<TaskItem>(map =>
        {
            map.AutoMap();
            map.MapIdMember(t => t.Id);
        });
        BsonClassMap.RegisterClassMap<User>(map =>
        {
            map.AutoMap();
            map.MapIdMember(u => u.Id);
        });
    }

    /// <summary>Registers the serialization mappings and creates the indexes. Safe to run more than once.</summary>
    public static async Task ConfigureAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        await database.GetCollection<User>(UsersCollection).Indexes.CreateOneAsync(
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(u => u.Email), new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);

        await database.GetCollection<TaskItem>(TasksCollection).Indexes.CreateOneAsync(
            new CreateIndexModel<TaskItem>(Builders<TaskItem>.IndexKeys.Ascending(t => t.OwnerId).Ascending(t => t.DeletedAt)),
            cancellationToken: cancellationToken);
    }

    private sealed class EmailSerializer : SerializerBase<Email>
    {
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Email value)
            => context.Writer.WriteString(value.Value);

        public override Email Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            => Email.Create(context.Reader.ReadString()).Value;
    }
}
