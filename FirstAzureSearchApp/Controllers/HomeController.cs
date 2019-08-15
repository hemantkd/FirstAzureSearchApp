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
                if (model.SearchText == null)
                {
                    model.SearchText = string.Empty;
                }

                await RunQueryAsync(model);
            }
            catch
            {
                return View("Error", new ErrorViewModel {RequestId = "1"});
            }

            return View(model);
        }

        private async Task<ActionResult> RunQueryAsync(SearchData model)
        {
            InitSearch();

            var parameters = new SearchParameters
            {
                Select = new[] {"HotelName", "Description"}
            };

            model.ResultList = await _indexClient.Documents.SearchAsync<Hotel>(model.SearchText, parameters);

            return View("Index", model);
        }

        private void InitSearch()
        {
            _builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            _configuration = _builder.Build();

            string searchServiceName = _configuration["SearchServiceName"];
            string queryApiKey = _configuration["SearchServiceQueryApiKey"];

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
    }
}
