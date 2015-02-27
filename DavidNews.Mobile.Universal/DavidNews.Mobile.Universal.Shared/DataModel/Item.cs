using System;

namespace DavidNews.Mobile.Universal.DataModel
{
    public class Item
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string Source { get; set; }
        public DateTime PublishDate { get; set; }
        public string[] Categories { get; set; }
        public Link[] Links { get; set; }
    }

    public class Link
    {
        public Uri Url { get; set; }
        public string ContentType { get; set; }
    }

    public class ItemWithScore
    {
        public double Score { get; set; }
        public Item Item { get; set; }

    }

}
