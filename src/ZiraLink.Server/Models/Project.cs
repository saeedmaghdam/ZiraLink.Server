using ZiraLink.Server.Enums;

namespace ZiraLink.Server.Models
{
    public class Project
    {
        public long Id { get; set; }
        public Guid ViewId { get; set; }
        public long CustomerId { get; set; }
        public string Title { get; set; }
        public DomainType DomainType { get; set; }
        public string Domain { get; set; }
        public string InternalUrl { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }
        public ProjectState State { get; set; }

        public Customer Customer { get; set; }
    }
}
