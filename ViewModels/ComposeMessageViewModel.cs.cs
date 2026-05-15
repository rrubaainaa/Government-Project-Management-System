namespace GPMS.ViewModels
{
    public class ComposeMessageViewModel
    {
        public string Subject { get; set; }

        public string Body { get; set; }

        public List<int> ReceiverIds { get; set; }
    }
}
