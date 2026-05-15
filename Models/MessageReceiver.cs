using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPMS.Models
{
    public class MessageReceiver
    {
        [Key]
        public int Id { get; set; }

        public int MessageId { get; set; }

        [ForeignKey("MessageId")]
        public Message Message { get; set; }

        public int ReceiverId { get; set; }

        [ForeignKey("ReceiverId")]
        public Employee Receiver { get; set; }

        public bool IsRead { get; set; } = false;
    }
}