using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyApp.Models;
using Microsoft.AspNetCore.Authorization;
using ServiceStack;
using ServiceStack.Mvc;

namespace MyApp.Controllers
{
    public class HomeController : ServiceStackController
    {
        public IActionResult Index()
        {
            return View(SessionAs<CustomUserSession>());
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [Authorize]
        public IActionResult RequiresAuth()
        {
            return View();
        }

        [Authorize(Roles = "Manager")]
        public IActionResult RequiresRole()
        {
            return View();
        }

        [Authorize(Roles = "Admin")]
        public IActionResult RequiresAdmin()
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
