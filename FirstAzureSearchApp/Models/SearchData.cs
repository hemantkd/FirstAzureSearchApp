using Microsoft.Azure.Search.Models;

namespace FirstAzureSearchApp.Models
{
    public class SearchData
    {
        public string SearchText { get; set; }

        public int CurrentPage { get; set; }

        public int PageCount { get; set; }

        public int LeftMostPage { get; set; }

        public int PageRange { get; set; }

        public string Paging { get; set; }

        public DocumentSearchResult<Hotel> ResultList { get; set; }
    }
}