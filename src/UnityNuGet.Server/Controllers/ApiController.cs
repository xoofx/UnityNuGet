using System.IO;
using System.Linq;
using System.Text;
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
    public class ApiController(RegistryCacheSingleton cacheSingleton, RegistryCacheReport registryCacheReport) : ControllerBase
    {
        private readonly RegistryCacheSingleton _cacheSingleton = cacheSingleton;
        private readonly RegistryCacheReport _registryCacheReport = registryCacheReport;

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

            var result = instance?.All();
            return new JsonResult(result);
        }

        // GET {packageId}
        [HttpGet("{id}")]
        public JsonResult GetPackage(string id)
        {
            if (!TryGetInstance(out var instance, out var error)) return new JsonResult(error);

            var package = instance?.GetPackage(id);
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

            var package = instance?.GetPackage(id);
            if (package == null)
            {
                return new JsonResult(NpmError.NotFound);
            }

            if (!file.StartsWith(id + "-") || !file.EndsWith(".tgz"))
            {
                return new JsonResult(NpmError.NotFound);
            }

            var filePath = instance?.GetPackageFilePath(file);
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
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

        private bool TryGetInstance(out RegistryCache? cacheInstance, out NpmError? npmError)
        {
            var instance = _cacheSingleton.Instance;
            cacheInstance = instance;

            if (instance == null)
            {
                if (_registryCacheReport.ErrorMessages.Any())
                {
                    var stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine("Error initializing the server:");

                    foreach (var error in _registryCacheReport.ErrorMessages)
                    {
                        stringBuilder.AppendLine(error);
                    }

                    npmError = new NpmError("not_initialized", stringBuilder.ToString());
                }
                else
                {
                    npmError = new NpmError("not_initialized", $"The server is initializing ({_registryCacheReport.Progress:F1}% completed). Please retry later...");
                }
            }
            else
            {
                npmError = null;
            }

            return instance != null;
        }
    }
}
