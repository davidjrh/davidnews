using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.ServiceModel.Syndication;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Xml;
using System.Xml.Schema;
using DavidNews.Common.Entities;
using StackExchange.Redis;

namespace DavidNews.Common
{
    public class Redis
    {
        private static string ConnectionString
        {
            get
            {
                var cs = Settings.GetConnectionString("Redis.ConnectionString");
                if (string.IsNullOrEmpty(cs))
                {
                    throw new ConfigurationErrorsException(
                        "The Redis connection string can't be an empty string. Check the Redis connectionString attribute in your config file.");
                }
                return cs;
            }
        }

        private static string _keyPrefix;
        private static string KeyPrefix
        {
            get
            {
                if (string.IsNullOrEmpty(_keyPrefix))
                {
                    _keyPrefix = Settings.GetSetting("Redis.KeyPrefix");
                }
                return _keyPrefix;
            }
        }

        private static Lazy<ConnectionMultiplexer> lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
        {
            var cn = ConnectionMultiplexer.Connect(ConnectionString);
          /*  cn.GetSubscriber()
                .Subscribe(new RedisChannel(KeyPrefix + "Redis.*", RedisChannel.PatternMode.Pattern),
                    ProcessMessage);*/
            return cn;
        });

        public static ConnectionMultiplexer Connection
        {
            get { return lazyConnection.Value; }
        }

        private static Lazy<IDatabase> lazyDatabase = new Lazy<IDatabase>(() =>
        {
            return Connection.GetDatabase();
        });
        public static IDatabase Database
        {
            get { return lazyDatabase.Value; }
        }

        public static void Insert(SyndicationFeed syndFeed)
        {
            var baseScore = Settings.GetSetting("Redis.BaseScore", 1000);
            var totalNews = syndFeed.Items.ToArray().Length;
            // Store the feed items on Redis
            for (var i = 0; i<syndFeed.Items.ToArray().Length; i++)
            {
                var score = baseScore - i*baseScore/totalNews;
                var item = syndFeed.Items.ToArray()[i];
                item.SourceFeed = syndFeed;
                Insert(item, score);
            }            
        }

        public static void  Insert(SyndicationItem item, double score)
        {
            Insert(item.ToItem(), score);
        }
        public static void Insert(Item item, double score)
        {
            var id = KeyPrefix + item.Id;

            var tran = Database.CreateTransaction();
            tran.AddCondition(Condition.KeyNotExists(KeyPrefix + item.Links[0].Url));
            
            // Adds to the database
            tran.StringSetAsync(id, Serialize(item));

            // Adds the Url relationship
            tran.StringSetAsync(KeyPrefix + item.Links[0].Url, item.Id);

            // Adds to the set of sorted news
            tran.SortedSetAddAsync(KeyPrefix + "AllNews", item.Id, score);

            foreach (var category in item.Categories)
            {
                // Add the category to the set of categories
                tran.SortedSetIncrementAsync(KeyPrefix + "Categories", category, 1);

                // Add the new to the category
                tran.SortedSetIncrementAsync(KeyPrefix + "Categories:" + category, item.Id, score);
            }

            // For more info see http://stackoverflow.com/questions/16741476/redis-session-expiration-and-reverse-lookup/16747795
            // WORKAROUND: Add the item to the "Items to Expire" set                
            var expiry = TimeSpan.FromMinutes(
                Settings.GetSetting("Redis.CacheExpirationInMinutes", 1440)); 
            tran.SortedSetAddAsync(KeyPrefix + "ItemsToExpire", item.Id,
                DateTime.UtcNow.Ticks + expiry.Ticks);

            var committed = tran.Execute();
        }

        public static void ExpirePoints()
        {
            var interval = Settings.GetSetting("Redis.ExpirationIntervalInMinutes", 5);
            var items = Database.SortedSetRangeByScoreWithScores(KeyPrefix + "ItemsToExpire");
            foreach (var item in items)
            {
                if (item.Element.HasValue)
                {
                    var currentScore = (double) Database.SortedSetScore(KeyPrefix + "AllNews", item.Element);
                    var expirationTime = new DateTime((long) item.Score);
                    var minutesToExpire = (expirationTime - DateTime.UtcNow).TotalMinutes;
                    var intervalsUntilExpiration = minutesToExpire/interval;
                    var newScore = currentScore - (int) (currentScore / intervalsUntilExpiration);
                    VoteItem(item.Element, - (int) (currentScore - newScore));
                }
            }
            
        }

        // Called by a worker every X minutes
        public static void ExpireItems()
        {
            var scoreToExpire = DateTime.UtcNow.Ticks;
            var items = Database.SortedSetRangeByScore(
                KeyPrefix + "ItemsToExpire", stop: scoreToExpire);
            foreach (var itemId in items)
            {
                // Obtains the item
                var item = Deserialize<Item>(Database.StringGet(KeyPrefix + itemId));

                foreach (var category in item.Categories)
                {
                    // Decrement the category to the set of categories
                    Database.SortedSetDecrement(KeyPrefix + "Categories", category, 1);

                    // Add the new to the category
                    Database.SortedSetRemove(KeyPrefix + "Categories:" + category, itemId);
                }

                // Remove the item from the all news set
                Database.SortedSetRemove(KeyPrefix + "AllNews", itemId);

                // Deletes the URL key
                Database.KeyDelete(KeyPrefix + item.Links[0].Url);

                // Deletes the item
                Database.KeyDelete(KeyPrefix + itemId);

                // Decrement source feed score
                Database.StringDecrement(KeyPrefix + "Source:" + item.Source);
            }

            // Remove the items from the Items to Expire set
            Database.SortedSetRemoveRangeByScore(KeyPrefix + "ItemsToExpire", 0, scoreToExpire);
        }

        public static void VoteItem(string itemId, int points)
        {            
            Database.SortedSetIncrement(KeyPrefix + "AllNews", itemId, points);
            var item = GetItem(itemId);
            foreach (var category in item.Categories)
            {
                Database.SortedSetIncrement(KeyPrefix + "Categories:" + category, item.Id, points);                
            }
        }

        public static Item GetItem(string itemId)
        {
            return Deserialize<Item>(Database.StringGet(KeyPrefix + itemId));
        }

        public static IEnumerable<Item> GetTopItems()
        {
            var itemList = Database.SortedSetRangeByRank(KeyPrefix + "AllNews", order: Order.Descending,
                stop: Settings.GetSetting("Redis.MaxItemsToShow", 50));
            return
                itemList.Select(
                    redisValue => Deserialize<Item>(Database.StringGet(KeyPrefix + redisValue.ToString())))
                    .ToArray();
        }
        public static IEnumerable<ItemWithScore> GetTopItemsWithScore()
        {
            // Obtains the top voted news in descending order
            var itemList = Database.SortedSetRangeByRankWithScores(KeyPrefix + "AllNews", 
                order: Order.Descending,
                stop: Settings.GetSetting("Redis.MaxItemsToShow", 50));

            // Returns a deserialized array of news with their score
            return itemList.Select(redisValue => new ItemWithScore()
            {
                Item = Deserialize<Item>(
                        Database.StringGet(KeyPrefix + redisValue.Element.ToString())),
                Score = redisValue.Score
            }).ToArray();
        }


        public static IEnumerable<Item> GetCategoryItems(string category)
        {
            var itemList = Database.SortedSetRangeByRank(KeyPrefix + "Categories:" + category, order: Order.Descending,
                stop: Settings.GetSetting("Redis.MaxItemsToShow", 50));
            return
                itemList.Select(
                    redisValue => Deserialize<Item>(Database.StringGet(KeyPrefix + redisValue.ToString())))
                    .ToArray();
        }

        public static IEnumerable<ItemWithScore> GetCategoryItemsWithScore(string category)
        {
            var itemList = Database.SortedSetRangeByRankWithScores(KeyPrefix + "Categories:" + category, order: Order.Descending,
                stop: Settings.GetSetting("Redis.MaxItemsToShow", 50));
            return itemList.Select(redisValue => new ItemWithScore()
            {
                Item = Deserialize<Item>(Database.StringGet(KeyPrefix + redisValue.Element.ToString())),
                Score = redisValue.Score
            }).ToArray();
        }

        public static IEnumerable<string> GetTopCategories()
        {
            var minScore = Settings.GetSetting("Redis.MinCategoryScore", 3);
            var itemList = Database.SortedSetRangeByRankWithScores(KeyPrefix + "Categories", order: Order.Descending, stop: 20);
            return itemList.Where(x => x.Score > minScore).Select(y => y.Element.ToString()).ToArray();            
        } 

        public static string Serialize(object source)
        {
            IFormatter formatter = new BinaryFormatter();
            var stream = new MemoryStream();
            formatter.Serialize(stream, source);
            return Convert.ToBase64String(stream.ToArray());
        }

        public static T Deserialize<T>(string base64String)
        {
            var stream = new MemoryStream(Convert.FromBase64String(base64String));
            IFormatter formatter = new BinaryFormatter();
            stream.Position = 0;
            return (T)formatter.Deserialize(stream);
        }

        public static byte[] SerializeXmlBinary(object obj)
        {
            using (var ms = new MemoryStream())
            {
                using (var wtr = XmlDictionaryWriter.CreateBinaryWriter(ms))
                {
                    var serializer = new NetDataContractSerializer();
                    serializer.WriteObject(wtr, obj);
                    ms.Flush();
                }
                return ms.ToArray();
            }
        }
        public static object DeSerializeXmlBinary(byte[] bytes)
        {
            using (var rdr = XmlDictionaryReader.CreateBinaryReader(bytes, XmlDictionaryReaderQuotas.Max))
            {
                var serializer = new NetDataContractSerializer { AssemblyFormat = FormatterAssemblyStyle.Simple };
                return serializer.ReadObject(rdr);
            }
        }
        public static byte[] CompressData(object obj)
        {
            byte[] inb = SerializeXmlBinary(obj);
            byte[] outb;
            using (var ostream = new MemoryStream())
            {
                using (var df = new DeflateStream(ostream, CompressionMode.Compress, true))
                {
                    df.Write(inb, 0, inb.Length);
                } outb = ostream.ToArray();
            } return outb;
        }

        public static object DecompressData(byte[] inb)
        {
            byte[] outb;
            using (var istream = new MemoryStream(inb))
            {
                using (var ostream = new MemoryStream())
                {
                    using (var sr =
                        new DeflateStream(istream, CompressionMode.Decompress))
                    {
                        sr.CopyTo(ostream);
                    } outb = ostream.ToArray();
                }
            } return DeSerializeXmlBinary(outb);
        }

    }
}
