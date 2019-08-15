using Microsoft.Azure.Search.Models;

namespace FirstAzureSearchApp.Models
{
    public class SearchData
    {
        public string SearchText { get; set; }

        public DocumentSearchResult<Hotel> ResultList { get; set; }
    }
}