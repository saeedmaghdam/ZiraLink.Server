namespace ZiraLink.Server.Models
{
    public class Customer
    {
        public long Id { get; set; }
        public Guid ViewId { get; set; }
        public string ExternalId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        public string Family { get; set; }
    }
}
