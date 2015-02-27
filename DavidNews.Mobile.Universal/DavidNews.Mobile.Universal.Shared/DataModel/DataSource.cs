using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using DavidNews.Mobile.Universal.DataModel;

// The data model defined by this file serves as a representative example of a strongly-typed
// model.  The property names chosen coincide with data bindings in the standard item templates.
//
// Applications may use this model as a starting point and build on it, or discard it entirely and
// replace it with something appropriate to their needs. If using this model, you might improve app 
// responsiveness by initiating the data loading task in the code behind for App.xaml when the app 
// is first launched.

namespace DavidNews.Mobile.Universal.Data
{
    /// <summary>
    /// Generic item data model.
    /// </summary>
    public class DataItem
    {
        public DataItem(String uniqueId, String title, String subtitle, String imagePath, String description, String content)
        {
            this.UniqueId = uniqueId;
            this.Title = title;
            this.Subtitle = subtitle;
            this.Description = description;
            this.ImagePath = imagePath;
            this.Content = content;
        }

        public string UniqueId { get; private set; }
        public string Title { get; private set; }
        public string Subtitle { get; private set; }
        public string Description { get; private set; }
        public string ImagePath { get; private set; }
        public string Content { get; private set; }

        public override string ToString()
        {
            return this.Title;
        }
    }

    /// <summary>
    /// Generic group data model.
    /// </summary>
    public class DataGroup
    {
        public DataGroup(String uniqueId, String title, String subtitle, String imagePath, String description)
        {
            this.UniqueId = uniqueId;
            this.Title = title;
            this.Subtitle = subtitle;
            this.Description = description;
            this.ImagePath = imagePath;
            this.Items = new ObservableCollection<DataItem>();
        }

        public string UniqueId { get; private set; }
        public string Title { get; private set; }
        public string Subtitle { get; private set; }
        public string Description { get; private set; }
        public string ImagePath { get; private set; }
        public ObservableCollection<DataItem> Items { get; private set; }

        public override string ToString()
        {
            return this.Title;
        }
    }

    /// <summary>
    /// Creates a collection of groups and items with content read from a static json file.
    /// 
    /// DataSource initializes with data read from a static json file included in the 
    /// project.  This provides  data at both design-time and run-time.
    /// </summary>
    public sealed class DataSource
    {
        private static DataSource _dataSource = new DataSource();

        private ObservableCollection<DataGroup> _groups = new ObservableCollection<DataGroup>();
        public ObservableCollection<DataGroup> Groups
        {
            get { return this._groups; }
        }

        public static async Task<IEnumerable<DataGroup>> GetGroupsAsync()
        {
            await _dataSource.GetDataAsync();

            return _dataSource.Groups;
        }

        public static async Task<DataGroup> GetGroupAsync(string uniqueId)
        {
            await _dataSource.GetDataAsync();
            // Simple linear search is acceptable for small data sets
            var matches = _dataSource.Groups.Where((group) => group.UniqueId.Equals(uniqueId));
            if (matches.Count() == 1) return matches.First();
            return null;
        }

        public static async Task<DataItem> GetItemAsync(string uniqueId)
        {
            await _dataSource.GetDataAsync();
            // Simple linear search is acceptable for small data sets
            var matches = _dataSource.Groups.SelectMany(group => group.Items).Where((item) => item.UniqueId.Equals(uniqueId));
            if (matches.Count() == 1) return matches.First();
            return null;
        }

        private async Task GetDataAsync()
        {
            if (this._groups.Count != 0)
                return;

            var items = await App.MobileService
                    .InvokeApiAsync<IEnumerable<Item>>("Item",
                    System.Net.Http.HttpMethod.Get, null);
            var categories = await App.MobileService
                    .InvokeApiAsync<IEnumerable<string>>("Categories",
                    System.Net.Http.HttpMethod.Get, null);            

            var allGroup = new DataGroup("", "Todas", "", "", "");
            this.Groups.Add(allGroup);

            foreach (var item in items)
            {
                var dataItem = new DataItem(item.Id, item.Title, item.Summary,
                    item.Links.Length > 1 ? item.Links[1].Url.ToString() : "",
                    item.Summary, item.Summary);
                allGroup.Items.Add(dataItem);

                foreach (var category in item.Categories)
                {
                    if (!categories.Contains(category)) continue;
                    var group = Groups.FirstOrDefault(x => x.UniqueId == category);
                    if (group == null)
                    {
                        group = new DataGroup(category, category, "", "", "");
                        Groups.Add(group);
                    }
                    group.Items.Add(dataItem);
                }
            }

        }
    }
}