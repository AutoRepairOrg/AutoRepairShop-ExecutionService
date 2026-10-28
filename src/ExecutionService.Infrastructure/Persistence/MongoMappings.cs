using ExecutionService.Domain.Entities;
using ExecutionService.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace ExecutionService.Infrastructure.Persistence;

public static class MongoMappings
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;

        if (!BsonClassMap.IsClassMapRegistered(typeof(Execution)))
        {
            BsonClassMap.RegisterClassMap<Execution>(cm =>
            {
                cm.AutoMap();
                cm.MapIdMember(e => e.Id);
                cm.UnmapMember(e => e.DomainEvents);
                cm.GetMemberMap(e => e.Status)
                    .SetSerializer(new EnumSerializer<ExecutionStatus>(BsonType.String));
            });
        }

        _registered = true;
    }
}
