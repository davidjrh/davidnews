using System.Collections.Generic;
using System.Web.Http;
using DavidNews.Common;
using Microsoft.WindowsAzure.Mobile.Service;

namespace DavidNews.Mobile.Controllers
{
    public class CategoriesController : ApiController
    {
        public ApiServices Services { get; set; }

        // GET api/Categories
        public  IEnumerable<string> Get()
        {
            return Redis.GetTopCategories();
        }

    }
}
