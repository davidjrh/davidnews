using System.Collections.Generic;
using System.Web.Http;
using DavidNews.Common;
using DavidNews.Common.Entities;
using Microsoft.WindowsAzure.Mobile.Service;

namespace DavidNews.Mobile.Controllers
{
    public class ItemController : ApiController
    {
        public ApiServices Services { get; set; }

        // GET api/Item?itemid={itemid}
        public Item Get(string itemId)
        {
            return Redis.GetItem(itemId);
        }

        // GET api/Item
        public IEnumerable<Item> Get()
        {
            return Redis.GetTopItems();
        }

        // POST api/Items/Vote
        public void PostVoteItem(string itemId)
        {
            Redis.VoteItem(itemId, 1);
        }
    }
}
