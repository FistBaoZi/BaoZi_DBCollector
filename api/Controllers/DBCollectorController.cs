using Microsoft.AspNetCore.Mvc;
using dbcollector_api.Services;
using dbcollector_api.Models;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Authorization;

namespace dbcollector_api.Controllers
{
    [Authorize]
    public class DBCollectorController : Controller
    {
        private readonly DBCollectorService _service;

        public DBCollectorController(DBCollectorService service)
        {
            _service = service;
        }

        public  List<StationConfig> List()
        {
            return  _service.GetStationList();
        }

        public async Task Add(StationConfig station)
        {
            await _service.AddStationAsync(station);
        }

        public async Task Update(StationConfig station)
        {
            await _service.UpdateStationAsync(station);
        }

        public async Task Delete(int id)
        {
            await _service.DeleteStationAsync(id);
        }

        public async Task<IActionResult> GetTableList(string stationId)
        {
            try
            {
                var result = await _service.GetTableListAsync(stationId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        public async Task SaveCollectConfig(string stationId, List<string> tables)
        {
            await _service.SaveCollectConfigAsync(stationId, tables);
        }

        public async Task<IActionResult> BatchSaveCollectConfig(List<string> stationIds, List<string> tables)
        {
            try
            {
                await _service.BatchSaveCollectConfigAsync(stationIds, tables);
                return Ok(new { message = "批量添加采集配置成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        public async Task<IActionResult> GetCollectorLogs(string? stationId = null, string? tableName = null, string? logLevel = null, int page = 1, int pageSize = 20)
        {
            try
            {
                var result = await _service.GetCollectorLogsAsync(stationId, tableName, logLevel, page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
