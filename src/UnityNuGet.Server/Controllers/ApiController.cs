using System.IO;
using Microsoft.AspNetCore.Mvc;
using UnityNuGet.Npm;

namespace UnityNuGet.Server.Controllers
{
    /// <summary>
    /// Main entry to emulate the following NPM endpoints:
    ///
    ///  - "-/all": query all packages (return json)
    ///  - "{packageId}": query a specific package (return json)
    ///  - "{package_id}/-/{package_file}": download a specific package
    /// </summary>
    [Route("")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly RegistryCacheSingleton _cacheSingleton;

        public ApiController(RegistryCacheSingleton cacheSingleton)
        {
            _cacheSingleton = cacheSingleton;
        }

        // GET /
        [HttpGet("")]
        public IActionResult Home()
        {
            return Ok();
        }

        // GET -/all
        [HttpGet("-/all")]
        public JsonResult GetAll()
        {
            if (!TryGetInstance(out var instance, out var error)) return new JsonResult(error);

            var result = instance.All();
            return new JsonResult(result);
        }

        // GET {packageId}
        [HttpGet("{id}")]
        public JsonResult GetPackage(string id)
        {
            if (!TryGetInstance(out var instance, out var error)) return new JsonResult(error);

            var package = instance.GetPackage(id);
            if (package == null)
            {
                return new JsonResult(NpmError.NotFound);
            }

            return new JsonResult(package);
        }

        // GET {package_id}/-/{package_file}
        [HttpGet("{id}/-/{file}")]
        [HttpHead("{id}/-/{file}")]
        public IActionResult DownloadPackage(string id, string file)
        {
            if (!TryGetInstance(out var instance, out var error)) return new JsonResult(error);

            var package = instance.GetPackage(id);
            if (package == null)
            {
                return new JsonResult(NpmError.NotFound);
            }

            if (!file.StartsWith(id + "-") || !file.EndsWith(".tgz"))
            {
                return new JsonResult(NpmError.NotFound);
            }

            var filePath = instance.GetPackageFilePath(file);
            if (!System.IO.File.Exists(filePath))
            {
                return new JsonResult(NpmError.NotFound);
            }

            // This method can be called with HEAD request, so in that case we just calculate the content length
            if (Request.Method.Equals("HEAD"))
            {
                Response.ContentType = "application/octet-stream";
                Response.ContentLength = new FileInfo(filePath).Length;
                return Ok();
            }
            else
            {
                return new PhysicalFileResult(filePath, "application/octet-stream") { FileDownloadName = file };
            }
        }

        private bool TryGetInstance(out RegistryCache cacheInstance, out NpmError npmError)
        {
            var instance = _cacheSingleton.Instance;
            cacheInstance = instance;
            var currentIndex = _cacheSingleton.ProgressPackageIndex;
            var totalCount = _cacheSingleton.ProgressTotalPackageCount;
            npmError = instance == null ? new NpmError("not_initialized", $"The server is initializing ({(totalCount != 0 ? (double)currentIndex * 100 / totalCount : 0):F1}% completed). Please retry later...") : null;

            return instance != null;
        }
    }
}
