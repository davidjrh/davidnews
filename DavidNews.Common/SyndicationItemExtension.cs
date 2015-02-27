using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.ServiceModel.Syndication;
using DavidNews.Common.Entities;

namespace DavidNews.Common
{
    public static class SyndicationItemExtension
    {
        public static Item ToItem(this SyndicationItem syndItem)
        {
            return new Item()
            {
                Id = Guid.NewGuid().ToString().ToLowerInvariant(),
                Title = (syndItem.Title != null) ? HttpUtility.HtmlDecode(syndItem.Title.Text) : string.Empty,
                Summary = (syndItem.Summary != null) ? HttpUtility.HtmlDecode(syndItem.Summary.Text) : string.Empty,
                Source = syndItem.SourceFeed.Title.Text,
                PublishDate = syndItem.PublishDate.DateTime,
                Categories = syndItem.Categories.Select(x => x.Name).ToArray(),
                Links = syndItem.Links.Select(y => new Link() { Url = y.Uri, ContentType = y.MediaType }).ToArray()
            };
        }
    }
}
