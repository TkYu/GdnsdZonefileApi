using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace GdnsdZonefileApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ZoneController : ControllerBase
    {
        private readonly ILogger<ZoneController> _logger;
        private readonly IConfiguration _configuration;
        private static readonly Regex regexZoneFile = new(@"^\$TTL\s+(?<ttl>\d+).*?@\s+(IN)?\s+SOA\s+(?<name>.*?)\s+(?<admin>.*?)\s+\(.*?(?<serial>\d+).*?(?<refresh>\d+[hHmMdDwW]?).*?(?<retry>\d+[hHmMdDwW]?).*?(?<expire>\d+[hHmMdDwW]?).*?(?<minimum>\d+[hHmMdDwW]?).*?\)(?<content>.*?)$", RegexOptions.Singleline);
        public ZoneController(ILogger<ZoneController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// list all zones
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult Get()
        {
            if (HttpContext.Request.Headers["X-Auth-Key"] != _configuration["Key"]) return Unauthorized();
            return Ok(Directory.GetFiles(_configuration["ZoneFolder"]).Select(Path.GetFileName));
        }

        [HttpGet("{zone}")]
        public async Task<IActionResult> GetContentAsync(string zone)
        {
            if (HttpContext.Request.Headers["X-Auth-Key"] != _configuration["Key"]) return Unauthorized();
            var zoneFile = Path.Combine(_configuration["ZoneFolder"], zone);
            if (!System.IO.File.Exists(zoneFile)) return NotFound();
            return Ok(await System.IO.File.ReadAllTextAsync(zoneFile));
        }

        [HttpGet("{zone}/ttl")]
        public async Task<IActionResult> GetTTLAsync(string zone)
        {
            if (HttpContext.Request.Headers["X-Auth-Key"] != _configuration["Key"]) return Unauthorized();
            var zoneFile = Path.Combine(_configuration["ZoneFolder"], zone);
            if (!System.IO.File.Exists(zoneFile)) return NotFound();
            var zoneFileContent = await System.IO.File.ReadAllTextAsync(zoneFile);
            var match = regexZoneFile.Match(zoneFileContent);
            if (!match.Success) return null;
            return Ok(match.Groups["ttl"].Value);
        }

        [HttpGet("{zone}/soa")]
        public async Task<IActionResult> GetSoaAsync(string zone)
        {
            if (HttpContext.Request.Headers["X-Auth-Key"] != _configuration["Key"]) return Unauthorized();
            var zoneFile = Path.Combine(_configuration["ZoneFolder"], zone);
            if (!System.IO.File.Exists(zoneFile)) return NotFound();
            var zoneFileContent = await System.IO.File.ReadAllTextAsync(zoneFile);
            var match = regexZoneFile.Match(zoneFileContent);
            if (!match.Success) return NotFound();
            return Ok(new Soa
            {
                PrimaryNameServer = match.Groups["name"].Value,
                HostmasterEmail = match.Groups["admin"].Value,
                SerialNumber = uint.Parse(match.Groups["serial"].Value),
                TimeToRefresh = match.Groups["refresh"].Value,
                TimeToRetry = match.Groups["retry"].Value,
                TimeToExpire = match.Groups["expire"].Value,
                MinimumTTL = match.Groups["minimum"].Value
            });
        }

        [HttpGet("{zone}/nextid")]
        public async Task<IActionResult> GetNextSerialNumberAsync(string zone)
        {
            if (HttpContext.Request.Headers["X-Auth-Key"] != _configuration["Key"]) return Unauthorized();
            var zoneFile = Path.Combine(_configuration["ZoneFolder"], zone);
            if (!System.IO.File.Exists(zoneFile)) return NotFound();
            var zoneFileContent = await System.IO.File.ReadAllTextAsync(zoneFile);
            var match = regexZoneFile.Match(zoneFileContent);
            if (!match.Success) return NotFound();
            return Ok(uint.Parse(match.Groups["serial"].Value)+1);
        }

        [HttpGet("{zone}/record")]
        public async Task<IActionResult> GetRecordAsync(string zone,[FromQuery] string type = null,[FromQuery] string label = null)
        {
            if (HttpContext.Request.Headers["X-Auth-Key"] != _configuration["Key"]) return Unauthorized();
            var lst = new List<Record>();
            var zoneFile = Path.Combine(_configuration["ZoneFolder"], zone);
            if (!System.IO.File.Exists(zoneFile)) return NotFound();
            var zoneFileContent = await System.IO.File.ReadAllTextAsync(zoneFile);
            var match = regexZoneFile.Match(zoneFileContent);
            if (!match.Success) return NotFound();
            var lines = match.Groups["content"].Value.Split('\n').Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
            foreach (var record in lines)
                if (Record.TryParse(record, out var rec)) lst.Add(rec);
            if (type != null && Enum.TryParse<RecordType>(type, out var pType))
                lst = lst.Where(c => c.RecordType == pType).ToList();
            if (label != null && lst.Any(c=>c.HostLabel == label))
                lst = lst.Where(c => c.HostLabel == label).ToList();
            return Ok(lst);
        }


        [HttpPost("{zone}/record")]
        public async Task<IActionResult> AddOrUpdateRecordAsync(string zone,[FromBody]Record record, [FromQuery] string submit = null)
        {
            if (HttpContext.Request.Headers["X-Auth-Key"] != _configuration["Key"]) return Unauthorized();
            if (record.Serial == null) return BadRequest();
            var zoneFile = Path.Combine(_configuration["ZoneFolder"], zone);
            if (!System.IO.File.Exists(zoneFile)) return NotFound();
            var stageFile = Path.Combine(Path.GetTempPath(), $"GdnsdZone_{zone}_{record.Serial}.txt");
            var zoneFileContent = System.IO.File.Exists(stageFile) ? await System.IO.File.ReadAllTextAsync(stageFile) : await System.IO.File.ReadAllTextAsync(zoneFile);
            var match = regexZoneFile.Match(zoneFileContent);
            if (!match.Success) return StatusCode(500);
            var lines = match.Groups["content"].Value.Split('\n').Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
            var lst = new List<Record>();
            foreach (var line in lines)
                if (Record.TryParse(line, out var rec)) lst.Add(rec);
            string newContent;
            if (lst.Any(c => c.HostLabel == record.HostLabel && c.RecordType == record.RecordType && c.RecordData == record.RecordData))
            {
                newContent = Regex.Replace(zoneFileContent.Remove(match.Groups["serial"].Index, match.Groups["serial"].Length).Insert(match.Groups["serial"].Index, record.Serial.Value.ToString()), $@"{record.HostLabel}\s+[\d/]{{0,10}}\s+{record.RecordType:G}\s+{record.RecordData}", record.ToString());
                if (zoneFileContent == newContent) return StatusCode(304);
            }
            else
            {
                newContent = $"{zoneFileContent.Remove(match.Groups["serial"].Index, match.Groups["serial"].Length).Insert(match.Groups["serial"].Index, record.Serial.Value.ToString())}{record}\n";
            }

            if (string.IsNullOrEmpty(submit))
            {
                await System.IO.File.WriteAllTextAsync(stageFile, newContent);
                return NoContent();
            }

            var save = await SaveAndReloadAsync(zoneFile, newContent);
            return save == null ? StatusCode(500) : Ok(save);
        }

        [HttpPost("{zone}/record/delete")]
        public async Task<IActionResult> DeleteRecordAsync(string zone, [FromBody] Record record,[FromQuery] string submit = null)
        {
            if (HttpContext.Request.Headers["X-Auth-Key"] != _configuration["Key"]) return Unauthorized();
            if (record.Serial == null) return BadRequest();
            var zoneFile = Path.Combine(_configuration["ZoneFolder"], zone);
            if (!System.IO.File.Exists(zoneFile)) return NotFound();
            var stageFile = Path.Combine(Path.GetTempPath(), $"GdnsdZone_{zone}_{record.Serial}.txt");
            var zoneFileContent = System.IO.File.Exists(stageFile) ? await System.IO.File.ReadAllTextAsync(stageFile) : await System.IO.File.ReadAllTextAsync(zoneFile);
            var match = regexZoneFile.Match(zoneFileContent);
            if (!match.Success) return StatusCode(500);
            var lines = match.Groups["content"].Value.Split('\n').Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
            var lst = new List<Record>();
            foreach (var line in lines)
                if (Record.TryParse(line, out var rec)) lst.Add(rec);
            if (lst.Any(c => c.HostLabel == record.HostLabel && c.RecordType == record.RecordType && c.RecordData == record.RecordData))
            {
                var newContent = Regex.Replace(zoneFileContent.Remove(match.Groups["serial"].Index, match.Groups["serial"].Length).Insert(match.Groups["serial"].Index, record.Serial.Value.ToString()), $@"{record.HostLabel}\s+[\d/]{{0,10}}\s+{record.RecordType:G}\s+{record.RecordData}\n", "");
                if (zoneFileContent == newContent) return StatusCode(304);
                if (string.IsNullOrEmpty(submit))
                {
                    await System.IO.File.WriteAllTextAsync(stageFile, newContent);
                    return NoContent();
                }
                var save = await SaveAndReloadAsync(zoneFile, newContent);
                return save == null ? StatusCode(500) : Ok(save);
            }
            return StatusCode(304);
        }

        [HttpPost("{zone}/record/submit/{serial}")]
        public async Task<IActionResult> CommitAsync(string zone, string serial)
        {
            if (HttpContext.Request.Headers["X-Auth-Key"] != _configuration["Key"]) return Unauthorized();
            if (string.IsNullOrEmpty(serial)) return BadRequest();
            var zoneFile = Path.Combine(_configuration["ZoneFolder"], zone);
            if (!System.IO.File.Exists(zoneFile)) return NotFound();
            var stageFile = Path.Combine(Path.GetTempPath(), $"GdnsdZone_{zone}_{serial}.txt");
            if (!System.IO.File.Exists(stageFile)) return NotFound();

            //move
            var oldContent = await System.IO.File.ReadAllTextAsync(zoneFile);
            System.IO.File.Delete(zoneFile);
            System.IO.File.Move(stageFile, zoneFile);

            //test and reload
            if (RunCommand(_configuration["CheckCommand"], out var testResult) == 0 && RunCommand(_configuration["ReloadCommand"], out var reloadResult) == 0) return Ok($"{testResult}{reloadResult}");
            //rollback
            System.IO.File.Delete(zoneFile);
            await System.IO.File.WriteAllTextAsync(zoneFile, oldContent);
            return StatusCode(500);
        }

        private async Task<string> SaveAndReloadAsync(string zoneFile,string newContent)
        {
            var oldContent = await System.IO.File.ReadAllTextAsync(zoneFile);
            System.IO.File.Delete(zoneFile);
            await System.IO.File.WriteAllTextAsync(zoneFile, newContent);
            try
            {
                if (RunCommand(_configuration["CheckCommand"], out var testResult) == 0 && RunCommand(_configuration["ReloadCommand"], out var reloadResult) == 0) return $"{testResult}{reloadResult}";
            }
            catch (Exception e)
            {
                //ignore
                Console.WriteLine(e);
            }
            System.IO.File.Delete(zoneFile);
            await System.IO.File.WriteAllTextAsync(zoneFile, oldContent);
            return null;
        }

        private int RunCommand(string command, out string result)
        {
            var spl = command.Split(' ');
            var args = spl.Length > 1 ? string.Join(" ", spl.Skip(1)) : string.Empty;
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = spl[0],
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            result = string.IsNullOrEmpty(error) ? output : error;
            return process.ExitCode;
        }

    }
}
