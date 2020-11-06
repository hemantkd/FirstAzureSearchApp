using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using FirstAzureSearchApp.Models;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Configuration;

namespace FirstAzureSearchApp.Controllers
{
    public class HomeController : Controller
    {
        private IConfigurationBuilder _builder;
        private IConfigurationRoot _configuration;
        private SearchServiceClient _serviceClient;
        private ISearchIndexClient _indexClient;

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> Index(SearchData model)
        {
            try
            {
                // Ensure the search string is valid.
                if (model.SearchText == null)
                {
                    model.SearchText = string.Empty;
                }

                // Make the Azure Search call.
                await RunQueryAsync(model, 0, 0);

                // Ensure temporary data is stored for the next call.
                TempData["page"] = 0;
                TempData["leftMostPage"] = 0;
                TempData["searchfor"] = model.SearchText;
            }
            catch
            {
                return View("Error", new ErrorViewModel { RequestId = "1" });
            }

            return View(model);
        }

        private async Task<ActionResult> RunQueryAsync(SearchData model, int page, int leftMostPage)
        {
            InitSearch();

            var parameters = new SearchParameters
            {
                // Enter Hotel property names into this list so only these values will be returned.
                // If Select is empty, all values will be returned, which can be inefficient.
                Select = new[] { "HotelName", "Description" },
                SearchMode = SearchMode.All,

                // Skip past results that have already been returned.
                Skip = page * GlobalVariables.ResultsPerPage,

                // Take only the next page worth of results.
                Top = GlobalVariables.ResultsPerPage,

                // Include the total number of results.
                IncludeTotalResultCount = true
            };

            // For efficiency, the search call should be asynchronous, so use SearchAsync rather than Search
            model.ResultList = await _indexClient.Documents.SearchAsync<Hotel>(model.SearchText, parameters);

            // This variable communicates the total number of pages to the view.
            model.PageCount = ((int) model.ResultList.Count + GlobalVariables.ResultsPerPage - 1) /
                              GlobalVariables.ResultsPerPage;

            // This variable communicates the page number being displayed to the view.
            model.CurrentPage = page;

            // Calculate the range of page numbers to display.
            if (page == 0)
            {
                leftMostPage = 0;
            }
            else if (page <= leftMostPage)
            {
                // Trigger a switch to a lower page range.
                leftMostPage = Math.Max(page - GlobalVariables.PageRangeDelta, 0);
            }
            else if (page >= leftMostPage + GlobalVariables.PageRangeDelta - 1)
            {
                // Trigger a switch to a higher page range.
                leftMostPage = Math.Min(page - GlobalVariables.PageRangeDelta,
                    model.PageCount - GlobalVariables.MaxPageRange);
            }

            model.LeftMostPage = leftMostPage;

            // Calculate the number of page numbers to display.\
            model.PageRange = Math.Min(model.PageCount - leftMostPage, GlobalVariables.MaxPageRange);

            return View("Index", model);
        }

        private void InitSearch()
        {
            // Create a configuration using the appsettings file.
            _builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            _configuration = _builder.Build();

            // Pull the values from the appsettings.json file.
            string searchServiceName = _configuration["SearchServiceName"];
            string queryApiKey = _configuration["SearchServiceQueryApiKey"];

            // Create a service and index client.
            _serviceClient = new SearchServiceClient(searchServiceName, new SearchCredentials(queryApiKey));
            _indexClient = _serviceClient.Indexes.GetClient("hotels");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public async Task<ActionResult> AutocompleteAndSuggest(string term)
        {
            InitSearch();

            // Setup the type-ahead search parameters.
            var ap = new AutocompleteParameters
            {
                AutocompleteMode = AutocompleteMode.OneTermWithContext,
                Top = 1
            };
            AutocompleteResult autocompleteResult = await _indexClient.Documents.AutocompleteAsync(term, "sg", ap);

            // Setup the suggest search parameters.
            var sp = new SuggestParameters
            {
                Top = 8
            };

            DocumentSuggestResult<Hotel> suggestResult =
                await _indexClient.Documents.SuggestAsync<Hotel>(term, "sg", sp);

            var results = new List<string>();

            if (autocompleteResult.Results.Count > 0)
            {
                // Add the top result for type-ahead
                results.Add(autocompleteResult.Results[0].Text);
            }
            else
            {
                // There were no type-ahead suggestions, so add an empty string.
                results.Add(string.Empty);
            }

            foreach (var result in suggestResult.Results)
            {
                results.Add(result.Text);
            }

            return new JsonResult(results);
        }

        public async Task<ActionResult> AutoComplete(string term)
        {
            InitSearch();

            // Setup the autocomplete parameters.
            var ap = new AutocompleteParameters
            {
                AutocompleteMode = AutocompleteMode.OneTermWithContext,
                Top = 6
            };

            AutocompleteResult autocompleteResult = await _indexClient.Documents.AutocompleteAsync(term, "sg", ap);

            // Convert the results to a list that can be displayed in the client.
            List<string> autocomplete = autocompleteResult.Results.Select(x => x.Text).ToList();

            // Return the list.
            return new JsonResult(autocomplete);
        }

        public async Task<ActionResult> Suggest(bool highlights, bool fuzzy, string term)
        {
            InitSearch();

            // Setup the suggest parameters.
            var parameters = new SuggestParameters
            {
                UseFuzzyMatching = fuzzy,
                Top = 8
            };

            if (highlights)
            {
                parameters.HighlightPreTag = "<b>";
                parameters.HighlightPostTag = "</b>";
            }

            // Only one suggester can be specified per index. The name of the suggester is set when the suggester is specified by other API calls.
            // The suggester for the hotel database is called "sg", and simply searches the hotel name.
            DocumentSuggestResult<Hotel> suggestResult =
                await _indexClient.Documents.SuggestAsync<Hotel>(term, "sg", parameters);

            // Convert the suggest query results to a list that can be displayed in the client.
            List<string> suggestions = suggestResult.Results.Select(x => x.Text).ToList();

            return new JsonResult(suggestions);
        }

        public async Task<IActionResult> Page(SearchData model)
        {
            try
            {
                int page;

                switch (model.Paging)
                {
                    case "prev":
                        page = (int)TempData["page"] - 1;
                        break;

                    case "next":
                        page = (int)TempData["page"] + 1;
                        break;

                    default:
                        page = int.Parse(model.Paging);
                        break;
                }

                // Recover the leftMostPage.
                int leftMostPage = (int)TempData["leftMostPage"];

                // Recover the search text and search for the data for the new page.
                model.SearchText = TempData["searchfor"].ToString();

                await RunQueryAsync(model, page, leftMostPage);

                // Ensure Temp data is stored for next call, as TempData only stored for one call.
                TempData["page"] = (object)page;
                TempData["searchfor"] = model.SearchText;
                TempData["leftMostPage"] = model.LeftMostPage;
            }
            catch
            {
                return View("Error", new ErrorViewModel { RequestId = "2" });
            }

            return View("Index", model);
        }
    }
}
