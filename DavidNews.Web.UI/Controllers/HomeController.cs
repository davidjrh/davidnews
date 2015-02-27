using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.UI;
using DavidNews.Common;
using DavidNews.Web.UI.Models;

namespace DavidNews.Web.UI.Controllers
{
    public class HomeController : Controller
    {
#if !DEBUG
        [OutputCache(Location = OutputCacheLocation.Server, Duration=60, VaryByParam = "category")]
#endif
        public ActionResult Index(string category)
        {
            return View(new NewsFeedsModel
            {
                Items =
                    string.IsNullOrEmpty(category)
                        ? Redis.GetTopItemsWithScore()
                        : Redis.GetCategoryItemsWithScore(category),
                Categories = Redis.GetTopCategories()
            });
        }

        public ActionResult About()
        {
            return View();
        }

        public ActionResult BrowseItem(string id)
        {
            // Vote!!
            Redis.VoteItem(id, 1);

            // Clear output cache
            if (bool.Parse(ConfigurationManager.AppSettings["ClearOutputCacheOnVote"]))
                HttpResponse.RemoveOutputCacheItem("/");


            Response.Redirect(Redis.GetItem(id).Links[0].Url.ToString(), true);
            return new EmptyResult();
        }
    }
}