using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace CosmosDBConsoleApp
{
    public class MembershipRegistryItem
    {
        [BsonId]
        [BsonIgnoreIfDefault]
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string HomeAddress { get; set; }
        public string ZIP { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public DateTime? MembershipStart { get; set; }
        public MembershipRegistryItem()
        {
        }
        public MembershipRegistryItem(MembershipRegistryItem item)
        {
            FirstName = (string)item.FirstName?.Clone();
            LastName = (string)item.LastName?.Clone();
            HomeAddress = (string)item.HomeAddress?.Clone();
            ZIP = (string)item.ZIP?.Clone();
            PhoneNumber = (string)item.PhoneNumber?.Clone();
            Email = (string)item.Email?.Clone();
            MembershipStart = item?.MembershipStart;
        }
    }
}